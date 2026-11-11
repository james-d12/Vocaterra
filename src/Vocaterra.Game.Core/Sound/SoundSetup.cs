using Vocaterra.Game.Core.Save;

namespace Vocaterra.Game.Core.Sound;

/// <summary>The decisions made once at start about the sound side (S24), apart from the engine so that they can be tested.</summary>
public static class SoundSetup
{
    /// <summary>
    /// Whether the run plays through the null output: a scripted run (<c>--screenshot</c>) and <c>--no-sounds</c> make no noise and open no audio device, so a headless
    /// machine, the tests and the orchestrator's runs stay silent and repeatable. The sound log still records a scripted run's cues.
    /// </summary>
    /// <param name="options">The command line's sound options.</param>
    public static bool UsesNullOutput(SoundOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.IsScreenshotRun || options.NoSounds;
    }

    /// <summary>The volumes of the run: the save's (or the defaults), then <c>--voice-volume</c>, <c>--sfx-volume</c>, <c>--ui-volume</c>, <c>--ambient-volume</c> and <c>--weather-volume</c> over them, all limited to 0 to 1.</summary>
    /// <param name="options">The command line's sound options.</param>
    /// <param name="saved">The volumes in the loaded save, or <see langword="null"/>.</param>
    public static SoundVolumes VolumesFor(SoundOptions options, SoundVolumes? saved)
    {
        ArgumentNullException.ThrowIfNull(options);
        var volumes = saved ?? SoundVolumes.Default;
        return (volumes with { Voice = options.VoiceVolume ?? volumes.Voice, Sfx = options.SfxVolume ?? volumes.Sfx, Ui = options.UiVolume ?? volumes.Ui, Ambient = options.AmbientVolume ?? volumes.Ambient, Weather = options.WeatherVolume ?? volumes.Weather }).Clamped();
    }
}
