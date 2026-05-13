using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Drift.Ui.Controls;

// Tiny shape preview used inside ComboBox item templates. Knows how to draw
// every Waveform / LfoShape value by name (Sine / Triangle / Saw / Square /
// Noise / SampleHold). Bind the enum item directly to Value -- Avalonia stores
// it as an object and ToString() gives the name.
public sealed class WaveformIcon : Control
{
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<WaveformIcon, object?>(nameof(Value));

    public static readonly StyledProperty<IBrush> StrokeProperty =
        AvaloniaProperty.Register<WaveformIcon, IBrush>(nameof(Stroke),
            new ImmutableSolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF)));

    static WaveformIcon()
    {
        AffectsRender<WaveformIcon>(ValueProperty, StrokeProperty);
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(22, 12);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 2 || h <= 2 || Value is null)
        {
            return;
        }

        var color = (Stroke as ISolidColorBrush)?.Color ?? Color.FromRgb(0x00, 0xD4, 0xFF);
        var pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1.4,
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        var pad = 1.5;
        var x0 = pad;
        var x1 = w - pad;
        var midY = h / 2.0;
        var top = pad;
        var bot = h - pad;
        var amp = (h - pad * 2) / 2.0;

        var name = Value.ToString() ?? "";
        switch (name)
        {
            case "Sine":
                DrawSine(ctx, pen, x0, x1, midY, amp);
                break;
            case "Triangle":
                DrawTriangle(ctx, pen, x0, x1, midY, amp);
                break;
            case "Saw":
                DrawSaw(ctx, pen, x0, x1, top, bot);
                break;
            case "Square":
                DrawSquare(ctx, pen, x0, x1, top, bot, midY);
                break;
            case "Noise":
                DrawNoise(ctx, pen, x0, x1, top, bot);
                break;
            case "SampleHold":
                DrawSampleHold(ctx, pen, x0, x1, top, bot);
                break;
        }
    }

    private static void DrawSine(DrawingContext ctx, IPen pen, double x0, double x1, double midY, double amp)
    {
        const int samples = 32;
        var path = new PathGeometry();
        var fig = new PathFigure { IsClosed = false };
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (double)samples;
            var x = x0 + t * (x1 - x0);
            var y = midY - Math.Sin(t * Math.PI * 2) * amp;
            if (i == 0)
            {
                fig.StartPoint = new Point(x, y);
            }
            else
            {
                fig.Segments!.Add(new LineSegment { Point = new Point(x, y) });
            }
        }

        path.Figures!.Add(fig);
        ctx.DrawGeometry(null, pen, path);
    }

    private static void DrawTriangle(DrawingContext ctx, IPen pen, double x0, double x1, double midY, double amp)
    {
        var qx = (x1 - x0) / 4.0;
        var path = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(x0, midY), IsClosed = false };
        fig.Segments!.Add(new LineSegment { Point = new Point(x0 + qx, midY - amp) });
        fig.Segments!.Add(new LineSegment { Point = new Point(x0 + 3 * qx, midY + amp) });
        fig.Segments!.Add(new LineSegment { Point = new Point(x1, midY) });
        path.Figures!.Add(fig);
        ctx.DrawGeometry(null, pen, path);
    }

    private static void DrawSaw(DrawingContext ctx, IPen pen, double x0, double x1, double top, double bot)
    {
        var midX = (x0 + x1) / 2.0;
        var path = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(x0, bot), IsClosed = false };
        fig.Segments!.Add(new LineSegment { Point = new Point(midX, top) });
        fig.Segments!.Add(new LineSegment { Point = new Point(midX, bot) });
        fig.Segments!.Add(new LineSegment { Point = new Point(x1, top) });
        path.Figures!.Add(fig);
        ctx.DrawGeometry(null, pen, path);
    }

    private static void DrawSquare(DrawingContext ctx, IPen pen, double x0, double x1, double top, double bot, double midY)
    {
        var qx = (x1 - x0) / 4.0;
        var path = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(x0, midY), IsClosed = false };
        fig.Segments!.Add(new LineSegment { Point = new Point(x0, top) });
        fig.Segments!.Add(new LineSegment { Point = new Point(x0 + 2 * qx, top) });
        fig.Segments!.Add(new LineSegment { Point = new Point(x0 + 2 * qx, bot) });
        fig.Segments!.Add(new LineSegment { Point = new Point(x1, bot) });
        fig.Segments!.Add(new LineSegment { Point = new Point(x1, midY) });
        path.Figures!.Add(fig);
        ctx.DrawGeometry(null, pen, path);
    }

    private static void DrawNoise(DrawingContext ctx, IPen pen, double x0, double x1, double top, double bot)
    {
        // Deterministic short jagged line so every render is identical.
        var heights = new[] { 0.4, 0.8, 0.2, 0.95, 0.55, 0.15, 0.7, 0.35, 0.85, 0.5 };
        var path = new PathGeometry();
        var fig = new PathFigure { IsClosed = false };
        for (var i = 0; i < heights.Length; i++)
        {
            var t = i / (double)(heights.Length - 1);
            var x = x0 + t * (x1 - x0);
            var y = top + heights[i] * (bot - top);
            if (i == 0)
            {
                fig.StartPoint = new Point(x, y);
            }
            else
            {
                fig.Segments!.Add(new LineSegment { Point = new Point(x, y) });
            }
        }

        path.Figures!.Add(fig);
        ctx.DrawGeometry(null, pen, path);
    }

    private static void DrawSampleHold(DrawingContext ctx, IPen pen, double x0, double x1, double top, double bot)
    {
        // Step pattern: four held levels separated by vertical edges.
        var levels = new[] { 0.7, 0.2, 0.85, 0.4 };
        var stepW = (x1 - x0) / levels.Length;
        var path = new PathGeometry();
        var fig = new PathFigure { IsClosed = false };
        for (var i = 0; i < levels.Length; i++)
        {
            var y = top + (1 - levels[i]) * (bot - top);
            var x = x0 + i * stepW;
            var xNext = x0 + (i + 1) * stepW;
            if (i == 0)
            {
                fig.StartPoint = new Point(x, y);
            }
            else
            {
                fig.Segments!.Add(new LineSegment { Point = new Point(x, y) });
            }

            fig.Segments!.Add(new LineSegment { Point = new Point(xNext, y) });
        }

        path.Figures!.Add(fig);
        ctx.DrawGeometry(null, pen, path);
    }
}
