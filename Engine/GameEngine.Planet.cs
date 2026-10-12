using System;
using System.Linq;
using Avalonia;
using AVAGravitar.Models;

namespace AVAGravitar.Engine;

// Down on a planet: the bunkers shooting up, the tractor beam lifting fuel, the saucer that comes for a
// ship that lingers, and the reactor's countdown. Flying off the top goes back out into space.
public partial class GameEngine
{
    private const double TractorReach = 160;
    private const double TractorPull = 120;
    private const double PlanetSaucerDelay = 35;

    private double _planetClock;
    private double _planetSaucerTimer;
    private double _reactorTimer;
    private bool _coreHit;

    /// <summary>Seconds left to get out of the reactor; 0 when it isn't counting.</summary>
    public double ReactorTimer => _reactorTimer;
    public bool CoreHit => _coreHit;

    private void EnterPlanet(Body body)
    {
        Body = body;
        Scene = Scene.Planet;
        Bullets.Clear();
        Saucers.Clear();
        ResetReactor();
        _planetClock = 0;
        _planetSaucerTimer = PlanetSaucerDelay;
        foreach (var t in body.Planet.Tanks.Where(t => t.Alive))
            t.Position = t.Home;
        PlaceAtPlanetTop();
        Sound.PlayEnter();
        if (body.Kind == PlanetKind.Reactor)
            ShowBanner("REACTOR", "SHOOT THE CORE, THEN GET OUT", 2.5);
    }

    private void PlaceAtPlanetTop()
    {
        var planet = Body!.Planet;
        Ship.Position = new Point(planet.EntryX, 70);
        Ship.Velocity = new Vector(0, System.Reversed && planet.Kind != PlanetKind.Reactor ? -10 : 20);
        Ship.Angle = System.Reversed && planet.Kind != PlanetKind.Reactor ? Math.PI : 0;
        UpdateCamera();
    }

    private void UpdatePlanet(double deltaTime)
    {
        var planet = Planet!;
        _planetClock += deltaTime;
        UpdateCamera();

        if (Ship.Alive && Ship.Position.Y < -12)
        {
            LeavePlanet();
            return;
        }

        double toughness = Math.Max(0.5, 1 - System.Number * 0.06);
        foreach (var k in planet.Bunkers)
        {
            if (!k.Alive) continue;
            k.Cooldown -= deltaTime;
            if (k.Cooldown > 0) continue;
            if (!Ship.Alive)
            {
                k.Cooldown = 0.5;
                continue;
            }
            var muzzle = k.Position + k.Normal * 14;
            var to = Between(muzzle, Ship.Position);
            double dist = to.Length;
            if (dist > 560 || Field.Dot(to / dist, k.Normal) < 0.1 || planet.Blocked(muzzle, muzzle + to))
            {
                k.Cooldown = 0.3;
                continue;
            }
            // Leads the ship a little, with a shaky aim.
            var aim = to + Ship.Velocity * (dist / EnemyShotSpeed) * 0.6;
            aim = Rotate(aim, (Random.NextDouble() - 0.5) * 0.28);
            if (k.Kind == BunkerKind.Twin)
            {
                EnemyShot(muzzle, Rotate(aim, -0.12));
                EnemyShot(muzzle, Rotate(aim, 0.12));
                k.Cooldown = 3.0 * toughness * (0.8 + Random.NextDouble() * 0.4);
            }
            else
            {
                EnemyShot(muzzle, aim);
                k.Cooldown = 2.4 * toughness * (0.8 + Random.NextDouble() * 0.4);
            }
        }

        UpdateTractor(deltaTime, planet);

        // Hang about too long and a saucer comes down after the ship.
        if (Ship.Alive && planet.Kind != PlanetKind.Reactor)
        {
            _planetSaucerTimer -= deltaTime;
            if (_planetSaucerTimer <= 0 && Saucers.Count == 0)
            {
                _planetSaucerTimer = 25;
                Saucers.Add(new Saucer { Position = planet.WrapPoint(new Point(Ship.Position.X + 500, 30)), Cooldown = 2 });
            }
        }
        UpdateSaucers(deltaTime, 120 + System.Number * 8);

        if (_reactorTimer > 0)
        {
            _reactorTimer -= deltaTime;
            Shake = Math.Max(Shake, 0.12);
            if (_reactorTimer <= 0)
            {
                // Too slow: the reactor goes up with the ship inside.
                _reactorTimer = 0;
                Flash = 0.6;
                Explode(planet.Core, Palette.Reactor, 40, big: true);
                Sound.PlayReactorBoom();
                CrashShip("reactor");
                ResetReactor();
            }
        }
    }

