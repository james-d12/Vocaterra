using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Es;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The lesson "El pueblo" of session 4 (VILLAGE_PLAN V10, 3.5): the fisher Mar at the bridge, places and the way to them (¿Dónde está…?, cerca and lejos, a la izquierda and
/// a la derecha, hay), and one verb in the three times around the walk it makes (voy a la panadería, voy a comprar pan, fui a la panadería); the walk from the bridge to the
/// bakery, the smithy and the villager's signpost line, and back; the tense steps; the fisher's order fruit-for-mar; and the fisher's own answers outside the lesson.
/// </summary>
public sealed class VillageLessonTests(ITestOutputHelper output)
{
    private const string LessonId = "el-pueblo";

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    // Ayer y mañana done in session 3 (El pueblo comes after it), then session 4 at the fisher: his lesson starts by itself.
    private static VoxelSession SessionFourAtFisher()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Day = 3;
        session.Sitting = 3;
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        Assert.Equal("ayer-y-manana", session.Lessons.Active!.Id);
        while (session.Lessons.StatusOf("ayer-y-manana") == LessonStatus.InProgress)
        {
            if (session.Lessons.Current is { Id: "f3-walk" })
            {
                session.EndTalk();
                Stage.TeleportToNpc(session, "shopkeeper");
                _ = session.OpenTalk("shopkeeper");
                continue;
            }

            _ = LessonDriver.DoStep(session);
        }

        session.EndTalk();
        session.Day = 4;
        session.Sitting = 4;
        Stage.TeleportToNpc(session, "fisher");
        _ = session.OpenTalk("fisher");
        return session;
    }

    // The lesson as a learner who gets everything right does it, up to the step with this id: the walks are the arrivals the steps wait for, and the talks they open.
    internal static VoxelSession AtStep(string stepId)
    {
        var session = SessionFourAtFisher();
        while (session.Lessons.Current is { } step && step.Id != stepId)
        {
            Walk(session, step);
        }

        return session;
    }

    // One step: a talk the step waits for is opened at that NPC; every other step is the driver's.
    private static LessonDriver.Done? Walk(VoxelSession session, LessonStep step)
    {
        if (step is { Type: LessonStepType.Do, At: { } npc })
        {
            session.EndTalk();
            Stage.TeleportToNpc(session, npc);
            _ = session.OpenTalk(npc);
            return null;
        }

        return LessonDriver.DoStep(session);
    }

    // ---- The lesson

    [Fact]
    public void ElPuebloIsTheFishersLessonOfSessionFourAfterAyerYMananaWithTheVillagerAsItsSecondNpc()
    {
        var lesson = Book.Find(LessonId)!;

        Assert.Equal(("fisher", 4, true, "ayer-y-manana"), (lesson.Npc, lesson.Session, lesson.AutoStart, lesson.After));
        Assert.StartsWith("Session 4", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("bridge", lesson.Objective, StringComparison.Ordinal); // the objective line sends the player down the road to the bridge
        Assert.Contains("El pueblo", lesson.Objective, StringComparison.Ordinal);
        Assert.Equal(["fisher", "villager"], lesson.Steps.Select(lesson.NpcOf).Distinct());

        // Eight phrases the learner says, the ceiling of a lesson: seven new rows and the farewell, which no lesson had taught.
        Assert.Equal(
            ["donde-esta-la-panaderia", "donde-esta-la-herreria", "esta-lejos", "hay-un-pozo", "voy-a-la-panaderia", "voy-a-comprar-pan", "fui-a-la-panaderia", "adios"],
            lesson.TaughtPhrases);
        Assert.Equal(2, lesson.Steps.Count(static s => s.Type == LessonStepType.Review)); // hola at the start, gracias at the end (X06)
        Assert.Equal(["hola", "gracias"], lesson.Steps.Where(static s => s.Type == LessonStepType.Review).SelectMany(static s => s.Candidates!).Select(static c => c.Phrase));
        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type);
        Assert.InRange(lesson.Steps.Count, 15, 26);
        foreach (var step in lesson.Steps)
        {
            if (step.Line is { } line)
            {
                Assert.Equal(lesson.NpcOf(step), line.Speaker);
            }

            foreach (var candidate in step.Candidates ?? [])
            {
                Assert.Equal(lesson.NpcOf(step), candidate.Line.Speaker);
            }
        }

        // The heard rows are the lines of its steps.
        var heard = new[] { "que-buscas", "esta-a-la-izquierda", "esta-cerca", "hay-tres-casas", "adonde-vas", "que-vas-a-hacer-alli", "el-letrero", "fuiste-a-la-panaderia" };
        var sheet = TestGame.Data.Phrases;
        foreach (var id in heard)
        {
            var row = sheet.Find(id)!;
            Assert.Equal("hears", row.Says);
            Assert.Contains(lesson.Steps, s => s.Line is { } line && row.AudioLineIds.Contains(line.LineId, StringComparer.Ordinal));
        }

        // The walk: the bakery and the smithy by the beacon, the villager at the square, and back to the fisher at the bridge.
        var walks = lesson.Steps.Where(static s => s.Type == LessonStepType.Do).ToList();
        Assert.Equal(
            [("bakery", WorldEvents.BeaconReached, null, "bakery"), ("smithy", WorldEvents.BeaconReached, null, "smithy"), (null, WorldEvents.TalkOpened, "villager", null), ("npc.fisher", WorldEvents.TalkOpened, "fisher", null)],
            walks.Select(static s => (s.PointPlace, s.Event!, s.At, s.Detail)));
        Assert.All(walks, static s => Assert.False(string.IsNullOrWhiteSpace(s.Hud), s.Id));
    }

    [Fact]
    public void ItsTenseStepsDeclareThePersonAndTheTenseAndAnA1LessonDeclaresNoSimpleFuture()
    {
        var lesson = Book.Find(LessonId)!;
        var expected = new Dictionary<string, (string Person, string Tense)>
        {
            ["voy-a-la-panaderia"] = ("1sg", "pres"),
            ["voy-a-comprar-pan"] = ("1sg", "ir-a"),
            ["fui-a-la-panaderia"] = ("1sg", "pret"),
        };

        foreach (var step in lesson.Steps.Where(s => s.Phrase is { } p && expected.ContainsKey(p) && s.Type == LessonStepType.Answer))
        {
            Assert.Equal(expected[step.Phrase!], (step.Person!, step.Tense!));
            Assert.False(string.IsNullOrWhiteSpace(step.PersonRetry), step.Id);
            Assert.False(string.IsNullOrWhiteSpace(step.TenseRetry), step.Id);
            Assert.Equal(step.Tense == TaughtVerbForm.NearFuture, step.PresentNote is not null);
        }

        // The forms it declares for the book: the present, the past and the near future only (an a1 lesson, VILLAGE_PLAN 3.9), each a word of the lesson.
        Assert.All(lesson.TaughtVerbs, static v => Assert.Contains(v.Tense, new[] { "pres", "pret", TaughtVerbForm.NearFuture }));
        var morphology = TestGame.Bundle.SpanishMorphology();
        Assert.Equal(["buscas", "voy a comprar", "vas a hacer"], lesson.TaughtVerbs.Select(v => v.Text(morphology, TestGame.Data.Words.NearFuture)));
    }

    [Fact]
    public void TheLessonIsNotOfferedBeforeAyerYMananaIsDoneNorBeforeSessionFour()
    {
        var early = TestGame.NewSessionWithShippedLessons();
        early.Day = 3;
        early.Sitting = 3;
        Assert.Null(early.Lessons.OfferFor("fisher"));

        var skipped = TestGame.NewSessionWithShippedLessons();
        skipped.Day = 4; // session 4 reached without session 3's lesson: El pueblo waits for it (after)
        skipped.Sitting = 4;
        Assert.Null(skipped.Lessons.OfferFor("fisher"));
        Assert.Equal(LessonStatus.NotStarted, skipped.Lessons.StatusOf(LessonId));

        var session = SessionFourAtFisher();
        Assert.Equal(LessonId, session.Lessons.Active!.Id);
        Assert.True(session.Lessons.Engaged);
    }

    [Fact]
    public void TheLessonRunsFromTheBridgeThroughTheVillageAndBackWithTypedAnswersAndEveryStepEndsDone()
    {
        var session = SessionFourAtFisher();
        Assert.Equal("v0-intro", session.Lessons.Current!.Id); // the greeting's review step had nothing due: hola was never said

        var done = new List<LessonDriver.Done>();
        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            var step = session.Lessons.Current!;
            if (step.Id == "v1-bakery")
            {
                done.Add(LessonDriver.DoStep(session));
                Assert.Equal("bakery", session.Pointer!.Point); // he points the way from the place the learner named (fisher.point, point_at_slot)
                continue;
            }

            if (step.Id == "v3-well")
            {
                done.Add(LessonDriver.DoStep(session));
                Assert.Equal("well", session.Pointer!.Point); // ¿Hay un pozo?: he points at it too (fisher.show)
                continue;
            }

            if (step.Id == "v6-back")
            {
                Assert.Equal(("npc.fisher", "pescador"), (session.Pointer!.Point, session.Pointer.Place)); // the walk back points at where he stands, by his word
            }

            if (step.Id == "v5-bakery")
            {
                Assert.Equal(("bakery", "panadería"), (session.Pointer!.Point, session.Pointer.Place)); // the walk's beacon names the place in Spanish
                Assert.Equal("Walk to the bakery (la panadería)", session.Lessons.HudLine);
            }

            if (Walk(session, step) is { } d)
            {
                done.Add(d);
            }
        }

        foreach (var d in done)
        {
            output.WriteLine($"{d.Id,-12} {d.Type,-8} {d.Sent,-28} {d.Turn?.Intent,-16} {d.Outcome}");
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));
        Assert.Equal(4, session.Lessons.Record.Single(static r => r.LessonId == LessonId).CompletedSession);
        Assert.All(done, static d => Assert.Equal(StepOutcome.Done, d.Outcome));
        var turns = done.Where(static d => d.Turn is not null).ToList();
        Assert.Equal(
            ["¿Dónde está la panadería?", "¿Dónde está la herrería?", "¿Está lejos?", "¿Hay un pozo en el pueblo?", "Voy a la panadería", "Voy a comprar pan", "Sí, fui a la panadería", "Adiós"],
            turns.Select(static d => d.Sent));
        Assert.Equal(["ask_directions", "ask_directions", "ask_far", "ask_availability", "state_action", "state_action", "state_action", "farewell"], turns.Select(static d => d.Turn!.Intent));
        Assert.All(turns, static d =>
        {
            Assert.True(d.Turn!.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.True(d.Turn.LessonHandled, d.Id);
            Assert.Equal("fisher", d.Turn.Npc);
        });

        // The two talks of the walk, and the places the fisher was asked for.
        var opened = session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.TalkOpened).Select(static e => e.Npc).ToList();
        Assert.Equal(["villager", "fisher"], opened.TakeLast(2));
        Assert.Equal(["bakery", "smithy", "well"], session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.PlaceAsked && e.Npc == "fisher").Select(static e => e.Detail));
    }

    // ---- The tense check

    private static void AssertMiss(VoxelSession session, string said, string stepId, string reasonStart, string hintContains)
    {
        var attempts = session.Lessons.Attempts;
        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.True(turn.LessonHandled, said);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal)); // no time word: the core says nothing of the tense, the lesson does
        Assert.Equal(stepId, session.Lessons.Current!.Id); // a miss: the step stays
        Assert.Equal(attempts + 1, session.Lessons.Attempts);
        Assert.Contains(hintContains, session.Lessons.View!.Hint, StringComparison.Ordinal);
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == stepId && e.Detail!.StartsWith(reasonStart, StringComparison.Ordinal));
    }

    [Fact]
    public void ThePastWhereTheLearnerIsGoingNowIsAMissWithATenseRetry()
    {
        var session = AtStep("v4-going");

        AssertMiss(session, "fui a la panadería", "v4-going", "wrong tense", "“voy”");
        _ = session.Say("Voy a la panadería");

        Assert.Equal("v4-plan", session.Lessons.Current!.Id);
    }

    [Theory]
    [InlineData("mañana compro pan")]
    [InlineData("luego compro pan")]
    [InlineData("después compro pan")]
    public void APlanInThePresentWithAFutureTimeWordIsCreditedWithTheNote(string said)
    {
        // Correct Spanish is never a miss (S27): the later time words count as mañana does (game/words.json, V10).
        var session = AtStep("v4-plan");

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.Empty(turn.Errors);
        Assert.Equal("v5-bakery", session.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf(LessonId)[^1].Outcome);
        Assert.Contains(session.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "v4-plan" && e.Detail == "present for a plan, credited");
        Assert.Equal(Book.Find(LessonId)!.Steps.Single(static s => s.Id == "v4-plan").PresentNote, session.Lessons.View!.Hint);
    }

    [Theory]
    [InlineData("compro pan")]
    [InlineData("compré pan")]
    public void ThePresentOrThePastWithNoTimeWordAtThePlanIsAMissWithATenseRetry(string said)
    {
        var session = AtStep("v4-plan");

        AssertMiss(session, said, "v4-plan", "wrong tense", "“voy a comprar”");
        _ = session.Say("Voy a comprar pan");

        Assert.Equal("v5-bakery", session.Lessons.Current!.Id);
    }

    [Fact]
    public void ThePresentAtTheReportIsAMissWithATenseRetryAndAyerVoyIsTheCoresSlip()
    {
        // Back at the bridge: the walk is over. "Voy a la panadería" has no time word, so the lesson judges it: a miss whose retry names "fui". With "ayer" the core flags the
        // tense itself (conj.tense, expected fui, M49): a slip, and the step goes on, as at Ayer y mañana's questions (S27).
        var miss = AtStep("v7-went");
        AssertMiss(miss, "voy a la panadería", "v7-went", "wrong tense", "“fui”");
        _ = miss.Say("Sí, fui a la panadería");
        Assert.Equal("v7-hungry", miss.Lessons.Current!.Id);

        var slip = AtStep("v7-went");
        var turn = slip.Say("ayer voy a la panadería");
        Assert.True(turn.Communicated);
        Assert.Equal("fui", Assert.Single(turn.Errors, static e => e.Tag == "conj.tense").Expected);
        Assert.Equal("v7-hungry", slip.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, slip.Lessons.OutcomesOf(LessonId)[^1].Outcome);
    }

    // ---- The fisher outside the lesson

    [Fact]
    public void TheFisherPointsAtThePlaceAskedForSaysWhenHeDoesNotKnowItAndSaysItIsNear()
    {
        var session = TestGame.NewSessionWithShippedLessons(); // session 1: El pueblo is not unlocked, so he talks as himself
        Stage.TeleportToNpc(session, "fisher");

        Assert.Equal(["¡Hola!"], session.OpenTalk("fisher").Lines); // he greets at every talk
        var far = session.Say("¿Está lejos?");
        Assert.Equal(["¿Lejos? ¿Qué buscas?"], far.GuideLines); // nothing was asked for yet

        var bakery = session.Say("¿Dónde está la panadería?");
        Assert.Equal("ask_directions", bakery.Intent);
        Assert.Equal(("bakery", "panadería"), (session.Pointer!.Point, session.Pointer.Place));
        Assert.Equal(["No, está cerca."], session.Say("¿Está lejos?").GuideLines);

        _ = session.Say("¿Hay un pozo?");
        Assert.Equal("well", session.Pointer!.Point);

        var unknown = session.Say("¿Dónde está la montaña?"); // the signpost's arm, but no place of the scene
        Assert.Contains("No sé dónde está.", unknown.GuideLines);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.PlaceAsked && e.Npc == "fisher" && e.Detail == "unknown");
    }

    // ---- The order

    [Fact]
    public void TheOrderOfFruitCountsFromSessionFourAndBringingThreeFruitsPaysTwoCoins()
    {
        var early = TestGame.NewSessionWithShippedLessons();
        _ = early.Simulation.GrantItems(ItemId.Berry, 3);
        Stage.TeleportToNpc(early, "fisher");
        Assert.False(early.OpenTalk("fisher").Delivered); // before its session the order is not open: the fruit stays
        Assert.Equal(3, early.Simulation.Inventory.Count(ItemId.Berry));

        var session = TestGame.NewSessionWithShippedLessons();
        session.Day = 4;
        session.Sitting = 4;
        _ = session.Simulation.GrantItems(ItemId.Berry, 4);
        Stage.TeleportToNpc(session, "fisher");

        var opening = session.OpenTalk("fisher"); // walking up with the fruit is enough (D-game-8, D-game-9)

        Assert.True(opening.Delivered);
        Assert.Equal(["¡Gracias! Toma dos monedas."], opening.Lines);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Berry));
        Assert.Equal(2, session.Simulation.Inventory.Count(ItemId.Coin));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.OrderDelivered && e.Npc == "fisher" && e.Detail == "fruit-for-mar");
        Assert.True(session.Objectives.Single(static o => o.Id == "fruit-for-mar").Complete);
        Assert.Equal(1, session.ObjectiveStep("fruit-for-mar.delivered")!.Count);
    }

    // ---- The review steps X06 left undone (VILLAGE_PLAN 2.8)

    [Fact]
    public void PersonasAndAyerYMananaEachAskTwoEarlierPhrasesAgainAtTheirNpcs()
    {
        var personas = Book.Find("personas")!;
        var ayer = Book.Find("ayer-y-manana")!;

        Assert.Equal([("p0-hello", "villager"), ("p4-review", "shopkeeper")], personas.Steps.Where(static s => s.Type == LessonStepType.Review).Select(s => (s.Id, personas.NpcOf(s))));
        Assert.Equal([("f0-review", "villager"), ("y0-review", "shopkeeper")], ayer.Steps.Where(static s => s.Type == LessonStepType.Review).Select(s => (s.Id, ayer.NpcOf(s))));
        Assert.Equal(["quiero-madera", "necesitamos-agua", "ella-quiere-fruta"], ayer.Steps.Single(static s => s.Id == "f0-review").Candidates!.Select(static c => c.Phrase)); // session 2's, at the villager
        Assert.Equal(["necesito-pan", "queremos-pan", "tengo-botella"], ayer.Steps.Single(static s => s.Id == "y0-review").Candidates!.Select(static c => c.Phrase)); // and at the stall
    }

    [Fact]
    public void TheVillagerGreetsAndTheLearnerGreetsBackWhenHolaIsDue()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        Assert.True(session.Say("hola").Communicated); // hola said in session 1, at the villager
        session.EndTalk();
        session.Day = 2; // a new play session: hola is due
        session.Sitting = 2;

        _ = session.OpenTalk("villager");

        var step = session.Lessons.Current!;
        Assert.Equal(("p0-hello", LessonStepType.Review, "hola", "Hola."), (step.Id, step.Type, step.Phrase, step.Line!.Text));
        Assert.True(session.Say("Hola").Communicated);
        Assert.Equal("p1-intro", session.Lessons.Current!.Id);
        Assert.Equal("hola, lesson", session.Events.OfType<WorldEvent>().Single(static e => e.Name == WorldEvents.ReviewAnswered).Detail);
    }
}
