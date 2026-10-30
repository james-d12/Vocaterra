using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Language.Phrases;

/// <summary>
/// The review deck (X06, R06): which due phrases a warm-up asks and in what order, the guide's offer at the first talk of a later session (never gating, skipped
/// with one key), each item asked and answered as the guide's review of a due phrase (so the credit is the spacing gaps' as everywhere, D-game-6), and the book's
/// "Review now", which starts the same warm-up at the guide.
/// </summary>
public sealed class ReviewDeckTests
{
    private const long Minute = 60;

    // Session 1 credits the stall's request and the river question; session 2 has begun (a new play session lifts every gap), and the player stands at the guide.
    private static VoxelSession DueInSessionTwo(bool enabled = true)
    {
        var session = TestGame.NewSession();
        session.Deck.Enabled = enabled;
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        _ = session.Say("quiero agua, por favor");
        session.EndTalk();
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        _ = session.Say("¿dónde está el río?");
        session.EndTalk();
        session.PlaySession = 2;
        Stage.TeleportToNpc(session, "guide");
        return session;
    }

    private static List<WorldEvent> DeckEvents(VoxelSession session) =>
        session.Events.OfType<WorldEvent>().Where(static e => e.Name is WorldEvents.WarmupOffered or WorldEvents.WarmupStarted or WorldEvents.WarmupEnded or WorldEvents.WarmupSkipped
            or WorldEvents.ReviewSkipped or WorldEvents.ReviewAnswered).ToList();

    // ---- Which items (Select).

    [Fact]
    public void TheMostOverdueComesFirstThenTheMostNoSeThenTheSheetsOrderAndAtMostFour()
    {
        var progress = new PhraseProgress(TestGame.Data.Phrases, 2, TestGame.Data.Npcs.ScaffoldPhrases);
        PhraseRecord Credited(string id, long at, int dontKnow = 0) =>
            new(id, SupportLevel.CardAndAudio, Supported: 6, DontKnow: dontKnow, CreditedUses: 1, LastCreditedAt: at, LastCreditedSession: 1);
        progress.Restore(
        [
            Credited("pedir-agua", 1000),
            Credited("si-por-favor", 1000, dontKnow: 2),
            Credited("no-gracias", 500),
            Credited("donde-esta-el-rio", 1000),
        ]);

        var items = ReviewDeck.Select(progress, 5000, CreditRules.Default, static _ => true);

        Assert.Equal(["no-gracias", "si-por-favor", "pedir-agua", "donde-esta-el-rio"], items); // the oldest; then the "no sé"; then the sheet's order
        Assert.Equal(["pedir-agua", "no-gracias"], ReviewDeck.Select(progress, 5000, CreditRules.Default, static _ => true, max: 2, first: "pedir-agua"));
        Assert.DoesNotContain("si-por-favor", ReviewDeck.Select(progress, 5000, CreditRules.Default, static id => id != "si-por-favor")); // the guide must be able to ask it
    }

    [Fact]
    public void NothingNotYetCreditedIsEverAsked()
    {
        var session = TestGame.NewSession();

        Assert.Equal(0, session.Deck.Ready);
        Assert.False(session.Deck.Request()); // "Review now" with nothing due is refused
        Assert.Equal(DeckStatus.Idle, session.Deck.Status);
    }

    // ---- The offer.

    [Fact]
    public void AtTheFirstTalkOfALaterSessionTheGuideOffersAWarmUpAndAsksNothingYet()
    {
        var session = DueInSessionTwo();

        _ = session.OpenTalk("guide");

        Assert.Equal(DeckStatus.Offered, session.Deck.Status);
        Assert.Equal(["pedir-agua", "donde-esta-el-rio"], session.Deck.Items);
        Assert.Null(session.PhraseReview); // the offer waits: her own review question is not asked over it
        var offer = session.Deck.OfferFor("guide")!;
        Assert.True(offer.Warmup);
        Assert.Equal(("Warm-up", "2 phrases from last time are ready for a quick warm-up. Not a test: skip it any time."), (offer.Title, offer.English));
        Assert.Null(session.Deck.OfferFor("villager"));
        Assert.Equal((WorldEvents.WarmupOffered, "pedir-agua, donde-esta-el-rio"), (DeckEvents(session).Single().Name, DeckEvents(session).Single().Detail));
    }

