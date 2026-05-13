using System.Collections.Concurrent;

namespace Drift.Engine.Midi;

// MPSC: many MIDI input threads can enqueue, the audio thread drains.
public sealed class MidiQueue
{
    private readonly ConcurrentQueue<MidiEvent> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(MidiEvent ev)
    {
        _queue.Enqueue(ev);
    }

    public bool TryDequeue(out MidiEvent ev)
    {
        return _queue.TryDequeue(out ev);
    }
}