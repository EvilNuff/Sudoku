using Sudoku.Core.Model;
using Sudoku.Core.Solving;

namespace Sudoku.Core.Game;

public enum HintKind
{
    /// <summary>A player entry is wrong; applying the hint erases it.</summary>
    Mistake,

    /// <summary>A logical deduction leads to a placement.</summary>
    Deduction,

    /// <summary>No technique applies (should not happen for generated puzzles); the digit is revealed.</summary>
    Reveal,
}

/// <summary>
/// A hint is shown in three stages: <see cref="Title"/> with <see cref="HighlightCells"/>, then
/// <see cref="Explanation"/>, then it is applied (placing <see cref="Digit"/> at <see cref="Target"/>,
/// or erasing <see cref="Target"/> for a mistake).
/// </summary>
public sealed record Hint(
    HintKind Kind,
    string Title,
    string Explanation,
    IReadOnlyList<Position> HighlightCells,
    Position Target,
    int Digit);

public static class HintProvider
{
    public static Hint GetHint(Board board, Solution solution, LogicalSolver? solver = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(solution);

        // A wrong entry poisons every deduction, so point it out first.
        var mistake = SolutionChecker.FindMistakes(board, solution).Cast<Position?>().FirstOrDefault();
        if (mistake is { } wrong)
        {
            return new Hint(
                HintKind.Mistake,
                "Incorrect entry",
                $"The {board[wrong].Value} in {wrong} is not part of the solution. Remove it before continuing.",
                [wrong],
                wrong,
                0);
        }

        if (board.IsFilled)
            throw new InvalidOperationException("The puzzle is already solved.");

        solver ??= new LogicalSolver();
        var grid = CandidateGrid.FromValues(board.CopyValues());
        var steps = new List<SolveStep>();
        while (solver.FindNextStep(grid) is { } step)
        {
            steps.Add(step);
            if (step.Placements.Count > 0)
                return FromDeduction(steps);
            grid.Apply(step);
        }

        return Reveal(board, solution);
    }

    private static Hint FromDeduction(List<SolveStep> steps)
    {
        var placementStep = steps[^1];
        var placement = placementStep.Placements[0];

        // The "key" step is the hardest one in the chain; that is the technique the player needs to learn.
        var key = steps.Aggregate((best, s) => s.Difficulty >= best.Difficulty ? s : best);
        var explanation = steps.Count == 1
            ? placementStep.Explanation
            : string.Join(Environment.NewLine, steps.Select((s, i) => $"{i + 1}. {s.Technique}: {s.Explanation}"));

        return new Hint(
            HintKind.Deduction,
            key.Technique,
            explanation,
            key == placementStep ? placementStep.PatternCells : [.. key.PatternCells.Append(placement.Position).Distinct()],
            placement.Position,
            placement.Digit);
    }

    private static Hint Reveal(Board board, Solution solution)
    {
        var target = Enumerable.Range(0, Board.CellCount)
            .Select(Position.FromIndex)
            .First(p => board[p].IsEmpty);
        return new Hint(
            HintKind.Reveal,
            "Reveal",
            $"No standard technique applies here, so the answer for {target} is revealed: {solution[target]}.",
            [target],
            target,
            solution[target]);
    }
}
