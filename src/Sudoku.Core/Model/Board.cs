using System.Collections.Immutable;

namespace Sudoku.Core.Model;

/// <summary>An immutable snapshot of all 81 cells. Every edit returns a new board, which makes undo trivial.</summary>
public sealed class Board : IEquatable<Board>
{
    public const int CellCount = 81;

    private readonly ImmutableArray<Cell> _cells;

    private Board(ImmutableArray<Cell> cells) => _cells = cells;

    public static Board Empty { get; } = new([.. Enumerable.Repeat(default(Cell), CellCount)]);

    public static Board FromCells(IEnumerable<Cell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var array = cells.ToImmutableArray();
        if (array.Length != CellCount)
            throw new ArgumentException($"A board needs exactly {CellCount} cells.", nameof(cells));
        foreach (var cell in array)
        {
            if (cell.Value is < 0 or > 9)
                throw new ArgumentException("Cell values must be 0-9.", nameof(cells));
            if (cell.IsGiven && cell.IsEmpty)
                throw new ArgumentException("A given cell must have a value.", nameof(cells));
        }
        return new Board(array);
    }

    /// <summary>Parses an 81-character puzzle; digits are givens, '0' or '.' are empty cells.</summary>
    public static Board FromPuzzle(string puzzle)
    {
        var values = GridText.Parse(puzzle);
        return new Board([.. values.Select(v => v == 0 ? default : Cell.Given(v))]);
    }

    public Cell this[int index] => _cells[index];
    public Cell this[Position position] => _cells[position.Index];

    public IReadOnlyList<Cell> Cells => _cells;

    public bool IsFilled => _cells.All(c => !c.IsEmpty);

    public Board With(Position position, Cell cell)
    {
        if (cell.Value is < 0 or > 9)
            throw new ArgumentOutOfRangeException(nameof(cell), "Cell values must be 0-9.");
        return _cells[position.Index] == cell ? this : new Board(_cells.SetItem(position.Index, cell));
    }

    /// <summary>Applies several cell changes in one copy.</summary>
    public Board WithMany(IEnumerable<(Position Position, Cell Cell)> changes)
    {
        var builder = _cells.ToBuilder();
        foreach (var (position, cell) in changes)
            builder[position.Index] = cell;
        var result = builder.MoveToImmutable();
        return result.SequenceEqual(_cells) ? this : new Board(result);
    }

    public int[] CopyValues() => [.. _cells.Select(c => c.Value)];

    /// <summary>The 81-character value string ('0' for empty), givens and player entries alike.</summary>
    public string ToValueString() => GridText.Format(_cells.Select(c => c.Value));

    public bool Equals(Board? other) =>
        other is not null && (ReferenceEquals(this, other) || _cells.SequenceEqual(other._cells));

    public override bool Equals(object? obj) => Equals(obj as Board);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var cell in _cells)
            hash.Add(cell);
        return hash.ToHashCode();
    }

    public override string ToString() => ToValueString();
}

/// <summary>Parsing and formatting of the common 81-character grid notation.</summary>
public static class GridText
{
    public static int[] Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var chars = text.Where(c => !char.IsWhiteSpace(c)).ToArray();
        if (chars.Length != Board.CellCount)
            throw new FormatException($"Expected {Board.CellCount} cells but found {chars.Length}.");

        var values = new int[Board.CellCount];
        for (var i = 0; i < chars.Length; i++)
        {
            values[i] = chars[i] switch
            {
                '.' or '0' => 0,
                >= '1' and <= '9' => chars[i] - '0',
                _ => throw new FormatException($"Invalid character '{chars[i]}' at position {i}."),
            };
        }
        return values;
    }

    public static string Format(IEnumerable<int> values) => string.Concat(values.Select(v => (char)('0' + v)));
}
