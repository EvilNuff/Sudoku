using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sudoku.Core.Game;
using Sudoku.Core.Model;
using Sudoku.Core.Stats;
using Sudoku.UI.Services;

namespace Sudoku.UI.ViewModels;

public sealed record StatsRow(string Difficulty, string Started, string Completed, string Best, string BestNoHints, string Average)
{
    public static StatsRow From(Difficulty difficulty, DifficultyStats s) => new(
        difficulty.ToString(),
        s.GamesStarted.ToString(System.Globalization.CultureInfo.CurrentCulture),
        s.GamesCompleted.ToString(System.Globalization.CultureInfo.CurrentCulture),
        TimeFormat.OrDash(s.BestTime),
        TimeFormat.OrDash(s.BestTimeNoHints),
        TimeFormat.OrDash(s.AverageTime));
}

public sealed partial class StatsViewModel(GameService games, INavigator navigator, IDialogService dialogs) : PageViewModel
{
    public ObservableCollection<StatsRow> Rows { get; } = [];

    public override void OnActivated() => _ = LoadAsync();

    private async Task LoadAsync()
    {
        var stats = await games.GetStatsAsync();
        Rows.Clear();
        foreach (var (difficulty, s) in stats.All.OrderBy(kv => kv.Key))
            Rows.Add(StatsRow.From(difficulty, s));
    }

    [RelayCommand]
    private async Task Reset()
    {
        if (!await dialogs.ConfirmAsync("Reset statistics?", "All personal bests and totals will be erased. This cannot be undone.", "Reset", "Cancel"))
            return;
        await games.ResetStatsAsync();
        await LoadAsync();
    }

    [RelayCommand]
    private void Back() => navigator.ShowMenu();
}

public sealed partial class SettingsViewModel(SettingsService settings, INavigator navigator) : PageViewModel
{
    private bool _loading;

    [ObservableProperty]
    public partial bool ShowMistakes { get; set; }

    [ObservableProperty]
    public partial bool AutoRemoveMarks { get; set; }

    [ObservableProperty]
    public partial bool HighlightSameDigits { get; set; }

    public override void OnActivated()
    {
        _loading = true;
        ShowMistakes = settings.Current.ShowMistakes;
        AutoRemoveMarks = settings.Current.AutoRemoveMarks;
        HighlightSameDigits = settings.Current.HighlightSameDigits;
        _loading = false;
    }

    partial void OnShowMistakesChanged(bool value) => Save();
    partial void OnAutoRemoveMarksChanged(bool value) => Save();
    partial void OnHighlightSameDigitsChanged(bool value) => Save();

    private void Save()
    {
        if (!_loading)
            _ = settings.UpdateAsync(new GameSettings(ShowMistakes, AutoRemoveMarks, HighlightSameDigits));
    }

    [RelayCommand]
    private void Back() => navigator.ShowMenu();
}
