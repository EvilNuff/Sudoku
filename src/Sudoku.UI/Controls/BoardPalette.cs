using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Sudoku.UI.Controls;

/// <summary>Board colors for one theme variant.</summary>
public sealed record BoardPalette(
    IBrush Background,
    IBrush PeerTint,
    IBrush MarkTint,
    IBrush SameDigitTint,
    IBrush SelectedTint,
    IBrush HintTint,
    IBrush ConflictTint,
    IBrush GivenText,
    IBrush PlayerText,
    IBrush ErrorText,
    IBrush MarkText,
    IBrush MarkHighlightText,
    IBrush PausedText,
    IPen ThinLine,
    IPen ThickLine,
    IPen HintOutline,
    IPen SelectedOutline)
{
    public static BoardPalette Light { get; } = Create(
        background: 0xFFFFFF, peer: 0xEAF0F8, mark: 0xE4EEFC, same: 0xC9DAF2, selected: 0xA9C7F0,
        hint: 0xFFF0C2, conflict: 0xFBDADA, given: 0x1A1A1A, player: 0x2457B5, error: 0xC62828,
        markText: 0x5A6270, markHighlight: 0x1F4FA8, paused: 0x8A93A0, thin: 0xC3C9D2, thick: 0x2E3440, hintOutline: 0xD99A00,
        selectedOutline: 0x2F6FD6);

    public static BoardPalette Dark { get; } = Create(
        background: 0x1E2128, peer: 0x262C36, mark: 0x283447, same: 0x2F4566, selected: 0x3D5F91,
        hint: 0x4A4020, conflict: 0x4A2427, given: 0xE6E6E6, player: 0x8AB4FF, error: 0xFF6B6B,
        markText: 0x9AA3B2, markHighlight: 0xB5D0FF, paused: 0x707886, thin: 0x3A404B, thick: 0xA0A8B5, hintOutline: 0xE0B030,
        selectedOutline: 0x8AB4FF);

    private static BoardPalette Create(
        uint background, uint peer, uint mark, uint same, uint selected, uint hint, uint conflict,
        uint given, uint player, uint error, uint markText, uint markHighlight, uint paused,
        uint thin, uint thick, uint hintOutline, uint selectedOutline) =>
        new(
            Brush(background), Brush(peer), Brush(mark), Brush(same), Brush(selected), Brush(hint), Brush(conflict),
            Brush(given), Brush(player), Brush(error), Brush(markText), Brush(markHighlight), Brush(paused),
            new ImmutablePen(Brush(thin), 1),
            new ImmutablePen(Brush(thick), 2.5),
            new ImmutablePen(Brush(hintOutline), 3),
            new ImmutablePen(Brush(selectedOutline), 2.5));

    private static ImmutableSolidColorBrush Brush(uint rgb) => new(Color.FromUInt32(0xFF000000 | rgb));
}
