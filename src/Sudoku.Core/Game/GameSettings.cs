namespace Sudoku.Core.Game;

/// <param name="ShowMistakes">Flag entries that differ from the solution (rule conflicts are always flagged).</param>
/// <param name="AutoRemoveMarks">Placing a digit removes it from pencil marks in the same row, column and box.</param>
/// <param name="HighlightSameDigits">Highlight every cell holding, or pencil-marked with, the selected digit.</param>
public sealed record GameSettings(bool ShowMistakes = false, bool AutoRemoveMarks = true, bool HighlightSameDigits = true)
{
    public static GameSettings Default { get; } = new();
}

public enum EntryMode
{
    Value,
    CornerMark,
    CenterMark,
}
