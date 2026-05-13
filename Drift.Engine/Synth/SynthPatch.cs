using Drift.Engine.Dsp;
using Drift.Engine.Sequencer;

namespace Drift.Engine.Synth;

public enum LfoTarget
{
    Off,
    Pitch,
    Cutoff,
    Amp
}

public sealed class OscParams : Observable
{
    private float _fineCents;
    private float _level = 0.7f;
    private float _octave;
    private float _semitone;
    private Waveform _wave = Waveform.Saw;

    public Waveform Wave
    {
        get => _wave;
        set => Set(ref _wave, value);
    }

    public float Octave
    {
        get => _octave;
        set => Set(ref _octave, MathF.Round(Math.Clamp(value, -3f, 3f)));
    }

    public float Semitone
    {
        get => _semitone;
        set => Set(ref _semitone, MathF.Round(Math.Clamp(value, -12f, 12f)));
    }

    public float FineCents
    {
        get => _fineCents;
        set => Set(ref _fineCents, Math.Clamp(value, -100f, 100f));
    }

    public float Level
    {
        get => _level;
        set => Set(ref _level, Math.Clamp(value, 0f, 1f));
    }

    public float FrequencyMultiplier()
    {
        var semis = _octave * 12f + _semitone + _fineCents / 100f;
        return MathF.Pow(2f, semis / 12f);
    }

    public void CopyFrom(OscParams s)
    {
        Wave = s.Wave;
        Octave = s.Octave;
        Semitone = s.Semitone;
        FineCents = s.FineCents;
        Level = s.Level;
    }
}

public sealed class EnvelopeParams : Observable
{
    private float _attack = 0.005f;
    private float _decay = 0.2f;
    private float _release = 0.3f;
    private float _sustain = 0.7f;

    public float Attack
    {
        get => _attack;
        set => Set(ref _attack, Math.Clamp(value, 0.001f, 10f));
    }

    public float Decay
    {
        get => _decay;
        set => Set(ref _decay, Math.Clamp(value, 0.001f, 10f));
    }

    public float Sustain
    {
        get => _sustain;
        set => Set(ref _sustain, Math.Clamp(value, 0f, 1f));
    }

    public float Release
    {
        get => _release;
        set => Set(ref _release, Math.Clamp(value, 0.001f, 10f));
    }

    public void CopyFrom(EnvelopeParams s)
    {
        Attack = s.Attack;
        Decay = s.Decay;
        Sustain = s.Sustain;
        Release = s.Release;
    }
}

public sealed class FilterParams : Observable
{
    private float _cutoff = 4000;
    private float _envAmount = 0.7f;
    private float _keyTrack = 0.4f;
    private float _resonance = 0.4f;

    public float Cutoff
    {
        get => _cutoff;
        set => Set(ref _cutoff, Math.Clamp(value, 20f, 18000f));
    }

    public float Resonance
    {
        get => _resonance;
        set => Set(ref _resonance, Math.Clamp(value, 0f, 1f));
    }

    public float EnvAmount
    {
        get => _envAmount;
        set => Set(ref _envAmount, Math.Clamp(value, -1f, 1f));
    }

    public float KeyTrack
    {
        get => _keyTrack;
        set => Set(ref _keyTrack, Math.Clamp(value, 0f, 1f));
    }

    public void CopyFrom(FilterParams s)
    {
        Cutoff = s.Cutoff;
        Resonance = s.Resonance;
        EnvAmount = s.EnvAmount;
        KeyTrack = s.KeyTrack;
    }
}

public sealed class LfoParams : Observable
{
    private float _amount;
    private float _rate = 4f;
    private LfoShape _shape = LfoShape.Sine;
    private LfoTarget _target = LfoTarget.Pitch;

    public LfoShape Shape
    {
        get => _shape;
        set => Set(ref _shape, value);
    }

    public float Rate
    {
        get => _rate;
        set => Set(ref _rate, Math.Clamp(value, 0.01f, 30f));
    }

    public float Amount
    {
        get => _amount;
        set => Set(ref _amount, Math.Clamp(value, 0f, 1f));
    }

    public LfoTarget Target
    {
        get => _target;
        set => Set(ref _target, value);
    }

    public void CopyFrom(LfoParams s)
    {
        Shape = s.Shape;
        Rate = s.Rate;
        Amount = s.Amount;
        Target = s.Target;
    }
}

public sealed class MixerParams : Observable
{
    private float _noiseLevel;
    private float _subLevel;

    public float SubLevel
    {
        get => _subLevel;
        set => Set(ref _subLevel, Math.Clamp(value, 0f, 1f));
    }

    public float NoiseLevel
    {
        get => _noiseLevel;
        set => Set(ref _noiseLevel, Math.Clamp(value, 0f, 1f));
    }

    public void CopyFrom(MixerParams s)
    {
        SubLevel = s.SubLevel;
        NoiseLevel = s.NoiseLevel;
    }
}

