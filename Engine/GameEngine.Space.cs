using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using AVAGravitar.Models;

namespace AVAGravitar.Engine;

// The solar system: the star's pull, flying into planets, the saucer that hunts the ship out here, and
// the warp to the next system once every planet and the reactor are gone.
public partial class GameEngine
{
    private const double WarpLength = 3.5;
    private double _saucerTimer;
    private double _warpTimer;

    /// <summary>How far through the warp to the next system, 0 to 1.</summary>
    public double WarpProgress => Scene == Scene.Warp ? 1 - _warpTimer / WarpLength : 0;

    /// <summary>Bodies blown up recently, for the rings they leave in space: where, and how long ago.</summary>
    public List<(Point At, double Age)> Blasts { get; } = new();

    private void EnterSystem(SolarSystem system)
    {
        System = system;
        Scene = Scene.Space;
        Body = null;
        Camera = default;
        Bullets.Clear();
        Saucers.Clear();
        Blasts.Clear();
        ResetReactor();
        _saucerTimer = 14;
        Ship.Alive = true;
        PlaceAtHome();

        int universe = SolarSystem.Universe(system.Number);
        string sub = (universe % 3) switch
        {
            1 => "GRAVITY IS REVERSED",
            2 => "THE GROUND IS INVISIBLE",
            _ => "DESTROY THE PLANETS AND THE REACTOR"
        };
        ShowBanner($"UNIVERSE {universe + 1}  SYSTEM {system.Number % 3 + 1}", sub, 3);
        Sound.PlayTune();
    }

    private void PlaceAtHome()
    {
        Scene = Scene.Space;
        Camera = default;
        Ship.Position = SolarSystem.Home;
        Ship.Velocity = default;
        Ship.Angle = 0;
        // A saucer sitting on the home base gets a moment's grace.
        foreach (var s in Saucers.Where(s => Field.Distance(s.Position, SolarSystem.Home) < 200))
            s.Position = new Point(Field.Width - 80, 80);
    }

    private void UpdateSpace(double deltaTime)
    {
        foreach (var i in Enumerable.Range(0, Blasts.Count))
            Blasts[i] = (Blasts[i].At, Blasts[i].Age + deltaTime);
        Blasts.RemoveAll(b => b.Age > 2.5);

        if (Ship.Alive)
        {
            // Space wraps round at the edges.
            Ship.Position = new Point((Ship.Position.X + Field.Width) % Field.Width, (Ship.Position.Y + Field.Height) % Field.Height);

            var body = System.Bodies.FirstOrDefault(b => !b.Destroyed &&
                                                         Field.Distance(b.Position, Ship.Position) < b.Radius + 8);
            if (body != null)
            {
                EnterPlanet(body);
                return;
            }
        }

        // The saucer comes out after a while, and again a while after it's shot down.
        if (Saucers.Count(s => s.Alive) == 0 && Ship.Alive)
        {
            _saucerTimer -= deltaTime;
            if (_saucerTimer <= 0)
            {
                _saucerTimer = 16;
                var corner = new Point(Ship.Position.X < Field.Width / 2 ? Field.Width - 40 : 40, Ship.Position.Y < Field.Height / 2 ? Field.Height - 40 : 40);
                Saucers.Add(new Saucer { Position = corner, Cooldown = 2 });
            }
        }
        UpdateSaucers(deltaTime, 110 + System.Number * 8);
    }

    private void UpdateSaucers(double deltaTime, double speed)
    {
        var planet = Planet;
        bool any = false;
        foreach (var s in Saucers)
        {
            if (!s.Alive) continue;
            any = true;
            s.Spin += deltaTime * 5;
            var to = Ship.Alive ? Between(s.Position, Ship.Position) : Field.FromAngle(Clock * 0.7) * 100;
            var want = to / Math.Max(to.Length, 1) * speed + new Vector(Math.Sin(Clock * 2.3), Math.Cos(Clock * 1.7)) * 40;
            if (planet == null)
            {
                // Keep clear of the star.
                var fromStar = Field.Sub(s.Position, SolarSystem.Star);
                if (fromStar.Length < 120)
                    want += fromStar / fromStar.Length * 200;
            }
            s.Velocity += (want - s.Velocity) * Math.Min(1, 1.5 * deltaTime);
            s.Position += s.Velocity * deltaTime;

            if (planet != null)
            {
                double floor = planet.Skyline(s.Position.X) - 45;
                s.Position = planet.WrapPoint(new Point(s.Position.X, Math.Clamp(s.Position.Y, 30, Math.Max(30, floor))));
            }
            else
            {
                s.Position = new Point((s.Position.X + Field.Width) % Field.Width, (s.Position.Y + Field.Height) % Field.Height);
            }

            if (!Ship.Alive) continue;
            s.Cooldown -= deltaTime;
            if (s.Cooldown <= 0 && to.Length < 520)
            {
                s.Cooldown = Math.Max(1.0, 2.2 - System.Number * 0.1);
                EnemyShot(s.Position, Rotate(to + Ship.Velocity * (to.Length / EnemyShotSpeed) * 0.5, (Random.NextDouble() - 0.5) * 0.25));
            }
            if (to.Length < Saucer.Radius + 10)
            {
                s.Alive = false;
                Explode(s.Position, Palette.Saucer, 16, big: false);
                Sound.PlayBoom();
                if (Shielding)
                    AddScore(Saucer.Points);
                else
                    CrashShip("saucer");
            }
        }
        Saucers.RemoveAll(s => !s.Alive);
        if (any != _saucerHumming)
        {
            if (any) Sound.StartSaucer(); else Sound.StopSaucer();
            _saucerHumming = any;
        }
    }

    private bool _saucerHumming;

    public static Vector Rotate(Vector v, double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return new Vector(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    private void StartWarp()
    {
        Scene = Scene.Warp;
        _warpTimer = WarpLength;
        Bullets.Clear();
        Saucers.Clear();
        StopShipSounds();
        Sound.StopAllLoops();
        _saucerHumming = false;
        Sound.PlayWarp();
    }

    private void UpdateWarp(double deltaTime)
    {
        _warpTimer -= deltaTime;
        if (_warpTimer <= 0)
            EnterSystem(new SolarSystem(System.Number + 1));
    }
}
