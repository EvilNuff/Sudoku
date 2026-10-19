using System.Collections.Immutable;

namespace Sudoku.Core.Model;

/// <summary>A complete, valid 9x9 grid.</summary>
public sealed class Solution
{
    private readonly ImmutableArray<int> _values;

    public Solution(IEnumerable<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = [.. values];
        if (_values.Length != Board.CellCount || _values.Any(v => v is < 1 or > 9))
            throw new ArgumentException("A solution needs 81 digits from 1 to 9.", nameof(values));
        if (!IsValidGrid(_values))
            throw new ArgumentException("The grid violates Sudoku rules.", nameof(values));
    }

    public static Solution Parse(string text) => new(GridText.Parse(text));

    public int this[int index] => _values[index];
    public int this[Position position] => _values[position.Index];

    public override string ToString() => GridText.Format(_values);

    private static bool IsValidGrid(ImmutableArray<int> values) =>
        Units.All.All(unit => unit.Cells.Select(i => values[i]).Distinct().Count() == 9);
}

/// <summary>A generated puzzle: its givens, unique solution and graded difficulty.</summary>
public sealed record Puzzle(string Givens, Solution Solution, Difficulty Difficulty)
{
    public Board CreateBoard() => Board.FromPuzzle(Givens);
}
