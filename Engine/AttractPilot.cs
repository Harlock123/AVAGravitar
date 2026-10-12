using System;
using System.Linq;
using Avalonia;
using AVAGravitar.Models;

namespace AVAGravitar.Engine;

/// <summary>
/// Flies the ship in attract mode. It works out the push it wants (towards a hover point, with gravity
/// cancelled), turns the nose that way and thrusts once lined up. Over a bunker it stops, turns the gun
/// down and fires, then rights itself before it falls too far. Through the reactor it follows the shaft.
/// </summary>
public class AttractPilot
{
    private static readonly Random Random = new();

    private enum Mode { Fly, Aim, Recover }
    private Mode _mode;
    private double _modeTimer;
    private Body? _lastBody;
    private int _waypoint;

    // Targets it has given up on (out of reach), and how long it has been after the current one.
    private readonly System.Collections.Generic.HashSet<object> _skip = new();
    private readonly System.Collections.Generic.HashSet<Body> _givenUp = new();
    private object? _target;
    private double _targetTime;
    private double _dt;

    // The reactor shaft's middle, top to bottom, and where to hover to shoot the core.
    private static readonly Point[] ReactorPath =
    {
        new(512, 90), new(512, 325), new(220, 325), new(220, 695), new(809, 695), new(809, 1150), new(660, 1200)
    };

    public void Reset()
    {
        _mode = Mode.Fly;
        _modeTimer = 0;
        _lastBody = null;
        _waypoint = 0;
        _skip.Clear();
        _givenUp.Clear();
        _target = null;
    }

    public Controls Fly(GameEngine e, double deltaTime)
    {
        if (e.Scene == Scene.Warp) return default;
        if (!e.Ship.Alive)
        {
            // A new ship starts from the top (or from home): start the route again.
            _mode = Mode.Fly;
            _waypoint = 0;
            return default;
        }
        _modeTimer -= deltaTime;
        _dt = deltaTime;
        if (e.Body != _lastBody)
        {
            // Left a planet without destroying it: try the others first.
            if (_lastBody != null && !_lastBody.Destroyed)
                _givenUp.Add(_lastBody);
            _lastBody = e.Body;
            _mode = Mode.Fly;
            _waypoint = 0;
        }
        bool shield = Danger(e);
        var c = e.Scene == Scene.Space ? FlySpace(e)
            : e.Planet!.Kind == PlanetKind.Reactor ? FlyReactor(e)
            : FlyPlanet(e);
        return c with { Shield = c.Shield || shield };
    }

    // An enemy shot closing in: raise the shield.
    private static bool Danger(GameEngine e) =>
        e.Fuel > 600 && e.Bullets.Any(b => !b.FromPlayer && IsClosing(e, b));

    private static bool IsClosing(GameEngine e, Bullet b)
    {
        var rel = e.Between(e.Ship.Position, b.Position);
        return rel.Length < 75 && Field.Dot(rel, b.Velocity - e.Ship.Velocity) < 0;
    }

    /// <summary>Turns towards the push wanted and thrusts once the nose is near enough.</summary>
    private static Controls Steer(GameEngine e, Vector want, bool fire = false)
    {
        var thrust = want - e.GravityAt(e.Ship.Position);
        if (thrust.Length < 25)
            return new Controls(0, Field.AngleOf(-e.GravityAt(e.Ship.Position) + new Vector(0, -1e-3)), false, fire, false);
        double aim = Field.AngleOf(thrust);
        bool on = Math.Abs(Field.AngleDiff(e.Ship.Angle, aim)) < 0.4;
        return new Controls(0, aim, on, fire, false);
    }

    /// <summary>The push that brings the ship to a point and stops it there, at no more than maxSpeed.</summary>
    private static Vector Towards(GameEngine e, Point target, double maxSpeed)
    {
        var err = e.Between(e.Ship.Position, target);
        // Slow enough to stop in time, allowing for turning the ship round to brake.
        double dist = err.Length;
        double speed = Math.Min(maxSpeed, Math.Min(dist * 1.4, Math.Sqrt(2 * 100 * Math.Max(0, dist - 10))));
        var vWant = dist < 1e-6 ? default : err / dist * speed;
        var push = (vWant - e.Ship.Velocity) * 2.2;
        if (push.Length > 210) push = push / push.Length * 210;
        return push;
    }

    // ----- Space -----

