using System.Text.Json;

namespace Drift.Engine.Settings;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static DriftSettings LoadOrCreate(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var s = JsonSerializer.Deserialize<DriftSettings>(json, JsonOptions);
                if (s is not null)
                {
                    return s;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable — start fresh.
        }

        return new DriftSettings();
    }

    public static void Save(string path, DriftSettings settings)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }
}
