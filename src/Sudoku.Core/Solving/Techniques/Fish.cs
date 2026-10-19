using Sudoku.Core.Model;
using static Sudoku.Core.Solving.Techniques.Combinatorics;

namespace Sudoku.Core.Solving.Techniques;

/// <summary>
/// X-Wing (size 2) and Swordfish (size 3): in N rows a digit's candidates are confined to the
/// same N columns, so it can be removed from those columns in every other row (and vice versa).
/// </summary>
public sealed class Fish : ITechnique
{
    private readonly int _size;

    public Fish(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, 3);
        _size = size;
    }

    public string Name => _size == 2 ? "X-Wing" : "Swordfish";
    public Difficulty Difficulty => Difficulty.Expert;

    public SolveStep? Find(CandidateGrid grid)
    {
        for (var digit = 1; digit <= 9; digit++)
        {
            var step = FindFor(grid, digit, Units.Rows, Units.Columns, i => i % 9)
                ?? FindFor(grid, digit, Units.Columns, Units.Rows, i => i / 9);
            if (step is not null)
                return step;
        }
        return null;
    }

    private SolveStep? FindFor(
        CandidateGrid grid,
        int digit,
        IReadOnlyList<Unit> baseLines,
        IReadOnlyList<Unit> coverLines,
        Func<int, int> coverIndexOf)
    {
        var eligible = baseLines
            .Where(line => grid.CellsWithCandidate(line, digit).Count() is >= 2 && grid.CellsWithCandidate(line, digit).Count() <= _size)
            .ToList();

        foreach (var bases in Combinations(eligible, _size))
        {
            var patternCells = bases.SelectMany(b => grid.CellsWithCandidate(b, digit)).ToList();
            var covers = patternCells.Select(coverIndexOf).Distinct().ToList();
            if (covers.Count != _size)
                continue;

            var baseCells = bases.SelectMany(b => b.Cells).ToHashSet();
            var eliminations = covers
                .SelectMany(c => coverLines[c].Cells)
                .Where(i => !baseCells.Contains(i) && grid.CandidatesAt(i).Contains(digit))
                .Select(i => new Elimination(Position.FromIndex(i), digit))
                .ToList();
            if (eliminations.Count == 0)
                continue;

            var baseNames = string.Join(", ", bases.Select(b => b.ToString()));
            var coverNames = string.Join(", ", covers.Select(c => coverLines[c].ToString()));
            return new SolveStep(
                Name,
                Difficulty,
                [],
                eliminations,
                [.. patternCells.Select(Position.FromIndex)],
                $"In {baseNames}, {digit} can only go in {coverNames}. Whichever way it resolves, " +
                $"those {(_size == 2 ? "two" : "three")} lines get their {digit} from these cells, " +
                $"so {digit} can be removed from the rest of {coverNames}.");
        }
        return null;
    }
}
