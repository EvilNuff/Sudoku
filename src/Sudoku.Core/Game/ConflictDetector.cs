using Sudoku.Core.Model;

namespace Sudoku.Core.Game;

/// <summary>Finds rule violations: cells whose value is repeated in a row, column or box.</summary>
public static class ConflictDetector
{
    public static IReadOnlySet<Position> FindConflicts(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var conflicts = new HashSet<Position>();
        for (var i = 0; i < Board.CellCount; i++)
        {
            var value = board[i].Value;
            if (value == 0)
                continue;
            foreach (var peer in Units.Peers[i])
            {
                if (board[peer].Value == value)
                {
                    conflicts.Add(Position.FromIndex(i));
                    break;
                }
            }
        }
        return conflicts;
    }

    /// <summary>
    /// For each empty cell, the pencil-mark digits (corner or center) that are already placed in
    /// the cell's row, column or box. Cells without illegal marks are omitted.
    /// </summary>
    public static IReadOnlyDictionary<Position, DigitSet> FindIllegalMarks(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var result = new Dictionary<Position, DigitSet>();
        for (var i = 0; i < Board.CellCount; i++)
        {
            var cell = board[i];
            if (!cell.IsEmpty || !cell.HasMarks)
                continue;

            var used = DigitSet.Empty;
            foreach (var peer in Units.Peers[i])
            {
                if (board[peer].Value != 0)
                    used = used.With(board[peer].Value);
            }

            var illegal = cell.CornerMarks.Union(cell.CenterMarks).Intersect(used);
            if (!illegal.IsEmpty)
                result[Position.FromIndex(i)] = illegal;
        }
        return result;
    }

    public static bool HasConflict(Board board, Position position)
    {
        ArgumentNullException.ThrowIfNull(board);
        var value = board[position].Value;
        return value != 0 && Units.Peers[position.Index].Any(p => board[p].Value == value);
    }
}

/// <summary>Compares player entries with the known solution. Used only when "Show mistakes" is on.</summary>
public static class SolutionChecker
{
    public static IReadOnlySet<Position> FindMistakes(Board board, Solution solution)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(solution);
        var mistakes = new HashSet<Position>();
        for (var i = 0; i < Board.CellCount; i++)
        {
            var cell = board[i];
            if (!cell.IsEmpty && !cell.IsGiven && cell.Value != solution[i])
                mistakes.Add(Position.FromIndex(i));
        }
        return mistakes;
    }
}
