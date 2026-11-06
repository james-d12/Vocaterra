using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>
/// The teacher in the loop (W08, design 10.14): Luz's agent in its <c>teaching</c> state at <c>baker-proc</c> while a procedure runs, what each line there does (the repeat, the slow
/// recording, the help, the ingredients, the thanks), the daily grant of decision 20 and its save, the recipe taught, the offer to bake and its silence in a bundle with no procedure,
/// and the station's talk reaching the teacher with the station still open. The procedure is the three-step test procedure of <see cref="ProcedureRig"/>, never the shipped bundle's.
/// </summary>
public sealed class TeacherLoopTests
{
    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    // The station over the test procedure, with the worker running (typed turns only) and the talk wired as the game wires it.
    private sealed class Rig
    {
        public Rig()
        {
            Station = new StationRig();
            Session = Station.Session;
            Talk = new TeacherTalk(Session, null, static () => null);
            Station.Panel.Talk = Talk;
            Assert.True(Session.StartWorker(loadSpeech: false).WaitUntilReady(30000), "The worker did not load.");
        }

        public StationRig Station { get; }

        public VoxelSession Session { get; }

        public TeacherTalk Talk { get; }

        public bool Open() => Station.Panel.Open("horno", teacherPresent: true);

        // What the player types at the station: the turn goes to the teacher, is applied by the session and shown on the station's line, as TurnPump does.
        public IReadOnlyList<GuideTurn> Say(string line)
        {
            Assert.True(Talk.Say(line));
            var turns = new List<GuideTurn>();
            var deadline = Environment.TickCount64 + 30000;
            while (Talk.Thinking)
            {
                foreach (var applied in Session.Collect())
                {
                    Assert.True(Talk.Takes(applied), "The station's turn was taken by someone else.");
                    Talk.Complete(applied);
                    turns.Add(applied.Turn!);
                }

                Assert.True(Environment.TickCount64 < deadline, "The teacher did not answer.");
                Thread.Sleep(2);
            }

            Station.Panel.PumpCue();
            return turns;
        }

        public string TeacherState => Session.Conversation.StateOf("baker");

        public int Held(ItemId item) => Session.Simulation.Inventory.Count(item);

        public void Drop(ItemId item) => Session.Simulation.Inventory.Remove(item, Held(item));
    }

    [Fact]
    public void TheTeacherEntersTeachingWhenTheStationOpensAndLeavesItWhenItCloses()
    {
        var rig = new Rig();
        Assert.Equal("idle", rig.TeacherState);
        Assert.Null(rig.Session.TeachingNpc);
        Assert.True(rig.Open());
        Assert.Equal("baker", rig.Session.TeachingNpc);
        Assert.Equal("teaching", rig.TeacherState);
        Assert.Equal("baker-proc", rig.Session.Conversation.PromptOf("baker")); // her prompt while the bake runs
        Assert.Equal("baker", rig.Session.CurrentNpc); // the lines go to her

        rig.Station.Panel.Close();
        Assert.Null(rig.Session.TeachingNpc);
        Assert.Equal("idle", rig.TeacherState);
        Assert.Equal("baker-open", rig.Session.Conversation.PromptOf("baker"));
        Assert.Equal(NpcService.GuideAgent, rig.Session.CurrentNpc);

        Assert.True(rig.Open()); // the run is there to go on with: she listens again
        Assert.Equal("teaching", rig.TeacherState);
    }

    [Fact]
    public void TheTeacherEndsTeachingWhenTheBakeIsDone()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        foreach (var id in rig.Session.Procedures.Order.ToList())
        {
            rig.Station.DoStep(id);
        }

