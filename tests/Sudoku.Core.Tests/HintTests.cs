using Sudoku.Core.Game;
using Sudoku.Core.Generation;
using Sudoku.Core.Model;
using Sudoku.Core.Solving;

namespace Sudoku.Core.Tests;

[TestClass]
public sealed class HintTests
{
    [TestMethod]
    public void Hint_DoesNotRepeatEliminationsAlreadyInPlayersNotes()
    {
        var (puzzle, board, first) = PlayUntilHintNeedsEliminations();

        // The player makes exactly the hinted eliminations by writing full candidate notes.
        var grid = HintProvider.GridFromPlayerState(board);
        foreach (var step in first.Steps.Take(first.Steps.Count - 1))
            grid.Apply(step);
        var noted = board.WithMany(Enumerable.Range(0, Board.CellCount)
            .Where(i => board[i].IsEmpty)
            .Select(i => (Position.FromIndex(i), board[i] with { CenterMarks = grid.CandidatesAt(i) })));

        var second = HintProvider.GetHint(noted, puzzle.Solution);

        Assert.AreEqual(HintKind.Deduction, second.Kind);
        Assert.HasCount(1, second.Steps, $"Expected a direct single but got: {second.Explanation}");
        Assert.AreEqual(puzzle.Solution[second.Target], second.Digit);
    }

    [TestMethod]
    public void Hint_StepsAreMinimalAndConsistentWithSolution()
    {
        var (puzzle, board, hint) = PlayUntilHintNeedsEliminations();

        Assert.AreEqual(puzzle.Solution[hint.Target], hint.Digit);
        foreach (var step in hint.Steps)
        {
            foreach (var (position, digit) in step.Eliminations)
                Assert.AreNotEqual(puzzle.Solution[position], digit, $"{step.Technique} eliminated the answer.");
        }

        // Dropping any one supporting step must break the deduction, otherwise it wasn't needed.
        var supporting = hint.Steps.Take(hint.Steps.Count - 1).ToList();
        for (var skip = 0; skip < supporting.Count; skip++)
        {
            var grid = HintProvider.GridFromPlayerState(board);
            for (var j = 0; j < supporting.Count; j++)
            {
                if (j != skip)
                    grid.Apply(supporting[j]);
            }
            var stillSingle = grid.CandidatesAt(hint.Target.Index) == DigitSet.Of(hint.Digit) ||
                new[] { Units.RowOf(hint.Target.Index), Units.ColumnOf(hint.Target.Index), Units.BoxOf(hint.Target.Index) }
                    .Any(u => grid.CellsWithCandidate(u, hint.Digit).Count() == 1);
            Assert.IsFalse(stillSingle, $"Step {skip + 1} ({supporting[skip].Technique}) was not needed.");
        }
    }

    [TestMethod]
    public void Hint_FlagsCenterNotesThatExcludeTheAnswer_AndApplyClearsThem()
    {
        var session = NewEasySession();
        var cell = new Position(0, 2); // answer is 4
        session.Enter(cell, 1, EntryMode.CenterMark);
        session.Enter(cell, 2, EntryMode.CenterMark);

        var hint = session.RequestHint()!;

        Assert.AreEqual(HintKind.NoteMistake, hint.Kind);
        Assert.AreEqual(cell, hint.Target);
        Assert.IsTrue(session.ApplyHint(hint));
        Assert.IsTrue(session.Board[cell].CenterMarks.IsEmpty);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(DigitSet.Of(1, 2), session.Board[cell].CenterMarks);
    }

    [TestMethod]
    public void Hint_FlagsCornerNotesThatMissTheAnswersCell()
    {
        var session = NewEasySession();
        // In box 1 the 4 belongs in r1c3; marking 4 only in r2c2 and r2c3 misses it.
        session.Enter(new Position(1, 1), 4, EntryMode.CornerMark);
        session.Enter(new Position(1, 2), 4, EntryMode.CornerMark);

        var hint = session.RequestHint()!;

        Assert.AreEqual(HintKind.NoteMistake, hint.Kind);
        Assert.AreEqual(4, hint.Digit);
        session.ApplyHint(hint);
        Assert.IsFalse(session.Board[new Position(1, 1)].CornerMarks.Contains(4));
        Assert.IsFalse(session.Board[new Position(1, 2)].CornerMarks.Contains(4));
    }

    [TestMethod]
    public void Hint_TrustsCorrectCornerNotes()
    {
        var session = NewEasySession();
        // Correct corner notes for 4 in box 1 (r1c3 is its home) must not be reported as a mistake.
        session.Enter(new Position(0, 2), 4, EntryMode.CornerMark);
        session.Enter(new Position(1, 1), 4, EntryMode.CornerMark);

        var hint = session.RequestHint()!;

        Assert.AreNotEqual(HintKind.NoteMistake, hint.Kind);
    }

    private static GameSession NewEasySession() => new(
        new Puzzle(TestPuzzles.EasyGivens, Solution.Parse(TestPuzzles.EasySolution), Difficulty.Easy),
        TimeProvider.System);

    /// <summary>Plays hinted singles until a hint needs at least one elimination step first.</summary>
    private static (Puzzle Puzzle, Board Board, Hint Hint) PlayUntilHintNeedsEliminations()
    {
        var generator = new PuzzleGenerator(new Random(11));
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var puzzle = generator.Generate(Difficulty.Hard);
            var board = puzzle.CreateBoard();
            while (!board.IsFilled)
            {
                var hint = HintProvider.GetHint(board, puzzle.Solution);
                if (hint.Steps.Count > 1)
                    return (puzzle, board, hint);
                board = board.With(hint.Target, board[hint.Target] with { Value = hint.Digit });
            }
        }
        Assert.Fail("No position needing eliminations was found.");
        throw new InvalidOperationException();
    }
}
