using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AVAGravitar.Engine;
using AVAGravitar.Models;

namespace AVAGravitar.Views;

// The solar system, the planets and the warp between systems, all in glowing vector lines.
public partial class GameCanvas
{
    private static readonly (Point At, double Bright)[] Stars = MakeStars();

    private static (Point, double)[] MakeStars()
    {
        var rng = new Random(1982);
        return Enumerable.Range(0, 140)
            .Select(_ => (new Point(rng.NextDouble() * ScreenWidth, rng.NextDouble() * ScreenHeight), 0.2 + rng.NextDouble() * 0.6))
            .ToArray();
    }

    private void DrawScene(DrawingContext context)
    {
        switch (_engine.Scene)
        {
            case Scene.Warp:
                DrawWarp(context);
                break;
            case Scene.Planet:
                DrawPlanet(context);
                break;
            default:
                DrawSpace(context, withShip: true);
                break;
        }
        if (_engine.Flash > 0)
            context.FillRectangle(GetBrush(Colors.White, Math.Min(0.6, _engine.Flash * 1.5)), new Rect(0, 0, ScreenWidth, ScreenHeight));
    }

    private void DrawStarfield(DrawingContext context, double alpha = 1)
    {
        foreach (var (at, bright) in Stars)
        {
            double twinkle = 0.75 + 0.25 * Math.Sin(Clock * 2 + at.X);
            context.FillRectangle(GetBrush(Colors.White, bright * twinkle * alpha), new Rect(at.X, at.Y, 1.6, 1.6));
        }
    }

    // ----- Space -----

    private void DrawSpace(DrawingContext context, bool withShip)
    {
        DrawStarfield(context);
        DrawSun(context);

        // Home base: a little landing pad.
        var home = SolarSystem.Home;
        DrawGlowLine(context, home + new Vector(-22, 22), home + new Vector(22, 22), Palette.Ship, 0.6, 1.5);
        DrawGlowLine(context, home + new Vector(-16, 22), home + new Vector(-10, 28), Palette.Ship, 0.5, 1.2);
        DrawGlowLine(context, home + new Vector(16, 22), home + new Vector(10, 28), Palette.Ship, 0.5, 1.2);

        foreach (var body in _engine.System.Bodies.Where(b => !b.Destroyed))
            DrawBody(context, body);

        foreach (var (at, age) in _engine.Blasts)
        {
            double t = age / 2.5;
            for (int r = 0; r < 3; r++)
                DrawCircle(context, at, 10 + t * (120 + r * 40), Palette.Star, (1 - t) * 0.7, 2, 24);
        }

        if (withShip && _engine.IsInPlay || _engine.State is GameState.Paused)
            DrawActors(context, null);
        else
            DrawDebris(context, null);
    }

    private void DrawSun(DrawingContext context)
    {
        var c = SolarSystem.Star;
        double pulse = 1 + 0.06 * Math.Sin(Clock * 3);
        context.DrawEllipse(GetBrush(Palette.Star, 0.08), null, c, 90 * pulse, 90 * pulse);
        context.DrawEllipse(GetBrush(Palette.Star, 0.15), null, c, 55 * pulse, 55 * pulse);
        DrawCircle(context, c, SolarSystem.StarRadius * pulse, Palette.Star, 1, 2.5, 28);
        // Flares turning slowly round it.
        for (int i = 0; i < 12; i++)
        {
            double a = Clock * 0.4 + i * Math.PI / 6;
            double len = 14 + 8 * Math.Sin(Clock * 4 + i * 1.7);
            var dir = Field.FromAngle(a);
            DrawGlowLine(context, c + dir * (SolarSystem.StarRadius + 5), c + dir * (SolarSystem.StarRadius + 5 + len),
                Color.FromRgb(255, 160, 50), 0.8, 1.6);
        }
    }

