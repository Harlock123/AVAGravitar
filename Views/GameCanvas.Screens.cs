using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AVAGravitar.Engine;
using AVAGravitar.Models;

namespace AVAGravitar.Views;

// The score across the top, and the title, entry and high-score screens.
public partial class GameCanvas
{
    private void DrawHud(DrawingContext context)
    {
        if (_engine.State == GameState.EnterInitials) return;
        bool title = _engine.State is GameState.StartScreen or GameState.HighScores;
        context.DrawText(Text(_engine.Score.ToString("D2"), 26, Palette.Ship), new Point(30, 8));
        int high = _engine.State == GameState.Attract ? _engine.HighScore : Math.Max(_engine.HighScore, _engine.Score);
        DrawRightText(context, high.ToString(), 20, ScreenWidth - 30, 12, Gold);

        if (!title)
        {
            // Ships in reserve.
            for (int i = 0; i < Math.Min(_engine.Ships - (_engine.Ship.Alive ? 1 : 0), 10); i++)
                DrawShip(context, new Point(40 + i * 22, 56), 0, false, 0.8);

            // Fuel, flashing when low.
            bool low = _engine.Fuel < 2000;
            var fuelColor = low ? (Blink(3) ? AlertRed : Colors.White) : TextColor;
            DrawCentredText(context, $"FUEL {(int)_engine.Fuel}", 20, 10, fuelColor);
            double bar = Math.Min(1, _engine.Fuel / GameEngine.StartFuel);
            context.FillRectangle(GetBrush(fuelColor, 0.6), new Rect(ScreenWidth / 2 - 100, 38, 200 * bar, 4));
            DrawFrame(context, new Rect(ScreenWidth / 2 - 101, 37, 202, 6), fuelColor, 0.4);

            if (_engine.Body is { } body && _engine.Scene == Scene.Planet)
            {
                int left = body.Planet.Bunkers.Count(b => b.Alive);
                string what = body.Kind == PlanetKind.Reactor ? "REACTOR" : $"BUNKERS {left}";
                DrawRightText(context, $"{what}   BONUS {body.Bonus}", 14, ScreenWidth - 30, 40, body.Planet.Color);
            }
            if (_engine.ReactorTimer > 0)
            {
                var color = Blink(4) ? AlertRed : Colors.White;
                DrawCentredText(context, $"{Math.Ceiling(_engine.ReactorTimer):0}", 64, 70, color);
            }
        }

        if (_engine.Sound.IsMuted)
            DrawRightText(context, "MUTED", 11, ScreenWidth - 30, 60, Colors.White);

        if (_engine.BannerTimer > 0 && _engine.IsInPlay)
        {
            double alpha = Math.Min(1, _engine.BannerTimer * 2);
            DrawCentredText(context, _engine.Banner, 34, 250, Gold, alpha: alpha);
            if (_engine.SubBanner.Length > 0)
                DrawCentredText(context, _engine.SubBanner, 18, 296, Colors.White, alpha: alpha);
        }
    }

    private void DrawGameOver(DrawingContext context)
    {
        double t = _engine.EndClock;
        double alpha = Math.Min(1, t * 1.5);
        context.FillRectangle(GetBrush(Colors.Black, 0.7 * alpha), new Rect(212, 280, 600, 190));
        DrawFrame(context, new Rect(212, 280, 600, 190), AlertRed, 0.5 * alpha);
        DrawCentredText(context, "GAME OVER", 64, 300, AlertRed, alpha: alpha);
        if (_engine.EndReason.Length > 0)
            DrawCentredText(context, _engine.EndReason, 20, 385, Colors.White, alpha: alpha);
        if (t > 2 && Blink(2))
            DrawCentredText(context, "Press FIRE to play again", 20, 430, TextColor);
    }

    // ----- Title screen -----

    private void DrawTitleScreen(DrawingContext context, bool withText)
    {
        DrawSpace(context, withShip: false);
        if (!withText) return;

        context.FillRectangle(GetBrush(Colors.Black, 0.6), new Rect(0, 0, ScreenWidth, ScreenHeight));
        DrawLogo(context, 70);
        DrawCentredText(context, "GRAVITAR", 22, 160, Color.FromRgb(230, 90, 255));

        string[] scoring =
        {
            "BUNKER 250    TWIN BUNKER 500    SAUCER 500",
            "DESTROY EVERY BUNKER ON A PLANET FOR ITS BONUS",
            "SHOOT THE REACTOR CORE, THEN ESCAPE IN TIME",
            $"FUEL TANK +{FuelTank.Fuel} FUEL    EXTRA SHIP EVERY {GameEngine.ExtraShipEvery}"
        };
        for (int i = 0; i < scoring.Length; i++)
            DrawCentredText(context, scoring[i], 15, 205 + i * 25, Colors.White);

        var controls = new (string Keys, string Action)[]
        {
            ("LEFT / RIGHT   A D", "Rotate"),
            ("UP   W   right click", "Thrust"),
            ("SPACE   CTRL   left click", "Fire"),
            ("DOWN   S   SHIFT   middle", "Shield and tractor beam"),
            ("Mouse", "Point the ship at the pointer"),
            ("Gamepad", "Stick aims, A thrust, X fire, Y / LB shield"),
            ("ESC  M", "Pause, mute")
        };
        for (int i = 0; i < controls.Length; i++)
        {
            double y = 330 + i * 22;
            context.DrawText(Text(controls[i].Keys, 14, Colors.White), new Point(170, y));
            context.DrawText(Text(controls[i].Action, 14, TextColor), new Point(460, y));
        }
        DrawCentredText(context, "Mind the star's pull. Running out of fuel ends the game.", 14, 495, Gold);

        var best = _engine.Table.Entries.Count > 0 ? _engine.Table.Entries[0] : null;
        DrawCentredText(context, best == null ? "HIGH SCORE 0" : $"HIGH SCORE  {best.Initials.TrimEnd()}  {best.Score}", 18, 540, Gold);
        if (Blink(2))
            DrawCentredText(context, "Press FIRE to start", 26, 585, Colors.White);
    }

