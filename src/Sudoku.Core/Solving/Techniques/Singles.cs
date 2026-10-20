using Sudoku.Core.Model;

namespace Sudoku.Core.Solving.Techniques;

/// <summary>A digit has only one possible cell left within a box, row or column.</summary>
public sealed class HiddenSingle : ITechnique
{
    public string Name => "Hidden Single";
    public Difficulty Difficulty => Difficulty.Easy;

    public SolveStep? Find(CandidateGrid grid)
    {
        // Boxes first: that is where people look for singles most naturally.
        foreach (var unit in Units.Boxes.Concat(Units.Rows).Concat(Units.Columns))
        {
            for (var digit = 1; digit <= 9; digit++)
            {
                if (unit.Cells.Any(i => grid.ValueAt(i) == digit))
                    continue;
                var cells = grid.CellsWithCandidate(unit, digit).ToList();
                if (cells.Count != 1)
                    continue;

                return Build(unit, Position.FromIndex(cells[0]), digit);
            }
        }
        return null;
    }

    /// <summary>Explains a specific placement as a hidden single, if it is one.</summary>
    public SolveStep? FindFor(CandidateGrid grid, Placement placement)
    {
        var index = placement.Position.Index;
        foreach (var unit in new[] { Units.BoxOf(index), Units.RowOf(index), Units.ColumnOf(index) })
        {
            var cells = grid.CellsWithCandidate(unit, placement.Digit).ToList();
            if (cells is [var only] && only == index)
                return Build(unit, placement.Position, placement.Digit);
        }
        return null;
    }

    private SolveStep Build(Unit unit, Position target, int digit) => new(
        Name,
        Difficulty,
        [new Placement(target, digit)],
        [],
        [.. unit.Cells.Select(Position.FromIndex)],
        $"In {unit}, {digit} can only go in {target}: every other cell is blocked by a {digit} in the same row, column or box, or has been ruled out.");
}

/// <summary>A cell has only one candidate left.</summary>
public sealed class NakedSingle : ITechnique
{
    public string Name => "Naked Single";
    public Difficulty Difficulty => Difficulty.Easy;

    public SolveStep? Find(CandidateGrid grid)
    {
        for (var i = 0; i < Board.CellCount; i++)
        {
            var candidates = grid.CandidatesAt(i);
            if (candidates.Count != 1)
                continue;

            return Build(Position.FromIndex(i), candidates.First());
        }
        return null;
    }

    /// <summary>Explains a specific placement as a naked single, if it is one.</summary>
    public SolveStep? FindFor(CandidateGrid grid, Placement placement) =>
        grid.CandidatesAt(placement.Position.Index) == DigitSet.Of(placement.Digit)
            ? Build(placement.Position, placement.Digit)
            : null;

    private SolveStep Build(Position target, int digit) => new(
        Name,
        Difficulty,
        [new Placement(target, digit)],
        [],
        [target],
        $"{target} can only be {digit}: every other digit already appears in its row, column or box, or has been ruled out.");
}
