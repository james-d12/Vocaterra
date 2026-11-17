using System.Globalization;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Sound;

namespace Vocaterra.Game.Startup;

/// <summary>The sound side of a run, as made at its start: the front-end the game plays through, the output it plays to, and the sound log, if any.</summary>
/// <param name="Sound">The front-end: the mixer, the cues and the ambient beds.</param>
/// <param name="Output">The output (a device, or the null output).</param>
/// <param name="Log">The sound log (<c>--sound-log</c>), or <see langword="null"/>.</param>
public sealed record SoundParts(SoundFrontend Sound, ISoundOutput Output, SoundLogWriter? Log);

/// <summary>
/// Makes the sound side of a run (S24, S25): scripted runs and <c>--no-sounds</c> play nothing aloud (the null output, no audio device needed); the
/// sound log still records the cues of a scripted run. The volumes are the save's, then the command line's. The presentation generator is seeded
/// from the world seed and is the sound's alone. The ambient beds listen to the scene's places; a run without the layout (a test) has none of them,
/// only the height and the day.
/// </summary>
public static class SoundBootstrap
{
    /// <summary>The sound side of a run.</summary>
    /// <param name="options">The launch options.</param>
    /// <param name="session">The session (the seed, the lessons' book, the events already in it).</param>
    /// <param name="saved">The volumes a loaded save holds, or <see langword="null"/>.</param>
    /// <param name="layout">The scene's layout, or <see langword="null"/>.</param>
    /// <param name="info">The world's place query (B14), from which the biome's data and the depth under the ground choose the ambient beds, or <see langword="null"/> where the world has none (the valley alone).</param>
    public static SoundParts Create(LaunchOptions options, VoxelSession session, SoundVolumes? saved, SceneLayout? layout, IPlaceInfo? info = null)
    {
        var volumes = SoundSetup.VolumesFor(options.Sound, saved);
        ISoundOutput output = SoundSetup.UsesNullOutput(options.Sound) ? new NullSoundOutput() : new SoundEffectOutput(AssetPaths.Audio, Console.WriteLine);
        var log = options.SoundLogPath is { } path ? SoundLogWriter.Open(path) : null;
        var mixer = new SoundMixer(output, volumes, new PresentationRng(session.WorldSeed), log, options.NoSounds);
        if (log is not null)
        {
            var clamped = mixer.Volumes;
            log.Write($"# vocaterra sound log, format 1: tick, category, cue, take, gain, pitch (marks: voice and recording windows); seed {session.WorldSeed}");
            log.Write(string.Create(CultureInfo.InvariantCulture, $"# volumes: voice {clamped.Voice:0.00} sfx {clamped.Sfx:0.00} ui {clamped.Ui:0.00} ambient {clamped.Ambient:0.00} weather {clamped.Weather:0.00}; sounds {(options.NoSounds ? "off (--no-sounds)" : "on")}; output {(output is NullSoundOutput ? "null (silent)" : "device")}"));
        }

        var places = layout is null ? new AmbientPlaces(null, null, null, [], null, [], []) : AmbientPlaces.From(layout);
        var ambient = new AmbientSound(mixer, places, info);
        var sound = new SoundFrontend(mixer, new SoundEventMapper(session.Lessons.Book), ambient);
        sound.SkipEvents(session.Events.Count);
        return new SoundParts(sound, output, log);
    }
}
