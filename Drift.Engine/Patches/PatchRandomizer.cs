using Drift.Engine.Dsp;
using Drift.Engine.Synth;

namespace Drift.Engine.Patches;

// Smart-range patch randomizer. Each parameter is rolled within bounds chosen
// to land in musically useful territory rather than the absolute legal range
// (e.g. cutoff is log-uniform 250-7500 Hz, not 20-18000; resonance caps below
// self-oscillation; AmpEnv branches into pluck/pad characteristics).
//
// Intentionally untouched:
//   * Patch.Name -> set to "Random NNNN" so you can tell at a glance.
//   * Voice (Mono/Legato/Glide), Master.Volume, Polyphony, PitchBendRange,
//     and the entire Arp section. These are performance/global settings;
//     hammering them on every roll would be annoying.
public static class PatchRandomizer
{
    public static void Randomize(SynthPatch p, FastRng? rng = null)
    {
        var r = rng ?? FastRng.CreateUncorrelated();

        p.Name = $"Random {r.Next(1000, 9999)}";

        RandomizeOsc(p.Osc1, ref r, true);
        RandomizeOsc(p.Osc2, ref r, false);

        // MIXER -- sub and noise are seasoning, not staples.
        p.Mixer.SubLevel = Bool(ref r, 0.45f) ? Lin(ref r, 0.10f, 0.50f) : 0;
        p.Mixer.NoiseLevel = Bool(ref r, 0.20f) ? Lin(ref r, 0.02f, 0.12f) : 0;

        // FILTER
        p.Filter.Cutoff = Log(ref r, 250f, 7500f);
        p.Filter.Resonance = Lin(ref r, 0.10f, 0.65f);
        p.Filter.EnvAmount = Lin(ref r, -0.25f, 1.00f);
        p.Filter.KeyTrack = Lin(ref r, 0.00f, 1.00f);

        RandomizeAmpEnv(p.AmpEnv, ref r);

        // FILTER ENV -- generally short and snappy.
        p.FilterEnv.Attack = Log(ref r, 0.001f, 0.05f);
        p.FilterEnv.Decay = Log(ref r, 0.05f, 1.20f);
        p.FilterEnv.Sustain = Lin(ref r, 0.00f, 0.60f);
        p.FilterEnv.Release = Log(ref r, 0.05f, 0.80f);

        // LFO
        p.Lfo.Shape = Pick(ref r, EnumValues<LfoShape>());
        p.Lfo.Rate = Log(ref r, 0.30f, 8.0f);
        p.Lfo.Target = WeightedTarget(ref r);
        p.Lfo.Amount = p.Lfo.Target switch
        {
            LfoTarget.Off => 0,
            LfoTarget.Pitch => Lin(ref r, 0f, 0.15f), // small or it warbles wildly
            LfoTarget.Cutoff => Lin(ref r, 0f, 0.50f),
            LfoTarget.Amp => Lin(ref r, 0f, 0.30f),
            _ => 0
        };

        // DELAY -- often subtle, sometimes off.
        p.Delay.TimeMs = Lin(ref r, 90f, 480f);
        p.Delay.Feedback = Lin(ref r, 0.10f, 0.55f);
        p.Delay.Tone = Lin(ref r, 0.35f, 0.85f);
        p.Delay.Mix = Bool(ref r, 0.65f) ? Lin(ref r, 0.05f, 0.30f) : 0;

        // MASTER -- leave Volume alone. Reverb is part of the sound.
        p.Master.ReverbMix = Lin(ref r, 0.05f, 0.40f);
        p.Master.ReverbSize = Lin(ref r, 0.30f, 0.85f);
        p.Master.ReverbDamp = Lin(ref r, 0.30f, 0.75f);
    }

