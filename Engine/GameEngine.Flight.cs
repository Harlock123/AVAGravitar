using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AVAGravitar.Models;

namespace AVAGravitar.Engine;

// Flying: the ship's controls, gravity, crashing into things, bullets and explosions.
public partial class GameEngine
{
    public const double TurnRate = 4.4;          // radians a second
    public const double ThrustAccel = 250;       // px/s²
    private const double MaxSpeed = 430;
    private const double Drag = 0.1;             // a touch, so the ship can be brought to a hover
    private const double ThrustBurn = 90;        // fuel a second
    private const double ShieldBurn = 230;
    public const double ShotSpeed = 520;
    private const double ShotLife = 1.0;
    private const int MaxShots = 5;
    private const double FireRepeat = 0.2;
    public const double EnemyShotSpeed = 250;

    private bool _wasFire;
    private double _fireRepeat;

    /// <summary>The pull on the ship here: the star in space, the planet's gravity on a planet.</summary>
    public Vector GravityAt(Point p)
    {
        if (Scene == Scene.Planet && Planet is { } planet)
            return new Vector(0, planet.Gravity * (System.Reversed && planet.Kind != PlanetKind.Reactor ? -1 : 1));
        var toStar = Field.Sub(SolarSystem.Star, p);
        double r = Math.Max(toStar.Length, 1);
        return toStar / r * Math.Min(400, SolarSystem.StarPull / (r * r));
    }

    private void Fly(Controls c, double deltaTime)
    {
        if (c.Aim is { } aim)
            Ship.Angle += Math.Clamp(Field.AngleDiff(Ship.Angle, aim), -TurnRate * deltaTime, TurnRate * deltaTime);
        else
            Ship.Angle += Math.Clamp(c.Turn, -1, 1) * TurnRate * deltaTime;
        Ship.Angle = Field.AngleDiff(0, Ship.Angle);

        bool thrust = c.Thrust && Fuel > 0;
        bool shield = c.Shield && Fuel > 0;
        if (thrust != Thrusting)
        {
            if (thrust) Sound.StartThrust(); else Sound.StopThrust();
            Thrusting = thrust;
        }
        if (shield != Shielding)
        {
            if (shield) Sound.StartShield(); else Sound.StopShield();
            Shielding = shield;
        }
        if (thrust) Burn(ThrustBurn * deltaTime);
        if (shield) Burn(ShieldBurn * deltaTime);

        // Fire: one shot a press, or a steady stream while held.
        _fireRepeat -= deltaTime;
        if (c.Fire && (!_wasFire || _fireRepeat <= 0))
            FireShot();
        _wasFire = c.Fire;

        var accel = GravityAt(Ship.Position);
        if (thrust)
        {
            accel += Ship.Nose * ThrustAccel;
            if (Random.NextDouble() < 0.7)
                Sparks.Add(new Spark
                {
                    Position = Ship.World(new Point(0, 9)),
                    Velocity = Ship.Velocity - Ship.Nose * (120 + Random.NextDouble() * 80) +
                               new Vector(Random.NextDouble() - 0.5, Random.NextDouble() - 0.5) * 60,
                    Life = 0.25, MaxLife = 0.25, Color = Palette.Flame
                });
        }
        Ship.Velocity += accel * deltaTime;
        Ship.Velocity *= 1 - Drag * deltaTime;
        if (Ship.Velocity.Length > MaxSpeed)
            Ship.Velocity = Ship.Velocity / Ship.Velocity.Length * MaxSpeed;

        // Small steps, so a fast ship can't skip through a thin bit of rock.
        int steps = Math.Clamp((int)Math.Ceiling(Ship.Velocity.Length * deltaTime / 3), 1, 12);
        double dt = deltaTime / steps;
        for (int i = 0; i < steps; i++)
        {
            var before = Ship.Touch.Select(Ship.World).ToArray();
            Ship.Position += Ship.Velocity * dt;
            if (ShipHitsSomething(before))
            {
                CrashShip("rock");
                return;
            }
        }
        if (Planet is { } p)
            Ship.Position = p.WrapPoint(Ship.Position);
    }

