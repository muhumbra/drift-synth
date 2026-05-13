namespace Drift.Engine.Midi;

public enum MidiKind : byte
{
    NoteOn,
    NoteOff,
    Cc,
    PitchBend
}

// Compact value type so the audio thread drains the queue without GC pressure.
//   NoteOn:    Data1 = note,        Data2 = velocity (0..127)
//   NoteOff:   Data1 = note,        Data2 = release velocity (often 0)
//   Cc:        Data1 = controller,  Data2 = value (0..127)
//   PitchBend: Data1 = ignored,     Data2 = 14-bit value (0..16383, centred at 8192)
public readonly record struct MidiEvent(MidiKind Kind, byte Channel, byte Data1, int Data2);