public sealed class MasterParams : Observable
{
    private float _reverbDamp = 0.4f;
    private float _reverbMix = 0.2f;
    private float _reverbSize = 0.85f;
    private float _volume = 0.5f;

    public float Volume
    {
        get => _volume;
        set => Set(ref _volume, Math.Clamp(value, 0f, 1f));
    }

    public float ReverbMix
    {
        get => _reverbMix;
        set => Set(ref _reverbMix, Math.Clamp(value, 0f, 1f));
    }

    public float ReverbSize
    {
        get => _reverbSize;
        set => Set(ref _reverbSize, Math.Clamp(value, 0f, 1f));
    }

    public float ReverbDamp
    {
        get => _reverbDamp;
        set => Set(ref _reverbDamp, Math.Clamp(value, 0f, 1f));
    }

    public void CopyFrom(MasterParams s)
    {
        Volume = s.Volume;
        ReverbMix = s.ReverbMix;
        ReverbSize = s.ReverbSize;
        ReverbDamp = s.ReverbDamp;
    }
}

// Glide + mono mode. When Mono is true, only voice 0 plays and a note stack tracks
// held keys so releasing a key falls back to the previously-held one. Glide is the
// time (seconds) the pitch takes to slide one octave; 0 = instant.
public sealed class VoiceParams : Observable
{
    private float _glide;
    private bool _mono;
    private bool _monoLegato = true;

    public float Glide
    {
        get => _glide;
        set => Set(ref _glide, Math.Clamp(value, 0f, 2f));
    }

    public bool Mono
    {
        get => _mono;
        set => Set(ref _mono, value);
    }

    public bool MonoLegato
    {
        get => _monoLegato;
        set => Set(ref _monoLegato, value);
    }

    public void CopyFrom(VoiceParams s)
    {
        Glide = s.Glide;
        Mono = s.Mono;
        MonoLegato = s.MonoLegato;
    }
}

public sealed class DelayParams : Observable
{
    private float _feedback = 0.4f;
    private float _mix;
    private float _timeMs = 350;
    private float _tone = 0.5f;

    public float TimeMs
    {
        get => _timeMs;
        set => Set(ref _timeMs, Math.Clamp(value, 5f, 1500f));
    }

    public float Feedback
    {
        get => _feedback;
        set => Set(ref _feedback, Math.Clamp(value, 0f, 0.92f));
    }

    public float Mix
    {
        get => _mix;
        set => Set(ref _mix, Math.Clamp(value, 0f, 1f));
    }

    public float Tone
    {
        get => _tone;
        set => Set(ref _tone, Math.Clamp(value, 0f, 1f));
    }

    public void CopyFrom(DelayParams s)
    {
        TimeMs = s.TimeMs;
        Feedback = s.Feedback;
        Mix = s.Mix;
        Tone = s.Tone;
    }
}

public sealed class SynthPatch : Observable
{
    private string _name = "Init";
    private int _pitchBendRangeSemitones = 2;
    private int _polyphony = 16;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public int Polyphony
    {
        get => _polyphony;
        set => Set(ref _polyphony, Math.Clamp(value, 1, 32));
    }

    public int PitchBendRangeSemitones
    {
        get => _pitchBendRangeSemitones;
        set => Set(ref _pitchBendRangeSemitones, Math.Clamp(value, 1, 24));
    }

    public OscParams Osc1 { get; set; } = new();
    public OscParams Osc2 { get; set; } = new();
    public MixerParams Mixer { get; set; } = new();
    public FilterParams Filter { get; set; } = new();
    public EnvelopeParams AmpEnv { get; set; } = new();
    public EnvelopeParams FilterEnv { get; set; } = new();
    public LfoParams Lfo { get; set; } = new();
    public VoiceParams Voice { get; set; } = new();
    public DelayParams Delay { get; set; } = new();
    public MasterParams Master { get; set; } = new();
    public ArpParams Arp { get; set; } = new();

    // Copy all values from another patch into this instance, preserving sub-object
    // identity so UI bindings keep firing PropertyChanged events.
    public void CopyFrom(SynthPatch s)
    {
        Name = s.Name;
        Polyphony = s.Polyphony;
        PitchBendRangeSemitones = s.PitchBendRangeSemitones;
        Osc1.CopyFrom(s.Osc1);
        Osc2.CopyFrom(s.Osc2);
        Mixer.CopyFrom(s.Mixer);
        Filter.CopyFrom(s.Filter);
        AmpEnv.CopyFrom(s.AmpEnv);
        FilterEnv.CopyFrom(s.FilterEnv);
        Lfo.CopyFrom(s.Lfo);
        Voice.CopyFrom(s.Voice);
        Delay.CopyFrom(s.Delay);
        Master.CopyFrom(s.Master);
        Arp.CopyFrom(s.Arp);
    }
}
