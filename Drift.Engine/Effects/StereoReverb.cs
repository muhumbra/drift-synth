namespace Drift.Engine.Effects;

// Freeverb-style Schroeder reverb: 8 parallel damped combs feed into 4 series allpasses,
// duplicated per channel with the right side offset by ~0.5 ms for stereo width.
public sealed class StereoReverb
{
    private readonly Reverb _l, _r;

    public StereoReverb(int sampleRate, float roomSize = 0.85f, float damping = 0.4f, int stereoOffsetSamples = 23)
    {
        _l = new Reverb(sampleRate, roomSize, damping, 0);
        _r = new Reverb(sampleRate, roomSize, damping, stereoOffsetSamples);
    }

    public void Set(float roomSize, float damping)
    {
        _l.Set(roomSize, damping);
        _r.Set(roomSize, damping);
    }

    public (float L, float R) Process(float input)
    {
        return (_l.Process(input), _r.Process(input));
    }
}

internal sealed class Reverb
{
    private static readonly int[] CombSamples44k = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617];
    private static readonly int[] AllpassSamples44k = [556, 441, 341, 225];
    private readonly Allpass[] _allpasses;

    private readonly Comb[] _combs;

    public Reverb(int sampleRate, float roomSize, float damping, int stereoOffsetSamples)
    {
        var scale = sampleRate / 44100f;
        _combs = new Comb[CombSamples44k.Length];
        for (var i = 0; i < _combs.Length; i++)
        {
            _combs[i] = new Comb((int)(CombSamples44k[i] * scale) + stereoOffsetSamples, roomSize, damping);
        }

        _allpasses = new Allpass[AllpassSamples44k.Length];
        for (var i = 0; i < _allpasses.Length; i++)
        {
            _allpasses[i] = new Allpass((int)(AllpassSamples44k[i] * scale) + stereoOffsetSamples, 0.5f);
        }
    }

    public void Set(float roomSize, float damping)
    {
        foreach (var c in _combs)
        {
            c.Set(roomSize, damping);
        }
    }

    public float Process(float input)
    {
        float sum = 0;
        foreach (var c in _combs)
        {
            sum += c.Process(input);
        }

        sum *= 1f / _combs.Length;
        foreach (var a in _allpasses)
        {
            sum = a.Process(sum);
        }

        return sum;
    }

    private sealed class Comb
    {
        private readonly float[] _buffer;
        private float _damping;
        private float _feedback;
        private float _filterStore;
        private int _idx;

        public Comb(int delay, float feedback, float damping)
        {
            _buffer = new float[Math.Max(1, delay)];
            _feedback = feedback;
            _damping = damping;
        }

        public void Set(float feedback, float damping)
        {
            _feedback = feedback;
            _damping = damping;
        }

        public float Process(float input)
        {
            var output = _buffer[_idx];
            _filterStore = output * (1f - _damping) + _filterStore * _damping;
            _buffer[_idx] = input + _filterStore * _feedback;
            _idx = (_idx + 1) % _buffer.Length;
            return output;
        }
    }

    private sealed class Allpass
    {
        private readonly float[] _buffer;
        private readonly float _feedback;
        private int _idx;

        public Allpass(int delay, float feedback)
        {
            _buffer = new float[Math.Max(1, delay)];
            _feedback = feedback;
        }

        public float Process(float input)
        {
            var bufout = _buffer[_idx];
            var output = bufout - input;
            _buffer[_idx] = input + bufout * _feedback;
            _idx = (_idx + 1) % _buffer.Length;
            return output;
        }
    }
}
