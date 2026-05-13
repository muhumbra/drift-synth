using Drift.Engine.Synth;

namespace Drift.Engine.Sequencer;

public sealed class ArpParams : Observable
{
    private float _bpm = 120;
    private float _gate = 0.5f;
    private bool _latch;
    private ArpMode _mode = ArpMode.Up;
    private float _octaves = 1;
    private bool _on;
    private ArpRate _rate = ArpRate.Sixteenth;
    private float _swing;

    public bool On
    {
        get => _on;
        set => Set(ref _on, value);
    }

    public float Bpm
    {
        get => _bpm;
        set => Set(ref _bpm, MathF.Round(Math.Clamp(value, 40f, 240f)));
    }

    public ArpRate Rate
    {
        get => _rate;
        set => Set(ref _rate, value ?? ArpRate.Sixteenth);
    }

    public ArpMode Mode
    {
        get => _mode;
        set => Set(ref _mode, value);
    }

    public float Octaves
    {
        get => _octaves;
        set => Set(ref _octaves, MathF.Round(Math.Clamp(value, 1f, 4f)));
    }

    public float Gate
    {
        get => _gate;
        set => Set(ref _gate, Math.Clamp(value, 0.05f, 1f));
    }

    public float Swing
    {
        get => _swing;
        set => Set(ref _swing, Math.Clamp(value, 0f, 0.75f));
    }

    public bool Latch
    {
        get => _latch;
        set => Set(ref _latch, value);
    }

    public void CopyFrom(ArpParams s)
    {
        On = s.On;
        Bpm = s.Bpm;
        Rate = s.Rate;
        Mode = s.Mode;
        Octaves = s.Octaves;
        Gate = s.Gate;
        Swing = s.Swing;
        Latch = s.Latch;
    }
}
