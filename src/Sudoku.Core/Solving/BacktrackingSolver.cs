using System.Numerics;
using Sudoku.Core.Model;

namespace Sudoku.Core.Solving;

/// <summary>
/// Brute-force solver using bitmasks and the minimum-remaining-values heuristic.
/// Used to prove uniqueness and to build random full grids; never used for hints.
/// </summary>
public static class BacktrackingSolver
{
    /// <summary>Counts solutions, stopping once <paramref name="limit"/> is reached.</summary>
    public static int CountSolutions(ReadOnlySpan<int> values, int limit = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var state = State.Create(values);
        if (state is null)
            return 0;
        var count = 0;
        Search(state, ref count, limit, random: null, firstSolution: null);
        return count;
    }

    public static bool HasUniqueSolution(ReadOnlySpan<int> values) => CountSolutions(values, 2) == 1;

    /// <summary>Returns the first solution found, or null when the grid is unsolvable.</summary>
    public static int[]? Solve(ReadOnlySpan<int> values, Random? random = null)
    {
        var state = State.Create(values);
        if (state is null)
            return null;
        var count = 0;
        var solution = new int[Board.CellCount];
        Search(state, ref count, 1, random, solution);
        return count == 0 ? null : solution;
    }

    /// <summary>Builds a uniformly shuffled, complete valid grid.</summary>
    public static int[] GenerateFullGrid(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Solve(new int[Board.CellCount], random)
            ?? throw new InvalidOperationException("An empty grid always has a solution.");
    }

    private static bool Search(State state, ref int count, int limit, Random? random, int[]? firstSolution)
    {
        var best = -1;
        var bestCount = 10;
        for (var i = 0; i < Board.CellCount; i++)
        {
            if (state.Values[i] != 0)
                continue;
            var c = BitOperations.PopCount((uint)state.CandidatesOf(i));
            if (c < bestCount)
            {
                best = i;
                bestCount = c;
                if (c <= 1)
                    break;
            }
        }

        if (best < 0)
        {
            count++;
            if (count == 1 && firstSolution is not null)
                state.Values.CopyTo(firstSolution, 0);
            return count >= limit;
        }

        var candidates = state.CandidatesOf(best);
        if (candidates == 0)
            return false;

        Span<int> digits = stackalloc int[9];
        var n = 0;
        for (var d = 1; d <= 9; d++)
        {
            if ((candidates & (1 << (d - 1))) != 0)
                digits[n++] = d;
        }
        if (random is not null)
            random.Shuffle(digits[..n]);

        for (var k = 0; k < n; k++)
        {
            state.Place(best, digits[k]);
            var done = Search(state, ref count, limit, random, firstSolution);
            state.Remove(best, digits[k]);
            if (done)
                return true;
        }
        return false;
    }

    private sealed class State
    {
        public readonly int[] Values = new int[Board.CellCount];
        private readonly int[] _rows = new int[9];
        private readonly int[] _cols = new int[9];
        private readonly int[] _boxes = new int[9];

        public static State? Create(ReadOnlySpan<int> values)
        {
            if (values.Length != Board.CellCount)
                throw new ArgumentException($"Expected {Board.CellCount} values.", nameof(values));
            var state = new State();
            for (var i = 0; i < Board.CellCount; i++)
            {
                var v = values[i];
                if (v == 0)
                    continue;
                if (v is < 1 or > 9)
                    throw new ArgumentException("Values must be 0-9.", nameof(values));
                if ((state.CandidatesOf(i) & (1 << (v - 1))) == 0)
                    return null; // the givens already conflict
                state.Place(i, v);
            }
            return state;
        }

        public int CandidatesOf(int i) =>
            ~(_rows[i / 9] | _cols[i % 9] | _boxes[i / 27 * 3 + i % 9 / 3]) & DigitSet.AllBits;

        public void Place(int i, int d)
        {
            var bit = 1 << (d - 1);
            Values[i] = d;
            _rows[i / 9] |= bit;
            _cols[i % 9] |= bit;
            _boxes[i / 27 * 3 + i % 9 / 3] |= bit;
        }

        public void Remove(int i, int d)
        {
            var bit = ~(1 << (d - 1));
            Values[i] = 0;
            _rows[i / 9] &= bit;
            _cols[i % 9] &= bit;
            _boxes[i / 27 * 3 + i % 9 / 3] &= bit;
        }
    }
}
