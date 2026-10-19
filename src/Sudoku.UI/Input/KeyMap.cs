using Avalonia.Input;
using Sudoku.Core.Game;

namespace Sudoku.UI.Input;

public enum GameKeyAction
{
    Digit,
    Erase,
    Move,
    Undo,
    Redo,
    Hint,
    CycleMode,
    TogglePause,
}

/// <param name="Mode">For digits: an explicit entry mode from a modifier key, or null to use the current mode.</param>
public readonly record struct GameKey(GameKeyAction Action, int Digit = 0, EntryMode? Mode = null, int RowDelta = 0, int ColDelta = 0);

/// <summary>
/// Keyboard shortcuts. Shift+digit adds a corner mark and Ctrl+digit a center mark.
/// The platform "command" key (Ctrl on Windows, Cmd on macOS/iPad keyboards) drives undo/redo.
/// </summary>
public static class KeyMap
{
    public static GameKey? Translate(Key key, KeyModifiers modifiers)
    {
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var command = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

        if (DigitOf(key) is { } digit)
        {
            if (digit == 0)
                return new GameKey(GameKeyAction.Erase);
            EntryMode? mode = shift ? EntryMode.CornerMark : command ? EntryMode.CenterMark : null;
            return new GameKey(GameKeyAction.Digit, digit, mode);
        }

        return key switch
        {
            Key.Up => new GameKey(GameKeyAction.Move, RowDelta: -1),
            Key.Down => new GameKey(GameKeyAction.Move, RowDelta: 1),
            Key.Left => new GameKey(GameKeyAction.Move, ColDelta: -1),
            Key.Right => new GameKey(GameKeyAction.Move, ColDelta: 1),
            Key.Delete or Key.Back => new GameKey(GameKeyAction.Erase),
            Key.Z when command && shift => new GameKey(GameKeyAction.Redo),
            Key.Z when command => new GameKey(GameKeyAction.Undo),
            Key.Y when command => new GameKey(GameKeyAction.Redo),
            Key.H => new GameKey(GameKeyAction.Hint),
            Key.Space => new GameKey(GameKeyAction.CycleMode),
            Key.P or Key.Escape => new GameKey(GameKeyAction.TogglePause),
            _ => null,
        };
    }

    private static int? DigitOf(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null,
    };
}
