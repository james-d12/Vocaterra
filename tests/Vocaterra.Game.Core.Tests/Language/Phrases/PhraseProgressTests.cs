using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Language.Phrases;

/// <summary>
/// The phrase progress of S8 on its own, fed with hand-made <see cref="PhraseUse"/> events: the mapping from each kind of use to the counts, the typed
/// half credit, the support level stepping down only after the spacing gap and up after "no sé", a choice or a fail-forward, the due list, and
/// determinism (the same events give the same progress; nothing reads a clock).
/// </summary>
public sealed class PhraseProgressTests
{
    private const long T0 = 1_000_000;
    private const long Minute = 60;

    private static readonly CreditRules Default = CreditRules.Default; // spoken full, typed half

    private static PhraseProgress New(int session = 1) => new(RouteScene.DataOf(TestGame.Bundle).Phrases, session, TestGame.Data.Npcs.ScaffoldPhrases);

    private static PhraseUse Use(
        string phrase,
        PhraseUseKind kind,
        long time = T0,
        InputKind input = InputKind.Spoken,
        bool correct = true,
        string npc = "shopkeeper",
        string prompt = "stall-need",
        IReadOnlyList<string>? inPlay = null) =>
        new(time * 60, time, phrase, kind, npc, prompt, "line", null, input, correct, SupportLevel.None, inPlay);

    [Fact]
    public void EveryRowOfTheSheetHasARecordAndOnlyTheTaughtPhrasesStartWithAScaffold()
    {
        var progress = New();

        Assert.Equal(173, progress.Records.Count);
        Assert.Equal(progress.Sheet.Rows.Select(static r => r.Id), progress.Records.Select(static r => r.Id));
        Assert.Equal(
            ["como-te-llamas", "cuanto-cuesta", "cuanto-es-todo", "donde-esta-el-rio", "donde-esta-la-herreria", "donde-esta-la-panaderia", "hay-pan", "no-gracias", "pedir-agua", "puedo-pagar-con-fruta", "que-haces", "quiero-dos-panes", "quiero-tres-manzanas-rojas", "si-por-favor", "vendo-madera"],
            progress.Records.Where(static r => r.Level == SupportLevel.EnglishAndCard).Select(static r => r.Id).Order(StringComparer.Ordinal)); // the NPCs' focus: the stall's, the river's and, since V12b, the village's (the farmer's apples since V13)
        Assert.All(progress.Records.Where(r => !progress.HasScaffold(r.Id)), static r => Assert.Equal(SupportLevel.None, r.Level));
    }

