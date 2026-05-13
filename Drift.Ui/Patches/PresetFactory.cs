using Drift.Engine.Dsp;
using Drift.Engine.Synth;

namespace Drift.Ui.Patches;

// 50 hand-tuned starter patches. Each preset is one short Setup<T> lambda; the
// file naming "NN_Category_Name.dpatch.json" keeps them grouped on disk and in
// the dropdown.
public static class PresetFactory
{
    // Preset literals are doubles for readability; engine patch uses float.
    private static float Lf(double v) => (float)v;

    public static IEnumerable<(string fileName, SynthPatch patch)> All()
    {
        var n = 0;
        foreach (var (cat, name, setup) in Definitions())
        {
            n++;
            var p = new SynthPatch();
            setup(p);
            p.Name = $"{cat}: {name}";
            var safe = name.Replace(' ', '-').ToLowerInvariant();
            yield return ($"{n:D2}_{cat}_{safe}.dpatch.json", p);
        }
    }

    private static IEnumerable<(string cat, string name, Action<SynthPatch> setup)> Definitions()
    {
        // -- LEADS --------------------------------------------------------------
        yield return ("Lead", "Drift Lead", p =>
        {
            Saws(p, -3, +5);
            p.Mixer.SubLevel = Lf(0.35);
            Filter(p, 2400, 0.35, 0.55, 0.4);
            Env(p.AmpEnv, 0.005, 0.30, 0.75, 0.55);
            Env(p.FilterEnv, 0.005, 0.45, 0.20, 0.50);
            Lfo(p, LfoShape.Sine, 5.2, 0, LfoTarget.Pitch);
            Master(p, 0.55, 0.28);
        });

        yield return ("Lead", "Synthwave Stab", p =>
        {
            Saws(p, -7, +7);
            p.Mixer.SubLevel = Lf(0.25);
            Filter(p, 1800, 0.55, 0.85, 0.3);
            Env(p.AmpEnv, 0.002, 0.35, 0.30, 0.25);
            Env(p.FilterEnv, 0.001, 0.18, 0.0, 0.20);
            Lfo(p, LfoShape.Sine, 5.5, 0, LfoTarget.Pitch);
            Delay(p, 380, 0.45, 0.28, 0.55);
            Master(p, 0.55, 0.30);
        });

        yield return ("Lead", "Hard Sync Lead", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Saw;
            p.Osc2.Semitone = 7;
            p.Osc2.Level = Lf(0.55);
            Filter(p, 1300, 0.7, 0.9, 0.6);
            Env(p.AmpEnv, 0.005, 0.5, 0.6, 0.4);
            Env(p.FilterEnv, 0.01, 0.6, 0.0, 0.3);
            Lfo(p, LfoShape.Triangle, 6, 0.05, LfoTarget.Cutoff);
            Master(p, 0.5, 0.18);
        });

        yield return ("Lead", "Pluck Lead", p =>
        {
            Saws(p, -2, +3);
            p.Osc2.Octave = 1;
            p.Osc2.Level = Lf(0.5);
            Filter(p, 1100, 0.45, 0.95, 0.5);
            Env(p.AmpEnv, 0.001, 0.18, 0.0, 0.18);
            Env(p.FilterEnv, 0.001, 0.18, 0.0, 0.20);
            Delay(p, 280, 0.5, 0.28, 0.6);
            Master(p, 0.6, 0.32);
        });

