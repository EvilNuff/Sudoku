using Sudoku.Core.Game;
using Sudoku.Core.Model;

namespace Sudoku.Core.Tests;

[TestClass]
public sealed class GameSessionTests
{
    private static readonly Position Empty = new(0, 2);   // r1c3 is empty; solution is 4
    private static readonly Position Given = new(0, 0);   // r1c1 is a given 5

    private ManualTimeProvider _time = null!;
    private GameSession _session = null!;

    [TestInitialize]
    public void Setup()
    {
        _time = new ManualTimeProvider();
        _session = new GameSession(
            new Puzzle(TestPuzzles.EasyGivens, Solution.Parse(TestPuzzles.EasySolution), Difficulty.Easy),
            _time);
    }

    [TestMethod]
    public void Enter_Value_SetsCellAndTogglesOff()
    {
        Assert.IsTrue(_session.Enter(Empty, 4, EntryMode.Value));
        Assert.AreEqual(4, _session.Board[Empty].Value);

        Assert.IsTrue(_session.Enter(Empty, 4, EntryMode.Value));
        Assert.IsTrue(_session.Board[Empty].IsEmpty);
    }

    [TestMethod]
    public void Givens_CannotBeChangedOrErased()
    {
        Assert.IsFalse(_session.Enter(Given, 1, EntryMode.Value));
        Assert.IsFalse(_session.Erase(Given));
        Assert.AreEqual(5, _session.Board[Given].Value);
    }

    [TestMethod]
    public void Marks_ToggleIndependently_AndAreIgnoredOnFilledCells()
    {
        _session.Enter(Empty, 1, EntryMode.CornerMark);
        _session.Enter(Empty, 2, EntryMode.CenterMark);
        _session.Enter(Empty, 4, EntryMode.CenterMark);

        Assert.AreEqual(DigitSet.Of(1), _session.Board[Empty].CornerMarks);
        Assert.AreEqual(DigitSet.Of(2, 4), _session.Board[Empty].CenterMarks);

        _session.Enter(Empty, 4, EntryMode.Value);
        Assert.IsFalse(_session.Board[Empty].HasMarks, "Placing a value clears the cell's marks.");
        Assert.IsFalse(_session.Enter(Empty, 3, EntryMode.CornerMark));
    }

    [TestMethod]
    public void AutoNote_FillsCornerMarksWithCandidates_AndIsOneUndoableMove()
    {
        _session.Enter(Empty, 9, EntryMode.CornerMark);
        _session.Enter(Empty, 2, EntryMode.CenterMark);
        var before = _session.Board;

        Assert.IsTrue(_session.AutoNote());

        var board = _session.Board;
        for (var i = 0; i < Board.CellCount; i++)
        {
            var cell = board[i];
            if (!cell.IsEmpty)
            {
                Assert.IsTrue(cell.CornerMarks.IsEmpty, $"Filled cell {i} got corner marks.");
                continue;
            }
            var used = Units.Peers[i].Select(p => board[p].Value).Where(v => v != 0).ToHashSet();
            var expected = DigitSet.Of([.. Enumerable.Range(1, 9).Where(d => !used.Contains(d))]);
            Assert.AreEqual(expected, cell.CornerMarks, $"Cell {i}");
        }

        Assert.AreEqual(DigitSet.Of(2), board[Empty].CenterMarks, "Center marks are kept.");
        Assert.IsTrue(board[Empty].CornerMarks.Contains(4), "The solution digit is always a candidate.");

        Assert.IsTrue(_session.Undo());
        Assert.AreEqual(before, _session.Board);
    }

    [TestMethod]
    public void AutoNote_ReturnsFalse_WhenNotesAlreadyMatch()
    {
        Assert.IsTrue(_session.AutoNote());
        Assert.IsFalse(_session.AutoNote());
    }

    [TestMethod]
    public void AutoRemoveMarks_ClearsDigitFromPeers()
    {
        var peer = new Position(0, 3); // same row
        var unrelated = new Position(8, 0);
        _session.Enter(peer, 4, EntryMode.CenterMark);
        _session.Enter(unrelated, 4, EntryMode.CenterMark);

        _session.Enter(Empty, 4, EntryMode.Value);

        Assert.IsFalse(_session.Board[peer].CenterMarks.Contains(4));
        Assert.IsTrue(_session.Board[unrelated].CenterMarks.Contains(4));
    }

    [TestMethod]
    public void Undo_And_Redo_RoundTripEveryKindOfMove()
    {
        var start = _session.Board;
        _session.Enter(Empty, 1, EntryMode.CornerMark);
        var afterMark = _session.Board;
        _session.Enter(Empty, 4, EntryMode.Value);
        var afterValue = _session.Board;
        _session.Erase(Empty);

        Assert.IsTrue(_session.Undo());
        Assert.AreEqual(afterValue, _session.Board);
        Assert.IsTrue(_session.Undo());
        Assert.AreEqual(afterMark, _session.Board);
        Assert.IsTrue(_session.Undo());
        Assert.AreEqual(start, _session.Board);
        Assert.IsFalse(_session.Undo());

        Assert.IsTrue(_session.Redo());
        Assert.AreEqual(afterMark, _session.Board);
    }

