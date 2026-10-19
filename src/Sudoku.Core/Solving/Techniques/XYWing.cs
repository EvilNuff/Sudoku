using Sudoku.Core.Model;

namespace Sudoku.Core.Solving.Techniques;

/// <summary>
/// A pivot cell {X,Y} sees two pincers {X,Z} and {Y,Z}. Whatever the pivot is, one pincer
/// must be Z, so Z can be removed from every cell that sees both pincers.
/// </summary>
public sealed class XYWing : ITechnique
{
    public string Name => "XY-Wing";
    public Difficulty Difficulty => Difficulty.Expert;

    public SolveStep? Find(CandidateGrid grid)
    {
        var bivalue = Enumerable.Range(0, Board.CellCount).Where(i => grid.CandidatesAt(i).Count == 2).ToList();

        foreach (var pivot in bivalue)
        {
            var pivotDigits = grid.CandidatesAt(pivot);
            var pincers = Units.Peers[pivot]
                .Where(p => grid.CandidatesAt(p).Count == 2 && grid.CandidatesAt(p).Intersect(pivotDigits).Count == 1)
                .ToList();

            for (var a = 0; a < pincers.Count; a++)
            {
                for (var b = a + 1; b < pincers.Count; b++)
                {
                    var first = grid.CandidatesAt(pincers[a]);
                    var second = grid.CandidatesAt(pincers[b]);
                    var z = first.Except(pivotDigits);
                    if (z.Count != 1 || second.Except(pivotDigits) != z)
                        continue;
                    // The pincers must cover different pivot digits.
                    if (first.Intersect(pivotDigits) == second.Intersect(pivotDigits))
                        continue;

                    var digit = z.First();
                    var eliminations = Units.Peers[pincers[a]]
                        .Intersect(Units.Peers[pincers[b]])
                        .Where(i => i != pivot && grid.CandidatesAt(i).Contains(digit))
                        .Select(i => new Elimination(Position.FromIndex(i), digit))
                        .ToList();
                    if (eliminations.Count == 0)
                        continue;

                    var p = Position.FromIndex(pivot);
                    var pa = Position.FromIndex(pincers[a]);
                    var pb = Position.FromIndex(pincers[b]);
                    return new SolveStep(
                        Name,
                        Difficulty,
                        [],
                        eliminations,
                        [p, pa, pb],
                        $"{p} is {pivotDigits.First()} or {pivotDigits.Without(pivotDigits.First()).First()}. " +
                        $"Either way, one of {pa} ({first}) or {pb} ({second}) must be {digit}, " +
                        $"so {digit} can be removed from any cell that sees both of them.");
                }
            }
        }
        return null;
    }
}
