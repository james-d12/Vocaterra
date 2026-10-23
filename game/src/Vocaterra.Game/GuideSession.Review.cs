namespace Vocaterra.Game;

// The review deck's part of the session (X06, R06): the warm-up of due phrases and the review steps of the lessons both ask earlier phrases again, and both end by
// logging what was reviewed. See ReviewDeck for the rules.
public abstract partial class GuideSession
{
    // Whether the deck claims the talk being opened with the guide (its offer, or a review asked for from the book): her own review question does not open it as well.
    private bool _deckClaim;

    /// <summary>
    /// The review deck (X06, R06): the warm-up of due phrases the guide offers at the start of a session and the book's "Review now" asks for. The credit of its answers flows
    /// through <see cref="PhraseProgress"/> and its spacing gaps like any review.
    /// </summary>
    public ReviewDeck Deck { get; }

    /// <summary>
    /// Opens the guide's review of a phrase for the deck, or ends it when <paramref name="review"/> is <see langword="null"/>: the request line is the guide's, no card is shown and
    /// the answer is graded like the review of a due phrase (S8).
    /// </summary>
    /// <param name="review">The review, or <see langword="null"/>.</param>
    internal void AskDeckReview(PhraseReview? review)
    {
        if (review is null)
        {
            if (PhraseReview is not null)
            {
                PhraseReview = null;
                _retry = null;
                RefreshDeckSupport();
            }

            return;
        }

        EndReview();
        PhraseReview = review;
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.PhraseReviewAsked, NpcService.GuideAgent, review.PhraseId));
        RefreshDeckSupport();
    }

    // The panel follows the review now open (or closed): no card for the phrase asked, the cards of the prompt again once it is over.
    private void RefreshDeckSupport()
    {
        if (_talkOpen && Support.Npc is { } npc)
        {
            RefreshSupport(npc, _npcs.PromptOf(npc));
        }
    }

    /// <summary>A phrase was reviewed and answered (a warm-up item or a lesson's review step): the summary card counts it.</summary>
    /// <param name="phraseId">The phrase.</param>
    /// <param name="how">Where it was reviewed: <c>warm-up</c> or <c>lesson</c>.</param>
    internal void NoteReviewed(string phraseId, string how) =>
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.ReviewAnswered, CurrentNpc, $"{phraseId}, {how}"));
}
