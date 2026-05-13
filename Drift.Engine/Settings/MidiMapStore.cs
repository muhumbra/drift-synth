using System.Text.Json;

namespace Drift.Engine.Settings;

// Persisted to Settings/midimap.json next to the executable.
// Schema: { "version": 1, "bindings": { "<paramId>": <ccNumber>, ... } }
public sealed class MidiMapFile
{
    public int Version { get; set; } = 1;

    public Dictionary<string, byte> Bindings { get; set; } = new(StringComparer.Ordinal);
}

public static class MidiMapStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static MidiMapFile LoadOrCreate(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var f = JsonSerializer.Deserialize<MidiMapFile>(json, JsonOptions);
                if (f is not null)
                {
                    f.Bindings ??= new Dictionary<string, byte>(StringComparer.Ordinal);
                    return f;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable -- start fresh rather than fail to launch.
        }

        return new MidiMapFile();
    }

    public static void Save(string path, MidiMapFile file)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(file, JsonOptions);
        File.WriteAllText(path, json);
    }
}