    [TestMethod]
    public void NewMove_ClearsRedo()
    {
        _session.Enter(Empty, 4, EntryMode.Value);
        _session.Undo();
        _session.Enter(Empty, 2, EntryMode.CornerMark);

        Assert.IsFalse(_session.CanRedo);
    }

    [TestMethod]
    public void WrongEntries_AreCountedAndDetected()
    {
        _session.Enter(Empty, 9, EntryMode.Value);

        Assert.AreEqual(1, _session.Mistakes);
        CollectionAssert.Contains(_session.WrongEntries.ToList(), Empty);
    }

    [TestMethod]
    public void Conflicts_FlagDuplicatesInUnits()
    {
        _session.Enter(Empty, 5, EntryMode.Value); // row 1 already has a given 5

        CollectionAssert.IsSubsetOf(new[] { Empty, Given }, _session.Conflicts.ToList());
    }

    [TestMethod]
    public void IllegalMarks_FlagDigitsAlreadyPlacedInUnits()
    {
        // r1c3: row 1 has 5,3,7; column 3 has 8; box 1 has 5,3,6,9,8.
        _session.Enter(Empty, 5, EntryMode.CornerMark);   // illegal: 5 in row
        _session.Enter(Empty, 4, EntryMode.CornerMark);   // legal (the solution)
        _session.Enter(Empty, 8, EntryMode.CenterMark);   // illegal: 8 in column and box
        _session.Enter(Empty, 1, EntryMode.CenterMark);   // legal

        var illegal = _session.IllegalMarks;

        Assert.AreEqual(DigitSet.Of(5, 8), illegal[Empty]);
        Assert.HasCount(1, illegal);
    }

    [TestMethod]
    public void IllegalMarks_AppearWhenAPeerNumberIsPlaced()
    {
        _session.Settings = GameSettings.Default with { AutoRemoveMarks = false };
        var peer = new Position(0, 7); // same row as r1c3, empty, solution 1
        _session.Enter(Empty, 1, EntryMode.CornerMark);
        Assert.IsEmpty(_session.IllegalMarks);

        _session.Enter(peer, 1, EntryMode.Value);

        Assert.AreEqual(DigitSet.Of(1), _session.IllegalMarks[Empty]);
    }

    [TestMethod]
    public void Hint_PointsOutMistakeFirst_ThenApplyErasesIt()
    {
        _session.Enter(Empty, 9, EntryMode.Value);

        var hint = _session.RequestHint();

        Assert.IsNotNull(hint);
        Assert.AreEqual(HintKind.Mistake, hint.Kind);
        Assert.AreEqual(Empty, hint.Target);
        Assert.IsTrue(_session.ApplyHint(hint));
        Assert.IsTrue(_session.Board[Empty].IsEmpty);
        Assert.AreEqual(1, _session.HintsUsed);
    }

    [TestMethod]
    public void Hint_PlacesCorrectDigit_AndCanBeUndone()
    {
        var hint = _session.RequestHint()!;

        Assert.AreEqual(HintKind.Deduction, hint.Kind);
        Assert.IsFalse(string.IsNullOrWhiteSpace(hint.Explanation));
        Assert.IsTrue(_session.ApplyHint(hint));
        Assert.AreEqual(_session.Solution[hint.Target], _session.Board[hint.Target].Value);
        Assert.AreEqual(0, _session.Mistakes);

        _session.Undo();
        Assert.IsTrue(_session.Board[hint.Target].IsEmpty);
    }

    [TestMethod]
    public void SolvingWithHints_CompletesGame_AndStopsTimer()
    {
        var completed = 0;
        _session.Completed += (_, _) => completed++;
        _session.Resume();

        while (!_session.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            _session.ApplyHint(_session.RequestHint()!);
        }
        var elapsed = _session.Elapsed;
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.AreEqual(1, completed);
        Assert.AreEqual(elapsed, _session.Elapsed);
        Assert.IsFalse(_session.IsTimerRunning);
        Assert.IsFalse(_session.Enter(Empty, 1, EntryMode.Value));
        Assert.IsNull(_session.RequestHint());
    }

    [TestMethod]
    public void Timer_PausesAndResumes()
    {
        _session.Resume();
        _time.Advance(TimeSpan.FromSeconds(10));
        _session.Pause();
        _time.Advance(TimeSpan.FromSeconds(30));
        _session.Resume();
        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.AreEqual(TimeSpan.FromSeconds(15), _session.Elapsed);
    }
}

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
