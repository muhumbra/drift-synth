using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Drift.Engine.Synth;

namespace Drift.Ui.Controls;

// 2nd-order LP magnitude response over 20 Hz - 18 kHz, log frequency axis,
// dB magnitude axis. Cutoff/resonance changes redraw via PropertyChanged.
//
// |H(f)| = 1 / sqrt((1 - r^2)^2 + (r/Q)^2)  where r = f / cutoff and
// Q is mapped from the 0..1 resonance knob with a comfortable curve
// (low Q at 0, ~25 at 1 -- a noticeable peak without screaming).
public sealed class FilterResponseCurve : Control
{
    public static readonly StyledProperty<FilterParams?> SourceProperty =
        AvaloniaProperty.Register<FilterResponseCurve, FilterParams?>(nameof(Source));

    public static readonly StyledProperty<IBrush> AccentProperty =
        AvaloniaProperty.Register<FilterResponseCurve, IBrush>(nameof(Accent),
            new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x3C)));

    private static readonly ImmutableSolidColorBrush BgBrush = new(Color.FromRgb(0x0F, 0x14, 0x1B));
    private static readonly ImmutableSolidColorBrush GridBrush = new(Color.FromRgb(0x22, 0x2B, 0x36));
    private static readonly ImmutableSolidColorBrush GridLabelBrush = new(Color.FromRgb(0x4A, 0x52, 0x60));

    private const double FreqMin = 20.0;
    private const double FreqMax = 18000.0;
    private const double DbMin = -36.0;
    private const double DbMax = 12.0;

    static FilterResponseCurve()
    {
        SourceProperty.Changed.AddClassHandler<FilterResponseCurve>((c, e) => c.OnSourceChanged(e));
        AffectsRender<FilterResponseCurve>(AccentProperty);
    }

    public FilterParams? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public IBrush Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsFinite(availableSize.Width) ? availableSize.Width : 200;
        return new Size(w, 56);
    }

    private void OnSourceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is FilterParams oldP)
        {
            oldP.PropertyChanged -= OnParamsChanged;
        }

        if (e.NewValue is FilterParams newP)
        {
            newP.PropertyChanged += OnParamsChanged;
        }

        InvalidateVisual();
    }

    private void OnParamsChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 4 || h <= 4)
        {
            return;
        }

        ctx.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h), 3, 3);

        // Grid: vertical lines at decade marks (100, 1k, 10k); horizontal at 0 dB.
        var gridPen = new ImmutablePen(GridBrush, 1);
        var pad = 4.0;
        var innerW = w - pad * 2;
        var innerH = h - pad * 2;

        var logMin = Math.Log10(FreqMin);
        var logMax = Math.Log10(FreqMax);
        var logSpan = logMax - logMin;

        double FreqToX(double f)
        {
            var t = (Math.Log10(f) - logMin) / logSpan;
            return pad + t * innerW;
        }

        double DbToY(double db)
        {
            var t = (db - DbMin) / (DbMax - DbMin);
            return pad + (1 - t) * innerH;
        }

        foreach (var f in (double[])[100, 1000, 10000])
        {
            var x = FreqToX(f);
            ctx.DrawLine(gridPen, new Point(x, pad), new Point(x, pad + innerH));
        }

        var y0 = DbToY(0);
        ctx.DrawLine(gridPen, new Point(pad, y0), new Point(pad + innerW, y0));

        var src = Source;
        if (src is null)
        {
            return;
        }

        var cutoff = Math.Clamp(src.Cutoff, FreqMin, FreqMax);
        var res = Math.Clamp(src.Resonance, 0f, 1f);
        // Q curve: gentle base, sharper peak as resonance approaches 1.
        var q = 0.7 + Math.Pow(res, 2.4) * 25.0;

        var color = (Accent as ISolidColorBrush)?.Color ?? Color.FromRgb(0xFF, 0x9A, 0x3C);

        // Sample the response and build a path.
        var samples = Math.Max(64, (int)innerW);
        var path = new PathGeometry();
        var fig = new PathFigure { IsClosed = false };

        for (var i = 0; i <= samples; i++)
        {
            var t = i / (double)samples;
            var f = Math.Pow(10, logMin + t * logSpan);
            var r = f / cutoff;
            var rr = r * r;
            var denom = Math.Sqrt(Math.Pow(1 - rr, 2) + rr / (q * q));
            var mag = denom > 1e-9 ? 1.0 / denom : 1e9;
            var db = Math.Clamp(20 * Math.Log10(Math.Max(mag, 1e-6)), DbMin, DbMax);

            var x = pad + t * innerW;
            var y = DbToY(db);

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

        // Filled area under the curve (faint).
        var fillFig = new PathFigure { StartPoint = new Point(pad, pad + innerH), IsClosed = true };
        foreach (var seg in fig.Segments!)
        {
            fillFig.Segments!.Add(seg);
        }

        fillFig.Segments!.Add(new LineSegment { Point = new Point(pad + innerW, pad + innerH) });
        var fill = new PathGeometry();
        fill.Figures!.Add(fillFig);
        ctx.DrawGeometry(new ImmutableSolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
            null, fill);

        var pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1.6,
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        ctx.DrawGeometry(null, pen, path);

        // Cutoff cursor.
        var cx = FreqToX(cutoff);
        ctx.DrawLine(new ImmutablePen(
                new ImmutableSolidColorBrush(Color.FromArgb(0xB0, color.R, color.G, color.B)),
                1,
                new ImmutableDashStyle([2, 3], 0)),
            new Point(cx, pad), new Point(cx, pad + innerH));
    }
}
