using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sudoku.Core.Generation;
using Sudoku.Core.Model;
using Sudoku.UI.Services;

namespace Sudoku.UI.ViewModels;

public sealed partial class MenuViewModel(GameService games, INavigator navigator, IDialogService dialogs) : PageViewModel
{
    private CancellationTokenSource? _generation;

    [ObservableProperty]
    public partial bool HasSavedGame { get; private set; }

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string BusyText { get; private set; } = "";

    public override void OnActivated() => _ = RefreshAsync();

    private async Task RefreshAsync() => HasSavedGame = await games.HasResumableGameAsync();

    [RelayCommand]
    private async Task NewGame(Difficulty difficulty)
    {
        if (IsBusy)
            return;
        if (HasSavedGame && !await dialogs.ConfirmAsync(
                "Start a new game?", "Your game in progress will be replaced.", "New game", "Cancel"))
            return;

        _generation = new CancellationTokenSource();
        IsBusy = true;
        BusyText = $"Creating a {difficulty.ToString().ToLowerInvariant()} puzzle…";
        try
        {
            await games.StartNewAsync(difficulty, _generation.Token);
            navigator.ShowGame();
        }
        catch (OperationCanceledException)
        {
            // The player cancelled; stay on the menu.
        }
        catch (PuzzleGenerationException ex)
        {
            await dialogs.AlertAsync("Could not create a puzzle", ex.Message);
        }
        finally
        {
            IsBusy = false;
            _generation.Dispose();
            _generation = null;
        }
    }

    [RelayCommand]
    private void CancelGeneration() => _generation?.Cancel();

    [RelayCommand]
    private async Task Resume()
    {
        if (await games.ResumeAsync() is null)
        {
            HasSavedGame = false;
            await dialogs.AlertAsync("Nothing to resume", "The saved game could not be found.");
            return;
        }
        navigator.ShowGame();
    }

    [RelayCommand]
    private void ShowStats() => navigator.ShowStats();

    [RelayCommand]
    private void ShowSettings() => navigator.ShowSettings();
}