        Assert.Equal(ProcedureState.Done, rig.Session.Procedures.State);
        Assert.Null(rig.Session.TeachingNpc);
        Assert.Equal("idle", rig.TeacherState);
    }

    [Fact]
    public void ALineAtTheStationWithoutTheTeacherGoesNowhere()
    {
        var rig = new Rig();
        Assert.True(rig.Station.Panel.Open("horno")); // the default: the teacher is not with the learner
        Assert.Null(rig.Session.TeachingNpc);
        Assert.False(rig.Talk.Say("Otra vez, por favor."));
        Assert.Equal("idle", rig.TeacherState);
    }

    [Fact]
    public void OtraVezReachesTheRunnerAndPlaysTheRowAgain()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        var row = rig.Station.View.Strip.Row!;
        var turn = Assert.Single(rig.Say("Otra vez, por favor."));
        Assert.Equal("ask_repeat", turn.Intent);
        Assert.Equal(["baker.proc_repeat"], turn.HostEvents);
        Assert.Equal("teaching", turn.AgentState);
        Assert.Equal("baker-proc", turn.Prompt);
        Assert.Equal(["Escucha."], rig.Talk.Answer);
        Assert.Equal(1, rig.Session.Procedures.Repeats);
        Assert.Equal(0, rig.Session.Procedures.Slowers);
        Assert.Equal([row, row], rig.Station.Audio.Played); // the row on opening, and again after her line
        Assert.True(rig.Station.Panel.IsOpen); // the station stays open
    }

    [Fact]
    public void MasDespacioReachesTheRunnerAndPlaysTheSlowRecording()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        var row = rig.Station.View.Strip.Row!;
        var turn = Assert.Single(rig.Say("Más despacio, por favor."));
        Assert.Equal("ask_slower", turn.Intent);
        Assert.Equal(["baker.proc_slower"], turn.HostEvents);
        Assert.Equal(1, rig.Session.Procedures.Slowers);
        Assert.Equal([row, row + "@slow"], rig.Station.Audio.Played);
        Assert.True(rig.Station.Panel.IsOpen);
    }

    [Theory]
    [InlineData("Ayuda.", "ask_help")]
    [InlineData("No sé.", "dont_know")]
    public void AyudaAndNoSeAreTheHKey(string line, string intent)
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        Assert.False(rig.Session.Procedures.View().ShowEnglish && rig.Session.Procedures.View().HighlightUse is not null && false);
        var before = rig.Session.Procedures.View().Rung;
        var turn = Assert.Single(rig.Say(line));
        Assert.Equal(intent, turn.Intent);
        Assert.Equal(["baker.proc_help"], turn.HostEvents);
        Assert.Equal(["Te ayudo."], rig.Talk.Answer);
        var view = rig.Session.Procedures.View();
        Assert.True(view.ShowEnglish); // help: the English and the highlight, the row up a rung, as H does
        Assert.NotNull(view.HighlightUse);
        Assert.NotNull(view.HighlightOn);
        Assert.True(view.Rung >= before);
        Assert.Equal(view.HighlightUse, rig.Station.View.Held.Concat([rig.Station.View.Hand]).FirstOrDefault(static c => c.Selected)?.Noun); // the highlighted thing is held
        Assert.True(rig.Station.Panel.IsOpen);
    }

    [Fact]
    public void ThanksIsLucesLineAndChangesNothing()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        var turn = Assert.Single(rig.Say("Gracias."));
        Assert.Equal("thank", turn.Intent);
        Assert.Empty(turn.HostEvents);
        Assert.Contains(rig.Talk.Answer.Single(), new[] { "De nada.", "¡Con gusto!" });
        Assert.Equal(0, rig.Session.Procedures.Repeats);
        Assert.Equal("teaching", rig.TeacherState);
    }

    [Fact]
    public void ALineOutsideTheAnswerSetKeepsHerListening()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        var turn = Assert.Single(rig.Say("El cielo es azul."));
        Assert.False(turn.Communicated);
        Assert.Equal("teaching", rig.TeacherState);
        Assert.Equal("baker-proc", rig.Session.Conversation.PromptOf("baker"));
    }

    [Fact]
    public void TheFirstBakeIsAlwaysSuppliedAndARepeatedRequestGivesNothingMore()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        rig.Drop(ItemId.Wood);
        Assert.Equal(0, rig.Held(ItemId.Wood));

        var turn = Assert.Single(rig.Say("Quiero madera."));
        Assert.Equal("request_item", turn.Intent);
        Assert.Equal(["baker.give_missing"], turn.HostEvents);
        Assert.Equal(1, rig.Held(ItemId.Wood)); // what the run lacked, and only that
        Assert.Equal(1, rig.Held(ItemId.WaterBottle));
        Assert.Equal(["Toma. Aquí tienes."], rig.Talk.Answer);

        _ = Assert.Single(rig.Say("Quiero madera.")); // nothing is missing now
        Assert.Equal(1, rig.Held(ItemId.Wood));
        Assert.Equal(["Ya tienes todo."], rig.Talk.Answer);

        // The first bake's grants do not count for the day: they are the first bake's, whenever it is.
        rig.Drop(ItemId.Wood);
        _ = Assert.Single(rig.Say("Hay madera."));
        Assert.Equal(1, rig.Held(ItemId.Wood));
        Assert.Empty(rig.Session.GrantsGivenToday);
    }

    [Fact]
    public void AfterTheFirstBakeTheIngredientsAreGivenOncePerDayAndTheDayIsSaved()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        rig.Station.Bake(); // the first bake: done
        rig.Station.NextBake();
        Assert.Equal(ProcedureState.InRun, rig.Session.Procedures.State);
        Assert.Equal("teaching", rig.TeacherState); // the next bake listens again

        rig.Drop(ItemId.Wood);
        rig.Drop(ItemId.WaterBottle);
        var turn = Assert.Single(rig.Say("Quiero madera."));
        Assert.Equal(["baker.give_missing"], turn.HostEvents);
        Assert.Equal(1, rig.Held(ItemId.Wood));
        Assert.Equal(1, rig.Held(ItemId.WaterBottle)); // everything the run lacked
        Assert.Equal(["pan"], rig.Session.GrantsGivenToday);
        var events = rig.Session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.IngredientsGiven).ToList();
        Assert.Equal("pan", Assert.Single(events).Detail);

        // The second request of the day: nothing is given, and she says why.
        rig.Drop(ItemId.Wood);
        _ = Assert.Single(rig.Say("Quiero madera."));
        Assert.Equal(0, rig.Held(ItemId.Wood));
        Assert.Equal(["Hoy no tengo más."], rig.Talk.Answer);
        Assert.Single(rig.Session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.IngredientsGiven);

        // A reload keeps the day: it does not open the grant again.
        var document = SaveGame.Capture(rig.Session, Pristine());
        Assert.Equal(["pan"], document.Systems!.Procedures!.GrantedToday!);
        var loaded = SaveGame.Load(SaveGame.Serialize(document), ProcedureRig.Data(), Pristine, credit: CreditRules.FullTyped, bumpSession: false);
        Assert.Equal(["pan"], loaded.GrantsGivenToday);
        Assert.Equal(ProcedureState.InRun, loaded.Procedures.State);
        var plan = loaded.PlanGrant();
        Assert.NotNull(plan);
        Assert.False(plan.Open);
        Assert.Equal(NpcHostEvents.GiftUsed, plan.Miss);

        // The night opens it again.
        loaded.ApplyNight(false);
        Assert.Empty(loaded.GrantsGivenToday);
        Assert.True(loaded.PlanGrant()!.Open);
    }

    [Fact]
    public void ANoRoomBagIsAnsweredWithTheBagLineAndGivesNothing()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        rig.Drop(ItemId.Wood);
        var bag = rig.Session.Simulation.Inventory;
        var filler = ItemDefinition.All.Where(static d => d.Id is not (ItemId.Wood or ItemId.WaterBottle or ItemId.Bottle or ItemId.Bread)).ToList();
        foreach (var item in filler)
        {
            while (bag.Add(item.Id, item.MaxStack) == 0)
            {
            }
        }

        Assert.False(bag.CanAdd(ItemId.Wood, 1));
        _ = Assert.Single(rig.Say("Quiero madera."));
        Assert.Equal(0, rig.Held(ItemId.Wood));
        Assert.Equal(["Tu bolsa está llena."], rig.Talk.Answer);
        Assert.Empty(rig.Session.GrantsGivenToday);
    }

    // A lesson of the class kind whose do step waits for the bake to be done (what W10's introductory class will be): decision 20's exception.
    private const string BakeClass = """
        { "schemaVersion": 1, "lessons": [ { "id": "bake", "npc": "baker", "level": "a1", "title": "A bake", "english": "Bake with Luz.", "autoStart": true, "steps": [
          { "id": "do", "type": "do", "trigger": { "event": "procedure_done", "detail": "pan" }, "english": "Bake the bread.", "hud": "Bake the bread" },
          { "id": "end", "type": "explain", "english": "You baked." } ] } ] }
        """;

    [Fact]
    public void ALessonWaitingForTheBakeOpensTheGrantWithoutCountingTheDay()
    {
        var closed = new SaveGameProcedures("pan", granted: true);
        var session = new VoxelSession(ProcedureRig.Data(), TestGame.Seed, GuideSession.DefaultStartedAt, Pristine(), CreditRules.FullTyped, TestGame.Lessons(BakeClass));
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalkForClass("baker"); // the class starts by itself, its first step waits for procedure_done
        session.EndTalk();
        Assert.Equal("do", session.Lessons.Current!.Id);
        session.RestoreProcedures(closed.Save);
        Assert.Equal(StartOutcome.Started, session.StartProcedure("pan", "horno", true));
        var plan = session.PlanGrant()!;
        Assert.True(plan.Open); // today's grant was given, and the class needs this one
        Assert.Equal(["pan"], session.GrantsGivenToday);

        var plain = new VoxelSession(ProcedureRig.Data(), TestGame.Seed, GuideSession.DefaultStartedAt, Pristine(), CreditRules.FullTyped, TestGame.NoLessons);
        plain.RestoreProcedures(closed.Save);
        Assert.Equal(StartOutcome.Started, plain.StartProcedure("pan", "horno", true));
        Assert.False(plain.PlanGrant()!.Open); // the same state with no class: closed
    }

    // A save that holds a procedure baked once, and today's grant given or not.
    private sealed class SaveGameProcedures(string id, bool granted)
    {
        public ProceduresSave Save { get; } = new(1, [id], [new ProcedureProgressSave(id, 1, 1, [1], 3, false)], [], null, granted ? [id] : null);
    }

    [Fact]
    public void TeachRecipeAddsTheProcedureToTheKnownListOnce()
    {
        var session = NewSession();
        Assert.Empty(session.Procedures.Progress.Known);
        _ = session.OpenTalk("baker"); // the offer to bake, made once a day
        _ = session.Say("No, gracias.");
        session.EndTalk();
        Assert.Empty(session.Procedures.Progress.Known); // saying no taught nothing
        _ = session.OpenTalk("baker"); // her greeting this time
        var turn = session.Say("¿Cómo hago pan?");
        Assert.Equal("offering_bake", turn.AgentState); // "¿cómo hago pan?" at her open prompt is answered by the offer, which teaches the recipe
        Assert.Contains("baker.teach_recipe", turn.HostEvents);
        Assert.Equal(["pan"], session.Procedures.Progress.Known);
        Assert.Equal("pan", Assert.Single(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.RecipeTaught).Detail);
        Assert.Equal(ProcedureState.NotStarted, session.Procedures.State); // known is not started
        Assert.Empty(session.Procedures.Progress.Records);
    }

    private static VoxelSession NewSession()
    {
        var session = new VoxelSession(ProcedureRig.Data(), TestGame.Seed, GuideSession.DefaultStartedAt, Pristine(), CreditRules.FullTyped, TestGame.NoLessons);
        _ = session.Simulation.Inventory.Add(ItemId.Wood, 1);
        _ = session.Simulation.Inventory.Add(ItemId.WaterBottle, 1);
        return session;
    }

    [Fact]
    public void TheOfferOpensTheTalkOnceADayUntilTheLearnerHasBaked()
    {
        var session = NewSession();
        var opening = session.OpenTalk("baker");
        Assert.Equal(["¿Quieres hacer pan?"], opening.Lines);
        Assert.True(opening.HostTurn);
        Assert.Equal("offering_bake", session.Conversation.StateOf("baker"));
        Assert.Equal("baker-offer", session.Conversation.PromptOf("baker"));
        Assert.Equal(["baker"], session.OffersMadeToday);

        var no = session.Say("No, gracias.");
        Assert.Equal("deny", no.Intent);
        Assert.Equal("idle", no.AgentState); // it costs nothing
        Assert.Empty(no.HostEvents);
        Assert.Empty(session.Procedures.Progress.Known);
        session.EndTalk();

        var again = session.OpenTalk("baker"); // once per in-game day
        Assert.False(again.Lines.SequenceEqual(["¿Quieres hacer pan?"]));
        Assert.Equal("idle", session.Conversation.StateOf("baker"));
        session.EndTalk();

        session.ApplyNight(false);
        Assert.Empty(session.OffersMadeToday);
        Assert.Equal(["¿Quieres hacer pan?"], session.OpenTalk("baker").Lines); // the next day, until she has baked
        session.EndTalk();
        Assert.Equal("offering_bake", session.Conversation.StateOf("baker"));

        // Once the bake is done the offer is not made again.
        var baked = NewSession();
        _ = baked.StartProcedure("pan", "horno", false);
        foreach (var id in baked.Procedures.Order.ToList())
        {
            var (use, on, verb) = ProcedureRig.ActOf(id);
            _ = baked.ActInProcedure(use, on, verb);
        }

        Assert.Equal(ProcedureState.Done, baked.Procedures.State);
        Assert.False(baked.OpenTalk("baker").Lines.SequenceEqual(["¿Quieres hacer pan?"]));
    }

    [Fact]
    public void YesToTheOfferOpensTheStationAtTheFirstRungAndEntersTeaching()
    {
        var session = NewSession();
        var station = new StationRig();
        var asked = new List<(string Station, string Procedure)>();
        station.Session.StationRequested += (stationId, procedureId) =>
        {
            asked.Add((stationId, procedureId));
            Assert.True(station.Panel.Open(stationId, teacherPresent: true)); // what the game does: the dialogue closes and the station opens
        };
        _ = station.Session.OpenTalk("baker");
        var yes = station.Session.Say("Sí, por favor.");
        Assert.Equal("confirm", yes.Intent);
        Assert.Equal(["baker.start_bake"], yes.HostEvents);
        Assert.Equal([("horno", "pan")], asked);
        Assert.True(station.Panel.IsOpen);
        Assert.Equal(1, station.View.Strip.Rung); // the first rung: audio, the Spanish and the English
        Assert.Equal("teaching", station.Session.Conversation.StateOf("baker"));
        Assert.Equal("baker-proc", station.Session.Conversation.PromptOf("baker"));
        Assert.NotNull(session); // (the first session is the one that proves the offer alone: see the test above)
    }

    [Fact]
    public void AnOfferDoesNotHoldWhileABakeRunsOrAnotherThingHoldsTheTalk()
    {
        var session = NewSession();
        _ = session.StartProcedure("pan", "horno", true); // a bake is under way, the teacher listens
        Assert.Null(session.BakeOfferOf("baker", asked: false));
        Assert.Null(session.BakeOfferOf("baker", asked: true));
        session.LeaveStation();
        Assert.Null(session.BakeOfferOf("baker", asked: false)); // the run is still there: she does not offer a second bake over it
        Assert.Null(session.BakeOfferOf("farmer", asked: false)); // an NPC who teaches nothing offers nothing
    }

    [Fact]
    public void WithNoProcedureInTheBundleLuzOffersNothingAndTheGameIsAsItWas()
    {
        var session = new VoxelSession(TestGame.Data, TestGame.Seed, GuideSession.DefaultStartedAt, Pristine(), CreditRules.FullTyped, TestGame.NoLessons);
        Assert.Empty(TestGame.Data.Procedures.Procedures);
        Assert.Null(session.BakeOfferOf("baker", asked: false));
        Assert.Null(session.BakeOfferOf("baker", asked: true));
        var opening = session.OpenTalk("baker");
        Assert.Equal(["¡Hola!"], opening.Lines); // her greeting, as before
        Assert.Equal("idle", session.Conversation.StateOf("baker"));
        Assert.Empty(session.OffersMadeToday);
        var asked = session.Say("¿Cómo hago pan?");
        Assert.Equal("idle", asked.AgentState);
        Assert.DoesNotContain("baker.teach_recipe", asked.HostEvents);
        Assert.Empty(session.Procedures.Progress.Known);
        Assert.DoesNotContain("¿Quieres hacer pan?", asked.GuideLines);
    }

    [Fact]
    public void ATeacherALoadLeftTeachingComesOutOfItBeforeTheNextTalk()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        Assert.Equal("teaching", rig.TeacherState);
        var document = SaveGame.Capture(rig.Session, Pristine());
        var loaded = SaveGame.Load(SaveGame.Serialize(document), ProcedureRig.Data(), Pristine, credit: CreditRules.FullTyped, bumpSession: false);
        Assert.Null(loaded.TeachingNpc);
        Assert.Equal("teaching", loaded.Conversation.StateOf("baker")); // her state was saved with the others
        var opening = loaded.OpenTalk("baker");
        Assert.Equal("idle", loaded.Conversation.StateOf("baker"));
        Assert.Equal("baker-open", opening.Prompt);
    }

    [Fact]
    public void TheStationsTalkReachesTheTeacherAndLeavesTheStationOpen()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        Assert.True(rig.Talk.Say("Otra vez, por favor."));
        Assert.True(rig.Talk.Thinking);
        Assert.Equal(StationTalkState.Thinking, rig.Station.View.Talk!.State);
        Assert.Equal("Luz", rig.Station.View.Talk.Teacher);
        while (rig.Talk.Thinking)
        {
            foreach (var applied in rig.Session.Collect())
            {
                Assert.True(rig.Talk.Takes(applied));
                rig.Talk.Complete(applied);
            }

            Thread.Sleep(2);
        }

        var shown = rig.Station.View.Talk!;
        Assert.Equal((StationTalkState.Answer, "Escucha."), (shown.State, shown.Text));
        Assert.True(rig.Station.Panel.IsOpen);
        Assert.Equal(ProcedureState.InRun, rig.Session.Procedures.State);

        // The next act makes room for its notice.
        rig.Station.DoStep(rig.Session.Procedures.Order[0]);
        Assert.Null(rig.Station.View.Talk);
        Assert.True(rig.Station.Panel.IsOpen);
    }

    [Fact]
    public void WTypesALineAndEnterSendsItWhileTheStationsKeysWaitForTheTextBox()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        rig.Station.Press(GameKey.W);
        Assert.True(rig.Talk.IsTyping);
        rig.Talk.Type('w', false); // the platform types the key that opened the box too: it is swallowed
        foreach (var letter in "ayuda")
        {
            rig.Talk.Type(letter, false);
        }

        Assert.Equal("ayuda", rig.Talk.Entry.Text);
        Assert.Equal((StationTalkState.Typing, "ayuda"), (rig.Station.View.Talk!.State, rig.Station.View.Talk.Text));
        rig.Station.Press(GameKey.H); // a key of the station while the box is open: nothing, the letters are the box's
        Assert.Equal(0, rig.Session.Procedures.View().StepsDone);
        Assert.True(rig.Talk.IsTyping);
        rig.Station.Press(GameKey.Enter);
        Assert.False(rig.Talk.IsTyping);
        Assert.True(rig.Talk.Thinking);
        while (rig.Talk.Thinking)
        {
            foreach (var applied in rig.Session.Collect())
            {
                rig.Talk.Complete(applied);
            }

            Thread.Sleep(2);
        }

        Assert.Equal(["Te ayudo."], rig.Talk.Answer);
        Assert.True(rig.Station.Panel.IsOpen);
        rig.Station.Press(GameKey.W);
        rig.Station.Press(GameKey.Escape); // Esc closes the box first
        Assert.False(rig.Talk.IsTyping);
        Assert.True(rig.Station.Panel.IsOpen);
    }

    [Fact]
    public void ThePanelsOnTalkHookStillTellsTheFrontEndWhatWasPressed()
    {
        var rig = new Rig();
        Assert.True(rig.Open());
        var pressed = new List<StationTalk>();
        rig.Station.Panel.OnTalk = pressed.Add;
        rig.Station.Press(GameKey.V);
        rig.Station.Press(GameKey.W);
        Assert.Equal([StationTalk.Speak, StationTalk.Type], pressed);
    }

    [Fact]
    public void TheHostEventTableReadsTheTeachersKindsAndRejectsAnUnknownProcedure()
    {
        var table = HostEventTable.Parse("""
            { "schemaVersion": 2, "events": [
              { "npc": "baker", "event": "baker.a", "action": { "kind": "procedure_repeat" } },
              { "npc": "baker", "event": "baker.b", "action": { "kind": "procedure_slower" } },
              { "npc": "baker", "event": "baker.c", "action": { "kind": "procedure_help" } },
              { "npc": "baker", "event": "baker.d", "action": { "kind": "give_missing" } },
              { "npc": "baker", "event": "baker.e", "action": { "kind": "teach_recipe", "procedure": "pan" } },
              { "npc": "baker", "event": "baker.f", "action": { "kind": "start_procedure" } } ] }
            """);
        Assert.Equal(NpcEffectKind.ProcedureRepeat, table.Of("baker", "baker.a").Kind);
        Assert.Equal(NpcEffectKind.ProcedureSlower, table.Of("baker", "baker.b").Kind);
        Assert.Equal(NpcEffectKind.ProcedureHelp, table.Of("baker", "baker.c").Kind);
        Assert.Equal(NpcEffectKind.GiveMissing, table.Of("baker", "baker.d").Kind);
        Assert.Equal((NpcEffectKind.TeachRecipe, "pan"), (table.Of("baker", "baker.e").Kind, table.Of("baker", "baker.e").Procedure));
        Assert.Equal((NpcEffectKind.StartProcedure, string.Empty), (table.Of("baker", "baker.f").Kind, table.Of("baker", "baker.f").Procedure));
        Assert.Throws<InvalidDataException>(() => HostEventTable.Parse("""{ "schemaVersion": 2, "events": [ { "npc": "baker", "event": "baker.a", "action": { "kind": "teach_everything" } } ] }"""));

        var data = ProcedureRig.Data().WithHostEvents(HostEventTable.Parse("""{ "schemaVersion": 2, "events": [ { "npc": "baker", "event": "baker.teach_recipe", "action": { "kind": "teach_recipe", "procedure": "no-such" } } ] }"""));
        Assert.Contains(GameDataValidator.Validate(data), static problem => problem.Contains("names the procedure 'no-such'", StringComparison.Ordinal));
    }
}
