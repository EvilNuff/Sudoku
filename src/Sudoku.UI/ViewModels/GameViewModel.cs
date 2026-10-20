using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sudoku.Core.Game;
using Sudoku.Core.Model;
using Sudoku.UI.Controls;
using Sudoku.UI.Input;
using Sudoku.UI.Services;

namespace Sudoku.UI.ViewModels;

public sealed partial class GameViewModel : PageViewModel
{
    private static readonly IReadOnlySet<Position> NoCells = new HashSet<Position>();

    private readonly GameService _games;
    private readonly SettingsService _settings;
    private readonly INavigator _navigator;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _clock;

    private GameSession? _session;
    private Position? _selected;
    private Hint? _hint;
    private int _hintStage;
    private int _pickedDigit;

    public GameViewModel(GameService games, SettingsService settings, INavigator navigator, IDialogService dialogs)
    {
        _games = games;
        _settings = settings;
        _navigator = navigator;
        _dialogs = dialogs;
        _clock = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => UpdateClock());
        Digits = [.. Enumerable.Range(1, 9).Select(d => new DigitButtonViewModel(d, PickNumberCommand))];
    }

    public IReadOnlyList<DigitButtonViewModel> Digits { get; }

    [ObservableProperty]
    public partial BoardRenderState? BoardState { get; private set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    [ObservableProperty]
    public partial string TimerText { get; private set; } = "0:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValueMode), nameof(IsCornerMode), nameof(IsCenterMode), nameof(ModeDescription))]
    public partial EntryMode Mode { get; private set; }

    public bool IsValueMode => Mode == EntryMode.Value;
    public bool IsCornerMode => Mode == EntryMode.CornerMark;
    public bool IsCenterMode => Mode == EntryMode.CenterMark;

    /// <summary>One line telling the player what tapping a number will do right now.</summary>
    public string ModeDescription => Mode switch
    {
        EntryMode.CornerMark => "Corner notes: pick a number, then click cells to add or remove it in their corners.",
        EntryMode.CenterMark => "Center notes: pick a number, then click cells to add or remove it as a candidate.",
        _ => "Numbers: pick a number, then click cells to place it. Click a cell again to remove it.",
    };

    // The picked number is kept when switching modes, so you can flip between notes and numbers quickly.
    partial void OnModeChanged(EntryMode value)
    {
        foreach (var digit in Digits)
            digit.Mode = value;
    }

    [ObservableProperty]
    public partial bool IsPaused { get; private set; }

    [ObservableProperty]
    public partial bool ShowMistakeCounter { get; private set; }

    [ObservableProperty]
    public partial string MistakesText { get; private set; } = "";

    [ObservableProperty]
    public partial string HintsText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    public partial string? HintTitle { get; private set; }

    [ObservableProperty]
    public partial string? HintExplanation { get; private set; }

    public bool HasHint => HintTitle is not null;

    [ObservableProperty]
    public partial string HintButtonText { get; private set; } = "Hint";

    [ObservableProperty]
    public partial bool CanUndo { get; private set; }

    [ObservableProperty]
    public partial bool CanRedo { get; private set; }

    public override void OnActivated()
    {
        if (_games.Current is not { } session)
        {
            Dispatcher.UIThread.Post(_navigator.ShowMenu);
            return;
        }

        _session = session;
        _session.BoardChanged += OnBoardChanged;
        _session.Completed += OnCompleted;
        _games.Suspended += OnSuspended;
        _settings.Changed += OnSettingsChanged;

        _selected = null;
        Mode = EntryMode.Value;
        PickDigit(0);
        IsPaused = false;
        Title = $"{session.Difficulty} puzzle";
        ClearHint();
        session.Resume();
        _clock.Start();
        Refresh();
    }

    public override void OnDeactivated()
    {
        _clock.Stop();
        _games.Suspended -= OnSuspended;
        _settings.Changed -= OnSettingsChanged;
        if (_session is null)
            return;
        _session.BoardChanged -= OnBoardChanged;
        _session.Completed -= OnCompleted;
        _session.Pause();
        _games.RequestSave();
        _session = null;
    }

    /// <summary>Returns true when the key did something.</summary>
    public bool HandleKey(GameKey key)
    {
        if (_session is null)
            return false;
        if (IsPaused && key.Action != GameKeyAction.TogglePause)
            return false;

        switch (key.Action)
        {
            case GameKeyAction.Digit:
                PickFromKeyboard(key.Digit, key.Mode);
                break;
            case GameKeyAction.Apply:
                if (_pickedDigit != 0 && _selected is { } target)
                    _session.Enter(target, _pickedDigit, Mode);
                break;
            case GameKeyAction.Dismiss:
                if (HasHint)
                    ClearHint();
                else
                    PickDigit(0);
                Refresh();
                break;
            case GameKeyAction.Erase:
                Erase();
                break;
            case GameKeyAction.Move:
                MoveSelection(key.RowDelta, key.ColDelta);
                break;
            case GameKeyAction.Undo:
                Undo();
                break;
            case GameKeyAction.Redo:
                Redo();
                break;
            case GameKeyAction.Hint:
                Hint();
                break;
            case GameKeyAction.CycleMode:
                Mode = (EntryMode)(((int)Mode + 1) % 3);
                break;
            case GameKeyAction.TogglePause:
                TogglePause();
                break;
            default:
                return false;
        }
        return true;
    }

    [RelayCommand]
    private void SelectCell(Position position)
    {
        if (IsPaused)
            return;
        _selected = position;
        // Number-first input: clicking a cell applies the picked number in the current mode.
        // A number replaces the cell's notes; notes are ignored on cells that hold a number.
        if (_pickedDigit != 0 && _session is not null && _session.Enter(position, _pickedDigit, Mode))
            return; // BoardChanged already refreshed
        Refresh();
    }

    /// <summary>Picks a number for number-first input, or un-picks it when tapped again.</summary>
    [RelayCommand]
    private void PickNumber(int digit)
    {
        if (IsPaused)
            return;
        PickDigit(_pickedDigit == digit ? 0 : digit);
        Refresh();
    }

    [RelayCommand]
    private void SetMode(EntryMode mode) => Mode = mode;

    [RelayCommand]
    private void Erase()
    {
        if (!IsPaused && _selected is { } position)
            _session?.Erase(position);
    }

    [RelayCommand]
    private void Undo()
    {
        if (!IsPaused)
            _session?.Undo();
    }

    [RelayCommand]
    private void Redo()
    {
        if (!IsPaused)
            _session?.Redo();
    }

    /// <summary>Three presses: name the technique and highlight its cells, then explain, then apply.</summary>
    [RelayCommand]
    private void Hint()
    {
        if (_session is null || IsPaused || _session.IsCompleted)
            return;

        switch (_hintStage)
        {
            case 0:
                _hint = _session.RequestHint();
                if (_hint is null)
                    return;
                _hintStage = 1;
                HintTitle = _hint.Kind == HintKind.Deduction ? $"Hint: {_hint.Title}" : _hint.Title;
                HintExplanation = "Look at the highlighted cells. Press Hint again for an explanation.";
                HintButtonText = "Explain";
                break;
            case 1:
                _hintStage = 2;
                HintExplanation = _hint!.Explanation;
                HintButtonText = "Apply";
                break;
            default:
                var hint = _hint!;
                _selected = hint.Target;
                ClearHint();
                _session.ApplyHint(hint);
                break;
        }
        Refresh();
    }

    /// <summary>Hides the current hint and its highlight without applying it.</summary>
    [RelayCommand]
    private void DismissHint()
    {
        ClearHint();
        Refresh();
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (_session is null || _session.IsCompleted)
            return;
        IsPaused = !IsPaused;
        if (IsPaused)
        {
            _session.Pause();
            _games.RequestSave();
        }
        else
        {
            _session.Resume();
        }
        Refresh();
    }

    [RelayCommand]
    private void BackToMenu() => _navigator.ShowMenu();

    private void PickDigit(int digit)
    {
        _pickedDigit = digit;
        foreach (var d in Digits)
            d.IsPicked = d.Digit == digit;
    }

    /// <summary>
    /// A number key behaves like tapping the pad. A modifier switches the entry mode first; switching
    /// mode keeps the number picked instead of toggling it off.
    /// </summary>
    private void PickFromKeyboard(int digit, EntryMode? mode)
    {
        if (mode is { } requested && requested != Mode)
        {
            Mode = requested;
            PickDigit(digit);
            Refresh();
            return;
        }
        PickNumber(digit);
    }

    private void MoveSelection(int rowDelta, int colDelta)
    {
        var current = _selected ?? new Position(4, 4);
        _selected = _selected is null
            ? current
            : new Position((current.Row + rowDelta + 9) % 9, (current.Col + colDelta + 9) % 9);
        Refresh();
    }

    private void OnBoardChanged(object? sender, EventArgs e)
    {
        // Any move can invalidate a pending hint.
        ClearHint();
        Refresh();
        _games.RequestSave();
    }

    private async void OnCompleted(object? sender, EventArgs e)
    {
        if (sender is not GameSession session)
            return;
        UpdateClock();
        Refresh();

        var result = await _games.RecordCompletionAsync(session);
        var message = $"Time: {TimeFormat.Clock(session.Elapsed)}   Hints: {session.HintsUsed}   Mistakes: {session.Mistakes}";
        if (result is { IsNewBest: true })
            message += $"\n\nNew personal best for {session.Difficulty}!";
        else if (result is { IsNewNoHintBest: true })
            message += $"\n\nNew best time without hints for {session.Difficulty}!";
        if (result is not null)
            message += $"\nBest: {TimeFormat.OrDash(result.Stats.BestTime)}   Average: {TimeFormat.OrDash(result.Stats.AverageTime)}";

        await _dialogs.AlertAsync("Solved!", message, "Back to menu");
        _navigator.ShowMenu();
    }

    private void OnSuspended(object? sender, EventArgs e)
    {
        if (_session is { IsCompleted: false } && !IsPaused)
        {
            IsPaused = true;
            Refresh();
        }
    }

    private void OnSettingsChanged(object? sender, GameSettings e) => Refresh();

    private void ClearHint()
    {
        _hint = null;
        _hintStage = 0;
        HintTitle = null;
        HintExplanation = null;
        HintButtonText = "Hint";
    }

    private void UpdateClock()
    {
        if (_session is not null)
            TimerText = TimeFormat.Clock(_session.Elapsed);
    }

    private void Refresh()
    {
        if (_session is not { } session)
            return;

        var settings = _settings.Current;
        var board = session.Board;
        // While a note digit is picked, show where it already is; otherwise follow the selected cell.
        var highlightDigit = _pickedDigit != 0 ? _pickedDigit
            : settings.HighlightSameDigits && _selected is { } s ? board[s].Value : 0;

        BoardState = new BoardRenderState(
            board,
            _selected,
            highlightDigit,
            session.Conflicts,
            settings.ShowMistakes ? session.WrongEntries : NoCells,
            session.IllegalMarks,
            _hint is null ? NoCells : _hint.HighlightCells.ToHashSet(),
            _hintStage >= 2 ? _hint?.Target : null,
            _pickedDigit != 0,
            IsPaused);

        var counts = session.DigitCounts();
        foreach (var digit in Digits)
            digit.IsComplete = counts[digit.Digit] >= 9;

        CanUndo = session.CanUndo;
        CanRedo = session.CanRedo;
        ShowMistakeCounter = settings.ShowMistakes;
        MistakesText = $"Mistakes: {session.Mistakes}";
        HintsText = $"Hints: {session.HintsUsed}";
        UpdateClock();
    }
}

public sealed partial class DigitButtonViewModel(int digit, IRelayCommand<int> enter) : ViewModelBase
{
    public int Digit { get; } = digit;
    public string Label { get; } = digit.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public IRelayCommand<int> Command { get; } = enter;

    /// <summary>All nine of this digit are on the board.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Opacity))]
    public partial bool IsComplete { get; set; }

    public double Opacity => IsComplete ? 0.35 : 1.0;

    /// <summary>The pad restyles itself in note modes so it is obvious what a tap will do.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCornerMode), nameof(IsCenterMode))]
    public partial EntryMode Mode { get; set; }

    public bool IsCornerMode => Mode == EntryMode.CornerMark;
    public bool IsCenterMode => Mode == EntryMode.CenterMark;

    /// <summary>Picked for number-first note taking.</summary>
    [ObservableProperty]
    public partial bool IsPicked { get; set; }
}
