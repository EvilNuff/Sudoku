using System.Text.Json;
using System.Text.Json.Serialization;
using Sudoku.Core.Game;
using Sudoku.Core.Model;
using Sudoku.Core.Stats;

namespace Sudoku.Core.Persistence;

/// <summary>Serialized form of an in-progress game.</summary>
public sealed record SavedGame(
    string Givens,
    string Solution,
    Difficulty Difficulty,
    string Board,
    IReadOnlyList<string> UndoBoards,
    IReadOnlyList<string> RedoBoards,
    TimeSpan Elapsed,
    int HintsUsed,
    int Mistakes)
{
    public static SavedGame From(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SavedGame(
            session.Puzzle.Givens,
            session.Solution.ToString(),
            session.Difficulty,
            BoardCodec.Encode(session.Board),
            [.. session.History.UndoSnapshots.Select(BoardCodec.Encode)],
            [.. session.History.RedoSnapshots.Select(BoardCodec.Encode)],
            session.Elapsed,
            session.HintsUsed,
            session.Mistakes);
    }

    public GameSession ToSession(TimeProvider time, GameSettings settings)
    {
        var puzzle = new Puzzle(Givens, Model.Solution.Parse(Solution), Difficulty);
        var history = new UndoHistory(
            UndoBoards.Select(b => BoardCodec.Decode(b, Givens)),
            RedoBoards.Select(b => BoardCodec.Decode(b, Givens)));
        return GameSession.Restore(puzzle, BoardCodec.Decode(Board, Givens), history, Elapsed, HintsUsed, Mistakes, time, settings);
    }
}

public interface IGameStore
{
    /// <summary>Returns null when there is no saved game or the file is unreadable.</summary>
    Task<SavedGame?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SavedGame game, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface ISettingsStore
{
    /// <summary>Synchronous so settings are available before the first frame; falls back to defaults.</summary>
    GameSettings Load();
    Task SaveAsync(GameSettings settings, CancellationToken cancellationToken = default);
}

public interface IStatsStore
{
    Task<StatsBook> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StatsBook stats, CancellationToken cancellationToken = default);
}

[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SavedGame))]
[JsonSerializable(typeof(Dictionary<Difficulty, DifficultyStats>))]
[JsonSerializable(typeof(GameSettings))]
internal sealed partial class SudokuJsonContext : JsonSerializerContext;

/// <summary>
/// Reads and writes one JSON file. Writes go to a temp file first and are then moved into place,
/// so a crash mid-write never leaves a truncated file. Calls are serialized.
/// </summary>
internal sealed class JsonFile<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) where T : class
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T?> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
                return null;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null; // corrupt or from an incompatible version; treat as absent
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteAsync(T value, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temp = path + ".tmp";
            await using (var stream = File.Create(temp))
                await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            File.Delete(path);
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed class JsonGameStore(string path) : IGameStore
{
    private readonly JsonFile<SavedGame> _file = new(path, SudokuJsonContext.Default.SavedGame);

    public async Task<SavedGame?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var game = await _file.ReadAsync(cancellationToken).ConfigureAwait(false);
        return game is not null && IsUsable(game) ? game : null;
    }

    public Task SaveAsync(SavedGame game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        return _file.WriteAsync(game, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) => _file.DeleteAsync(cancellationToken);

    /// <summary>Validates by actually decoding, so a damaged file is ignored instead of crashing on resume.</summary>
    private static bool IsUsable(SavedGame game)
    {
        try
        {
            game.ToSession(TimeProvider.System, GameSettings.Default);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return false;
        }
    }
}

public sealed class JsonStatsStore(string path) : IStatsStore
{
    private readonly JsonFile<Dictionary<Difficulty, DifficultyStats>> _file =
        new(path, SudokuJsonContext.Default.DictionaryDifficultyDifficultyStats);

    public async Task<StatsBook> LoadAsync(CancellationToken cancellationToken = default) =>
        new(await _file.ReadAsync(cancellationToken).ConfigureAwait(false));

    public Task SaveAsync(StatsBook stats, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return _file.WriteAsync(stats.All.ToDictionary(), cancellationToken);
    }
}

public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private readonly JsonFile<GameSettings> _file = new(path, SudokuJsonContext.Default.GameSettings);

    public GameSettings Load()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), SudokuJsonContext.Default.GameSettings) ?? GameSettings.Default
                : GameSettings.Default;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return GameSettings.Default;
        }
    }

    public Task SaveAsync(GameSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _file.WriteAsync(settings, cancellationToken);
    }
}
