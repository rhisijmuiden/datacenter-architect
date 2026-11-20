using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>One line in a chart: values per x (game day), its colour and how a value is written.</summary>
public record ChartSeries(string Name, Color Color, IReadOnlyList<(double x, double y)> Points, Func<double, string> Format, bool Fill = false);

/// <summary>
/// A small line chart drawn by hand (step 11 statistics): soft grid, filled area under the first line, the last value
/// at the end, and a vertical cursor with the values under the mouse.
/// </summary>
public partial class LineChart : Control
{
    readonly List<ChartSeries> _series = new();
    readonly Func<double, string> _xLabel;
    double? _hoverX;
    /// <summary>Lowest value the y axis shows (null = from the data), for example 0 for money or 1.0 for PUE.</summary>
    public double? MinY, MaxY;

    const float PadL = 8, PadR = 8, PadT = 8, PadB = 20;

    public LineChart(Func<double, string> xLabel, float height = 150)
    {
        _xLabel = xLabel;
        CustomMinimumSize = new Vector2(0, height);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Pass;
    }

    public void Add(ChartSeries s) { _series.Add(s); QueueRedraw(); }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion m) { _hoverX = m.Position.X; QueueRedraw(); }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit) { _hoverX = null; QueueRedraw(); }
    }

    (double x0, double x1, double y0, double y1) Range()
    {
        var pts = _series.SelectMany(s => s.Points).ToList();
        if (pts.Count == 0) return (0, 1, 0, 1);
        double x0 = pts.Min(p => p.x), x1 = pts.Max(p => p.x);
        double y0 = MinY ?? pts.Min(p => p.y), y1 = MaxY ?? pts.Max(p => p.y);
        if (MinY == null) y0 = Math.Min(y0, pts.Min(p => p.y));
        if (MaxY == null) y1 = Math.Max(y1, pts.Max(p => p.y));
        if (y1 - y0 < 1e-9) { y1 += Math.Abs(y1) * 0.1 + 1; y0 -= MinY == null ? Math.Abs(y0) * 0.1 + 1 : 0; }
        var pad = (y1 - y0) * 0.08;
        if (MaxY == null) y1 += pad;
        if (MinY == null) y0 -= pad;
        if (x1 - x0 < 1) x1 = x0 + 1;
        return (x0, x1, y0, y1);
    }

    public override void _Draw()
    {
        var font = Ui.Body;
        var w = Size.X; var h = Size.Y;
        var plot = new Rect2(PadL, PadT, Math.Max(10, w - PadL - PadR), Math.Max(10, h - PadT - PadB));
        // soft horizontal grid
        for (var i = 0; i <= 3; i++)
        {
            var y = plot.Position.Y + plot.Size.Y * i / 3f;
            DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), new Color(Brand.Line, i == 3 ? 0.5f : 0.22f), 1);
        }
        if (_series.All(s => s.Points.Count == 0))
        {
            DrawString(font, new Vector2(plot.Position.X, plot.Position.Y + plot.Size.Y / 2), Loc.T("pg_no_data"), HorizontalAlignment.Center, plot.Size.X, 14, Brand.InkDim);
            return;
        }
        var (x0, x1, y0, y1) = Range();
        Vector2 P(double x, double y) => new(plot.Position.X + (float)((x - x0) / (x1 - x0)) * plot.Size.X,
            plot.End.Y - (float)((Math.Clamp(y, y0, y1) - y0) / (y1 - y0)) * plot.Size.Y);
        // zero line when the chart crosses it (money, profit)
        if (y0 < 0 && y1 > 0) DrawDashedLine(P(x0, 0), P(x1, 0), new Color(Brand.InkDim, 0.6f), 1, 4);
        foreach (var s in _series)
        {
            if (s.Points.Count == 0) continue;
            var pts = s.Points.Select(p => P(p.x, p.y)).ToArray();
            if (s.Fill && pts.Length > 1)
            {
                var poly = new Vector2[pts.Length + 2];
                pts.CopyTo(poly, 0);
                poly[^2] = new Vector2(pts[^1].X, plot.End.Y); poly[^1] = new Vector2(pts[0].X, plot.End.Y);
                var cols = Enumerable.Repeat(new Color(s.Color, 0.13f), poly.Length).ToArray();
                if (Geometry2D.TriangulatePolygon(poly).Length > 0) DrawPolygon(poly, cols);
            }
            if (pts.Length == 1) DrawCircle(pts[0], 3, s.Color);
            else DrawPolyline(pts, s.Color, 2, true);
        }
        // x labels: first and last day
        DrawString(font, new Vector2(plot.Position.X, h - 4), _xLabel(x0), HorizontalAlignment.Left, -1, 12, Brand.InkDim);
        DrawString(font, new Vector2(plot.End.X - 160, h - 4), _xLabel(x1), HorizontalAlignment.Right, 160, 12, Brand.InkDim);
        // y labels: top and bottom of the first series' format
        var fmt = _series[0].Format;
        DrawString(font, new Vector2(plot.Position.X + 2, plot.Position.Y + 12), fmt(y1), HorizontalAlignment.Left, -1, 11, new Color(Brand.InkDim, 0.85f));
        DrawString(font, new Vector2(plot.Position.X + 2, plot.End.Y - 3), fmt(y0), HorizontalAlignment.Left, -1, 11, new Color(Brand.InkDim, 0.85f));
        if (_hoverX is not double hx || hx < plot.Position.X || hx > plot.End.X) return;
        // cursor with the values at that day
        var xv = x0 + (hx - plot.Position.X) / plot.Size.X * (x1 - x0);
        DrawLine(new Vector2((float)hx, plot.Position.Y), new Vector2((float)hx, plot.End.Y), new Color(Brand.Ink, 0.4f), 1);
        var lines = new List<(string, Color)> { (_xLabel(xv), Brand.Ink) };
        foreach (var s in _series)
        {
            if (s.Points.Count == 0) continue;
            var near = s.Points.OrderBy(p => Math.Abs(p.x - xv)).First();
            DrawCircle(P(near.x, near.y), 3.5f, s.Color);
            lines.Add((s.Name + ": " + s.Format(near.y), s.Color));
        }
        var bw = lines.Max(l => font.GetStringSize(l.Item1, HorizontalAlignment.Left, -1, 13).X) + 16;
        var bx = (float)Math.Min(hx + 10, w - bw - 2);
        var box = new Rect2(bx, plot.Position.Y + 4, bw, lines.Count * 17 + 8);
        DrawStyleBox(Ui.Box(Brand.PanelSolid, 6, Brand.Line, 1, 4), box);
        for (var i = 0; i < lines.Count; i++)
            DrawString(font, new Vector2(bx + 8, box.Position.Y + 18 + i * 17), lines[i].Item1, HorizontalAlignment.Left, -1, 13, lines[i].Item2);
    }
}
