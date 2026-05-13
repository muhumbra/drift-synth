namespace Drift.Engine.Midi;

// Routes incoming MIDI CC messages to patch parameters via per-CC slots.
// Per-machine, app-lifetime registry. Persisted to Settings/midimap.json by the UI.
//
// Threading model:
//   - Audio thread:  ApplyCc() drains the queue, reads _byCc via Volatile.Read,
//                    invokes the slot's setter. The setter is responsible for
//                    marshalling to the UI thread if its target needs that.
//   - Any thread:    Bind / Unbind / Arm / Disarm / RegisterParam mutate state
//                    under a small lock.
//   - Changed event fires from whichever thread mutated the map; subscribers
//     that touch Avalonia state must marshal to the UI thread themselves.
//
// Reserved CCs (1 mod wheel, 64 sustain, 120 all sound off, 123 all notes off)
// are excluded from learn / bind so the synth's hardcoded behaviour always
// wins. Bind() returns false for reserved CCs.
public sealed class MidiCcMap
{
    public const int CcCount = 128;

    private static readonly HashSet<byte> ReservedCcSet = [1, 64, 120, 123];

    public static IReadOnlyCollection<byte> ReservedCcs => ReservedCcSet;

    private readonly Slot?[] _byCc = new Slot?[CcCount];
    private readonly Dictionary<string, ParamRegistration> _params = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte> _byParamId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte> _pendingByParamId = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    private string? _armedParamId;

    public event Action<MidiCcMapChange>? Changed;

    public static bool IsReserved(byte cc)
    {
        return ReservedCcSet.Contains(cc);
    }

    public string? ArmedParamId
    {
        get
        {
            lock (_lock)
            {
                return _armedParamId;
            }
        }
    }

    // Currently bound CC for this paramId, or null. UI badge uses this.
    public byte? CcFor(string paramId)
    {
        lock (_lock)
        {
            return _byParamId.TryGetValue(paramId, out var cc) ? cc : null;
        }
    }

    // Snapshot for persistence.
    public Dictionary<string, byte> Snapshot()
    {
        lock (_lock)
        {
            return new Dictionary<string, byte>(_byParamId, StringComparer.Ordinal);
        }
    }

    // Knobs call this when they attach to the visual tree. Replaces any previous
    // registration for the same paramId. If a binding (real or pending) already
    // targets this paramId, the slot is wired up immediately so CCs start
    // flowing without any further action.
    public void RegisterParam(string paramId, Action<float> setter, double min, double max, bool logarithmic)
    {
        if (string.IsNullOrEmpty(paramId))
        {
            return;
        }

        var installCc = (byte)0;
        var doInstall = false;

        lock (_lock)
        {
            _params[paramId] = new ParamRegistration(setter, min, max, logarithmic);

            if (_pendingByParamId.TryGetValue(paramId, out var pendingCc) && !IsReserved(pendingCc))
            {
                _pendingByParamId.Remove(paramId);
                installCc = pendingCc;
                doInstall = true;
            }
            else if (_byParamId.TryGetValue(paramId, out var existingCc))
            {
                installCc = existingCc;
                doInstall = true;
            }
        }

        if (doInstall)
        {
            BindInternal(paramId, installCc, raiseEvent: false);
        }
    }

    // Knobs call this when they detach. Clears the slot but keeps the binding
    // in _byParamId so the badge can still display "CC 74" (and so the slot
    // gets re-installed when the knob attaches again, e.g. after a reload).
    public void UnregisterParam(string paramId)
    {
        if (string.IsNullOrEmpty(paramId))
        {
            return;
        }

        lock (_lock)
        {
            _params.Remove(paramId);
            if (_byParamId.TryGetValue(paramId, out var cc))
            {
                Volatile.Write(ref _byCc[cc], null);
            }
        }
    }

    public void Arm(string paramId)
    {
        if (string.IsNullOrEmpty(paramId))
        {
            return;
        }

        string? previous;
        lock (_lock)
        {
            previous = _armedParamId;
            _armedParamId = paramId;
        }

        if (!string.Equals(previous, paramId, StringComparison.Ordinal))
        {
            if (previous is not null)
            {
                RaiseChanged(MidiCcMapChange.Disarmed(previous));
            }

            RaiseChanged(MidiCcMapChange.Armed(paramId));
        }
    }

    public void Disarm()
    {
        string? was;
        lock (_lock)
        {
            was = _armedParamId;
            _armedParamId = null;
        }

        if (was is not null)
        {
            RaiseChanged(MidiCcMapChange.Disarmed(was));
        }
    }

    // True if the slot was installed; false if the CC is reserved/out of range
    // or the paramId has no registration yet (in which case it goes into the
    // pending list and will install on the next RegisterParam).
    public bool Bind(string paramId, byte cc)
    {
        return BindInternal(paramId, cc, raiseEvent: true);
    }

    public void Unbind(string paramId)
    {
        if (string.IsNullOrEmpty(paramId))
        {
            return;
        }

        byte? removedCc = null;
        lock (_lock)
        {
            if (_byParamId.TryGetValue(paramId, out var cc))
            {
                removedCc = cc;
                _byParamId.Remove(paramId);
                Volatile.Write(ref _byCc[cc], null);
            }
            else
            {
                _pendingByParamId.Remove(paramId);
            }
        }

        if (removedCc.HasValue)
        {
            RaiseChanged(MidiCcMapChange.Unbound(paramId, removedCc.Value));
        }
    }

