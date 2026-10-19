using Sudoku.Core.Game;
using Sudoku.Core.Persistence;

namespace Sudoku.UI.Services;

/// <summary>Holds the current <see cref="GameSettings"/>, persists changes and announces them.</summary>
public sealed class SettingsService
{
    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store)
    {
        _store = store;
        Current = store.Load();
    }

    public event EventHandler<GameSettings>? Changed;

    public GameSettings Current { get; private set; }

    public async Task UpdateAsync(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings == Current)
            return;

        Current = settings;
        Changed?.Invoke(this, settings);
        try
        {
            await _store.SaveAsync(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The new settings still apply for this run; they just won't survive a restart.
            System.Diagnostics.Trace.TraceWarning($"Could not save settings: {ex.Message}");
        }
    }
}
