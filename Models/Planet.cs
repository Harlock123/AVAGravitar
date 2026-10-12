using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace AVAGravitar.Models;

public enum PlanetKind { Hills, Spires, Islands, Canyon, Reactor }

/// <summary>One straight piece of rock. Normal points out into the open air.</summary>
public sealed class Segment
{
    public Point A, B;
    public Vector Normal;
    public double MinX => Math.Min(A.X, B.X);
    public double MaxX => Math.Max(A.X, B.X);
}

/// <summary>
/// A planet seen side-on: rock, the bunkers and fuel tanks sitting on it, and its pull. Most planets wrap
/// round (fly far enough right and you're back where you started); the reactor is a closed cave.
/// </summary>
public sealed class Planet
{
    public const double DefaultHeight = 1150;

    public PlanetKind Kind { get; }
    public double Width { get; private set; }
    public double Height { get; private set; } = DefaultHeight;
    public bool Wraps { get; private set; } = true;
    public double Gravity { get; set; }
    public Color Color { get; private set; }
    public List<Segment> Rock { get; } = new();
    public List<Bunker> Bunkers { get; } = new();
    public List<FuelTank> Tanks { get; } = new();

    /// <summary>The reactor's core, in the reactor cave.</summary>
    public Point Core { get; private set; }
    public const double CoreRadius = 24;

    /// <summary>Where the ship comes in, at the top.</summary>
    public double EntryX { get; private set; }

    public bool Cleared => Kind != PlanetKind.Reactor && Bunkers.All(b => !b.Alive);

    // Flat pieces of ground for bunkers and tanks: where, and which way is out.
    private readonly List<(Point At, Vector Normal)> _pads = new();

    private Planet(PlanetKind kind) => Kind = kind;

    // ----- Wrapping -----

    /// <summary>The copy of a point (shifted by whole planet widths) nearest to x.</summary>
    public Point Near(Point p, double x)
    {
        if (!Wraps) return p;
        double shift = Math.Round((x - p.X) / Width) * Width;
        return new Point(p.X + shift, p.Y);
    }

    public Point WrapPoint(Point p)
    {
        if (!Wraps) return p;
        double x = p.X % Width;
        if (x < 0) x += Width;
        return new Point(x, p.Y);
    }

    /// <summary>The way from one point to another, the short way round.</summary>
    public Vector Delta(Point from, Point to) => Field.Sub(Near(to, from.X), from);

    /// <summary>The rock segments near a point, each shifted to the copy closest to it.</summary>
    public IEnumerable<(Point A, Point B, Vector Normal)> RockNear(Point p, double range)
    {
        foreach (var s in Rock)
        {
            var a = Near(s.A, p.X);
            var b = a + Field.Sub(s.B, s.A);
            if (Math.Min(a.X, b.X) - range > p.X || Math.Max(a.X, b.X) + range < p.X) continue;
            if (Math.Min(a.Y, b.Y) - range > p.Y || Math.Max(a.Y, b.Y) + range < p.Y) continue;
            yield return (a, b, s.Normal);
        }
    }

    /// <summary>The highest rock surface at x (smallest y), or the planet's floor if there's none.</summary>
    public double Skyline(double x)
    {
        double best = Height;
        foreach (var s in Rock)
        {
            var a = Near(s.A, x);
            var b = a + Field.Sub(s.B, s.A);
            double lo = Math.Min(a.X, b.X), hi = Math.Max(a.X, b.X);
            if (x < lo || x > hi) continue;
            double y = hi - lo < 1e-6 ? Math.Min(a.Y, b.Y) : a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
            best = Math.Min(best, y);
        }
        return best;
    }

