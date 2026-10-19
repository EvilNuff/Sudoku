using Sudoku.Core.Model;
using Sudoku.Core.Solving;

namespace Sudoku.Core.Generation;

public sealed class PuzzleGenerationException(string message) : Exception(message);

/// <summary>
/// Builds puzzles by digging clues out of a random full grid while the solution stays unique and the
/// puzzle stays within the target difficulty, then accepting it only if it grades exactly at the target.
/// </summary>
public sealed class PuzzleGenerator
{
    public const int DefaultMaxAttempts = 500;

    private readonly Random _random;
    private readonly LogicalSolver _solver;

    public PuzzleGenerator(Random? random = null, LogicalSolver? solver = null)
    {
        _random = random ?? Random.Shared;
        _solver = solver ?? new LogicalSolver();
    }

    /// <summary>Easy puzzles keep more clues so they feel approachable; harder levels may dig freely.</summary>
    public static int MinimumClues(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 36,
        Difficulty.Medium => 30,
        Difficulty.Hard => 24,
        Difficulty.Expert => 17,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null),
    };

    public Puzzle Generate(Difficulty difficulty, int maxAttempts = DefaultMaxAttempts, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var puzzle = TryGenerate(difficulty, cancellationToken);
            if (puzzle is not null)
                return puzzle;
        }
        throw new PuzzleGenerationException($"No {difficulty} puzzle found after {maxAttempts} attempts.");
    }

    private Puzzle? TryGenerate(Difficulty target, CancellationToken cancellationToken)
    {
        // Thread-safety: Random is not thread-safe, so serialize access when a shared instance is injected.
        int[] solution;
        int[] pairOrder;
        lock (_random)
        {
            solution = BacktrackingSolver.GenerateFullGrid(_random);
            pairOrder = [.. Enumerable.Range(0, 41)];
            _random.Shuffle(pairOrder);
        }

        var puzzle = (int[])solution.Clone();
        var clues = Board.CellCount;
        var minimum = MinimumClues(target);

        // Remove clues in 180-degree symmetric pairs; index 40 is the center cell and pairs with itself.
        foreach (var i in pairOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var j = Board.CellCount - 1 - i;
            var removed = i == j ? 1 : 2;
            if (clues - removed < minimum)
                continue;

            var (a, b) = (puzzle[i], puzzle[j]);
            puzzle[i] = 0;
            puzzle[j] = 0;

            if (BacktrackingSolver.HasUniqueSolution(puzzle) && DifficultyGrader.Grade(puzzle, _solver, target) is not null)
            {
                clues -= removed;
            }
            else
            {
                puzzle[i] = a;
                puzzle[j] = b;
            }
        }

        var grade = DifficultyGrader.Grade(puzzle, _solver);
        return grade == target
            ? new Puzzle(GridText.Format(puzzle), new Solution(solution), target)
            : null;
    }
}
