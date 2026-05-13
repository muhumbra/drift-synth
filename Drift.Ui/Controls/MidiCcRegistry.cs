using Drift.Engine.Midi;
using Drift.Engine.Synth;

namespace Drift.Ui.Controls;

// Process-wide accessor for the MIDI CC map and the live patch instance.
// Set by App during startup so Knob and MidiBindRow can hook in without each
// site needing extra XAML wiring. Single-window app -- a static here is fine.
public static class MidiCcRegistry
{
    public static MidiCcMap? Map { get; set; }
    public static SynthPatch? Patch { get; set; }
}
