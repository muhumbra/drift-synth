using System.Diagnostics;

namespace Drift.Engine.Engine;

// Lightweight per-block section timings for Mixer.Read. Audio thread only
// updates EMA fields when Enabled; UI thread reads floats (best-effort, no
// locks). When disabled, Mixer.Read skips all timestamp calls.
public sealed class MixerProfiler
{
    internal const float EmaAlpha = 0.05f;

    private static readonly float MicrosPerTick = 1_000_000f / Stopwatch.Frequency;

    private int _enabled;

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) != 0;
        set
        {
            var n = value ? 1 : 0;
            if (Volatile.Read(ref _enabled) == n)
            {
                return;
            }

            Volatile.Write(ref _enabled, n);
            if (n != 0)
            {
                ResetEma();
            }
        }
    }

    public float EmaUsSetup;
    public float EmaUsMidi;
    public float EmaUsArp;
    public float EmaUsVoices;
    public float EmaUsFx;
    public float EmaUsTotal;

    public ulong Blocks;

    public void ResetEma()
    {
        EmaUsSetup = 0;
        EmaUsMidi = 0;
        EmaUsArp = 0;
        EmaUsVoices = 0;
        EmaUsFx = 0;
        EmaUsTotal = 0;
        Blocks = 0;
    }

    public void RecordBlock(long t0, long t1, long t2, long t3, long t4, long t5)
    {
        if (Volatile.Read(ref _enabled) == 0)
        {
            return;
        }

        Update(ref EmaUsSetup, t0, t1);
        Update(ref EmaUsMidi, t1, t2);
        Update(ref EmaUsArp, t2, t3);
        Update(ref EmaUsVoices, t3, t4);
        Update(ref EmaUsFx, t4, t5);
        Update(ref EmaUsTotal, t0, t5);
        Blocks++;
    }

    private static void Update(ref float ema, long start, long end)
    {
        var span = end - start;
        if (span < 0)
        {
            span = 0;
        }

        var us = span * MicrosPerTick;
        ema = ema * (1f - EmaAlpha) + us * EmaAlpha;
    }
}
