namespace Drift.Engine.Engine;

// Cross-thread signal monitor. Single 32-bit IEEE 754 reads/writes are atomic
// on .NET, so the audio thread can publish each block's measurements with no
// locks and the UI poll at ~30 Hz will get a coherent (possibly slightly stale
// per field, never torn) snapshot.
//
// Peak / RMS are sampled BEFORE the soft clipper, so the meter shows when the
// signal is being driven into saturation -- you'll see the bar in the red
// before tanh() rounds it off.
public sealed class LevelMonitor
{
    public int ActiveVoices;
    public float PeakL;
    public float PeakR;
    public int PolyphonyMax;
    public float RmsL;
    public float RmsR;

    public void Reset()
    {
        PeakL = 0;
        PeakR = 0;
        RmsL = 0;
        RmsR = 0;
        ActiveVoices = 0;
    }
}