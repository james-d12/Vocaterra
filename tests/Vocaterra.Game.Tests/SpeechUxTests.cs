using Vocaterra.Game.Language.Speech;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Npc;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Simulation.Items;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The speech UX of S7 in the game layer, with no model and no microphone: the uncertainty rule (D-game-9), the turn that is held back when the
/// recognizer was not sure, the choices the repair ladder offers and the turn marked as a choice, the voice events, and the cancel that undoes a
/// turn. A scripted recognizer stands in for Vosk.
/// </summary>
public sealed class SpeechUxTests
{
    private static readonly PcmClip Clip = PcmClip.FromSamples(new short[1600]);

    // A word recognizer that always hears the same words with the same confidences, whatever the clip.
    private sealed class Scripted(string transcript, params double[] confidences) : IWordRecognizer
    {
        public string ModelHash => "test-model";

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            var heard = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words = heard.Select((w, i) => new AsrWord(w, i * 0.3, (i * 0.3) + 0.25, confidences[Math.Min(i, confidences.Length - 1)])).ToList();
            return new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], words, 1.0, ModelHash, request.Mode);
        }
    }

    private static AsrResult Heard(string transcript, params double[] confidences) =>
        new Scripted(transcript, confidences).Recognize(Clip, RecognitionRequest.Open(1));

    private static (VoxelSession Session, NpcService Npcs) AtStall(IWordRecognizer recognizer)
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        session.Conversation.AttachSpeech(new SpokenUtteranceEvaluator(TestGame.Bundle, recognizer));
        return (session, session.Conversation);
    }

    private static GuideTurn SaySpoken(VoxelSession session, string npc = "shopkeeper") =>
        session.Conversation.SaySpoken(npc, Clip, new GuideScene(TestGame.ViewpointOf(session)), session.StoredTime, session.Simulation.ComputeStateDigest());

    private static List<AppliedTurn> Drain(VoxelSession session, int count)
    {
        var all = new List<AppliedTurn>();
        var deadline = Environment.TickCount64 + 30000;
        while (all.Count < count)
        {
            all.AddRange(session.Collect());
            if (all.Count >= count)
            {
                break;
            }

            Assert.True(Environment.TickCount64 < deadline, $"Only {all.Count} of {count} turns came back.");
            Thread.Sleep(2);
        }

        return all;
    }

    [Fact]
    public void TheMeanWordConfidenceLeavesOutTheUnknownMarksAndTheThresholdIsAConstant()
    {
        Assert.Equal(0.8, UncertaintyRule.MeanWordConfidence(Heard("quiero agua", 0.6, 1.0)), 6);
        Assert.Equal(0.8, UncertaintyRule.MeanWordConfidence(Heard("[unk] quiero agua [unk]", 1.0, 0.6, 1.0, 0.2)), 6); // [unk] is not a word
        Assert.Equal(0.0, UncertaintyRule.MeanWordConfidence(Heard("[unk]", 1.0)));
        Assert.Equal(0.9, UncertaintyRule.MeanWordConfidence(new AsrResult(string.Empty, [], [], 0.9, "m", RecognitionMode.Open)), 6); // no words: the result's own
        Assert.Equal(0.55, UncertaintyRule.ConfidenceThreshold);
    }

    [Fact]
    public void ALineIsUncertainWhenNotUnderstoodOrWhenUnderstoodOnWordsUnderTheThreshold()
    {
        Assert.True(UncertaintyRule.IsUncertain(communicated: false, confidence: 0.99));
        Assert.True(UncertaintyRule.IsUncertain(communicated: true, confidence: 0.54));
        Assert.False(UncertaintyRule.IsUncertain(communicated: true, confidence: 0.55));
        Assert.False(UncertaintyRule.IsUncertain(communicated: true, confidence: 1.0));
        Assert.False(UncertaintyRule.IsUncertain(communicated: true, confidence: null)); // a typed line has no confidence
        Assert.True(UncertaintyRule.IsUncertain(communicated: false, confidence: null)); // but a typed line the NPC did not understand is still "not understood"

        Assert.True(UncertaintyRule.IsTooUnsure(communicated: true, confidence: 0.3));
        Assert.False(UncertaintyRule.IsTooUnsure(communicated: false, confidence: 0.3)); // the core's own repair handles a line it did not understand
        Assert.False(UncertaintyRule.IsTooUnsure(communicated: true, confidence: 0.8));
    }

    [Fact]
    public void AnUnderstoodLineOnWordsTheRecognizerWasNotSureOfIsHeldBackAndNothingHappens()
    {
        var (session, npcs) = AtStall(new Scripted("quiero agua por favor", 0.2, 0.3, 0.6, 0.5));
        var before = npcs.ContextOf("shopkeeper");
        var records = npcs.Records.Count;

        var turn = SaySpoken(session);

        Assert.True(turn.Unsure);
        Assert.True(turn.Uncertain);
        Assert.False(turn.Communicated);
        Assert.Equal(0.4, turn.Confidence!.Value, 6);
        Assert.Equal("quiero agua por favor", turn.Line);
        Assert.Equal(InputKind.Spoken, turn.Input);
        Assert.Empty(turn.HostEvents);
        Assert.Empty(turn.Errors);
        Assert.Null(turn.Repair);
        Assert.Null(turn.Choices);
        Assert.Equal("asking", turn.AgentState); // the shopkeeper did not move on
        Assert.Same(before, npcs.ContextOf("shopkeeper")); // not the state, the learner, the repeat counts: nothing
        Assert.Equal(records, npcs.Records.Count); // and no replay record: the turn was not completed
        var uncertain = TestGame.Bundle.Dialogue.Pools["guide.uncertain"].Variants.Select(static v => v.Text);
        Assert.Contains(turn.Replies.Single(), uncertain);
    }

    [Fact]
    public void AnUnderstoodLineOnSureWordsPassesTheRule()
    {
        var (session, _) = AtStall(new Scripted("quiero agua por favor", 0.9, 0.9, 1.0, 1.0));

        var turn = SaySpoken(session);

        Assert.True(turn.Communicated);
        Assert.False(turn.Uncertain);
        Assert.Equal("request_item", turn.Intent);
        Assert.Contains("stall.give_water", turn.HostEvents);
    }

    [Fact]
    public void ALineTheCoreDidNotUnderstandIsUncertainRunsTheRepairLadderAndCostsTheLearnerNothing()
    {
        var (session, npcs) = AtStall(new Scripted("el gato come queso", 0.99));
        var learner = npcs.ContextOf("shopkeeper").Learner;

        var first = SaySpoken(session);

        Assert.True(first.Uncertain);
        Assert.False(first.Unsure); // the core ran: the ladder moves on
        Assert.False(first.Communicated);
        Assert.Equal(RepairKind.AskAgain, first.RepairKind);
        Assert.Same(learner, npcs.ContextOf("shopkeeper").Learner); // no learner-model penalty for the recognizer's trouble
        Assert.Equal(1, npcs.ContextOf("shopkeeper").RepeatCountsByTag["not_communicated"]);
        Assert.Equal(RepairKind.SlowRepeat, SaySpoken(session).RepairKind);

        var third = SaySpoken(session);
        Assert.Equal(RepairKind.OfferChoices, third.RepairKind);
        Assert.Equal(["quiero agua por favor", "quiero pan por favor", "quiero fruta por favor"], third.Choices); // one canonical per answer, declared order (M29f)
    }

    [Fact]
    public void ATypedLineTheCoreDidNotUnderstandStillMarksTheLearnerBecauseItIsALanguageFailure()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        var learner = session.Conversation.ContextOf("shopkeeper").Learner;

        var turn = session.Say("el gato come queso");

        Assert.False(turn.Uncertain); // only a spoken line is an uncertain recognition
        Assert.False(turn.Communicated);
        Assert.NotSame(learner, session.Conversation.ContextOf("shopkeeper").Learner);
    }

    // The turn as the worker would hand it to the session: applied on the caller's thread (the same call Collect makes).
    private static AppliedTurn Apply(VoxelSession session, GuideTurn turn)
    {
        var request = new TurnRequest(
            1, TurnSource.Clip, null, Clip, null, new GuideScene(TestGame.ViewpointOf(session)), session.StoredTime, session.Simulation.ComputeStateDigest(), null, session.Simulation.Tick, turn.Npc);
        return session.Apply(new TurnOutcome(request, turn, null, new TurnTimings(0, 0, 0, false)));
    }

    [Fact]
    public void AnUncertainTurnIsOnlyAVoiceEventNeverAPhraseUseAnErrorOrWordCredit()
    {
        var (session, npcs) = AtStall(new Scripted("quiero agua por favor", 0.2));

        var unsure = Apply(session, SaySpoken(session));

        Assert.True(unsure.Turn!.Unsure);
        Assert.Equal(["voice uncertain at shopkeeper (\"quiero agua por favor\")"], session.Events.OfType<VoiceEvent>().Select(static e => e.Text));
        Assert.Empty(session.Events.OfType<PhraseUse>()); // not a phrase use, not a language failure
        Assert.Equal(0, session.Words.RecordOf("agua").CreditUnits);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle)); // and the world did not move
        Assert.Equal("asking", npcs.ContextOf("shopkeeper").AgentState.StateId);

        // A line the core did not understand: the same voice event, no phrase use, and the ladder goes on.
        npcs.AttachSpeech(new SpokenUtteranceEvaluator(TestGame.Bundle, new Scripted("el gato come queso", 0.99)));
        var lost = Apply(session, SaySpoken(session));
        Assert.True(lost.Turn!.Uncertain);
        Assert.Equal(2, session.Events.OfType<VoiceEvent>().Count());
        Assert.Empty(session.Events.OfType<PhraseUse>());

        // A line that was understood on sure words is the phrase use and raises no voice event.
        npcs.AttachSpeech(new SpokenUtteranceEvaluator(TestGame.Bundle, new Scripted("quiero agua por favor", 0.95)));
        var good = Apply(session, SaySpoken(session));
        Assert.True(good.Turn!.Communicated);
        Assert.Equal(2, session.Events.OfType<VoiceEvent>().Count());
        var use = Assert.Single(session.Events.OfType<PhraseUse>());
        Assert.Equal((PhraseIds.RequestWater, PhraseUseKind.Supported), (use.PhraseId, use.Kind));
    }

    [Fact]
    public void ACancelledOutcomeIsDroppedByTheSessionAndAMicCheckOutcomeIsNoTurn()
    {
        var session = TestGame.NewSession();
        var request = new TurnRequest(
            1, TurnSource.Typed, "quiero agua, por favor", null, null, new GuideScene(TestGame.ViewpointOf(session)), session.StoredTime, session.Simulation.ComputeStateDigest(), null, 0, "shopkeeper");
        var turn = session.Conversation.Say("shopkeeper", "quiero agua, por favor", request.Scene, request.Now, request.HostStateHash);

        var dropped = session.Apply(new TurnOutcome(request, turn, null, new TurnTimings(0, 0, 0, false), Cancelled: true));
        var heard = session.Apply(new TurnOutcome(request, null, null, new TurnTimings(0, 0, 0, false), MicCheck: new MicCheckResult("hola", true, 0.2)));

        Assert.Null(dropped.Words); // nothing of a cancelled turn is applied, even when its outcome carries one
        Assert.Empty(session.Events);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Null(heard.Turn);
        Assert.Equal("hola", heard.Outcome.MicCheck!.Heard);
        Assert.Empty(session.Events);
    }

    [Fact]
    public void TheVoiceEventsHaveStableWireNamesAndTheMicCheckHasNoNpc()
    {
        Assert.Equal(
            ["uncertain", "cancel", "timeout", "mic_check_passed", "mic_check_failed", "mic_check_skipped", "replay", "slower", "hint", "no_se", "typed_fallback", "choice"],
            Enum.GetValues<VoiceEventKind>().Select(VoiceEventNames.Of));

        var session = TestGame.NewSession();
        session.LogMicCheck(VoiceEventKind.MicCheckPassed, "hola");
        session.LogMicCheck(VoiceEventKind.MicCheckSkipped);

        Assert.Equal(["voice mic_check_passed (\"hola\")", "voice mic_check_skipped"], session.Events.Select(static e => e.Text));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.LogMicCheck(VoiceEventKind.Cancel));
    }

    [Fact]
    public void ThreeLinesTheCoreDidNotUnderstandOfferChoicesAndAPickedChoiceIsATurnMarkedChoiceThatEarnsNoCredit()
    {
        using var session = TestGame.NewSession(credit: CreditRules.FullTyped);
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        Assert.True(session.StartWorker(loadSpeech: false).WaitUntilReady(30000));

        for (var i = 0; i < 2; i++)
        {
            Assert.Null(session.Say("el gato come queso").Choices);
        }

        var offered = session.Say("el gato come queso");
        Assert.Equal(RepairKind.OfferChoices, offered.RepairKind);
        var options = session.Phrases.OptionsFor(offered.Choices!);
        Assert.Equal(["Quiero agua, por favor.", "Quiero pan por favor.", "Quiero fruta por favor."], options.Select(static o => o.Label));

        _ = session.SubmitChoice(options[0].Line);
        var picked = Drain(session, 1).Single().Turn!;

        Assert.True(picked.Choice);
        Assert.Equal(InputKind.Button, picked.Input);
        Assert.Equal("quiero agua por favor", picked.Line);
        Assert.Equal("request_item", picked.Intent);
        Assert.True(picked.Communicated);
        Assert.Contains("stall.give_water", picked.HostEvents); // the choice works like the phrase: the shopkeeper hands over the bottle
        var use = Assert.Single(session.Events.OfType<PhraseUse>());
        Assert.Equal((PhraseIds.RequestWater, PhraseUseKind.Choice), (use.PhraseId, use.Kind)); // a recognition signal, never counted as production
        Assert.Equal(["voice choice at shopkeeper (\"quiero agua por favor\")"], session.Events.OfType<VoiceEvent>().Select(static e => e.Text));
        Assert.Equal(0, session.Words.RecordOf("agua").CreditUnits); // no word credit for a button
        Assert.Equal(0, session.Words.RecordOf("agua").Spoken);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
    }

    [Fact]
    public void TheHelpAndNoSeButtonsSendTheirLinesAsButtonTurnsThatAreNeverAFailure()
    {
        using var session = TestGame.NewSession(credit: CreditRules.FullTyped);
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        Assert.True(session.StartWorker(loadSpeech: false).WaitUntilReady(30000));

        _ = session.SubmitHelp();
        var help = Drain(session, 1).Single().Turn!;
        _ = session.SubmitDontKnow();
        var unknown = Drain(session, 1).Single().Turn!;

        Assert.Equal((GuideSession.HelpLine, "ask_help", InputKind.Button, false), (help.Line, help.Intent, help.Input, help.Choice));
        Assert.Equal((GuideSession.DontKnowLine, "dont_know", InputKind.Button, false), (unknown.Line, unknown.Intent, unknown.Input, unknown.Choice));
        Assert.All(new[] { help, unknown }, static t => Assert.True(t.Communicated));
        var uses = session.Events.OfType<PhraseUse>().ToList();
        Assert.Equal(["ayuda", "no-se"], uses.Where(static u => u.Kind == PhraseUseKind.DontKnow).Select(static u => u.PhraseId));
        Assert.DoesNotContain(uses, static u => u.Kind is PhraseUseKind.Supported or PhraseUseKind.Unaided or PhraseUseKind.Choice); // never a production
        Assert.Contains(uses, static u => u.Kind == PhraseUseKind.FailForward); // help, then "no sé": the stall goes on without the phrase (S6)
        Assert.Equal(["voice hint at shopkeeper", "voice no_se at shopkeeper"], session.Events.OfType<VoiceEvent>().Select(static e => e.Text));
    }

    [Fact]
    public void AnUnsureTurnDoesNotForgetThatChoicesWereOffered()
    {
        // The line after an offer of choices is marked as a choice even when a held-back line came in between.
        var (session, npcs) = AtStall(new Scripted("el gato come queso", 0.99));
        _ = SaySpoken(session);
        _ = SaySpoken(session);
        var offered = SaySpoken(session);
        Assert.Equal(RepairKind.OfferChoices, offered.RepairKind);

        // The same line, now an unsure recognition (the words are understood but not sure): held back, nothing changes.
        npcs.AttachSpeech(new SpokenUtteranceEvaluator(TestGame.Bundle, new Scripted("quiero agua por favor", 0.2)));
        var before = npcs.ContextOf("shopkeeper");
        var unsure = SaySpoken(session);

        Assert.True(unsure.Unsure);
        Assert.Same(before, npcs.ContextOf("shopkeeper"));
    }

    [Fact]
    public void ACancelBeforeTheWorkerGetsToATurnSkipsItAndTheOutcomeCarriesNoTurn()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        var before = session.Conversation.ContextOf("shopkeeper");
        using var worker = new TurnWorker(session.Conversation, TestGame.Bundle, null, loadSpeech: false);
        var request = new TurnRequest(
            1, TurnSource.Typed, "quiero agua, por favor", null, null, new GuideScene(TestGame.ViewpointOf(session)), session.StoredTime, session.Simulation.ComputeStateDigest(), null, 0, "shopkeeper");
        worker.Submit(request); // queued: the thread has not started

        Assert.Equal(1, worker.CancelPending());
        Assert.Equal(0, worker.CancelPending()); // already cancelled
        worker.Start();
        Assert.True(worker.WaitUntilReady(30000));
        TurnOutcome? outcome = null;
        var deadline = Environment.TickCount64 + 30000;
        while (outcome is null && Environment.TickCount64 < deadline)
        {
            _ = worker.TryTake(out outcome);
            Thread.Sleep(2);
        }

        Assert.NotNull(outcome);
        Assert.True(outcome.Cancelled);
        Assert.Null(outcome.Turn);
        Assert.Null(outcome.Problem);
        Assert.Equal(0, worker.Pending); // counted down like any outcome
        Assert.Same(before, session.Conversation.ContextOf("shopkeeper")); // the turn never ran
    }

    [Fact]
    public void ATurnCancelledWhileItRanIsRolledBackSoItNeverHappenedAndTheSameLineThenGivesTheSameTurn()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        var npcs = session.Conversation;
        var before = npcs.ContextOf("shopkeeper");
        var records = npcs.Records.Count;
        using var worker = new TurnWorker(npcs, TestGame.Bundle, null, loadSpeech: false);
        var request = new TurnRequest(
            1, TurnSource.Typed, "quiero agua, por favor", null, null, new GuideScene(TestGame.ViewpointOf(session)), session.StoredTime, session.Simulation.ComputeStateDigest(), null, 0, "shopkeeper");

        var outcome = worker.Handle(request, null, out var checkpoint); // the worker ran the turn: the shopkeeper moved on and handed over the bottle
        var ran = outcome.Turn!;
        Assert.Equal("offering", npcs.ContextOf("shopkeeper").AgentState.StateId);
        Assert.True(npcs.Records.Count > records);

        request.Cancellation.Cancel(); // Esc arrived while it ran
        var settled = worker.Settle(request, outcome, checkpoint);

        Assert.True(settled.Cancelled);
        Assert.Null(settled.Turn);
        Assert.Same(before, npcs.ContextOf("shopkeeper")); // the shopkeeper is where it was: asking, the bottle not given
        Assert.Equal(records, npcs.Records.Count);

        // It really never happened: the same line now gives the same turn, including the bottle.
        var again = npcs.Say("shopkeeper", "quiero agua, por favor", request.Scene, request.Now, request.HostStateHash);
        Assert.Equal(ran.GuideLines, again.GuideLines);
        Assert.Equal(ran.HostEvents, again.HostEvents);
        Assert.Contains("stall.give_water", again.HostEvents);
    }

    [Fact]
    public void ACancelWhileATurnIsOnTheWorkerDropsItSoNothingOfItReachesTheWorldWhicheverWayTheRaceGoes()
    {
        using var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        _ = session.StartWorker(loadSpeech: false); // not waited for: the worker is still loading, so the request waits behind it

        _ = session.SubmitTyped("quiero agua, por favor");
        var cancelled = session.CancelPending();
        var outcome = Drain(session, 1).Single();

        Assert.Equal(0, session.PendingTurns);
        if (cancelled > 0)
        {
            Assert.True(outcome.Outcome.Cancelled);
            Assert.Null(outcome.Turn);
            Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle));
            Assert.Empty(session.Events.OfType<PhraseUse>());
            Assert.Equal("asking", session.Conversation.ContextOf("shopkeeper").AgentState.StateId);
            Assert.Equal(["voice cancel at shopkeeper"], session.Events.OfType<VoiceEvent>().Select(static e => e.Text));
        }
        else
        {
            Assert.NotNull(outcome.Turn); // the turn was done before Esc: it is applied whole
            Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
        }
    }
}
