using Vocaterra.Nlp.Pipeline;
using Vocaterra.Simulation.Items;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The session side of S8 through the real core: the support card at the panel's prompt and how it fades, the key words before the first stall
/// exchange, the phrase skills in the core's learner model, the phrase uses fed into the progress, and the guide's review of a due phrase.
/// Typed lines earn full credit here (<see cref="CreditRules.FullTyped"/>) unless a test says otherwise; the gap rules are the owner's.
/// </summary>
public sealed class PhraseSupportTests
{
    private const long Minute = 60;

    private static VoxelSession AtStall(CreditRules? credit = null)
    {
        var session = TestGame.NewSession(credit: credit);
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        return session;
    }

    private static VoxelSession AtVillager()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        return session;
    }

    private static IEnumerable<PhraseUse> Phrases(VoxelSession session) => session.Events.OfType<PhraseUse>();

    private static void Reopen(VoxelSession session, string npc, long skipSeconds)
    {
        session.EndTalk();
        session.SkipTime(skipSeconds);
        _ = session.OpenTalk(npc);
    }

    [Fact]
    public void TheShopkeepersPanelOpensWithTheCardOfTheRequestAndItsAudioCue()
    {
        var session = AtStall();

        var card = Assert.Single(session.Support.Cards);
        Assert.Equal("pedir-agua", card.PhraseId);
        Assert.Equal("Quiero agua, por favor.", card.Phrase);
        Assert.Equal("I want water, please.", card.Meaning);
        Assert.Equal(["quiero = I want", "agua = water", "por favor = please"], card.Glosses);
        Assert.Equal("shop_help_ask.1", card.AudioLineId);
        var cue = Assert.Single(session.Support.TakeCues());
        Assert.Equal(("shopkeeper", "shop_help_ask.1", "card", "Puedes decir: «Quiero agua, por favor»."), (cue.Npc, cue.LineId, cue.Reason, cue.Text));
        var record = session.PhraseProgress.RecordOf("pedir-agua");
        Assert.Equal((1, SupportLevel.CardAndAudio), (record.CardShown, record.Level));
        Assert.Equal(1, session.PhraseProgress.RecordOf("que-necesitas").Heard); // the question the learner hears
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.SupportCardShown, Detail: "pedir-agua, level" });
    }

    [Fact]
    public void ACorrectRequestIsASupportedUseStepsTheLevelDownAndTheOfferShowsItsOwnCards()
    {
        var session = AtStall();
        _ = session.Support.TakeCues();

        var turn = session.Say("quiero agua, por favor");

        var use = Phrases(session).Single();
        Assert.Equal(("pedir-agua", PhraseUseKind.Supported, true, SupportLevel.CardAndAudio, InputKind.Typed), (use.PhraseId, use.Kind, use.Correct, use.Support, use.Input));
        var record = session.PhraseProgress.RecordOf("pedir-agua");
        Assert.Equal((SupportLevel.AudioOnly, 6, 1), (record.Level, record.Supported, record.CreditedUses));
        Assert.Equal(1, session.PhraseProgress.RecordOf("que-necesitas").Answered);
        Assert.Equal("stall-offer", NpcService.PromptAfter("shopkeeper", turn.AgentState));
        Assert.Equal(["si-por-favor", "no-gracias"], session.Support.Cards.Select(static c => c.PhraseId)); // the bread offer: both answers
        Assert.Equal(["shop_help_offer.1", "shop_help_offer.1"], session.Support.TakeCues().Select(static c => c.LineId));
        Assert.Equal(1, session.PhraseProgress.RecordOf("quieres-pan").Heard);
    }

    [Fact]
    public void TheCardIsShownOnlyAtCardAndAudioAndAudioOnlyHasTheReplayHookAndNoCard()
    {
        var session = AtVillager();
        Assert.Equal(["donde-esta-el-rio"], session.Support.Cards.Select(static c => c.PhraseId));
        _ = session.Support.TakeCues();
        _ = session.Say("¿dónde está el río?");
        Assert.Equal(SupportLevel.AudioOnly, session.PhraseProgress.LevelOf("donde-esta-el-rio"));
        Assert.Empty(session.Support.Cards); // the use stepped it down: the card goes with it

        Reopen(session, "villager", 11 * Minute);

        Assert.Empty(session.Support.Cards);
        Assert.Equal(["donde-esta-el-rio"], session.Support.AudioOnly);
        var replay = session.Support.ReplayCue;
        Assert.NotNull(replay);
        Assert.Equal("villager_help.1", replay.LineId);
        var cues = session.Support.TakeCues();
        Assert.Contains(cues, static c => c is { Reason: "audio-only", LineId: "villager_help.1" }); // the sound plays when the prompt appears
        Assert.DoesNotContain(cues, static c => c.Reason == "card");
        Assert.Equal(1, session.PhraseProgress.RecordOf("donde-esta-el-rio").CardShown); // no second card

        var second = session.Say("¿dónde está el río?");
        Assert.True(second.Communicated);
        Assert.Equal(PhraseUseKind.Supported, Phrases(session).Last().Kind); // the audio was on offer
        Assert.Equal(SupportLevel.None, session.PhraseProgress.LevelOf("donde-esta-el-rio")); // the gap had passed: the next step down

        Reopen(session, "villager", 0);
        Assert.Empty(session.Support.Cards);
        Assert.Empty(session.Support.AudioOnly);
        Assert.Null(session.Support.ReplayCue);
        Assert.DoesNotContain(session.Support.TakeCues(), static c => c.Reason is "card" or "audio-only");
        _ = session.Say("¿dónde está el río?");
        Assert.Equal(PhraseUseKind.Unaided, Phrases(session).Last().Kind); // nothing was on offer
    }

    [Fact]
    public void AnEarlyCorrectUseIsLoggedAndTheLevelDoesNotStepDownUntilTheGapHasPassed()
    {
        var session = AtVillager();
        _ = session.Say("¿dónde está el río?");
        Reopen(session, "villager", 5 * Minute); // inside the ten minutes

        _ = session.Say("¿dónde está el río?");

        Assert.Equal(SupportLevel.AudioOnly, session.PhraseProgress.LevelOf("donde-esta-el-rio"));
        var record = session.PhraseProgress.RecordOf("donde-esta-el-rio");
        Assert.Equal((12, 1), (record.Supported, record.CreditedUses)); // both uses are counted, only the first earned a step
        Reopen(session, "villager", 6 * Minute); // eleven minutes since the first
        _ = session.Say("¿dónde está el río?");
        Assert.Equal(SupportLevel.None, session.PhraseProgress.LevelOf("donde-esta-el-rio"));
    }

    [Fact]
    public void AyudaAtNothingShowsTheCardAgainAndTheNextUseCountsAsSupported()
    {
        var session = AtVillager();
        _ = session.Say("¿dónde está el río?");
        Reopen(session, "villager", 11 * Minute);
        _ = session.Say("¿dónde está el río?"); // none now
        Reopen(session, "villager", 0);
        Assert.Empty(session.Support.Cards);
        var shownBefore = session.PhraseProgress.RecordOf("donde-esta-el-rio").CardShown;

        _ = session.Say("ayuda");

        Assert.Equal(["donde-esta-el-rio"], session.Support.Cards.Select(static c => c.PhraseId));
        Assert.Equal(shownBefore + 1, session.PhraseProgress.RecordOf("donde-esta-el-rio").CardShown);
        Assert.True(session.Support.HelpGiven);
        Assert.Equal(SupportLevel.AudioOnly, session.PhraseProgress.LevelOf("donde-esta-el-rio")); // asking for help steps the level up
        _ = session.Say("¿dónde está el río?");
        Assert.Equal(PhraseUseKind.Supported, Phrases(session).Last().Kind); // the model was just shown
        Assert.Empty(session.Support.Cards); // spent
    }

    [Fact]
    public void NoSeStepsTheLevelBackUpAndCountsOnThePhraseInPlay()
    {
        var session = AtVillager();
        _ = session.Say("¿dónde está el río?");
        Assert.Equal(SupportLevel.AudioOnly, session.PhraseProgress.LevelOf("donde-esta-el-rio"));

        _ = session.Say("no sé");

        var dont = Phrases(session).Last();
        Assert.Equal((PhraseIds.DontKnow, PhraseUseKind.DontKnow), (dont.PhraseId, dont.Kind));
        Assert.Equal(["donde-esta-el-rio"], dont.InPlay);
        var record = session.PhraseProgress.RecordOf("donde-esta-el-rio");
        Assert.Equal((1, SupportLevel.CardAndAudio), (record.DontKnow, record.Level));
        Assert.Equal(["donde-esta-el-rio"], session.Support.Cards.Select(static c => c.PhraseId)); // the card is back
    }

    [Fact]
    public void AChoiceAndAFailForwardAreRecognitionAndCarryOnOnlyAndNeverProductionCredit()
    {
        var chosen = AtStall();
        _ = chosen.Say("zzz qqq");
        _ = chosen.Say("zzz qqq");
        _ = chosen.Say("zzz qqq"); // the choices are offered
        _ = chosen.Say("quiero agua por favor");
        var choice = chosen.PhraseProgress.RecordOf("pedir-agua");
        Assert.Equal((1, 0, SupportLevel.CardAndAudio), (choice.ChoiceOnly, choice.Supported, choice.Level)); // recognition only: no production credit, no step down

        var forward = AtStall();
        _ = forward.Say("zzz qqq");
        _ = forward.Say("zzz qqq");
        _ = forward.Say("zzz qqq");
        _ = forward.Say("zzz qqq");
        var record = forward.PhraseProgress.RecordOf("pedir-agua");
        Assert.Equal((1, 0), (record.FailForward, record.Supported + record.Unaided));
        Assert.Equal(SupportLevel.CardAndAudio, record.Level); // never lower than the card: the step up from the card changes nothing but clears the credit
    }

    [Fact]
    public void AnUncertainRecognitionNeverTouchesThePhraseProgress()
    {
        var session = AtStall();
        var before = session.PhraseProgress.Records.ToList();

        _ = session.Say("blah");

        Assert.Equal(before, session.PhraseProgress.Records);
    }

    [Fact]
    public void AMistakeInTheRequestIsASlipThatNeitherCountsNorStepsTheLevel()
    {
        var session = AtStall();

        var turn = session.Say("yo quieres agua, por favor");

        Assert.True(turn.Communicated);
        Assert.NotEmpty(turn.Errors);
        var use = Phrases(session).Single();
        Assert.False(use.Correct);
        var record = session.PhraseProgress.RecordOf("pedir-agua");
        Assert.Equal((0, 1, SupportLevel.CardAndAudio), (record.Supported, record.Slips, record.Level));
    }

    [Fact]
    public void TypedUsesEarnHalfCreditUnderTheDefaultRulesAndTwoCreditedUsesAreNeededToStepDown()
    {
        var session = AtVillager();
        var typed = TestGame.NewSession(credit: CreditRules.Default);
        Stage.TeleportToNpc(typed, "villager");
        _ = typed.OpenTalk("villager");

        _ = typed.Say("¿dónde está el río?");

        var record = typed.PhraseProgress.RecordOf("donde-esta-el-rio");
        Assert.Equal((3, SupportLevel.CardAndAudio), (record.Supported, record.Level));
        Reopen(typed, "villager", 11 * Minute);
        _ = typed.Say("¿dónde está el río?");
        Assert.Equal((SupportLevel.AudioOnly, 6), (typed.PhraseProgress.LevelOf("donde-esta-el-rio"), typed.PhraseProgress.RecordOf("donde-esta-el-rio").Supported));
        Assert.Equal(SupportLevel.CardAndAudio, session.PhraseProgress.LevelOf("donde-esta-el-rio")); // the untouched session is unaffected
    }

    [Fact]
    public void TheKeyWordsAreTaughtBeforeTheFirstStallQuestionAndThenTheExchangeStarts()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");

        var first = session.OpenTalk("shopkeeper", teachKeyWords: true);

        Assert.Empty(first.Lines); // no question yet
        Assert.True(session.KeyWordsPending);
        Assert.Equal(["el agua", "el pan"], session.KeyWordCards.Select(static c => c.Phrase));
        foreach (var lemma in new[] { "agua", "pan" })
        {
            var word = session.Words.RecordOf(lemma);
            Assert.Equal((1, 1, 0, 0), (word.Heard, word.Read, word.Spoken, word.CreditUnits)); // heard and read; never a quiz
        }

        var cues = session.Support.TakeCues();
        Assert.Equal(["el agua", "el pan"], cues.Where(static c => c.Reason == "key-word").Select(static c => c.Text));
        Assert.Empty(session.Support.Cards); // the phrase card waits for the question
        Assert.Equal("idle", session.Conversation.ContextOf("shopkeeper").AgentState.StateId); // the shopkeeper has not spoken
        Assert.Empty(session.Conversation.Records);
        Assert.Empty(session.TakeWordCards()); // not a pickup card

        for (var i = 0; i < GuideSession.KeyWordTicks - 1; i++)
        {
            session.Step([]);
        }

        Assert.True(session.KeyWordsPending);
        session.Step([]);
        Assert.False(session.KeyWordsPending);
        var opening = session.TakeOpening();
        Assert.NotNull(opening);
        Assert.Equal(["¿Qué necesitas?"], opening.Lines);
        Assert.True(opening.HostTurn);
        Assert.Equal(["el agua", "el pan"], session.KeyWordCards.Select(static c => c.Phrase)); // they stay up through the talk
        Assert.Equal(["pedir-agua"], session.Support.Cards.Select(static c => c.PhraseId)); // and now the phrase's card
        Assert.Null(session.TakeOpening());
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.KeyWordsShown, Detail: "agua, pan" });

        session.EndTalk();
        Assert.Empty(session.KeyWordCards);
        _ = session.OpenTalk("shopkeeper", teachKeyWords: true); // only the first time
        Assert.False(session.KeyWordsPending);
        Assert.Equal(1, session.Words.RecordOf("agua").Heard);
    }

    [Fact]
    public void ALineSentDuringTheKeyWordsStartsTheExchangeFirstAndLeavingEndsThem()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper", teachKeyWords: true);

        _ = session.Say("quiero agua, por favor");

        Assert.False(session.KeyWordsPending);
        Assert.NotNull(session.TakeOpening()); // the question came first
        Assert.Equal(PhraseUseKind.Supported, Phrases(session).Single().Kind);

        var other = TestGame.NewSession();
        Stage.TeleportToNpc(other, "shopkeeper");
        _ = other.OpenTalk("shopkeeper", teachKeyWords: true);
        other.EndTalk();
        Assert.False(other.KeyWordsPending);
        Assert.Null(other.TakeOpening());
        Assert.Equal(["¿Qué necesitas?"], other.OpenTalk("shopkeeper", teachKeyWords: true).Lines); // the words were taught; the question comes at once
    }

    [Fact]
    public void TheKeyWordsDoNotShowASecondCardWhenTheBreadIsPickedUp()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper", teachKeyWords: true);
        session.FinishKeyWords();
        _ = session.Say("quiero agua, por favor");
        _ = session.Say("sí, por favor");

        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Equal(["botella"], session.TakeWordCards().Select(static c => c.Lemma)); // pan was carded already
    }

    [Fact]
    public void ThePhraseSkillsArePassedInTheTurnAndTheCardShownStepsTheLearnerModel()
    {
        var session = AtStall();
        var learner = session.Conversation.ContextOf("shopkeeper").Learner;
        Assert.Equal(1, learner.Find("phrase.pedir-agua")?.Attempts); // the card shown (LearnerModel.StepSkills)

        _ = session.Say("quiero agua, por favor");

        var envelope = Assert.IsType<ReplayEnvelope>(session.Conversation.Records.Last().Utterance);
        Assert.Equal(["phrase.pedir-agua", "phrase.que-necesitas", "word.agua"], envelope.Context.ExtraSkills);
        var after = session.Conversation.ContextOf("shopkeeper").Learner;
        Assert.Equal(2, after.Find("phrase.pedir-agua")?.Attempts);
        Assert.Equal(1, after.Find("phrase.que-necesitas")?.Attempts);
        Assert.True(after.PKnown("phrase.pedir-agua") > learner.PKnown("phrase.pedir-agua"));
    }

    [Fact]
    public void AMistakeAChoiceAndAServiceWordPassNoPhraseSkill()
    {
        var slip = AtStall();
        _ = slip.Say("yo quieres agua, por favor");
        var slipSkills = Assert.IsType<ReplayEnvelope>(slip.Conversation.Records.Last().Utterance).Context.ExtraSkills;
        Assert.DoesNotContain(slipSkills ?? [], static s => s.StartsWith("phrase.", StringComparison.Ordinal));

        var help = AtStall();
        _ = help.Say("ayuda");
        var helpSkills = Assert.IsType<ReplayEnvelope>(help.Conversation.Records.Last().Utterance).Context.ExtraSkills;
        Assert.DoesNotContain(helpSkills ?? [], static s => s.StartsWith("phrase.", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSameRunGivesTheSamePhraseProgressAndTheSameLearnerSkills()
    {
        static (IReadOnlyList<PhraseRecord>, string) Run()
        {
            var session = AtStall();
            _ = session.Say("quiero agua, por favor");
            _ = session.Say("sí, por favor");
            session.EndTalk();
            Stage.TeleportToNpc(session, "villager");
            _ = session.OpenTalk("villager");
            _ = session.Say("¿dónde está el río?");
            var skills = string.Join(',', session.Conversation.ContextOf("shopkeeper").Learner.Skills.Select(static s => $"{s.SkillId}:{s.Attempts}:{s.PKnown:0.0000}"));
            return (session.PhraseProgress.Records.ToList(), skills);
        }

        var (recordsA, skillsA) = Run();
        var (recordsB, skillsB) = Run();

        Assert.Equal(recordsA, recordsB);
        Assert.Equal(skillsA, skillsB);
    }

    [Fact]
    public void TheGuideAsksADuePhraseInContextAndTheAnswerIsUnaided()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        _ = session.Say("quiero agua, por favor");
        session.EndTalk();
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        Assert.Null(session.PhraseReview); // not due yet (the ten minutes have not passed): the guide opens as she always did
        session.EndTalk();

        session.SkipTime(11 * Minute);
        var opening = session.OpenTalk("guide");

        var review = session.PhraseReview;
        Assert.NotNull(review);
        Assert.Equal(("pedir-agua", "stall-need", "¿Qué necesitas?", true), (review.PhraseId, review.Prompt, review.Request, review.FromBundle));
        Assert.Equal("¿Qué necesitas?", session.ReviewLine);
        Assert.Equal(["¡Hola! Soy la guía. ¿En qué te ayudo?"], opening.Lines);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.PhraseReviewAsked, Detail: "pedir-agua" });
        Assert.Empty(session.Support.Cards); // a review shows no card

        var turn = session.Say("quiero agua, por favor");

        Assert.True(turn.Communicated);
        Assert.Equal("request_item", turn.Intent);
        Assert.Equal("guide", turn.Npc);
        Assert.Contains(turn.Replies.Single(), new[] { "¡Muy bien!", "¡Bien!", "¡Eso es!" }); // the placeholder reply of the bundle's answer_accepted
        var use = Phrases(session).Last();
        Assert.Equal((PhraseIds.RequestWater, PhraseUseKind.Unaided, "guide"), (use.PhraseId, use.Kind, use.Npc));
        Assert.Null(session.PhraseReview); // answered
        Assert.Equal(SupportLevel.None, session.PhraseProgress.LevelOf("pedir-agua")); // the second credited use, after the gap
        Assert.Equal(6, session.PhraseProgress.RecordOf("pedir-agua").Unaided);
    }

    [Fact]
    public void AyudaDuringTheReviewShowsTheCardAndTheAnswerIsSupported()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        _ = session.Say("quiero agua, por favor");
        session.EndTalk();
        Stage.TeleportToNpc(session, "guide");
        session.SkipTime(11 * Minute);
        _ = session.OpenTalk("guide");
        Assert.NotNull(session.PhraseReview);

        _ = session.Say("ayuda");
        Assert.Equal(["pedir-agua"], session.Support.Cards.Select(static c => c.PhraseId));
        Assert.NotNull(session.PhraseReview); // a request for help does not end the review
        _ = session.Say("quiero agua, por favor");

        Assert.Equal(PhraseUseKind.Supported, Phrases(session).Last().Kind);
    }

    [Fact]
    public void TheGuidesWordReviewComesBeforeThePhraseReviewAndTheReviewLineFollowsIt()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "guide");

        _ = session.OpenTalk("guide");

        Assert.Null(session.PhraseReview);
        Assert.Null(session.ReviewLine);
    }

    [Fact]
    public void EveryAudioCueTheSupportCanQueueResolvesToAPreRenderedFileOfS9()
    {
        // The cues go to the line player by text: each text must be a speakable line of the bundle with its normal and its slow file in speech/.
        var sheet = PhraseSheet.Load(TestGame.Bundle);
        var index = new SpeechIndex(SpeechLines.Enumerate(TestGame.Bundle, TestGame.Nouns, sheet));
        var store = new Vocaterra.Nlp.Response.Speech.StaticLineStore(Path.Combine(GameBundle.ResolveDirectory(), SpeechLines.FolderName));
        var texts = new List<(string Speaker, string Text)> { ("guide", "el agua"), ("guide", "el pan") };
        foreach (var id in PhraseIds.All.Where(PhraseIds.HasScaffold))
        {
            var support = new PhraseSupport(sheet, new PhraseProgress(sheet), TestGame.Bundle);
            _ = support.Show("shopkeeper", Prompt(id));
            texts.AddRange(support.TakeCues().Select(static c => (c.Npc, c.Text)));
        }

        Assert.True(texts.Count >= 6);
        foreach (var (speaker, text) in texts)
        {
            var line = index.Find(speaker, text);
            Assert.True(line is not null, $"no speech line for \"{text}\"");
            Assert.True(store.Contains(line.LineId), $"{line.LineId}.wav is missing");
            Assert.True(store.Contains(SpeechFiles.SlowId(line.LineId)), $"{line.LineId}.slow.wav is missing");
        }
    }

    private static string Prompt(string phraseId) => phraseId switch
    {
        "pedir-agua" => "stall-need",
        "donde-esta-el-rio" => "villager-open",
        _ => "stall-offer",
    };

    [Fact]
    public void ThePhraseReviewUsesTheBundlesOwnLineWhenThePoolExists()
    {
        Assert.Equal("guide.review.phrase.pedir_agua", PhraseReviews.PoolOf("pedir-agua"));
        Assert.Equal("guide.review.phrase.donde_esta_el_rio", PhraseReviews.PoolOf("donde-esta-el-rio"));
        var sheet = PhraseSheet.Load(TestGame.Bundle);
        Assert.Equal("¿Qué necesitas?", PhraseReviews.Create("pedir-agua", sheet, TestGame.Bundle)?.Request);
        Assert.Equal("¿Quieres agua?", PhraseReviews.Create("no-gracias", sheet, TestGame.Bundle)?.Request);
        Assert.Equal("¿Qué buscas?", PhraseReviews.Create("donde-esta-el-rio", sheet, TestGame.Bundle)?.Request);
        Assert.Null(PhraseReviews.Create("hola", sheet, TestGame.Bundle)); // no scaffold, nothing to fade, nothing to review
    }
}
