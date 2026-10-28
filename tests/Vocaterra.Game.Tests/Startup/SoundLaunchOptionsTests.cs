using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The command line's sound flags (S24): parsed or refused, and the start-up's sound choices they make (<c>LaunchOptions.Sound</c>).</summary>
public sealed class SoundLaunchOptionsTests
{
    [Fact]
    public void TheSoundFlagsAreParsedAndRefuseWhatIsNotAVolume()
    {
        Assert.True(LaunchOptions.TryParse(["--sfx-volume", "0.25", "--ambient-volume", "0", "--no-sounds", "--sound-log", "/tmp/x.log"], out var options, out var error), error);
        Assert.Equal(0.25, options.SfxVolume);
        Assert.Equal(0.0, options.AmbientVolume);
        Assert.True(options.NoSounds);
        Assert.Equal("/tmp/x.log", options.SoundLogPath);

        Assert.True(LaunchOptions.TryParse(["--mute"], out var plain, out _));
        Assert.True(plain.Muted); // --mute keeps its meaning: the voices
        Assert.False(plain.NoSounds);
        Assert.Null(plain.SfxVolume);
        Assert.Null(plain.SoundLogPath);

        Assert.False(LaunchOptions.TryParse(["--sfx-volume", "1.5"], out _, out var tooLoud));
        Assert.Contains("--sfx-volume needs a number from 0 to 1", tooLoud, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--ambient-volume", "loud"], out _, out _));
        Assert.False(LaunchOptions.TryParse(["--sound-log", string.Empty], out _, out _));
        Assert.False(LaunchOptions.TryParse(["--sfx-volume"], out _, out _));
        foreach (var flag in new[] { "--sfx-volume", "--ambient-volume", "--no-sounds", "--sound-log" })
        {
            Assert.Contains(flag, LaunchOptions.Usage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AScriptedRunAndNoSoundsUseTheNullOutputAndTheCommandLineOverridesTheSavedVolumes()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png"], out var scripted, out _));
        Assert.True(SoundSetup.UsesNullOutput(scripted.Sound)); // silent, and no audio device is opened
        Assert.True(LaunchOptions.TryParse(["--no-sounds"], out var quiet, out _));
        Assert.True(SoundSetup.UsesNullOutput(quiet.Sound));
        Assert.True(LaunchOptions.TryParse([], out var live, out _));
        Assert.False(SoundSetup.UsesNullOutput(live.Sound));
        Assert.False(new NullSoundOutput().Available);

        var saved = new SoundVolumes(0.5, 0.4, 0.3, 0.2);
        Assert.Equal(saved, SoundSetup.VolumesFor(live.Sound, saved));
        Assert.Equal(SoundVolumes.Default, SoundSetup.VolumesFor(live.Sound, null));
        Assert.True(LaunchOptions.TryParse(["--sfx-volume", "1", "--ambient-volume", "0.9"], out var loud, out _));
        Assert.Equal(new SoundVolumes(0.5, 1.0, 0.3, 0.9), SoundSetup.VolumesFor(loud.Sound, saved));
    }
}