    [Fact]
    public void SupportedUnaidedAndTransferUsesCountByTheCreditOfTheInput()
    {
        var progress = New();

        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported), Default);
        _ = progress.Apply(Use("hola", PhraseUseKind.Unaided, input: InputKind.Typed), Default);
        _ = progress.Apply(Use("si-por-favor", PhraseUseKind.Transfer, npc: "guide", prompt: "guide-offer"), Default);

        Assert.Equal((6, 0, 0), (progress.RecordOf("pedir-agua").Supported, progress.RecordOf("pedir-agua").Unaided, progress.RecordOf("pedir-agua").Transfer));
        Assert.Equal((0, 3, 0), (progress.RecordOf("hola").Supported, progress.RecordOf("hola").Unaided, progress.RecordOf("hola").Transfer)); // typed: half a use
        Assert.Equal((0, 0, 6), (progress.RecordOf("si-por-favor").Supported, progress.RecordOf("si-por-favor").Unaided, progress.RecordOf("si-por-favor").Transfer));
        Assert.Equal(0.5, progress.RecordOf("hola").UnaidedUses);
        Assert.Equal(1.0, progress.RecordOf("pedir-agua").SupportedUses);
    }

    [Fact]
    public void ATypedUseEarnsHalfCreditAndASpokenOneFullCreditWithTheDefaultRulesAndBothFullWithFullTyped()
    {
        var defaults = New();
        _ = defaults.Apply(Use("pedir-agua", PhraseUseKind.Supported, input: InputKind.Typed), Default);
        var full = New();
        _ = full.Apply(Use("pedir-agua", PhraseUseKind.Supported, input: InputKind.Typed), CreditRules.FullTyped);

        Assert.Equal(3, defaults.RecordOf("pedir-agua").Supported);
        Assert.Equal(SupportLevel.EnglishAndCard, defaults.LevelOf("pedir-agua")); // half a use is not a step
        Assert.Equal(6, full.RecordOf("pedir-agua").Supported);
        Assert.Equal(SupportLevel.CardAndAudio, full.LevelOf("pedir-agua")); // the first step: the English goes, the card and the audio stay
    }

    [Fact]
    public void ATypedUseStepsTheLevelDownOnlyWhenTwoCreditedUsesAddUpToAFullOne()
    {
        var progress = New();
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0), Default); // the English rung goes at once: card and audio now

        var first = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0 + (11 * Minute), InputKind.Typed), Default).Single();
        var early = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0 + (30 * Minute), InputKind.Typed), Default).Single();
        var second = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0 + (72 * Minute), InputKind.Typed), Default).Single();

        Assert.Equal(PhraseStep.Credited, first.Step);
        Assert.Equal(PhraseStep.TooEarly, early.Step);
        Assert.Equal(41 * Minute, early.SecondsRemaining); // an hour after the second credited use, which was at T0 + 11 min
        Assert.Equal(PhraseStep.SteppedDown, second.Step);
        Assert.Equal(SupportLevel.AudioOnly, progress.LevelOf("pedir-agua"));
        Assert.Equal(6 + 9, progress.RecordOf("pedir-agua").Supported); // every use counted: one full, three halves
    }

    [Fact]
    public void ATypedUseAtTheEnglishRungNeedsTwoOfThemButNoGapBetween()
    {
        var progress = New();

        var first = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0, InputKind.Typed), Default).Single();
        var second = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0 + 5, InputKind.Typed), Default).Single();

        Assert.Equal((PhraseStep.Credited, SupportLevel.EnglishAndCard), (first.Step, first.Level));
        Assert.Equal((PhraseStep.SteppedDown, SupportLevel.CardAndAudio), (second.Step, second.Level)); // five seconds later: the English rung has no gap
    }

    [Fact]
    public void ASpokenUseStepsDownAtOnceAndTheNextOnlyAfterTheGap()
    {
        var progress = New();

        var first = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0), Default).Single();
        var early = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0 + (9 * Minute)), Default).Single();
        var late = progress.Apply(Use("pedir-agua", PhraseUseKind.Unaided, T0 + (11 * Minute)), Default).Single();

        Assert.Equal((PhraseStep.SteppedDown, SupportLevel.CardAndAudio), (first.Step, first.Level)); // the English rung: no gap needed
        Assert.Equal((PhraseStep.TooEarly, SupportLevel.CardAndAudio), (early.Step, early.Level)); // the card rung waits for the gap: logged, no step
        Assert.Equal((PhraseStep.SteppedDown, SupportLevel.AudioOnly), (late.Step, late.Level));
        var record = progress.RecordOf("pedir-agua");
        Assert.Equal((6 + 6, 6, 2), (record.Supported, record.Unaided, record.CreditedUses));
    }

    [Fact]
    public void TheGapAfterTheSecondCreditedUseIsAnHourAndANewSessionLiftsIt()
    {
        var progress = New();
        _ = progress.Apply(Use("si-por-favor", PhraseUseKind.Supported, T0, prompt: "stall-offer"), Default);
        _ = progress.Apply(Use("si-por-favor", PhraseUseKind.Supported, T0 + (11 * Minute), prompt: "stall-offer"), Default);

        Assert.Equal(60 * Minute, progress.SecondsUntilNextCredit("si-por-favor", T0 + (11 * Minute), Default));
        progress.Session = 2;
        Assert.Equal(0L, progress.SecondsUntilNextCredit("si-por-favor", T0 + (12 * Minute), Default));
        Assert.Null(progress.SecondsUntilNextCredit("hola", T0, Default)); // no credited use yet
    }

    [Fact]
    public void ASlipIsLoggedAsASlipAndNeverCountsOrStepsTheLevel()
    {
        var progress = New();

        var result = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, correct: false), Default).Single();

        Assert.Equal(PhraseStep.Logged, result.Step);
        var record = progress.RecordOf("pedir-agua");
        Assert.Equal((0, 1, SupportLevel.EnglishAndCard, T0), (record.Supported, record.Slips, record.Level, record.LastUseAt));
    }

    [Fact]
    public void ANoSeOnAPhraseInPlayCountsOnItAndStepsItsLevelUpToTheEnglish()
    {
        var progress = New();
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0), Default); // english to card-and-audio
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Unaided, T0 + (11 * Minute)), Default); // to audio-only
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Unaided, T0 + (72 * Minute)), Default); // to none
        Assert.Equal(SupportLevel.None, progress.LevelOf("pedir-agua"));

        var results = progress.Apply(Use("no-se", PhraseUseKind.DontKnow, T0 + (73 * Minute), inPlay: ["pedir-agua"]), Default);

        Assert.Equal([("pedir-agua", PhraseStep.SteppedUp, SupportLevel.AudioOnly)], results.Select(static r => (r.PhraseId, r.Step, r.Level)));
        Assert.Equal(1, progress.RecordOf("pedir-agua").DontKnow);
        Assert.Equal(0, progress.RecordOf("no-se").DontKnow); // the service phrase is counted only when nothing else was in play
        Assert.Equal(6, progress.RecordOf("no-se").Unaided); // but typed or said, it is a use of its own
        _ = progress.Apply(Use("ayuda", PhraseUseKind.DontKnow, T0 + (74 * Minute), inPlay: ["pedir-agua"]), Default);
        Assert.Equal((SupportLevel.CardAndAudio, 2), (progress.LevelOf("pedir-agua"), progress.RecordOf("pedir-agua").DontKnow));
        _ = progress.Apply(Use("ayuda", PhraseUseKind.DontKnow, T0 + (75 * Minute), inPlay: ["pedir-agua"]), Default);
        _ = progress.Apply(Use("ayuda", PhraseUseKind.DontKnow, T0 + (76 * Minute), inPlay: ["pedir-agua"]), Default);
        Assert.Equal(SupportLevel.EnglishAndCard, progress.LevelOf("pedir-agua")); // help brings the English back, and no rung is above it
    }

    [Fact]
    public void ANoSeWithNothingInPlayCountsOnTheServicePhraseAndAButtonEarnsNoUse()
    {
        var progress = New();

        _ = progress.Apply(Use("no-se", PhraseUseKind.DontKnow, input: InputKind.Button, prompt: "guide-open", npc: "guide"), Default);

        var record = progress.RecordOf("no-se");
        Assert.Equal((1, 0, SupportLevel.None), (record.DontKnow, record.Unaided, record.Level));
    }

    [Fact]
    public void AChoiceIsRecognitionOnlyAndStepsTheLevelUp()
    {
        var progress = New();
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0), Default); // card-and-audio now

        var result = progress.Apply(Use("pedir-agua", PhraseUseKind.Choice, T0 + (1 * Minute), InputKind.Button), Default).Single();

        Assert.Equal((PhraseStep.SteppedUp, SupportLevel.EnglishAndCard), (result.Step, result.Level));
        var record = progress.RecordOf("pedir-agua");
        Assert.Equal((1, 6), (record.ChoiceOnly, record.Supported)); // no production credit for the choice
    }

    [Fact]
    public void AFailForwardStepsTheLevelUpAndCountsNoProduction()
    {
        var progress = New();
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0), Default);
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Unaided, T0 + (11 * Minute)), Default); // audio-only

        var result = progress.Apply(Use("pedir-agua", PhraseUseKind.FailForward, T0 + (12 * Minute)), Default).Single();

        Assert.Equal((PhraseStep.SteppedUp, SupportLevel.CardAndAudio), (result.Step, result.Level));
        var record = progress.RecordOf("pedir-agua");
        Assert.Equal((1, 12), (record.FailForward, record.Supported + record.Unaided));
    }

    [Fact]
    public void ASteppedUpLevelClearsTheCreditTowardTheNextStepAndAPhraseWithNoScaffoldNeverMoves()
    {
        var progress = New();
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0, InputKind.Typed), Default); // half a step banked
        Assert.Equal(3, progress.RecordOf("pedir-agua").StepUnits);
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Choice, T0 + Minute, InputKind.Button), Default);
        Assert.Equal(0, progress.RecordOf("pedir-agua").StepUnits);

        for (var i = 0; i < 4; i++)
        {
            _ = progress.Apply(Use("hola", PhraseUseKind.Unaided, T0 + (i * 2 * 3600)), Default);
        }

        _ = progress.Apply(Use("hola", PhraseUseKind.Choice, T0 + 99_999, InputKind.Button), Default);
        Assert.Equal(SupportLevel.None, progress.LevelOf("hola"));
    }

    [Fact]
    public void APhraseIsDueForTheGuidesReviewOnceItsGapHasPassedAndItStillHasHelpToFade()
    {
        var progress = New();
        Assert.Empty(progress.DuePhrases(T0, Default)); // nothing credited yet

        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0), Default); // card-and-audio, credited at T0
        _ = progress.Apply(Use("si-por-favor", PhraseUseKind.Supported, T0 + 60, prompt: "stall-offer"), Default);

        Assert.Empty(progress.DuePhrases(T0 + (5 * Minute), Default)); // inside the gap
        var due = progress.DuePhrases(T0 + (20 * Minute), Default);
        Assert.Equal(["pedir-agua", "si-por-favor"], due.Select(static d => d.PhraseId)); // the most overdue first
        Assert.Equal(10 * Minute, due[0].OverdueSeconds);

        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Unaided, T0 + (20 * Minute)), Default); // audio-only
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Unaided, T0 + (82 * Minute)), Default); // none: nothing left to fade
        Assert.DoesNotContain(progress.DuePhrases(T0 + (300 * Minute), Default), static d => d.PhraseId == "pedir-agua");

        progress.Session = 2; // a new session lifts every gap
        Assert.Contains(progress.DuePhrases(T0 + 90, Default), static d => d.PhraseId == "si-por-favor");
    }

    [Fact]
    public void TheSameEventsGiveTheSameProgressInAnyRun()
    {
        PhraseUse[] events =
        [
            Use("pedir-agua", PhraseUseKind.Supported, T0),
            Use("no-se", PhraseUseKind.DontKnow, T0 + 5, InputKind.Button, inPlay: ["pedir-agua"]),
            Use("si-por-favor", PhraseUseKind.Supported, T0 + 9, prompt: "stall-offer", input: InputKind.Typed),
            Use("donde-esta-el-rio", PhraseUseKind.Unaided, T0 + 700, npc: "villager", prompt: "villager-open"),
            Use("pedir-agua", PhraseUseKind.FailForward, T0 + 800),
        ];
        var a = New();
        var b = New();

        foreach (var e in events)
        {
            _ = a.Apply(e, Default);
            _ = b.Apply(e, Default);
        }

        Assert.Equal(a.Records, b.Records);
        Assert.Equal(a.SummaryLines(), b.SummaryLines());
        Assert.Equal(a.ConsoleLine("pedir-agua"), b.ConsoleLine("pedir-agua"));
    }

    [Fact]
    public void ARestoredProgressContinuesWhereTheSavedOneStopped()
    {
        var first = New();
        _ = first.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0), Default);
        var second = New();

        second.Restore(first.Records);

        Assert.Equal(first.Records, second.Records);
    }

    [Fact]
    public void TheConsoleLineNamesTheCountsInWholeUsesAndTheLevel()
    {
        var progress = New();
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, T0), Default);
        progress.NoteCardShown("pedir-agua");
        _ = progress.Apply(Use("hola", PhraseUseKind.Unaided, T0, InputKind.Typed), Default);

        Assert.Equal("phrase: pedir-agua, supported 1, unaided 0, choice 0, no sé 0, transfer 0, fail-forward 0, cards 1, support card-and-audio", progress.ConsoleLine("pedir-agua"));
        Assert.StartsWith("phrase: hola, supported 0, unaided 0.5,", progress.ConsoleLine("hola"), StringComparison.Ordinal);
        Assert.Equal(2, progress.SummaryLines().Count);
    }
}
