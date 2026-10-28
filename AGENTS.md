# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
the downloads; this file covers how the code is put together and the rules that keep it working.

## What this is

A recreation of Atari's 1982 vector arcade game Gravitar, in C# on .NET 10 (`net10.0`) and Avalonia 12. It is
one project at the repository root, with no tests. The graphics are drawn in code, and the sound effects are
synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVAGravitar.slnx                 # there is no global.json: any .NET 10 SDK will do
dotnet run --project AVAGravitar.csproj       # play it; takes no command-line arguments
python3 tools/make_sounds.py                  # rebuild Sounds/ (standard-library Python only)
```

There are no tests yet.

## Layout

- `Program.cs`, `App.axaml`, `MainWindow.axaml`: the Avalonia start-up and the window that holds the canvas.
- `Engine/`: the game.
  - `GameEngine.cs` runs the states (title, play, attract, initials, high scores), scoring, lives and fuel.
  - `GameEngine.Flight.cs` moves the ship under gravity and handles crashes. `GameEngine.Space.cs` covers
    the solar system and the warp; `GameEngine.Planet.cs` covers bunkers, fuel tanks and the reactor.
  - `AttractPilot.cs` flies the ship in the attract-mode demonstration.
  - `InputHandler.cs` (keys and mouse) and `ControllerManager.cs` (SDL2 gamepads) reduce input to rotate,
    thrust, fire and shield.
  - `SoundManager.cs`, `HighScoreTable.cs` and `InitialsEntry.cs` do what their names say.
- `Models/`: `SolarSystem.cs` and `Planet.cs` build the systems and the planets' rock, `Entities.cs` holds the
  ship and other objects, and `Geometry.cs` has the fixed 1024x768 screen and vector helpers.
- `Views/`: `GameCanvas` (split over partial files) runs the timer, handles keys and draws every screen.
- `Sounds/`: the WAV files, copied to the output folder. `Images/`: the README's screenshots.

## Rules that keep it working

- **Frame time, not fixed steps.** `GameCanvas` ticks a `DispatcherTimer` and passes the real elapsed time,
  capped at 0.1 s, to `GameEngine.Update`. Movement and timers must scale by that `deltaTime`.
- **Worlds are seeded, the rest is not.** `SolarSystem` and `Planet.Make` build their layouts from
  `new Random(seed)`, so a system and its planets come out the same every time; keep that. Enemy aim, debris
  and the attract pilot use unseeded `Random` instances.
- **Angles.** The ship's angle 0 points straight up and positive turns clockwise (`Geometry.cs`).
  `InputHandler.StickAngle` turns the stick into that convention with `Atan2(x, -y)`, since the stick's `y` is
  negative when pushed up. Keep new controls on the same convention.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. Keep new SDL
  calls inside it.
- **Audio.** `SoundManager` plays WAV files with NetCoreAudio, which needs `aplay` on Linux. The attract mode
  silences it without touching the player's mute setting.
- **Files.** High scores are saved in the app-data folder (`SpecialFolder.ApplicationData`, `AVAGravitar`).
  Ctrl+S saves screenshots to the Pictures folder.
- **Line endings.** Keep each file's existing line endings (some files, such as `Program.cs` and
  `ControllerManager.cs`, are CRLF) so diffs show only real changes.

## Making a change

- Build with no new warnings, and play the change, since there are no tests to catch it.
- Update `README.md` (features, controls, screenshots in `Images/`) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds every push and pull request (it runs no tests). It then publishes
self-contained single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64, kept as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. A release also asks the portfolio site to rebuild.
