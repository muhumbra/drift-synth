namespace Drift.Engine.Sequencer;

// Note division for the arp clock. Stored as a record so the ComboBox can
// just call ToString() and get a friendly label like "1/16T".
public sealed record ArpRate(string Name, float BeatMultiplier)
{
    public static readonly ArpRate Quarter = new("1/4", 1f);
    public static readonly ArpRate QuarterTriplet = new("1/4T", 2f / 3f);
    public static readonly ArpRate Eighth = new("1/8", 0.5f);
    public static readonly ArpRate EighthTriplet = new("1/8T", 1f / 3f);
    public static readonly ArpRate Sixteenth = new("1/16", 0.25f);
    public static readonly ArpRate SixteenthTriplet = new("1/16T", 1f / 6f);
    public static readonly ArpRate ThirtySecond = new("1/32", 0.125f);

    public static readonly ArpRate[] All =
    {
        Quarter, QuarterTriplet,
        Eighth, EighthTriplet,
        Sixteenth, SixteenthTriplet,
        ThirtySecond
    };

    public override string ToString()
    {
        return Name;
    }
}

public enum ArpMode
{
    Up,
    Down,
    UpDown,
    DownUp,
    Random,
    AsPlayed,
    Chord
}
