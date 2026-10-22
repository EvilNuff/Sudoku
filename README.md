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

## iOS (build on a Mac, run on an iPhone)
The iOS head (`src/Sudoku.iOS`, bundle id `com.goodnuff.sudoku`) uses the same `Sudoku.UI` and `Sudoku.Core`. It isn't in
`Sudoku.sln` because it can only be built on macOS.

### One-time setup on the Mac
1. Install **Xcode** from the Mac App Store, open it once and accept the license. The .NET iOS workload needs a matching
   Xcode (26.x for the current workload); if a build reports a version mismatch, install the version it names.
2. Install the **.NET 10 SDK** (macOS Arm64) from https://dot.net.
3. `sudo dotnet workload install ios`
4. Xcode → Settings → Accounts → **+** → sign in with your Apple ID (this creates a free "Personal Team").

### Get the code
```bash
brew install gh
gh auth login
gh repo clone Sudoku
cd Sudoku
```

Optional check in the Simulator (no signing needed):
```bash
dotnet build src/Sudoku.iOS -t:Run -f net10.0-ios
```

### Prepare the iPhone
1. Connect it by cable, unlock it and tap **Trust This Computer**.
2. Settings → Privacy & Security → **Developer Mode** → On (the phone restarts).

### Create the signing profile (free Apple ID)
In Xcode: File → New → Project → iOS App, with bundle identifier exactly `com.goodnuff.sudoku` and your Personal Team.
Pick your iPhone as the run target and press Run once, so Xcode creates the provisioning profile. Close that project.

### Build and install
1. Find the phone's id: `xcrun devicectl list devices` (or Xcode → Window → Devices and Simulators).
2. Build and run:
   ```bash
   dotnet build src/Sudoku.iOS -t:Run -f net10.0-ios -p:RuntimeIdentifier=ios-arm64 -p:_DeviceName=<device-id>
   ```
3. First time only, on the phone: Settings → General → **VPN & Device Management** → your Apple ID → Trust.

With a free Apple ID the app stops launching after 7 days; run the build command again to refresh it. A paid Apple
Developer account ($99/year) makes installs last a year and enables TestFlight and the App Store. To update, `git pull`
and run the build command again.
