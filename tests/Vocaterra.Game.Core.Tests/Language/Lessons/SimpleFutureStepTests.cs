using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Es;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The simple future at a lesson step (VILLAGE_PLAN 3.8, V16), before any lesson of the bundle has one (session 9, V17): a step may expect <c>fut</c>, the finite future
/// (<c>compraré</c>) is its answer, and correct Spanish for the future is never a miss: the near future or a present with a future-time word is credited with the step's
/// <c>futureNote</c>, and at an <c>ir-a</c> step the simple future is credited with its <c>presentNote</c>. The fixtures ask <c>voy-a-comprar-pan</c> at the fisher's
/// <c>¿Qué vas a hacer allí?</c> (<c>lesson-plan-there</c>), whose intent, <c>state_action</c>, the core recognises in every tense.
/// </summary>
public sealed class SimpleFutureStepTests
{
    private const string FutureRetry = "Say it with the future: “compraré”.";

    private const string FutureNote = "That is right too. Here we practise “compraré”: Mañana compraré pan.";

    private const string NearRetry = "Say it with “voy a”: Voy a comprar pan.";

    private const string PresentNote = "That is right too. Here we practise “voy a comprar”: Voy a comprar pan.";

    private static string Lesson(string tense, string level = "a2") => $$"""
        { "schemaVersion": 1, "lessons": [ { "id": "t", "npc": "guide", "level": "{{level}}", "title": "T", "english": "E", "steps": [
          { "id": "say", "type": "repeat", "phrase": "voy-a-comprar-pan", "prompt": "lesson-plan-there", "person": "1sg", "tense": "{{tense}}", "attempts": 3, "english": "Say it.",
            "personRetry": "Say it for yourself.", "tenseRetry": "{{(tense == "fut" ? FutureRetry : NearRetry)}}", "presentNote": "{{PresentNote}}", "futureNote": "{{FutureNote}}" },
          { "id": "end", "type": "explain", "english": "Done." } ] } ] }
        """;

    private static VoxelSession AtStep(string tense)
    {
        var session = TestGame.NewSession(lessons: TestGame.Lessons(Lesson(tense)));
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        Assert.True(session.Lessons.Start("t"));
        return session;
    }

    private static LessonEvent Done(VoxelSession session) =>
        Assert.Single(session.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "say");

    // ---- The step

