using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace AVAGravitar.Models;

/// <summary>A planet (or the reactor) as it appears in the solar system: a circle to fly into.</summary>
public sealed class Body
{
    public Point Position;
    public double Radius;
    public Planet Planet = null!;
    public int Bonus;
    public bool Destroyed;
    public PlanetKind Kind => Planet.Kind;
}

/// <summary>
/// One solar system: a star at the middle whose pull drags the ship in, the home base where each ship
/// starts, four planets and a reactor. Clear them all and the ship warps to the next system.
/// </summary>
public sealed class SolarSystem
{
    public static readonly Point Star = new(512, 400);
    public const double StarRadius = 34;
    public const double StarPull = 2.6e6;   // pull = StarPull / distance², px/s²
    public static readonly Point Home = new(110, 680);

    public int Number { get; }
    public List<Body> Bodies { get; } = new();
    public bool Cleared => Bodies.All(b => b.Destroyed);

    // Where the four planets and the reactor (last) sit, for each of the three systems in a universe.
    private static readonly Point[][] Layouts =
    {
        new Point[] { new(230, 200), new(800, 170), new(860, 560), new(330, 600), new(560, 150) },
        new Point[] { new(180, 380), new(470, 640), new(780, 640), new(840, 260), new(300, 160) },
        new Point[] { new(240, 170), new(720, 130), new(890, 420), new(620, 650), new(260, 480) }
    };

    public SolarSystem(int number)
    {
        Number = number;
        int universe = number / 3;
        var layout = Layouts[number % 3];
        var rng = new Random(number * 7919 + 17);
        var kinds = new[] { PlanetKind.Hills, PlanetKind.Spires, PlanetKind.Islands, PlanetKind.Canyon }
            .OrderBy(_ => rng.Next()).ToArray();
        int bunkers = Math.Min(9, 5 + number);
        for (int i = 0; i < 4; i++)
        {
            var planet = Planet.Make(kinds[i], number * 101 + i * 13 + 3, bunkers, 3);
            Bodies.Add(new Body
            {
                Position = layout[i], Radius = 26 + (int)kinds[i] * 3, Planet = planet,
                Bonus = (2000 + 1000 * (int)kinds[i]) * (universe + 1)
            });
        }
        Bodies.Add(new Body
        {
            Position = layout[4], Radius = 24, Planet = Planet.Make(PlanetKind.Reactor, number, 0, 0),
            Bonus = 5000 * (universe + 1)
        });
    }

    /// <summary>Universe 1 plays straight, universe 2 turns gravity upside down, universe 3 hides the rock.</summary>
    public static int Universe(int systemNumber) => systemNumber / 3;
    public bool Reversed => Universe(Number) % 3 == 1;
    public bool Invisible => Universe(Number) % 3 == 2;
}
