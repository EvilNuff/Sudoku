using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Sudoku.Core.Generation;
using Sudoku.Core.Persistence;
using Sudoku.UI.Services;
using Sudoku.UI.ViewModels;
using Sudoku.UI.Views;

namespace Sudoku.UI;

public partial class App : Application
{
    private PuzzlePool? _pool;
    private GameService? _games;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var shell = Compose();

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                var window = new MainWindow { DataContext = shell };
                window.PropertyChanged += (_, e) =>
                {
                    if (e.Property == Window.WindowStateProperty && e.GetNewValue<WindowState>() == WindowState.Minimized)
                        _games!.Suspend();
                };
                desktop.MainWindow = window;
                desktop.Exit += (_, _) => Shutdown();
                break;
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => new MainView { DataContext = shell };
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new MainView { DataContext = shell };
                break;
        }

        // Mobile: pause and save when the app goes to the background.
        if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
        {
            activatable.Deactivated += (_, e) =>
            {
                if (e.Kind == ActivationKind.Background)
                    _games!.Suspend();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Composition root: builds the services and pages by hand (small app, no container needed).</summary>
    private MainViewModel Compose()
    {
        var settings = new SettingsService(new JsonSettingsStore(AppPaths.SettingsFile));
        _pool = new PuzzlePool(new PuzzleGenerator());
        _games = new GameService(
            _pool,
            new JsonGameStore(AppPaths.GameFile),
            new JsonStatsStore(AppPaths.StatsFile),
            settings,
            TimeProvider.System);

        var shell = new MainViewModel();
        shell.RegisterPages(
            new MenuViewModel(_games, shell, shell),
            new GameViewModel(_games, settings, shell, shell),
            new StatsViewModel(_games, shell, shell),
            new SettingsViewModel(settings, shell));
        shell.ShowMenu();

        _pool.WarmUp();
        return shell;
    }

    private void Shutdown()
    {
        _games?.SaveNow();
        _pool?.Dispose();
    }
}
