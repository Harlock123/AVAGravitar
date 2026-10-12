using System;
using System.Collections.Generic;
using Avalonia;
using AVAGravitar.Models;

namespace AVAGravitar.Engine;

public enum GameState
{
    StartScreen,
    Playing,
    Paused,
    GameOver,
    Attract,
    EnterInitials,
    HighScores
}

/// <summary>Where the ship is: out among the planets, down on one, or warping between systems.</summary>
public enum Scene { Space, Planet, Warp }

/// <summary>One frame's worth of flying, from the player or the attract pilot.</summary>
public readonly record struct Controls(double Turn, double? Aim, bool Thrust, bool Fire, bool Shield);

/// <summary>
/// The game: the ship and its fuel, the solar system (GameEngine.Space), the planets and the reactor
/// (GameEngine.Planet), the physics they share (GameEngine.Flight), scoring, and the title, attract and
/// high-score cycle around it.
/// </summary>
public partial class GameEngine : IDisposable
{
    private static readonly Random Random = new();

    public const int StartFuel = 10000;
    public const int StartShips = 6;
    public const int ExtraShipEvery = 10000;
    public const int SystemFuelBonus = 7500;
    private const double RespawnDelay = 2.6;

    private const double GameOverInputDelay = 1.0;
    private const double AttractIdleDelay = 20.0;   // seconds on the title screen before the demo
    private const double AttractLength = 80.0;      // longest a demo runs
    private const double GameOverIdleDelay = 20.0;  // seconds on game over before the title returns
    private const double GameOverBeforeEntry = 2.5; // "GAME OVER" shows this long before initials entry
    private const double EntryInputGrace = 0.8;     // ignore input this long, so a mashed fire button doesn't enter letters
    private const double EntryTimeout = 45.0;       // idle this long in entry and the initials are taken as they are
    private const double HighScoresShowTime = 10.0; // the table stays up this long

    public GameState State { get; private set; }
    public int Score { get; private set; }
    public int Ships { get; private set; }
    public double Fuel { get; private set; }

    public Scene Scene { get; private set; }
    public SolarSystem System { get; private set; } = new(0);
    public Body? Body { get; private set; }
    public Planet? Planet => Scene == Scene.Planet ? Body?.Planet : null;

    public Ship Ship { get; } = new();
    public bool Thrusting { get; private set; }
    public bool Shielding { get; private set; }
    public List<Bullet> Bullets { get; } = new();
    public List<Saucer> Saucers { get; } = new();
    public List<Shard> Shards { get; } = new();
    public List<Spark> Sparks { get; } = new();

    /// <summary>The top-left of the view, in the current scene's coordinates.</summary>
    public Point Camera { get; private set; }

    public double Clock { get; private set; }
    public double Shake { get; private set; }
    public double Flash { get; private set; }
    public bool IsInPlay => State is GameState.Playing or GameState.Attract;

    /// <summary>A line or two across the middle of the screen for a while.</summary>
    public string Banner { get; private set; } = "";
    public string SubBanner { get; private set; } = "";
    public double BannerTimer { get; private set; }

    public HighScoreTable Table { get; } = HighScoreTable.Load();
    public int HighScore => Table.TopScore;
    public InitialsEntry? Entry { get; private set; }
    public int HighlightIndex { get; private set; } = -1;

    public InputHandler Input { get; } = new();
    public SoundManager Sound { get; } = new();

    /// <summary>When the game ended, for the "GAME OVER" animation; −1 while playing.</summary>
    public double EndClock { get; private set; } = -1;
    public string EndReason { get; private set; } = "";

    private double _respawnTimer;
    private int _nextExtraShip;

    private bool _wasStartPressed;
    private double _gameOverTimer;
    private bool _entryPending;
    private double _entryClock;
    private double _entryIdle;
    private double _padRepeat;
    private bool _wasPadUp, _wasPadDown, _wasPadLeft, _wasPadRight, _wasPadAccept, _wasPadDone, _wasLeftMouse;
    private double _tableTimer;
    private bool _tableLeadsToDemo;
    private double _idleTimer;
    private double _attractTimer;
    private bool _wasAnyInputLastFrame;
    private readonly AttractPilot _pilot = new();

    public GameEngine()
    {
        State = GameState.StartScreen;
        ResetForTitle();
    }