    private Controls FlySpace(GameEngine e)
    {
        var ship = e.Ship;
        var targets = e.System.Bodies.Where(b => !b.Destroyed).ToList();
        // Everything left is out of its reach: patrol near home and shoot saucers instead.
        if (targets.Count > 0 && targets.All(_givenUp.Contains)) targets.Clear();
        // Planets first, nearest first; the reactor last; anything it gave up on after the rest.
        var body = targets.OrderBy(b => _givenUp.Contains(b)).ThenBy(b => b.Kind == PlanetKind.Reactor)
            .ThenBy(b => Field.Distance(b.Position, ship.Position)).FirstOrDefault();

        // A saucer close by gets shot at first.
        var saucer = e.Saucers.FirstOrDefault(s => Field.Distance(s.Position, ship.Position) < 330);
        if (saucer != null && ship.Velocity.Length < 140)
        {
            var to = Field.Sub(saucer.Position, ship.Position);
            var lead = to + saucer.Velocity * (to.Length / GameEngine.ShotSpeed);
            double aim = Field.AngleOf(lead);
            var fall = e.GravityAt(ship.Position);
            if (fall.Length < 60)
                return new Controls(0, aim, false, Math.Abs(Field.AngleDiff(ship.Angle, aim)) < 0.12, false);
        }

        if (body == null)
            return Steer(e, Towards(e, new Point(200, 520), 100));

        // Go round the star, and round any other planet in the way, rather than into them.
        var dest = body.Position;
        var closest = Field.Closest(SolarSystem.Star, ship.Position, dest);
        if (Field.Distance(closest, SolarSystem.Star) < 150)
        {
            var side = Field.Sub(closest, SolarSystem.Star);
            if (side.Length < 1) side = new Vector(1, 0);
            dest = SolarSystem.Star + side / side.Length * 230;
        }
        foreach (var other in targets.Where(b => b != body))
        {
            var near = Field.Closest(other.Position, ship.Position, dest);
            double gap = Field.Distance(near, other.Position);
            if (gap < other.Radius + 45 && Field.Distance(ship.Position, other.Position) > other.Radius + 10)
            {
                var side = Field.Sub(near, other.Position);
                if (side.Length < 1) side = new Vector(-(dest.Y - ship.Position.Y), dest.X - ship.Position.X);
                dest = other.Position + side / side.Length * (other.Radius + 80);
                break;
            }
        }
        double speed = Field.Distance(ship.Position, body.Position) < 140 ? 90 : 170;
        return Steer(e, Towards(e, dest, speed));
    }

    // ----- Planets -----

    private Controls FlyPlanet(GameEngine e)
    {
        var planet = e.Planet!;
        var ship = e.Ship;
        bool reversed = e.GravityAt(ship.Position).Y < 0;

        // Too close to the ground: climb, whatever else is going on.
        double groundBelow = Math.Min(planet.Skyline(ship.Position.X - 30), Math.Min(planet.Skyline(ship.Position.X), planet.Skyline(ship.Position.X + 30)));
        if (ship.Position.Y > groundBelow - 60 || (ship.Velocity.Y > 110 && ship.Position.Y > groundBelow - 180))
        {
            _mode = Mode.Recover;
            _modeTimer = 0.6;
            return Steer(e, Towards(e, new Point(ship.Position.X + Sideways(planet, ship), groundBelow - 160), 140));
        }

        // Rock overhead (under a floating island): slide out from under it before climbing.
        double side = Sideways(planet, ship);
        if (side != 0 && ship.Velocity.Y < 40)
            return Steer(e, Towards(e, new Point(ship.Position.X + side, ship.Position.Y), 140));

        // Only things on top of the rock with open sky above: nothing hanging underneath, nothing under an island.
        bool Open(Point p, Vector n) => n.Y < -0.3 && planet.Skyline(p.X) > p.Y - 20 && !_skip.Contains(p);
        var bunker = planet.Bunkers.Where(b => b.Alive && Open(b.Position, b.Normal))
            .OrderBy(b => Math.Abs(planet.Delta(ship.Position, b.Position).X)).FirstOrDefault();
        var tank = planet.Tanks.Where(t => t.Alive && Open(t.Home, t.Normal))
            .OrderBy(t => Math.Abs(planet.Delta(ship.Position, t.Position).X)).FirstOrDefault();

        // A saucer close by: turn and shoot it, if the ship isn't falling.
        var saucer = e.Saucers.FirstOrDefault(s => planet.Delta(ship.Position, s.Position).Length < 320);
        if (saucer != null && Math.Abs(ship.Velocity.Y) < 80 && ship.Position.Y < groundBelow - 140)
        {
            var toSaucer = planet.Delta(ship.Position, saucer.Position);
            var lead = toSaucer + saucer.Velocity * (toSaucer.Length / GameEngine.ShotSpeed);
            double aim = Field.AngleOf(lead);
            return new Controls(0, aim, false, Math.Abs(Field.AngleDiff(ship.Angle, aim)) < 0.12, false);
        }

        bool wantFuel = tank != null && (e.Fuel < 4000 ||
            (e.Fuel < 8000 && (bunker == null || Math.Abs(planet.Delta(ship.Position, tank.Position).X) < Math.Abs(planet.Delta(ship.Position, bunker.Position).X))));

        // Stuck on one target too long (it can't be reached): give up on it.
        object? target = wantFuel ? tank!.Home : bunker?.Position;
        if (!Equals(target, _target))
        {
            _target = target;
            _targetTime = 0;
        }
        _targetTime += _dt;
        if (target != null && _targetTime > 25)
        {
            _skip.Add(target);
            _target = null;
        }

        if (wantFuel)
        {
            var over = tank!.Home + new Vector(0, -70);
            var d = planet.Delta(ship.Position, tank.Position);
            var c = Steer(e, Travel(e, planet, over, 0));
            bool inBeam = d.Y > 15 && d.Y < 150 && Math.Abs(d.X) < 22 + d.Y * 0.3;
            return c with { Shield = inBeam };
        }

        if (bunker == null || reversed && e.Fuel < 1500)
            return Steer(e, Towards(e, new Point(ship.Position.X, -80), 160));

        var hover = bunker.Position + bunker.Normal * 170;
        var toBunker = planet.Delta(ship.Position, bunker.Position);

        if (_mode == Mode.Aim)
        {
            bool falling = reversed ? ship.Velocity.Y < -60 : ship.Velocity.Y > 60;
            if (_modeTimer <= 0 || falling || ship.Position.Y > groundBelow - 110)
            {
                _mode = Mode.Recover;
                _modeTimer = 0.9;
            }
            else
            {
                double aim = Field.AngleOf(toBunker + new Vector(0, -8));
                bool lined = Math.Abs(Field.AngleDiff(ship.Angle, aim)) < 0.08;
                return new Controls(0, aim, false, lined, false);
            }
        }

        var to = planet.Delta(ship.Position, hover);
        if (_mode != Mode.Aim && _modeTimer <= 0 && to.Length < 70 && ship.Velocity.Length < 60 &&
            !planet.Blocked(ship.Position, ship.Position + toBunker + bunker.Normal * 8))
        {
            _mode = Mode.Aim;
            _modeTimer = 1.5;
        }
        if (_mode == Mode.Recover && _modeTimer <= 0) _mode = Mode.Fly;
        return Steer(e, Travel(e, planet, hover, 0));
    }

