using BenchmarkDotNet.Attributes;
using Drift.Engine.Dsp;

namespace Drift.Benchmarks;

/// <summary>
/// Relative cost of <see cref="PolyBlepOscillator.Render"/> per <see cref="Waveform"/>.
/// </summary>
public class PolyBlepOscillatorBenchmarks
{
    private const int SampleRate = 48000;
    private const float FrequencyHz = 440f;
    private const int Count = 1 << 16;

    private PolyBlepOscillator _sine = null!;
    private PolyBlepOscillator _triangle = null!;
    private PolyBlepOscillator _saw = null!;
    private PolyBlepOscillator _square = null!;
    private PolyBlepOscillator _noise = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sine = new PolyBlepOscillator(SampleRate, 1)
        {
            Wave = Waveform.Sine,
            Frequency = FrequencyHz,
        };
        _triangle = new PolyBlepOscillator(SampleRate, 2)
        {
            Wave = Waveform.Triangle,
            Frequency = FrequencyHz,
        };
        _saw = new PolyBlepOscillator(SampleRate, 3)
        {
            Wave = Waveform.Saw,
            Frequency = FrequencyHz,
        };
        _square = new PolyBlepOscillator(SampleRate, 4)
        {
            Wave = Waveform.Square,
            Frequency = FrequencyHz,
        };
        _noise = new PolyBlepOscillator(SampleRate, 5)
        {
            Wave = Waveform.Noise,
            Frequency = FrequencyHz,
        };
    }

    [Benchmark(Baseline = true)]
    public float Sine()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += _sine.Render();
        return sum;
    }

    [Benchmark]
    public float Triangle()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += _triangle.Render();
        return sum;
    }

    [Benchmark]
    public float Saw()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += _saw.Render();
        return sum;
    }

    [Benchmark]
    public float Square()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += _square.Render();
        return sum;
    }

    [Benchmark]
    public float Noise()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += _noise.Render();
        return sum;
    }
}
