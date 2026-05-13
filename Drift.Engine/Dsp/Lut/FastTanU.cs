using System.Runtime.CompilerServices;

namespace Drift.Engine.Dsp.Lut;

/// <summary>
/// <c>tan(u)</c> for <c>u</c> in <c>[0, UMax]</c> with <c>UMax = π·0.49</c> (matches a typical SVF
/// prewarp where <c>u = π·fc/fs</c> and <c>fc</c> is clamped below ~0.49·Nyquist). Not wired into the engine.
/// Replaces <see cref="MathF.Tan"/> for <c>u</c> in this range.
/// <see cref="TanPiFcOverFs"/> replaces <c>MathF.Tan(MathF.PI * cutoffHz / sampleRate)</c>.
/// </summary>
public static class FastTanU
{
    private const int Size = 4096;
    private static readonly float[] Table = new float[Size];
    private static readonly float UMax = MathF.PI * 0.49f;

    static FastTanU()
    {
        for (var i = 0; i < Size; i++)
        {
            var u = i * (UMax / (Size - 1));
            Table[i] = MathF.Tan(u);
        }
    }

    /// <summary>Replaces <see cref="MathF.Tan"/> for <paramref name="u"/> in the table domain.</summary>
    /// <param name="u">Argument in radians; clamped to [0, UMax].</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tan(float u)
    {
        if (u <= 0f)
        {
            return 0f;
        }

        if (u >= UMax)
        {
            return Table[Size - 1];
        }

        var t = u * ((Size - 1) / UMax);
        var i0 = (int)t;
        var frac = t - i0;
        var i1 = i0 + 1;
        if (i1 >= Size)
        {
            i1 = Size - 1;
        }

        return Table[i0] + (Table[i1] - Table[i0]) * frac;
    }

    /// <summary>Replaces <c>MathF.Tan(MathF.PI * cutoffHz / sampleRate)</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float TanPiFcOverFs(float cutoffHz, float sampleRate)
    {
        if (sampleRate <= 0f)
        {
            return 0f;
        }

        return Tan(MathF.PI * cutoffHz / sampleRate);
    }
}