    private bool ShipHitsSomething(Point[] before)
    {
        if (Scene == Scene.Space)
            return Field.Distance(Ship.Position, SolarSystem.Star) < SolarSystem.StarRadius + 6;

        var planet = Planet!;
        var after = Ship.Touch.Select(Ship.World).ToArray();
        foreach (var (a, b, _) in planet.RockNear(Ship.Position, 40))
        {
            for (int k = 0; k < after.Length; k++)
            {
                if (Field.DistanceToSegment(after[k], a, b) < 1.5) return true;
                if (Field.Crosses(before[k], after[k], a, b)) return true;
            }
            // A sharp peak poking between the touch points.
            for (int k = 0; k < Ship.Hull.Length; k++)
            {
                var h1 = Ship.World(Ship.Hull[k]);
                var h2 = Ship.World(Ship.Hull[(k + 1) % Ship.Hull.Length]);
                if (Field.Crosses(h1, h2, a, b)) return true;
            }
        }
        if (planet.Kind == PlanetKind.Reactor && !_coreHit &&
            Field.Distance(Ship.Position, planet.Core) < Planet.CoreRadius + Ship.Radius)
            return true;
        return false;
    }

    /// <summary>What destroyed the last ship lost: rock, shot, saucer or reactor.</summary>
    public string DeathCause { get; private set; } = "";

    private void CrashShip(string cause)
    {
        if (!Ship.Alive) return;
        Ship.Alive = false;
        DeathCause = cause;
        Explode(Ship.Position, Palette.Ship, 26, big: true);
        Shake = 0.8;
        StopShipSounds();
        Sound.PlayShipBoom();
        ShipLost();
        if (_reactorTimer > 0)
            ResetReactor();
    }

    private void FireShot()
    {
        if (!Ship.Alive || Bullets.Count(b => b.FromPlayer) >= MaxShots) return;
        _fireRepeat = FireRepeat;
        Bullets.Add(new Bullet
        {
            Position = Ship.Muzzle, Velocity = Ship.Nose * ShotSpeed + Ship.Velocity * 0.6,
            Life = ShotLife, FromPlayer = true
        });
        Sound.PlayFire();
    }

    private void EnemyShot(Point from, Vector direction)
    {
        Bullets.Add(new Bullet
        {
            Position = from, Velocity = direction / Math.Max(direction.Length, 1e-6) * EnemyShotSpeed,
            Life = 2.6, FromPlayer = false
        });
        Sound.PlayEnemyFire();
    }

    /// <summary>The way from one point to another in the current scene (round the planet if it wraps).</summary>
    public Vector Between(Point from, Point to) => Planet is { } p ? p.Delta(from, to) : Field.Sub(to, from);

    private void UpdateBullets(double deltaTime)
    {
        var planet = Planet;
        foreach (var b in Bullets)
        {
            if (b.Dead) continue;
            b.Life -= deltaTime;
            if (b.Life <= 0)
            {
                b.Dead = true;
                continue;
            }
            var from = b.Position;
            b.Position += b.Velocity * deltaTime;

            if (planet != null)
            {
                if (planet.Blocked(from, b.Position))
                {
                    b.Dead = true;
                    Sparkle(b.Position, b.FromPlayer ? Palette.Shot : Palette.EnemyShot, 5);
                    continue;
                }
                b.Position = planet.WrapPoint(b.Position);
            }
            else
            {
                // Space wraps at the screen edges; the star swallows shots.
                b.Position = new Point((b.Position.X + Field.Width) % Field.Width, (b.Position.Y + Field.Height) % Field.Height);
                if (Field.Distance(b.Position, SolarSystem.Star) < SolarSystem.StarRadius)
                {
                    b.Dead = true;
                    continue;
                }
            }

            if (b.FromPlayer)
                PlayerShotHits(b);
            else if (Ship.Alive && Between(Ship.Position, b.Position).Length < 11)
            {
                b.Dead = true;
                if (Shielding)
                    Sparkle(b.Position, Palette.Shield, 8);
                else
                    CrashShip("shot");
            }
        }
        Bullets.RemoveAll(b => b.Dead);
    }

