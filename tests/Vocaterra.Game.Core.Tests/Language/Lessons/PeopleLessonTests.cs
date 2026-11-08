using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Es;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The lesson "Gente del pueblo" of session 6 (village-plan V12, 3.5), the last of phase A: the villager in the square shows the learner the smith Domingo, the learner meets
/// him and the farmer León, asks their names and what they do (¿Cómo te llamas?, ¿Qué haces?), says where they live (vivo en), and tells the villager about Domingo in the
/// three times, the third person for the first time: va a hacer un hacha (before the order, asked before the learner meets him), Domingo trabaja en la herrería (now),
/// Domingo hizo un hacha (after it). The order axe-handle (two wood and two coins for the axe); a learner who arrives with the goods; the tense and person steps; the smith
/// and the farmer outside the lesson.
/// </summary>
public sealed class PeopleLessonTests(ITestOutputHelper output)
{
    private const string LessonId = "gente-del-pueblo";

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    // Números y pan done in session 5 (Gente del pueblo comes after it), then session 6 at the villager: his lesson starts by itself. A learner who followed
    // sessions 4 and 5 has two coins (two from fruit-for-mar, four from oven-wood, four spent on two loaves); the run of the earlier lessons in these tests brings no
    // fruit to the fisher, so the two coins are granted. With goods, the learner also carries the order's wood.
    private static VoxelSession SessionSixAtVillager(int coins = 2, int wood = 0)
    {
        var session = BakeryLessonTests.AtStep("(after the last step)"); // no step has this id: Números y pan runs to its end
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("numeros-y-pan"));
        session.EndTalk();
        session.Day = 6;
        session.Sitting = 6;
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Coin)); // the four coins of the oven went on two loaves
        Assert.Equal(0, session.Simulation.GrantItems(ItemId.Coin, coins));
        if (wood > 0)
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.Wood, wood));
        }

        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalkForClass("villager");
        return session;
    }

    // The lesson as a learner who gets everything right does it, up to the step with this id.
    internal static VoxelSession AtStep(string stepId)
    {
        var session = SessionSixAtVillager();
        while (session.Lessons.Current is { } step && step.Id != stepId)
        {
            Walk(session, step);
        }

        return session;
    }

    // One step: the wood is what the forest's edge gives (the step waits for the first piece, the order for both); a talk the step waits for (a walk, or the delivery,
    // which happens as the talk opens) is opened at that NPC; every other step is the driver's.
    private static LessonDriver.Done? Walk(VoxelSession session, LessonStep step)
    {
        if (step.Id == "g4-wood")
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.Wood, 2));
            return LessonDriver.DoStep(session);
        }

        if (step is { Type: LessonStepType.Do, At: { } npc })
        {
            session.EndTalk();
            Stage.TeleportToNpc(session, npc);
            _ = session.OpenTalkForClass(npc);
            return null;
        }

        return LessonDriver.DoStep(session);
    }

    // ---- The lesson

    [Fact]
    public void GenteDelPuebloIsTheVillagersLessonOfSessionSixAfterNumerosYPanWithTheSmithAndTheFarmer()
    {
        var lesson = Book.Find(LessonId)!;

        Assert.Equal(("villager", 6, false, "numeros-y-pan"), (lesson.Npc, lesson.Session, lesson.AutoStart, lesson.After));
        Assert.StartsWith("Session 6", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("square", lesson.Objective, StringComparison.Ordinal); // the objective line names where to go: the villager in the square, who shows the smith
        Assert.Contains("Domingo", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("Gente del pueblo", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("smithy", Book.Find("numeros-y-pan")!.Steps[^1].English, StringComparison.Ordinal); // and Números y pan's recap names the smithy as what comes next
        Assert.Equal(["villager", "smith", "farmer"], lesson.Steps.Select(lesson.NpcOf).Distinct());

        // Seven phrases the learner says, ¿Cómo te llamas? to two people, and two review steps: session 4's report or the greeting at the start, session 2's need at the end.
        Assert.Equal(
            ["es-el-herrero", "va-a-hacer-un-hacha", "como-te-llamas", "que-haces", "vivo-en-el-campamento", "trabaja-en-la-herreria", "hizo-un-hacha"],
            lesson.TaughtPhrases);
        Assert.Equal(["smith", "farmer"], lesson.Steps.Where(static s => s.Phrase == "como-te-llamas").Select(lesson.NpcOf));
        Assert.Equal(
            [["fui-a-la-panaderia", "hola"], ["necesito-pan"]],
            lesson.Steps.Where(static s => s.Type == LessonStepType.Review).Select(static s => s.Candidates!.Select(static c => c.Phrase).ToArray()));
        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type);
        Assert.InRange(lesson.Steps.Count, 15, 28);
        foreach (var step in lesson.Steps)
        {
            if (step.Line is { } line)
            {
                Assert.Equal(lesson.NpcOf(step), line.Speaker); // the speaker of each line its NPC (V01's rule): the villager's, Domingo's and León's own voices
            }

            foreach (var candidate in step.Candidates ?? [])
            {
                Assert.Equal(lesson.NpcOf(step), candidate.Line.Speaker);
            }
        }

        // The heard rows are the lines of its steps: the lesson takes the learner's questions, so each man's answer is a step of its own (Domingo's name, his job, León's name).
        var heard = new[]
        {
            "me-llamo-domingo", "soy-el-herrero", "que-va-a-hacer-domingo", "donde-vives", "quieres-un-hacha", "toma-el-hacha", "vive-en-la-casa", "que-hizo-domingo", "ayer-trabajo-mucho",
        };
        var sheet = TestGame.Data.Phrases;
        foreach (var id in heard)
        {
            var row = sheet.Find(id)!;
            Assert.Equal("hears", row.Says);
            Assert.Contains(lesson.Steps, s => s.Line is { } line && row.AudioLineIds.Contains(line.LineId, StringComparer.Ordinal));
        }

        Assert.Equal(["smith_name.1", "farmer_name.1"], lesson.Steps.Where(static s => s.Id.EndsWith("-llamo", StringComparison.Ordinal)).Select(static s => s.Line!.LineId));

        // The do steps: the walk to the smith, the wood (the arrow on the nearest tree), the order delivered, the walk to the farmer and back to the villager.
        var walks = lesson.Steps.Where(static s => s.Type == LessonStepType.Do).ToList();
        Assert.Equal(
            [
                ("npc.smith", null, WorldEvents.TalkOpened, "smith", null),
                (null, "árbol", WorldEvents.ItemPickedUp, null, "madera"),
                (null, null, WorldEvents.OrderDelivered, "smith", "axe-handle"),
                ("npc.farmer", null, WorldEvents.TalkOpened, "farmer", null),
                (null, null, WorldEvents.TalkOpened, "villager", null),
            ],
            walks.Select(static s => (s.PointPlace, s.Point, s.Event!, s.At, s.Detail)));
        Assert.All(walks, static s => Assert.False(string.IsNullOrWhiteSpace(s.Hud), s.Id));

        // The plan comes before the learner meets the smith, so before the order can be delivered whatever the learner carries (the talk with him delivers it).
        var steps = lesson.Steps.ToList();
        Assert.True(steps.FindIndex(static s => s.Phrase == "va-a-hacer-un-hacha") < steps.FindIndex(static s => s.Type == LessonStepType.Do && s.At == "smith"));
        Assert.True(steps.FindIndex(static s => s.Event == WorldEvents.OrderDelivered) < steps.FindIndex(static s => s.Phrase == "hizo-un-hacha"));
    }

    [Fact]
    public void ItsStepsDeclareThePersonAndTheTenseAndItsVerbsAreThePresentThePastAndTheNearFuture()
    {
        var lesson = Book.Find(LessonId)!;
        var expected = new Dictionary<string, (string Person, string? Tense)>
        {
            ["es-el-herrero"] = ("3sg", null),
            ["va-a-hacer-un-hacha"] = ("3sg", "ir-a"),
            ["como-te-llamas"] = ("2sg", null),
            ["que-haces"] = ("2sg", null),
            ["vivo-en-el-campamento"] = ("1sg", "pres"),
            ["trabaja-en-la-herreria"] = ("3sg", "pres"),
            ["hizo-un-hacha"] = ("3sg", "pret"),
        };

        var steps = lesson.Steps.Where(static s => s.Type is LessonStepType.Answer or LessonStepType.Repeat).ToList();
        Assert.Equal(8, steps.Count);
        foreach (var step in steps)
        {
            Assert.Equal(expected[step.Phrase!], (step.Person!, step.Tense));
            Assert.False(string.IsNullOrWhiteSpace(step.PersonRetry), step.Id);
            Assert.Equal(step.Tense is not null, !string.IsNullOrWhiteSpace(step.TenseRetry));
            Assert.Equal(step.Tense == TaughtVerbForm.NearFuture, step.PresentNote is not null);
        }

        // The forms it declares for the book: llamar (me llamo, te llamas), hacer in the three times for él (hace, va a hacer, hizo) and haces, trabajar (trabajo,
        // trabaja, trabajó) and vivir (vivo, vive). An a1 lesson: no simple future (village-plan 3.9).
        Assert.All(lesson.TaughtVerbs, static v => Assert.Contains(v.Tense, new[] { "pres", "pret", TaughtVerbForm.NearFuture }));
        var morphology = TestGame.Bundle.SpanishMorphology();
        Assert.Equal(
            ["llamo", "llamas", "haces", "hace", "va a hacer", "hizo", "trabajo", "trabaja", "trabajó", "vivo", "vive"],
            lesson.TaughtVerbs.Select(v => v.Text(morphology, TestGame.Data.Words.NearFuture)));
    }

    [Fact]
    public void TheLessonIsNotOfferedBeforeNumerosYPanIsDoneNorBeforeSessionSix()
    {
        var early = TestGame.NewSessionWithShippedLessons();
        early.Day = 5;
        early.Sitting = 5;
        Assert.NotEqual(LessonId, early.Lessons.OfferFor("villager")?.LessonId);

        var skipped = TestGame.NewSessionWithShippedLessons();
        skipped.Day = 6; // session 6 reached without session 5's lesson: Gente del pueblo waits for it (after)
        skipped.Sitting = 6;
        Assert.NotEqual(LessonId, skipped.Lessons.OfferFor("villager")?.LessonId);
        Assert.Equal(LessonStatus.NotStarted, skipped.Lessons.StatusOf(LessonId));

        var session = SessionSixAtVillager();
        Assert.Equal(LessonId, session.Lessons.Active!.Id);
        Assert.True(session.Lessons.Engaged);
    }

    [Fact]
    public void TheLessonRunsFromTheSquareToTheSmithyTheForestAndTheFieldAndBackWithTypedAnswersAndEveryStepEndsDone()
    {
        var session = SessionSixAtVillager();
        Assert.Equal(2, session.Simulation.Inventory.Count(ItemId.Coin));

        var done = new List<LessonDriver.Done>();
        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            var step = session.Lessons.Current!;
            if (step.Id == "g2-walk")
            {
                Assert.Equal(("npc.smith", "herrero"), (session.Pointer!.Point, session.Pointer.Place)); // the walk to the smith names him in Spanish
            }

            if (step.Id == "g4-wood")
            {
                Assert.Equal("árbol", session.Pointer!.Place); // the arrow on the nearest tree, at the forest's edge
                Assert.Equal("Collect wood (madera) at the forest's edge", session.Lessons.HudLine);
            }

            if (step.Id == "g5-walk")
            {
                Assert.Equal(("npc.farmer", "granjero"), (session.Pointer!.Point, session.Pointer.Place));
            }

            if (Walk(session, step) is { } d)
            {
                done.Add(d);
            }

            if (step.Id == "g3-lives")
            {
                Assert.Equal((2, 0, 0), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Wood), session.Simulation.Inventory.Count(ItemId.Axe)));
            }

            if (step.Id == "g4-back")
            {
                // The goods went and the axe came, the moment the talk opened.
                Assert.Equal((0, 0, 1), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Wood), session.Simulation.Inventory.Count(ItemId.Axe)));
            }
        }

        foreach (var d in done)
        {
            output.WriteLine($"{d.Id,-12} {d.Type,-8} {d.Sent,-32} {d.Turn?.Intent,-16} {d.Outcome}");
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));
        Assert.Equal(6, session.Lessons.Record.Single(static r => r.LessonId == LessonId).CompletedSession);
        Assert.All(done, static d => Assert.Equal(StepOutcome.Done, d.Outcome));

        // The reviews, asked by the villager: session 4's report at the start (due by now); session 2's need at the end, which session 3 reviewed and which is
        // not due again in this run, whose sessions follow one another with no time between them (a learner's days apart would make it due), so it is skipped.
        var review = Assert.Single(done, static d => d.Type == "review");
        Assert.Equal(("g0-review", "Sí, fui a la panadería", "villager", true), (review.Id, review.Sent, review.Turn!.Npc, review.Turn.Communicated));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ReviewAnswered && e.Detail == "fui-a-la-panaderia, lesson");
        Assert.Equal(StepOutcome.Skipped, session.Lessons.OutcomesOf(LessonId).Single(static o => o.StepId == "g7-review").Outcome);

        var turns = done.Where(static d => d.Turn is not null && d.Type != "review").ToList();
        Assert.Equal(
            ["Es el herrero", "Va a hacer un hacha", "¿Cómo te llamas?", "¿Qué haces?", "Vivo en el campamento", "¿Cómo te llamas?", "Domingo trabaja en la herrería", "Domingo hizo un hacha"],
            turns.Select(static d => d.Sent));
        Assert.Equal(["name_object", "state_action", "ask_name", "ask_activity", "state_action", "ask_name", "state_action", "state_action"], turns.Select(static d => d.Turn!.Intent));
        Assert.All(turns, static d =>
        {
            Assert.True(d.Turn!.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.True(d.Turn.LessonHandled, d.Id);
        });
        Assert.Equal(["villager", "villager", "smith", "smith", "smith", "farmer", "villager", "villager"], turns.Select(static d => d.Turn!.Npc));


        // The order in the log.
        var world = session.Events.OfType<WorldEvent>().ToList();
        Assert.Contains(world, static e => e.Name == WorldEvents.OrderDelivered && e.Npc == "smith" && e.Detail == "axe-handle");
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemTaken && e.Npc == "smith" && e.Detail == "madera, order");
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemTaken && e.Npc == "smith" && e.Detail == "moneda, order");
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemGiven && e.Npc == "smith" && e.Detail == "hacha, order");
        Assert.True(session.Objectives.Single(static o => o.Id == "axe-handle").Complete);
    }

    [Fact]
    public void ALearnerWhoArrivesWithTheWoodAndTheCoinsHasTheAxeAtTheFirstTalkAndTheClassTakenLateGoesOn()
    {
        // The objective line shows the order from the session's start, so the goods may come first. The lesson starts at the villager, who asks the plan before the
        // learner meets the smith, and the order names its lesson (V14): the first talk with Domingo delivers nothing, so his offer ("¿Quieres un hacha? Necesito dos
        // bloques de madera y dos monedas.") comes before his thanks, and the axe is made at the step that waits for it. Before V14 the first talk took the goods, and
        // the offer followed the axe it offered.
        var session = SessionSixAtVillager(coins: 2, wood: 2);
        Assert.Equal(LessonId, TestGame.Data.Objectives.Find("axe-handle")!.Lesson);
        Assert.False(session.Objectives.Single(static o => o.Id == "axe-handle").Complete); // talking to the villager delivers nothing: Domingo is not met yet
        // The two wood are in the bag already, so the wood step ends by itself (V23a) and the lesson waits at the step for the delivery.
        while (session.Lessons.Current is { Id: not "g4-back" } step)
        {
            Walk(session, step);
        }

        Assert.True(session.Objectives.Single(static o => o.Id == "axe-handle").Complete); // no lesson holds a delivery (L01): the first talk with Domingo made the axe
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Axe));
        while (session.Lessons.Current is { Id: not "g4-axe" } step)
        {
            Walk(session, step);
        }

        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Axe));
        Assert.Contains(session.Events.OfType<LessonEvent>(), static e => e.StepId == "g4-back" && e.Detail == "delivered before"); // the class finds the axe made, and goes on
    }

    // ---- The tense and person checks

    private static void AssertMiss(VoxelSession session, string said, string stepId, string reasonStart, string hintContains)
    {
        var attempts = session.Lessons.Attempts;
        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.True(turn.LessonHandled, said);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal)); // the core says nothing of it: the lesson does
        Assert.Equal(stepId, session.Lessons.Current!.Id); // a miss: the step stays
        Assert.Equal(attempts + 1, session.Lessons.Attempts);
        Assert.Contains(hintContains, session.Lessons.View!.Hint, StringComparison.Ordinal);
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == stepId && e.Detail!.StartsWith(reasonStart, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("voy a hacer un hacha")] // the near future of the learner, not Domingo's
    [InlineData("Domingo voy a hacer un hacha")] // a lone name before the verb may be the listener (Domingo, voy a...), so the core says nothing: the lesson's step does
    public void ThePlanInTheFirstPersonIsAMissWithAPersonRetry(string said)
    {
        var session = AtStep("g1-plan");

        AssertMiss(session, said, "g1-plan", "wrong person: said 1sg, expected 3sg", "“va a hacer”");
        _ = session.Say("Va a hacer un hacha");
        Assert.Equal("g2-walk", session.Lessons.Current!.Id);
    }

    [Fact]
    public void ThePresentAtThePlanIsAMissWithATenseRetryAndAPlanInThePresentWithATimeWordIsCredited()
    {
        var miss = AtStep("g1-plan");
        AssertMiss(miss, "Domingo hace un hacha", "g1-plan", "wrong tense", "“va a hacer”");

        // Right Spanish for a plan (S27): the present with a future time word is credited, with the note.
        var plan = AtStep("g1-plan");
        var turn = plan.Say("luego Domingo hace un hacha");
        Assert.Empty(turn.Errors);
        Assert.Equal("g2-walk", plan.Lessons.Current!.Id);
        Assert.Contains(plan.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "g1-plan" && e.Detail == "present for a plan, credited");

        // A conjugated verb after "ir a" is the core's conj.tense (M49): a slip, and the step goes on.
        var slip = AtStep("g1-plan");
        var tense = slip.Say("Domingo va a hizo un hacha");
        Assert.Equal("hacer", Assert.Single(tense.Errors, static e => e.Tag == "conj.tense").Expected);
        Assert.Equal("g2-walk", slip.Lessons.Current!.Id);
    }

    [Fact]
    public void TheFirstPersonWhereDomingoWorksIsAMissWithAPersonRetryAndElTrabajoIsTheCoresSlip()
    {
        // "Domingo trabajo en la herrería": a lone name before the verb may be the listener (Domingo, trabajo en la herrería), so the core says nothing of the person
        // (M59) and the lesson's person check makes it a miss; after "él" the core flags it itself (conj.person, expected trabaja): a slip, and the step goes on.
        var miss = AtStep("g6-works");
        AssertMiss(miss, "Domingo trabajo en la herrería", "g6-works", "wrong person: said 1sg, expected 3sg", "“trabaja”");
        _ = miss.Say("Domingo trabaja en la herrería");
        Assert.Equal("g6-house", miss.Lessons.Current!.Id);

        var slip = AtStep("g6-works");
        var turn = slip.Say("él trabajo en la herrería");
        Assert.True(turn.Communicated);
        Assert.Equal("trabaja", Assert.Single(turn.Errors, static e => e.Tag == "conj.person").Expected);
        Assert.Equal("g6-house", slip.Lessons.Current!.Id);
        Assert.Contains(slip.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "g6-works" && e.Detail == "with a slip");
    }

    [Fact]
    public void ThePresentAtTheReportIsAMissWithATenseRetryAndAyerHaceIsTheCoresSlip()
    {
        // After the order: it is done. "Domingo hace un hacha" has no time word, so the lesson judges it: a miss whose retry names "hizo". With "ayer" the core flags
        // the tense itself (conj.tense, expected hizo, M49): a slip, and the step goes on.
        var miss = AtStep("g6-made");
        AssertMiss(miss, "Domingo hace un hacha", "g6-made", "wrong tense", "“hizo”");
        _ = miss.Say("Domingo hizo un hacha");
        Assert.Equal("g6-much", miss.Lessons.Current!.Id);

        var slip = AtStep("g6-made");
        var turn = slip.Say("ayer Domingo hace un hacha");
        Assert.True(turn.Communicated);
        Assert.Equal("hizo", Assert.Single(turn.Errors, static e => e.Tag == "conj.tense").Expected);
        Assert.Equal("g6-much", slip.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, slip.Lessons.OutcomesOf(LessonId)[^1].Outcome);
    }

    [Fact]
    public void TheNameAskedAsUstedIsAMissWithAPersonRetryAndComoTeLlamaIsTheCoresSlip()
    {
        // "¿Cómo se llama usted?" is right Spanish, but the NPCs say tú: the lesson's person check makes it a miss. "¿Cómo te llama?" the core flags itself since M59,
        // which agrees a reflexive clitic after cómo with its verb (conj.person, expected llamas): a slip, and the step goes on.
        var miss = AtStep("g3-name");
        AssertMiss(miss, "¿Cómo se llama usted?", "g3-name", "wrong person: said 3sg, expected 2sg", "“¿Cómo te llamas?”");
        _ = miss.Say("¿Cómo te llamas?");
        Assert.Equal("g3-llamo", miss.Lessons.Current!.Id); // and he says his name

        var slip = AtStep("g3-name");
        var turn = slip.Say("¿Cómo te llama?");
        Assert.True(turn.Communicated);
        Assert.Equal("ask_name", turn.Intent);
        Assert.Equal("llamas", Assert.Single(turn.Errors, static e => e.Tag == "conj.person").Expected);
        Assert.Equal("g3-llamo", slip.Lessons.Current!.Id);
        Assert.Contains(slip.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "g3-name" && e.Detail == "with a slip");
    }

    [Fact]
    public void TheJobAskedAsUstedIsAMissAndSoyElHerreroWhereDomingoIsMeantToo()
    {
        var job = AtStep("g3-job");
        AssertMiss(job, "¿Qué hace usted?", "g3-job", "wrong person: said 3sg, expected 2sg", "“¿Qué haces?”");

        var who = AtStep("g1-who");
        AssertMiss(who, "soy el herrero", "g1-who", "wrong person: said 1sg, expected 3sg", "“es”");
        _ = who.Say("Es el herrero");
        Assert.Equal("g1-axe", who.Lessons.Current!.Id);
    }

    // ---- The smith and the farmer outside the lesson, and the order

    [Fact]
    public void TheSmithAndTheFarmerStandAtTheirPointsAndSpeakWithTheirOwnVoices()
    {
        var data = TestGame.Data;
        var smith = data.Scene.Npcs.Single(static n => n.Name == "smith");
        var farmer = data.Scene.Npcs.Single(static n => n.Name == "farmer");
        Assert.Equal((302, 206), (smith.X, smith.Z)); // npc.smith, by the smithy's door
        Assert.Equal((300, 218), (farmer.X, farmer.Z)); // npc.farmer, at the wheat field's west fence

        Assert.Equal(new NpcVoiceSpec(36, "im_nicola", 1.0f, 0.6f), data.Npcs.Find("smith")!.Voice);
        Assert.Equal(new NpcVoiceSpec(44, "pm_santa", 1.0f), data.Npcs.Find("farmer")!.Voice);
        Assert.Equal(("smith", "smith", "farmer", "villager"), (SpeechSpeakers.OfPool("smith_name", data.Npcs), SpeechSpeakers.OfPool("guide.lesson.smith.lives", data.Npcs), SpeechSpeakers.OfPool("farmer_name", data.Npcs), SpeechSpeakers.OfPool("guide.lesson.villager.who", data.Npcs)));

        var session = TestGame.NewSessionWithShippedLessons(); // session 1: no lesson of theirs, so they talk as themselves
        Stage.TeleportToNpc(session, "smith");
        Assert.Equal(["¡Hola!"], session.OpenTalkForClass("smith").Lines); // he greets at every talk
        Assert.Equal(["¡Hola! Esta es mi herrería."], session.Say("Hola").GuideLines);
        Assert.Equal(["Me llamo Domingo."], session.Say("¿Cómo te llamas?").GuideLines);
        Assert.Equal(["Soy el herrero. Trabajo con fuego y madera."], session.Say("¿Qué haces?").GuideLines);
        Assert.Equal(["¿Quieres un hacha? Necesito dos bloques de madera y dos monedas."], session.Say("Tengo madera").GuideLines);
        var help = session.Say("¿Qué hago?"); // what do I do: the help question, not ¿Qué haces?
        Assert.Equal("ask_help", help.Intent);
        Assert.Equal(["Puedes preguntar: «¿Cómo te llamas?»."], help.GuideLines);
        session.EndTalk();

        Stage.TeleportToNpc(session, "farmer");
        Assert.Equal(["¡Hola!"], session.OpenTalkForClass("farmer").Lines);
        Assert.Equal(["Me llamo León."], session.Say("¿Cómo te llamas?").GuideLines);
        Assert.Equal(["Soy el granjero. Trabajo en el campo."], session.Say("¿Qué haces?").GuideLines);
    }

    [Fact]
    public void TheOrderTakesTwoWoodAndTwoCoinsOnceDomingoIsMetAndGivesTheAxeAndWithOneCoinNothing()
    {
        var early = TestGame.NewSessionWithShippedLessons();
        early.Day = 5;
        early.Sitting = 5;
        _ = early.Simulation.GrantItems(ItemId.Wood, 2);
        _ = early.Simulation.GrantItems(ItemId.Coin, 2);
        Stage.TeleportToNpc(early, "smith");
        _ = early.OpenTalkForClass("smith"); // L01: the order opens once Domingo is met, whatever the day, and the goods are handed over in Spanish (D-game-27)
        Assert.Equal((0, 0, 1), (early.Simulation.Inventory.Count(ItemId.Wood), early.Simulation.Inventory.Count(ItemId.Coin), early.Simulation.Inventory.Count(ItemId.Axe)));

        var poor = TestGame.NewSessionWithShippedLessons();
        poor.Day = 6;
        poor.Sitting = 6;
        _ = poor.Simulation.GrantItems(ItemId.Wood, 3);
        _ = poor.Simulation.GrantItems(ItemId.Coin, 1);
        Stage.TeleportToNpc(poor, "smith");
        var short1 = poor.OpenTalkForClass("smith"); // one coin is not enough: nothing changes hands, he greets as usual
        Assert.False(short1.Delivered);
        Assert.Equal(["¡Hola!"], short1.Lines);
        Assert.Equal((3, 1, 0), (poor.Simulation.Inventory.Count(ItemId.Wood), poor.Simulation.Inventory.Count(ItemId.Coin), poor.Simulation.Inventory.Count(ItemId.Axe)));
        Assert.DoesNotContain(poor.Events.OfType<WorldEvent>(), static e => e.Name is WorldEvents.ItemTaken or WorldEvents.OrderDelivered && e.Npc == "smith");

        var session = TestGame.NewSessionWithShippedLessons();
        session.Day = 6;
        session.Sitting = 6;
        _ = session.Simulation.GrantItems(ItemId.Wood, 3);
        _ = session.Simulation.GrantItems(ItemId.Coin, 2);
        Stage.TeleportToNpc(session, "smith");

        var opening = session.OpenTalk("smith"); // Domingo asks what the learner has, and the goods are handed over in Spanish (D-game-27)
        Assert.Equal(["¡Hola! ¿Qué tienes?"], opening.Lines);

        var turn = session.HandOverGoods()!; // «Aquí tienes la madera y las monedas.»

        Assert.Equal(["¡Gracias! Toma el hacha."], turn.FollowUp);
        Assert.Equal((1, 0, 1), (session.Simulation.Inventory.Count(ItemId.Wood), session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Axe)));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.OrderDelivered && e.Npc == "smith" && e.Detail == "axe-handle");
        Assert.True(session.Objectives.Single(static o => o.Id == "axe-handle").Complete);
        Assert.Equal(1, session.ObjectiveStep("axe-handle.delivered")!.Count);

        // Once is enough: the next talk is an ordinary one.
        session.EndTalk();
        _ = session.Simulation.GrantItems(ItemId.Coin, 2);
        Assert.False(session.OpenTalkForClass("smith").Delivered);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Axe));
    }
}
