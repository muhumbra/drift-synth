namespace Drift.Engine.Synth;

public interface ISynthVoice
{
    int Note { get; }
    bool IsActive { get; }
    bool IsReleasing { get; }
    long StartTimestamp { get; }

    void NoteOn(int note, float velocity);
    void NoteOff();
    void HardReset();
    void GlideTo(int note);
    void GlideFrom(int note);
    void RenderBlock(float[] buffer, int offset, int count);
}
