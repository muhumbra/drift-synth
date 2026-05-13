using Drift.Engine.Dsp.Lut;

namespace Drift.Engine.Dsp;

public static class Music
{
    private const float A4 = 440f;

    public static float MidiToHz(float midiNote)
    {
        return A4 * FastExp2.Exp2((midiNote - 69f) / 12f);
    }
}
