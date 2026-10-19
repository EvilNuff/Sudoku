using CommunityToolkit.Mvvm.ComponentModel;

namespace Sudoku.UI.ViewModels;

public abstract class ViewModelBase : ObservableObject;

/// <summary>A screen shown by the shell. The shell calls the hooks when the page is shown or hidden.</summary>
public abstract class PageViewModel : ViewModelBase
{
    public virtual void OnActivated() { }
    public virtual void OnDeactivated() { }
}

internal static class TimeFormat
{
    public static string Clock(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
        : $"{time.Minutes}:{time.Seconds:00}";

    public static string OrDash(TimeSpan? time) => time is { } t ? Clock(t) : "—";
}