    /// <summary>
    /// If there's rock just above the ship, which way to move to get out from under it (the nearer open side);
    /// 0 if the sky above is clear.
    /// </summary>
    private static double Sideways(Planet planet, Ship ship)
    {
        bool Clear(double dx) => !planet.Blocked(ship.Position + new Vector(dx, 0), ship.Position + new Vector(dx, -170));
        if (Clear(0) && Clear(-14) && Clear(14)) return 0;
        for (double dx = 40; dx <= 400; dx += 40)
        {
            if (Clear(dx) && Clear(dx + 20)) return dx + 40;
            if (Clear(-dx) && Clear(-dx - 20)) return -dx - 40;
        }
        return 0;
    }

    /// <summary>The push towards a point on a planet, staying high enough to clear the ground in between.</summary>
    private static Vector Travel(GameEngine e, Planet planet, Point target, double extra)
    {
        var ship = e.Ship;
        var d = planet.Delta(ship.Position, target);
        double sky = planet.Skyline(ship.Position.X);
        int samples = (int)Math.Ceiling(Math.Abs(d.X) / 30);
        for (int i = 0; i <= samples; i++)
            sky = Math.Min(sky, planet.Skyline(ship.Position.X + d.X * i / Math.Max(1, samples)));
        sky = Math.Min(sky, planet.Skyline(ship.Position.X + d.X + 50));
        sky = Math.Min(sky, planet.Skyline(ship.Position.X + d.X - 50));
        double cruise = Math.Max(40, sky - 140 - extra);
        var goal = ship.Position + d;
        if (Math.Abs(d.X) > 110)
            goal = new Point(ship.Position.X + Math.Sign(d.X) * Math.Min(Math.Abs(d.X), 220), Math.Min(cruise, goal.Y));
        else
            goal = new Point(goal.X, Math.Min(goal.Y, Math.Max(40, Math.Min(planet.Skyline(goal.X - 40), planet.Skyline(goal.X + 40)) - 100)));
        return Towards(e, goal, Math.Abs(d.X) > 110 || d.Length > 160 ? 170 : 90);
    }

    // ----- The reactor -----

    private Controls FlyReactor(GameEngine e)
    {
        var ship = e.Ship;
        var planet = e.Planet!;
        if (e.CoreHit)
        {
            // Back the way it came, and out of the top.
            if (_waypoint >= ReactorPath.Length) _waypoint = ReactorPath.Length - 1;
            var wp = _waypoint < 0 ? new Point(512, -120) : ReactorPath[_waypoint];
            if (Field.Distance(ship.Position, wp) < 35 && _waypoint >= 0) _waypoint--;
            return Steer(e, Towards(e, wp, _waypoint < 0 ? 200 : 125));
        }

        if (_waypoint < ReactorPath.Length - 1)
        {
            var wp = ReactorPath[_waypoint];
            if (Field.Distance(ship.Position, wp) < 40) _waypoint++;
            return Steer(e, Towards(e, ReactorPath[_waypoint], 170));
        }

        // At the bottom: hold still over the core and shoot it.
        var spot = ReactorPath[^1];
        var toCore = Field.Sub(planet.Core, ship.Position);
        if (Field.Distance(ship.Position, spot) < 40 && ship.Velocity.Length < 50 && ship.Velocity.Y < 40)
        {
            double aim = Field.AngleOf(toCore);
            return new Controls(0, aim, false, Math.Abs(Field.AngleDiff(ship.Angle, aim)) < 0.1, false);
        }
        return Steer(e, Towards(e, spot, 90));
    }
}
