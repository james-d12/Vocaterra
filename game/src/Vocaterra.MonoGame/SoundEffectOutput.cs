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
    }

    /// <inheritdoc />
    public void SetBeds(IReadOnlyList<BedLevel> levels)
    {
        // S25 plays the beds through this call; S24 has none.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        StopAll();
        foreach (var effect in _effects.Values.Distinct())
        {
            effect.Dispose();
        }

        _effects.Clear();
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
                    _effects[(definition.Cue, 0)] = Make(SoundSynth.SampleRate, SoundSynth.Render(definition.Cue));
                    continue;
                }

                for (var take = 0; take < definition.Files.Count; take++)
                {
                    var wav = WavFile.Parse(File.ReadAllBytes(Path.Combine(_directory, definition.Files[take])));
                    _effects[(definition.Cue, take)] = Make(wav.SampleRate, wav.Samples);
                }
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
