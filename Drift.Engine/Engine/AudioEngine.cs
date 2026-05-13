using Drift.Engine.Dsp.Lut;
using Drift.Engine.Midi;
using Drift.Engine.Synth;
using NAudio.Wave;

namespace Drift.Engine.Engine;

// Top-level engine. Holds the patch, voice pool, MIDI input, and ASIO output.
// Building the audio chain is deferred until OpenDriver(), because the Mixer/voices
// must match the ASIO driver's sample rate.
public sealed class AudioEngine : IDisposable
{
    private static readonly int[] PreferredRates = [48000, 44100, 96000];
    private Mixer? _mixer;

    private AsioOut? _output;

    public AudioEngine()
    {
        FastExp2.Exp2(0);
        FastSin.SinFromPhase01(0);
        FastTanU.Tan(0);
        FastTanh.Tanh(0);
        
        Midi = new MidiInputManager(MidiQueue);
    }

    public SynthPatch Patch { get; } = new();
    public VoiceState VoiceState { get; } = new();
    public MidiQueue MidiQueue { get; } = new();
    public MidiInputManager Midi { get; }
    public MidiCcMap CcMap { get; } = new();

    public int SampleRate { get; private set; }
    public string CurrentDriverName { get; private set; } = "(none)";
    public int CurrentLatencyMs { get; private set; }
    public bool IsPlaying { get; private set; }

    public VoicePool? Pool { get; private set; }

    public LevelMonitor? Levels => _mixer?.Levels;

    public MixerProfiler? Profiler => _mixer?.Profiler;

    public void Dispose()
    {
        CloseDriver();
        Midi.Dispose();
    }

    public static IReadOnlyList<string> ListAsioDrivers()
    {
        try
        {
            return AsioOut.GetDriverNames();
        }
        catch
        {
            return [];
        }
    }

    public void OpenDriver(string driverName)
    {
        var wasPlaying = IsPlaying;
        CloseDriver();

        Exception? lastEx = null;
        foreach (var rate in PreferredRates)
        {
            try
            {
                BuildAudioChain(rate);
                var asio = new AsioOut(driverName);
                asio.Init(_mixer!);
                _output = asio;
                CurrentDriverName = driverName;
                SampleRate = rate;
                var frames = asio.FramesPerBuffer;
                CurrentLatencyMs = frames > 0 ? (int)(frames * 1000.0 / rate) : 0;
                if (wasPlaying)
                {
                    Start();
                }

                return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _output?.Dispose();
                _output = null;
            }
        }

        throw new InvalidOperationException(
            $"Could not open ASIO driver '{driverName}' at any supported sample rate (48k/44.1k/96k).",
            lastEx);
    }

    public void CloseDriver()
    {
        if (_output is null)
        {
            return;
        }

        try
        {
            _output.Stop();
        }
        catch
        {
        }

        _output.Dispose();
        _output = null;
        _mixer = null;
        Pool = null;
        CurrentDriverName = "(none)";
        CurrentLatencyMs = 0;
        IsPlaying = false;
    }

    public void Start()
    {
        if (_output is null)
        {
            return;
        }

        _output.Play();
        IsPlaying = true;
    }

    public void Stop()
    {
        if (_output is null)
        {
            return;
        }

        _output.Stop();
        IsPlaying = false;
    }

    public void OpenAsioControlPanel()
    {
        _output?.ShowControlPanel();
    }

    public void Panic()
    {
        Pool?.Panic();
    }

    public void ClearArp()
    {
        _mixer?.Arp.Clear();
    }

    private void BuildAudioChain(int sampleRate)
    {
        VoiceState.BendRangeSemitones = Patch.PitchBendRangeSemitones;
        var voices = new ISynthVoice[Patch.Polyphony];
        for (var i = 0; i < voices.Length; i++)
        {
            voices[i] = new SynthVoice(sampleRate, Patch, VoiceState, i + 1);
        }

        Pool = new VoicePool(voices, VoiceState, Patch);
        _mixer = new Mixer(sampleRate, Patch, Pool, VoiceState, MidiQueue, CcMap);
    }
}