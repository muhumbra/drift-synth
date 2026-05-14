namespace Drift.Engine.Synth;

// Polyphonic + monophonic voice allocator.
//
// Poly mode: picks the best voice for an incoming note in this order:
//   1) An idle voice (free).
//   2) A voice already releasing (steals the quietest tail).
//   3) The oldest sustained voice (last-resort steal).
//
// Mono mode: only voice 0 is used. A note stack tracks held keys so releasing
// the top key falls back to the previously-held one. With Legato on, retriggers
// don't restart envelopes -- the pitch just glides to the new note.
//
// Sustain pedal: notes released while the pedal is down stay alive until the
// pedal goes up.
public sealed class VoicePool
{
    private readonly bool[] _heldByPedal;
    private readonly List<(int note, float vel)> _monoStack = new();
    private readonly SynthPatch _patch;
    private readonly ISynthVoice[] _voices;
    private readonly VoiceState _voiceState;

    public VoicePool(ISynthVoice[] voices, VoiceState voiceState, SynthPatch patch)
    {
        _voices = voices;
        _voiceState = voiceState;
        _patch = patch;
        _heldByPedal = new bool[voices.Length];
    }

    public IReadOnlyList<ISynthVoice> Voices => _voices;

    public void NoteOn(int note, float velocity)
    {
        if (_patch.Voice.Mono)
        {
            MonoNoteOn(note, velocity);
        }
        else
        {
            PolyNoteOn(note, velocity);
        }
    }

    public void NoteOff(int note)
    {
        if (_patch.Voice.Mono)
        {
            MonoNoteOff(note);
        }
        else
        {
            PolyNoteOff(note);
        }
    }

    public void SetSustainPedal(bool down)
    {
        var wasDown = _voiceState.SustainPedal;
        _voiceState.SustainPedal = down;
        if (wasDown && !down)
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                if (_heldByPedal[i])
                {
                    _heldByPedal[i] = false;
                    _voices[i].NoteOff();
                }
            }
        }
    }

    public void AllNotesOff()
    {
        _monoStack.Clear();
        for (var i = 0; i < _voices.Length; i++)
        {
            _heldByPedal[i] = false;
            _voices[i].NoteOff();
        }
    }

    public void Panic()
    {
        _monoStack.Clear();
        for (var i = 0; i < _voices.Length; i++)
        {
            _heldByPedal[i] = false;
            _voices[i].HardReset();
        }
    }

    private void PolyNoteOn(int note, float velocity)
    {
        var idx = FindVoiceToSteal();
        _heldByPedal[idx] = false;
        _voices[idx].NoteOn(note, velocity);
    }

    private void PolyNoteOff(int note)
    {
        for (var i = 0; i < _voices.Length; i++)
        {
            if (_voices[i].Note == note && _voices[i].IsActive && !_voices[i].IsReleasing)
            {
                if (_voiceState.SustainPedal)
                {
                    _heldByPedal[i] = true;
                }
                else
                {
                    _voices[i].NoteOff();
                }
            }
        }
    }

    private void MonoNoteOn(int note, float vel)
    {
        var voice = _voices[0];
        var wasActive = voice.IsActive && !voice.IsReleasing;
        var previous = voice.Note;

        // remove duplicate of this note then push to top
        _monoStack.RemoveAll(x => x.note == note);
        _monoStack.Add((note, vel));
        _heldByPedal[0] = false;

        if (wasActive && _patch.Voice.MonoLegato)
        {
            voice.GlideTo(note);
        }
        else
        {
            voice.NoteOn(note, vel);
            if (wasActive && previous >= 0)
            {
                voice.GlideFrom(previous);
            }
        }
    }

    private void MonoNoteOff(int note)
    {
        _monoStack.RemoveAll(x => x.note == note);
        if (_monoStack.Count == 0)
        {
            if (_voiceState.SustainPedal)
            {
                _heldByPedal[0] = true;
            }
            else
            {
                _voices[0].NoteOff();
            }
        }
        else
        {
            var (top, _) = _monoStack[^1];
            _voices[0].GlideTo(top);
        }
    }

    private int FindVoiceToSteal()
    {
        var oldest = 0;
        var oldestTs = long.MaxValue;
        var oldestReleasing = -1;
        var oldestRelTs = long.MaxValue;

        for (var i = 0; i < _voices.Length; i++)
        {
            var v = _voices[i];
            if (!v.IsActive)
            {
                return i;
            }

            if (v.IsReleasing && v.StartTimestamp < oldestRelTs)
            {
                oldestRelTs = v.StartTimestamp;
                oldestReleasing = i;
            }

            if (v.StartTimestamp < oldestTs)
            {
                oldestTs = v.StartTimestamp;
                oldest = i;
            }
        }

        return oldestReleasing >= 0 ? oldestReleasing : oldest;
    }
}
