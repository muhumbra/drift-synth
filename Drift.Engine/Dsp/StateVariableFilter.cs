using Drift.Engine.Dsp.Lut;

namespace Drift.Engine.Dsp;

// Cytomic / Andy Simper "Linear Trapezoidal SVF" -- a TPT state-variable filter that
// stays stable from 20 Hz right up against Nyquist. Cheap enough to retune every sample,
// which is exactly what a modulated cutoff needs.
//
// Resonance is exposed in [0..1]. Internally that maps to k in [2 .. 0.02], which is
// 1/Q. So 0 = lush and damped, 1 = whistling on the edge of self-oscillation.
public sealed class StateVariableFilter
{
    private readonly int _sampleRate;

    private float _cutoff = 1000;
    private float _g, _k, _a1, _a2, _a3;
    private float _ic1eq, _ic2eq;
    private float _resonance = 0.5f;

    public StateVariableFilter(int sampleRate)
    {
        _sampleRate = sampleRate;
        UpdateCoeffs();
    }

    public void Reset()
    {
        _ic1eq = 0;
        _ic2eq = 0;
    }

    public void Set(float cutoffHz, float resonance)
    {
        _cutoff = cutoffHz;
        _resonance = resonance;
        UpdateCoeffs();
    }

    private void UpdateCoeffs()
    {
        var cutoff = Math.Clamp(_cutoff, 20f, _sampleRate * 0.49f);
        var res = Math.Clamp(_resonance, 0f, 1f);
        _g = FastTanU.TanPiFcOverFs(cutoff, _sampleRate);
        _k = 2f - 1.99f * res;
        _a1 = 1f / (1f + _g * (_g + _k));
        _a2 = _g * _a1;
        _a3 = _g * _a2;
    }

    public float ProcessLp(float input)
    {
        var v3 = input - _ic2eq;
        var v1 = _a1 * _ic1eq + _a2 * v3;
        var v2 = _ic2eq + _a2 * _ic1eq + _a3 * v3;
        _ic1eq = 2f * v1 - _ic1eq;
        _ic2eq = 2f * v2 - _ic2eq;
        return v2;
    }

    public float ProcessHp(float input)
    {
        var v3 = input - _ic2eq;
        var v1 = _a1 * _ic1eq + _a2 * v3;
        var v2 = _ic2eq + _a2 * _ic1eq + _a3 * v3;
        _ic1eq = 2f * v1 - _ic1eq;
        _ic2eq = 2f * v2 - _ic2eq;
        return input - _k * v1 - v2;
    }
}
