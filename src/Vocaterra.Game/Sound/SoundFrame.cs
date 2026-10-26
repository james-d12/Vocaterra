using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Sound;

/// <summary>Once a frame (S24): the voice's state ducks the other sounds, the game events, the panels and the frame's cues are played, and the duck and the beds move on.</summary>
public sealed class SoundFrame(SoundFrontend sound, NpcVoice voice, VoxelSession session, Func<PanelState> panels)
{
    /// <summary>Plays the frame's sounds and moves the mixer on by <paramref name="seconds"/>.</summary>
    public void Update(double seconds)
    {
        var mixer = sound.Mixer;
        var tick = session.Simulation.Tick;
        mixer.SetTick(tick);
        mixer.SetVoice(voice.Busy);
        voice.SetVolume(mixer.Gain(SoundCategory.Voice));
        sound.ObserveGameEvents(session.Events);
        sound.ObservePanels(panels());
        sound.Flush(tick);
        mixer.Advance(seconds);
    }
}
