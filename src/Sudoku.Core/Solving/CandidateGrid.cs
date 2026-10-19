using Sudoku.Core.Model;

namespace Sudoku.Core.Solving;

/// <summary>Mutable working state for the logical solver: placed values plus each empty cell's candidates.</summary>
public sealed class CandidateGrid
{
    private readonly int[] _values;
    private readonly DigitSet[] _candidates;

    private CandidateGrid(int[] values, DigitSet[] candidates)
    {
        _values = values;
        _candidates = candidates;
    }

    /// <summary>Builds a grid whose candidates are every digit not already used by a peer.</summary>
    public static CandidateGrid FromValues(ReadOnlySpan<int> values)
    {
        if (values.Length != Board.CellCount)
            throw new ArgumentException($"Expected {Board.CellCount} values.", nameof(values));

        var grid = new CandidateGrid(new int[Board.CellCount], new DigitSet[Board.CellCount]);
        Array.Fill(grid._candidates, DigitSet.All);
        for (var i = 0; i < Board.CellCount; i++)
        {
            if (values[i] != 0)
                grid.Place(i, values[i]);
        }
        return grid;
    }

    public CandidateGrid Clone() => new((int[])_values.Clone(), (DigitSet[])_candidates.Clone());

    public int ValueAt(int index) => _values[index];
    public DigitSet CandidatesAt(int index) => _values[index] != 0 ? DigitSet.Empty : _candidates[index];

    public bool IsSolved => _values.All(v => v != 0);

    /// <summary>True when an empty cell has no candidates left, i.e. the grid has no solution.</summary>
    public bool IsBroken
    {
        get
        {
            for (var i = 0; i < Board.CellCount; i++)
            {
                if (_values[i] == 0 && _candidates[i].IsEmpty)
                    return true;
            }
            return false;
        }
    }

    public int[] CopyValues() => (int[])_values.Clone();

    /// <summary>Cells in <paramref name="unit"/> that still have <paramref name="digit"/> as a candidate.</summary>
    public IEnumerable<int> CellsWithCandidate(Unit unit, int digit) =>
        unit.Cells.Where(i => CandidatesAt(i).Contains(digit));

    public void Place(int index, int digit)
    {
        _values[index] = digit;
        _candidates[index] = DigitSet.Empty;
        foreach (var peer in Units.Peers[index])
            _candidates[peer] = _candidates[peer].Without(digit);
    }

    public void Eliminate(int index, int digit) => _candidates[index] = _candidates[index].Without(digit);

    public void Apply(SolveStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        foreach (var (position, digit) in step.Eliminations)
            Eliminate(position.Index, digit);
        foreach (var (position, digit) in step.Placements)
            Place(position.Index, digit);
    }
}