    private void DrawBody(DrawingContext context, Body body)
    {
        var p = body.Position;
        var color = body.Planet.Color;
        if (body.Kind == PlanetKind.Reactor)
        {
            // The reactor: an armoured octagon with a pulsing core.
            var oct = Enumerable.Range(0, 8).Select(i => p + Field.FromAngle(i * Math.PI / 4 + Math.PI / 8) * body.Radius).ToArray();
            DrawShape(context, oct, Colors.Black, 0.9, Palette.Reactor, 1, 2);
            double core = 7 + 2 * Math.Sin(Clock * 6);
            DrawCircle(context, p, core, Colors.White, 0.9, 1.8, 12);
            for (int i = 0; i < 4; i++)
            {
                var d = Field.FromAngle(i * Math.PI / 2 + Clock);
                DrawGlowLine(context, p + d * (core + 3), p + d * (body.Radius - 4), Palette.Reactor, 0.7, 1.4);
            }
        }
        else
        {
            context.DrawEllipse(GetBrush(Colors.Black, 0.85), null, p, body.Radius, body.Radius);
            DrawCircle(context, p, body.Radius, color, 1, 2.2, 30);
            switch (body.Kind)
            {
                case PlanetKind.Hills:
                    // Rolling hills across its face.
                    for (int k = -1; k <= 1; k++)
                    {
                        double y = k * body.Radius * 0.45;
                        double half = Math.Sqrt(body.Radius * body.Radius - y * y) - 3;
                        var pts = Enumerable.Range(0, 7).Select(i => p + new Vector(-half + i * half / 3, y + Math.Sin(i * 1.7 + k) * 4)).ToArray();
                        for (int i = 0; i + 1 < pts.Length; i++) DrawGlowLine(context, pts[i], pts[i + 1], color, 0.5, 1.2);
                    }
                    break;
                case PlanetKind.Spires:
                    for (int i = -2; i <= 2; i++)
                    {
                        double x = i * body.Radius * 0.35;
                        double bottom = Math.Sqrt(Math.Max(0, body.Radius * body.Radius - x * x)) - 3;
                        DrawGlowLine(context, p + new Vector(x - 5, bottom), p + new Vector(x, bottom - body.Radius * (0.7 + 0.3 * (i & 1))), color, 0.6, 1.2);
                        DrawGlowLine(context, p + new Vector(x + 5, bottom), p + new Vector(x, bottom - body.Radius * (0.7 + 0.3 * (i & 1))), color, 0.6, 1.2);
                    }
                    break;
                case PlanetKind.Islands:
                    // A ring, tilted.
                    DrawEllipseLines(context, p, body.Radius * 1.7, body.Radius * 0.45, -0.3, color, 0.8);
                    DrawGlowLine(context, p + new Vector(-12, -6), p + new Vector(10, -6), color, 0.5, 1.2);
                    DrawGlowLine(context, p + new Vector(-6, 8), p + new Vector(14, 8), color, 0.5, 1.2);
                    break;
                case PlanetKind.Canyon:
                    DrawGlowLine(context, p + new Vector(-body.Radius + 4, -4), p + new Vector(-8, -4), color, 0.6, 1.2);
                    DrawGlowLine(context, p + new Vector(-8, -4), p + new Vector(-4, 14), color, 0.6, 1.2);
                    DrawGlowLine(context, p + new Vector(-4, 14), p + new Vector(6, 14), color, 0.6, 1.2);
                    DrawGlowLine(context, p + new Vector(6, 14), p + new Vector(10, -4), color, 0.6, 1.2);
                    DrawGlowLine(context, p + new Vector(10, -4), p + new Vector(body.Radius - 4, -4), color, 0.6, 1.2);
                    break;
            }
        }
        DrawCentredText(context, body.Bonus.ToString(), 13, p.Y + body.Radius + (body.Kind == PlanetKind.Islands ? 14 : 6),
            body.Kind == PlanetKind.Reactor ? Palette.Reactor : color, p.X, 0.85);
    }

    private void DrawCircle(DrawingContext context, Point c, double r, Color color, double alpha, double width, int segments)
    {
        var pts = new Point[segments];
        for (int i = 0; i < segments; i++)
            pts[i] = c + Field.FromAngle(i * 2 * Math.PI / segments) * r;
        DrawGlowPolygon(context, pts, color, alpha, width);
    }

    private void DrawEllipseLines(DrawingContext context, Point c, double rx, double ry, double tilt, Color color, double alpha)
    {
        var pts = new Point[28];
        for (int i = 0; i < pts.Length; i++)
        {
            double a = i * 2 * Math.PI / pts.Length;
            pts[i] = c + GameEngine.Rotate(new Vector(Math.Cos(a) * rx, Math.Sin(a) * ry), tilt);
        }
        DrawGlowPolygon(context, pts, color, alpha, 1.3);
    }

