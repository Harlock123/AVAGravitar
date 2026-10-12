# AVAGravitar

A recreation of Atari's 1982 vector arcade game **Gravitar**, built with .NET 10 and Avalonia UI. Fly a
small ship through a solar system whose star drags you in. Dive into the planets, where gravity pulls you
towards jagged ground, and shoot out the bunkers firing up at you. Pull fuel tanks aboard with the tractor
beam, because when the fuel runs out the game is over. Fly down the reactor's shaft, shoot its core, and
get out before it blows. Clear every planet and the reactor and you warp to the next system. After three
systems gravity turns upside down, and after that the ground goes invisible.

## Features

- **The solar system**: a star at the centre whose pull grows the closer you get, four planets, a reactor,
  and a saucer that comes out to hunt you. Touch a planet to fly down to it.
- **Four kinds of planet**, side-on and wrapping round (fly far enough and you're back where you started):
  rolling **hills**, needle-sharp **spires**, **floating islands** with guns on top and hanging underneath,
  and a **canyon** world of plateaus and deep, stepped trenches.
- **Bunkers** sit on the rock and turn to track you. Turrets fire single shots and twin bunkers fire pairs,
  both leading their aim. Destroy every bunker on a planet and the whole planet blows up when you leave,
  for a bonus.
- **The reactor**: a winding, guarded shaft down to the core. One hit starts the countdown; you have
  seconds to climb back out.
- **Fuel**: thrust burns it, and the shield burns it faster. Running dry ends the game, however many ships
  you have left. **Fuel tanks** on the planets are worth 2,500 each, pulled up with the tractor beam.
- **Shield and tractor beam** on one button, as on the cabinet: it stops bullets and saucers, and lifts
  any fuel tank in the cone beneath the ship.
- **Saucers** hunt you in space, and come down after you on a planet if you take too long.
- **Universes**: system after system, the guns fire faster. After three systems comes universe 2, where
  **gravity is reversed** and the planets push you up and away. Universe 3 has **invisible ground**, lit
  only close to the ship.
- **Vector graphics**: every line glows like a colour vector monitor, and the rock has a soft glow behind
  its edge. There are tumbling debris, sparks, shockwave rings where planets were, and a starfield warp
  between systems.
- **Keyboard, mouse or gamepad**: the cabinet's rotate, thrust, fire and shield buttons. The mouse can
  also point the ship at the pointer, and a gamepad stick turns the ship to face where it's pushed.
- **Synthesised sound**: the thrust roar, the shield's hum, bunker fire, explosions, the fuel tank's glug,
  the reactor alarm, the saucer warble, the warp, and a short theme for each new system.
- **Attract mode**: leave the title screen alone for 20 seconds and a computer pilot plays. It clears
  planets, collects fuel, dodges shots with the shield, and threads the reactor shaft.
- **Top-ten high-score table** with arcade-style initials entry.
- **Any window size**: the 4:3 screen scales to fit, letterboxed. **Ctrl+S** saves a screenshot.
- **One self-contained executable** per platform: Windows, macOS and Linux on Intel/AMD and ARM.

## Screenshots

| Title screen | The solar system | Rolling hills |
|:------------:|:----------------:|:-------------:|
| ![Title screen](Images/AVAGravitar-title.png) | ![A saucer near the star](Images/AVAGravitar-space.png) | ![The hills planet](Images/AVAGravitar-hills.png) |
| The rules and the controls, over the solar system. | A saucer comes out to hunt the ship, near the star. | Bunkers firing up at the ship. |

| Spires | Floating islands | The canyon |
|:------:|:----------------:|:----------:|
| ![The spires planet](Images/AVAGravitar-spires.png) | ![The islands planet](Images/AVAGravitar-islands.png) | ![The canyon planet](Images/AVAGravitar-canyon.png) |
| Deep valleys between needle peaks. | Rocks hanging in the sky, with guns on them. | Plateaus and stepped trenches. |

| The tractor beam | The reactor | Escape! |
|:----------------:|:-----------:|:-------:|
| ![Pulling up a fuel tank](Images/AVAGravitar-tractor.png) | ![The reactor core](Images/AVAGravitar-reactor.png) | ![The reactor countdown](Images/AVAGravitar-escape.png) |
| Shield up, pulling a fuel tank aboard. | At the bottom of the shaft, lining up on the core. | The core is hit and the countdown is running. |

| A planet destroyed | Warp | Reversed gravity |
|:------------------:|:----:|:----------------:|
| ![A planet destroyed](Images/AVAGravitar-destroyed.png) | ![Warp to the next system](Images/AVAGravitar-warp.png) | ![Universe 2](Images/AVAGravitar-reversed.png) |
| Every bunker gone: the planet goes up as the ship leaves. | System cleared; on to the next. | Universe 2: the ship hangs nose-down against the pull. |

| Invisible ground | Entering initials | The high-score table |
|:----------------:|:-----------------:|:--------------------:|
| ![Universe 3](Images/AVAGravitar-invisible.png) | ![Entering initials](Images/AVAGravitar-initials.png) | ![The high-score table](Images/AVAGravitar-high-scores.png) |
| Universe 3: only the rock near the ship shows. | A score that makes the top ten earns three initials. | The new entry flashes in the table. |

## Download and run

Ready-to-run builds for Windows, macOS and Linux, on both Intel/AMD and ARM, are on the
[Releases page](https://github.com/Harlock123/AVAGravitar/releases). **Latest build** is rebuilt on every
push to `main`. Each download is one self-contained executable: no .NET install is needed.

```bash
tar -xzf AVAGravitar-linux-arm64.tar.gz     # or linux-x64, linux-arm, osx-arm64, osx-x64
./AVAGravitar
```

On Windows, unzip `AVAGravitar-win-x64.zip` (or `-win-arm64`, `-win-x86`) and run `AVAGravitar.exe`. On
macOS, if Gatekeeper refuses to open it, run `xattr -d com.apple.quarantine AVAGravitar` once.

### Linux: sound and gamepads

- **Sound** needs `aplay`, from the `alsa-utils` package. Without it the game runs but is
  silent. Minimal installs (Arch, Omarchy, some Raspberry Pi images) don't include it:

  ```bash
  sudo apt install alsa-utils             # Debian / Ubuntu / Raspberry Pi OS
  sudo pacman -S --needed alsa-utils      # Arch / Omarchy
  ```
- **Gamepads** use SDL2. It is bundled for Windows, macOS and Intel/AMD Linux, but **ARM Linux builds need it
  installed**. Without it the keyboard and mouse still work; only the controller does not:

  ```bash
  sudo apt install libsdl2-dev            # Debian / Ubuntu / Raspberry Pi OS
  sudo pacman -S --needed sdl2-compat     # Arch / Omarchy
  ```

  On Debian-based systems it has to be `libsdl2-dev`: the game looks for `libSDL2.so`, which
  the runtime-only `libsdl2-2.0-0` package doesn't provide.

## Requirements

- .NET 10 SDK (to build; the released executables need nothing)
- Windows, macOS or Linux

## Building and Running

```bash
cd AVAGRAVITAR
dotnet build
dotnet run
```

## Controls

The cabinet had five buttons: rotate left, rotate right, thrust, fire, and shield/tractor.

### Keyboard

| Key | Action |
|-----|--------|
| **Left** / **Right**, **A** / **D** | Rotate |
| **Up** / **W** | Thrust |
| **Space** / **Ctrl** | Fire (hold for a stream of shots) |
| **Down** / **S** / **Shift** | Shield and tractor beam |
| **Enter** | Start a game |
| **Esc** | Pause (Esc again to quit) |
| **M** | Mute all sound |

### Mouse

| Input | Action |
|-------|--------|
| **Move** | The ship turns to face the pointer (a rotate key hands control back to the keys) |
| **Left Button** | Fire |
| **Right Button** | Thrust |
| **Middle Button** | Shield and tractor beam |

### Controller (Xbox/PlayStation/Switch Pro)

| Input | Action |
|-------|--------|
| **Left Stick** | Turn the ship to face the way the stick is pushed |
| **D-Pad Left/Right** | Rotate |
| **A** / **Right Trigger** | Thrust |
| **X** / **B** / **Right Bumper** | Fire |
| **Y** / **Left Bumper** / **Left Trigger** | Shield and tractor beam |
| **Start** | Pause / Resume game |
| **Back/Select** (while paused) | Exit game |

### Entering initials

| Key | Action |
|-----|--------|
| **A**–**Z**, **0**–**9**, **.** | Type a letter and move to the next |
| **Up** / **Down** | Dial the current letter |
| **Left** / **Backspace**, **Right** | Move between letters |
| **Space** | Accept the letter |
| **Enter** | Done |

Gamepad: D-pad dials and moves, **X** accepts, **Start** finishes. Mouse: the wheel dials, a click accepts.

### Other

| Key | Action |
|-----|--------|
| **Ctrl+S** / **Cmd+S** | Save screenshot to Pictures folder |

## Gameplay

### Flying

The ship turns, thrusts and coasts. Nothing stops it but thrust, a little drag, and the pull: the star in
space, the planet on its surface. Touching rock, or the star, destroys it. Thrust is the only way to brake,
so turning the ship round in time is half the game.

### The solar system

Each ship starts on the home pad in the bottom-left corner. Each planet shows the bonus for destroying it.
Fly into one to go down to it, and fly off the top of the screen to come back out. A saucer turns up after
a while and keeps coming; shoot it, or ram it with the shield up.

### The planets

| Planet | What it's like |
|--------|----------------|
| **Hills** | Rolling ground; guns on the crests and in the dips |
| **Spires** | Narrow valleys between needle peaks, with guns on the valley floors and on ledges |
| **Islands** | Low ground with floating rocks; guns on their tops, and some hanging underneath |
| **Canyon** | High plateaus split by deep, stepped trenches |
| **Reactor** | A shaft winding down to the core, with guns on its ledges and walls |

Destroy every bunker and leave, and the planet is destroyed: its bonus is added and it's gone from the
system. Leave early and it waits for you, with the bunkers you destroyed still gone. At the reactor, one
shot at the core starts the countdown: climb out before it reaches zero or the ship goes up with it.

### Fuel

You start with 10,000 units. Thrust burns 90 a second and the shield 230. The fuel gauge flashes below
2,000. Each tank pulled aboard adds 2,500, and clearing a whole system adds 7,500. At zero, the game is
over.

### Universes

| Universe | Systems | The twist |
|----------|---------|-----------|
| 1 | 1–3 | Gravity pulls down |
| 2 | 4–6 | Gravity is reversed: the planets push you up and off the top |
| 3 | 7–9 | The ground is invisible except right round the ship |

Then round again, with more bunkers and faster guns each system.

## Scoring

| Target | Points |
|--------|--------|
| Bunker | 250 |
| Twin bunker | 500 |
| Saucer | 500 |
| Planet destroyed | 2,000 to 5,000 by kind, × the universe |
| Reactor destroyed | 5,000 × the universe |

You start with six ships and get an extra one every 10,000 points.

## Sound Effects

All effects are synthesised by `tools/make_sounds.py`; nothing is sampled from the original machine.

| Sound | When |
|-------|------|
| `Fire.wav`, `EnemyFire.wav` | The ship's gun, a bunker's or saucer's |
| `Boom.wav`, `ShipBoom.wav` | A bunker, tank or saucer destroyed; the ship lost |
| `PlanetBoom.wav`, `ReactorBoom.wav`, `CoreHit.wav` | A planet destroyed; the reactor blowing; the core hit |
| `Fuel.wav`, `LowFuel.wav` | A fuel tank aboard; fuel running low |
| `Cleared.wav` | The last bunker on a planet destroyed |
| `Enter.wav`, `Exit.wav`, `Warp.wav` | Diving into a planet, climbing out, warping to the next system |
| `Tune.wav`, `ExtraShip.wav`, `GameOver.wav` | A new system, an extra ship, the end |
| `Thrust.wav`, `Shield.wav`, `Saucer.wav`, `Alarm.wav` | Thrust, shield, a saucer about, the reactor countdown (loops) |

## Project Structure

```
AVAGRAVITAR/
├── AVAGravitar.csproj, AVAGravitar.slnx
├── Program.cs, App.axaml(.cs), MainWindow.axaml(.cs)
├── Engine/
│   ├── GameEngine.cs        # States, ships, fuel, scoring, the title/demo/high-score cycle
│   ├── GameEngine.Flight.cs # The ship's controls, gravity, collisions, bullets and explosions
│   ├── GameEngine.Space.cs  # The solar system, saucers and the warp between systems
│   ├── GameEngine.Planet.cs # Bunkers, the tractor beam, the reactor countdown, leaving a planet
│   ├── AttractPilot.cs      # The computer pilot for attract mode
│   ├── HighScoreTable.cs, InitialsEntry.cs
│   ├── InputHandler.cs      # Rotate, thrust, fire and shield from keyboard, mouse and gamepad
│   ├── ControllerManager.cs # SDL2 gamepad support
│   └── SoundManager.cs      # Effects and the looping sounds
├── Models/
│   ├── Geometry.cs          # The screen and vector helpers
│   ├── Entities.cs          # The ship, bullets, bunkers, fuel tanks, saucers, debris, colours
│   ├── Planet.cs            # Rock, wrapping, and the generators for each kind of planet and the reactor
│   └── SolarSystem.cs       # The star, the planets' places and bonuses, the universes
├── Views/
│   ├── GameCanvas.cs        # Input, game loop, overlays
│   ├── GameCanvas.Scene.cs  # Space, planets, the reactor, the ship, saucers, debris and the warp
│   ├── GameCanvas.Screens.cs# HUD, title, game over, entry and table screens
│   └── GameCanvas.Draw.cs   # Glowing lines, shapes and text
├── Sounds/                  # Generated WAV files
├── Images/                  # README screenshots
└── tools/
    └── make_sounds.py       # Synthesises the sound effects
```

## Technical Details

- **Framework**: Avalonia UI 12.1 on .NET 10
- **Audio**: NetCoreAudio for cross-platform sound playback
- **Controller**: SDL2 GameController API for cross-platform gamepad support
- **Terrain** is a set of straight rock segments, each with a normal pointing into the open air. Planets
  wrap horizontally, so every check uses the copy of a segment nearest the ship. The ship moves in small
  sub-steps and is tested against the rock by its nose, wingtips and tail, plus its hull edges, so it
  can't slip through a thin peak.
- **Planets are generated** from a seed per system: the same system always looks the same.
- **The attract pilot** works out the push it wants (towards a hover point, slowing so it can stop in time,
  with gravity cancelled out), turns towards it and thrusts when lined up. It hovers over a bunker, turns
  to fire, and rights itself before it falls too far. It slides out from under floating rocks and follows
  waypoints down the reactor shaft and back.
- **Rendering**: a custom `Control` with `DrawingContext`, every line drawn twice (wide and faint, then thin
  and bright) for the vector-monitor glow
- **Screen**: a fixed 1024×768 screen, scaled to fit any window size with letterboxing
- **Game Loop**: 60 FPS using `DispatcherTimer`
- **High scores**: saved to `AVAGravitar/highscores.txt` in the user's application-data folder
  (`~/.config` on Linux, `%APPDATA%` on Windows, `~/Library/Application Support` on macOS)

## Roadmap

### Done

- The solar system with its star's pull, four kinds of planet and the reactor
- Bunkers, saucers, fuel, the shield and tractor beam
- Three universes: normal, reversed gravity and invisible ground
- Keyboard, mouse-pointing and gamepad controls
- Synthesised sound, attract mode, top-ten high scores with initials, screenshots and mute
- Self-contained single-file releases for 8 platforms on every push to `main`

### Planned

- Zooming out on planets when the ship is high above the ground, as the cabinet did
- Two-player alternating games

## Credits

Gravitar was designed by Mike Hally, programmed by Rich Adam, and released by Atari in 1982. This is a
fan recreation of its gameplay; no code, graphics or sound from the original are used. Gravitar is a
trademark of its owner, and this project is not affiliated with or endorsed by it.

## License

MIT License
