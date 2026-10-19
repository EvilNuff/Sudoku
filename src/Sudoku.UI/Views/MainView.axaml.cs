using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sudoku.UI.Input;
using Sudoku.UI.ViewModels;

namespace Sudoku.UI.Views;

public partial class MainView : UserControl
{
    private TopLevel? _topLevel;

    public MainView()
    {
        InitializeComponent();
    }

    // Keys are caught on the way down (tunnel) so focused buttons can't swallow game shortcuts.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel shell &&
            shell.HandleKey(KeyMap.Translate(e.Key, e.KeyModifiers), e.Key == Key.Enter, e.Key == Key.Escape))
        {
            e.Handled = true;
        }
    }
}