    [Fact]
    public void AFutStepLoadsWithItsFutureNoteAndNoPresentNote()
    {
        var future = Assert.Single(TestGame.Lessons(Lesson("fut")).Lessons).Steps[0];
        Assert.Equal((TaughtVerbForm.SimpleFuture, FutureRetry, FutureNote), (future.Tense, future.TenseRetry, future.FutureNote));
        Assert.Null(future.PresentNote); // a fut step's one note: the futureNote, whichever other right form was said

        var near = Assert.Single(TestGame.Lessons(Lesson("ir-a")).Lessons).Steps[0];
        Assert.Equal((TaughtVerbForm.NearFuture, PresentNote), (near.Tense, near.PresentNote));
        Assert.Null(near.FutureNote);

        var other = Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("future")));
        Assert.Contains("\"tense\" is pres, pret, ir-a or fut", other.Message, StringComparison.Ordinal);
    }

    // ---- The simple future is the answer of a fut step

    [Theory]
    [InlineData("compraré pan")]
    [InlineData("mañana compraré pan")]
    [InlineData("Mañana compraré pan")]
    [InlineData("la semana que viene compraré pan")]
    public void TheSimpleFutureIsTheAnswerOfAFutStep(string said)
    {
        var session = AtStep("fut");

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal));
        Assert.Equal("end", session.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf("t")[^1].Outcome);
        Assert.DoesNotContain("credited", Done(session).Detail ?? string.Empty, StringComparison.Ordinal); // the step's own form: no note
        Assert.NotEqual(FutureNote, session.Lessons.View!.Hint);
    }

    [Fact]
    public void TheCoreReadsTheSimpleFutureAsFut()
    {
        // The label the core's readings give a finite simple-future form is the lexicon's own feature, "fut" (the Wiktionary import), which the step compares as it is.
        foreach (var (said, form, lemma, person) in new[] { ("compraré pan", "compraré", "comprar", "1"), ("iré al río", "iré", "ir", "1"), ("Luz hará pan", "hará", "hacer", "3") })
        {
            var turn = AtStep("fut").Say(said);
            var verb = Assert.Single(turn.Words!, w => w.Text == form);
            Assert.Equal((lemma, "verb", person, "sg", TaughtVerbForm.SimpleFuture, "ind"), (verb.Lemma, verb.Pos, verb.Person, verb.Number, verb.Tense, verb.Mood));
        }
    }

    // ---- Correct Spanish for the future is never a miss

    [Theory]
    [InlineData("voy a comprar pan", "near future for a plan, credited")]
    [InlineData("mañana voy a comprar pan", "near future for a plan, credited")]
    [InlineData("mañana compro pan", "present for a plan, credited")]
    [InlineData("pasado mañana compro pan", "present for a plan, credited")]
    [InlineData("luego compro pan", "present for a plan, credited")]
    [InlineData("la semana que viene compro pan", "present for a plan, credited")] // a time word of several words, matched as a whole
    [InlineData("la próxima semana compro pan", "present for a plan, credited")]
    [InlineData("el próximo día compro pan", "present for a plan, credited")]
    public void TheNearFutureOrAPresentWithATimeWordAtAFutStepIsCreditedWithTheFutureNote(string said, string detail)
    {
        var session = AtStep("fut");

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal));
        Assert.Equal("end", session.Lessons.Current!.Id); // the step ends: not a retry
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf("t")[^1].Outcome);
        Assert.Equal(detail, Done(session).Detail); // a distinct outcome in the log
        Assert.Equal(FutureNote, session.Lessons.View!.Hint); // the next step opens with the note naming the form the step practises
        Assert.DoesNotContain(session.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.Retry);
    }

    [Theory]
    [InlineData("mañana compraré pan")]
    [InlineData("compraré pan")]
    public void TheSimpleFutureAtAnIrAStepIsCreditedWithThePresentNote(string said)
    {
        // A learner who has met the simple future (session 9) is not told off at a later ir-a step: it is right Spanish for a plan, and the step's presentNote, which
        // names the form the step practises, says what the step wanted.
        var session = AtStep(TaughtVerbForm.NearFuture);

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.Equal("end", session.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf("t")[^1].Outcome);
        Assert.Equal("simple future for a plan, credited", Done(session).Detail);
        Assert.Equal(PresentNote, session.Lessons.View!.Hint);
    }

    [Fact]
    public void ThePresentWithAnyTimeWordAtAnIrAStepIsStillCreditedWithThePresentNote()
    {
        var session = AtStep(TaughtVerbForm.NearFuture);

        _ = session.Say("la semana que viene compro pan");

        Assert.Equal("present for a plan, credited", Done(session).Detail);
        Assert.Equal(PresentNote, session.Lessons.View!.Hint);
    }

    // ---- A wrong tense the core cannot see is the lesson's miss; one next to a time word is the core's slip

    private static void AssertMiss(VoxelSession session, string said, string reasonStart, string hint)
    {
        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.True(turn.LessonHandled, said);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal)); // no time word: the core says nothing of the tense
        Assert.Equal("say", session.Lessons.Current!.Id); // a miss: the step stays
        Assert.Equal(hint, session.Lessons.View!.Hint);
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == "say" && e.Detail!.StartsWith(reasonStart, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("compré pan", "wrong tense: said pret, expected fut")]
    [InlineData("compro pan", "wrong tense: said pres, expected fut")]
    [InlineData("iba a comprar pan", "wrong tense: said impf, expected fut")] // ir in a past, then a and an infinitive, is no near future
    [InlineData("después compré pan", "wrong tense: said pret, expected fut")] // después is no marker to the core: ordinary narration
    public void APastOrAPresentWithoutATimeWordAtAFutStepIsAMissWithTheTenseRetry(string said, string reason)
    {
        var session = AtStep("fut");

        AssertMiss(session, said, reason, FutureRetry);
        _ = session.Say("mañana compraré pan");

        Assert.Equal("end", session.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf("t")[^1].Outcome);
    }

    [Fact]
    public void IrInAPastBeforeAnInfinitiveIsNoNearFutureAtAnIrAStepEither()
    {
        // S27 counted any finite form of ir before "a" and an infinitive; iba a comprar ("I was going to buy") is a past, so it is the step's miss, not a plan.
        AssertMiss(AtStep(TaughtVerbForm.NearFuture), "iba a comprar pan", "wrong tense: said impf, expected ir-a", NearRetry);
    }

    [Theory]
    [InlineData("mañana compré pan", "voy a comprar")] // M49: a future marker with a past verb
    [InlineData("la semana que viene compré pan", "voy a comprar")]
    [InlineData("ayer compraré pan", "compré")] // a past marker with a future verb
    public void AWrongTenseNextToATimeWordIsTheCoresSlipAndTheStepEnds(string said, string expected)
    {
        var session = AtStep("fut");

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.Equal(expected, Assert.Single(turn.Errors, static e => e.Tag == "conj.tense").Expected);
        Assert.Equal("end", session.Lessons.Current!.Id); // a slip is the phrase tracking's business: the lesson goes on
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf("t")[^1].Outcome);
        Assert.Equal("with a slip", Done(session).Detail);
    }

    [Fact]
    public void TheVerbOfATimeWordIsNotTheAnswersVerb()
    {
        // The viene of "la semana que viene" is the time word's, not the person the answer said: comprarás is the tú form, and the retry names it.
        var session = AtStep("fut");

        AssertMiss(session, "la semana que viene comprarás pan", "wrong person: said 2sg, expected 1sg", "Say it for yourself.");
    }

    // ---- The time words

    [Fact]
    public void ATimeWordOfSeveralWordsIsMatchedAsAWholeAndInOrder()
    {
        var words = TestGame.Data.Words.NearFuture;
        Assert.Contains("semana que viene", words.TimeWords);
        Assert.Contains("mañana", words.TimeWords);

        Assert.Equal([false, true, true, true, false, false], words.Mark(["la", "semana", "que", "viene", "compro", "pan"]));
        Assert.Equal([true, true, false, false], words.Mark(["pasado", "mañana", "compro", "pan"]));
        Assert.Equal([false, false, false], words.Mark(["la", "semana", "pasada"])); // one word of a phrase alone is nothing
        Assert.Equal([false, false, false, false], words.Mark(["quiero", "que", "viene", "luz"]));
        Assert.Equal([false, true, true, false], words.Mark(["la", "próxima", "semana", "voy"]));
        Assert.Empty(words.Mark([]));
    }

    // ---- The forms

    [Theory]
    [InlineData("comprar", "1", "sg", "compraré")]
    [InlineData("comprar", "2", "sg", "comprarás")]
    [InlineData("comprar", "3", "sg", "comprará")]
    [InlineData("ir", "1", "sg", "iré")]
    [InlineData("buscar", "1", "sg", "buscaré")]
    [InlineData("hacer", "3", "sg", "hará")]
    [InlineData("tener", "1", "sg", "tendré")]
    [InlineData("trabajar", "3", "sg", "trabajará")]
    [InlineData("pescar", "3", "sg", "pescará")]
    public void TheSimpleFutureFormsSessionNineNeedsAreInTheBundlesLexicon(string lemma, string person, string number, string expected)
    {
        // The core's regular paradigm builds no simple future: these come from the lexicon (the Wiktionary import), so a book cell and a lesson's verbs may name them.
        var morphology = TestGame.Bundle.SpanishMorphology();

        Assert.Equal(expected, new TaughtVerbForm(lemma, person, number, TaughtVerbForm.SimpleFuture).Text(morphology, TestGame.Data.Words.NearFuture));
    }
}
