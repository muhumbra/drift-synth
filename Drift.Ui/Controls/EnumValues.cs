using Drift.Engine.Dsp;
using Drift.Engine.Sequencer;
using Drift.Engine.Synth;

namespace Drift.Ui.Controls;

public static class EnumValues
{
    public static Waveform[] Waveforms { get; } = Enum.GetValues<Waveform>();
    public static LfoShape[] LfoShapes { get; } = Enum.GetValues<LfoShape>();
    public static LfoTarget[] LfoTargets { get; } = Enum.GetValues<LfoTarget>();
    public static ArpRate[] ArpRates { get; } = ArpRate.All;
    public static ArpMode[] ArpModes { get; } = Enum.GetValues<ArpMode>();
    public static ArpPreset[] ArpPresetList { get; } = ArpPresets.All;
}