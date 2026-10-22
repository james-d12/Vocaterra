using Vocaterra.Game;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The sound the NPC voices play through: one clip at a time. The MonoGame implementation is <c>DynamicSoundOutput</c>; the tests use a fake.
/// </summary>
public interface IAudioOutput : IDisposable
{
    /// <summary>Whether a sound device takes clips at all. False after a start failed (no device): the voice then stays silent for the run.</summary>
    bool Available { get; }

    /// <summary>Starts the clip, replacing the one that plays. Returns false when it cannot start.</summary>
    /// <param name="clip">The 16-bit mono clip, with its sample rate.</param>
    bool Start(AudioBuffer clip);

    /// <summary>Whether the started clip is still playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Stops the clip that plays, if any.</summary>
    void Stop();
}

/// <summary>
/// The NPC voices of S9: plays the pre-rendered WAV of an NPC line (the bundle's <c>speech/</c> folder, read through the core's
/// <see cref="StaticLineStore"/>) when the panel shows it, and the slow file for <c>Más despacio</c>. A turn's lines play one after the other;
/// a new utterance (a turn, the opening, <c>Repetir</c>, a word card) replaces what plays.
/// </summary>
/// <remarks>
/// <b>Degradation.</b> <c>--mute</c> plays nothing. Without a sound device the first start fails, the voice logs <c>no audio device</c> once and
/// stays silent (the panel still shows the text). A line with no file counts as <see cref="Missing"/> and is skipped; a line with no speakable
/// id at all (a templated world line of the guide) counts as <see cref="TextOnly"/>: it has no voice by design.
/// </remarks>
public sealed class NpcVoice : ILinePlayer, IDisposable
{
    private readonly StaticLineStore _store;
    private readonly SpeechIndex _index;
    private readonly IAudioOutput _output;
    private readonly Action<string> _log;
    private readonly Queue<(string Id, AudioBuffer Clip)> _queue = new();
    private bool _playing;
    private bool _noDeviceLogged;

    /// <summary>Creates the voice over a speech folder.</summary>
    /// <param name="speechDirectory">The bundle's <c>speech/</c> folder.</param>
    /// <param name="index">The text-to-line index of the bundle's speakable lines.</param>
    /// <param name="output">Where the clips go.</param>
    /// <param name="muted">Whether <c>--mute</c> was given: nothing plays.</param>
    /// <param name="log">Where the one-line reports go (each line played, the no-device notice, missing files).</param>
    public NpcVoice(string speechDirectory, SpeechIndex index, IAudioOutput output, bool muted, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(speechDirectory);
        _store = new StaticLineStore(speechDirectory);
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        Muted = muted;
    }

    /// <summary>Whether the voice is muted (<c>--mute</c>).</summary>
    public bool Muted { get; }

    /// <summary>How many clips started.</summary>
    public int Played { get; private set; }

    /// <summary>How many lines had a speakable id whose file was not there (or not readable).</summary>
    public int Missing { get; private set; }

    /// <summary>How many shown NPC lines have no speakable line (a templated world line): text only, by design.</summary>
    public int TextOnly { get; private set; }

    /// <summary>Whether a clip is playing or waiting.</summary>
    public bool Busy => _playing || _queue.Count > 0;

    /// <summary>
    /// Says the NPC's lines in order, replacing what plays. Each text is looked up as the speaker's line; a text with no line is counted as
    /// <see cref="TextOnly"/>.
    /// </summary>
    /// <param name="speaker">The NPC's agent id (<see cref="SpeechSpeakers"/>).</param>
    /// <param name="lines">The Spanish texts, as the panel shows them.</param>
    public void Say(string speaker, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Stop();
        foreach (var text in lines)
        {
            Enqueue(speaker, text, slow: false);
        }

        StartNext();
    }

    /// <summary>
    /// <see cref="ILinePlayer"/>: plays the NPC's line again (<c>Repetir</c> at normal speed, <c>Más despacio</c> from the slow file), replacing
    /// what plays.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="line">The Spanish text of the line.</param>
    /// <param name="slow">Whether to play the slow file.</param>
    public void Play(string npc, string line, bool slow)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(line);
        Stop();
        Enqueue(npc, line, slow);
        StartNext();
    }

    /// <summary>Called every frame: starts the next queued clip when the one that plays has ended.</summary>
    public void Update()
    {
        if (_playing && !_output.IsPlaying)
        {
            _playing = false;
        }

        if (!_playing)
        {
            StartNext();
        }
    }

    /// <summary>Stops the clip that plays and drops the queue.</summary>
    public void Stop()
    {
        _queue.Clear();
        if (_playing)
        {
            _output.Stop();
            _playing = false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        _output.Dispose();
    }

    private void Enqueue(string speaker, string text, bool slow)
    {
        if (Muted || !_output.Available)
        {
            return;
        }

        var line = _index.Find(speaker, text);
        if (line is null)
        {
            TextOnly++;
            return;
        }

        var id = slow ? SpeechFiles.SlowId(line.LineId) : line.LineId;
        try
        {
            if (_store.TryGet(id, out var clip))
            {
                _queue.Enqueue((id, clip));
                return;
            }
        }
        catch (InvalidDataException)
        {
            // A file that is not mono 16-bit PCM is no voice: it counts as missing, like one that is not there.
        }

        Missing++;
        _log($"voice: missing {id}.wav");
    }

    private void StartNext()
    {
        _playing = false;
        while (_queue.Count > 0)
        {
            var (id, clip) = _queue.Dequeue();
            if (_output.Start(clip))
            {
                _playing = true;
                Played++;
                _log($"voice: {id}");
                return;
            }

            if (!_noDeviceLogged)
            {
                _noDeviceLogged = true;
                _log("voice: no audio device; the NPC lines are shown and not played");
            }

            _queue.Clear();
        }
    }
}
