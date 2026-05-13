using System.Runtime.CompilerServices;

namespace Drift.Engine.Dsp.Lut;

/// <summary>
/// Helpers for wrapping phase / position into [0, 1). Not wired into the engine —
/// for use with LUT prototypes under <c>Drift.Engine.Dsp.Lut</c>.
/// <see cref="Frac01"/> is equivalent to <c>x - MathF.Floor(x)</c> (uses <see cref="MathF.Floor"/>).
/// </summary>
public static class LutFrac
{
    /// <summary>Wraps to <c>[0, 1)</c>; pairs with LUT sines instead of <c>MathF.Sin</c> on raw phase.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Frac01(float x) => x - MathF.Floor(x);
}