    /// <summary>Whether a straight line passes through rock.</summary>
    public bool Blocked(Point from, Point to)
    {
        var mid = new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);
        double range = Field.Distance(from, to) / 2 + 2;
        foreach (var (a, b, _) in RockNear(mid, range))
            if (Field.Crosses(from, to, a, b)) return true;
        return false;
    }

    // ----- Making planets -----

    private static readonly Color[] Colors =
    {
        Color.FromRgb(70, 230, 90), Color.FromRgb(230, 90, 255), Color.FromRgb(255, 200, 60),
        Color.FromRgb(60, 220, 255), Color.FromRgb(255, 120, 90), Color.FromRgb(160, 255, 60)
    };

    public static Planet Make(PlanetKind kind, int seed, int bunkers, int tanks)
    {
        var rng = new Random(seed);
        var p = new Planet(kind) { Color = Colors[(seed / 7 + (int)kind) % Colors.Length] };
        switch (kind)
        {
            case PlanetKind.Hills: p.MakeHills(rng); break;
            case PlanetKind.Spires: p.MakeSpires(rng); break;
            case PlanetKind.Islands: p.MakeIslands(rng); break;
            case PlanetKind.Canyon: p.MakeCanyon(rng); break;
            case PlanetKind.Reactor:
                p.MakeReactor();
                return p;
        }
        p.PlaceThings(rng, bunkers, tanks);
        return p;
    }

    // A ground line across the whole planet, joined end to end.
    private void AddGround(List<Point> points)
    {
        points.Add(new Point(points[0].X + Width, points[0].Y));
        for (int i = 0; i + 1 < points.Count; i++)
            AddSegment(points[i], points[i + 1]);
    }

    private void AddSegment(Point a, Point b)
    {
        var d = Field.Sub(b, a);
        double len = d.Length;
        if (len < 1e-6) return;
        // Travelling left to right, (dy, −dx) points up: out of ground, out of a clockwise island.
        Rock.Add(new Segment { A = a, B = b, Normal = new Vector(d.Y / len, -d.X / len) });
    }

    // A closed loop of rock. Normals face away from the loop's inside when `outward`, into it otherwise.
    private void AddLoop(IReadOnlyList<Point> points, bool outward, bool closed = true)
    {
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        // Clockwise on screen (positive area) already has outward normals.
        bool flip = (area > 0) != outward;
        int count = closed ? points.Count : points.Count - 1;
        for (int i = 0; i < count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            if (flip) AddSegment(b, a); else AddSegment(a, b);
        }
    }

    // A flat pad at x, y: two points 56 apart, recorded for a bunker or tank.
    private void Pad(List<Point> points, double x, double y)
    {
        points.Add(new Point(x - 28, y));
        points.Add(new Point(x + 28, y));
        _pads.Add((new Point(x, y), new Vector(0, -1)));
    }

    private void MakeHills(Random rng)
    {
        Width = 2600;
        Gravity = 55;
        double phase = rng.NextDouble() * 10;
        double Y(double x) => Height - 330 + Math.Sin(x / Width * 2 * Math.PI * 3 + phase) * 140 +
                              Math.Sin(x / Width * 2 * Math.PI * 7 + phase * 2) * 60;
        var pts = new List<Point>();
        double x0 = 0;
        int n = 0;
        while (x0 < Width - 120)
        {
            if (n % 3 == 1)
                Pad(pts, x0 + 28, Y(x0 + 28) + rng.Next(-10, 10));
            else
                pts.Add(new Point(x0, Y(x0) + rng.Next(-40, 40)));
            x0 += 70 + rng.Next(50);
            n++;
        }
        AddGround(pts);
    }

    private void MakeSpires(Random rng)
    {
        Width = 2800;
        Gravity = 45;
        var pts = new List<Point>();
        double x = 0;
        while (x < Width - 420)
        {
            // A valley floor with a pad, then a spire, sometimes with a pad on a shoulder.
            double floor = Height - 90 - rng.Next(80);
            pts.Add(new Point(x, floor - rng.Next(20)));
            Pad(pts, x + 80, floor);
            x += 160;
            double peak = Height - 450 - rng.Next(250);
            if (rng.Next(2) == 0)
            {
                double shoulder = (floor + peak) / 2 + 40;
                pts.Add(new Point(x + 10, shoulder + 20));
                Pad(pts, x + 50, shoulder);
                x += 80;
            }
            pts.Add(new Point(x + 40, peak + 30));
            pts.Add(new Point(x + 60, peak));
            pts.Add(new Point(x + 85, peak + 60));
            x += 150;
        }
        AddGround(pts);
    }

    private void MakeIslands(Random rng)
    {
        Width = 3000;
        Gravity = 60;
        var pts = new List<Point>();
        for (double x = 0; x < Width - 100; x += 100)
        {
            if ((int)(x / 100) % 4 == 2)
                Pad(pts, x + 28, Height - 110 - rng.Next(40));
            else
                pts.Add(new Point(x, Height - 90 - rng.Next(70)));
        }
        AddGround(pts);

        // Floating rocks: a flat top with a pad, a ragged underside, sometimes a pad hanging underneath.
        for (int i = 0; i < 5; i++)
        {
            double cx = 250 + i * Width / 5 + rng.Next(-60, 60);
            double top = Height - 650 + rng.Next(250);
            double half = 110 + rng.Next(60);
            var loop = new List<Point>
            {
                new(cx - half, top + 25), new(cx - 28, top), new(cx + 28, top), new(cx + half, top + 30),
                new(cx + half * 0.6, top + 90), new(cx + 28, top + 120 + rng.Next(30)), new(cx - 28, top + 120 + rng.Next(30)),
                new(cx - half * 0.7, top + 85)
            };
            // Level the underside under the middle so something can hang there.
            loop[6] = new Point(loop[6].X, loop[5].Y);
            AddLoop(loop, outward: true);
            _pads.Add((new Point(cx, top), new Vector(0, -1)));
            if (i % 2 == 1)
                _pads.Add((new Point(cx, loop[5].Y), new Vector(0, 1)));
        }
    }

    private void MakeCanyon(Random rng)
    {
        Width = 2600;
        Gravity = 50;
        var pts = new List<Point>();
        double x = 0;
        double plateau = Height - 520;
        while (x < Width - 600)
        {
            // Plateau with a pad, then a stepped canyon down to a floor and back up.
            pts.Add(new Point(x, plateau + rng.Next(-30, 30)));
            Pad(pts, x + 90, plateau + rng.Next(-20, 20));
            pts.Add(new Point(x + 170, plateau + rng.Next(-20, 20)));
            double floor = Height - 80;
            double ledge = plateau + 220;
            pts.Add(new Point(x + 200, ledge - 30));
            Pad(pts, x + 240, ledge);
            pts.Add(new Point(x + 290, floor - 10));
            Pad(pts, x + 360, floor);
            pts.Add(new Point(x + 440, floor - 20));
            pts.Add(new Point(x + 470, plateau + 80));
            x += 520 + rng.Next(80);
        }
        AddGround(pts);
    }

    private void PlaceThings(Random rng, int bunkers, int tanks)
    {
        var pads = _pads.OrderBy(_ => rng.Next()).ToList();
        int i = 0;
        foreach (var (at, normal) in pads)
        {
            if (i < bunkers)
            {
                Bunkers.Add(new Bunker
                {
                    Position = at, Normal = normal,
                    Kind = rng.Next(3) == 0 ? BunkerKind.Twin : BunkerKind.Turret,
                    Cooldown = 1 + rng.NextDouble() * 2
                });
            }
            else if (i < bunkers + tanks && normal.Y < 0)
            {
                Tanks.Add(new FuelTank { Position = at, Home = at, Normal = normal });
            }
            else continue;
            i++;
        }
        // The ship comes in over open ground, away from the guns.
        EntryX = Enumerable.Range(0, 26).Select(k => k * Width / 26)
            .OrderByDescending(x => Bunkers.Min(b => Math.Abs(Delta(new Point(x, 0), b.Position).X)))
            .First();
    }

    // The reactor: a man-made shaft that winds down to the core. Straight walls, guns on the ledges.
    private void MakeReactor()
    {
        Width = Field.Width;
        Height = 1400;
        Wraps = false;
        Gravity = 45;
        Color = Color.FromRgb(255, 120, 60);
        var air = new Point[]
        {
            new(452, -200), new(452, 260), new(150, 260), new(150, 760), new(744, 760), new(744, 1060),
            new(330, 1060), new(330, 1340), new(900, 1340), new(900, 1060), new(874, 1060), new(874, 630),
            new(290, 630), new(290, 390), new(572, 390), new(572, -200)
        };
        // Open at the top: the shaft's two walls rise above the screen.
        AddLoop(air, outward: false, closed: false);
        Core = new Point(615, 1340 - CoreRadius);
        EntryX = 512;

        void Gun(double x, double y, Vector n, BunkerKind kind) =>
            Bunkers.Add(new Bunker { Position = new Point(x, y), Normal = n, Kind = kind, Cooldown = 1.5 });
        Gun(400, 390, new Vector(0, -1), BunkerKind.Turret);
        Gun(220, 260, new Vector(0, 1), BunkerKind.Turret);
        Gun(520, 630, new Vector(0, 1), BunkerKind.Twin);
        Gun(150, 520, new Vector(1, 0), BunkerKind.Turret);
        Gun(874, 900, new Vector(-1, 0), BunkerKind.Turret);
        Gun(420, 1340, new Vector(0, -1), BunkerKind.Twin);
        Gun(830, 1340, new Vector(0, -1), BunkerKind.Turret);
        Tanks.Add(new FuelTank { Position = new Point(450, 760), Home = new Point(450, 760), Normal = new Vector(0, -1) });
    }
}