    public void Dispose()
    {
        // Closing the window mid-entry still keeps the score, under whatever initials are showing.
        if (_entryPending)
            CommitEntry();

        Sound.Dispose();
        Input.Dispose();
    }

    // ----- Starting, stopping and the attract cycle -----

    public void StartNewGame() => BeginRun(GameState.Playing);

    /// <summary>The game plays itself, silently, until it loses or time runs out.</summary>
    public void StartAttract()
    {
        BeginRun(GameState.Attract);
        _attractTimer = AttractLength;
    }

    public void ReturnToTitle()
    {
        State = GameState.StartScreen;
        Sound.StopAllLoops();
        Sound.IsSilenced = false;
        ResetForTitle();
        _idleTimer = 0;
        HighlightIndex = -1;
    }

    // The title screen shows the first solar system, empty of ships.
    private void ResetForTitle()
    {
        System = new SolarSystem(0);
        Scene = Scene.Space;
        Body = null;
        Camera = default;
        Ship.Alive = false;
        Bullets.Clear();
        Saucers.Clear();
        EndClock = -1;
        BannerTimer = 0;
    }

    private void BeginRun(GameState state)
    {
        State = state;
        Sound.StopAllLoops();
        Sound.IsSilenced = state == GameState.Attract;
        Score = 0;
        Ships = StartShips;
        Fuel = StartFuel;
        _nextExtraShip = ExtraShipEvery;
        EndClock = -1;
        EndReason = "";
        Shards.Clear();
        Sparks.Clear();
        _pilot.Reset();
        _wasFire = true;
        EnterSystem(new SolarSystem(0));
    }

    private void ShowBanner(string text, string sub = "", double seconds = 2.5)
    {
        Banner = text;
        SubBanner = sub;
        BannerTimer = seconds;
    }

    private void AddScore(int points)
    {
        Score += points;
        while (Score >= _nextExtraShip)
        {
            _nextExtraShip += ExtraShipEvery;
            Ships++;
            Sound.PlayExtraShip();
        }
    }

    /// <summary>Burns fuel; running dry ends the game, as on the cabinet.</summary>
    private void Burn(double amount)
    {
        bool wasLow = Fuel < 2000;
        Fuel = Math.Max(0, Fuel - amount);
        if (!wasLow && Fuel < 2000)
            Sound.PlayLowFuel();
    }

    private void GiveFuel(double amount) => Fuel += amount;

    private void ShipLost()
    {
        Ships--;
        _respawnTimer = RespawnDelay;
    }

    private void EndGame(string reason)
    {
        if (State is not (GameState.Playing or GameState.Attract)) return;
        EndClock = 0;
        EndReason = reason;
        Sound.StopAllLoops();
        if (State == GameState.Attract)
        {
            State = GameState.GameOver;
            _gameOverTimer = 4;
            _entryPending = false;
            _idleTimer = GameOverIdleDelay - 4;
            return;
        }
        State = GameState.GameOver;
        Sound.PlayGameOver();
        _gameOverTimer = GameOverInputDelay;
        _idleTimer = 0;
        _entryPending = Table.Qualifies(Score);
    }

    private void ShowHighScores(bool leadsToDemo)
    {
        State = GameState.HighScores;
        _tableTimer = HighScoresShowTime;
        _tableLeadsToDemo = leadsToDemo;
        if (leadsToDemo)
            HighlightIndex = -1;
    }

    private void BeginInitials()
    {
        State = GameState.EnterInitials;
        Entry = new InitialsEntry(Table.LastInitials);
        _entryClock = 0;
        _entryIdle = 0;
        _wasPadUp = _wasPadDown = _wasPadLeft = _wasPadRight = _wasPadAccept = _wasPadDone = true;
        _wasLeftMouse = true;
    }

    private void CommitEntry()
    {
        _entryPending = false;
        string initials = Entry?.Initials ?? Table.LastInitials;
        HighlightIndex = Table.Insert(initials, Score, System.Number + 1);
        Table.Save();
        Entry = null;
    }

    private bool EntryAcceptsInput => State == GameState.EnterInitials && _entryClock >= EntryInputGrace && Entry != null;

