using System.Globalization;
using System.Text;
using Sudoku.Core.Model;

namespace Sudoku.Core.Persistence;

/// <summary>
/// Compact text encoding of a board: 7 characters per cell (value digit, then corner and center
/// mark masks as 3 hex digits each). Givens are not encoded; they come from the puzzle.
/// </summary>
public static class BoardCodec
{
    private const int CharsPerCell = 7;

    public static string Encode(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var sb = new StringBuilder(Board.CellCount * CharsPerCell);
        foreach (var cell in board.Cells)
        {
            sb.Append((char)('0' + cell.Value));
            sb.Append(cell.CornerMarks.Bits.ToString("X3", CultureInfo.InvariantCulture));
            sb.Append(cell.CenterMarks.Bits.ToString("X3", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    public static Board Decode(string encoded, string givens)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        var givenValues = GridText.Parse(givens);
        if (encoded.Length != Board.CellCount * CharsPerCell)
            throw new FormatException("Encoded board has the wrong length.");

        var cells = new Cell[Board.CellCount];
        for (var i = 0; i < Board.CellCount; i++)
        {
            var chunk = encoded.AsSpan(i * CharsPerCell, CharsPerCell);
            var value = chunk[0] - '0';
            if (value is < 0 or > 9)
                throw new FormatException($"Invalid value at cell {i}.");
            var corner = ParseMask(chunk[1..4], i);
            var center = ParseMask(chunk[4..7], i);

            if (givenValues[i] != 0)
            {
                if (value != givenValues[i])
                    throw new FormatException($"Cell {i} does not match its given.");
                cells[i] = Cell.Given(value);
            }
            else
            {
                cells[i] = new Cell(value, false, corner, center);
            }
        }
        return Board.FromCells(cells);
    }

    private static DigitSet ParseMask(ReadOnlySpan<char> hex, int cell) =>
        ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits) && bits <= DigitSet.AllBits
            ? new DigitSet(bits)
            : throw new FormatException($"Invalid pencil marks at cell {cell}.");
}