    // Used at startup. Slots are installed lazily as knobs register; until then
    // the desired CC sits in the pending list keyed by paramId.
    public void LoadPending(IReadOnlyDictionary<string, byte> bindings)
    {
        lock (_lock)
        {
            for (var i = 0; i < CcCount; i++)
            {
                Volatile.Write(ref _byCc[i], null);
            }

            _byParamId.Clear();
            _pendingByParamId.Clear();

            foreach (var (paramId, cc) in bindings)
            {
                if (string.IsNullOrEmpty(paramId) || cc >= CcCount || IsReserved(cc))
                {
                    continue;
                }

                _pendingByParamId[paramId] = cc;
            }
        }
    }

    // Called per non-reserved CC that arrives. Audio thread in normal use.
    // If a paramId is armed and the CC is non-reserved, this also performs the
    // bind (consuming the armed state).
    public void ApplyCc(byte cc, int value)
    {
        if (cc >= CcCount)
        {
            return;
        }

        if (_armedParamId is not null && !IsReserved(cc))
        {
            string? armed = null;
            lock (_lock)
            {
                if (_armedParamId is not null)
                {
                    armed = _armedParamId;
                    _armedParamId = null;
                }
            }

            if (armed is not null)
            {
                BindInternal(armed, cc, raiseEvent: true);
            }
        }

        var slot = Volatile.Read(ref _byCc[cc]);
        if (slot is null)
        {
            return;
        }

        var unit = value <= 0 ? 0f : value >= 127 ? 1f : value / 127f;
        double v;
        if (slot.Logarithmic)
        {
            var minSafe = Math.Max(slot.Min, 1e-6);
            var span = Math.Log(slot.Max / minSafe);
            v = minSafe * Math.Exp(unit * span);
        }
        else
        {
            v = slot.Min + (slot.Max - slot.Min) * unit;
        }

        slot.Setter((float)v);
    }

    private bool BindInternal(string paramId, byte cc, bool raiseEvent)
    {
        if (string.IsNullOrEmpty(paramId) || cc >= CcCount || IsReserved(cc))
        {
            return false;
        }

        string? evictedParam = null;
        byte? oldCcForParam = null;
        var installed = false;

        lock (_lock)
        {
            // 1) Steal: another paramId previously held this CC -> evict.
            foreach (var kv in _byParamId)
            {
                if (kv.Value == cc && !string.Equals(kv.Key, paramId, StringComparison.Ordinal))
                {
                    evictedParam = kv.Key;
                    break;
                }
            }

            if (evictedParam is not null)
            {
                _byParamId.Remove(evictedParam);
            }

            // 2) Same paramId already had a different CC -> free the old slot.
            if (_byParamId.TryGetValue(paramId, out var prev) && prev != cc)
            {
                oldCcForParam = prev;
                Volatile.Write(ref _byCc[prev], null);
                _byParamId.Remove(paramId);
            }

            // 3) Install if the param is registered; otherwise hold as pending.
            if (_params.TryGetValue(paramId, out var reg))
            {
                _byParamId[paramId] = cc;
                _pendingByParamId.Remove(paramId);
                Volatile.Write(ref _byCc[cc], new Slot(reg.Setter, reg.Min, reg.Max, reg.Logarithmic));
                installed = true;
            }
            else
            {
                _pendingByParamId[paramId] = cc;
            }
        }

        if (raiseEvent)
        {
            if (evictedParam is not null)
            {
                RaiseChanged(MidiCcMapChange.Unbound(evictedParam, cc));
            }

            if (oldCcForParam.HasValue)
            {
                RaiseChanged(MidiCcMapChange.Unbound(paramId, oldCcForParam.Value));
            }

            RaiseChanged(MidiCcMapChange.Bound(paramId, cc));
        }

        return installed;
    }

    private void RaiseChanged(MidiCcMapChange change)
    {
        try
        {
            Changed?.Invoke(change);
        }
        catch
        {
            // A faulty subscriber must not corrupt the map.
        }
    }

    private sealed class Slot
    {
        public Slot(Action<float> setter, double min, double max, bool log)
        {
            Setter = setter;
            Min = min;
            Max = max;
            Logarithmic = log;
        }

        public Action<float> Setter { get; }
        public double Min { get; }
        public double Max { get; }
        public bool Logarithmic { get; }
    }

    private readonly record struct ParamRegistration(Action<float> Setter, double Min, double Max, bool Logarithmic);
}

public enum MidiCcMapChangeKind
{
    Bound,
    Unbound,
    Armed,
    Disarmed
}

public readonly record struct MidiCcMapChange(MidiCcMapChangeKind Kind, string ParamId, byte Cc)
{
    public static MidiCcMapChange Bound(string paramId, byte cc)
    {
        return new MidiCcMapChange(MidiCcMapChangeKind.Bound, paramId, cc);
    }

    public static MidiCcMapChange Unbound(string paramId, byte cc)
    {
        return new MidiCcMapChange(MidiCcMapChangeKind.Unbound, paramId, cc);
    }

    public static MidiCcMapChange Armed(string paramId)
    {
        return new MidiCcMapChange(MidiCcMapChangeKind.Armed, paramId, 0);
    }

    public static MidiCcMapChange Disarmed(string paramId)
    {
        return new MidiCcMapChange(MidiCcMapChangeKind.Disarmed, paramId, 0);
    }
}
