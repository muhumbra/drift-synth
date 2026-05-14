using BenchmarkDotNet.Attributes;
using Drift.Engine.Dsp.Lut;

namespace Drift.Benchmarks;

/// <summary>
/// Compares one-cycle sine from unit phase: <c>sin(phase01 * 2π)</c> vs <see cref="FastSin.SinFromPhase01"/>.
/// </summary>
public class SinTrigBenchmarks
{
    private const int Count = 1 << 16;
    private float[] _phase01 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _phase01 = new float[Count];
        var x = 0x9E3779B9u;
        for (var i = 0; i < Count; i++)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _phase01[i] = (x & 0xFFFFFF) / 16777216f;
        }

        _ = FastSin.SinFromPhase01(0f);
    }

    [Benchmark(Baseline = true)]
    public double MathSinDouble()
    {
        double sum = 0;
        for (var i = 0; i < Count; i++)
        {
            var p = _phase01[i];
            sum += Math.Sin(p * Math.Tau);
        }

        return sum;
    }

    [Benchmark]
    public float MathFSin()
    {
        float sum = 0;
        for (var i = 0; i < Count; i++)
        {
            var p = _phase01[i];
            sum += MathF.Sin(p * MathF.Tau);
        }

        return sum;
    }

    [Benchmark]
    public float FastSinLut()
    {
        float sum = 0;
        for (var i = 0; i < Count; i++)
            sum += FastSin.SinFromPhase01(_phase01[i]);
        return sum;
    }
}
