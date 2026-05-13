namespace Drift.Ui.Controls;

// Stable, dotted string ids for every learnable knob in the UI. Used both as
// XAML attribute values on <c:Knob ParamId="..."> and as the keys persisted to
// Settings/midimap.json. Keep in sync with KnobParamSetters.Build.
public static class ParamIds
{
    // OSC 1
    public const string Osc1Octave = "patch.osc1.octave";
    public const string Osc1Semi = "patch.osc1.semi";
    public const string Osc1Fine = "patch.osc1.fine";
    public const string Osc1Level = "patch.osc1.level";

    // OSC 2
    public const string Osc2Octave = "patch.osc2.octave";
    public const string Osc2Semi = "patch.osc2.semi";
    public const string Osc2Fine = "patch.osc2.fine";
    public const string Osc2Level = "patch.osc2.level";

    // MIXER
    public const string MixerSub = "patch.mixer.sub";
    public const string MixerNoise = "patch.mixer.noise";

    // FILTER
    public const string FilterCutoff = "patch.filter.cutoff";
    public const string FilterResonance = "patch.filter.reso";
    public const string FilterEnvAmount = "patch.filter.env";
    public const string FilterKeyTrack = "patch.filter.track";

    // VOICE
    public const string VoiceGlide = "patch.voice.glide";

    // AMP ENV
    public const string AmpEnvAttack = "patch.ampenv.a";
    public const string AmpEnvDecay = "patch.ampenv.d";
    public const string AmpEnvSustain = "patch.ampenv.s";
    public const string AmpEnvRelease = "patch.ampenv.r";

    // FILTER ENV
    public const string FilterEnvAttack = "patch.filterenv.a";
    public const string FilterEnvDecay = "patch.filterenv.d";
    public const string FilterEnvSustain = "patch.filterenv.s";
    public const string FilterEnvRelease = "patch.filterenv.r";

    // LFO
    public const string LfoRate = "patch.lfo.rate";
    public const string LfoAmount = "patch.lfo.amount";

    // DELAY
    public const string DelayTime = "patch.delay.time";
    public const string DelayFeedback = "patch.delay.feedback";
    public const string DelayTone = "patch.delay.tone";
    public const string DelayMix = "patch.delay.mix";

    // MASTER
    public const string MasterVolume = "patch.master.volume";
    public const string MasterReverbMix = "patch.master.reverb.mix";
    public const string MasterReverbSize = "patch.master.reverb.size";
    public const string MasterReverbDamp = "patch.master.reverb.damp";

    // ARP
    public const string ArpBpm = "patch.arp.bpm";
    public const string ArpOctaves = "patch.arp.octaves";
    public const string ArpGate = "patch.arp.gate";
    public const string ArpSwing = "patch.arp.swing";
}
