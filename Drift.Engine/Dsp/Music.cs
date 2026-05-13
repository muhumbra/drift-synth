namespace Drift.Engine.Dsp;

public static class Music
{
    public const float A4 = 440f;

    public static float MidiToHz(float midiNote)
    {
        return A4 * MathF.Pow(2f, (midiNote - 69f) / 12f);
    }
}
