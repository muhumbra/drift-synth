namespace Drift.Engine.Sequencer;

public sealed record ArpPreset(string Name, Action<ArpParams> Apply)
{
    public override string ToString()
    {
        return Name;
    }
}

public static class ArpPresets
{
    public static readonly ArpPreset[] All =
    {
        new("Classic Up", p => Set(p, ArpMode.Up, ArpRate.Sixteenth, 1, 0.5f, 0f, 120)),
        new("Octave Up", p => Set(p, ArpMode.Up, ArpRate.Sixteenth, 2, 0.5f, 0f, 120)),
        new("Big Climb", p => Set(p, ArpMode.Up, ArpRate.Eighth, 4, 0.6f, 0f, 110)),
        new("Slow Down", p => Set(p, ArpMode.Down, ArpRate.Eighth, 2, 0.7f, 0f, 100)),
        new("Bouncy", p => Set(p, ArpMode.UpDown, ArpRate.Sixteenth, 2, 0.6f, 0.15f, 120)),
        new("Trance Gate", p => Set(p, ArpMode.AsPlayed, ArpRate.Sixteenth, 1, 0.30f, 0f, 138)),
        new("Goa Trip", p => Set(p, ArpMode.Up, ArpRate.SixteenthTriplet, 2, 0.4f, 0f, 145)),
        new("Triplet Down", p => Set(p, ArpMode.Down, ArpRate.EighthTriplet, 2, 0.5f, 0f, 110)),
        new("Random Walk", p => Set(p, ArpMode.Random, ArpRate.Sixteenth, 2, 0.4f, 0.10f, 120)),
        new("Funky", p => Set(p, ArpMode.UpDown, ArpRate.Sixteenth, 1, 0.45f, 0.55f, 100)),
        new("Wide Sweep", p => Set(p, ArpMode.Up, ArpRate.ThirtySecond, 4, 0.5f, 0f, 90)),
        new("Pad Hold", p => Set(p, ArpMode.Chord, ArpRate.Quarter, 1, 0.95f, 0f, 90)),
        new("Stutter", p => Set(p, ArpMode.AsPlayed, ArpRate.ThirtySecond, 1, 0.25f, 0f, 130)),
        new("Slow Pad Arp", p => Set(p, ArpMode.AsPlayed, ArpRate.Quarter, 1, 0.9f, 0f, 80)),
        new("DnB Roll", p => Set(p, ArpMode.Up, ArpRate.Sixteenth, 2, 0.5f, 0f, 174))
    };

    private static void Set(ArpParams p, ArpMode mode, ArpRate rate, int oct, float gate, float swing, float bpm)
    {
        p.Mode = mode;
        p.Rate = rate;
        p.Octaves = oct;
        p.Gate = gate;
        p.Swing = swing;
        p.Bpm = bpm;
    }
}
