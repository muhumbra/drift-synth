using Drift.Engine.Dsp;
using Drift.Engine.Synth;

namespace Drift.Engine.Sequencer;

// Arpeggiator. Owns a set of "active" notes (driven by the keyboard, with
// optional latching), builds a play sequence based on the current Mode +
// Octaves, and ticks per audio block to fire NoteOn / NoteOff into the
// VoicePool at sample-accurate offsets within the block.
//
// Latch behaviour: while latched, NoteOff doesn't remove the note. Pressing
// a key after all physical keys are released starts a fresh chord.
public sealed class Arpeggiator
{
    private readonly List<(int note, float vel)> _activeSet = new();
    private readonly List<int> _currentlySounding = new();
    private readonly ArpParams _params;

    private readonly HashSet<int> _physicallyHeld = new();
    private readonly FastRng _rng;
    private readonly int _sampleRate;
    private readonly List<int> _sequence = new();

    // Track the last params so we can rebuild the sequence when Mode/Octaves change.
    private ArpMode _lastMode;
    private int _lastOctaves;
    private float _lastVel = 0.85f;
    private int _samplesUntilGateOff;

    private int _samplesUntilStep;

    private int _seqIndex;
    private bool _useLongSwingStep;

    public Arpeggiator(int sampleRate, ArpParams arpParams)
    {
        _sampleRate = sampleRate;
        _params = arpParams;
        _lastMode = arpParams.Mode;
        _lastOctaves = (int)arpParams.Octaves;
        _rng = FastRng.CreateUncorrelated();
    }

    public void NoteOn(int note, float vel)
    {
        // If latched and no physical keys are currently down, a fresh press
        // means start a new chord.
        if (_params.Latch && _physicallyHeld.Count == 0 && !_activeSet.Any(x => x.note == note))
        {
            _activeSet.Clear();
        }

        _physicallyHeld.Add(note);
        if (!_activeSet.Any(x => x.note == note))
        {
            _activeSet.Add((note, vel));
        }

        _lastVel = vel;
        RebuildSequence();

        // If the arp was idle, schedule the first step on the next tick.
        if (_currentlySounding.Count == 0 && _samplesUntilStep <= 0 && _activeSet.Count == 1)
        {
            _samplesUntilStep = 0;
        }
    }

    public void NoteOff(int note)
    {
        _physicallyHeld.Remove(note);
        if (!_params.Latch)
        {
            _activeSet.RemoveAll(x => x.note == note);
            RebuildSequence();
        }
    }

    public void Clear()
    {
        _physicallyHeld.Clear();
        _activeSet.Clear();
        RebuildSequence();
    }

    public void Tick(int frames, VoicePool pool)
    {
        // Rebuild if Mode / Octaves changed live.
        var octsNow = (int)_params.Octaves;
        if (_params.Mode != _lastMode || octsNow != _lastOctaves)
        {
            _lastMode = _params.Mode;
            _lastOctaves = octsNow;
            RebuildSequence();
        }

        if (!_params.On || _activeSet.Count == 0)
        {
            ReleaseSounding(pool);
            _samplesUntilStep = 0;
            _samplesUntilGateOff = 0;
            _seqIndex = 0;
            _useLongSwingStep = false;
            return;
        }

        if (_sequence.Count == 0)
        {
            RebuildSequence();
        }

        if (_sequence.Count == 0)
        {
            return;
        }

        var t = 0;
        while (t < frames)
        {
            var chunk = frames - t;
            if (_samplesUntilGateOff > 0 && _samplesUntilGateOff < chunk)
            {
                chunk = _samplesUntilGateOff;
            }

            if (_samplesUntilStep > 0 && _samplesUntilStep < chunk)
            {
                chunk = _samplesUntilStep;
            }

            if (chunk <= 0)
            {
                chunk = 1;
            }

            if (_samplesUntilGateOff > 0)
            {
                _samplesUntilGateOff -= chunk;
                if (_samplesUntilGateOff <= 0)
                {
                    ReleaseSounding(pool);
                }
            }

            _samplesUntilStep -= chunk;
            if (_samplesUntilStep <= 0)
            {
                FireStep(pool);
            }

            t += chunk;
        }
    }

