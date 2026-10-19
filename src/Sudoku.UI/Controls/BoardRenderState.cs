using Sudoku.Core.Model;

namespace Sudoku.UI.Controls;

/// <summary>Everything the board needs to draw one frame. Immutable, so a new instance means "redraw".</summary>
/// <param name="HighlightDigit">Digit to highlight across the board (0 for none).</param>
/// <param name="IllegalMarks">Pencil-mark digits that clash with a placed number, drawn in red.</param>
/// <param name="IsDigitPicked">A number is picked on the pad: its cells are emphasized and peer tinting is dropped.</param>
/// <param name="IsContentHidden">True while paused: the grid is drawn without any digits.</param>
public sealed record BoardRenderState(
    Board Board,
    Position? Selected,
    int HighlightDigit,
    IReadOnlySet<Position> Conflicts,
    IReadOnlySet<Position> Mistakes,
    IReadOnlyDictionary<Position, DigitSet> IllegalMarks,
    IReadOnlySet<Position> HintCells,
    Position? HintTarget,
    bool IsDigitPicked,
    bool IsContentHidden);

/// <summary>Square-board geometry shared by rendering and hit testing.</summary>
public readonly record struct BoardGeometry(double Left, double Top, double Size)
{
    public double CellSize => Size / 9;

    public static BoardGeometry For(double width, double height, double inset)
    {
        var size = Math.Max(0, Math.Min(width, height) - inset * 2);
        return new BoardGeometry((width - size) / 2, (height - size) / 2, size);
    }

    public Position? HitTest(double x, double y)
    {
        if (CellSize <= 0)
            return null;
        var col = (int)Math.Floor((x - Left) / CellSize);
        var row = (int)Math.Floor((y - Top) / CellSize);
        return row is >= 0 and < 9 && col is >= 0 and < 9 ? new Position(row, col) : null;
    }

    public Avalonia.Rect CellRect(int row, int col) =>
        new(Left + col * CellSize, Top + row * CellSize, CellSize, CellSize);
}
