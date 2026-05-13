using System.Runtime.CompilerServices;

namespace Drift.Engine.Dsp.Lut;

/// <summary>
/// <c>2^x</c> via LUT + lerp over <c>[XMin, XMax]</c>. Not wired into the engine.
/// Default range is wide enough for typical semitone-ish exponents in a voice loop.
/// Replaces <see cref="MathF.Pow"/> with base 2: <c>MathF.Pow(2f, x)</c> for <c>x</c> in <c>[XMin, XMax]</c>.
/// </summary>
public static class FastExp2
{
    private const int Size = 4096;
    private static readonly float[] Table = new float[Size];
    private const float XMin = -24f;
    private const float XMax = 24f;
    private static readonly float InvSpan = (Size - 1) / (XMax - XMin);

    static FastExp2()
    {
        for (var i = 0; i < Size; i++)
        {
            var x = XMin + (XMax - XMin) * i / (Size - 1);
            Table[i] = MathF.Pow(2f, x);
        }
    }

    /// <summary>Replaces <c>MathF.Pow(2f, x)</c> for <c>x</c> in the built-in <c>[XMin, XMax]</c> range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Exp2(float x)
    {
        if (x <= XMin)
        {
            return Table[0];
        }

        if (x >= XMax)
        {
            return Table[Size - 1];
        }

        var t = (x - XMin) * InvSpan;
        var i0 = (int)t;
        var frac = t - i0;
        var i1 = i0 + 1;
        if (i1 >= Size)
        {
            i1 = Size - 1;
        }

        return Table[i0] + (Table[i1] - Table[i0]) * frac;
    }
}
