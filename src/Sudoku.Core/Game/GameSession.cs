using Sudoku.Core.Model;

namespace Sudoku.Core.Game;

/// <summary>
/// One game in progress: the board, undo history, timer and counters. All player actions go
/// through here; each returns false (and changes nothing) when it would be a no-op.
/// </summary>
public sealed class GameSession
{
    private readonly UndoHistory _history;
    private readonly GameTimer _timer;

    public GameSession(Puzzle puzzle, TimeProvider time, GameSettings? settings = null)
        : this(puzzle, puzzle.CreateBoard(), new UndoHistory(), new GameTimer(time), settings ?? GameSettings.Default, 0, 0)
    {
    }

    private GameSession(
        Puzzle puzzle,
        Board board,
        UndoHistory history,
        GameTimer timer,
        GameSettings settings,
        int hintsUsed,
        int mistakes)
    {
        ArgumentNullException.ThrowIfNull(puzzle);
        Puzzle = puzzle;
        Board = board;
        _history = history;
        _timer = timer;
        Settings = settings;
        HintsUsed = hintsUsed;
        Mistakes = mistakes;
        IsCompleted = IsSolvedBoard(board);
    }

    /// <summary>Rebuilds a session from saved state; the timer starts paused.</summary>
    public static GameSession Restore(
        Puzzle puzzle,
        Board board,
        UndoHistory history,
        TimeSpan elapsed,
        int hintsUsed,
        int mistakes,
        TimeProvider time,
        GameSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentOutOfRangeException.ThrowIfNegative(hintsUsed);
        ArgumentOutOfRangeException.ThrowIfNegative(mistakes);
        return new GameSession(puzzle, board, history, new GameTimer(time, elapsed), settings ?? GameSettings.Default, hintsUsed, mistakes);
    }

    /// <summary>Raised after any change to <see cref="Board"/>.</summary>
    public event EventHandler? BoardChanged;

    /// <summary>Raised once, when the last correct digit is placed.</summary>
    public event EventHandler? Completed;

    public Puzzle Puzzle { get; }
    public Solution Solution => Puzzle.Solution;
    public Difficulty Difficulty => Puzzle.Difficulty;
    public Board Board { get; private set; }
    public GameSettings Settings { get; set; }

    public int HintsUsed { get; private set; }

    /// <summary>Wrong digits entered so far. Counted always; shown only when "Show mistakes" is on.</summary>
    public int Mistakes { get; private set; }

    public bool IsCompleted { get; private set; }
    public bool CanUndo => !IsCompleted && _history.CanUndo;
    public bool CanRedo => !IsCompleted && _history.CanRedo;
    public UndoHistory History => _history;

    public TimeSpan Elapsed => _timer.Elapsed;
    public bool IsTimerRunning => _timer.IsRunning;

    public void Resume()
    {
        if (!IsCompleted)
            _timer.Start();
    }

    public void Pause() => _timer.Pause();

    public IReadOnlySet<Position> Conflicts => ConflictDetector.FindConflicts(Board);

    /// <summary>Pencil marks that contradict a number already placed in the same row, column or box.</summary>
    public IReadOnlyDictionary<Position, DigitSet> IllegalMarks => ConflictDetector.FindIllegalMarks(Board);

    public IReadOnlySet<Position> WrongEntries => SolutionChecker.FindMistakes(Board, Solution);

    /// <summary>How many of each digit (index 1-9) are on the board, for greying out finished digits.</summary>
    public int[] DigitCounts()
    {
        var counts = new int[10];
        foreach (var cell in Board.Cells)
            counts[cell.Value]++;
        return counts;
    }

    public bool Enter(Position position, int digit, EntryMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);
        if (IsCompleted)
            return false;

        var cell = Board[position];
        if (cell.IsGiven)
            return false;

        return mode switch
        {
            EntryMode.Value => SetValue(position, cell.Value == digit ? 0 : digit),
            EntryMode.CornerMark when cell.IsEmpty => Commit(Board.With(position, cell with { CornerMarks = cell.CornerMarks.Toggle(digit) })),
            EntryMode.CenterMark when cell.IsEmpty => Commit(Board.With(position, cell with { CenterMarks = cell.CenterMarks.Toggle(digit) })),
            EntryMode.CornerMark or EntryMode.CenterMark => false,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    /// <summary>Clears a cell's value and pencil marks. Givens cannot be erased.</summary>
    public bool Erase(Position position)
    {
        var cell = Board[position];
        if (IsCompleted || cell.IsGiven || (cell.IsEmpty && !cell.HasMarks))
            return false;
        return Commit(Board.With(position, default));
    }

    public bool Undo()
    {
        if (!CanUndo)
            return false;
        Board = _history.Undo(Board);
        BoardChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
            return false;
        Board = _history.Redo(Board);
        OnBoardCommitted();
        return true;
    }

    /// <summary>Computes the next hint and counts it as used. Returns null when the game is over.</summary>
    public Hint? RequestHint()
    {
        if (IsCompleted)
            return null;
        HintsUsed++;
        return HintProvider.GetHint(Board, Solution);
    }

    /// <summary>Applies a hint from <see cref="RequestHint"/>; it can be undone like any move.</summary>
    public bool ApplyHint(Hint hint)
    {
        ArgumentNullException.ThrowIfNull(hint);
        if (IsCompleted)
            return false;
        return hint.Kind == HintKind.Mistake
            ? Erase(hint.Target)
            : SetValue(hint.Target, hint.Digit, countMistake: false);
    }

    private bool SetValue(Position position, int digit, bool countMistake = true)
    {
        var cell = Board[position];
        if (cell.IsGiven || cell.Value == digit)
            return false;

        var changes = new List<(Position, Cell)>
        {
            (position, cell with { Value = digit, CornerMarks = DigitSet.Empty, CenterMarks = DigitSet.Empty }),
        };

        if (digit != 0 && Settings.AutoRemoveMarks)
        {
            foreach (var peerIndex in Units.Peers[position.Index])
            {
                var peer = Board[peerIndex];
                if (peer.CornerMarks.Contains(digit) || peer.CenterMarks.Contains(digit))
                {
                    changes.Add((Position.FromIndex(peerIndex), peer with
                    {
                        CornerMarks = peer.CornerMarks.Without(digit),
                        CenterMarks = peer.CenterMarks.Without(digit),
                    }));
                }
            }
        }

        if (countMistake && digit != 0 && digit != Solution[position])
            Mistakes++;

        return Commit(Board.WithMany(changes));
    }

    private bool Commit(Board next)
    {
        if (ReferenceEquals(next, Board))
            return false;
        _history.Record(Board);
        Board = next;
        OnBoardCommitted();
        return true;
    }

    private void OnBoardCommitted()
    {
        // Any input counts as playing, so make sure the clock is running.
        if (!IsCompleted)
            _timer.Start();

        var justCompleted = !IsCompleted && IsSolvedBoard(Board);
        if (justCompleted)
        {
            IsCompleted = true;
            _timer.Pause();
        }

        BoardChanged?.Invoke(this, EventArgs.Empty);
        if (justCompleted)
            Completed?.Invoke(this, EventArgs.Empty);
    }

    private bool IsSolvedBoard(Board board)
    {
        for (var i = 0; i < Board.CellCount; i++)
        {
            if (board[i].Value != Puzzle.Solution[i])
                return false;
        }
        return true;
    }
}