    /// <summary>Keyboard input during initials entry, passed straight from the window.</summary>
    public void InitialsKey(Avalonia.Input.Key key)
    {
        if (!EntryAcceptsInput) return;
        var entry = Entry!;
        _entryIdle = 0;

        if (key >= Avalonia.Input.Key.A && key <= Avalonia.Input.Key.Z)
            entry.Type((char)('A' + (key - Avalonia.Input.Key.A)));
        else if (key >= Avalonia.Input.Key.D0 && key <= Avalonia.Input.Key.D9)
            entry.Type((char)('0' + (key - Avalonia.Input.Key.D0)));
        else if (key >= Avalonia.Input.Key.NumPad0 && key <= Avalonia.Input.Key.NumPad9)
            entry.Type((char)('0' + (key - Avalonia.Input.Key.NumPad0)));
        else if (key is Avalonia.Input.Key.OemPeriod or Avalonia.Input.Key.Decimal)
            entry.Type('.');
        else if (key == Avalonia.Input.Key.Up)
            entry.Cycle(1);
        else if (key == Avalonia.Input.Key.Down)
            entry.Cycle(-1);
        else if (key == Avalonia.Input.Key.Left || key == Avalonia.Input.Key.Back)
            entry.Back();
        else if (key == Avalonia.Input.Key.Right)
            entry.Move(1);
        else if (key == Avalonia.Input.Key.Space)
            entry.Advance();
        else if (key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Escape)
            entry.Finish();
    }

    // Gamepad and mouse input during initials entry: up/down dial the letter (holding repeats),
    // left/right move, a fire button accepts a letter, Start finishes. The wheel dials, a click accepts.
    private void UpdateInitials(double deltaTime)
    {
        _entryClock += deltaTime;
        _entryIdle += deltaTime;
        var entry = Entry!;

        bool up = Input.PadUp, down = Input.PadDown, left = Input.PadLeft, right = Input.PadRight;
        bool accept = Input.PadAccept, done = Input.PadDone, click = Input.IsLeftMousePressed;

        if (EntryAcceptsInput)
        {
            if (up || down || left || right || accept || done || click || Input.MouseWheelDelta != 0)
                _entryIdle = 0;

            int dial = 0;
            if ((up && !_wasPadUp) || (down && !_wasPadDown))
            {
                dial = up ? 1 : -1;
                _padRepeat = 0.4;
            }
            else if (up || down)
            {
                _padRepeat -= deltaTime;
                if (_padRepeat <= 0)
                {
                    dial = up ? 1 : -1;
                    _padRepeat = 0.12;
                }
            }
            if (Input.MouseWheelDelta != 0)
                dial = Math.Sign(Input.MouseWheelDelta);
            if (dial != 0)
                entry.Cycle(dial);

            if (left && !_wasPadLeft) entry.Back();
            if (right && !_wasPadRight) entry.Move(1);
            if ((accept && !_wasPadAccept) || (click && !_wasLeftMouse)) entry.Advance();
            if (done && !_wasPadDone) entry.Finish();
        }

        _wasPadUp = up; _wasPadDown = down; _wasPadLeft = left; _wasPadRight = right;
        _wasPadAccept = accept; _wasPadDone = done; _wasLeftMouse = click;

        if (_entryIdle >= EntryTimeout)
            entry.Finish();

        if (entry.IsDone)
        {
            CommitEntry();
            ShowHighScores(leadsToDemo: false);
        }
    }

    // ----- The frame -----

