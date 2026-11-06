using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Language.Book;

namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// The station's instruction rows through the NPC voice (W07): a row's recording is found as the book finds a phrase's (<see cref="BookRecordings.ForPhrase"/>) and played by the
/// book's speaker, normal or from the slow file. Nothing plays when the voice is muted, when there is no sound device or when the recording is missing: <see cref="CanPlay"/>
/// says so, and the station shows the Spanish card instead.
/// </summary>
public sealed class NpcInstructionAudio : IInstructionAudio
{
    private readonly NpcVoice _voice;
    private readonly BookSpeaker _speaker;
    private readonly BookRecordings _recordings;

    /// <summary>Creates the audio over the game's voice.</summary>
    /// <param name="voice">The NPC voice.</param>
    /// <param name="speaker">The book's speaker over the same voice (it marks the sound log).</param>
    /// <param name="recordings">Which recordings exist and which one a phrase plays.</param>
    public NpcInstructionAudio(NpcVoice voice, BookSpeaker speaker, BookRecordings recordings)
    {
        _voice = voice;
        _speaker = speaker;
        _recordings = recordings;
    }

    /// <inheritdoc />
    public bool CanPlay(string rowId) => _voice.CanPlay && _recordings.ForPhrase(rowId) is not null;

    /// <inheritdoc />
    public bool Play(string rowId, bool slow) => _recordings.ForPhrase(rowId) is { } line && _speaker.PlayLine(line, slow);
}
