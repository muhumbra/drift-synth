using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Drift.Engine.Dsp;
using Drift.Engine.Synth;

namespace Drift.Ui.Controls;

// Static preview of the LFO's shape (one and a bit cycles), with an animated
// vertical cursor that walks at the current rate so users can see how fast
// the modulation is moving even with the synth silent.
public sealed class LfoWaveformPreview : Control
{
    public static readonly StyledProperty<LfoParams?> SourceProperty =
        AvaloniaProperty.Register<LfoWaveformPreview, LfoParams?>(nameof(Source));

    public static readonly StyledProperty<IBrush> AccentProperty =
        AvaloniaProperty.Register<LfoWaveformPreview, IBrush>(nameof(Accent),
            new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x2D, 0x95)));

    private static readonly ImmutableSolidColorBrush BgBrush = new(Color.FromRgb(0x0F, 0x14, 0x1B));
    private static readonly ImmutableSolidColorBrush GridBrush = new(Color.FromRgb(0x22, 0x2B, 0x36));
    private static readonly ImmutableSolidColorBrush CursorBrush = new(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
    private static readonly double[] MidLineDashPattern = [2, 3];
    private static readonly ImmutableDashStyle MidLineDashStyle = new(MidLineDashPattern, 0);
    private static readonly ImmutablePen MidLineGridPen = new(GridBrush, 1, MidLineDashStyle);
    private static readonly ImmutablePen CursorLinePen = new(CursorBrush, 1);

    private DispatcherTimer? _timer;
    private double _phase;
    private long _lastTickMs;
    // Reusable noise series for SampleHold preview so the picture is steady.
    private readonly float[] _shSamples = new float[16];

    static LfoWaveformPreview()
    {
        SourceProperty.Changed.AddClassHandler<LfoWaveformPreview>((c, e) => c.OnSourceChanged(e));
        AffectsRender<LfoWaveformPreview>(AccentProperty);
    }

    public LfoWaveformPreview()
    {
        var rng = new FastRng(0xCAFEu);
        for (var i = 0; i < _shSamples.Length; i++)
        {
            _shSamples[i] = rng.NextFloat11();
        }
    }

    public LfoParams? Source
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _lastTickMs = Environment.TickCount64;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }
    }

    private void OnSourceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is LfoParams oldP)
        {
            oldP.PropertyChanged -= OnParamsChanged;
        }

        if (e.NewValue is LfoParams newP)
        {
            newP.PropertyChanged += OnParamsChanged;
        }

        InvalidateVisual();
    }

    private void OnParamsChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var src = Source;
        if (src is null)
        {
            return;
        }

        var now = Environment.TickCount64;
        var dtMs = now - _lastTickMs;
        _lastTickMs = now;

        // Walk the cursor at the LFO's actual rate. Cap dt so a paused window
        // doesn't whip the cursor on resume.
        if (dtMs > 250)
        {
            dtMs = 33;
        }

        var rate = Math.Clamp(src.Rate, 0.01f, 30f);
        _phase += rate * (dtMs / 1000.0);
        if (_phase >= 1)
        {
            _phase -= Math.Floor(_phase);
        }

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

        // Centre line.
        var midY = h / 2.0;
        ctx.DrawLine(MidLineGridPen, new Point(0, midY), new Point(w, midY));

        var src = Source;
        if (src is null)
        {
            return;
        }

        var color = (Accent as ISolidColorBrush)?.Color ?? Color.FromRgb(0xFF, 0x2D, 0x95);
        var amount = Math.Clamp(src.Amount, 0f, 1f);
        // Stroke alpha tracks Amount so a "0" LFO looks visibly inert.
        var strokeAlpha = (byte)(60 + amount * 195);
        var strokeColor = Color.FromArgb(strokeAlpha, color.R, color.G, color.B);
        var pen = new ImmutablePen(new ImmutableSolidColorBrush(strokeColor), 1.6,
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        // Show ~1.5 cycles so users can see the wave restart.
        const double cycles = 1.5;
        var pad = 3.0;
        var innerW = w - pad * 2;
        var innerH = h - pad * 2;
        var halfH = innerH / 2.0;

        var samples = Math.Max(48, (int)innerW);
        var path = new PathGeometry();
        var fig = new PathFigure { IsClosed = false };

        var shape = src.Shape;
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (double)samples * cycles;
            var phase = t - Math.Floor(t);
            var v = SampleShape(shape, phase, t);
            var x = pad + i * innerW / samples;
            var y = midY - v * halfH;

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

        // Cursor: vertical line at current phase within the first cycle.
        if (amount > 0.001f)
        {
            var cursorX = pad + _phase * (innerW / cycles);
            ctx.DrawLine(CursorLinePen, new Point(cursorX, pad), new Point(cursorX, h - pad));

            var cursorV = SampleShape(shape, _phase, _phase);
            var cursorY = midY - cursorV * halfH;
            var dotColor = Color.FromArgb(0xFF, color.R, color.G, color.B);
            ctx.DrawEllipse(new ImmutableSolidColorBrush(dotColor), null,
                new Point(cursorX, cursorY), 2.5, 2.5);
        }
    }

    private double SampleShape(LfoShape shape, double phase, double cyclePos)
    {
        switch (shape)
        {
            case LfoShape.Sine:
                return Math.Sin(phase * Math.PI * 2);
            case LfoShape.Triangle:
                return phase < 0.5 ? -1 + 4 * phase : 3 - 4 * phase;
            case LfoShape.Saw:
                return 2 * phase - 1;
            case LfoShape.Square:
                return phase < 0.5 ? 1 : -1;
            case LfoShape.SampleHold:
                var idx = (int)(cyclePos * 4) & (_shSamples.Length - 1);
                return _shSamples[idx];
            default:
                return 0;
        }
    }
}
