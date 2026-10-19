namespace Sudoku.Core.Model;

/// <summary>
/// The player-visible state of one cell. <see cref="Value"/> is 0 when empty.
/// Corner marks note where a digit could go within a box; center marks list a cell's candidates.
/// </summary>
public readonly record struct Cell(int Value, bool IsGiven, DigitSet CornerMarks, DigitSet CenterMarks)
{
    public bool IsEmpty => Value == 0;
    public bool HasMarks => !CornerMarks.IsEmpty || !CenterMarks.IsEmpty;

    public static Cell Given(int value) => new(value, true, DigitSet.Empty, DigitSet.Empty);
}
