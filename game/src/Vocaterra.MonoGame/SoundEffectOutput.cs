using System.Buffers.Binary;
using Microsoft.Xna.Framework.Audio;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The one-shot sounds through the engine (S24): every file of the catalogue (16-bit PCM WAV under <c>assets/audio</c>) and every synthesized cue is turned into a
/// <see cref="SoundEffect"/> on the first sound, and each play is one <see cref="SoundEffectInstance"/> with its gain and pitch. With no audio device, or with a file that
/// cannot be read, the output stays unavailable for the run and the game goes on silent: nothing else depends on it.
/// </summary>
public sealed class SoundEffectOutput : ISoundOutput
{
    private const int MaxLive = 24;

    private readonly string _directory;
    private readonly Action<string> _log;
    private readonly Dictionary<(SoundCue Cue, int Take), SoundEffect> _effects = [];
    private readonly Dictionary<string, SoundEffect> _loops = [];
    private readonly Dictionary<string, SoundEffectInstance> _beds = [];
    private readonly List<SoundEffectInstance> _live = [];
    private bool _loaded;
    private bool _unavailable;

    /// <summary>Creates the output over the audio folder; nothing is read until the first sound.</summary>
    /// <param name="directory">The folder with the catalogue's files.</param>
    /// <param name="log">Where a failure is reported (once).</param>
    public SoundEffectOutput(string directory, Action<string> log)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public bool Available => !_unavailable;

    /// <inheritdoc />
    public void Play(SoundRequest request)
    {
        if (_unavailable || !EnsureLoaded() || !_effects.TryGetValue((request.Cue, request.Take), out var effect))
        {
            return;
        }

        try
        {
            Prune();
            var instance = effect.CreateInstance();
            instance.Volume = (float)Math.Clamp(request.Gain, 0, 1);
            instance.Pitch = (float)Math.Clamp(request.Pitch, -1, 1);
            instance.Pan = (float)Math.Clamp(request.Pan, -1, 1);
            instance.Play();
            _live.Add(instance);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Fail($"sounds: off, {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void StopAll()
    {
        foreach (var instance in _live)
        {
            instance.Stop();
            instance.Dispose();
        }

        _live.Clear();
        foreach (var bed in _beds.Values)
        {
            bed.Volume = 0; // the microphone rule: the beds go silent too (they keep playing, so they resume without a click)
        }
    }

    /// <inheritdoc />
    public void SetBeds(IReadOnlyList<BedLevel> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (_unavailable || !EnsureLoaded())
        {
            return;
        }

        try
        {
            foreach (var bed in _beds.Keys.ToList())
            {
                if (!levels.Any(level => level.Bed == bed))
                {
                    _beds[bed].Volume = 0;
                }
            }

            foreach (var level in levels)
            {
                if (!_loops.TryGetValue(level.Bed, out var loop))
                {
                    continue; // a scheduled bed (birds, fire) has no loop: its sounds are one-shots
                }

                if (!_beds.TryGetValue(level.Bed, out var instance))
                {
                    instance = loop.CreateInstance();
                    instance.IsLooped = true;
                    instance.Volume = 0;
                    instance.Play();
                    _beds[level.Bed] = instance;
                }

                instance.Volume = (float)Math.Clamp(level.Level, 0, 1);
                instance.Pan = (float)Math.Clamp(level.Pan, -1, 1);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Fail($"sounds: off, {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        StopAll();
        foreach (var bed in _beds.Values)
        {
            bed.Dispose();
        }

        _beds.Clear();
        foreach (var effect in _effects.Values.Distinct())
        {
            effect.Dispose();
        }

        _effects.Clear();
        foreach (var loop in _loops.Values.Distinct())
        {
            loop.Dispose();
        }

        _loops.Clear();
    }

    private bool EnsureLoaded()
    {
        if (_loaded)
        {
            return true;
        }

        try
        {
            foreach (var definition in SoundCatalog.All)
            {
                if (SoundSynth.IsSynthesized(definition.Cue))
                {
                    for (var take = 0; take < definition.Takes; take++)
                    {
                        _effects[(definition.Cue, take)] = Make(SoundSynth.SampleRate, SoundSynth.Render(definition.Cue, take));
                    }

                    continue;
                }

                for (var take = 0; take < definition.Files.Count; take++)
                {
                    var wav = WavFile.Parse(File.ReadAllBytes(Path.Combine(_directory, definition.Files[take])));
                    _effects[(definition.Cue, take)] = Make(wav.SampleRate, wav.Samples);
                }
            }

            foreach (var bed in AmbientLoops.Looping)
            {
                _loops[bed] = Make(SoundSynth.SampleRate, AmbientLoops.Render(bed));
            }

            _loaded = true;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Several types: no audio device, the audio library missing, a file gone or damaged. None stops the game.
            Fail($"sounds: off, {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static SoundEffect Make(int sampleRate, short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2 * i), samples[i]);
        }

        return new SoundEffect(bytes, sampleRate, AudioChannels.Mono);
    }

    private void Prune()
    {
        for (var i = _live.Count - 1; i >= 0; i--)
        {
            if (_live[i].State == SoundState.Stopped)
            {
                _live[i].Dispose();
                _live.RemoveAt(i);
            }
        }

        while (_live.Count >= MaxLive)
        {
            _live[0].Stop();
            _live[0].Dispose();
            _live.RemoveAt(0);
        }
    }

    private void Fail(string message)
    {
        if (!_unavailable)
        {
            _unavailable = true;
            _log(message);
        }
    }
}
