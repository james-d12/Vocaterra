using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Session;

/// <summary>A word met for the first time in a tick (S4, S9, S24): its card on screen, the sparkle, and the guide saying the noun.</summary>
public sealed class WordCardPresenter(VoxelSession session, WordCardQueue cards, SoundFrontend sound, NpcVoice voice) : ITickObserver
{
    /// <inheritdoc />
    public void AfterTick(IReadOnlyList<VoxelCommand> commands)
    {
        foreach (var card in session.TakeWordCards())
        {
            cards.Add(card);
            sound.Queue(SoundCue.WordLearnt); // a new word: the sparkle (S24)
            voice.Say(SpeechSpeakers.Guide, [card.Phrase]); // a word card says its noun (S9)
        }
    }
}
