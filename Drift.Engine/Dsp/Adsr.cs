namespace Drift.Engine.Dsp;

// Exponential analog-style ADSR. Each stage approaches its target with a one-pole filter
// whose time constant is set so the stage reaches ~99% of the target in `time` seconds.
public sealed class Adsr
{
    public enum Stage
    {
        Idle,
        Attack,
        Decay,
        Sustain,
        Release
    }

    // Tiny overshoot on attack so we cross 1.0 cleanly.
    private const float AttackOvershoot = 0.0009f;
    private const float IdleEpsilon = 0.0001f;

    private readonly int _sampleRate;
    private float _coeff;
    private float _target;

    public Adsr(int sampleRate)
    {
        _sampleRate = sampleRate;
    }

    public float Attack { get; set; } = 0.005f;
    public float Decay { get; set; } = 0.20f;
    public float Sustain { get; set; } = 0.70f;
    public float Release { get; set; } = 0.30f;

    public Stage CurrentStage { get; private set; } = Stage.Idle;
    public bool IsActive => CurrentStage != Stage.Idle;
    public float Value { get; private set; }

    public void NoteOn()
    {
        CurrentStage = Stage.Attack;
        _target = 1f + AttackOvershoot;
        _coeff = ComputeCoeff(Attack);
    }

    public void NoteOff()
    {
        if (CurrentStage == Stage.Idle)
        {
            return;
        }

        CurrentStage = Stage.Release;
        _target = 0f;
        _coeff = ComputeCoeff(Release);
    }

    public void Reset()
    {
        CurrentStage = Stage.Idle;
        Value = 0;
    }

    public float Process()
    {
        switch (CurrentStage)
        {
            case Stage.Idle:
                return 0;

            case Stage.Attack:
                Value += (_target - Value) * _coeff;
                if (Value >= 1f)
                {
                    Value = 1f;
                    CurrentStage = Stage.Decay;
                    _target = Sustain;
                    _coeff = ComputeCoeff(Decay);
                }

                break;

            case Stage.Decay:
                Value += (_target - Value) * _coeff;
                if (MathF.Abs(Value - Sustain) < IdleEpsilon)
                {
                    Value = Sustain;
                    CurrentStage = Stage.Sustain;
                }

                break;

            case Stage.Sustain:
                // Live edits to Sustain take effect immediately.
                Value = Sustain;
                break;

            case Stage.Release:
                Value += (_target - Value) * _coeff;
                if (Value < IdleEpsilon)
                {
                    Value = 0;
                    CurrentStage = Stage.Idle;
                }

                break;
        }

        return Value;
    }

    // 1 - exp(-2.2 / (t*sr)) gives ~90% of target in `t` seconds; the small overshoot
    // on attack and the IdleEpsilon on release tighten the perceived timing.
    private float ComputeCoeff(float timeSec)
    {
        if (timeSec <= 0.0001f)
        {
            return 1f;
        }

        return 1f - MathF.Exp(-2.2f / (timeSec * _sampleRate));
    }
}