    // ----- Planets -----

    /// <summary>A point in the world, moved to where it appears on screen.</summary>
    private Point ToScreen(Point world)
    {
        var cam = _engine.Camera;
        var centre = new Point(cam.X + ScreenWidth / 2, 0);
        var p = _engine.Planet is { } planet ? planet.Near(world, centre.X) : world;
        return new Point(p.X - cam.X, p.Y - cam.Y);
    }

    private void DrawPlanet(DrawingContext context)
    {
        var planet = _engine.Planet!;
        var cam = _engine.Camera;
        DrawStarfield(context, 0.35);

        bool invisible = _engine.System.Invisible && planet.Kind != PlanetKind.Reactor;
        var ship = ToScreen(_engine.Ship.Position);
        foreach (var s in planet.Rock)
        {
            var a = ToScreen(s.A);
            var b = a + Field.Sub(s.B, s.A);
            if (Math.Max(a.X, b.X) < -20 || Math.Min(a.X, b.X) > ScreenWidth + 20) continue;
            if (Math.Max(a.Y, b.Y) < -20 || Math.Min(a.Y, b.Y) > ScreenHeight + 20) continue;
            double alpha = 1;
            if (invisible)
            {
                // Only the rock right round the ship shows, and only while the ship lives.
                double d = Field.DistanceToSegment(ship, a, b);
                alpha = _engine.Ship.Alive ? Math.Clamp(1 - (d - 60) / 120, 0.03, 1) : 0.03;
            }
            DrawRockBand(context, a, b, s.Normal, planet.Color, alpha);
            DrawGlowLine(context, a, b, planet.Color, alpha, 2);
        }
        if (planet.Kind == PlanetKind.Reactor)
            DrawCore(context, planet);

        foreach (var t in planet.Tanks.Where(t => t.Alive))
            DrawTank(context, ToScreen(t.Position), t.Normal);
        foreach (var k in planet.Bunkers.Where(k => k.Alive))
            DrawBunker(context, ToScreen(k.Position), k);

        // The top of the planet: a dotted line to fly out through.
        if (cam.Y < 10)
            for (double x = (-cam.X % 40 + 40) % 40; x < ScreenWidth; x += 40)
                context.FillRectangle(GetBrush(Palette.Ship, 0.35), new Rect(x, -cam.Y + 2, 10, 1.5));

        DrawActors(context, planet);
    }

