using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Drift.Engine.Midi;

namespace Drift.Ui.Controls;

// Four activity LEDs (note / other CC / pitch bend / mod wheel CC1) driven by
// MidiInputManager tick timestamps. UI-thread timer ~30 Hz; matches LevelMeter pattern.
public sealed class MidiActivityDots : Control
{
    public static readonly StyledProperty<MidiInputManager?> SourceProperty =
        AvaloniaProperty.Register<MidiActivityDots, MidiInputManager?>(nameof(Source));

    private const int HoldMs = 150;
    private const int FadeMs = 280;

    private static readonly ImmutableSolidColorBrush IdleBrush = new(Color.FromRgb(0x2A, 0x33, 0x40));
    private static readonly ImmutableSolidColorBrush LabelIdleBrush = new(Color.FromRgb(0x4A, 0x52, 0x60));

    private static readonly Color NoteColor = Color.FromRgb(0x37, 0xE0, 0x8C);
    private static readonly Color CcColor = Color.FromRgb(0xB9, 0x87, 0xFF);
    private static readonly Color BendColor = Color.FromRgb(0x00, 0xD4, 0xFF);
    private static readonly Color ModColor = Color.FromRgb(0xFF, 0x2D, 0x95);

    private DispatcherTimer? _timer;

    private double _nLevel;
    private double _ccLevel;
    private double _bendLevel;
    private double _modLevel;

    public MidiInputManager? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(118, 34);
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
            _nLevel = 0;
            _ccLevel = 0;
            _bendLevel = 0;
            _modLevel = 0;
        }
        else
        {
            _nLevel = ActivityLevel(src.LastNoteTickMs64);
            _ccLevel = ActivityLevel(src.LastCcTickMs64);
            _bendLevel = ActivityLevel(src.LastPitchBendTickMs64);
            _modLevel = ActivityLevel(src.LastModWheelTickMs64);
        }

        InvalidateVisual();
    }

    private static double ActivityLevel(long lastTickMs64)
    {
        if (lastTickMs64 == 0)
        {
            return 0;
        }

        var now = Environment.TickCount64;
        var dt = now - lastTickMs64;
        if (dt < 0)
        {
            dt = 0;
        }

        if (dt <= HoldMs)
        {
            return 1.0;
        }

        if (dt >= HoldMs + FadeMs)
        {
            return 0;
        }

        return 1.0 - (dt - HoldMs) / (double)FadeMs;
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var colW = w / 4.0;
        const double dotR = 5.0;
        var dotY = 4.0;
        var typeface = new Typeface("Inter, Segoe UI");

        DrawCell(ctx, 0 * colW, colW, dotY, dotR, "N", NoteColor, _nLevel, typeface);
        DrawCell(ctx, 1 * colW, colW, dotY, dotR, "CC", CcColor, _ccLevel, typeface);
        DrawCell(ctx, 2 * colW, colW, dotY, dotR, "BND", BendColor, _bendLevel, typeface);
        DrawCell(ctx, 3 * colW, colW, dotY, dotR, "MOD", ModColor, _modLevel, typeface);
    }

    private static void DrawCell(
        DrawingContext ctx,
        double x,
        double colWidth,
        double dotY,
        double dotR,
        string label,
        Color rgb,
        double level,
        Typeface typeface)
    {
        var cx = x + colWidth / 2.0;
        var fill = Blend(IdleBrush.Color, rgb, level);
        var dotBrush = new ImmutableSolidColorBrush(fill);

        if (level > 0.35)
        {
            var glow = new ImmutableSolidColorBrush(Color.FromArgb((byte)(40 + level * 80), rgb.R, rgb.G, rgb.B));
            ctx.DrawEllipse(glow, null, new Rect(cx - dotR - 3, dotY - 3, (dotR + 3) * 2, (dotR + 3) * 2));
        }

        ctx.DrawEllipse(dotBrush, null, new Rect(cx - dotR, dotY, dotR * 2, dotR * 2));

        var labelBrush = new ImmutableSolidColorBrush(Blend(LabelIdleBrush.Color, rgb, level * 0.85));
        var ft = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 8,
            labelBrush);
        ctx.DrawText(ft, new Point(cx - ft.Width / 2, dotY + dotR * 2 + 3));
    }

    private static Color Blend(Color a, Color b, double t)
    {
        if (t <= 0)
        {
            return a;
        }

        if (t >= 1)
        {
            return b;
        }

        var u = (float)t;
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * u),
            (byte)(a.G + (b.G - a.G) * u),
            (byte)(a.B + (b.B - a.B) * u));
    }
}
