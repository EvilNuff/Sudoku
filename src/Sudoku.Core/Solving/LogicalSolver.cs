using Sudoku.Core.Model;
using Sudoku.Core.Solving.Techniques;

namespace Sudoku.Core.Solving;

/// <summary>The result of solving with human techniques only.</summary>
/// <param name="IsSolved">False when the solver got stuck and the puzzle would need guessing or harder techniques.</param>
/// <param name="HardestDifficulty">The hardest technique used; Easy when no steps were needed.</param>
public sealed record LogicalSolveResult(bool IsSolved, Difficulty HardestDifficulty, IReadOnlyList<SolveStep> Steps, CandidateGrid FinalGrid);

/// <summary>
/// Solves by repeatedly applying the simplest technique that makes progress. One solver drives
/// grading (the hardest technique needed) and hints (the next step from the player's position).
/// </summary>
public sealed class LogicalSolver
{
    /// <summary>Ordered from simplest to hardest; the solver always tries the earliest one first.</summary>
    public static IReadOnlyList<ITechnique> DefaultTechniques { get; } =
    [
        new HiddenSingle(),
        new NakedSingle(),
        new PointingCandidates(),
        new NakedSubset(2),
        new HiddenSubset(2),
        new BoxLineReduction(),
        new NakedSubset(3),
        new HiddenSubset(3),
        new Fish(2),
        new XYWing(),
        new Fish(3),
    ];

    private readonly IReadOnlyList<ITechnique> _techniques;

    public LogicalSolver(IReadOnlyList<ITechnique>? techniques = null)
    {
        _techniques = techniques ?? DefaultTechniques;
        if (_techniques.Count == 0)
            throw new ArgumentException("At least one technique is required.", nameof(techniques));
    }

    public SolveStep? FindNextStep(CandidateGrid grid, Difficulty? maxDifficulty = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        foreach (var technique in _techniques)
        {
            if (maxDifficulty is { } max && technique.Difficulty > max)
                continue;
            var step = technique.Find(grid);
            if (step is { MakesProgress: true })
                return step;
        }
        return null;
    }

    /// <summary>
    /// Solves as far as possible. With <paramref name="maxDifficulty"/>, stops as soon as a harder
    /// technique would be required (cheap early-out for the generator).
    /// </summary>
    public LogicalSolveResult Solve(ReadOnlySpan<int> values, Difficulty? maxDifficulty = null, CancellationToken cancellationToken = default)
    {
        var grid = CandidateGrid.FromValues(values);
        var steps = new List<SolveStep>();
        var hardest = Difficulty.Easy;

        while (!grid.IsSolved)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (grid.IsBroken)
                break;
            var step = FindNextStep(grid, maxDifficulty);
            if (step is null)
                break;
            grid.Apply(step);
            steps.Add(step);
            if (step.Difficulty > hardest)
                hardest = step.Difficulty;
        }

        return new LogicalSolveResult(grid.IsSolved && !grid.IsBroken, hardest, steps, grid);
    }
}

public static class DifficultyGrader
{
    /// <summary>The puzzle's difficulty, or null if human techniques alone cannot solve it.</summary>
    public static Difficulty? Grade(ReadOnlySpan<int> values, LogicalSolver? solver = null, Difficulty? ceiling = null)
    {
        var result = (solver ?? new LogicalSolver()).Solve(values, ceiling);
        return result.IsSolved ? result.HardestDifficulty : null;
    }
}
