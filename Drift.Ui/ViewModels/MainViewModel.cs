using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Drift.Engine.Engine;
using Drift.Engine.Midi;
using Drift.Engine.Patches;
using Drift.Engine.Sequencer;
using Drift.Engine.Settings;
using Drift.Engine.Synth;

namespace Drift.Ui.ViewModels;

public sealed class MainViewModel : Observable
{
    private string _latencyText = "-- ms";

    private string _playLabel = "START AUDIO";

    private ArpPreset? _selectedArpPreset;

    private string? _selectedDriver;

    private string? _selectedMidi;

    private PatchEntry? _selectedPatch;

    private string _statusText = "Ready. Pick an ASIO driver to start.";

    private bool _profilerOpen;

    private DispatcherTimer? _profilerTimer;

    private Process? _diagProcess;

    private bool _diagCpuPrimed;

    private TimeSpan _diagLastCpu = TimeSpan.Zero;

    private long _diagLastWallMs;

    private string _profilerMixerBody =
        "Per-block timings require an ASIO driver (mixer not built yet).";

    private string _profilerManagedBody = "";

    private string _profilerProcessBody = "";

    private string _profilerBudgetText = "";

    private readonly string _settingsPath;

    private readonly string _midiMapPath;

    private bool _suppressSettingsPersistence;

    private DispatcherTimer? _settingsSaveDebounceTimer;

    private DispatcherTimer? _midiMapSaveDebounceTimer;

    private bool _isPatchModified;

    private bool _suppressPatchDirty;

    public MainViewModel(AudioEngine engine, PatchManager patches, string settingsPath, string midiMapPath)
    {
        Engine = engine;
        Patches = patches;
        _settingsPath = settingsPath;
        _midiMapPath = midiMapPath;
        Patch.Master.PropertyChanged += OnMasterPropertyChanged;
        Engine.CcMap.Changed += OnCcMapChanged;
        HookPatchDirtyTracking();
        RefreshDevices();
        RefreshPatches();
    }

    private void HookPatchDirtyTracking()
    {
        Patch.PropertyChanged += OnPatchDirtyChanged;
        Patch.Osc1.PropertyChanged += OnPatchDirtyChanged;
        Patch.Osc2.PropertyChanged += OnPatchDirtyChanged;
        Patch.Mixer.PropertyChanged += OnPatchDirtyChanged;
        Patch.Filter.PropertyChanged += OnPatchDirtyChanged;
        Patch.AmpEnv.PropertyChanged += OnPatchDirtyChanged;
        Patch.FilterEnv.PropertyChanged += OnPatchDirtyChanged;
        Patch.Lfo.PropertyChanged += OnPatchDirtyChanged;
        Patch.Voice.PropertyChanged += OnPatchDirtyChanged;
        Patch.Delay.PropertyChanged += OnPatchDirtyChanged;
        Patch.Master.PropertyChanged += OnPatchDirtyChanged;
        Patch.Arp.PropertyChanged += OnPatchDirtyChanged;
    }

    private void OnPatchDirtyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Patch.Name updates always need to refresh the display string, even when
        // we're suppressing the dirty flag (e.g. loading a saved patch).
        if (ReferenceEquals(sender, Patch) && e.PropertyName == nameof(SynthPatch.Name))
        {
            Raise(nameof(PatchDisplayName));
        }

        if (_suppressPatchDirty)
        {
            return;
        }

