using Vocaterra.Game;
using Vocaterra.MonoGame.Utilities;

namespace Vocaterra.MonoGame.Sound;

/// <summary>The decisions made once at start about the sound side (S24), apart from the engine so that they can be tested.</summary>
public static class SoundSetup
{
    /// <summary>
    /// Whether the run plays through the null output: a scripted run (<c>--screenshot</c>) and <c>--no-sounds</c> make no noise and open no audio device, so a headless
    /// machine, the tests and the orchestrator's runs stay silent and repeatable. The sound log still records a scripted run's cues.
    /// </summary>
    /// <param name="options">The command line.</param>
    public static bool UsesNullOutput(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.IsScreenshotRun || options.NoSounds;
    }

    /// <summary>The volumes of the run: the save's (or the defaults), then <c>--sfx-volume</c> and <c>--ambient-volume</c> over them, all limited to 0 to 1.</summary>
    /// <param name="options">The command line.</param>
    /// <param name="saved">The volumes in the loaded save, or <see langword="null"/>.</param>
    public static SoundVolumes VolumesFor(LaunchOptions options, SoundVolumes? saved)
    {
        ArgumentNullException.ThrowIfNull(options);
        var volumes = saved ?? SoundVolumes.Default;
        return (volumes with { Sfx = options.SfxVolume ?? volumes.Sfx, Ambient = options.AmbientVolume ?? volumes.Ambient }).Clamped();
    }
}
