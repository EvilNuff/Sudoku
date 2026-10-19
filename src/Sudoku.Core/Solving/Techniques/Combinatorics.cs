namespace Sudoku.Core.Solving.Techniques;

internal static class Combinatorics
{
    /// <summary>All k-element combinations of <paramref name="items"/>, in lexicographic order.</summary>
    public static IEnumerable<T[]> Combinations<T>(IReadOnlyList<T> items, int k)
    {
        if (k <= 0 || k > items.Count)
            yield break;

        var indices = Enumerable.Range(0, k).ToArray();
        while (true)
        {
            yield return [.. indices.Select(i => items[i])];

            var pos = k - 1;
            while (pos >= 0 && indices[pos] == items.Count - k + pos)
                pos--;
            if (pos < 0)
                yield break;

            indices[pos]++;
            for (var j = pos + 1; j < k; j++)
                indices[j] = indices[j - 1] + 1;
        }
    }

    public static string JoinDigits(IEnumerable<int> digits) => string.Join("/", digits);

    public static string JoinCells(IEnumerable<Model.Position> cells) => string.Join(", ", cells);
}
