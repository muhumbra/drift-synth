namespace Drift.Engine.Effects;

// Stereo ping-pong delay. Mono input arrives on the L tap; the L tap's filtered
// echo feeds the R tap; the R tap's filtered echo feeds back into L. The result
// is a back-and-forth ping-pong with feedback that decays through a one-pole
// lowpass (the Tone control: 0 = dark / dub, 1 = bright / hi-fi).
public sealed class Delay
{
    private readonly float[] _bufL, _bufR;
    private readonly int _sampleRate;
    private float _feedback = 0.4f;
    private int _idxL, _idxR;
    private float _lpStateL, _lpStateR;

    private float _timeMs = 350;
    private float _tone = 0.5f;

    public Delay(int sampleRate)
    {
        _sampleRate = sampleRate;
        var max = sampleRate * 2; // 2 second max
        _bufL = new float[max];
        _bufR = new float[max];
    }

    public void Set(float timeMs, float feedback, float tone)
    {
        _timeMs = timeMs;
        _feedback = feedback;
        _tone = tone;
    }

    public void Reset()
    {
        Array.Clear(_bufL, 0, _bufL.Length);
        Array.Clear(_bufR, 0, _bufR.Length);
        _lpStateL = 0;
        _lpStateR = 0;
    }

    public (float L, float R) Process(float input)
    {
        var delaySamples = Math.Clamp((int)(_timeMs * 0.001f * _sampleRate), 1, _bufL.Length - 1);
        var rL = _idxL - delaySamples;
        if (rL < 0)
        {
            rL += _bufL.Length;
        }

        var rR = _idxR - delaySamples;
        if (rR < 0)
        {
            rR += _bufR.Length;
        }

        var outL = _bufL[rL];
        var outR = _bufR[rR];

        // Tone: cutoff coefficient 0.05..1.0 -- close to 1 = bright, near 0 = dark.
        var cutCoeff = _tone * _tone * 0.95f + 0.05f;
        _lpStateL = outL * cutCoeff + _lpStateL * (1f - cutCoeff);
        _lpStateR = outR * cutCoeff + _lpStateR * (1f - cutCoeff);

        var fb = _feedback;
        // Ping-pong: input only enters the L tap; R is a pure echo of L.
        _bufL[_idxL] = input + _lpStateR * fb;
        _bufR[_idxR] = _lpStateL * fb;

        _idxL++;
        if (_idxL >= _bufL.Length)
        {
            _idxL = 0;
        }

        _idxR++;
        if (_idxR >= _bufR.Length)
        {
            _idxR = 0;
        }

        return (outL, outR);
    }
}
