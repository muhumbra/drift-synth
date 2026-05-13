namespace Drift.Engine.Dsp;

/// <summary>
///     Tiny xorshift32 PRNG — allocation-free. Fine for audio noise, LFO sample-hold,
///     arp random step, patch randomize. Not cryptographic.
/// </summary>
public struct FastRng
{
    private uint _s;

    private static int _salt;

    public FastRng(uint seed)
    {
        _s = seed == 0 ? 0xC0FFEEu : seed;
    }

    /// <summary>Seed for one-off rolls (e.g. default patch randomize).</summary>
    public static FastRng CreateUncorrelated()
    {
        var x = unchecked((uint)Environment.TickCount);
        x ^= (uint)Interlocked.Increment(ref _salt) * 0x9E3779B9u;
        return new FastRng(x);
    }

    public uint NextU32()
    {
        var x = _s;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _s = x;
        return x;
    }

    /// <summary>Uniform in [0, 1).</summary>
    public float NextFloat01()
    {
        return NextU32() * (1f / (uint.MaxValue + 1f));
    }

    /// <summary>Approx uniform in [-1, 1].</summary>
    public float NextFloat11()
    {
        var u = NextU32();
        return u * (2f / uint.MaxValue) - 1f;
    }

    /// <summary>Uniform in <c>[0, maxExclusive)</c>; unbiased via rejection.</summary>
    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        var bound = (uint)maxExclusive;
        var limit = uint.MaxValue - (uint)(((ulong)uint.MaxValue + 1) % bound);
        while (true)
        {
            var r = NextU32();
            if (r <= limit)
            {
                return (int)(r % bound);
            }
        }
    }

    /// <summary>
    ///     Uniform in <c>[minInclusive, maxExclusive)</c> (same convention as <see cref="System.Random.Next(int,int)" />
    ///     ).
    /// </summary>
    public int Next(int minInclusive, int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minInclusive, maxExclusive);
        return minInclusive + Next(maxExclusive - minInclusive);
    }
}
