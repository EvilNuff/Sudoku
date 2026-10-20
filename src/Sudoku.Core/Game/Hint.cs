using Sudoku.Core.Model;
using Sudoku.Core.Solving;

namespace Sudoku.Core.Game;

public enum HintKind
{
    /// <summary>A player entry is wrong; applying the hint erases it.</summary>
    Mistake,

    /// <summary>The player's notes rule out the correct number; applying the hint applies <see cref="Hint.NoteFix"/>.</summary>
    NoteMistake,

    /// <summary>A logical deduction leads to a placement.</summary>
    Deduction,

    /// <summary>No technique applies (should not happen for generated puzzles); the digit is revealed.</summary>
    Reveal,
}

/// <summary>
/// A hint is shown in three stages: <see cref="Title"/> with <see cref="HighlightCells"/>, then
/// <see cref="Explanation"/>, then it is applied (placing <see cref="Digit"/> at <see cref="Target"/>,
/// erasing <see cref="Target"/> for a mistake, or applying <see cref="NoteFix"/> for a note mistake).
/// </summary>
/// <param name="Steps">For deductions: the supporting eliminations (only those needed) followed by the placement.</param>
public sealed record Hint(
    HintKind Kind,
    string Title,
    string Explanation,
    IReadOnlyList<Position> HighlightCells,
    Position Target,
    int Digit,
    IReadOnlyList<SolveStep> Steps,
    IReadOnlyList<(Position Position, Cell Cell)> NoteFix);

