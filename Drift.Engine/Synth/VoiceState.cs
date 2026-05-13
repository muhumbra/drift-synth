namespace Drift.Engine.Synth;

// Per-channel state shared across all voices in a pool. Pitch bend, mod wheel and
// sustain pedal are global to a MIDI channel (not per-note), so they live here
// rather than in each voice.
public sealed class VoiceState
{
    private int _bendRangeSemitones = 2;
    private float _pitchBend;

    // 0..1 from CC 1. Voices add this to the LFO amount so the player can dial
    // in vibrato (or whatever the LFO target is) on the fly.
    public float ModWheel;

    public bool SustainPedal;

    public int BendRangeSemitones
    {
        get => _bendRangeSemitones;
        set
        {
            _bendRangeSemitones = value;
            UpdateMul();
        }
    }

    public float PitchBend
    {
        get => _pitchBend;
        set
        {
            _pitchBend = Math.Clamp(value, -1f, 1f);
            UpdateMul();
        }
    }

    public float PitchBendMultiplier { get; private set; } = 1f;

    private void UpdateMul()
    {
        PitchBendMultiplier = MathF.Pow(2f, _pitchBend * _bendRangeSemitones / 12f);
    }
}
