using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Drift.Engine.Engine;

namespace Drift.Ui.Controls;

// Stereo level meter: two bars (smoothed RMS + peak line) and a clip LED.
// Optimized for UI-thread cost: no text, no per-frame allocations, conditional
// invalidation, static pens/brushes, cheap amplitude mapping, ~20 Hz timer.
public sealed class LevelMeter : Control
{
    private const float ClipThreshold = 0.99f;
    private const long PeakHoldMs = 600;
    private const long ClipLatchMs = 500;
    /// <summary>Minimum change in normalized bar position (0..1) before repainting.</summary>
    private const float RepaintEpsilon = 0.00035f;

    public static readonly StyledProperty<LevelMonitor?> SourceProperty =
        AvaloniaProperty.Register<LevelMeter, LevelMonitor?>(nameof(Source));

    private static readonly ImmutableSolidColorBrush BgBrush = new(Color.FromRgb(0x12, 0x18, 0x22));
    private static readonly ImmutableSolidColorBrush BorderBrush = new(Color.FromRgb(0x2A, 0x33, 0x40));
    private static readonly ImmutableSolidColorBrush RmsFillBrush = new(Color.FromRgb(0x30, 0xC0, 0x70));
    private static readonly ImmutableSolidColorBrush ClipOnBrush = new(Color.FromRgb(0xFF, 0x40, 0x50));
    private static readonly ImmutableSolidColorBrush ClipOffBrush = new(Color.FromRgb(0x33, 0x1C, 0x1F));
    private static readonly ImmutableSolidColorBrush PeakTickBrush = new(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));

    private static readonly ImmutablePen BarBorderPen = new(BorderBrush);
    private static readonly ImmutablePen PeakPen = new(PeakTickBrush, 1.5, lineCap: PenLineCap.Round);

    private long _clipTickL = -1;
    private long _clipTickR = -1;
    private float _peakHoldL, _peakHoldR;
    private long _peakHoldTickL;
    private long _peakHoldTickR;

    private float _rmsL, _rmsR;

    private DispatcherTimer? _timer;

    // Last painted display snapshot (updated at end of Render).
    private bool _havePainted;
    private double _paintW, _paintH;
    private float _paintRmsL, _paintRmsR, _paintPeakL, _paintPeakR;
    private bool _paintClip;

    public LevelMonitor? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(200, 28);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var src = Source;
        if (src is null)
        {
            return;
        }

        var pL = src.PeakL;
        var pR = src.PeakR;
        var rL = src.RmsL;
        var rR = src.RmsR;

        // RMS: max(new, prev * decay) -- instant attack, ~150 ms release at 50 Hz.
        _rmsL = Math.Max(rL, _rmsL * 0.78f);
        _rmsR = Math.Max(rR, _rmsR * 0.78f);

        var now = Environment.TickCount64;

        if (pL > _peakHoldL)
        {
            _peakHoldL = pL;
            _peakHoldTickL = now;
        }
        else if (now - _peakHoldTickL > PeakHoldMs)
        {
            _peakHoldL *= 0.85f;
        }

        if (pR > _peakHoldR)
        {
            _peakHoldR = pR;
            _peakHoldTickR = now;
        }
        else if (now - _peakHoldTickR > PeakHoldMs)
        {
            _peakHoldR *= 0.85f;
        }

        if (pL >= ClipThreshold)
        {
            _clipTickL = now;
        }

        if (pR >= ClipThreshold)
        {
            _clipTickR = now;
        }

        if (NeedsRepaint())
        {
            InvalidateVisual();
        }
    }

    private bool NeedsRepaint()
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var nRmsL = AmpToNorm(_rmsL);
        var nRmsR = AmpToNorm(_rmsR);
        var nPeakL = AmpToNorm(_peakHoldL);
        var nPeakR = AmpToNorm(_peakHoldR);
        var clip = ClipDisplay(Environment.TickCount64);

        if (!_havePainted || w != _paintW || h != _paintH)
        {
            return true;
        }

        if (clip != _paintClip)
        {
            return true;
        }

        if (Differs(nRmsL, _paintRmsL) || Differs(nRmsR, _paintRmsR) ||
            Differs(nPeakL, _paintPeakL) || Differs(nPeakR, _paintPeakR))
        {
            return true;
        }

        return false;
    }

    private static bool Differs(float a, float b)
    {
        return (a > b ? a - b : b - a) > RepaintEpsilon;
    }

    private bool ClipDisplay(long nowTicks)
    {
        return (_clipTickL >= 0 && nowTicks - _clipTickL < ClipLatchMs) ||
               (_clipTickR >= 0 && nowTicks - _clipTickR < ClipLatchMs);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;

        const double clipW = 10;
        const double gap = 4;
        const double pad = 2;
        var barsX = pad;
        var barsW = Math.Max(4, w - pad - clipW - gap);
        const double barH = 9;
        const double barLY = 2;
        var barRY = barLY + barH + 2;

        DrawBar(ctx, barsX, barLY, barsW, barH, _rmsL, _peakHoldL);
        DrawBar(ctx, barsX, barRY, barsW, barH, _rmsR, _peakHoldR);

        var now = Environment.TickCount64;
        var clipping = ClipDisplay(now);
        var ledRect = new Rect(w - clipW, barLY + 1, clipW - 2, barH * 2 + 2);
        ctx.DrawRectangle(clipping ? ClipOnBrush : ClipOffBrush, null, ledRect);

        _paintW = w;
        _paintH = h;
        _paintRmsL = AmpToNorm(_rmsL);
        _paintRmsR = AmpToNorm(_rmsR);
        _paintPeakL = AmpToNorm(_peakHoldL);
        _paintPeakR = AmpToNorm(_peakHoldR);
        _paintClip = clipping;
        _havePainted = true;
    }

    private static void DrawBar(DrawingContext ctx, double x, double y, double w, double h, float rms, float peak)
    {
        var rect = new Rect(x, y, w, h);
        ctx.DrawRectangle(BgBrush, BarBorderPen, rect);

        var innerW = w - 2;
        if (innerW <= 0)
        {
            return;
        }

        var rmsW = AmpToNorm(rms) * innerW;
        if (rmsW > 0.5)
        {
            var fillRect = new Rect(x + 1, y + 1, Math.Min(rmsW, innerW), h - 2);
            ctx.DrawRectangle(RmsFillBrush, null, fillRect);
        }

        var peakX = AmpToNorm(peak) * innerW;
        if (peakX > 0.5)
        {
            var tickX = x + 1 + Math.Min(peakX, innerW);
            ctx.DrawLine(PeakPen, new Point(tickX, y + 1), new Point(tickX, y + h - 1));
        }
    }

    // Cheap mapping similar to dB-ish spread: quiet sources get more bar resolution than linear.
    private static float AmpToNorm(float amp)
    {
        if (amp <= 0f)
        {
            return 0f;
        }

        const double lo = 0.03162277660168379; // sqrt(0.001) ~ -60 dBFS ref amplitude
        var s = Math.Sqrt(amp);
        if (s <= lo)
        {
            return 0f;
        }

        var n = (float)((s - lo) / (1.0 - lo));
        return n > 1f ? 1f : n;
    }
}
