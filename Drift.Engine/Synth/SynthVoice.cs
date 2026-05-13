using Drift.Engine.Dsp;

namespace Drift.Engine.Synth;

// One polyphonic voice. Signal flow:
//
//   OSC1 \
//   OSC2  >-- Mixer --> SVF Lowpass (cutoff = base + env*depth + lfo*depth + keytrack)
//   SUB  /                       \
//   NOISE                         +-- Amp (velocity * ampEnv * lfo*depth?) --> mono buffer
//
// Pitch is tracked in MIDI-note (semitone) space and smoothed exponentially so
// glide / portamento is musical (constant semitones-per-second per glide-time).
// Mod wheel value (0..1) is added to the patch's LFO amount before being used
// for any modulation target, giving the player on-the-fly vibrato.
public sealed class SynthVoice : ISynthVoice
{
    private static long _counter;
    private readonly Adsr _ampEnv, _filterEnv;
    private readonly StateVariableFilter _filter;
    private readonly Lfo _lfo;

    private readonly PolyBlepOscillator _osc1, _osc2;
    private readonly SynthPatch _patch;
    private readonly int _sampleRate;
    private readonly PolyBlepOscillator _sub, _noise;
    private readonly VoiceState _voiceState;

    private float _currentMidi; // smoothed pitch in MIDI semitones
    private float _targetMidi; // glide target
    private float _velocity;

    public SynthVoice(int sampleRate, SynthPatch patch, VoiceState voiceState, int seed)
    {
        _sampleRate = sampleRate;
        _patch = patch;
        _voiceState = voiceState;

        _osc1 = new PolyBlepOscillator(sampleRate, seed) { Wave = Waveform.Saw };
        _osc2 = new PolyBlepOscillator(sampleRate, seed * 7919 + 1) { Wave = Waveform.Saw };
        _sub = new PolyBlepOscillator(sampleRate, seed * 13 + 3) { Wave = Waveform.Sine };
        _noise = new PolyBlepOscillator(sampleRate, seed * 31 + 5) { Wave = Waveform.Noise };

        _filter = new StateVariableFilter(sampleRate);
        _ampEnv = new Adsr(sampleRate);
        _filterEnv = new Adsr(sampleRate);
        _lfo = new Lfo(sampleRate, seed * 23 + 7);
    }

    public int Note { get; private set; } = -1;
    public bool IsActive => _ampEnv.IsActive;
    public bool IsReleasing => _ampEnv.CurrentStage == Adsr.Stage.Release;
    public long StartTimestamp { get; private set; }

    public void NoteOn(int note, float velocity)
    {
        Note = note;
        _velocity = Math.Clamp(velocity, 0f, 1f);
        _targetMidi = note;
        _currentMidi = note;
        StartTimestamp = Stopwatch();

        _osc1.ResetPhase();
        _osc2.ResetPhase(0.13f);
        _sub.ResetPhase(0.27f);
        _filter.Reset();
        _lfo.Reset();

        CopyEnvelopes();
        _ampEnv.NoteOn();
        _filterEnv.NoteOn();
    }

    public void NoteOff()
    {
        _ampEnv.NoteOff();
        _filterEnv.NoteOff();
    }

    public void HardReset()
    {
        Note = -1;
        _ampEnv.Reset();
        _filterEnv.Reset();
        _filter.Reset();
    }

    // Change pitch target without retriggering envelopes (legato glide).
    public void GlideTo(int note)
    {
        Note = note;
        _targetMidi = note;
    }

    // Force the current smoothed-pitch state to a specific note. Use this AFTER
    // NoteOn() in mono retrigger mode, so envelopes restart but pitch glides
    // from the previously-held note.
    public void GlideFrom(int note)
    {
        _currentMidi = note;
    }

