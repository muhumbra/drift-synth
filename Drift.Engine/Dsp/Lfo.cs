namespace Drift.Engine.Dsp;

public enum LfoShape
{
    Sine,
    Triangle,
    Saw,
    Square,
    SampleHold
}

// Low-frequency oscillator. No anti-aliasing needed (audio-rate harmonics aren't in scope).
// Output is in [-1, 1]. SampleHold draws a new random value each full cycle.
public sealed class Lfo
{
    private readonly int _sampleRate;
    private float _dt;
    private float _holdValue;
    private float _phase;
    private FastRng _rng;

    public Lfo(int sampleRate, int seed = 0)
    {
        _sampleRate = sampleRate;
        _rng = new FastRng(unchecked((uint)seed));
        _holdValue = _rng.NextFloat11();
        Frequency = 1f;
    }

    public LfoShape Shape { get; set; } = LfoShape.Sine;

    public float Frequency
    {
        get => _dt * _sampleRate;
        set => _dt = value / _sampleRate;
    }

    public void Reset()
    {
        _phase = 0;
        _holdValue = _rng.NextFloat11();
    }

    public float Process()
    {
        var v = Shape switch
        {
            LfoShape.Sine => MathF.Sin(_phase * MathF.Tau),
            LfoShape.Triangle => _phase < 0.5f ? -1f + 4f * _phase : 3f - 4f * _phase,
            LfoShape.Saw => 2f * _phase - 1f,
            LfoShape.Square => _phase < 0.5f ? 1f : -1f,
            LfoShape.SampleHold => _holdValue,
            _ => 0
        };

        _phase += _dt;
        if (_phase >= 1f)
        {
            _phase -= 1f;
            if (Shape == LfoShape.SampleHold)
            {
                _holdValue = _rng.NextFloat11();
            }
        }

        return v;
    }
}
