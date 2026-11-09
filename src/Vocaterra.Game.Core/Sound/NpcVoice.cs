using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game.Core.Sound;

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

    /// <summary>Sets the voice volume, 0 to 1 (S24); an output with no volume control ignores it.</summary>
    /// <param name="volume">The volume.</param>
    void SetVolume(double volume)
    {
    }
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
    private readonly SpeechStore _store;
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
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _store = new SpeechStore(speechDirectory, _index.Lines);
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        Muted = muted;
    }

    /// <summary>Whether the voice is muted (<c>--mute</c>).</summary>
    public bool Muted { get; set; }

    /// <summary>How many clips started.</summary>
    public int Played { get; private set; }

    /// <summary>How many lines had a speakable id whose file was not there (or not readable).</summary>
    public int Missing { get; private set; }

    /// <summary>How many NPC copies of a shared line had no file and played the guide's file of the same line instead (V30).</summary>
    public int FellBack { get; private set; }

    /// <summary>How many shown NPC lines have no speakable line (a templated world line): text only, by design.</summary>
    public int TextOnly { get; private set; }

    /// <summary>Sets the volume of the voices, 0 to 1 (the mixer's voice volume, S24).</summary>
    /// <param name="volume">The volume.</param>
    public void SetVolume(double volume) => _output.SetVolume(volume);

    /// <summary>Whether a clip can be heard at all: the voice is not muted and a sound output is there (W07: the station shows the Spanish card when it is not).</summary>
    public bool CanPlay => !Muted && _output.Available;

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

    /// <summary><see cref="ILinePlayer.Append"/>: queues the line after what plays, without stopping it.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="line">The Spanish text of the line.</param>
    /// <param name="slow">Whether to play the slow file.</param>
    public void Append(string npc, string line, bool slow)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(line);
        Enqueue(npc, line, slow);
        if (!_playing)
        {
            StartNext();
        }
    }

    /// <summary>
    /// Plays a recording by its line id (S20b: a book row's button), normal or slow, replacing what plays. Returns whether the clip was queued: false
    /// when muted, when the output is unavailable or when the file is not there. (Without a sound device the clip is queued and the start fails, which
    /// the voice logs once, as for any line.)
    /// </summary>
    /// <param name="lineId">The line id, as the speech folder names it (<c>card.madera</c>, <c>shop_need.1</c>).</param>
    /// <param name="slow">Whether to play the slow file.</param>
    public bool PlayLine(string lineId, bool slow)
    {
        ArgumentNullException.ThrowIfNull(lineId);
        if (Muted || !_output.Available)
        {
            return false;
        }

        Stop();
        if (!TryQueue(slow ? SpeechFiles.SlowId(lineId) : lineId, countMissing: true))
        {
            return false;
        }

        StartNext();
        return true;
    }

    /// <summary>Whether the speech folder has the normal file of a line id (the book resolves its buttons with it).</summary>
    /// <param name="lineId">The line id.</param>
    public bool HasRecording(string lineId)
    {
        ArgumentNullException.ThrowIfNull(lineId);
        return _store.Contains(lineId);
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

        if (TryQueue(slow ? SpeechFiles.SlowId(line.LineId) : line.LineId, countMissing: line.Kind != SpeechKind.NpcCopy) || line.Kind != SpeechKind.NpcCopy)
        {
            return;
        }

        // V30: an NPC's copy of a shared line (<id>.<npc>) with no file plays the guide's file of the same line (SpeechKind.NpcCopy documents the fallback).
        var shared = line.LineId[..^(line.Speaker.Length + 1)];
        if (TryQueue(slow ? SpeechFiles.SlowId(shared) : shared, countMissing: true))
        {
            FellBack++;
            _log($"voice: fallback {line.LineId} to the guide's {shared}");
        }
    }

    // Queues the file of a line id; a missing or unreadable file counts as Missing and is logged.
    private bool TryQueue(string id, bool countMissing)
    {
        try
        {
            if (_store.TryGet(id, out var clip))
            {
                _queue.Enqueue((id, clip));
                return true;
            }
        }
        catch (InvalidDataException)
        {
            // A file that is not mono 16-bit PCM is no voice: it counts as missing, like one that is not there.
        }

        if (countMissing)
        {
            Missing++;
            _log($"voice: missing {id}.wav");
        }

        return false;
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
