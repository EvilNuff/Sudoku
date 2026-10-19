using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sudoku.UI.Input;
using Sudoku.UI.Services;

namespace Sudoku.UI.ViewModels;

/// <summary>
/// The shell: shows one page at a time and hosts modal dialogs as an overlay. Pages are long-lived
/// and re-activated on navigation.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase, INavigator, IDialogService
{
    private readonly SemaphoreSlim _dialogGate = new(1, 1);
    private PageViewModel? _menu;
    private PageViewModel? _game;
    private PageViewModel? _stats;
    private PageViewModel? _settings;

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; private set; }

    [ObservableProperty]
    public partial DialogViewModel? Dialog { get; private set; }

    public void RegisterPages(PageViewModel menu, PageViewModel game, PageViewModel stats, PageViewModel settings)
    {
        _menu = menu;
        _game = game;
        _stats = stats;
        _settings = settings;
    }

    public void ShowMenu() => Navigate(_menu);
    public void ShowGame() => Navigate(_game);
    public void ShowStats() => Navigate(_stats);
    public void ShowSettings() => Navigate(_settings);

    /// <summary>Routes a key press to the dialog or the current page. Returns true when handled.</summary>
    public bool HandleKey(GameKey? key, bool isConfirm, bool isCancel)
    {
        if (Dialog is { } dialog)
        {
            if (isConfirm)
                dialog.AcceptCommand.Execute(null);
            else if (isCancel)
                dialog.CancelCommand.Execute(null);
            return true; // modal: swallow everything else
        }
        return key is { } k && CurrentPage is GameViewModel game && game.HandleKey(k);
    }

    public async Task AlertAsync(string title, string message, string accept = "OK") =>
        await ShowDialogAsync(new DialogViewModel(title, message, accept, cancelText: null));

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        ShowDialogAsync(new DialogViewModel(title, message, accept, cancel));

    private async Task<bool> ShowDialogAsync(DialogViewModel dialog)
    {
        await _dialogGate.WaitAsync();
        try
        {
            Dialog = dialog;
            return await dialog.Result;
        }
        finally
        {
            Dialog = null;
            _dialogGate.Release();
        }
    }

    private void Navigate(PageViewModel? page)
    {
        if (page is null)
            throw new InvalidOperationException("Pages have not been registered.");
        if (ReferenceEquals(page, CurrentPage))
            return;
        CurrentPage?.OnDeactivated();
        CurrentPage = page;
        page.OnActivated();
    }
}

public sealed partial class DialogViewModel(string title, string message, string acceptText, string? cancelText) : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Title { get; } = title;
    public string Message { get; } = message;
    public string AcceptText { get; } = acceptText;
    public string? CancelText { get; } = cancelText;
    public bool HasCancel => CancelText is not null;

    public Task<bool> Result => _result.Task;

    [RelayCommand]
    private void Accept() => _result.TrySetResult(true);

    [RelayCommand]
    private void Cancel() => _result.TrySetResult(false);
}