        yield return ("Lead", "Mono Acid Lead", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc1.Level = Lf(0.85);
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.Level = Lf(0.0);
            Filter(p, 700, 0.85, 0.85, 0.5);
            Env(p.AmpEnv, 0.001, 0.5, 0.7, 0.2);
            Env(p.FilterEnv, 0.001, 0.25, 0.0, 0.25);
            Voice(p, 0.08, true, true);
            Lfo(p, LfoShape.Sine, 4, 0, LfoTarget.Pitch);
            Master(p, 0.55, 0.18);
        });

        yield return ("Lead", "Bell Lead", p =>
        {
            p.Osc1.Wave = Waveform.Sine;
            p.Osc1.Level = Lf(0.7);
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 7;
            p.Osc2.Level = Lf(0.45);
            p.Mixer.SubLevel = Lf(0.0);
            Filter(p, 6000, 0.1, 0.0, 0.6);
            Env(p.AmpEnv, 0.002, 1.6, 0.0, 1.4);
            Env(p.FilterEnv, 0.005, 1.2, 0.0, 1.0);
            Delay(p, 480, 0.55, 0.35, 0.65);
            Master(p, 0.5, 0.40);
        });

        yield return ("Lead", "Square Lead", p =>
        {
            p.Osc1.Wave = Waveform.Square;
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.FineCents = 6;
            p.Mixer.SubLevel = Lf(0.3);
            Filter(p, 2200, 0.3, 0.55, 0.5);
            Env(p.AmpEnv, 0.005, 0.3, 0.75, 0.4);
            Env(p.FilterEnv, 0.005, 0.5, 0.3, 0.4);
            Lfo(p, LfoShape.Sine, 5.5, 0, LfoTarget.Pitch);
            Master(p, 0.5, 0.22);
        });

        yield return ("Lead", "Sub Lead", p =>
        {
            p.Osc1.Wave = Waveform.Triangle;
            p.Osc2.Wave = Waveform.Triangle;
            p.Osc2.FineCents = 4;
            p.Mixer.SubLevel = Lf(0.55);
            Filter(p, 3500, 0.2, 0.3, 0.4);
            Env(p.AmpEnv, 0.005, 0.4, 0.8, 0.4);
            Env(p.FilterEnv, 0.01, 0.6, 0.4, 0.4);
            Master(p, 0.5, 0.20);
        });

        yield return ("Lead", "Sweep Lead", p =>
        {
            Saws(p, -8, +8);
            Filter(p, 500, 0.6, 0.95, 0.4);
            Env(p.AmpEnv, 0.01, 0.5, 0.9, 0.6);
            Env(p.FilterEnv, 0.5, 1.5, 0.4, 0.8);
            Lfo(p, LfoShape.Sine, 4, 0, LfoTarget.Pitch);
            Delay(p, 410, 0.5, 0.32, 0.5);
            Master(p, 0.5, 0.40);
        });

        yield return ("Lead", "Sustain Lead", p =>
        {
            Saws(p, -4, +6);
            p.Mixer.SubLevel = Lf(0.30);
            Filter(p, 3200, 0.30, 0.40, 0.45);
            Env(p.AmpEnv, 0.05, 0.3, 0.85, 0.5);
            Env(p.FilterEnv, 0.05, 0.6, 0.5, 0.5);
            Lfo(p, LfoShape.Sine, 5.0, 0.0, LfoTarget.Pitch);
            Master(p, 0.55, 0.30);
        });

        // -- BASSES -------------------------------------------------------------
        yield return ("Bass", "Sub Bass", p =>
        {
            p.Osc1.Wave = Waveform.Sine;
            p.Osc1.Level = Lf(0.85);
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Level = Lf(0.0);
            p.Mixer.SubLevel = Lf(0.5);
            Filter(p, 800, 0.0, 0.0, 0.0);
            Env(p.AmpEnv, 0.005, 0.3, 0.95, 0.2);
            Env(p.FilterEnv, 0.005, 0.2, 0.0, 0.2);
            Voice(p, 0.05, true, true);
            Master(p, 0.55, 0.05);
        });

        yield return ("Bass", "Saw Bass", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Saw;
            p.Osc2.FineCents = 3;
            p.Mixer.SubLevel = Lf(0.4);
            Filter(p, 1200, 0.35, 0.6, 0.4);
            Env(p.AmpEnv, 0.001, 0.3, 0.85, 0.18);
            Env(p.FilterEnv, 0.001, 0.25, 0.2, 0.2);
            Voice(p, 0.0, true, true);
            Master(p, 0.55, 0.10);
        });

        yield return ("Bass", "Square Bass", p =>
        {
            p.Osc1.Wave = Waveform.Square;
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.FineCents = -7;
            p.Mixer.SubLevel = Lf(0.45);
            Filter(p, 1100, 0.3, 0.55, 0.45);
            Env(p.AmpEnv, 0.002, 0.25, 0.85, 0.2);
            Env(p.FilterEnv, 0.001, 0.18, 0.2, 0.2);
            Voice(p, 0.0, true, true);
            Master(p, 0.55, 0.10);
        });

        yield return ("Bass", "Reese Bass", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc1.FineCents = -10;
            p.Osc2.Wave = Waveform.Saw;
            p.Osc2.FineCents = +10;
            p.Mixer.SubLevel = Lf(0.5);
            Filter(p, 700, 0.5, 0.4, 0.3);
            Env(p.AmpEnv, 0.005, 0.3, 0.9, 0.25);
            Env(p.FilterEnv, 0.005, 0.4, 0.4, 0.3);
            Lfo(p, LfoShape.Sine, 1.2, 0.15, LfoTarget.Cutoff);
            Voice(p, 0.0, true, true);
            Master(p, 0.55, 0.08);
        });

        yield return ("Bass", "Mono Pluck Bass", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.Level = Lf(0.4);
            p.Osc2.Octave = 1;
            p.Mixer.SubLevel = Lf(0.5);
            Filter(p, 600, 0.5, 0.95, 0.5);
            Env(p.AmpEnv, 0.001, 0.18, 0.0, 0.12);
            Env(p.FilterEnv, 0.001, 0.18, 0.0, 0.15);
            Voice(p, 0.04, true, false);
            Master(p, 0.55, 0.10);
        });

        yield return ("Bass", "Wobble Bass", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.FineCents = -5;
            p.Mixer.SubLevel = Lf(0.45);
            Filter(p, 500, 0.7, 0.7, 0.3);
            Env(p.AmpEnv, 0.005, 0.3, 0.9, 0.3);
            Env(p.FilterEnv, 0.005, 0.4, 0.5, 0.3);
            Lfo(p, LfoShape.Triangle, 2.5, 0.7, LfoTarget.Cutoff);
            Voice(p, 0.0, true, true);
            Master(p, 0.55, 0.10);
        });

        yield return ("Bass", "Glitch Bass", p =>
        {
            p.Osc1.Wave = Waveform.Square;
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.Octave = 1;
            p.Osc2.FineCents = -7;
            p.Mixer.NoiseLevel = Lf(0.05);
            Filter(p, 900, 0.6, 0.7, 0.4);
            Env(p.AmpEnv, 0.001, 0.2, 0.5, 0.15);
            Env(p.FilterEnv, 0.001, 0.18, 0.0, 0.2);
            Lfo(p, LfoShape.SampleHold, 14, 0.4, LfoTarget.Cutoff);
            Voice(p, 0.0, true, true);
            Master(p, 0.5, 0.10);
        });

        yield return ("Bass", "Acid Bass", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc1.Level = Lf(0.9);
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.Level = Lf(0.0);
            Filter(p, 500, 0.9, 0.95, 0.6);
            Env(p.AmpEnv, 0.001, 0.5, 0.6, 0.15);
            Env(p.FilterEnv, 0.001, 0.22, 0.0, 0.22);
            Voice(p, 0.06, true, true);
            Master(p, 0.5, 0.12);
        });

        yield return ("Bass", "Big Bass", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Saw;
            p.Osc2.FineCents = 8;
            p.Mixer.SubLevel = Lf(0.7);
            Filter(p, 1400, 0.25, 0.5, 0.4);
            Env(p.AmpEnv, 0.005, 0.4, 0.9, 0.3);
            Env(p.FilterEnv, 0.005, 0.4, 0.5, 0.3);
            Voice(p, 0.0, false, false);
            Master(p, 0.55, 0.12);
        });

        yield return ("Bass", "Tight Bass", p =>
        {
            p.Osc1.Wave = Waveform.Square;
            p.Osc1.Level = Lf(0.7);
            p.Mixer.SubLevel = Lf(0.55);
            Filter(p, 1500, 0.2, 0.4, 0.5);
            Env(p.AmpEnv, 0.001, 0.16, 0.0, 0.10);
            Env(p.FilterEnv, 0.001, 0.16, 0.0, 0.12);
            Voice(p, 0.0, true, false);
            Master(p, 0.55, 0.05);
        });

        // -- PADS ---------------------------------------------------------------
        yield return ("Pad", "Warm Pad", p =>
        {
            Saws(p, -12, +12);
            p.Mixer.SubLevel = Lf(0.25);
            Filter(p, 1200, 0.2, 0.5, 0.5);
            Env(p.AmpEnv, 1.2, 1.0, 0.85, 1.5);
            Env(p.FilterEnv, 1.5, 1.5, 0.6, 1.5);
            Lfo(p, LfoShape.Sine, 0.3, 0.05, LfoTarget.Cutoff);
            Delay(p, 520, 0.4, 0.20, 0.6);
            Master(p, 0.45, 0.55);
        });

        yield return ("Pad", "String Pad", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Saw;
            p.Osc2.FineCents = 7;
            Filter(p, 2200, 0.15, 0.4, 0.6);
            Env(p.AmpEnv, 0.6, 0.8, 0.85, 1.0);
            Env(p.FilterEnv, 0.8, 1.2, 0.5, 1.0);
            Lfo(p, LfoShape.Sine, 5.5, 0.05, LfoTarget.Pitch);
            Master(p, 0.45, 0.45);
        });

        yield return ("Pad", "Glass Pad", p =>
        {
            p.Osc1.Wave = Waveform.Triangle;
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 7;
            p.Osc2.Level = Lf(0.5);
            Filter(p, 5000, 0.15, 0.0, 0.5);
            Env(p.AmpEnv, 1.5, 1.2, 0.8, 2.0);
            Env(p.FilterEnv, 2.0, 1.5, 0.6, 1.5);
            Delay(p, 600, 0.5, 0.25, 0.7);
            Master(p, 0.45, 0.55);
        });

        yield return ("Pad", "Choir-ish Pad", p =>
        {
            Saws(p, -10, +10);
            p.Mixer.NoiseLevel = Lf(0.03);
            Filter(p, 1400, 0.25, 0.3, 0.5);
            Env(p.AmpEnv, 1.5, 1.5, 0.9, 2.0);
            Env(p.FilterEnv, 1.5, 1.5, 0.7, 1.5);
            Lfo(p, LfoShape.Sine, 4.5, 0.04, LfoTarget.Pitch);
            Master(p, 0.45, 0.55);
        });

        yield return ("Pad", "Sweep Pad", p =>
        {
            Saws(p, -8, +8);
            Filter(p, 400, 0.5, 0.95, 0.4);
            Env(p.AmpEnv, 1.0, 0.5, 0.85, 1.5);
            Env(p.FilterEnv, 3.0, 2.0, 0.5, 2.0);
            Master(p, 0.45, 0.50);
        });

        yield return ("Pad", "Sci-Fi Pad", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.FineCents = -15;
            p.Mixer.NoiseLevel = Lf(0.05);
            Filter(p, 1800, 0.4, 0.4, 0.4);
            Env(p.AmpEnv, 0.8, 1.5, 0.8, 2.0);
            Env(p.FilterEnv, 1.5, 2.0, 0.5, 2.0);
            Lfo(p, LfoShape.Triangle, 0.3, 0.3, LfoTarget.Cutoff);
            Delay(p, 700, 0.55, 0.30, 0.5);
            Master(p, 0.45, 0.60);
        });

        yield return ("Pad", "Vintage Pad", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Triangle;
            p.Osc2.Semitone = 5;
            Filter(p, 1600, 0.20, 0.35, 0.45);
            Env(p.AmpEnv, 0.8, 1.0, 0.85, 1.2);
            Env(p.FilterEnv, 1.0, 1.2, 0.6, 1.2);
            Lfo(p, LfoShape.Sine, 6.0, 0.06, LfoTarget.Pitch);
            Master(p, 0.45, 0.50);
        });

        yield return ("Pad", "Cinematic Pad", p =>
        {
            Saws(p, -15, +15);
            p.Mixer.SubLevel = Lf(0.2);
            p.Mixer.NoiseLevel = Lf(0.04);
            Filter(p, 900, 0.3, 0.6, 0.5);
            Env(p.AmpEnv, 2.0, 1.5, 0.9, 3.0);
            Env(p.FilterEnv, 3.0, 2.0, 0.6, 2.5);
            Lfo(p, LfoShape.Sine, 0.18, 0.15, LfoTarget.Cutoff);
            Delay(p, 720, 0.55, 0.28, 0.55);
            Master(p, 0.45, 0.65);
        });

        yield return ("Pad", "Drone Pad", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Saw;
            p.Osc2.Octave = -1;
            p.Osc2.FineCents = 8;
            p.Mixer.SubLevel = Lf(0.55);
            Filter(p, 700, 0.45, 0.0, 0.3);
            Env(p.AmpEnv, 2.5, 1.0, 0.95, 3.5);
            Env(p.FilterEnv, 1.0, 1.0, 0.7, 1.5);
            Lfo(p, LfoShape.Sine, 0.12, 0.2, LfoTarget.Cutoff);
            Master(p, 0.45, 0.65);
        });

        yield return ("Pad", "Movie Pad", p =>
        {
            Saws(p, -6, +6);
            p.Osc2.Octave = 1;
            p.Osc2.Level = Lf(0.45);
            Filter(p, 2200, 0.20, 0.4, 0.5);
            Env(p.AmpEnv, 1.2, 1.5, 0.85, 2.0);
            Env(p.FilterEnv, 2.0, 1.8, 0.6, 1.8);
            Delay(p, 600, 0.55, 0.32, 0.55);
            Master(p, 0.45, 0.60);
        });

        // -- PLUCKS / KEYS ------------------------------------------------------
        yield return ("Pluck", "Marimba Pluck", p =>
        {
            p.Osc1.Wave = Waveform.Sine;
            p.Osc1.Level = Lf(0.85);
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 12;
            p.Osc2.Level = Lf(0.3);
            Filter(p, 4000, 0.2, 0.0, 0.6);
            Env(p.AmpEnv, 0.001, 0.35, 0.0, 0.35);
            Env(p.FilterEnv, 0.001, 0.25, 0.0, 0.3);
            Master(p, 0.55, 0.30);
        });

        yield return ("Pluck", "Glass Pluck", p =>
        {
            p.Osc1.Wave = Waveform.Triangle;
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 19;
            p.Osc2.Level = Lf(0.4);
            Filter(p, 5000, 0.3, 0.0, 0.5);
            Env(p.AmpEnv, 0.001, 0.5, 0.0, 0.6);
            Env(p.FilterEnv, 0.001, 0.4, 0.0, 0.5);
            Delay(p, 320, 0.45, 0.30, 0.7);
            Master(p, 0.5, 0.40);
        });

        yield return ("Pluck", "Bell Pluck", p =>
        {
            p.Osc1.Wave = Waveform.Sine;
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 7;
            p.Osc2.Level = Lf(0.45);
            Filter(p, 6000, 0.0, 0.0, 0.5);
            Env(p.AmpEnv, 0.001, 1.2, 0.0, 1.2);
            Env(p.FilterEnv, 0.001, 0.8, 0.0, 0.8);
            Delay(p, 380, 0.55, 0.32, 0.7);
            Master(p, 0.5, 0.45);
        });

        yield return ("Key", "EP Keys", p =>
        {
            p.Osc1.Wave = Waveform.Sine;
            p.Osc1.Level = Lf(0.85);
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 19;
            p.Osc2.Level = Lf(0.18);
            Filter(p, 3500, 0.1, 0.4, 0.5);
            Env(p.AmpEnv, 0.005, 0.5, 0.4, 0.6);
            Env(p.FilterEnv, 0.005, 0.6, 0.0, 0.5);
            Master(p, 0.5, 0.30);
        });

        yield return ("Key", "Soft Keys", p =>
        {
            p.Osc1.Wave = Waveform.Triangle;
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.FineCents = 4;
            p.Mixer.SubLevel = Lf(0.2);
            Filter(p, 2800, 0.15, 0.3, 0.5);
            Env(p.AmpEnv, 0.005, 0.6, 0.5, 0.5);
            Env(p.FilterEnv, 0.005, 0.5, 0.2, 0.5);
            Master(p, 0.5, 0.35);
        });

        yield return ("Key", "Toy Piano", p =>
        {
            p.Osc1.Wave = Waveform.Sine;
            p.Osc2.Wave = Waveform.Triangle;
            p.Osc2.Semitone = 12;
            p.Osc2.Level = Lf(0.3);
            p.Mixer.NoiseLevel = Lf(0.03);
            Filter(p, 4000, 0.2, 0.5, 0.6);
            Env(p.AmpEnv, 0.001, 0.4, 0.0, 0.4);
            Env(p.FilterEnv, 0.001, 0.3, 0.0, 0.3);
            Master(p, 0.5, 0.30);
        });

        yield return ("Key", "Synth Organ", p =>
        {
            p.Osc1.Wave = Waveform.Sine;
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 12;
            p.Osc2.Level = Lf(0.5);
            p.Mixer.SubLevel = Lf(0.3);
            Filter(p, 4500, 0.0, 0.0, 0.5);
            Env(p.AmpEnv, 0.005, 0.05, 0.95, 0.1);
            Env(p.FilterEnv, 0.005, 0.05, 0.5, 0.1);
            Lfo(p, LfoShape.Sine, 6.5, 0.04, LfoTarget.Pitch);
            Master(p, 0.5, 0.25);
        });

        yield return ("Key", "FM Bell", p =>
        {
            p.Osc1.Wave = Waveform.Sine;
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 14;
            p.Osc2.Level = Lf(0.55);
            Filter(p, 6000, 0.0, 0.0, 0.5);
            Env(p.AmpEnv, 0.001, 1.5, 0.0, 1.4);
            Env(p.FilterEnv, 0.001, 1.0, 0.0, 1.0);
            Delay(p, 450, 0.5, 0.30, 0.65);
            Master(p, 0.5, 0.40);
        });

        yield return ("Pluck", "Crystal Pluck", p =>
        {
            p.Osc1.Wave = Waveform.Triangle;
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 24;
            p.Osc2.Level = Lf(0.35);
            Filter(p, 5500, 0.1, 0.3, 0.6);
            Env(p.AmpEnv, 0.001, 0.7, 0.0, 0.7);
            Env(p.FilterEnv, 0.001, 0.5, 0.0, 0.5);
            Delay(p, 240, 0.5, 0.30, 0.75);
            Master(p, 0.5, 0.40);
        });

        yield return ("Pluck", "Wood Pluck", p =>
        {
            p.Osc1.Wave = Waveform.Triangle;
            p.Osc1.Level = Lf(0.85);
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.Octave = 1;
            p.Osc2.Level = Lf(0.15);
            p.Mixer.NoiseLevel = Lf(0.06);
            Filter(p, 2500, 0.3, 0.6, 0.5);
            Env(p.AmpEnv, 0.001, 0.25, 0.0, 0.25);
            Env(p.FilterEnv, 0.001, 0.18, 0.0, 0.2);
            Master(p, 0.5, 0.25);
        });

        // -- FX / EXPERIMENTAL --------------------------------------------------
        yield return ("FX", "Sweep Up", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc1.Level = Lf(0.0);
            p.Mixer.NoiseLevel = Lf(0.7);
            Filter(p, 200, 0.4, 1.0, 0.0);
            Env(p.AmpEnv, 1.5, 0.5, 0.7, 0.8);
            Env(p.FilterEnv, 4.0, 0.3, 0.7, 0.5);
            Master(p, 0.5, 0.45);
        });

        yield return ("FX", "Sweep Down", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc1.Level = Lf(0.0);
            p.Mixer.NoiseLevel = Lf(0.7);
            Filter(p, 8000, 0.5, -1.0, 0.0);
            Env(p.AmpEnv, 0.5, 0.5, 0.7, 0.8);
            Env(p.FilterEnv, 0.05, 4.0, 0.0, 1.0);
            Master(p, 0.5, 0.45);
        });

        yield return ("FX", "Pulse FX", p =>
        {
            p.Osc1.Wave = Waveform.Square;
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.FineCents = -3;
            Filter(p, 1500, 0.5, 0.4, 0.4);
            Env(p.AmpEnv, 0.005, 0.5, 0.7, 0.4);
            Env(p.FilterEnv, 0.005, 0.5, 0.3, 0.4);
            Lfo(p, LfoShape.Square, 4.5, 0.6, LfoTarget.Amp);
            Delay(p, 320, 0.5, 0.35, 0.5);
            Master(p, 0.5, 0.30);
        });

        yield return ("FX", "Whoosh", p =>
        {
            p.Osc1.Level = Lf(0.0);
            p.Osc2.Level = Lf(0.0);
            p.Mixer.NoiseLevel = Lf(0.9);
            Filter(p, 600, 0.6, 1.0, 0.0);
            Env(p.AmpEnv, 0.5, 0.6, 0.0, 0.5);
            Env(p.FilterEnv, 0.6, 0.6, 0.0, 0.6);
            Master(p, 0.4, 0.50);
        });

        yield return ("FX", "Drone", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc2.Wave = Waveform.Saw;
            p.Osc2.FineCents = 12;
            p.Osc2.Octave = -1;
            p.Mixer.SubLevel = Lf(0.55);
            p.Mixer.NoiseLevel = Lf(0.05);
            Filter(p, 600, 0.6, 0.0, 0.0);
            Env(p.AmpEnv, 3.0, 1.5, 0.95, 4.0);
            Env(p.FilterEnv, 1.5, 1.5, 0.7, 2.0);
            Lfo(p, LfoShape.Sine, 0.08, 0.4, LfoTarget.Cutoff);
            Master(p, 0.4, 0.65);
        });

        yield return ("FX", "Vinyl Hiss", p =>
        {
            p.Osc1.Level = Lf(0.0);
            p.Osc2.Level = Lf(0.0);
            p.Mixer.NoiseLevel = Lf(0.5);
            Filter(p, 4500, 0.0, 0.0, 0.0);
            Env(p.AmpEnv, 0.05, 0.5, 0.6, 0.5);
            Master(p, 0.3, 0.30);
        });

        yield return ("FX", "Glitch Stab", p =>
        {
            p.Osc1.Wave = Waveform.Square;
            p.Osc2.Wave = Waveform.Saw;
            p.Osc2.Semitone = 3;
            Filter(p, 1500, 0.6, 0.7, 0.4);
            Env(p.AmpEnv, 0.001, 0.18, 0.0, 0.15);
            Env(p.FilterEnv, 0.001, 0.18, 0.0, 0.18);
            Lfo(p, LfoShape.SampleHold, 18, 0.6, LfoTarget.Pitch);
            Delay(p, 220, 0.55, 0.40, 0.6);
            Master(p, 0.5, 0.30);
        });

        yield return ("FX", "Robot Voice", p =>
        {
            p.Osc1.Wave = Waveform.Square;
            p.Osc2.Wave = Waveform.Square;
            p.Osc2.FineCents = -7;
            p.Mixer.SubLevel = Lf(0.3);
            Filter(p, 1300, 0.7, 0.4, 0.4);
            Env(p.AmpEnv, 0.01, 0.4, 0.7, 0.3);
            Env(p.FilterEnv, 0.01, 0.4, 0.3, 0.3);
            Lfo(p, LfoShape.Square, 8, 0.5, LfoTarget.Pitch);
            Master(p, 0.5, 0.25);
        });

        yield return ("FX", "UFO", p =>
        {
            p.Osc1.Wave = Waveform.Sine;
            p.Osc2.Wave = Waveform.Sine;
            p.Osc2.Semitone = 7;
            Filter(p, 4000, 0.4, 0.0, 0.4);
            Env(p.AmpEnv, 0.5, 0.5, 0.85, 1.0);
            Env(p.FilterEnv, 0.5, 0.5, 0.5, 1.0);
            Lfo(p, LfoShape.Sine, 6.5, 0.6, LfoTarget.Pitch);
            Delay(p, 560, 0.6, 0.30, 0.5);
            Master(p, 0.5, 0.55);
        });

        yield return ("FX", "Static Wash", p =>
        {
            p.Osc1.Wave = Waveform.Saw;
            p.Osc1.Level = Lf(0.2);
            p.Osc2.Wave = Waveform.Saw;
            p.Osc2.Level = Lf(0.2);
            p.Osc2.FineCents = 17;
            p.Mixer.NoiseLevel = Lf(0.4);
            Filter(p, 1800, 0.4, 0.3, 0.4);
            Env(p.AmpEnv, 1.5, 1.5, 0.85, 2.0);
            Env(p.FilterEnv, 1.5, 1.5, 0.6, 1.5);
            Lfo(p, LfoShape.Triangle, 0.4, 0.4, LfoTarget.Cutoff);
            Delay(p, 650, 0.55, 0.32, 0.5);
            Master(p, 0.4, 0.65);
        });
    }

    // -- helpers ----------------------------------------------------------------

    private static void Saws(SynthPatch p, double fine1, double fine2)
    {
        p.Osc1.Wave = Waveform.Saw;
        p.Osc1.FineCents = (float)fine1;
        p.Osc1.Level = 0.7f;
        p.Osc2.Wave = Waveform.Saw;
        p.Osc2.FineCents = (float)fine2;
        p.Osc2.Level = 0.55f;
    }

    private static void Filter(SynthPatch p, double cutoff, double res, double envAmt, double track)
    {
        p.Filter.Cutoff = (float)cutoff;
        p.Filter.Resonance = (float)res;
        p.Filter.EnvAmount = (float)envAmt;
        p.Filter.KeyTrack = (float)track;
    }

    private static void Env(EnvelopeParams e, double a, double d, double s, double r)
    {
        e.Attack = (float)a;
        e.Decay = (float)d;
        e.Sustain = (float)s;
        e.Release = (float)r;
    }

    private static void Lfo(SynthPatch p, LfoShape shape, double rate, double amount, LfoTarget target)
    {
        p.Lfo.Shape = shape;
        p.Lfo.Rate = (float)rate;
        p.Lfo.Amount = (float)amount;
        p.Lfo.Target = target;
    }

    private static void Voice(SynthPatch p, double glide, bool mono, bool legato)
    {
        p.Voice.Glide = (float)glide;
        p.Voice.Mono = mono;
        p.Voice.MonoLegato = legato;
    }

    private static void Delay(SynthPatch p, double timeMs, double feedback, double mix, double tone)
    {
        p.Delay.TimeMs = (float)timeMs;
        p.Delay.Feedback = (float)feedback;
        p.Delay.Mix = (float)mix;
        p.Delay.Tone = (float)tone;
    }

    private static void Master(SynthPatch p, double vol, double rev)
    {
        p.Master.Volume = (float)vol;
        p.Master.ReverbMix = (float)rev;
    }
}