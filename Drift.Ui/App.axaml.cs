using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Drift.Engine.Engine;
using Drift.Engine.Patches;
using Drift.Engine.Settings;
using Drift.Ui.Controls;
using Drift.Ui.Patches;
using Drift.Ui.ViewModels;

namespace Drift.Ui;

public class App : Application
{
    public AudioEngine? Engine { get; private set; }
    public PatchManager? PatchMgr { get; private set; }
    public MainViewModel? ViewModel { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Engine = new AudioEngine();

        // Presets live next to the executable so they can be edited and shared.
        var baseDir = AppContext.BaseDirectory;
        var presetDir = Path.Combine(baseDir, "Presets");
        PatchMgr = new PatchManager(presetDir);
        PatchMgr.EnsureSeeded(PresetFactory.All());

        var settingsPath = Path.Combine(baseDir, "Settings", "drift.json");
        var settings = SettingsStore.LoadOrCreate(settingsPath);

        // MIDI CC map is per-machine (lives in Settings/midimap.json) and survives
        // patch changes. Load before knobs attach so pending bindings install on first
        // RegisterParam.
        var midiMapPath = Path.Combine(baseDir, "Settings", "midimap.json");
        var midiMapFile = MidiMapStore.LoadOrCreate(midiMapPath);
        Engine.CcMap.LoadPending(midiMapFile.Bindings);

        // Static handles that knobs and MidiBindRow read at attach time.
        MidiCcRegistry.Map = Engine.CcMap;
        MidiCcRegistry.Patch = Engine.Patch;

        var first = PatchMgr.List().FirstOrDefault();

        ViewModel = new MainViewModel(Engine, PatchMgr, settingsPath, midiMapPath);
        ViewModel.ApplyStartupSettings(settings, first);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow { DataContext = ViewModel };
            MainViewModel.ApplyWindowPlacement(window, settings);
            window.Closing += (_, _) => ViewModel?.PersistSettingsImmediate(window);
            window.Closed += (_, _) => Engine.Dispose();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}