    public void Update(double deltaTime)
    {
        Clock += deltaTime;
        bool start = Input.IsStartPressed;
        bool startPressed = start && !_wasStartPressed;
        _wasStartPressed = start;
        bool newInput = Input.IsAnyInput && !_wasAnyInputLastFrame;
        _wasAnyInputLastFrame = Input.IsAnyInput;
        Shake = Math.Max(0, Shake - deltaTime);
        Flash = Math.Max(0, Flash - deltaTime);
        BannerTimer = Math.Max(0, BannerTimer - deltaTime);

        switch (State)
        {
            case GameState.Paused:
                return;

            case GameState.StartScreen:
            case GameState.HighScores:
                UpdateEffects(deltaTime);
                if (startPressed)
                {
                    StartNewGame();
                    return;
                }
                if (State == GameState.StartScreen)
                {
                    _idleTimer = Input.IsAnyInput ? 0 : _idleTimer + deltaTime;
                    if (_idleTimer >= AttractIdleDelay)
                        ShowHighScores(leadsToDemo: true);
                    return;
                }
                if (newInput)
                {
                    ReturnToTitle();
                    return;
                }
                _tableTimer -= deltaTime;
                if (_tableTimer <= 0)
                {
                    if (_tableLeadsToDemo)
                        StartAttract();
                    else
                        ReturnToTitle();
                }
                return;

            case GameState.EnterInitials:
                UpdateEffects(deltaTime);
                UpdateInitials(deltaTime);
                return;

            case GameState.GameOver:
                EndClock += deltaTime;
                UpdateEffects(deltaTime);
                _gameOverTimer -= deltaTime;
                if (_entryPending)
                {
                    if (_gameOverTimer <= GameOverInputDelay - GameOverBeforeEntry)
                        BeginInitials();
                    return;
                }
                if (startPressed && _gameOverTimer <= 0)
                {
                    StartNewGame();
                    return;
                }
                _idleTimer = Input.IsAnyInput ? 0 : _idleTimer + deltaTime;
                if (_idleTimer >= GameOverIdleDelay)
                    ReturnToTitle();
                return;

            case GameState.Attract:
                if (startPressed)
                {
                    StartNewGame();
                    return;
                }
                if (newInput)
                {
                    ReturnToTitle();
                    return;
                }
                _attractTimer -= deltaTime;
                if (_attractTimer <= 0)
                {
                    ReturnToTitle();
                    return;
                }
                Step(_pilot.Fly(this, deltaTime), deltaTime);
                return;

            default:
                Step(PlayerControls(), deltaTime);
                return;
        }
    }

    public void Pause()
    {
        if (State == GameState.Playing)
        {
            State = GameState.Paused;
            Sound.StopAllLoops();
        }
    }

    public void Resume()
    {
        if (State == GameState.Paused)
        {
            State = GameState.Playing;
            _wasFire = true;
        }
    }

    private Controls PlayerControls()
    {
        double? aim = Input.StickAngle;
        if (aim == null && Input.MouseAiming && Ship.Alive)
        {
            var onScreen = new Point(Ship.Position.X - Camera.X, Ship.Position.Y - Camera.Y);
            var toPointer = Field.Sub(Input.Pointer, onScreen);
            if (toPointer.Length > 20)
                aim = Field.AngleOf(toPointer);
        }
        return new Controls(Input.Turn, aim, Input.IsThrusting, Input.IsFiring, Input.IsShielding);
    }

    /// <summary>The game's clock ticks: the ship flies, then the scene it's in.</summary>
    private void Step(Controls controls, double deltaTime)
    {
        if (Scene == Scene.Warp)
        {
            UpdateWarp(deltaTime);
            UpdateEffects(deltaTime);
            return;
        }

        if (Ship.Alive)
        {
            Fly(controls, deltaTime);
        }
        else
        {
            StopShipSounds();
            _respawnTimer -= deltaTime;
            if (_respawnTimer <= 0)
            {
                if (Ships <= 0)
                {
                    EndGame("NO SHIPS LEFT");
                    return;
                }
                Respawn();
            }
        }

        if (Scene == Scene.Space)
            UpdateSpace(deltaTime);
        else
            UpdatePlanet(deltaTime);

        UpdateBullets(deltaTime);
        UpdateEffects(deltaTime);

        if (Fuel <= 0 && Ship.Alive && State is GameState.Playing or GameState.Attract)
        {
            ShowBanner("OUT OF FUEL", "", 4);
            Explode(Ship.Position, Palette.Ship, 30, big: true);
            Ship.Alive = false;
            StopShipSounds();
            Sound.PlayShipBoom();
            EndGame("OUT OF FUEL");
        }
    }

    private void Respawn()
    {
        Bullets.Clear();
        Ship.Alive = true;
        Ship.Velocity = default;
        if (Scene == Scene.Planet && Body != null)
            PlaceAtPlanetTop();
        else
            PlaceAtHome();
    }

    private void StopShipSounds()
    {
        if (Thrusting) Sound.StopThrust();
        if (Shielding) Sound.StopShield();
        Thrusting = Shielding = false;
    }
}
