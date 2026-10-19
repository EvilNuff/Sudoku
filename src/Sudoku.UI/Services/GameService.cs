using System.Diagnostics;
using Sudoku.Core.Game;
using Sudoku.Core.Generation;
using Sudoku.Core.Model;
using Sudoku.Core.Persistence;
using Sudoku.Core.Stats;

namespace Sudoku.UI.Services;

/// <summary>
/// Owns the current game and its persistence: starting, resuming, autosaving, suspending and
/// recording completions. All members are expected to be called on the UI thread.
/// </summary>
public sealed class GameService
{
    private readonly PuzzlePool _pool;
    private readonly IGameStore _gameStore;
    private readonly IStatsStore _statsStore;
    private readonly SettingsService _settings;
    private readonly TimeProvider _time;

    private StatsBook? _stats;
    private GameSession? _recordedSession;
    private bool _saveRequested;
    private bool _saving;

    public GameService(PuzzlePool pool, IGameStore gameStore, IStatsStore statsStore, SettingsService settings, TimeProvider time)
    {
        _pool = pool;
        _gameStore = gameStore;
        _statsStore = statsStore;
        _settings = settings;
        _time = time;
        _settings.Changed += (_, s) =>
        {
            if (Current is not null)
                Current.Settings = s;
        };
    }

    /// <summary>Raised when the app is minimized or backgrounded.</summary>
    public event EventHandler? Suspended;

    public GameSession? Current { get; private set; }

    public async Task<bool> HasResumableGameAsync() =>
        Current is { IsCompleted: false } || await LoadSavedAsync() is not null;

    public async Task<GameSession> StartNewAsync(Difficulty difficulty, CancellationToken cancellationToken)
    {
        var puzzle = await _pool.TakeAsync(difficulty, cancellationToken);
        Current = new GameSession(puzzle, _time, _settings.Current);

        var stats = await GetStatsAsync();
        stats.RecordStart(difficulty);
        await SaveStatsAsync(stats);
        RequestSave();
        return Current;
    }

    /// <summary>Returns the in-memory game, or loads the saved one. Null when there is nothing to resume.</summary>
    public async Task<GameSession?> ResumeAsync()
    {
        if (Current is { IsCompleted: false })
            return Current;

        var saved = await LoadSavedAsync();
        Current = saved?.ToSession(_time, _settings.Current);
        return Current;
    }

    /// <summary>Saves the current game in the background, coalescing bursts of moves into one write.</summary>
    public void RequestSave()
    {
        _saveRequested = true;
        if (!_saving)
            _ = SaveLoopAsync();
    }

    /// <summary>Blocking save for shutdown or backgrounding, when there is no time for a background write.</summary>
    public void SaveNow()
    {
        if (Current is not { IsCompleted: false } session)
            return;
        session.Pause();
        try
        {
            // The store uses ConfigureAwait(false) throughout, so blocking here cannot deadlock the UI thread.
            _gameStore.SaveAsync(SavedGame.From(session)).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError($"Final save failed: {ex.Message}");
        }
    }

    /// <summary>Pauses and saves synchronously: on iOS the app may be frozen right after backgrounding.</summary>
    public void Suspend()
    {
        if (Current is null)
            return;
        SaveNow();
        Suspended?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Records a finished game once and clears the save slot.</summary>
    public async Task<CompletionResult?> RecordCompletionAsync(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsCompleted || ReferenceEquals(session, _recordedSession))
            return null;
        _recordedSession = session;

        var stats = await GetStatsAsync();
        var result = stats.RecordCompletion(session.Difficulty, session.Elapsed, session.HintsUsed);
        await SaveStatsAsync(stats);
        try
        {
            await _gameStore.ClearAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Could not clear the saved game: {ex.Message}");
        }
        return result;
    }

    public async Task<StatsBook> GetStatsAsync()
    {
        if (_stats is null)
        {
            try
            {
                _stats = await _statsStore.LoadAsync();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceError($"Could not read stats; starting fresh: {ex.Message}");
                _stats = new StatsBook();
            }
        }
        return _stats;
    }

    public async Task ResetStatsAsync()
    {
        var stats = await GetStatsAsync();
        stats.Reset();
        await SaveStatsAsync(stats);
    }

    private async Task<SavedGame?> LoadSavedAsync()
    {
        try
        {
            return await _gameStore.LoadAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError($"Could not read the saved game: {ex.Message}");
            return null;
        }
    }

    private async Task SaveStatsAsync(StatsBook stats)
    {
        try
        {
            await _statsStore.SaveAsync(stats);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError($"Could not save stats: {ex.Message}");
        }
    }

    private async Task SaveLoopAsync()
    {
        _saving = true;
        try
        {
            while (_saveRequested)
            {
                _saveRequested = false;
                if (Current is not { IsCompleted: false } session)
                    break;
                // Snapshot on the UI thread; only the file write happens off-thread.
                await _gameStore.SaveAsync(SavedGame.From(session));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError($"Autosave failed: {ex.Message}");
        }
        finally
        {
            _saving = false;
        }
    }
}
