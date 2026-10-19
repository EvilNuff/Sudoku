using Sudoku.Core.Model;
using static Sudoku.Core.Solving.Techniques.Combinatorics;

namespace Sudoku.Core.Solving.Techniques;

/// <summary>
/// Within a box, a digit's candidates all lie on one row or column, so the digit can be
/// removed from that line outside the box.
/// </summary>
public sealed class PointingCandidates : ITechnique
{
    public string Name => "Pointing Pair/Triple";
    public Difficulty Difficulty => Difficulty.Medium;

    public SolveStep? Find(CandidateGrid grid)
    {
        foreach (var box in Units.Boxes)
        {
            for (var digit = 1; digit <= 9; digit++)
            {
                var cells = grid.CellsWithCandidate(box, digit).ToList();
                if (cells.Count < 2)
                    continue;

                var line = SharedLine(cells);
                if (line is null)
                    continue;

                var eliminations = line.Cells
                    .Except(box.Cells)
                    .Where(i => grid.CandidatesAt(i).Contains(digit))
                    .Select(i => new Elimination(Position.FromIndex(i), digit))
                    .ToList();
                if (eliminations.Count == 0)
                    continue;

                var positions = cells.Select(Position.FromIndex).ToList();
                return new SolveStep(
                    Name,
                    Difficulty,
                    [],
                    eliminations,
                    positions,
                    $"In {box}, {digit} must go in {JoinCells(positions)}, which all lie in {line}. " +
                    $"So {digit} can be removed from the rest of {line}.");
            }
        }
        return null;
    }

    private static Unit? SharedLine(List<int> cells)
    {
        if (cells.All(i => i / 9 == cells[0] / 9))
            return Units.RowOf(cells[0]);
        if (cells.All(i => i % 9 == cells[0] % 9))
            return Units.ColumnOf(cells[0]);
        return null;
    }
}

/// <summary>
/// Within a row or column, a digit's candidates all lie in one box, so the digit can be
/// removed from the rest of that box. Also called "claiming".
/// </summary>
public sealed class BoxLineReduction : ITechnique
{
    public string Name => "Box/Line Reduction";
    public Difficulty Difficulty => Difficulty.Hard;

    public SolveStep? Find(CandidateGrid grid)
    {
        foreach (var line in Units.Rows.Concat(Units.Columns))
        {
            for (var digit = 1; digit <= 9; digit++)
            {
                var cells = grid.CellsWithCandidate(line, digit).ToList();
                if (cells.Count < 2)
                    continue;

                var box = Units.BoxOf(cells[0]);
                if (!cells.All(box.Cells.Contains))
                    continue;

                var eliminations = box.Cells
                    .Except(line.Cells)
                    .Where(i => grid.CandidatesAt(i).Contains(digit))
                    .Select(i => new Elimination(Position.FromIndex(i), digit))
                    .ToList();
                if (eliminations.Count == 0)
                    continue;

                var positions = cells.Select(Position.FromIndex).ToList();
                return new SolveStep(
                    Name,
                    Difficulty,
                    [],
                    eliminations,
                    positions,
                    $"In {line}, {digit} must go in {JoinCells(positions)}, which all lie in {box}. " +
                    $"So {digit} can be removed from the rest of {box}.");
            }
        }
        return null;
    }
}
