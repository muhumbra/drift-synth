using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Drift.Engine.Synth;

namespace Drift.Ui.Controls;

// Small ADSR shape under the envelope knobs. Recomputes when any of A/D/S/R
// change. Pure UI: subscribes to the EnvelopeParams.PropertyChanged.
//
// The horizontal axis is sqrt-scaled time so very short attacks/decays still
// take up a visible fraction of the width instead of collapsing to a spike.
// A fixed sustain "hold" segment between decay and release shows the level.
public sealed class EnvelopeCurve : Control
{
    public static readonly StyledProperty<EnvelopeParams?> SourceProperty =
        AvaloniaProperty.Register<EnvelopeCurve, EnvelopeParams?>(nameof(Source));

    public static readonly StyledProperty<IBrush> AccentProperty =
        AvaloniaProperty.Register<EnvelopeCurve, IBrush>(nameof(Accent),
            new ImmutableSolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF)));

    private static readonly ImmutableSolidColorBrush GridBrush = new(Color.FromRgb(0x22, 0x2B, 0x36));
    private static readonly ImmutableSolidColorBrush BgBrush = new(Color.FromRgb(0x0F, 0x14, 0x1B));
    private static readonly ImmutablePen BaselinePen = new(GridBrush, 1);

    static EnvelopeCurve()
    {
        SourceProperty.Changed.AddClassHandler<EnvelopeCurve>((c, e) => c.OnSourceChanged(e));
        AffectsRender<EnvelopeCurve>(AccentProperty);
    }

    public EnvelopeParams? Source
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
        if (e.OldValue is EnvelopeParams oldP)
        {
            oldP.PropertyChanged -= OnParamsChanged;
        }

        if (e.NewValue is EnvelopeParams newP)
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

        // Faint baseline + sustain reference line.
        ctx.DrawLine(BaselinePen, new Point(0, h - 0.5), new Point(w, h - 0.5));

        var src = Source;
        if (src is null)
        {
            return;
        }

        var a = src.Attack;
        var d = src.Decay;
        var s = Math.Clamp(src.Sustain, 0f, 1f);
        var r = src.Release;

        // sqrt scaling on time so short stages stay visible.
        var sa = MathF.Sqrt(a);
        var sd = MathF.Sqrt(d);
        var sr = MathF.Sqrt(r);
        // Sustain hold segment size proportional to overall scale so it doesn't
        // dominate when other stages are short.
        var sh = (sa + sd + sr) * 0.4f + 0.05f;

        var total = sa + sd + sh + sr;
        if (total <= 0)
        {
            return;
        }

        var pad = 4.0;
        var innerW = w - pad * 2;
        var innerH = h - pad * 2;

        var xStart = pad;
        var yBottom = pad + innerH;
        var yTop = pad;

        var xPeak = xStart + innerW * (sa / total);
        var ySustain = yBottom - innerH * s;
        var xSustainStart = xPeak + innerW * (sd / total);
        var xSustainEnd = xSustainStart + innerW * (sh / total);
        var xRelease = xSustainEnd + innerW * (sr / total);

        // Filled area under the curve.
        var fillColor = (Accent as ISolidColorBrush)?.Color
                        ?? Color.FromRgb(0x00, 0xD4, 0xFF);
        var fillBrush = new ImmutableSolidColorBrush(
            Color.FromArgb(46, fillColor.R, fillColor.G, fillColor.B));

        var fill = new PathGeometry();
        var fillFig = new PathFigure { StartPoint = new Point(xStart, yBottom), IsClosed = true };
        fillFig.Segments!.Add(new LineSegment { Point = new Point(xPeak, yTop) });
        fillFig.Segments!.Add(new LineSegment { Point = new Point(xSustainStart, ySustain) });
        fillFig.Segments!.Add(new LineSegment { Point = new Point(xSustainEnd, ySustain) });
        fillFig.Segments!.Add(new LineSegment { Point = new Point(xRelease, yBottom) });
        fill.Figures!.Add(fillFig);
        ctx.DrawGeometry(fillBrush, null, fill);

        // Stroke on top.
        var strokeBrush = new ImmutableSolidColorBrush(fillColor);
        var pen = new ImmutablePen(strokeBrush, 1.6, lineCap: PenLineCap.Round,
            lineJoin: PenLineJoin.Round);

        var stroke = new PathGeometry();
        var strokeFig = new PathFigure { StartPoint = new Point(xStart, yBottom), IsClosed = false };
        strokeFig.Segments!.Add(new LineSegment { Point = new Point(xPeak, yTop) });
        strokeFig.Segments!.Add(new LineSegment { Point = new Point(xSustainStart, ySustain) });
        strokeFig.Segments!.Add(new LineSegment { Point = new Point(xSustainEnd, ySustain) });
        strokeFig.Segments!.Add(new LineSegment { Point = new Point(xRelease, yBottom) });
        stroke.Figures!.Add(strokeFig);
        ctx.DrawGeometry(null, pen, stroke);

        // Tiny dots at the four key vertices for legibility at small sizes.
        var dotBrush = new ImmutableSolidColorBrush(fillColor);
        ctx.DrawEllipse(dotBrush, null, new Point(xPeak, yTop), 1.8, 1.8);
        ctx.DrawEllipse(dotBrush, null, new Point(xSustainStart, ySustain), 1.6, 1.6);
        ctx.DrawEllipse(dotBrush, null, new Point(xSustainEnd, ySustain), 1.6, 1.6);
    }
}
