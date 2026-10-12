using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace AVAGravitar.Views;

// Drawing primitives shared by every part of the screen.
public partial class GameCanvas
{
    private static readonly Color TextColor = Color.FromRgb(120, 200, 255);
    private static readonly Color Gold = Color.FromRgb(255, 210, 90);
    private static readonly Color AlertRed = Color.FromRgb(255, 70, 60);
    private static readonly Color FrameColor = Color.FromRgb(70, 140, 220);

    private static readonly Typeface HudFont = new(new FontFamily("Consolas, Menlo, DejaVu Sans Mono, monospace"), FontStyle.Normal, FontWeight.Bold);

    private readonly Random _random = new();

    // Pens are cached by colour, opacity and width: a frame draws a couple of thousand lines.
    private readonly Dictionary<(Color, byte, double), IPen> _pens = new();
    private readonly Dictionary<(Color, byte), IBrush> _brushes = new();

    private double Clock => _engine.Clock;

    private bool Blink(double perSecond) => (int)(Clock * perSecond) % 2 == 0;

    // A vector-monitor look: each line is drawn once wide and faint, then again thin and bright.
    private void DrawGlowLine(DrawingContext context, Point a, Point b, Color color, double alpha, double width)
    {
        if (alpha <= 0.01) return;
        context.DrawLine(GetPen(color, alpha * 0.22, Math.Round(width * 3.5, 1)), a, b);
        context.DrawLine(GetPen(color, alpha, width), a, b);
    }

    private void DrawGlowPolygon(DrawingContext context, IReadOnlyList<Point> points, Color color, double alpha, double width)
    {
        for (int i = 0; i < points.Count; i++)
            DrawGlowLine(context, points[i], points[(i + 1) % points.Count], color, alpha, width);
    }

    private void DrawFrame(DrawingContext context, Rect rect, Color color, double alpha = 0.7)
    {
        DrawGlowPolygon(context, new[] { rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft }, color, alpha, 1.5);
    }

    private IPen GetPen(Color color, double alpha, double width)
    {
        byte a = (byte)Math.Clamp(alpha * 255, 0, 255);
        // Widths are rounded so distance-scaled lines don't fill the cache with near-duplicates.
        width = Math.Round(width * 4) / 4;
        var key = (color, a, width);
        if (!_pens.TryGetValue(key, out var pen))
        {
            pen = new Pen(GetBrush(color, alpha), width, lineCap: PenLineCap.Round);
            if (_pens.Count > 4000) _pens.Clear();
            _pens[key] = pen;
        }
        return pen;
    }

    private IBrush GetBrush(Color color, double alpha)
    {
        byte a = (byte)Math.Clamp(alpha * 255, 0, 255);
        var key = (color, a);
        if (!_brushes.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(Color.FromArgb(a, color.R, color.G, color.B));
            if (_brushes.Count > 2000) _brushes.Clear();
            _brushes[key] = brush;
        }
        return brush;
    }

    private static Color Lerp(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
    }

    private static FormattedText Text(string text, double size, Color color, double alpha = 1) => new(
        text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, HudFont, size,
        new SolidColorBrush(Color.FromArgb((byte)(255 * Math.Clamp(alpha, 0, 1)), color.R, color.G, color.B)));

    private static void DrawCentredText(DrawingContext context, string text, double size, double y, Color color,
        double centreX = ScreenWidth / 2, double alpha = 1)
    {
        var formatted = Text(text, size, color, alpha);
        context.DrawText(formatted, new Point(centreX - formatted.Width / 2, y));
    }

    private static void DrawRightText(DrawingContext context, string text, double size, double right, double y, Color color)
    {
        var formatted = Text(text, size, color);
        context.DrawText(formatted, new Point(right - formatted.Width, y));
    }

    // A dark panel over the screen, for the pause, entry and table screens.
    private void DrawPanel(DrawingContext context, Rect rect)
    {
        context.FillRectangle(GetBrush(Colors.Black, 0.85), rect);
        DrawFrame(context, rect, Gold, 0.6);
    }

    /// <summary>A filled shape with a glowing outline: the look of everything in space.</summary>
    private void DrawShape(DrawingContext context, IReadOnlyList<Point> points, Color fill, double fillAlpha, Color edge, double edgeAlpha, double width)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(points[0], true);
            for (int i = 1; i < points.Count; i++) g.LineTo(points[i]);
            g.EndFigure(true);
        }
        context.DrawGeometry(GetBrush(fill, fillAlpha), null, geometry);
        DrawGlowPolygon(context, points, edge, edgeAlpha, width);
    }

    /// <summary>Turns and places a shape given round its own middle.</summary>
    private static Point[] Place(IReadOnlyList<Point> shape, Point at, double rotation, double scale = 1)
    {
        double c = Math.Cos(rotation) * scale, s = Math.Sin(rotation) * scale;
        var result = new Point[shape.Count];
        for (int i = 0; i < shape.Count; i++)
            result[i] = new Point(at.X + shape[i].X * c - shape[i].Y * s, at.Y + shape[i].X * s + shape[i].Y * c);
        return result;
    }
}
