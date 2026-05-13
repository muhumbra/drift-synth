using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Drift.Engine.Midi;
using AvShapes = Avalonia.Controls.Shapes;

namespace Drift.Ui.Controls;

// Tiny three-state row shown beneath each learnable Knob.
//   - Idle (no binding) -> small "LEARN" button.
//   - Armed (waiting for next CC) -> "WAITING..." chip; click to cancel.
//   - Bound -> "CC 74" chip + "x" button to forget the binding.
//
// Reads the global MidiCcMap via MidiCcRegistry. Subscribes to Changed and
// marshals to the UI thread before rebuilding visual state.
public sealed class MidiBindRow : UserControl
{
    public static readonly StyledProperty<string?> ParamIdProperty =
        AvaloniaProperty.Register<MidiBindRow, string?>(nameof(ParamId));

    private static readonly ImmutableSolidColorBrush IdleFg = new(Color.FromRgb(0x4A, 0x52, 0x60));
    private static readonly ImmutableSolidColorBrush ArmedFg = new(Color.FromRgb(0xFF, 0x9A, 0x3C));
    private static readonly ImmutableSolidColorBrush BoundFg = new(Color.FromRgb(0x00, 0xD4, 0xFF));
    private static readonly ImmutableSolidColorBrush BoundBg = new(Color.FromArgb(0x33, 0x00, 0xD4, 0xFF));
    private static readonly ImmutableSolidColorBrush ArmedBg = new(Color.FromArgb(0x33, 0xFF, 0x9A, 0x3C));
    private static readonly ImmutableSolidColorBrush ChipBorder = new(Color.FromRgb(0x2D, 0x37, 0x44));

    private readonly Button _learnButton;
    private readonly Border _statusChip;
    private readonly TextBlock _statusText;
    private readonly Button _removeButton;

    static MidiBindRow()
    {
        ParamIdProperty.Changed.AddClassHandler<MidiBindRow>((row, _) => row.Refresh());
    }

    public MidiBindRow()
    {
        Height = 18;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Focusable = false;
        Background = Brushes.Transparent;

        _learnButton = new Button
        {
            Content = "LEARN",
            FontSize = 8,
            Padding = new Thickness(6, 1),
            MinHeight = 16,
            Foreground = IdleFg,
            BorderBrush = ChipBorder,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        };
        _learnButton.Click += OnLearnClick;

        _statusText = new TextBlock
        {
            FontSize = 9,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(6, 0, 4, 0),
            Foreground = BoundFg
        };

        _removeButton = new Button
        {
            Content = new AvShapes.Path
            {
                Data = Geometry.Parse("M 0 0 L 6 6 M 6 0 L 0 6"),
                Stroke = IdleFg,
                StrokeThickness = 1.4,
                Width = 6,
                Height = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Padding = new Thickness(3, 0, 4, 0),
            MinHeight = 16,
            MinWidth = 18,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0)
        };
        _removeButton.Click += OnRemoveClick;

        var chipContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        chipContent.Children.Add(_statusText);
        chipContent.Children.Add(_removeButton);

        _statusChip = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = ChipBorder,
            Background = BoundBg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = chipContent
        };

        var root = new Grid();
        root.Children.Add(_learnButton);
        root.Children.Add(_statusChip);
        Content = root;
    }

    public string? ParamId
    {
        get => GetValue(ParamIdProperty);
        set => SetValue(ParamIdProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (MidiCcRegistry.Map is { } map)
        {
            map.Changed += OnMapChanged;
        }

        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (MidiCcRegistry.Map is { } map)
        {
            map.Changed -= OnMapChanged;
        }
    }

    private void OnMapChanged(MidiCcMapChange change)
    {
        // Bound / Unbound / Armed / Disarmed all potentially affect this row's
        // visual: refresh on every event regardless of which paramId moved, since
        // a steal will fire two changes back-to-back and we want both rows updated.
        if (Dispatcher.UIThread.CheckAccess())
        {
            Refresh();
        }
        else
        {
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    private void Refresh()
    {
        var map = MidiCcRegistry.Map;
        var paramId = ParamId;

        if (map is null || string.IsNullOrEmpty(paramId))
        {
            _learnButton.IsVisible = false;
            _statusChip.IsVisible = false;
            return;
        }

        var cc = map.CcFor(paramId);
        var armed = string.Equals(map.ArmedParamId, paramId, StringComparison.Ordinal);

        if (cc.HasValue)
        {
            _learnButton.IsVisible = false;
            _statusChip.IsVisible = true;
            _statusChip.Background = BoundBg;
            _statusText.Foreground = BoundFg;
            _statusText.Text = $"CC {cc.Value}";
            _removeButton.IsVisible = true;
            ToolTip.SetTip(_statusChip, $"Bound to CC {cc.Value}. Click x to remove.");
        }
        else if (armed)
        {
            _learnButton.IsVisible = false;
            _statusChip.IsVisible = true;
            _statusChip.Background = ArmedBg;
            _statusText.Foreground = ArmedFg;
            _statusText.Text = "WAITING...";
            _removeButton.IsVisible = false;
            ToolTip.SetTip(_statusChip, "Move a knob or fader on your controller. Esc to cancel.");
        }
        else
        {
            _learnButton.IsVisible = true;
            _statusChip.IsVisible = false;
            ToolTip.SetTip(_learnButton, "Click, then move a knob/fader on your controller to bind it.");
        }
    }

    private void OnLearnClick(object? sender, RoutedEventArgs e)
    {
        var map = MidiCcRegistry.Map;
        var paramId = ParamId;
        if (map is null || string.IsNullOrEmpty(paramId))
        {
            return;
        }

        if (string.Equals(map.ArmedParamId, paramId, StringComparison.Ordinal))
        {
            map.Disarm();
        }
        else
        {
            map.Arm(paramId);
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        var map = MidiCcRegistry.Map;
        var paramId = ParamId;
        if (map is null || string.IsNullOrEmpty(paramId))
        {
            return;
        }

        map.Unbind(paramId);
    }
}