    private void FireStep(VoicePool pool)
    {
        ReleaseSounding(pool);

        var stepSec = StepSeconds();
        var stepSamples = Math.Max(1, (int)(stepSec * _sampleRate));

        // Swing: every other step is delayed by Swing * step length.
        var swingDelaySec = _useLongSwingStep ? stepSec * _params.Swing : 0f;
        _useLongSwingStep = !_useLongSwingStep;

        if (_params.Mode == ArpMode.Chord)
        {
            foreach (var item in _activeSet)
            {
                pool.NoteOn(item.note, item.vel);
                _currentlySounding.Add(item.note);
            }
        }
        else if (_params.Mode == ArpMode.Random)
        {
            var note = _sequence[_rng.Next(_sequence.Count)];
            pool.NoteOn(note, _lastVel);
            _currentlySounding.Add(note);
        }
        else
        {
            if (_seqIndex >= _sequence.Count)
            {
                _seqIndex = 0;
            }

            var note = _sequence[_seqIndex];
            pool.NoteOn(note, _lastVel);
            _currentlySounding.Add(note);
            _seqIndex = (_seqIndex + 1) % _sequence.Count;
        }

        _samplesUntilGateOff = Math.Max(1, (int)(stepSamples * _params.Gate));
        _samplesUntilStep = stepSamples + (int)(swingDelaySec * _sampleRate);
    }

    private void ReleaseSounding(VoicePool pool)
    {
        foreach (var n in _currentlySounding)
        {
            pool.NoteOff(n);
        }

        _currentlySounding.Clear();
    }

    private float StepSeconds()
    {
        var beat = 60f / MathF.Max(_params.Bpm, 1f);
        return beat * _params.Rate.BeatMultiplier;
    }

    private void RebuildSequence()
    {
        _sequence.Clear();
        if (_activeSet.Count == 0)
        {
            _seqIndex = 0;
            return;
        }

        var sorted = _activeSet.Select(x => x.note).Distinct().OrderBy(n => n).ToList();
        var asPlayed = _activeSet.Select(x => x.note).Distinct().ToList();
        var octs = Math.Max(1, (int)_params.Octaves);

        switch (_params.Mode)
        {
            case ArpMode.Up:
                for (var oct = 0; oct < octs; oct++)
                {
                    foreach (var n in sorted)
                    {
                        _sequence.Add(n + oct * 12);
                    }
                }

                break;

            case ArpMode.Down:
                for (var oct = octs - 1; oct >= 0; oct--)
                {
                    foreach (var n in sorted.AsEnumerable().Reverse())
                    {
                        _sequence.Add(n + oct * 12);
                    }
                }

                break;

            case ArpMode.UpDown:
            {
                var up = new List<int>();
                for (var oct = 0; oct < octs; oct++)
                {
                    foreach (var n in sorted)
                    {
                        up.Add(n + oct * 12);
                    }
                }

                _sequence.AddRange(up);
                if (up.Count > 2)
                {
                    foreach (var n in up.AsEnumerable().Reverse().Skip(1).SkipLast(1))
                    {
                        _sequence.Add(n);
                    }
                }

                break;
            }

            case ArpMode.DownUp:
            {
                var up = new List<int>();
                for (var oct = 0; oct < octs; oct++)
                {
                    foreach (var n in sorted)
                    {
                        up.Add(n + oct * 12);
                    }
                }

                foreach (var n in up.AsEnumerable().Reverse())
                {
                    _sequence.Add(n);
                }

                if (up.Count > 2)
                {
                    foreach (var n in up.Skip(1).SkipLast(1))
                    {
                        _sequence.Add(n);
                    }
                }

                break;
            }

            case ArpMode.Random:
                for (var oct = 0; oct < octs; oct++)
                {
                    foreach (var n in sorted)
                    {
                        _sequence.Add(n + oct * 12);
                    }
                }

                break;

            case ArpMode.AsPlayed:
                for (var oct = 0; oct < octs; oct++)
                {
                    foreach (var n in asPlayed)
                    {
                        _sequence.Add(n + oct * 12);
                    }
                }

                break;

            case ArpMode.Chord:
                _sequence.Add(0); // sentinel, real handling in FireStep
                break;
        }

        if (_seqIndex >= _sequence.Count)
        {
            _seqIndex = 0;
        }
    }
}
