using Microsoft.Xna.Framework.Audio;
using Vocaterra.Game.Sound;

namespace Vocaterra.Game.Tests.Sound;

/// <summary>
/// When the voice's clip has ended (#24, #36): a MonoGame <see cref="DynamicSoundEffectInstance"/> stays <see cref="SoundState.Playing"/> once its buffer has played out,
/// so the end of a clip is the end of its pending buffers. Reading the state alone kept the voice busy for ever: the queued lines of a lesson or of the shopkeeper's key
/// words never started, and the next utterance dropped them. No sound device is needed: the rule is a pure function of the two values.
/// </summary>
public sealed class DynamicSoundOutputTests
{
    [Theory]
    [InlineData(SoundState.Playing, 1, true)]
    [InlineData(SoundState.Playing, 0, false)] // played out: the instance still says Playing (checked against MonoGame 3.8.6's OpenAL instance)
    [InlineData(SoundState.Stopped, 1, false)]
    [InlineData(SoundState.Paused, 1, false)]
    public void AClipPlaysWhileTheInstanceHasABufferLeft(SoundState state, int pending, bool playing) =>
        Assert.Equal(playing, DynamicSoundOutput.ClipPlaying(state, pending));
}