        IsPatchModified = true;
    }

    public bool IsPatchModified
    {
        get => _isPatchModified;
        private set
        {
            if (Set(ref _isPatchModified, value))
            {
                Raise(nameof(PatchDisplayName));
            }
        }
    }

    public string PatchDisplayName
    {
        get
        {
            var prefix = _isPatchModified ? "• " : "";
            return $"now playing: {prefix}{Patch.Name}";
        }
    }

    public AudioEngine Engine { get; }
    public PatchManager Patches { get; }
    public SynthPatch Patch => Engine.Patch;
    public LevelMonitor? Levels => Engine.Levels;
    public MidiInputManager Midi => Engine.Midi;
    public MidiCcMap CcMap => Engine.CcMap;

    public bool ProfilerOpen
    {
        get => _profilerOpen;
        set
        {
            if (!Set(ref _profilerOpen, value))
            {
                return;
            }

            if (value)
            {
                Engine.Profiler?.ResetEma();
                if (Engine.Profiler is not null)
                {
                    Engine.Profiler.Enabled = true;
                }

                _diagCpuPrimed = false;
                EnsureProfilerTimer();
                _profilerTimer!.Start();
                RefreshProfilerDisplay();
            }
            else
            {
                if (Engine.Profiler is not null)
                {
                    Engine.Profiler.Enabled = false;
                }

                _profilerTimer?.Stop();
            }
        }
    }

    public string ProfilerMixerBody
    {
        get => _profilerMixerBody;
        private set => Set(ref _profilerMixerBody, value);
    }

    public string ProfilerManagedBody
    {
        get => _profilerManagedBody;
        private set => Set(ref _profilerManagedBody, value);
    }

    public string ProfilerProcessBody
    {
        get => _profilerProcessBody;
        private set => Set(ref _profilerProcessBody, value);
    }

    public string ProfilerBudgetText
    {
        get => _profilerBudgetText;
        private set => Set(ref _profilerBudgetText, value);
    }

    public IReadOnlyList<string> AsioDrivers { get; private set; } = [];
    public IReadOnlyList<string> MidiInputs { get; private set; } = [];
    public IReadOnlyList<PatchEntry> PatchList { get; private set; } = [];

    public string? SelectedDriver
    {
        get => _selectedDriver;
        set
        {
            if (Set(ref _selectedDriver, value))
            {
                ApplyDriver();
            }
        }
    }

    public string? SelectedMidi
    {
        get => _selectedMidi;
        set
        {
            if (Set(ref _selectedMidi, value))
            {
                ApplyMidi();
            }
        }
    }

    public PatchEntry? SelectedPatch
    {
        get => _selectedPatch;
        set
        {
            if (Set(ref _selectedPatch, value))
            {
                ApplyPatch();
            }
        }
    }

    public ArpPreset? SelectedArpPreset
    {
        get => _selectedArpPreset;
        set
        {
            if (Set(ref _selectedArpPreset, value))
            {
                ApplyArpPreset();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    public string LatencyText
    {
        get => _latencyText;
        set => Set(ref _latencyText, value);
    }

    public string PlayLabel
    {
        get => _playLabel;
        set => Set(ref _playLabel, value);
    }

    public void ApplyStartupSettings(DriftSettings settings, PatchEntry? fallbackFirst)
    {
        _suppressSettingsPersistence = true;
        _suppressPatchDirty = true;
        try
        {
            var patchLoaded = false;
            if (!string.IsNullOrWhiteSpace(settings.LastPatchPath) && File.Exists(settings.LastPatchPath))
            {
                try
                {
                    Patches.LoadInto(Patch, settings.LastPatchPath);
                    _selectedPatch = PatchList.FirstOrDefault(p =>
                        string.Equals(p.FilePath, settings.LastPatchPath, StringComparison.OrdinalIgnoreCase));
                    Raise(nameof(SelectedPatch));
                    patchLoaded = true;
                }
                catch
                {
                    // Fall through to factory default.
                }
            }

            if (!patchLoaded && fallbackFirst is not null)
            {
                Patches.LoadInto(Patch, fallbackFirst.FilePath);
                _selectedPatch = fallbackFirst;
                Raise(nameof(SelectedPatch));
            }

            if (settings.MasterVolume is float mvVol && mvVol >= 0f && mvVol <= 1f)
            {
                Patch.Master.Volume = mvVol;
            }

            if (!string.IsNullOrWhiteSpace(settings.AsioDriver))
            {
                if (AsioDrivers.Contains(settings.AsioDriver))
                {
                    _selectedDriver = settings.AsioDriver;
                    Raise(nameof(SelectedDriver));
                    ApplyDriver();
                }
                else
                {
                    _selectedDriver = AsioDrivers.Count > 0 ? AsioDrivers[0] : "(no driver)";
                    Raise(nameof(SelectedDriver));
                    StatusText = $"Saved ASIO device '{settings.AsioDriver}' is not available.";
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.MidiInput) && MidiInputs.Contains(settings.MidiInput))
            {
                _selectedMidi = settings.MidiInput;
                Raise(nameof(SelectedMidi));
                ApplyMidi();
            }
        }
        finally
        {
            _suppressSettingsPersistence = false;
            _suppressPatchDirty = false;
            IsPatchModified = false;
        }

        // If drift.json restored a driver and it opened, start the engine so the
        // session matches "last time" without an extra click.
        if (!string.IsNullOrWhiteSpace(settings.AsioDriver)
            && string.Equals(Engine.CurrentDriverName, settings.AsioDriver, StringComparison.OrdinalIgnoreCase)
            && !Engine.IsPlaying)
        {
            Engine.Start();
            PlayLabel = "STOP AUDIO";
            StatusText = $"Playing on {Engine.CurrentDriverName}.";
        }
    }

    public static void ApplyWindowPlacement(Window window, DriftSettings settings)
    {
        if (settings.WindowWidth is int ww && settings.WindowHeight is int wh)
        {
            var minW = (int)Math.Ceiling(window.MinWidth);
            var minH = (int)Math.Ceiling(window.MinHeight);
            if (ww >= minW && wh >= minH)
            {
                window.Width = ww;
                window.Height = wh;
            }
        }

        if (settings.WindowX is int x && settings.WindowY is int y)
        {
            window.Position = new PixelPoint(x, y);
        }
    }

    public void PersistSettingsImmediate(Window window)
    {
        FlushSettingsToDisk(window);
        FlushMidiMapToDisk();
    }

    public void RefreshDevices()
    {
        var drivers = new List<string> { "(no driver)" };
        drivers.AddRange(AudioEngine.ListAsioDrivers());
        AsioDrivers = drivers;
        Raise(nameof(AsioDrivers));

        var midis = new List<string> { "(no MIDI input)" };
        midis.AddRange(MidiInputManager.ListDevices());
        MidiInputs = midis;
        Raise(nameof(MidiInputs));

        if (string.IsNullOrEmpty(_selectedDriver))
        {
            _selectedDriver = drivers[0];
            Raise(nameof(SelectedDriver));
        }

        if (string.IsNullOrEmpty(_selectedMidi))
        {
            _selectedMidi = midis[0];
            Raise(nameof(SelectedMidi));
        }
    }

    public void RefreshPatches()
    {
        PatchList = Patches.List();
        Raise(nameof(PatchList));
    }

    public void TogglePlay()
    {
        if (Engine.CurrentDriverName == "(none)")
        {
            StatusText = "Pick an ASIO driver first.";
            return;
        }

        if (Engine.IsPlaying)
        {
            Engine.Stop();
            PlayLabel = "START AUDIO";
            StatusText = "Stopped.";
        }
        else
        {
            Engine.Start();
            PlayLabel = "STOP AUDIO";
            StatusText = $"Playing on {Engine.CurrentDriverName}.";
        }
    }

    public void OpenAsioPanel()
    {
        try
        {
            Engine.OpenAsioControlPanel();
        }
        catch (Exception ex)
        {
            StatusText = $"ASIO panel error: {ex.Message}";
        }
    }

    public void Panic()
    {
        Engine.Panic();
        StatusText = "Panic. All voices reset.";
    }

    public void ClearArp()
    {
        Engine.ClearArp();
        StatusText = "Arp cleared.";
    }

    public void RandomizePatch()
    {
        PatchRandomizer.Randomize(Patch);
        // Drop the dropdown selection so it's clear this isn't a saved file.
        _selectedPatch = null;
        Raise(nameof(SelectedPatch));
        // A randomized patch is fresh and unsaved -- treat as modified so the
        // bullet shows up next to the name.
        IsPatchModified = true;
        StatusText = $"Randomized -> {Patch.Name}";
        ScheduleSettingsSave();
    }

    private void ApplyArpPreset()
    {
        if (_selectedArpPreset is null)
        {
            return;
        }

        _selectedArpPreset.Apply(Patch.Arp);
        StatusText = $"Arp preset: {_selectedArpPreset.Name}";
    }

    public void SaveCurrentPatch()
    {
        if (string.IsNullOrEmpty(Patches.CurrentFile))
        {
            StatusText = "No patch file selected. Use Save As.";
            return;
        }

        try
        {
            Patches.Save(Patch);
            IsPatchModified = false;
            StatusText = $"Saved {Path.GetFileName(Patches.CurrentFile)}.";
            ScheduleSettingsSave();
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    public void SaveAs(string filePath)
    {
        try
        {
            Patches.SaveAs(Patch, filePath);
            RefreshPatches();
            _selectedPatch = PatchList.FirstOrDefault(p =>
                string.Equals(p.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            Raise(nameof(SelectedPatch));
            IsPatchModified = false;
            StatusText = $"Saved {Path.GetFileName(filePath)}.";
            ScheduleSettingsSave();
        }
        catch (Exception ex)
        {
            StatusText = $"Save As failed: {ex.Message}";
        }
    }

    public string SuggestedSaveAsFolder()
    {
        return Patches.Folder;
    }

    private void ApplyDriver()
    {
        if (string.IsNullOrEmpty(_selectedDriver) || _selectedDriver.StartsWith("(no"))
        {
            Engine.CloseDriver();
            LatencyText = "-- ms";
            PlayLabel = "START AUDIO";
            StatusText = "No driver. Pick one above.";
            Raise(nameof(Levels));
            if (_profilerOpen)
            {
                RefreshProfilerDisplay();
            }

            ScheduleSettingsSave();
            return;
        }

        try
        {
            Engine.OpenDriver(_selectedDriver);
            LatencyText = $"{Engine.CurrentLatencyMs} ms @ {Engine.SampleRate / 1000.0:0.#}k";
            StatusText = $"Opened {Engine.CurrentDriverName}.";
            PlayLabel = Engine.IsPlaying ? "STOP AUDIO" : "START AUDIO";
            Raise(nameof(Levels));
            if (_profilerOpen && Engine.Profiler is not null)
            {
                Engine.Profiler.Enabled = true;
                Engine.Profiler.ResetEma();
            }

            if (_profilerOpen)
            {
                RefreshProfilerDisplay();
            }

            ScheduleSettingsSave();
        }
        catch (Exception ex)
        {
            StatusText = $"Driver failed: {ex.Message}";
            LatencyText = "-- ms";
            ScheduleSettingsSave();
        }
    }

    private void ApplyMidi()
    {
        if (string.IsNullOrEmpty(_selectedMidi) || _selectedMidi.StartsWith("(no"))
        {
            Engine.Midi.Close();
            ScheduleSettingsSave();
            return;
        }

        var idx = MidiInputs.ToList().IndexOf(_selectedMidi) - 1;
        if (idx < 0)
        {
            return;
        }

        try
        {
            Engine.Midi.OpenDevice(idx);
            StatusText = $"MIDI input: {Engine.Midi.CurrentDeviceName}";
            ScheduleSettingsSave();
        }
        catch (Exception ex)
        {
            StatusText = $"MIDI open failed: {ex.Message}";
            ScheduleSettingsSave();
        }
    }

    private void ApplyPatch()
    {
        if (_selectedPatch is null)
        {
            return;
        }

        _suppressPatchDirty = true;
        try
        {
            Patches.LoadInto(Patch, _selectedPatch.FilePath);
            StatusText = $"Loaded {_selectedPatch.DisplayName}";
            ScheduleSettingsSave();
        }
        catch (Exception ex)
        {
            StatusText = $"Patch load failed: {ex.Message}";
            ScheduleSettingsSave();
        }
        finally
        {
            _suppressPatchDirty = false;
            IsPatchModified = false;
        }
    }

    private void OnMasterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSettingsPersistence)
        {
            return;
        }

        if (e.PropertyName == nameof(MasterParams.Volume))
        {
            ScheduleSettingsSave();
        }
    }

    private void EnsureSettingsSaveDebounceTimer()
    {
        if (_settingsSaveDebounceTimer is not null)
        {
            return;
        }

        _settingsSaveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _settingsSaveDebounceTimer.Tick += (_, _) =>
        {
            _settingsSaveDebounceTimer!.Stop();
            FlushSettingsToDisk(windowForPlacement: null);
        };
    }

    private void ScheduleSettingsSave()
    {
        if (_suppressSettingsPersistence || string.IsNullOrEmpty(_settingsPath))
        {
            return;
        }

        EnsureSettingsSaveDebounceTimer();
        _settingsSaveDebounceTimer!.Stop();
        _settingsSaveDebounceTimer.Start();
    }

    private void OnCcMapChanged(MidiCcMapChange change)
    {
        // Bound / Unbound mutate persisted state; Armed / Disarmed are transient
        // UI affordances that we don't write to disk.
        if (change.Kind != MidiCcMapChangeKind.Bound && change.Kind != MidiCcMapChangeKind.Unbound)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ScheduleMidiMapSave();
        }
        else
        {
            Dispatcher.UIThread.Post(ScheduleMidiMapSave);
        }
    }

    private void EnsureMidiMapSaveDebounceTimer()
    {
        if (_midiMapSaveDebounceTimer is not null)
        {
            return;
        }

        _midiMapSaveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _midiMapSaveDebounceTimer.Tick += (_, _) =>
        {
            _midiMapSaveDebounceTimer!.Stop();
            FlushMidiMapToDisk();
        };
    }

    private void ScheduleMidiMapSave()
    {
        if (string.IsNullOrEmpty(_midiMapPath))
        {
            return;
        }

        EnsureMidiMapSaveDebounceTimer();
        _midiMapSaveDebounceTimer!.Stop();
        _midiMapSaveDebounceTimer.Start();
    }

    private void FlushMidiMapToDisk()
    {
        if (string.IsNullOrEmpty(_midiMapPath))
        {
            return;
        }

        try
        {
            var file = new MidiMapFile { Version = 1, Bindings = Engine.CcMap.Snapshot() };
            MidiMapStore.Save(_midiMapPath, file);
        }
        catch
        {
            // Avoid surfacing IO errors from background debounce; next change will retry.
        }
    }

    private void FlushSettingsToDisk(Window? windowForPlacement)
    {
        if (string.IsNullOrEmpty(_settingsPath))
        {
            return;
        }

        try
        {
            var s = SettingsStore.LoadOrCreate(_settingsPath);
            s.Version = 1;
            s.AsioDriver = string.IsNullOrEmpty(_selectedDriver) || _selectedDriver.StartsWith("(no", StringComparison.Ordinal)
                ? null
                : _selectedDriver;
            s.MidiInput = string.IsNullOrEmpty(_selectedMidi) || _selectedMidi.StartsWith("(no", StringComparison.Ordinal)
                ? null
                : _selectedMidi;
            s.LastPatchPath = string.IsNullOrEmpty(Patches.CurrentFile) ? null : Patches.CurrentFile;
            s.MasterVolume = Patch.Master.Volume;

            if (windowForPlacement is not null)
            {
                s.WindowX = windowForPlacement.Position.X;
                s.WindowY = windowForPlacement.Position.Y;
                s.WindowWidth = (int)Math.Round(windowForPlacement.Width);
                s.WindowHeight = (int)Math.Round(windowForPlacement.Height);
            }

            SettingsStore.Save(_settingsPath, s);
        }
        catch
        {
            // Avoid surfacing IO errors from background debounce; next close will retry.
        }
    }

    private void EnsureProfilerTimer()
    {
        if (_profilerTimer is not null)
        {
            return;
        }

        _profilerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _profilerTimer.Tick += (_, _) => RefreshProfilerDisplay();
    }

    private void RefreshProfilerDisplay()
    {
        if (!_profilerOpen)
        {
            return;
        }

        RefreshProfilerManagedAndProcess();

        var p = Engine.Profiler;
        if (p is null)
        {
            ProfilerMixerBody =
                "Per-block timings require an ASIO driver (the mixer is created when a driver opens).";
            ProfilerBudgetText = "";
            Raise(nameof(ProfilerMixerBody));
            Raise(nameof(ProfilerBudgetText));
            return;
        }

        var budget = Engine.CurrentLatencyMs > 0
            ? $"ASIO buffer ~{Engine.CurrentLatencyMs} ms @ {Engine.SampleRate} Hz — keep TOTAL EMA comfortably below the buffer."
            : "Open a driver to see the nominal block budget.";
        if (!Engine.IsPlaying)
        {
            budget += " Mixer.Read runs only while audio is playing; timings hold at the last EMA until you press START.";
        }

        ProfilerBudgetText = budget;

        ProfilerMixerBody =
            $"SETUP (buf + arp edge)\t{p.EmaUsSetup,9:0.0} µs\n" +
            $"MIDI drain\t{p.EmaUsMidi,9:0.0} µs\n" +
            $"ARP tick\t{p.EmaUsArp,9:0.0} µs\n" +
            $"Voices render\t{p.EmaUsVoices,9:0.0} µs\n" +
            $"FX + mix + levels\t{p.EmaUsFx,9:0.0} µs\n" +
            $"TOTAL (callback)\t{p.EmaUsTotal,9:0.0} µs\n" +
            $"Blocks measured\t{p.Blocks}";

        Raise(nameof(ProfilerMixerBody));
        Raise(nameof(ProfilerBudgetText));
    }

    private void RefreshProfilerManagedAndProcess()
    {
        try
        {
            _diagProcess ??= Process.GetCurrentProcess();
            _diagProcess.Refresh();
            var wsMb = _diagProcess.WorkingSet64 / (1024.0 * 1024.0);

            var wall = Environment.TickCount64;
            if (!_diagCpuPrimed)
            {
                _diagLastCpu = _diagProcess.TotalProcessorTime;
                _diagLastWallMs = wall;
                _diagCpuPrimed = true;
                ProfilerProcessBody = $"Working set: {wsMb:0.#} MB  |  CPU: … (next sample)";
            }
            else
            {
                var wallDelta = wall - _diagLastWallMs;
                _diagLastWallMs = wall;
                if (wallDelta < 1)
                {
                    wallDelta = 1;
                }

                var newCpu = _diagProcess.TotalProcessorTime;
                var cpuDelta = newCpu - _diagLastCpu;
                _diagLastCpu = newCpu;
                var cores = Math.Max(1, Environment.ProcessorCount);
                var pct = 100.0 * cpuDelta.TotalMilliseconds / (wallDelta * cores);
                ProfilerProcessBody = $"Working set: {wsMb:0.#} MB  |  CPU (~app): {pct:0.#} %";
            }
        }
        catch (Exception ex)
        {
            ProfilerProcessBody = $"Process stats unavailable ({ex.Message})";
        }

        try
        {
            var heapMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            var gci = GC.GetGCMemoryInfo();
            var committedMb = gci.TotalCommittedBytes / (1024.0 * 1024.0);
            var g0 = GC.CollectionCount(0);
            var g1 = GC.CollectionCount(1);
            var g2 = GC.CollectionCount(2);
            ProfilerManagedBody =
                $"GetTotalMemory (no full collect): {heapMb:0.##} MB\n" +
                $"GC total committed: {committedMb:0.##} MB\n" +
                $"GC collections gen0 / gen1 / gen2: {g0} / {g1} / {g2}\n" +
                $"ThreadPool threads: {ThreadPool.ThreadCount}\n" +
                $"ThreadPool pending work items: {ThreadPool.PendingWorkItemCount}";
        }
        catch (Exception ex)
        {
            ProfilerManagedBody = $"GC info unavailable ({ex.Message})";
        }

        Raise(nameof(ProfilerManagedBody));
        Raise(nameof(ProfilerProcessBody));
    }
}