/// <summary>
/// Finds the next useful hint from the player's position, including what their notes already
/// establish, so hints never repeat deductions the player has made.
/// </summary>
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
                [wrong], wrong, 0, [], []);
        }

        if (board.IsFilled)
            throw new InvalidOperationException("The puzzle is already solved.");

        // Notes that exclude the answer would make every later deduction unsound.
        if (FindNoteMistake(board, solution) is { } noteMistake)
            return noteMistake;

        solver ??= new LogicalSolver();
        var start = GridFromPlayerState(board);
        var grid = start.Clone();
        var eliminations = new List<SolveStep>();
        while (solver.FindNextStep(grid) is { } step)
        {
            if (step.Placements.Count > 0)
                return FromDeduction(start, Minimize(start, eliminations, step.Placements[0]), step);
            eliminations.Add(step);
            grid.Apply(step);
        }

        return Reveal(board, solution);
    }

    /// <summary>
    /// Candidates implied by the placed numbers, narrowed by the player's notes: center notes are a
    /// cell's candidate list, and corner notes for a digit confine it to those cells within the box.
    /// Callers must first rule out notes that exclude the solution (see <see cref="FindNoteMistake"/>).
    /// </summary>
    internal static CandidateGrid GridFromPlayerState(Board board)
    {
        var grid = CandidateGrid.FromValues(board.CopyValues());
        for (var i = 0; i < Board.CellCount; i++)
        {
            var cell = board[i];
            if (!cell.IsEmpty || cell.CenterMarks.IsEmpty)
                continue;
            foreach (var digit in DigitSet.All.Except(cell.CenterMarks))
                grid.Eliminate(i, digit);
        }

        foreach (var box in Units.Boxes)
        {
            for (var digit = 1; digit <= 9; digit++)
            {
                var marked = box.Cells.Where(i => board[i].IsEmpty && board[i].CornerMarks.Contains(digit)).ToHashSet();
                if (marked.Count == 0)
                    continue;
                foreach (var i in box.Cells.Where(i => !marked.Contains(i)))
                    grid.Eliminate(i, digit);
            }
        }
        return grid;
    }

    private static Hint? FindNoteMistake(Board board, Solution solution)
    {
        for (var i = 0; i < Board.CellCount; i++)
        {
            var cell = board[i];
            if (cell.IsEmpty && !cell.CenterMarks.IsEmpty && !cell.CenterMarks.Contains(solution[i]))
            {
                var p = Position.FromIndex(i);
                return new Hint(
                    HintKind.NoteMistake,
                    "Notes rule out the answer",
                    $"The center notes in {p} ({string.Join(", ", cell.CenterMarks)}) don't include the number that belongs there. " +
                    "Applying this hint clears them so you can re-check that cell.",
                    [p], p, 0, [],
                    [(p, cell with { CenterMarks = DigitSet.Empty })]);
            }
        }

        foreach (var box in Units.Boxes)
        {
            for (var digit = 1; digit <= 9; digit++)
            {
                var marked = box.Cells.Where(i => board[i].IsEmpty && board[i].CornerMarks.Contains(digit)).ToList();
                if (marked.Count == 0)
                    continue;
                var home = box.Cells.First(i => solution[i] == digit);
                if (marked.Contains(home) || !board[home].IsEmpty)
                    continue;

                var cells = marked.Select(Position.FromIndex).ToList();
                return new Hint(
                    HintKind.NoteMistake,
                    "Notes rule out the answer",
                    $"In {box}, the corner notes for {digit} ({string.Join(", ", cells)}) miss the cell where {digit} belongs. " +
                    $"Applying this hint removes those {digit} corner notes so you can re-check the box.",
                    cells, cells[0], digit, [],
                    [.. marked.Select(i => (Position.FromIndex(i), board[i] with { CornerMarks = board[i].CornerMarks.Without(digit) }))]);
            }
        }
        return null;
    }

    /// <summary>Drops elimination steps the placement doesn't actually depend on, so the hint shows only what matters.</summary>
    private static List<SolveStep> Minimize(CandidateGrid start, List<SolveStep> eliminations, Placement placement)
    {
        var needed = new List<SolveStep>(eliminations);
        for (var i = needed.Count - 1; i >= 0; i--)
        {
            var trial = start.Clone();
            for (var j = 0; j < needed.Count; j++)
            {
                if (j != i)
                    trial.Apply(needed[j]);
            }
            if (IsSingle(trial, placement))
                needed.RemoveAt(i);
        }
        return needed;
    }

    /// <summary>True when the placement follows directly as a naked or hidden single.</summary>
    private static bool IsSingle(CandidateGrid grid, Placement placement)
    {
        var index = placement.Position.Index;
        var candidates = grid.CandidatesAt(index);
        if (!candidates.Contains(placement.Digit))
            return false;
        if (candidates.Count == 1)
            return true;
        return new[] { Units.RowOf(index), Units.ColumnOf(index), Units.BoxOf(index) }
            .Any(unit => grid.CellsWithCandidate(unit, placement.Digit).Count() == 1);
    }

    private static Hint FromDeduction(CandidateGrid start, List<SolveStep> eliminations, SolveStep placementStep)
    {
        var placement = placementStep.Placements[0];

        // Re-derive the closing single on the minimized grid so its explanation matches the shown steps.
        var grid = start.Clone();
        foreach (var step in eliminations)
            grid.Apply(step);
        var single = new Solving.Techniques.HiddenSingle().FindFor(grid, placement)
            ?? new Solving.Techniques.NakedSingle().FindFor(grid, placement)
            ?? placementStep;

        List<SolveStep> steps = [.. eliminations, single];

        // The "key" step is the hardest one in the chain; that is the technique the player needs to learn.
        var key = steps.Aggregate((best, s) => s.Difficulty >= best.Difficulty ? s : best);
        var explanation = steps.Count == 1
            ? single.Explanation
            : string.Join(Environment.NewLine, steps.Select((s, i) => $"{i + 1}. {s.Technique}: {s.Explanation}"));

        return new Hint(
            HintKind.Deduction,
            key.Technique,
            explanation,
            key == single ? single.PatternCells : [.. key.PatternCells.Append(placement.Position).Distinct()],
            placement.Position,
            placement.Digit,
            steps,
            []);
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
            [target], target, solution[target], [], []);
    }
}
