using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Language.Book;

/// <summary>
/// The book's speaker (S20b): plays the recording of a row through the NPC voice, so it goes through the mixer as voice (the other sounds duck under
/// it, <c>--mute</c> keeps it silent), and writes a mark to the sound log for each play that was accepted (<c>--sound-log</c>). Engine free.
/// </summary>
public sealed class BookSpeaker
{
    private readonly NpcVoice _voice;
    private readonly SoundMixer _mixer;

    /// <summary>Creates the speaker over the voice and the mixer.</summary>
    /// <param name="voice">The NPC voice (the recordings are its clips).</param>
    /// <param name="mixer">The mixer, for the sound log's mark.</param>
    public BookSpeaker(NpcVoice voice, SoundMixer mixer)
    {
        _voice = voice ?? throw new ArgumentNullException(nameof(voice));
        _mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));
    }

    /// <summary>
    /// Plays the recording of the speakable row with this index (among the page's rows that have a button), normal or slow. Returns whether the clip was
    /// accepted; a row with no button, or an index off the page, plays nothing.
    /// </summary>
    /// <param name="lines">The page's lines.</param>
    /// <param name="speakableIndex">The row's index among the speakable rows.</param>
    /// <param name="slow">Whether to play the slow recording (Shift).</param>
    public bool Speak(IReadOnlyList<BookLine> lines, int speakableIndex, bool slow)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var rows = BookSpeakers.Speakable(lines);
        if (speakableIndex < 0 || speakableIndex >= rows.Count)
        {
            return false;
        }

        return PlayLine(lines[rows[speakableIndex]].Speak!, slow);
    }

    /// <summary>Plays a recording by its line id, normal or slow; returns whether the clip was accepted.</summary>
    /// <param name="lineId">The line id.</param>
    /// <param name="slow">Whether to play the slow recording.</param>
    public bool PlayLine(string lineId, bool slow)
    {
        if (!_voice.PlayLine(lineId, slow))
        {
            return false;
        }

        _mixer.Mark(slow ? $"speak {lineId} slow" : $"speak {lineId}");
        return true;
    }
}
