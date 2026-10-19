using Sudoku.Core.Game;
using Sudoku.Core.Model;
using Sudoku.Core.Persistence;
using Sudoku.Core.Stats;

namespace Sudoku.Core.Tests;

[TestClass]
public sealed class PersistenceTests
{
    private string _dir = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SudokuTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_dir, recursive: true);

    [TestMethod]
    public void BoardCodec_RoundTripsValuesAndMarks()
    {
        var board = Board.FromPuzzle(TestPuzzles.EasyGivens)
            .With(new Position(0, 2), new Cell(4, false, DigitSet.Empty, DigitSet.Empty))
            .With(new Position(0, 3), new Cell(0, false, DigitSet.Of(1, 9), DigitSet.Of(2, 6, 8)));

        var decoded = BoardCodec.Decode(BoardCodec.Encode(board), TestPuzzles.EasyGivens);

        Assert.AreEqual(board, decoded);
    }

    [TestMethod]
    public async Task GameStore_RoundTripsSession()
    {
        var time = new ManualTimeProvider();
        var session = new GameSession(
            new Puzzle(TestPuzzles.EasyGivens, Solution.Parse(TestPuzzles.EasySolution), Difficulty.Easy), time);
        session.Resume();
        session.Enter(new Position(0, 2), 4, EntryMode.Value);
        session.Enter(new Position(0, 3), 6, EntryMode.CornerMark);
        session.RequestHint();
        time.Advance(TimeSpan.FromSeconds(42));
        var store = new JsonGameStore(Path.Combine(_dir, "game.json"));

        await store.SaveAsync(SavedGame.From(session));
        var restored = (await store.LoadAsync())!.ToSession(time, GameSettings.Default);

        Assert.AreEqual(session.Board, restored.Board);
        Assert.AreEqual(TimeSpan.FromSeconds(42), restored.Elapsed);
        Assert.AreEqual(1, restored.HintsUsed);
        Assert.IsFalse(restored.IsTimerRunning);
        Assert.IsTrue(restored.Undo());
        Assert.IsTrue(restored.Undo());
        Assert.AreEqual(Board.FromPuzzle(TestPuzzles.EasyGivens), restored.Board);
    }

    [TestMethod]
    public async Task GameStore_ReturnsNullForMissingOrCorruptFile()
    {
        var path = Path.Combine(_dir, "game.json");
        var store = new JsonGameStore(path);

        Assert.IsNull(await store.LoadAsync());

        await File.WriteAllTextAsync(path, "{ not json");
        Assert.IsNull(await store.LoadAsync());

        await File.WriteAllTextAsync(path, """{"Givens":"123","Solution":"x","Difficulty":"Easy","Board":"","UndoBoards":[],"RedoBoards":[],"Elapsed":"00:00:01","HintsUsed":0,"Mistakes":0}""");
        Assert.IsNull(await store.LoadAsync());
    }

    [TestMethod]
    public async Task StatsStore_RoundTripsAndTracksBests()
    {
        var store = new JsonStatsStore(Path.Combine(_dir, "stats.json"));
        var book = await store.LoadAsync();

        book.RecordStart(Difficulty.Hard);
        var first = book.RecordCompletion(Difficulty.Hard, TimeSpan.FromMinutes(10), hintsUsed: 2);
        var second = book.RecordCompletion(Difficulty.Hard, TimeSpan.FromMinutes(8), hintsUsed: 0);
        var third = book.RecordCompletion(Difficulty.Hard, TimeSpan.FromMinutes(9), hintsUsed: 0);
        await store.SaveAsync(book);
        var reloaded = (await store.LoadAsync())[Difficulty.Hard];

        Assert.IsTrue(first.IsNewBest);
        Assert.IsFalse(first.IsNewNoHintBest);
        Assert.IsTrue(second.IsNewBest);
        Assert.IsTrue(second.IsNewNoHintBest);
        Assert.IsFalse(third.IsNewBest);
        Assert.AreEqual(3, reloaded.GamesCompleted);
        Assert.AreEqual(1, reloaded.GamesStarted);
        Assert.AreEqual(TimeSpan.FromMinutes(8), reloaded.BestTime);
        Assert.AreEqual(TimeSpan.FromMinutes(8), reloaded.BestTimeNoHints);
        Assert.AreEqual(TimeSpan.FromMinutes(9), reloaded.AverageTime);
    }

    [TestMethod]
    public async Task SettingsStore_RoundTripsAndDefaultsWhenMissingOrCorrupt()
    {
        var path = Path.Combine(_dir, "settings.json");
        var store = new JsonSettingsStore(path);
        Assert.AreEqual(GameSettings.Default, store.Load());

        var custom = new GameSettings(ShowMistakes: true, AutoRemoveMarks: false, HighlightSameDigits: false);
        await store.SaveAsync(custom);
        Assert.AreEqual(custom, store.Load());

        await File.WriteAllTextAsync(path, "garbage");
        Assert.AreEqual(GameSettings.Default, store.Load());
    }
}
