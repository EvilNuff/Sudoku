# Sudoku

A cross-platform Sudoku game built with [Avalonia UI](https://avaloniaui.net/) on .NET 10. It runs on Windows today; iOS is set up for later.

## Features
- Four difficulty levels (Easy, Medium, Hard, Expert), graded by the hardest solving technique a puzzle needs
- Number-first input: choose Numbers, Corner notes or Center notes, pick a number, then click cells to place or remove it. Numbers replace a cell's notes; notes never change a numbered cell. Placing a number can also clear that number from notes in its row, column and box (a setting).
- Unlimited undo/redo and erase
- Auto-highlighting of the selected cell's row, column and box, and of matching digits and pencil marks
- Rule conflicts always shown, including notes that clash with a placed number (drawn in red); checking against the solution can be turned on in Settings
- Three-step hints (name the technique, explain it, apply it) that build on your notes and show only the steps needed; dismiss a hint with the Dismiss button or Esc
- Timer that pauses when the game is paused or the app is minimized/backgrounded; autosave and resume
- Personal bests per difficulty (overall and without hints), averages and totals

## Projects
| Project | Purpose |
|---|---|
| `src/Sudoku.Core` | All game logic, no UI code: model, logical solver/grader, generator, hints, session/undo, persistence, stats |
| `src/Sudoku.UI` | Shared Avalonia UI: views, view models, the board control |
| `src/Sudoku.Desktop` | Windows (and macOS/Linux) desktop entry point |
| `src/Sudoku.iOS` | iOS entry point (not in `Sudoku.sln`; needs a Mac to build) |
| `tests/Sudoku.Core.Tests` | MSTest tests for Core |

## Build and run (Windows)
```bash
dotnet test tests/Sudoku.Core.Tests
dotnet run --project src/Sudoku.Desktop
```

Save files go in `%LOCALAPPDATA%\Sudoku`.

## Keyboard
1–9 pick a number, like the pad (Shift+digit for corner notes, Ctrl+digit for center notes) · arrow keys move ·
Enter place the picked number in the selected cell · Delete/Backspace erase · Ctrl+Z / Ctrl+Y undo/redo · H hint ·
Esc dismiss the hint (or un-pick the number) · Space switch entry mode · P pause

## iOS
The iOS head (`src/Sudoku.iOS`) uses the same `Sudoku.UI` and `Sudoku.Core`. Building it needs:
- a Mac with Xcode, either your own or paired from Visual Studio, or a macOS CI runner
- the .NET `ios` workload
- an Apple Developer account for running on a device and publishing to the App Store

On the Mac:
```bash
dotnet build src/Sudoku.iOS -c Release
```