    private void PlayerShotHits(Bullet b)
    {
        foreach (var s in Saucers)
        {
            if (!s.Alive || Between(s.Position, b.Position).Length > Saucer.Radius + 3) continue;
            b.Dead = true;
            s.Alive = false;
            AddScore(Saucer.Points);
            Explode(s.Position, Palette.Saucer, 16, big: false);
            Sound.PlayBoom();
            return;
        }
        if (Planet is not { } planet) return;

        foreach (var k in planet.Bunkers)
        {
            if (!k.Alive || Between(k.Position + k.Normal * 8, b.Position).Length > 16) continue;
            b.Dead = true;
            k.Alive = false;
            AddScore(k.Points);
            Explode(k.Position + k.Normal * 8, k.Kind == BunkerKind.Twin ? Palette.Twin : Palette.Bunker, 12, big: false);
            Sound.PlayBoom();
            if (planet.Cleared)
            {
                ShowBanner("ALL BUNKERS DESTROYED", $"LEAVE THE PLANET FOR A {Body!.Bonus} BONUS", 3.5);
                Sound.PlayCleared();
            }
            return;
        }
        foreach (var t in planet.Tanks)
        {
            if (!t.Alive || Between(t.Position + t.Normal * 8, b.Position).Length > 14) continue;
            b.Dead = true;
            t.Alive = false;
            Explode(t.Position, Palette.Tank, 10, big: false);
            Sound.PlayBoom();
            return;
        }
        if (planet.Kind == PlanetKind.Reactor && !_coreHit &&
            Field.Distance(planet.Core, b.Position) < Planet.CoreRadius + 4)
        {
            b.Dead = true;
            HitCore();
        }
    }

    // ----- Explosions -----

    private void Explode(Point at, Color color, int pieces, bool big)
    {
        double speed = big ? 160 : 110;
        for (int i = 0; i < pieces; i++)
        {
            double len = (big ? 6 : 4) + Random.NextDouble() * (big ? 12 : 8);
            double life = 0.8 + Random.NextDouble() * (big ? 1.4 : 0.8);
            Shards.Add(new Shard
            {
                Position = at,
                Velocity = Field.FromAngle(Random.NextDouble() * Math.PI * 2) * speed * (0.3 + Random.NextDouble()),
                A = new Point(-len / 2, 0), B = new Point(len / 2, 0),
                Angle = Random.NextDouble() * Math.PI * 2, Spin = (Random.NextDouble() - 0.5) * 12,
                Life = life, MaxLife = life, Color = color
            });
        }
        Sparkle(at, Palette.Shot, big ? 30 : 14);
        if (big) Flash = Math.Max(Flash, 0.15);
    }

    private void Sparkle(Point at, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double life = 0.3 + Random.NextDouble() * 0.5;
            Sparks.Add(new Spark
            {
                Position = at,
                Velocity = Field.FromAngle(Random.NextDouble() * Math.PI * 2) * (40 + Random.NextDouble() * 160),
                Life = life, MaxLife = life, Color = color
            });
        }
    }

    private void UpdateEffects(double deltaTime)
    {
        // Debris falls the way the ship does: down on a planet, towards the star in space.
        foreach (var s in Shards)
        {
            s.Life -= deltaTime;
            s.Velocity += GravityAt(s.Position) * deltaTime * 0.5;
            s.Position += s.Velocity * deltaTime;
            s.Angle += s.Spin * deltaTime;
        }
        Shards.RemoveAll(s => s.Life <= 0);
        foreach (var s in Sparks)
        {
            s.Life -= deltaTime;
            s.Position += s.Velocity * deltaTime;
            s.Velocity *= 1 - 2 * deltaTime;
        }
        Sparks.RemoveAll(s => s.Life <= 0);
    }
}
