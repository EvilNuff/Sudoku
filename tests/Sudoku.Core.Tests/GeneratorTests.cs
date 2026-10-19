using Sudoku.Core.Generation;
using Sudoku.Core.Model;
using Sudoku.Core.Solving;

namespace Sudoku.Core.Tests;

[TestClass]
public sealed class GeneratorTests
{
    [TestMethod]
    [DataRow(Difficulty.Easy)]
    [DataRow(Difficulty.Medium)]
    [DataRow(Difficulty.Hard)]
    [DataRow(Difficulty.Expert)]
    public void Generate_ProducesUniqueSolvablePuzzleAtTargetDifficulty(Difficulty difficulty)
    {
        var puzzle = new PuzzleGenerator(new Random(123)).Generate(difficulty);
        var givens = GridText.Parse(puzzle.Givens);

        Assert.AreEqual(difficulty, puzzle.Difficulty);
        Assert.IsTrue(BacktrackingSolver.HasUniqueSolution(givens));
        Assert.AreEqual(difficulty, DifficultyGrader.Grade(givens));
        Assert.AreEqual(puzzle.Solution.ToString(), GridText.Format(BacktrackingSolver.Solve(givens)!));
        Assert.IsGreaterThanOrEqualTo(PuzzleGenerator.MinimumClues(difficulty), givens.Count(v => v != 0));
    }

    [TestMethod]
    public void Generate_HonoursCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() =>
            new PuzzleGenerator().Generate(Difficulty.Expert, cancellationToken: cts.Token));
    }

    [TestMethod]
    public async Task Pool_ReturnsPuzzleOfRequestedDifficulty()
    {
        using var pool = new PuzzlePool(new PuzzleGenerator(new Random(5)), targetSize: 1);

        var puzzle = await pool.TakeAsync(Difficulty.Medium);

        Assert.AreEqual(Difficulty.Medium, puzzle.Difficulty);
    }
}
