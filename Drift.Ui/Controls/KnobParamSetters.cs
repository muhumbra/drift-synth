using Avalonia.Threading;
using Drift.Engine.Synth;

namespace Drift.Ui.Controls;

// Builds a strongly-typed setter for a paramId so the engine's MidiCcMap can
// drop CC values directly onto patch sub-objects. The setter marshals to the
// UI thread because patch property changes flow through PropertyChanged
// subscribers (Avalonia bindings, MainViewModel.OnMasterPropertyChanged) that
// expect to run there.
//
// Keep cases in sync with Drift.Ui.Controls.ParamIds.
public static class KnobParamSetters
{
    public static Action<float>? Build(string paramId, SynthPatch patch)
    {
        switch (paramId)
        {
            case ParamIds.Osc1Octave: return v => Apply(() => patch.Osc1.Octave = v);
            case ParamIds.Osc1Semi: return v => Apply(() => patch.Osc1.Semitone = v);
            case ParamIds.Osc1Fine: return v => Apply(() => patch.Osc1.FineCents = v);
            case ParamIds.Osc1Level: return v => Apply(() => patch.Osc1.Level = v);

            case ParamIds.Osc2Octave: return v => Apply(() => patch.Osc2.Octave = v);
            case ParamIds.Osc2Semi: return v => Apply(() => patch.Osc2.Semitone = v);
            case ParamIds.Osc2Fine: return v => Apply(() => patch.Osc2.FineCents = v);
            case ParamIds.Osc2Level: return v => Apply(() => patch.Osc2.Level = v);

            case ParamIds.MixerSub: return v => Apply(() => patch.Mixer.SubLevel = v);
            case ParamIds.MixerNoise: return v => Apply(() => patch.Mixer.NoiseLevel = v);

            case ParamIds.FilterCutoff: return v => Apply(() => patch.Filter.Cutoff = v);
            case ParamIds.FilterResonance: return v => Apply(() => patch.Filter.Resonance = v);
            case ParamIds.FilterEnvAmount: return v => Apply(() => patch.Filter.EnvAmount = v);
            case ParamIds.FilterKeyTrack: return v => Apply(() => patch.Filter.KeyTrack = v);

            case ParamIds.VoiceGlide: return v => Apply(() => patch.Voice.Glide = v);

            case ParamIds.AmpEnvAttack: return v => Apply(() => patch.AmpEnv.Attack = v);
            case ParamIds.AmpEnvDecay: return v => Apply(() => patch.AmpEnv.Decay = v);
            case ParamIds.AmpEnvSustain: return v => Apply(() => patch.AmpEnv.Sustain = v);
            case ParamIds.AmpEnvRelease: return v => Apply(() => patch.AmpEnv.Release = v);

            case ParamIds.FilterEnvAttack: return v => Apply(() => patch.FilterEnv.Attack = v);
            case ParamIds.FilterEnvDecay: return v => Apply(() => patch.FilterEnv.Decay = v);
            case ParamIds.FilterEnvSustain: return v => Apply(() => patch.FilterEnv.Sustain = v);
            case ParamIds.FilterEnvRelease: return v => Apply(() => patch.FilterEnv.Release = v);

            case ParamIds.LfoRate: return v => Apply(() => patch.Lfo.Rate = v);
            case ParamIds.LfoAmount: return v => Apply(() => patch.Lfo.Amount = v);

            case ParamIds.DelayTime: return v => Apply(() => patch.Delay.TimeMs = v);
            case ParamIds.DelayFeedback: return v => Apply(() => patch.Delay.Feedback = v);
            case ParamIds.DelayTone: return v => Apply(() => patch.Delay.Tone = v);
            case ParamIds.DelayMix: return v => Apply(() => patch.Delay.Mix = v);

            case ParamIds.MasterVolume: return v => Apply(() => patch.Master.Volume = v);
            case ParamIds.MasterReverbMix: return v => Apply(() => patch.Master.ReverbMix = v);
            case ParamIds.MasterReverbSize: return v => Apply(() => patch.Master.ReverbSize = v);
            case ParamIds.MasterReverbDamp: return v => Apply(() => patch.Master.ReverbDamp = v);

            case ParamIds.ArpBpm: return v => Apply(() => patch.Arp.Bpm = v);
            case ParamIds.ArpOctaves: return v => Apply(() => patch.Arp.Octaves = v);
            case ParamIds.ArpGate: return v => Apply(() => patch.Arp.Gate = v);
            case ParamIds.ArpSwing: return v => Apply(() => patch.Arp.Swing = v);

            default: return null;
        }
    }

    private static void Apply(Action assign)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            assign();
        }
        else
        {
            Dispatcher.UIThread.Post(assign);
        }
    }
}
