using System.Runtime.CompilerServices;

namespace Drift.Engine.Dsp.Lut;

/// <summary>
/// Sine via 4096-point LUT + linear interpolation. Not wired into the engine.
/// Replaces hot-path <see cref="MathF.Sin"/> for a unit phase in <c>[0, 1)</c> (one cycle):
/// use <see cref="SinFromPhase01"/> instead of <c>MathF.Sin(phase01 * MathF.Tau)</c>.
/// <see cref="SinFromTurns"/> replaces <c>MathF.Sin(turns * MathF.Tau)</c> for arbitrary <c>turns</c> (wrapped).
/// </summary>
public static class FastSin
{
    private const int Size = 4096;
    private const int Mask = Size - 1;
    private static readonly float[] Table = new float[Size];

    static FastSin()
    {
        var phaseStep = MathF.Tau / Size;
        for (var i = 0; i < Size; i++)
        {
            Table[i] = MathF.Sin(i * phaseStep);
        }
    }

    /// <summary>Replaces <c>MathF.Sin(phase01 * MathF.Tau)</c> for <c>phase01</c> wrapped to a cycle.</summary>
    /// <param name="phase01">Phase in [0, 1) = one cycle. Values outside are wrapped with <see cref="LutFrac.Frac01"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SinFromPhase01(float phase01)
    {
        var p = LutFrac.Frac01(phase01);
        return SampleTable(p);
    }

    /// <summary>Replaces <c>MathF.Sin(turns * MathF.Tau)</c> with wrapping.</summary>
    /// <param name="turns">Full rotations (1 = 360°). Wrapped with <see cref="LutFrac.Frac01"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SinFromTurns(float turns) => SinFromPhase01(turns);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SampleTable(float phase01)
    {
        var index = phase01 * Size;
        var i0 = ((int)index) & Mask;
        var i1 = (i0 + 1) & Mask;
        var frac = index - (int)index;
        return Table[i0] + (Table[i1] - Table[i0]) * frac;
    }
}
