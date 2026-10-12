using System;
using Avalonia;

namespace AVAGravitar.Models;

/// <summary>The fixed screen everything is drawn on, and small vector helpers.</summary>
public static class Field
{
    public const double Width = 1024;
    public const double Height = 768;

    /// <summary>The vector from one point to another (Avalonia 12's Point − Point is a Point, not a Vector).</summary>
    public static Vector Sub(Point to, Point from) => new(to.X - from.X, to.Y - from.Y);

    public static double Distance(Point a, Point b) => Sub(a, b).Length;

    public static Vector FromAngle(double angle) => new(Math.Sin(angle), -Math.Cos(angle));

    /// <summary>The angle a vector points at, in the ship's convention: 0 is straight up, clockwise positive.</summary>
    public static double AngleOf(Vector v) => Math.Atan2(v.X, -v.Y);

    /// <summary>The signed turn from one angle to another, in (−π, π].</summary>
    public static double AngleDiff(double from, double to)
    {
        double d = (to - from) % (2 * Math.PI);
        if (d > Math.PI) d -= 2 * Math.PI;
        if (d <= -Math.PI) d += 2 * Math.PI;
        return d;
    }

    public static double Dot(Vector a, Vector b) => a.X * b.X + a.Y * b.Y;

    /// <summary>The closest point to p on the segment a–b.</summary>
    public static Point Closest(Point p, Point a, Point b)
    {
        var ab = Sub(b, a);
        double len2 = Dot(ab, ab);
        if (len2 < 1e-9) return a;
        double t = Math.Clamp(Dot(Sub(p, a), ab) / len2, 0, 1);
        return a + ab * t;
    }

    public static double DistanceToSegment(Point p, Point a, Point b) => Distance(p, Closest(p, a, b));

    /// <summary>Whether segments p1–p2 and q1–q2 cross.</summary>
    public static bool Crosses(Point p1, Point p2, Point q1, Point q2)
    {
        double d1 = Cross(q1, q2, p1), d2 = Cross(q1, q2, p2);
        double d3 = Cross(p1, p2, q1), d4 = Cross(p1, p2, q2);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static double Cross(Point a, Point b, Point c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
}
