using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Drift.Engine.Engine;

namespace Drift.Ui.Controls;

// Stereo dBFS bar meter. Reads a LevelMonitor exposed by the audio engine on a
// 30 Hz UI timer, applies meter ballistics in the UI thread (instant peak
// attack, exponential RMS smoothing, peak-hold tick that drops after ~600 ms),
// and renders two bars + a clip LED + voice count in a single Avalonia Control.
public sealed class LevelMeter : Control
{
    private const float ClipThreshold = 0.99f;

    public static readonly StyledProperty<LevelMonitor?> SourceProperty =
        AvaloniaProperty.Register<LevelMeter, LevelMonitor?>(nameof(Source));

    private static readonly TimeSpan PeakHoldDuration = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan ClipLatchDuration = TimeSpan.FromMilliseconds(500);

    private static readonly ImmutableSolidColorBrush BgBrush = new(Color.FromRgb(0x12, 0x18, 0x22));
    private static readonly ImmutableSolidColorBrush BorderBrush = new(Color.FromRgb(0x2A, 0x33, 0x40));
    private static readonly ImmutableSolidColorBrush PeakTickBrush = new(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
    private static readonly ImmutableSolidColorBrush ClipOnBrush = new(Color.FromRgb(0xFF, 0x40, 0x50));
    private static readonly ImmutableSolidColorBrush ClipOffBrush = new(Color.FromRgb(0x33, 0x1C, 0x1F));
    private static readonly ImmutableSolidColorBrush LabelBrush = new(Color.FromRgb(0x6E, 0x78, 0x88));
    private static readonly ImmutableSolidColorBrush VoiceTextBrush = new(Color.FromRgb(0xC8, 0xD0, 0xDE));

    private static readonly ImmutableLinearGradientBrush BarBrush = new(
        new[]
        {
            new ImmutableGradientStop(0.00, Color.FromRgb(0x00, 0xC0, 0x60)),
            new ImmutableGradientStop(0.55, Color.FromRgb(0x6F, 0xE0, 0x4A)),
            new ImmutableGradientStop(0.80, Color.FromRgb(0xFF, 0xB3, 0x00)),
            new ImmutableGradientStop(1.00, Color.FromRgb(0xFF, 0x40, 0x40))
        },
        startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
        endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));

    private int _activeVoices;
    private DateTime _clipTimeL = DateTime.MinValue;
    private DateTime _clipTimeR = DateTime.MinValue;
    private float _peakHoldL, _peakHoldR;
    private DateTime _peakHoldTimeL = DateTime.MinValue;
    private DateTime _peakHoldTimeR = DateTime.MinValue;
    private int _polyphony;

    private float _rmsL, _rmsR;

    private DispatcherTimer? _timer;

    public LevelMonitor? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(220, 38);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
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

        // Atomic single-field reads (IEEE 754 32-bit aligned writes are atomic on .NET).
        var pL = src.PeakL;
        var pR = src.PeakR;
        var rL = src.RmsL;
        var rR = src.RmsR;
        _activeVoices = src.ActiveVoices;
        _polyphony = src.PolyphonyMax;

        // RMS: max(new, prev * decay) -- instant attack, 150 ms-ish release.
        _rmsL = Math.Max(rL, _rmsL * 0.78f);
        _rmsR = Math.Max(rR, _rmsR * 0.78f);

        var now = DateTime.UtcNow;

        if (pL > _peakHoldL)
        {
            _peakHoldL = pL;
            _peakHoldTimeL = now;
        }
        else if (now - _peakHoldTimeL > PeakHoldDuration)
        {
            _peakHoldL *= 0.85f;
        }

        if (pR > _peakHoldR)
        {
            _peakHoldR = pR;
            _peakHoldTimeR = now;
        }
        else if (now - _peakHoldTimeR > PeakHoldDuration)
        {
            _peakHoldR *= 0.85f;
        }

        if (pL >= ClipThreshold)
        {
            _clipTimeL = now;
        }

        if (pR >= ClipThreshold)
        {
            _clipTimeR = now;
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;

        const double clipW = 12;
        const double labelW = 18; // tiny "L" / "R" letters left of bars
        const double gap = 4;
        var barsX = labelW;
        var barsW = w - labelW - clipW - gap * 2;
        double barH = 11;
        double barLY = 2;
        var barRY = barLY + barH + 2;

        DrawBar(ctx, barsX, barLY, barsW, barH, _rmsL, _peakHoldL);
        DrawBar(ctx, barsX, barRY, barsW, barH, _rmsR, _peakHoldR);

        var typeface = new Typeface("Inter, Segoe UI");
        ctx.DrawText(MakeText("L", 9, LabelBrush, typeface), new Point(2, barLY - 1));
        ctx.DrawText(MakeText("R", 9, LabelBrush, typeface), new Point(2, barRY - 1));

        // Clip LED
        var now = DateTime.UtcNow;
        var clipping = now - _clipTimeL < ClipLatchDuration || now - _clipTimeR < ClipLatchDuration;
        var ledRect = new Rect(w - clipW, barLY + 2, clipW - 2, barH * 2 - 2);
        var ledRound = new RoundedRect(ledRect, 2);
        ctx.DrawRectangle(clipping ? ClipOnBrush : ClipOffBrush, null, ledRound);

        // Voice count, bottom row
        var poly = _polyphony > 0 ? _polyphony : 16;
        var voiceText = MakeText($"VOICES {_activeVoices} / {poly}", 9, VoiceTextBrush, typeface);
        ctx.DrawText(voiceText, new Point(barsX, barRY + barH + 1));

        // CLIP label below LED
        var clipText = MakeText("CLIP", 8, clipping ? ClipOnBrush : LabelBrush, typeface);
        ctx.DrawText(clipText, new Point(w - clipW - 2 + (clipW - clipText.Width) / 2, barRY + barH + 2));
    }

    private static void DrawBar(DrawingContext ctx, double x, double y, double w, double h, float rms, float peak)
    {
        var rect = new Rect(x, y, w, h);
        var rounded = new RoundedRect(rect, 2);
        ctx.DrawRectangle(BgBrush, new ImmutablePen(BorderBrush), rounded);

        var rmsW = AmpToNorm(rms) * (w - 2);
        if (rmsW > 0.5)
        {
            var fillRect = new Rect(x + 1, y + 1, rmsW, h - 2);
            ctx.DrawRectangle(BarBrush, null, new RoundedRect(fillRect, 1));
        }

        var peakX = AmpToNorm(peak) * (w - 2);
        if (peakX > 0.5)
        {
            var tickX = x + 1 + Math.Min(peakX, w - 2);
            ctx.DrawLine(new ImmutablePen(PeakTickBrush, 1.5), new Point(tickX, y + 1), new Point(tickX, y + h - 1));
        }
    }

    // -60 dBFS -> 0, 0 dBFS -> 1.
    private static double AmpToNorm(float amp)
    {
        if (amp <= 0.001f)
        {
            return 0;
        }

        var db = 20 * MathF.Log10(amp);
        return Math.Clamp((db + 60) / 60.0, 0, 1);
    }

    private static FormattedText MakeText(string s, double size, IBrush brush, Typeface tf)
    {
        return new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, size, brush);
    }
}