    private void UpdateTractor(double deltaTime, Planet planet)
    {
        foreach (var t in planet.Tanks)
        {
            if (!t.Alive) continue;
            t.Beamed = false;
            if (Shielding && Ship.Alive)
            {
                var d = Between(Ship.Position, t.Position);
                if (d.Y > 0 && d.Y < TractorReach && Math.Abs(d.X) < 28 + d.Y * 0.35)
                {
                    t.Beamed = true;
                    if (d.Length < 22)
                    {
                        t.Alive = false;
                        GiveFuel(FuelTank.Fuel);
                        Sound.PlayFuel();
                        ShowBanner($"+{FuelTank.Fuel} FUEL", "", 1.2);
                        continue;
                    }
                    t.Position = planet.WrapPoint(t.Position - d / d.Length * TractorPull * deltaTime);
                    continue;
                }
            }
            // Let go, a tank sinks back down to where it sat.
            var home = Between(t.Position, t.Home);
            if (home.Length > 1)
                t.Position = planet.WrapPoint(t.Position + home / home.Length * Math.Min(home.Length, 80 * deltaTime));
        }
    }

    private void UpdateCamera()
    {
        var planet = Body!.Planet;
        double x = planet.Wraps ? Ship.Position.X - Field.Width / 2 : 0;
        double y = Math.Clamp(Ship.Position.Y - Field.Height * 0.42, 0, Math.Max(0, planet.Height - Field.Height));
        Camera = new Point(x, y);
    }

    private void LeavePlanet()
    {
        var body = Body!;
        var planet = body.Planet;
        Bullets.Clear();
        Saucers.Clear();
        bool reactorDone = planet.Kind == PlanetKind.Reactor && _coreHit;
        ResetReactor();

        // Back out in space, just outside the planet, heading away from the star.
        var away = Field.Sub(body.Position, SolarSystem.Star);
        away /= Math.Max(away.Length, 1);
        Scene = Scene.Space;
        Camera = default;
        Ship.Position = body.Position + away * (body.Radius + 26);
        Ship.Velocity = away * 70;
        Ship.Angle = Field.AngleOf(away);
        Sound.PlayExit();

        if (planet.Cleared || reactorDone)
        {
            body.Destroyed = true;
            AddScore(body.Bonus);
            Blasts.Add((body.Position, 0));
            Explode(body.Position, planet.Color, 40, big: true);
            Shake = 0.6;
            Sound.PlayPlanetBoom();
            ShowBanner(reactorDone ? "REACTOR DESTROYED" : "PLANET DESTROYED", $"BONUS {body.Bonus}", 2.5);
        }
        Body = null;

        if (System.Cleared)
        {
            GiveFuel(SystemFuelBonus);
            ShowBanner("SYSTEM CLEARED", $"BONUS FUEL {SystemFuelBonus}", 3.2);
            StartWarp();
        }
    }

    private void HitCore()
    {
        _coreHit = true;
        _reactorTimer = Math.Max(14, 24 - System.Number);
        Explode(Planet!.Core, Palette.Reactor, 14, big: false);
        Flash = 0.25;
        Sound.PlayCoreHit();
        Sound.StartAlarm();
        ShowBanner("REACTOR CRITICAL", "GET OUT!", 2.5);
    }

    private void ResetReactor()
    {
        _coreHit = false;
        _reactorTimer = 0;
        Sound.StopAlarm();
    }
}
