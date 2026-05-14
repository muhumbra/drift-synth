using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Drift.Engine.Midi;

namespace Drift.Ui.Controls;

// Four binary activity LEDs (note / other CC / pitch bend / mod wheel CC1) driven by
// MidiInputManager tick timestamps. Labels are static AXAML; UI-thread timer ~30 Hz.
public partial class MidiActivityStrip : UserControl
{
    private const int HoldMs = 150;
    private const int FadeMs = 280;

    private static readonly IBrush DotIdle = new ImmutableSolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x40));
    private static readonly IBrush NoteLit = new ImmutableSolidColorBrush(Color.FromRgb(0x37, 0xE0, 0x8C));
    private static readonly IBrush CcLit = new ImmutableSolidColorBrush(Color.FromRgb(0xB9, 0x87, 0xFF));
    private static readonly IBrush BendLit = new ImmutableSolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF));
    private static readonly IBrush ModLit = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x2D, 0x95));

    public static readonly StyledProperty<MidiInputManager?> SourceProperty =
        AvaloniaProperty.Register<MidiActivityStrip, MidiInputManager?>(nameof(Source));

    private DispatcherTimer? _timer;

    public MidiActivityStrip()
    {
        InitializeComponent();
        DotNote.Fill = DotIdle;
        DotCc.Fill = DotIdle;
        DotBend.Fill = DotIdle;
        DotMod.Fill = DotIdle;
    }

    public MidiInputManager? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
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
            DotNote.Fill = DotIdle;
            DotCc.Fill = DotIdle;
            DotBend.Fill = DotIdle;
            DotMod.Fill = DotIdle;
            return;
        }

        DotNote.Fill = IsRecent(src.LastNoteTickMs64) ? NoteLit : DotIdle;
        DotCc.Fill = IsRecent(src.LastCcTickMs64) ? CcLit : DotIdle;
        DotBend.Fill = IsRecent(src.LastPitchBendTickMs64) ? BendLit : DotIdle;
        DotMod.Fill = IsRecent(src.LastModWheelTickMs64) ? ModLit : DotIdle;
    }

    private static bool IsRecent(long lastTickMs64)
    {
        if (lastTickMs64 == 0)
        {
            return false;
        }

        var now = Environment.TickCount64;
        var dt = now - lastTickMs64;
        if (dt < 0)
        {
            dt = 0;
        }

        return dt < HoldMs + FadeMs;
    }
}
