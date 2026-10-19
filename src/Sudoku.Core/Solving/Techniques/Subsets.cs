using Sudoku.Core.Model;
using static Sudoku.Core.Solving.Techniques.Combinatorics;

namespace Sudoku.Core.Solving.Techniques;

/// <summary>
/// N cells in a unit whose candidates together are exactly N digits: those digits
/// can be removed from every other cell in the unit.
/// </summary>
public sealed class NakedSubset : ITechnique
{
    private readonly int _size;

    public NakedSubset(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, 3);
        _size = size;
    }

    public string Name => _size == 2 ? "Naked Pair" : "Naked Triple";
    public Difficulty Difficulty => _size == 2 ? Difficulty.Medium : Difficulty.Hard;

    public SolveStep? Find(CandidateGrid grid)
    {
        foreach (var unit in Units.All)
        {
            var eligible = unit.Cells
                .Where(i => grid.CandidatesAt(i).Count is >= 2 && grid.CandidatesAt(i).Count <= _size)
                .ToList();

            foreach (var subset in Combinations(eligible, _size))
            {
                var digits = subset.Aggregate(DigitSet.Empty, (acc, i) => acc.Union(grid.CandidatesAt(i)));
                if (digits.Count != _size)
                    continue;

                var eliminations = unit.Cells
                    .Except(subset)
                    .SelectMany(i => grid.CandidatesAt(i).Intersect(digits)
                        .Select(d => new Elimination(Position.FromIndex(i), d)))
                    .ToList();
                if (eliminations.Count == 0)
                    continue;

                var cells = subset.Select(Position.FromIndex).ToList();
                return new SolveStep(
                    Name,
                    Difficulty,
                    [],
                    eliminations,
                    cells,
                    $"In {unit}, cells {JoinCells(cells)} can only hold {JoinDigits(digits)} between them, " +
                    $"so those digits can be removed from the other cells in {unit}.");
            }
        }
        return null;
    }
}

/// <summary>
/// N digits that can only go in the same N cells of a unit: every other candidate
/// can be removed from those cells.
/// </summary>
public sealed class HiddenSubset : ITechnique
{
    private readonly int _size;

    public HiddenSubset(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, 3);
        _size = size;
    }

    public string Name => _size == 2 ? "Hidden Pair" : "Hidden Triple";
    public Difficulty Difficulty => _size == 2 ? Difficulty.Medium : Difficulty.Hard;

    public SolveStep? Find(CandidateGrid grid)
    {
        foreach (var unit in Units.All)
        {
            var openDigits = Enumerable.Range(1, 9)
                .Where(d => unit.Cells.All(i => grid.ValueAt(i) != d))
                .Where(d => grid.CellsWithCandidate(unit, d).Count() is >= 2 && grid.CellsWithCandidate(unit, d).Count() <= _size)
                .ToList();

            foreach (var digitCombo in Combinations(openDigits, _size))
            {
                var digits = DigitSet.Of(digitCombo);
                var cells = unit.Cells.Where(i => !grid.CandidatesAt(i).Intersect(digits).IsEmpty).ToList();
                if (cells.Count != _size)
                    continue;

                var eliminations = cells
                    .SelectMany(i => grid.CandidatesAt(i).Except(digits)
                        .Select(d => new Elimination(Position.FromIndex(i), d)))
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
                    $"In {unit}, {JoinDigits(digits)} can only go in {JoinCells(positions)}, " +
                    "so every other candidate can be removed from those cells.");
            }
        }
        return null;
    }
}
