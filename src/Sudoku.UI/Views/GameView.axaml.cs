using Avalonia;
using Avalonia.Controls;

namespace Sudoku.UI.Views;

public partial class GameView : UserControl
{
    private const double WideLayoutMinWidth = 760;
    private const double ControlsWidth = 360;
    private bool? _isWide;

    public GameView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width >= WideLayoutMinWidth);
    }

    /// <summary>Wide: board left, controls right. Narrow (phones, small windows): board above controls.</summary>
    private void ApplyLayout(bool wide)
    {
        if (_isWide == wide)
            return;
        _isWide = wide;

        Root.ColumnDefinitions = wide ? new ColumnDefinitions($"*,{ControlsWidth}") : new ColumnDefinitions("*");
        Root.RowDefinitions = wide ? new RowDefinitions("*") : new RowDefinitions("*,Auto");
        Grid.SetColumn(ControlsPanel, wide ? 1 : 0);
        Grid.SetRow(ControlsPanel, wide ? 0 : 1);
        ControlsPanel.Margin = wide ? new Thickness(24, 0, 0, 0) : new Thickness(0, 16, 0, 0);
        ControlsPanel.VerticalAlignment = wide ? Avalonia.Layout.VerticalAlignment.Center : Avalonia.Layout.VerticalAlignment.Top;
        // Keyboard help only matters where there is a keyboard and room for it.
        KeyHelp.IsVisible = wide;
    }
}
