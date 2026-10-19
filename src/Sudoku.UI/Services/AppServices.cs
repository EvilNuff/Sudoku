namespace Sudoku.UI.Services;

/// <summary>In-app dialogs (rendered as overlays, so they work the same on desktop and iOS).</summary>
public interface IDialogService
{
    Task AlertAsync(string title, string message, string accept = "OK");
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}

public interface INavigator
{
    void ShowMenu();
    void ShowGame();
    void ShowStats();
    void ShowSettings();
}

/// <summary>Where save files live: per-user local app data on desktop, the app's Library folder on iOS.</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sudoku");

    public static string GameFile => Path.Combine(DataDirectory, "current-game.json");
    public static string StatsFile => Path.Combine(DataDirectory, "stats.json");
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}
