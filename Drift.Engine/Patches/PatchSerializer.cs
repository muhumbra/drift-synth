using System.Text.Json;
using System.Text.Json.Serialization;
using Drift.Engine.Synth;

namespace Drift.Engine.Patches;

public static class PatchSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string ToJson(SynthPatch patch)
    {
        return JsonSerializer.Serialize(patch, Options);
    }

    public static SynthPatch FromJson(string json)
    {
        return JsonSerializer.Deserialize<SynthPatch>(json, Options)
               ?? throw new InvalidOperationException("Patch JSON deserialised to null.");
    }

    public static void Save(SynthPatch patch, string path)
    {
        File.WriteAllText(path, ToJson(patch));
    }

    public static SynthPatch Load(string path)
    {
        return FromJson(File.ReadAllText(path));
    }
}