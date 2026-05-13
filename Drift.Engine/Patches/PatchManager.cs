using Drift.Engine.Synth;

namespace Drift.Engine.Patches;

public sealed record PatchEntry(string FilePath, string DisplayName);

// File-backed patch library. Lists every .dpatch.json in `Folder`, lets the host
// load / save them, and (when given a factory) seeds the folder on first run.
public sealed class PatchManager
{
    public PatchManager(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(folder);
    }

    public string Folder { get; }
    public string CurrentFile { get; private set; } = "";

    // Writes every patch from `factory` to disk (only the missing ones, so users can
    // edit / delete / add without losing their work on next startup).
    public void EnsureSeeded(IEnumerable<(string fileName, SynthPatch patch)> factory)
    {
        foreach (var (fileName, patch) in factory)
        {
            var path = Path.Combine(Folder, fileName);
            if (!File.Exists(path))
            {
                PatchSerializer.Save(patch, path);
            }
        }
    }

    public IReadOnlyList<PatchEntry> List()
    {
        var result = new List<PatchEntry>();
        foreach (var file in Directory.GetFiles(Folder, "*.dpatch.json")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string display;
            try
            {
                var p = PatchSerializer.Load(file);
                display = string.IsNullOrWhiteSpace(p.Name) ? Path.GetFileNameWithoutExtension(file) : p.Name;
            }
            catch
            {
                display = Path.GetFileNameWithoutExtension(file);
            }

            result.Add(new PatchEntry(file, display));
        }

        return result;
    }

    public void LoadInto(SynthPatch target, string filePath)
    {
        var loaded = PatchSerializer.Load(filePath);
        target.CopyFrom(loaded);
        CurrentFile = filePath;
    }

    public void Save(SynthPatch patch)
    {
        if (string.IsNullOrEmpty(CurrentFile))
        {
            throw new InvalidOperationException("No file currently selected; use SaveAs first.");
        }

        PatchSerializer.Save(patch, CurrentFile);
    }

    public void SaveAs(SynthPatch patch, string filePath)
    {
        PatchSerializer.Save(patch, filePath);
        CurrentFile = filePath;
    }
}