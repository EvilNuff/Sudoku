using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Sudoku.Core.Model;

namespace Sudoku.UI.Controls;

/// <summary>
/// Draws the 9x9 grid with values, corner/center pencil marks and highlights, and reports which
/// cell was pressed. All game state comes in through <see cref="State"/>.
/// </summary>
public sealed class SudokuBoard : Control
{
    public static readonly StyledProperty<BoardRenderState?> StateProperty =
        AvaloniaProperty.Register<SudokuBoard, BoardRenderState?>(nameof(State));

    public static readonly StyledProperty<ICommand?> CellPressedCommandProperty =
        AvaloniaProperty.Register<SudokuBoard, ICommand?>(nameof(CellPressedCommand));

    private const double Inset = 2;

    // Corner marks fill the corners first, then edge midpoints (the usual "Snyder" layout).
    private static readonly (double X, double Y)[] CornerSlots =
    [
        (0.2, 0.2), (0.8, 0.2), (0.2, 0.8), (0.8, 0.8),
        (0.5, 0.2), (0.5, 0.8), (0.2, 0.5), (0.8, 0.5), (0.5, 0.5),
    ];

    static SudokuBoard()
    {
        AffectsRender<SudokuBoard>(StateProperty);
    }

    public SudokuBoard()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public BoardRenderState? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Executed with the pressed <see cref="Position"/>.</summary>
    public ICommand? CellPressedCommand
    {
        get => GetValue(CellPressedCommandProperty);
        set => SetValue(CellPressedCommandProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = (double.IsInfinity(availableSize.Width), double.IsInfinity(availableSize.Height)) switch
        {
            (false, false) => Math.Min(availableSize.Width, availableSize.Height),
            (false, true) => availableSize.Width,
            (true, false) => availableSize.Height,
            _ => 450,
        };
        return new Size(side, side);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        if (Geometry().HitTest(point.X, point.Y) is not { } position)
            return;
        if (CellPressedCommand is { } command && command.CanExecute(position))
        {
            command.Execute(position);
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        var palette = ActualThemeVariant == ThemeVariant.Dark ? BoardPalette.Dark : BoardPalette.Light;
        var g = Geometry();
        if (g.Size <= 0)
            return;

        context.FillRectangle(palette.Background, new Rect(g.Left, g.Top, g.Size, g.Size));

        if (State is { } state)
        {
            for (var row = 0; row < 9; row++)
            {
                for (var col = 0; col < 9; col++)
                {
                    var position = new Position(row, col);
                    var rect = g.CellRect(row, col);
                    var tint = state.IsContentHidden ? null : TintFor(state, position, palette);
                    if (tint is not null)
                        context.FillRectangle(tint, rect);
                    if (!state.IsContentHidden)
                        DrawCellContent(context, state, position, rect, palette);
                }
            }
        }

        DrawGridLines(context, g, palette);

        if (State is { IsContentHidden: true })
            DrawCentered(context, "Paused", g.Size * 0.08, FontWeight.SemiBold, palette.PausedText,
                new Point(g.Left + g.Size / 2, g.Top + g.Size / 2));
        else
        {
            // With a number picked, the last-clicked cell is outlined rather than filled so the picked number stands out.
            if (State is { IsDigitPicked: true, Selected: { } selected })
                context.DrawRectangle(null, palette.SelectedOutline, g.CellRect(selected.Row, selected.Col).Deflate(2));
            if (State?.HintTarget is { } target)
                context.DrawRectangle(null, palette.HintOutline, g.CellRect(target.Row, target.Col).Deflate(2));
        }
    }

    private BoardGeometry Geometry() => BoardGeometry.For(Bounds.Width, Bounds.Height, Inset);

    private static IBrush? TintFor(BoardRenderState state, Position position, BoardPalette palette)
    {
        var cell = state.Board[position];
        if (state.IsDigitPicked)
            return PickedTintFor(state, position, cell, palette);
        if (state.Selected == position)
            return palette.SelectedTint;
        if (state.Conflicts.Contains(position))
            return palette.ConflictTint;
        if (state.HintCells.Contains(position))
            return palette.HintTint;
        if (state.HighlightDigit != 0 && cell.Value == state.HighlightDigit)
            return palette.SameDigitTint;
        if (state.HighlightDigit != 0 && cell.IsEmpty &&
            (cell.CornerMarks.Contains(state.HighlightDigit) || cell.CenterMarks.Contains(state.HighlightDigit)))
            return palette.MarkTint;
        if (state.Selected is { } selected && selected.SharesUnitWith(position))
            return palette.PeerTint;
        return null;
    }

    /// <summary>
    /// Number-first mode: the picked number's cells get a strong fill, its notes a medium one, and every
    /// cell sharing a row, column or box with a placed copy gets a light tint (the number can't go there).
    /// </summary>
    private static IBrush? PickedTintFor(BoardRenderState state, Position position, Cell cell, BoardPalette palette)
    {
        var digit = state.HighlightDigit;
        if (state.Conflicts.Contains(position))
            return palette.ConflictTint;
        if (state.HintCells.Contains(position))
            return palette.HintTint;
        if (cell.Value == digit)
            return palette.SelectedTint;
        if (cell.IsEmpty && (cell.CornerMarks.Contains(digit) || cell.CenterMarks.Contains(digit)))
            return palette.SameDigitTint;
        if (Units.Peers[position.Index].Any(peer => state.Board[peer].Value == digit))
            return palette.PeerTint;
        return null;
    }

    private static void DrawCellContent(DrawingContext context, BoardRenderState state, Position position, Rect rect, BoardPalette palette)
    {
        var cell = state.Board[position];
        var size = rect.Width;

        if (!cell.IsEmpty)
        {
            var brush = cell.IsGiven
                ? palette.GivenText
                : state.Conflicts.Contains(position) || state.Mistakes.Contains(position) ? palette.ErrorText : palette.PlayerText;
            DrawCentered(context, cell.Value.ToString(CultureInfo.InvariantCulture), size * 0.6,
                cell.IsGiven ? FontWeight.SemiBold : FontWeight.Normal, brush, rect.Center);
            return;
        }

        var illegal = state.IllegalMarks.TryGetValue(position, out var bad) ? bad : DigitSet.Empty;

        var slot = 0;
        foreach (var digit in cell.CornerMarks)
        {
            var (fx, fy) = CornerSlots[slot++];
            DrawMark(context, digit, size * 0.22, new Point(rect.X + fx * size, rect.Y + fy * size), state, palette, illegal.Contains(digit));
        }

        if (!cell.CenterMarks.IsEmpty)
        {
            var count = cell.CenterMarks.Count;
            var fontSize = Math.Min(size * 0.3, size * 0.85 / (count * 0.62));
            var text = new FormattedText(string.Concat(cell.CenterMarks), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface(FontWeight.Normal), fontSize, palette.MarkText);
            var index = 0;
            foreach (var digit in cell.CenterMarks)
            {
                if (digit == state.HighlightDigit)
                {
                    text.SetForegroundBrush(palette.MarkHighlightText, index, 1);
                    text.SetFontWeight(FontWeight.Bold, index, 1);
                }
                // Illegal wins over highlight: red is the more important signal.
                if (illegal.Contains(digit))
                    text.SetForegroundBrush(palette.ErrorText, index, 1);
                index++;
            }
            context.DrawText(text, new Point(rect.Center.X - text.Width / 2, rect.Center.Y - text.Height / 2));
        }
    }

    private static void DrawMark(DrawingContext context, int digit, double fontSize, Point center, BoardRenderState state, BoardPalette palette, bool isIllegal)
    {
        var highlighted = digit == state.HighlightDigit;
        var brush = isIllegal ? palette.ErrorText : highlighted ? palette.MarkHighlightText : palette.MarkText;
        DrawCentered(context, digit.ToString(CultureInfo.InvariantCulture), fontSize,
            highlighted || isIllegal ? FontWeight.Bold : FontWeight.Normal, brush, center);
    }

    private static void DrawGridLines(DrawingContext context, BoardGeometry g, BoardPalette palette)
    {
        for (var i = 0; i <= 9; i++)
        {
            var pen = i % 3 == 0 ? palette.ThickLine : palette.ThinLine;
            var offset = i * g.CellSize;
            context.DrawLine(pen, new Point(g.Left + offset, g.Top), new Point(g.Left + offset, g.Top + g.Size));
            context.DrawLine(pen, new Point(g.Left, g.Top + offset), new Point(g.Left + g.Size, g.Top + offset));
        }
    }

    private static void DrawCentered(DrawingContext context, string text, double fontSize, FontWeight weight, IBrush brush, Point center)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface(weight), fontSize, brush);
        context.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private static Typeface Typeface(FontWeight weight) => new(FontFamily.Default, FontStyle.Normal, weight);
}
