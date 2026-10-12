using System;
using Avalonia;
using Avalonia.Media;

namespace AVAGravitar.Models;

/// <summary>The player's ship. Angle 0 points straight up; positive turns clockwise.</summary>
public sealed class Ship
{
    /// <summary>The outline, nose up, round the ship's middle.</summary>
    public static readonly Point[] Hull =
    {
        new(0, -14), new(10, 10), new(4, 5), new(-4, 5), new(-10, 10)
    };

    /// <summary>The points that touch things: nose, wingtips and tail.</summary>
    public static readonly Point[] Touch = { new(0, -14), new(10, 10), new(-10, 10), new(0, 5) };

    public const double Radius = 13;

    public Point Position;
    public Vector Velocity;
    public double Angle;
    public bool Alive = true;

    public Vector Nose => Field.FromAngle(Angle);
    public Point Muzzle => Position + Nose * 15;

    /// <summary>One of the hull's points, turned and placed in the world.</summary>
    public Point World(Point local)
    {
        double c = Math.Cos(Angle), s = Math.Sin(Angle);
        return new Point(Position.X + local.X * c - local.Y * s, Position.Y + local.X * s + local.Y * c);
    }
}

public sealed class Bullet
{
    public Point Position;
    public Vector Velocity;
    public double Life;
    public bool FromPlayer;
    public bool Dead;
}

public enum BunkerKind { Turret, Twin }

/// <summary>A gun emplacement sitting on the ground (or hanging under it), facing out along Normal.</summary>
public sealed class Bunker
{
    public Point Position;
    public Vector Normal;
    public BunkerKind Kind;
    public bool Alive = true;
    public double Cooldown;
    public int Points => Kind == BunkerKind.Twin ? 500 : 250;
}

/// <summary>A fuel tank waiting to be pulled up by the tractor beam.</summary>
public sealed class FuelTank
{
    public const int Fuel = 2500;
    public Point Position;
    /// <summary>Where it sits on the ground; it sinks back here when the beam lets go.</summary>
    public Point Home;
    public Vector Normal;
    public bool Alive = true;
    /// <summary>Caught in the tractor beam this frame.</summary>
    public bool Beamed;
}

/// <summary>An enemy ship that hunts the player, in space and on planets that are taking too long.</summary>
public sealed class Saucer
{
    public const int Points = 500;
    public const double Radius = 16;
    public Point Position;
    public Vector Velocity;
    public double Cooldown;
    public double Spin;
    public bool Alive = true;
}

/// <summary>A broken-off piece of a wireframe, tumbling away from an explosion.</summary>
public sealed class Shard
{
    public Point Position;
    public Vector Velocity;
    public Point A, B;
    public double Angle, Spin, Life, MaxLife;
    public Color Color;
}

public sealed class Spark
{
    public Point Position;
    public Vector Velocity;
    public double Life, MaxLife;
    public Color Color;
}

/// <summary>The colours of the vector screen.</summary>
public static class Palette
{
    public static readonly Color Ship = Color.FromRgb(90, 180, 255);
    public static readonly Color Flame = Color.FromRgb(255, 150, 40);
    public static readonly Color Shot = Color.FromRgb(255, 255, 200);
    public static readonly Color EnemyShot = Color.FromRgb(255, 90, 70);
    public static readonly Color Bunker = Color.FromRgb(255, 70, 60);
    public static readonly Color Twin = Color.FromRgb(255, 160, 40);
    public static readonly Color Tank = Color.FromRgb(80, 140, 255);
    public static readonly Color Shield = Color.FromRgb(120, 255, 200);
    public static readonly Color Beam = Color.FromRgb(120, 255, 200);
    public static readonly Color Saucer = Color.FromRgb(255, 90, 230);
    public static readonly Color Star = Color.FromRgb(255, 230, 90);
    public static readonly Color Reactor = Color.FromRgb(255, 80, 40);
}
