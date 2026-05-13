using System.Runtime.CompilerServices;

namespace Drift.Engine.Dsp.Lut;

/// <summary>
/// <c>tanh(x)</c> via symmetric LUT + lerp on <c>[-XMax, XMax]</c>. Outside range returns ±1.
/// Not wired into the engine.
/// Replaces <see cref="MathF.Tanh"/> for <c>x</c> in the table range; outside, matches <c>tanh</c> asymptotes (±1).
/// </summary>
public static class FastTanh
{
    private const int HalfSize = 2048;
    private const float XMax = 6f;
    private static readonly float[] Table = new float[HalfSize + 1];

    static FastTanh()
    {
        for (var i = 0; i <= HalfSize; i++)
        {
            var x = i * (XMax / HalfSize);
            Table[i] = MathF.Tanh(x);
        }
    }

    /// <summary>Replaces <see cref="MathF.Tanh"/> for <c>|x| ≤ XMax</c>; outside, returns ±1 (asymptote).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tanh(float x)
    {
        var ax = x < 0f ? -x : x;
        if (ax >= XMax)
        {
            return x < 0f ? -1f : 1f;
        }

        var t = ax * (HalfSize / XMax);
        var i0 = (int)t;
        var frac = t - i0;
        var i1 = i0 + 1;
        if (i1 > HalfSize)
        {
            i1 = HalfSize;
        }

        var y = Table[i0] + (Table[i1] - Table[i0]) * frac;
        return x < 0f ? -y : y;
    }
}
