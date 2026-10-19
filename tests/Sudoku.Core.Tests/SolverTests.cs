using Sudoku.Core.Generation;
using Sudoku.Core.Model;
using Sudoku.Core.Solving;
using Sudoku.Core.Solving.Techniques;

namespace Sudoku.Core.Tests;

[TestClass]
public sealed class SolverTests
{
    [TestMethod]
    public void Backtracking_SolvesKnownPuzzle()
    {
        var solution = BacktrackingSolver.Solve(GridText.Parse(TestPuzzles.EasyGivens));

        Assert.IsNotNull(solution);
        Assert.AreEqual(TestPuzzles.EasySolution, GridText.Format(solution));
    }

    [TestMethod]
    public void Backtracking_CountsSolutionsUpToLimit()
    {
        Assert.AreEqual(1, BacktrackingSolver.CountSolutions(GridText.Parse(TestPuzzles.EasyGivens)));
        Assert.AreEqual(2, BacktrackingSolver.CountSolutions(new int[81]));
    }

    [TestMethod]
    public void Backtracking_ReturnsZeroForConflictingGivens()
    {
        var values = GridText.Parse(TestPuzzles.EasyGivens);
        values[2] = 5; // duplicates the 5 in r1c1

        Assert.AreEqual(0, BacktrackingSolver.CountSolutions(values));
        Assert.IsNull(BacktrackingSolver.Solve(values));
    }

    [TestMethod]
    public void FullGrid_IsValidSolution()
    {
        var grid = BacktrackingSolver.GenerateFullGrid(new Random(42));

        _ = new Solution(grid); // throws if invalid
    }

    [TestMethod]
    public void LogicalSolver_SolvesEasyPuzzleWithSinglesOnly()
    {
        var result = new LogicalSolver().Solve(GridText.Parse(TestPuzzles.EasyGivens));

        Assert.IsTrue(result.IsSolved);
        Assert.AreEqual(Difficulty.Easy, result.HardestDifficulty);
        Assert.AreEqual(TestPuzzles.EasySolution, GridText.Format(result.FinalGrid.CopyValues()));
    }

    [TestMethod]
    public void NakedPair_EliminatesPairDigitsFromRestOfUnit()
    {
        // Row 1: r1c1 and r1c2 are both {1,2}; r1c3 is {1,2,3}.
        var grid = GridWithRowCandidates(DigitSet.Of(1, 2), DigitSet.Of(1, 2), DigitSet.Of(1, 2, 3));

        var step = new NakedSubset(2).Find(grid);

        Assert.IsNotNull(step);
        var expected = Enumerable.Range(2, 7)
            .SelectMany(c => new[] { new Elimination(new Position(0, c), 1), new Elimination(new Position(0, c), 2) })
            .ToArray();
        CollectionAssert.AreEquivalent(expected, step.Eliminations.ToArray());
    }

    [TestMethod]
    public void Techniques_NeverContradictTheSolution()
    {
        var generator = new PuzzleGenerator(new Random(7));
        var solver = new LogicalSolver();
        var techniquesSeen = new HashSet<string>();

        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            for (var n = 0; n < 4; n++)
            {
                var puzzle = generator.Generate(difficulty);
                var grid = CandidateGrid.FromValues(GridText.Parse(puzzle.Givens));
                while (solver.FindNextStep(grid) is { } step)
                {
                    techniquesSeen.Add(step.Technique);
                    foreach (var (position, digit) in step.Placements)
                        Assert.AreEqual(puzzle.Solution[position], digit, $"{step.Technique} placed a wrong digit at {position}.");
                    foreach (var (position, digit) in step.Eliminations)
                        Assert.AreNotEqual(puzzle.Solution[position], digit, $"{step.Technique} eliminated the true digit at {position}.");
                    grid.Apply(step);
                }
                Assert.IsTrue(grid.IsSolved, $"{difficulty} puzzle was not solved logically.");
            }
        }

        Assert.IsGreaterThanOrEqualTo(5, techniquesSeen.Count, $"Only saw: {string.Join(", ", techniquesSeen)}");
    }

    private static CandidateGrid GridWithRowCandidates(params DigitSet[] firstCells)
    {
        var grid = CandidateGrid.FromValues(new int[81]);
        for (var c = 0; c < firstCells.Length; c++)
        {
            foreach (var d in DigitSet.All.Except(firstCells[c]))
                grid.Eliminate(c, d);
        }
        return grid;
    }
}