    private static void RandomizeOsc(OscParams o, ref FastRng r, bool primary)
    {
        o.Wave = WeightedWave(ref r);
        o.Octave = PickOctave(ref r);
        o.Semitone = primary ? PickSemitonePrimary(ref r) : PickSemitoneSecondary(ref r);
        o.FineCents = primary ? Lin(ref r, -6f, 6f) : Lin(ref r, -12f, 12f); // OSC2 detunes a touch wider
        o.Level = primary ? Lin(ref r, 0.7f, 1.0f) : Lin(ref r, 0.4f, 0.95f);
    }

    private static void RandomizeAmpEnv(EnvelopeParams e, ref FastRng r)
    {
        // 65% pluck / lead, 35% pad.
        if (Bool(ref r, 0.65f))
        {
            e.Attack = Log(ref r, 0.001f, 0.020f);
            e.Decay = Log(ref r, 0.05f, 0.6f);
            e.Sustain = Lin(ref r, 0.30f, 0.90f);
            e.Release = Log(ref r, 0.08f, 0.6f);
        }
        else
        {
            e.Attack = Log(ref r, 0.10f, 1.20f);
            e.Decay = Log(ref r, 0.20f, 1.50f);
            e.Sustain = Lin(ref r, 0.60f, 0.95f);
            e.Release = Log(ref r, 0.50f, 2.50f);
        }
    }

    // ---- helpers --------------------------------------------------------

    private static float Lin(ref FastRng r, float min, float max)
    {
        return min + r.NextFloat01() * (max - min);
    }

    private static float Log(ref FastRng r, float min, float max)
    {
        return min * MathF.Pow(max / min, r.NextFloat01());
    }

    private static bool Bool(ref FastRng r, float pTrue)
    {
        return r.NextFloat01() < pTrue;
    }

    private static T Pick<T>(ref FastRng r, T[] arr)
    {
        return arr[r.Next(arr.Length)];
    }

    private static T[] EnumValues<T>() where T : struct, Enum
    {
        return Enum.GetValues<T>();
    }

    private static T Weighted<T>(ref FastRng r, (T value, int weight)[] table)
    {
        var total = 0;
        for (var i = 0; i < table.Length; i++)
        {
            total += table[i].weight;
        }

        var roll = r.Next(total);
        var acc = 0;
        for (var i = 0; i < table.Length; i++)
        {
            acc += table[i].weight;
            if (roll < acc)
            {
                return table[i].value;
            }
        }

        return table[^1].value;
    }

    // Saw / square dominate (most useful for synth sounds), sine/triangle
    // sit in the background, noise is rare.
    private static Waveform WeightedWave(ref FastRng r)
    {
        return Weighted(ref r, new[]
        {
            (Waveform.Sine, 2),
            (Waveform.Triangle, 2),
            (Waveform.Saw, 4),
            (Waveform.Square, 3),
            (Waveform.Noise, 1)
        });
    }

    private static float PickOctave(ref FastRng r)
    {
        return Weighted(ref r, new (float, int)[]
        {
            (-2f, 1), (-1f, 3), (0f, 5), (1f, 2)
        });
    }

    // OSC1 mostly unison / octaves -- a clean tonal centre.
    private static float PickSemitonePrimary(ref FastRng r)
    {
        return Weighted(ref r, new (float, int)[]
        {
            (0f, 8), (-12f, 1), (12f, 1)
        });
    }

    // OSC2 likes intervals: fifth, fourth, octave, the occasional minor third
    // for menace. Keeps the patch musical without being chordal.
    private static float PickSemitoneSecondary(ref FastRng r)
    {
        return Weighted(ref r, new (float, int)[]
        {
            (0f, 6),
            (7f, 3), // perfect fifth
            (-5f, 2), // fifth below
            (5f, 2), // perfect fourth
            (-7f, 2),
            (3f, 1), // minor third
            (4f, 1), // major third
            (12f, 2),
            (-12f, 2)
        });
    }

    private static LfoTarget WeightedTarget(ref FastRng r)
    {
        return Weighted(ref r, new[]
        {
            (LfoTarget.Off, 1),
            (LfoTarget.Pitch, 2), // vibrato
            (LfoTarget.Cutoff, 4), // most musical
            (LfoTarget.Amp, 2)
        });
    }
}