    // A soft glow just inside the rock, fading inwards, so the ground reads as solid.
    private static void DrawRockBand(DrawingContext context, Point a, Point b, Vector normal, Color color, double alpha)
    {
        const double depth = 34;
        var inward = -normal * depth;
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(a, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(a + inward, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(70 * alpha), color.R, color.G, color.B), 0),
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
            }
        };
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(a, true);
            g.LineTo(b);
            g.LineTo(b + inward);
            g.LineTo(a + inward);
            g.EndFigure(true);
        }
        context.DrawGeometry(brush, null, geometry);
    }

    private void DrawCore(DrawingContext context, Planet planet)
    {
        var c = ToScreen(planet.Core);
        double r = Planet.CoreRadius;
        bool critical = _engine.CoreHit;
        var color = critical ? (Blink(8) ? Colors.White : Palette.Reactor) : Palette.Reactor;
        DrawCircle(context, c, r, color, 1, 2.2, 20);
        DrawCircle(context, c, r * 0.5 + Math.Sin(Clock * (critical ? 20 : 5)) * 3, Colors.White, 0.9, 1.6, 12);
        for (int i = 0; i < 6; i++)
        {
            var d = Field.FromAngle(i * Math.PI / 3 + Clock * (critical ? 4 : 1));
            DrawGlowLine(context, c + d * (r * 0.6), c + d * (r - 2), color, 0.8, 1.4);
        }
        // The pipes feeding it.
        DrawGlowLine(context, c + new Vector(-r, 8), c + new Vector(-r - 60, 8), Palette.Reactor, 0.6, 1.5);
        DrawGlowLine(context, c + new Vector(r, 8), c + new Vector(r + 60, 8), Palette.Reactor, 0.6, 1.5);
    }

    // A bunker: a squat dome with a gun, standing out from the rock along its normal.
    private void DrawBunker(DrawingContext context, Point at, Bunker k)
    {
        var n = k.Normal;
        var side = new Vector(-n.Y, n.X);
        var color = k.Kind == BunkerKind.Twin ? Palette.Twin : Palette.Bunker;
        Point P(double s, double u) => at + side * s + n * u;
        var body = new[] { P(-16, 0), P(-12, 9), P(-6, 14), P(6, 14), P(12, 9), P(16, 0) };
        DrawShape(context, body, Colors.Black, 0.8, color, 1, 1.8);
        // The gun turns to follow the ship.
        var toShip = _engine.Ship.Alive ? _engine.Between(k.Position, _engine.Ship.Position) : n;
        var dir = toShip / Math.Max(1, toShip.Length);
        if (Field.Dot(dir, n) < 0.1) dir = n;
        var root = P(0, 12);
        if (k.Kind == BunkerKind.Twin)
        {
            var perp = new Vector(-dir.Y, dir.X) * 3;
            DrawGlowLine(context, root + perp, root + perp + dir * 12, color, 1, 1.6);
            DrawGlowLine(context, root - perp, root - perp + dir * 12, color, 1, 1.6);
        }
        else
            DrawGlowLine(context, root, root + dir * 13, color, 1, 1.8);
    }

    private void DrawTank(DrawingContext context, Point at, Vector n)
    {
        var side = new Vector(-n.Y, n.X);
        Point P(double s, double u) => at + side * s + n * u;
        var shape = new[] { P(-13, 0), P(-13, 18), P(-8, 22), P(8, 22), P(13, 18), P(13, 0) };
        DrawShape(context, shape, Colors.Black, 0.8, Palette.Tank, 1, 1.6);
        var label = Text("FUEL", 9, Palette.Tank);
        var mid = P(0, 11);
        context.DrawText(label, new Point(mid.X - label.Width / 2, mid.Y - label.Height / 2));
    }

    // ----- Things that move -----

    private void DrawActors(DrawingContext context, Planet? planet)
    {
        Point S(Point p) => planet == null ? p : ToScreen(p);
        var ship = _engine.Ship;

        if (ship.Alive && _engine.IsInPlay || ship.Alive && _engine.State == GameState.Paused)
        {
            var at = S(ship.Position);
            // Tractor beam: a cone of lines down from the ship.
            if (_engine.Shielding && planet != null)
            {
                double flick = 0.35 + 0.15 * Math.Sin(Clock * 30);
                foreach (var t in planet.Tanks.Where(t => t.Alive && t.Beamed))
                    DrawGlowLine(context, at, S(t.Position), Palette.Beam, 0.8, 1.4);
                DrawGlowLine(context, at, at + new Vector(-28 - 160 * 0.35, 160), Palette.Beam, flick, 1.2);
                DrawGlowLine(context, at, at + new Vector(28 + 160 * 0.35, 160), Palette.Beam, flick, 1.2);
                for (int i = 1; i <= 4; i++)
                {
                    double y = (i * 40 + Clock * 120) % 160;
                    double w = 28 + y * 0.35;
                    DrawGlowLine(context, at + new Vector(-w, y), at + new Vector(w, y), Palette.Beam, flick * (1 - y / 200), 1);
                }
            }
            DrawShip(context, at, ship.Angle, _engine.Thrusting, 1);
            if (_engine.Shielding)
            {
                double r = Ship.Radius + 6 + Math.Sin(Clock * 25) * 1.5;
                DrawCircle(context, at, r, Palette.Shield, 0.9, 1.6, 16);
            }
        }

        foreach (var s in _engine.Saucers)
            DrawSaucer(context, S(s.Position), s.Spin);

        foreach (var b in _engine.Bullets)
        {
            var p = S(b.Position);
            var color = b.FromPlayer ? Palette.Shot : Palette.EnemyShot;
            var tail = p - b.Velocity / Math.Max(1, b.Velocity.Length) * (b.FromPlayer ? 7 : 5);
            DrawGlowLine(context, tail, p, color, 1, b.FromPlayer ? 2.2 : 2.6);
        }

        DrawDebris(context, planet);
    }

    private void DrawDebris(DrawingContext context, Planet? planet)
    {
        Point S(Point p) => planet == null ? p : ToScreen(p);
        foreach (var s in _engine.Shards)
        {
            double fade = s.Life / s.MaxLife;
            var c = S(s.Position);
            var a = Place(new[] { s.A, s.B }, c, s.Angle);
            DrawGlowLine(context, a[0], a[1], s.Color, Math.Min(1, fade * 1.4), 1.6);
        }
        foreach (var s in _engine.Sparks)
        {
            var c = S(s.Position);
            double fade = s.Life / s.MaxLife;
            context.FillRectangle(GetBrush(s.Color, fade), new Rect(c.X - 1.2, c.Y - 1.2, 2.4, 2.4));
        }
    }

    private void DrawShip(DrawingContext context, Point at, double angle, bool flame, double alpha)
    {
        if (flame)
        {
            double len = 10 + _random.NextDouble() * 9;
            var fl = Place(new[] { new Point(-4, 6), new Point(0, 6 + len), new Point(4, 6) }, at, angle);
            DrawGlowLine(context, fl[0], fl[1], Palette.Flame, alpha, 1.6);
            DrawGlowLine(context, fl[1], fl[2], Palette.Flame, alpha, 1.6);
        }
        DrawShape(context, Place(Ship.Hull, at, angle), Colors.Black, 0.85 * alpha, Palette.Ship, alpha, 2);
        // A cockpit line.
        var ck = Place(new[] { new Point(0, -6), new Point(0, 1) }, at, angle);
        DrawGlowLine(context, ck[0], ck[1], Colors.White, 0.8 * alpha, 1.2);
    }

    private void DrawSaucer(DrawingContext context, Point at, double spin)
    {
        var hull = new[] { new Point(-18, 0), new Point(-9, -6), new Point(9, -6), new Point(18, 0), new Point(9, 6), new Point(-9, 6) };
        DrawShape(context, Place(hull, at, 0), Colors.Black, 0.8, Palette.Saucer, 1, 1.8);
        var dome = new[] { new Point(-6, -6), new Point(-3, -11), new Point(3, -11), new Point(6, -6) };
        var d = Place(dome, at, 0);
        for (int i = 0; i + 1 < d.Length; i++) DrawGlowLine(context, d[i], d[i + 1], Palette.Saucer, 0.9, 1.4);
        // Lights chasing round the rim.
        for (int i = 0; i < 3; i++)
        {
            double x = Math.Sin(spin + i * 2.1) * 14;
            if (Math.Cos(spin + i * 2.1) < 0) continue;
            context.FillRectangle(GetBrush(Colors.White, 0.9), new Rect(at.X + x - 1.5, at.Y - 1.5, 3, 3));
        }
    }

    // ----- Warp -----

    private void DrawWarp(DrawingContext context)
    {
        double t = _engine.WarpProgress;
        var c = new Point(ScreenWidth / 2, ScreenHeight / 2);
        double speed = Math.Sin(t * Math.PI);
        foreach (var (at, bright) in Stars)
        {
            var d = Field.Sub(at, c);
            double grow = 1 + speed * 0.6;
            var from = c + d * (1 + t * 0.5);
            var to = c + d * (1 + t * 0.5) * grow;
            DrawGlowLine(context, from, to, Color.FromRgb(160, 200, 255), bright * (0.4 + speed * 0.6), 1.3);
        }
        DrawShip(context, c, 0, true, 1);
        DrawCentredText(context, "WARP", 30, 450, Gold, alpha: Blink(3) ? 1 : 0.5);
    }

    // A small sight where the mouse is, when it steers the ship.
    private void DrawSight(DrawingContext context, Point p)
    {
        DrawCircle(context, p, 8, Palette.Shield, 0.6, 1.2, 12);
        DrawGlowLine(context, p + new Vector(-14, 0), p + new Vector(-4, 0), Palette.Shield, 0.6, 1.2);
        DrawGlowLine(context, p + new Vector(14, 0), p + new Vector(4, 0), Palette.Shield, 0.6, 1.2);
        DrawGlowLine(context, p + new Vector(0, -14), p + new Vector(0, -4), Palette.Shield, 0.6, 1.2);
        DrawGlowLine(context, p + new Vector(0, 14), p + new Vector(0, 4), Palette.Shield, 0.6, 1.2);
    }
}