    [Fact]
    public void SwitchedOffOrInTheFirstSessionTheGuideKeepsHerOwnReview()
    {
        var off = DueInSessionTwo(enabled: false);
        _ = off.OpenTalk("guide");
        Assert.Equal(DeckStatus.Idle, off.Deck.Status);
        Assert.Equal("pedir-agua", off.PhraseReview!.PhraseId); // S8: the guide asks one due phrase herself

        var first = TestGame.NewSession();
        first.Deck.Enabled = true;
        Stage.TeleportToNpc(first, "shopkeeper");
        _ = first.OpenTalk("shopkeeper");
        _ = first.Say("quiero agua, por favor");
        first.EndTalk();
        first.SkipTime(11 * Minute); // due inside session 1
        Stage.TeleportToNpc(first, "guide");
        _ = first.OpenTalk("guide");
        Assert.Equal(DeckStatus.Idle, first.Deck.Status);
        Assert.Equal("pedir-agua", first.PhraseReview!.PhraseId);
    }

    [Fact]
    public void SkippingTheOfferLeavesTheTalkAsItWasAndItIsNotOfferedAgainThisSession()
    {
        var session = DueInSessionTwo();
        _ = session.OpenTalk("guide");

        Assert.True(session.Deck.SkipAll());

        Assert.Equal(DeckStatus.Finished, session.Deck.Status);
        Assert.Null(session.PhraseReview);
        Assert.Equal(WorldEvents.WarmupSkipped, DeckEvents(session).Last().Name);
        Assert.Empty(session.Deck.TakeNotes()); // a skip is no "done" note
        session.EndTalk();
        _ = session.OpenTalk("guide");
        Assert.True(session.Deck.OfferedThisSession);
        Assert.Null(session.Deck.OfferFor("guide")); // once per play session
        Assert.Equal("pedir-agua", session.PhraseReview!.PhraseId); // and the guide's own review is back
    }

    // ---- The warm-up.

    [Fact]
    public void TheWarmUpAsksEachItemAsTheGuidesReviewAndEndsWithANote()
    {
        var session = DueInSessionTwo();
        _ = session.OpenTalk("guide");

        Assert.True(session.Deck.Accept());

        Assert.Equal(DeckStatus.Running, session.Deck.Status);
        Assert.Equal("pedir-agua", session.PhraseReview!.PhraseId);
        Assert.Equal(["¿Qué necesitas?"], session.Deck.TakeLines()); // her request goes on the panel
        Assert.Empty(session.Support.Cards); // a review shows no card
        var view = session.Deck.ViewFor("guide")!;
        Assert.Equal((ReviewDeck.StripId, 1, 2, "Remember it"), (view.LessonId, view.StepNumber, view.StepCount, view.Heading));

        Assert.True(session.Say("quiero agua, por favor").Communicated);
        Assert.Equal("donde-esta-el-rio", session.PhraseReview!.PhraseId); // the next item is asked at once
        Assert.True(session.Say("¿dónde está el río?").Communicated);

        Assert.Equal(DeckStatus.Finished, session.Deck.Status);
        Assert.Null(session.PhraseReview);
        Assert.Equal(["Warm-up done: 2 phrases reviewed."], session.Deck.TakeNotes());
        var logged = DeckEvents(session);
        Assert.Equal(["pedir-agua, warm-up", "donde-esta-el-rio, warm-up"], logged.Where(static e => e.Name == WorldEvents.ReviewAnswered).Select(static e => e.Detail));
        Assert.Equal("2 of 2", logged.Single(static e => e.Name == WorldEvents.WarmupEnded).Detail);
    }