    public void RenderBlock(float[] buffer, int offset, int count)
    {
        var osc1P = _patch.Osc1;
        var osc2P = _patch.Osc2;
        var mix = _patch.Mixer;
        var filt = _patch.Filter;
        var lfoP = _patch.Lfo;
        var voiceP = _patch.Voice;

        CopyEnvelopes();

        _osc1.Wave = osc1P.Wave;
        _osc2.Wave = osc2P.Wave;
        _lfo.Shape = lfoP.Shape;
        _lfo.Frequency = lfoP.Rate;

        var pitchBendMul = _voiceState.PitchBendMultiplier;

        // Glide coefficient: ~99% of target reached in `glideTime` seconds.
        var glideTime = voiceP.Glide;
        var glideCoeff = glideTime > 0.0001f
            ? 1f - MathF.Exp(-5f / (glideTime * _sampleRate))
            : 1f;

        // Mod wheel adds to the LFO amount, capped at 1.
        var effectiveLfoAmt = MathF.Min(1f, lfoP.Amount + _voiceState.ModWheel);

        // Key tracking ratio relative to middle C.
        var keyTrackHz = Music.MidiToHz(Note) / Music.MidiToHz(60) - 1f;
        var keyTrackMul = 1f + keyTrackHz * filt.KeyTrack;

        var level1 = osc1P.Level;
        var level2 = osc2P.Level;
        var levelSub = mix.SubLevel;
        var levelNz = mix.NoiseLevel;

        var lfoToPitch = lfoP.Target == LfoTarget.Pitch && effectiveLfoAmt > 0;
        var lfoToCutoff = lfoP.Target == LfoTarget.Cutoff && effectiveLfoAmt > 0;
        var lfoToAmp = lfoP.Target == LfoTarget.Amp && effectiveLfoAmt > 0;

        var cutoff = filt.Cutoff;
        var res = filt.Resonance;
        var envAmt = filt.EnvAmount;

        for (var i = 0; i < count; i++)
        {
            var lfoV = _lfo.Process();
            var fEnv = _filterEnv.Process();
            var aEnv = _ampEnv.Process();

            _currentMidi += (_targetMidi - _currentMidi) * glideCoeff;
            var freq = Music.MidiToHz(_currentMidi) * pitchBendMul;

            var pitchMod = 1f;
            if (lfoToPitch)
            {
                pitchMod = MathF.Pow(2f, lfoV * effectiveLfoAmt / 12f);
            }

            var f1 = freq * osc1P.FrequencyMultiplier() * pitchMod;
            var f2 = freq * osc2P.FrequencyMultiplier() * pitchMod;
            var fSub = freq * 0.5f * pitchMod;

            _osc1.Frequency = f1;
            _osc2.Frequency = f2;
            _sub.Frequency = fSub;

            var dry = _osc1.Render() * level1
                      + _osc2.Render() * level2
                      + _sub.Render() * levelSub
                      + _noise.Render() * levelNz;

            var cMod = cutoff * keyTrackMul * MathF.Pow(2f, envAmt * fEnv * 5f);
            if (lfoToCutoff)
            {
                cMod *= MathF.Pow(2f, lfoV * effectiveLfoAmt * 4f);
            }

            _filter.Set(cMod, res);
            var filtered = _filter.ProcessLp(dry);

            var amp = aEnv * _velocity;
            if (lfoToAmp)
            {
                amp *= 1f - effectiveLfoAmt * 0.5f * (1f - lfoV * 0.5f - 0.5f);
            }

            buffer[offset + i] += filtered * amp;
        }
    }

    private void CopyEnvelopes()
    {
        var a = _patch.AmpEnv;
        _ampEnv.Attack = a.Attack;
        _ampEnv.Decay = a.Decay;
        _ampEnv.Sustain = a.Sustain;
        _ampEnv.Release = a.Release;

        var f = _patch.FilterEnv;
        _filterEnv.Attack = f.Attack;
        _filterEnv.Decay = f.Decay;
        _filterEnv.Sustain = f.Sustain;
        _filterEnv.Release = f.Release;
    }

    private static long Stopwatch()
    {
        return Interlocked.Increment(ref _counter);
    }
}
