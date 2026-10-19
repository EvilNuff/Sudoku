using Sudoku.Core.Model;

namespace Sudoku.Core.Game;

/// <summary>
/// Undo/redo over immutable board snapshots. Each move pushes the board it replaced, so every
/// action (entries, marks, erase, hints) is reversible without per-command inverse logic.
/// </summary>
public sealed class UndoHistory
{
    private readonly List<Board> _undo;
    private readonly List<Board> _redo;

    public UndoHistory() : this([], []) { }

    public UndoHistory(IEnumerable<Board> undo, IEnumerable<Board> redo)
    {
        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);
        _undo = [.. undo];
        _redo = [.. redo];
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Oldest first, for persistence.</summary>
    public IReadOnlyList<Board> UndoSnapshots => _undo;
    public IReadOnlyList<Board> RedoSnapshots => _redo;

    public void Record(Board before)
    {
        ArgumentNullException.ThrowIfNull(before);
        _undo.Add(before);
        _redo.Clear();
    }

    public Board Undo(Board current)
    {
        if (!CanUndo)
            throw new InvalidOperationException("Nothing to undo.");
        var previous = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(current);
        return previous;
    }

    public Board Redo(Board current)
    {
        if (!CanRedo)
            throw new InvalidOperationException("Nothing to redo.");
        var next = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(current);
        return next;
    }
}
