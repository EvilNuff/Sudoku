namespace Sudoku.Core.Model;

/// <summary>A cell coordinate on the 9x9 grid (0-based row and column).</summary>
public readonly record struct Position
{
    public Position(int row, int col)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(row, 8);
        ArgumentOutOfRangeException.ThrowIfNegative(col);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(col, 8);
        Row = row;
        Col = col;
    }

    public int Row { get; }
    public int Col { get; }

    public int Index => Row * 9 + Col;
    public int Box => Row / 3 * 3 + Col / 3;

    public static Position FromIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 80);
        return new Position(index / 9, index % 9);
    }

    public bool SharesUnitWith(Position other) =>
        Row == other.Row || Col == other.Col || Box == other.Box;

    /// <summary>Human-readable "r1c1" notation (1-based), as used in hint explanations.</summary>
    public override string ToString() => $"r{Row + 1}c{Col + 1}";
}