    // The name in outlined vector letters, with a glow.
    private void DrawLogo(DrawingContext context, double y)
    {
        var text = Text("AVAGRAVITAR", 76, Palette.Ship);
        var origin = new Point(ScreenWidth / 2 - text.Width / 2, y);
        var geometry = text.BuildGeometry(origin);
        if (geometry == null)
        {
            context.DrawText(text, origin);
            return;
        }
        context.DrawGeometry(null, GetPen(Palette.Ship, 0.2, 7), geometry);
        context.DrawGeometry(GetBrush(Colors.Black, 0.9), GetPen(Palette.Ship, 1, 2), geometry);
    }

    // ----- High scores -----

    private string NewEntryHeadline => _engine.Score > _engine.HighScore ? "NEW HIGH SCORE" : "YOU MADE THE TOP TEN";

    private void DrawInitialsEntry(DrawingContext context)
    {
        var entry = _engine.Entry;
        if (entry == null) return;

        DrawPanel(context, new Rect(152, 110, 720, 560));
        DrawCentredText(context, NewEntryHeadline, 40, 140, Gold);
        DrawCentredText(context, $"SCORE {_engine.Score}    SYSTEM {_engine.System.Number + 1}", 22, 200, TextColor);
        DrawCentredText(context, "ENTER YOUR INITIALS, PILOT", 24, 260, Colors.White);

        bool blinkOn = Blink(3);
        const double cell = 100;
        double left = (ScreenWidth - cell * HighScoreTable.InitialsLength) / 2;
        string initials = entry.Initials;

        for (int i = 0; i < initials.Length; i++)
        {
            bool current = i == entry.Cursor;
            char c = initials[i];
            var letter = Text(c == ' ' ? "_" : c.ToString(), 76, current ? Colors.White : Gold);
            double x = left + i * cell + (cell - letter.Width) / 2;
            context.DrawText(letter, new Point(x, 315));

            if (current)
            {
                var up = Text("^", 22, TextColor);
                var down = Text("v", 22, TextColor);
                context.DrawText(up, new Point(left + i * cell + (cell - up.Width) / 2, 300));
                context.DrawText(down, new Point(left + i * cell + (cell - down.Width) / 2, 418));
                if (blinkOn)
                    DrawGlowLine(context, new Point(left + i * cell + 18, 412), new Point(left + (i + 1) * cell - 18, 412), Colors.White, 1, 2.5);
            }
        }

        string[] help =
        {
            "Type your initials, or UP/DOWN to choose a letter",
            "and LEFT/RIGHT to move.  ENTER when done.",
            "Gamepad: D-pad to choose, X to accept, START when done",
            "Mouse: wheel to choose, click to accept"
        };
        double y = 480;
        foreach (var line in help)
        {
            DrawCentredText(context, line, 16, y, TextColor);
            y += 26;
        }
    }

    private void DrawHighScoreTable(DrawingContext context)
    {
        DrawPanel(context, new Rect(192, 60, 640, 640));
        DrawCentredText(context, "HIGH SCORES", 44, 80, Palette.Ship);

        const double rankX = 250, nameX = 340, scoreRight = 620, waveX = 670;
        double y = 165;
        context.DrawText(Text("RANK", 16, TextColor), new Point(rankX, y));
        context.DrawText(Text("NAME", 16, TextColor), new Point(nameX, y));
        DrawRightText(context, "SCORE", 16, scoreRight, y, TextColor);
        context.DrawText(Text("SYS", 16, TextColor), new Point(waveX, y));

        bool blinkOn = Blink(3);
        var entries = _engine.Table.Entries;
        y = 200;
        for (int i = 0; i < HighScoreTable.Size; i++)
        {
            bool highlight = i == _engine.HighlightIndex;
            var color = highlight ? (blinkOn ? Colors.White : Gold) : i == 0 ? Gold : Colors.White;

            string name = i < entries.Count ? entries[i].Initials : "---";
            string score = i < entries.Count ? entries[i].Score.ToString() : "0";
            string wave = i < entries.Count ? entries[i].Wave.ToString() : "-";

            context.DrawText(Text($"{i + 1}.", 24, color), new Point(rankX, y));
            context.DrawText(Text(name, 24, color), new Point(nameX, y));
            DrawRightText(context, score, 24, scoreRight, y, color);
            DrawRightText(context, wave, 24, waveX + 50, y, color);

            if (highlight)
                DrawGlowLine(context, new Point(rankX - 20, y + 15), new Point(rankX - 8, y + 15), color, 1, 2);
            y += 42;
        }

        if (Blink(2))
            DrawCentredText(context, "Press FIRE to play", 24, 650, Colors.White);
    }
}
