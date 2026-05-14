using Drift.Engine.Dsp.Lut;
using Drift.Engine.Effects;
using Drift.Engine.Midi;
using Drift.Engine.Sequencer;
using Drift.Engine.Synth;
using NAudio.Wave;

namespace Drift.Engine.Engine;

// The audio callback. Drains MIDI events at the start of each block, optionally
// routes them through the arpeggiator, renders all active voices, splits to a
// parallel ping-pong delay and a reverb, then sums dry + wet, soft-clips, and
// writes interleaved stereo.
public sealed class Mixer : ISampleProvider
{
    private readonly Delay _delay;
    private readonly MidiQueue _midi;
    private readonly MidiCcMap _ccMap;
    private readonly StereoReverb _reverb;
    private readonly VoiceState _voiceState;

    private bool _arpWasOn;
    private float[] _voiceBuffer = [];

    public Mixer(int sampleRate, SynthPatch patch, VoicePool pool, VoiceState voiceState, MidiQueue midi, MidiCcMap ccMap)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        Patch = patch;
        Pool = pool;
        _voiceState = voiceState;
        _midi = midi;
        _ccMap = ccMap;
        _reverb = new StereoReverb(sampleRate);
        _delay = new Delay(sampleRate);
        Arp = new Arpeggiator(sampleRate, patch.Arp);
    }

    public SynthPatch Patch { get; }

    public VoicePool Pool { get; }

    public Arpeggiator Arp { get; }

    public LevelMonitor Levels { get; } = new();

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / 2;
        if (_voiceBuffer.Length < frames)
        {
            _voiceBuffer = new float[frames];
        }

        Array.Clear(_voiceBuffer, 0, frames);

        HandleArpToggleEdges();

        DrainMidi();

        Arp.Tick(frames, Pool);

        foreach (var v in Pool.Voices)
        {
            if (v.IsActive)
            {
                v.RenderBlock(_voiceBuffer, 0, frames);
            }
        }

        var master = Patch.Master;
        var delayP = Patch.Delay;
        _reverb.Set(master.ReverbSize, master.ReverbDamp);
        _delay.Set(delayP.TimeMs, delayP.Feedback, delayP.Tone);

        var vol = master.Volume;
        var reverbMix = master.ReverbMix;
        var delayMix = delayP.Mix;

        float peakL = 0, peakR = 0;
        float sumL2 = 0, sumR2 = 0;

        for (var i = 0; i < frames; i++)
        {
            var dryS = _voiceBuffer[i];
            var (dL, dR) = _delay.Process(dryS);
            var (rL, rR) = _reverb.Process(dryS + (dL + dR) * 0.3f);

            var lPre = (dryS + dL * delayMix + rL * reverbMix) * vol;
            var rPre = (dryS + dR * delayMix + rR * reverbMix) * vol;

            var aL = lPre < 0 ? -lPre : lPre;
            var aR = rPre < 0 ? -rPre : rPre;
            if (aL > peakL)
            {
                peakL = aL;
            }

            if (aR > peakR)
            {
                peakR = aR;
            }

            sumL2 += lPre * lPre;
            sumR2 += rPre * rPre;

            buffer[offset + i * 2 + 0] = SoftClip(lPre);
            buffer[offset + i * 2 + 1] = SoftClip(rPre);
        }

        Levels.PeakL = peakL;
        Levels.PeakR = peakR;
        Levels.RmsL = MathF.Sqrt(sumL2 / Math.Max(1, frames));
        Levels.RmsR = MathF.Sqrt(sumR2 / Math.Max(1, frames));

        var active = 0;
        foreach (var v in Pool.Voices)
        {
            if (v.IsActive)
            {
                active++;
            }
        }

        Levels.ActiveVoices = active;
        Levels.PolyphonyMax = Patch.Polyphony;

        return count;
    }

    // When the arp is toggled we need to release any in-flight notes that
    // belong to the *other* routing (direct or arp-driven), or they get stuck.
    private void HandleArpToggleEdges()
    {
        var arpOn = Patch.Arp.On;
        if (arpOn != _arpWasOn)
        {
            Pool.AllNotesOff();
            Arp.Clear();
            _arpWasOn = arpOn;
        }
    }

    private void DrainMidi()
    {
        _voiceState.BendRangeSemitones = Patch.PitchBendRangeSemitones;
        var arpOn = Patch.Arp.On;

        while (_midi.TryDequeue(out var ev))
        {
            switch (ev.Kind)
            {
                case MidiKind.NoteOn:
                    if (arpOn)
                    {
                        Arp.NoteOn(ev.Data1, ev.Data2 / 127f);
                    }
                    else
                    {
                        Pool.NoteOn(ev.Data1, ev.Data2 / 127f);
                    }

                    break;

                case MidiKind.NoteOff:
                    if (arpOn)
                    {
                        Arp.NoteOff(ev.Data1);
                    }
                    else
                    {
                        Pool.NoteOff(ev.Data1);
                    }

                    break;

                case MidiKind.PitchBend:
                    _voiceState.PitchBend = (ev.Data2 - 8192) / 8192f;
                    break;

                case MidiKind.Cc:
                    HandleCc(ev.Data1, ev.Data2);
                    break;
            }
        }
    }

    private void HandleCc(byte controller, int value)
    {
        switch (controller)
        {
            case 1: _voiceState.ModWheel = value / 127f; return;
            case 64: Pool.SetSustainPedal(value >= 64); return;
            case 120:
                Pool.Panic();
                Arp.Clear();
                return;
            case 123:
                Pool.AllNotesOff();
                Arp.Clear();
                return;
        }

        _ccMap.ApplyCc(controller, value);
    }

    private static float SoftClip(float x)
    {
        return FastTanh.Tanh(x * 0.9f);
    }
}
