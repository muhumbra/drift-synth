using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Drift.Ui.Controls;

// Custom rotary knob. 270 degree sweep from 7 o'clock (min) to 5 o'clock (max).
//
// Interactions:
//   - Vertical drag: down lowers, up raises. Hold Shift for fine control (10x slower).
//   - Mouse wheel: small steps, hold Shift for tiny.
//   - Double-click: snap to DefaultValue.
//
// Drawing layers (back to front):
//   - Body radial gradient (faux 3D)
//   - Outer rim ring
//   - Track arc (full 270 unfilled)
//   - Value arc (start..current in accent color, glows)
//   - Indicator notch from center to rim at current angle
//   - Caption above + value below
public sealed class Knob : Control
{
    private const double StartAngleDeg = 135.0; // 7 o'clock
    private const double SweepDeg = 270.0; // ends at 5 o'clock

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Value), 0.5,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: (k, v) =>
            {
                var knob = (Knob)k;
                v = Math.Clamp(v, knob.Minimum, knob.Maximum);
                if (knob.Step > 0)
                {
                    v = Math.Round(v / knob.Step) * knob.Step;
                }

                return v;
            });

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Step));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Maximum), 1);

    public static readonly StyledProperty<double> DefaultValueProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(DefaultValue), 0.5);

    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Caption), "");

    public static readonly StyledProperty<string> UnitsProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Units), "");

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<Knob, int>(nameof(Decimals), 2);

    public static readonly StyledProperty<bool> LogarithmicProperty =
        AvaloniaProperty.Register<Knob, bool>(nameof(Logarithmic));

    public static readonly StyledProperty<bool> BipolarProperty =
        AvaloniaProperty.Register<Knob, bool>(nameof(Bipolar));

    public static readonly StyledProperty<IBrush> AccentProperty =
        AvaloniaProperty.Register<Knob, IBrush>(nameof(Accent),
            new ImmutableSolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF)));

    // Stable identifier used to wire MIDI CC bindings. When set and the knob
    // attaches to the visual tree, the global MidiCcMap learns about this knob
    // so incoming CCs can drive the underlying patch parameter directly.
    public static readonly StyledProperty<string?> ParamIdProperty =
        AvaloniaProperty.Register<Knob, string?>(nameof(ParamId));

    private static readonly ImmutableSolidColorBrush KnobOuterFillBrush =
        new(Color.FromRgb(0x0F, 0x14, 0x1B));
    private static readonly ImmutableSolidColorBrush KnobRimBrush =
        new(Color.FromRgb(0x3D, 0x4A, 0x5C));
    private static readonly ImmutablePen KnobRimPen = new(KnobRimBrush);
    private static readonly ImmutableSolidColorBrush KnobTrackBrush =
        new(Color.FromRgb(0x22, 0x2B, 0x36));
    private static readonly ImmutablePen KnobTrackPen =
        new(KnobTrackBrush, 4, lineCap: PenLineCap.Round);
    private static readonly RadialGradientBrush KnobBodyRadialBrush = CreateKnobBodyRadialBrush();
    private static readonly ImmutableSolidColorBrush KnobDefaultAccentBrush =
        new(Color.FromRgb(0x00, 0xD4, 0xFF));
    private static readonly Typeface CaptionTypeface = new("Inter, Segoe UI");
    private static readonly Typeface ValueTypeface = new("Cascadia Mono, Consolas, Courier New");
    private static readonly ImmutableSolidColorBrush CaptionMutedBrush =
        new(Color.FromRgb(0x7B, 0x85, 0x95));

    private bool _dragging;
    private Point _dragOriginPoint;
    private double _dragOriginValue;
    private string? _registeredParamId;

    static Knob()
    {
        AffectsRender<Knob>(ValueProperty, MinimumProperty, MaximumProperty, CaptionProperty, UnitsProperty,
            AccentProperty, BipolarProperty);
    }

    private static RadialGradientBrush CreateKnobBodyRadialBrush()
    {
        var bodyHi = Color.FromRgb(0x2A, 0x33, 0x40);
        var bodyLo = Color.FromRgb(0x10, 0x14, 0x1B);
        return new RadialGradientBrush
        {
            Center = new RelativePoint(0.4, 0.3, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.4, 0.3, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.7, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.7, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(bodyHi, 0),
                new GradientStop(bodyLo, 1)
            }
        };
    }

    public Knob()
    {
        Width = 64;
        Height = 86;
        Focusable = true;
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Units
    {
        get => GetValue(UnitsProperty);
        set => SetValue(UnitsProperty, value);
    }

    public int Decimals
    {
        get => GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    public bool Logarithmic
    {
        get => GetValue(LogarithmicProperty);
        set => SetValue(LogarithmicProperty, value);
    }

    public bool Bipolar
    {
        get => GetValue(BipolarProperty);
        set => SetValue(BipolarProperty, value);
    }

    public IBrush Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public string? ParamId
    {
        get => GetValue(ParamIdProperty);
        set => SetValue(ParamIdProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TryRegisterParam();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnregisterParam();
    }

    private void TryRegisterParam()
    {
        var map = MidiCcRegistry.Map;
        var patch = MidiCcRegistry.Patch;
        var paramId = ParamId;
        if (map is null || patch is null || string.IsNullOrEmpty(paramId))
        {
            return;
        }

        var setter = KnobParamSetters.Build(paramId, patch);
        if (setter is null)
        {
            return;
        }

        map.RegisterParam(paramId, setter, Minimum, Maximum, Logarithmic);
        _registeredParamId = paramId;
    }

    private void UnregisterParam()
    {
        if (_registeredParamId is null)
        {
            return;
        }

        MidiCcRegistry.Map?.UnregisterParam(_registeredParamId);
        _registeredParamId = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.ClickCount == 2)
        {
            Value = DefaultValue;
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragging = true;
            _dragOriginPoint = e.GetPosition(this);
            _dragOriginValue = Value;
            e.Pointer.Capture(this);
            Focus();
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        var dy = _dragOriginPoint.Y - e.GetPosition(this).Y; // up positive
        var sensitivity = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? 400.0 : 120.0;
        var normDelta = dy / sensitivity;

        var range = Maximum - Minimum;
        if (Logarithmic)
        {
            // multiplicative drag in log space
            var minSafe = Math.Max(Minimum, 1e-6);
            var logSpan = Math.Log(Maximum / minSafe);
            var startLog = Math.Log(Math.Max(_dragOriginValue, minSafe));
            var newLog = startLog + normDelta * logSpan;
            Value = Math.Clamp(Math.Exp(newLog), Minimum, Maximum);
        }
        else
        {
            Value = Math.Clamp(_dragOriginValue + normDelta * range, Minimum, Maximum);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var range = Maximum - Minimum;
        var step = ((e.KeyModifiers & KeyModifiers.Shift) != 0 ? 0.005 : 0.025) * range;
        Value = Math.Clamp(Value + e.Delta.Y * step, Minimum, Maximum);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(64, 86);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        // Reserve 16px above for the caption text so the ring doesn't clip it,
        // and 14px below for the value readout.
        var knobSize = Math.Min(w, h - 34);
        var cx = w / 2;
        var cy = 16 + knobSize / 2;
        var rOuter = knobSize / 2 - 2;
        var rTrack = rOuter - 2;
        var rBody = rOuter - 7;

        var accent = Accent as ISolidColorBrush ?? KnobDefaultAccentBrush;
        var accentColor = accent.Color;

        // outer rim
        ctx.DrawEllipse(KnobOuterFillBrush, KnobRimPen, new Point(cx, cy), rOuter, rOuter);

        // track arc (full sweep, dim)
        ctx.DrawGeometry(null, KnobTrackPen, BuildArc(new Point(cx, cy), rTrack, StartAngleDeg, SweepDeg));

        // value arc
        var normalisedValue = NormaliseToUnit(Value);
        if (Bipolar)
        {
            // Center the fill at 12 o'clock; positive sweeps right, negative sweeps left.
            var centerNorm = 0.5;
            var lo = Math.Min(centerNorm, normalisedValue);
            var hi = Math.Max(centerNorm, normalisedValue);
            var startA = StartAngleDeg + lo * SweepDeg;
            var sweepA = (hi - lo) * SweepDeg;
            if (sweepA > 0.5)
            {
                var arcPen = new ImmutablePen(new ImmutableSolidColorBrush(accentColor), 4, lineCap: PenLineCap.Round);
                ctx.DrawGeometry(null, arcPen, BuildArc(new Point(cx, cy), rTrack, startA, sweepA));
            }
        }
        else
        {
            var sweepA = normalisedValue * SweepDeg;
            if (sweepA > 0.5)
            {
                var arcPen = new ImmutablePen(new ImmutableSolidColorBrush(accentColor), 4, lineCap: PenLineCap.Round);
                ctx.DrawGeometry(null, arcPen, BuildArc(new Point(cx, cy), rTrack, StartAngleDeg, sweepA));
            }
        }

        // body (radial gradient for faux highlight)
        ctx.DrawEllipse(KnobBodyRadialBrush, KnobRimPen, new Point(cx, cy), rBody, rBody);

        // indicator notch
        var angle = (StartAngleDeg + normalisedValue * SweepDeg) * Math.PI / 180.0;
        var nx0 = cx + Math.Cos(angle) * (rBody * 0.35);
        var ny0 = cy + Math.Sin(angle) * (rBody * 0.35);
        var nx1 = cx + Math.Cos(angle) * (rBody - 1);
        var ny1 = cy + Math.Sin(angle) * (rBody - 1);
        var notchPen = new ImmutablePen(new ImmutableSolidColorBrush(accentColor), 2.4, lineCap: PenLineCap.Round);
        ctx.DrawLine(notchPen, new Point(nx0, ny0), new Point(nx1, ny1));

        // notch glow dot
        ctx.DrawEllipse(new ImmutableSolidColorBrush(accentColor), null, new Point(nx1, ny1), 2.2, 2.2);

        // caption (above, dim) -- vertically centred in the 16px header strip
        var captionFt = new FormattedText(
            (Caption ?? "").ToUpperInvariant(),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            CaptionTypeface,
            9,
            CaptionMutedBrush);
        ctx.DrawText(captionFt, new Point((w - captionFt.Width) / 2, 1));

        // value text below
        var valueText = FormatValue();
        var valueFt = new FormattedText(
            valueText,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            ValueTypeface,
            10,
            new ImmutableSolidColorBrush(accentColor));
        ctx.DrawText(valueFt, new Point((w - valueFt.Width) / 2, h - 14));
    }

    private double NormaliseToUnit(double v)
    {
        var range = Maximum - Minimum;
        if (range <= 0)
        {
            return 0;
        }

        if (Logarithmic)
        {
            var minSafe = Math.Max(Minimum, 1e-6);
            return Math.Clamp(Math.Log(Math.Max(v, minSafe) / minSafe) / Math.Log(Maximum / minSafe), 0, 1);
        }

        return Math.Clamp((v - Minimum) / range, 0, 1);
    }

    private string FormatValue()
    {
        var fmt = Decimals switch
        {
            0 => "0",
            1 => "0.0",
            2 => "0.00",
            _ => "0.000"
        };
        var s = Value.ToString(fmt, CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(Units) ? s : s + Units;
    }

    private static Geometry BuildArc(Point center, double radius, double startDeg, double sweepDeg)
    {
        var startRad = startDeg * Math.PI / 180.0;
        var endRad = (startDeg + sweepDeg) * Math.PI / 180.0;
        var p1 = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
        var p2 = new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));

        var g = new PathGeometry();
        var f = new PathFigure { StartPoint = p1, IsClosed = false };
        f.Segments!.Add(new ArcSegment
        {
            Point = p2,
            Size = new Size(radius, radius),
            IsLargeArc = sweepDeg > 180,
            SweepDirection = SweepDirection.Clockwise
        });
        g.Figures!.Add(f);
        return g;
    }
}