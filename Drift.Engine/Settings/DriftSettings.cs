namespace Drift.Engine.Settings;

// Persisted to Settings/drift.json next to the executable.
public sealed class DriftSettings
{
    public int Version { get; set; } = 1;

    public string? AsioDriver { get; set; }

    public string? MidiInput { get; set; }

    public string? LastPatchPath { get; set; }

    public int? WindowX { get; set; }

    public int? WindowY { get; set; }

    public int? WindowWidth { get; set; }

    public int? WindowHeight { get; set; }

    public float? MasterVolume { get; set; }
}