    [Fact]
    public void AWarmUpAnswerIsCreditedLikeAnyReviewByTheSpacingGap()
    {
        var session = DueInSessionTwo();
        var before = session.PhraseProgress.RecordOf("pedir-agua");
        _ = session.OpenTalk("guide");
        _ = session.Deck.Accept();

        _ = session.Say("quiero agua, por favor");

        var use = session.Events.OfType<PhraseUse>().Last(static u => u.PhraseId == "pedir-agua");
        Assert.Equal(("guide", PhraseUseKind.Unaided), (use.Npc, use.Kind)); // no card: unaided, at the guide
        var after = session.PhraseProgress.RecordOf("pedir-agua");
        Assert.Equal(before.CreditedUses + 1, after.CreditedUses); // the new session lifted the gap: the use is credited
    }

    [Fact]
    public void HelpAndNoSeLeaveTheItemOpenAndKSkipsIt()
    {
        var session = DueInSessionTwo();
        _ = session.OpenTalk("guide");
        _ = session.Deck.Accept();

        _ = session.Say("no sé");
        Assert.Equal("pedir-agua", session.PhraseReview!.PhraseId); // not an answer: the item stays

        Assert.True(session.Deck.SkipItem());
        Assert.Equal("donde-esta-el-rio", session.PhraseReview!.PhraseId);
        Assert.True(session.Deck.SkipItem());

        Assert.Equal(DeckStatus.Finished, session.Deck.Status);
        Assert.Equal(["Warm-up done: 0 phrases reviewed."], session.Deck.TakeNotes());
        Assert.Equal(2, DeckEvents(session).Count(static e => e.Name == WorldEvents.ReviewSkipped));
        Assert.DoesNotContain(DeckEvents(session), static e => e.Name == WorldEvents.ReviewAnswered);
    }

    [Fact]
    public void ClosingThePanelEndsTheWarmUp()
    {
        var session = DueInSessionTwo();
        _ = session.OpenTalk("guide");
        _ = session.Deck.Accept();

        session.EndTalk();

        Assert.Equal(DeckStatus.Finished, session.Deck.Status);
        Assert.Null(session.PhraseReview);
        Assert.Equal("0 of 2", DeckEvents(session).Single(static e => e.Name == WorldEvents.WarmupEnded).Detail);
    }

    // ---- "Review now".

    [Fact]
    public void ReviewNowWaitsForTheGuideThenStartsTheWarmUpWithoutAnOffer()
    {
        var session = DueInSessionTwo(enabled: false); // the book's button works whether the offer is on or not
        Assert.Equal(2, session.Deck.Ready);

        Assert.True(session.Deck.Request());

        Assert.Equal(DeckStatus.Pending, session.Deck.Status);
        Assert.Equal("Review now: talk to the guide to start the warm-up", session.Deck.PendingHud);
        Assert.Equal("Review now: talk to the guide to start the warm-up", session.ObjectiveHud().Single().Text);
        _ = session.OpenTalk("guide");
        Assert.Equal(DeckStatus.Running, session.Deck.Status);
        Assert.Equal("pedir-agua", session.PhraseReview!.PhraseId);
        Assert.Empty(session.Deck.TakeLines()); // at the talk's own start the panel shows her request with the opening (ReviewLine)
        Assert.Equal("¿Qué necesitas?", session.ReviewLine);
    }

    [Fact]
    public void TheSameRecordGivesTheSameWarmUp()
    {
        static IReadOnlyList<string> Run()
        {
            var session = DueInSessionTwo();
            _ = session.OpenTalk("guide");
            _ = session.Deck.Accept();
            _ = session.Say("quiero agua, por favor");
            _ = session.Say("¿dónde está el río?");
            return DeckEvents(session).Select(static e => $"{e.Name} {e.Detail}").ToList();
        }

        Assert.Equal(Run(), Run());
    }
}
