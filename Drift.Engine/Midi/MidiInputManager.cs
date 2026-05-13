using System.Runtime.CompilerServices;
using NAudio.Midi;

namespace Drift.Engine.Midi;

// Opens one MIDI input device, parses NAudio events into our flat MidiEvent value type,
// and pushes them onto a lock-free queue for the audio thread.
public sealed class MidiInputManager : IDisposable
{
    private readonly MidiQueue _queue;
    private MidiIn? _midi;

    public MidiInputManager(MidiQueue queue)
    {
        _queue = queue;
    }

    public string CurrentDeviceName { get; private set; } = "(none)";
    public DateTime LastEventUtc { get; private set; }

    // UI polls with Environment.TickCount64 (ms). Written from the MIDI callback thread.
    private long _lastNoteTickMs64;
    private long _lastCcTickMs64;
    private long _lastModWheelTickMs64;
    private long _lastPitchBendTickMs64;

    public long LastNoteTickMs64 => Volatile.Read(ref _lastNoteTickMs64);

    public long LastCcTickMs64 => Volatile.Read(ref _lastCcTickMs64);

    public long LastModWheelTickMs64 => Volatile.Read(ref _lastModWheelTickMs64);

    public long LastPitchBendTickMs64 => Volatile.Read(ref _lastPitchBendTickMs64);

    public void Dispose()
    {
        Close();
    }

    public static IReadOnlyList<string> ListDevices()
    {
        var list = new List<string>();
        for (var i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            try
            {
                list.Add(MidiIn.DeviceInfo(i).ProductName);
            }
            catch
            {
                list.Add($"(device {i})");
            }
        }

        return list;
    }

    public void OpenDevice(int? deviceIndex)
    {
        Close();
        if (deviceIndex is null)
        {
            return;
        }

        _midi = new MidiIn(deviceIndex.Value);
        _midi.MessageReceived += OnMessageReceived;
        _midi.ErrorReceived += OnErrorReceived;
        _midi.Start();
        try
        {
            CurrentDeviceName = MidiIn.DeviceInfo(deviceIndex.Value).ProductName;
        }
        catch
        {
            CurrentDeviceName = $"(device {deviceIndex.Value})";
        }
    }

    public void Close()
    {
        if (_midi is null)
        {
            return;
        }

        try
        {
            _midi.Stop();
        }
        catch
        {
        }

        _midi.MessageReceived -= OnMessageReceived;
        _midi.ErrorReceived -= OnErrorReceived;
        _midi.Dispose();
        _midi = null;
        CurrentDeviceName = "(none)";
        ClearActivityTimestamps();
    }

    private void ClearActivityTimestamps()
    {
        Volatile.Write(ref _lastNoteTickMs64, 0);
        Volatile.Write(ref _lastCcTickMs64, 0);
        Volatile.Write(ref _lastModWheelTickMs64, 0);
        Volatile.Write(ref _lastPitchBendTickMs64, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Touch(ref long field)
    {
        Volatile.Write(ref field, Environment.TickCount64);
    }

    private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        try
        {
            LastEventUtc = DateTime.UtcNow;
            switch (e.MidiEvent)
            {
                case NoteOnEvent noteOn:
                    Touch(ref _lastNoteTickMs64);
                    if (noteOn.Velocity == 0)
                    {
                        _queue.Enqueue(
                            new MidiEvent(MidiKind.NoteOff, (byte)noteOn.Channel, (byte)noteOn.NoteNumber, 0));
                    }
                    else
                    {
                        _queue.Enqueue(new MidiEvent(MidiKind.NoteOn, (byte)noteOn.Channel, (byte)noteOn.NoteNumber,
                            noteOn.Velocity));
                    }

                    break;

                case NoteEvent note when note.CommandCode == MidiCommandCode.NoteOff:
                    Touch(ref _lastNoteTickMs64);
                    _queue.Enqueue(new MidiEvent(MidiKind.NoteOff, (byte)note.Channel, (byte)note.NoteNumber,
                        note.Velocity));
                    break;

                case ControlChangeEvent cc:
                    var ccNum = (byte)cc.Controller;
                    if (ccNum == 1)
                    {
                        Touch(ref _lastModWheelTickMs64);
                    }
                    else
                    {
                        Touch(ref _lastCcTickMs64);
                    }

                    _queue.Enqueue(
                        new MidiEvent(MidiKind.Cc, (byte)cc.Channel, ccNum, cc.ControllerValue));
                    break;

                case PitchWheelChangeEvent pb:
                    Touch(ref _lastPitchBendTickMs64);
                    _queue.Enqueue(new MidiEvent(MidiKind.PitchBend, (byte)pb.Channel, 0, pb.Pitch));
                    break;
            }
        }
        catch
        {
            // MIDI driver hiccups should not take down the audio engine.
        }
    }

    private void OnErrorReceived(object? sender, MidiInMessageEventArgs e)
    {
        // Ignored for now; could route to a status indicator.
    }
}