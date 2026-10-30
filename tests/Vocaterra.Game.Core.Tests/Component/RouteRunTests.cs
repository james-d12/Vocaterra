using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Component;

/// <summary>
/// The whole route headless (S6): gather, craft and place the torch, walk to the stall and ask for water, accept the bread, walk to the villager
/// and ask for the river, follow the beacon, fill the bottle, see <c>agua 1/1</c>, go back to the guide for the transfer offer and ask the villager
/// for bread. It is the same sequence as the front-end's scripted run (<c>README.md</c>, the S6 line), played with the same session, the same
/// worker-free turn path and a breadth-first walker, and it prints each turn, each game event and the objective's steps.
/// </summary>
public sealed class RouteRunTests(ITestOutputHelper output)
{
    /// <summary>What a full run did.</summary>
    private sealed record Run(VoxelSession Session, List<string> Log, string Digest);

    private static Run PlayRoute(ulong seed = TestGame.Seed, bool askVillager = true)
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp(seed);
        var log = new List<string> { $"objective: {session.Objective.Summary}" };
        var printed = 0;

        void Flush()
        {
            for (; printed < session.Events.Count; printed++)
            {
                log.Add($"  event: {session.Events[printed].Text}");
            }
        }

        void Talk(string npc)
        {
            Assert.True(Stage.WalkToNpc(session, npc), $"no path to {npc}");
            var opening = session.OpenTalk(npc);
            log.Add($"talk: {npc} ({(opening.HostTurn ? "host turn" : "prompt")}, {opening.Prompt}): {string.Join(" / ", opening.Lines)}");
            Flush();
        }

        void Say(string line)
        {
            var turn = session.Say(line);
            log.Add($"turn ({turn.Npc}, {turn.Prompt}): \"{line}\" -> {turn.Summary}, state {turn.AgentState}{(turn.HostEvents.Count > 0 ? ", host events " + string.Join(' ', turn.HostEvents) : string.Empty)}");
            foreach (var reply in turn.GuideLines)
            {
                log.Add($"  {turn.Npc}: {reply}");
            }

            Flush();
        }

        Talk("shopkeeper");
        Say("quiero agua, por favor");
        Say("sí, por favor");
        session.EndTalk();
        Flush();

        BlockPos water;
        if (askVillager)
        {
            Talk("villager");
            Say("¿dónde está el río?");
            session.EndTalk();
            water = session.Pointer is { } pointer ? pointer.Target.ToBlock() : throw new InvalidOperationException("the villager set no beacon");
            log.Add($"beacon: {session.Pointer!.Place} at {water.X},{water.Y},{water.Z}");
        }
        else
        {
            water = Stage.NearestRiver(session);
            log.Add($"walks to the river without asking, water at {water.X},{water.Y},{water.Z}");
        }

        Stage.WalkToWater(session, water);
        var bank = session.Simulation.Player.FeetBlock;
        log.Add($"at the bank: {bank.X},{bank.Y},{bank.Z}");
        var ticks = Stage.Fill(session, water);
        log.Add($"fill: {water.X},{water.Y},{water.Z} in {ticks} ticks");
        foreach (var step in session.Completed)
        {
            log.Add($"objective: Hecho: {step.Text} -> {session.Objective.Summary}");
        }

        Flush();
        log.Add($"objective: {session.Objective.Summary}");

        Talk("guide");
        Say("sí, por favor");
        session.EndTalk();
        Flush();

        Talk("villager");
        Say("quiero pan, por favor");
        session.EndTalk();
        Flush();
        return new Run(session, log, session.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void FromTheCampToTheRiverAndBackTheObjectiveReadsFourOfFourAndTheTransferIsLogged()
    {
        var run = PlayRoute();
        foreach (var line in run.Log)
        {
            output.WriteLine(line);
        }

        var session = run.Session;
        Assert.Equal("Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 1/1", session.Objective.Summary);
        Assert.True(session.Objective.Complete);
        var inventory = session.Simulation.Inventory;
        Assert.Equal(0, inventory.Count(ItemId.Bottle)); // the bottle was filled
        Assert.Equal(2, inventory.Count(ItemId.WaterBottle)); // the river's, and the guide's offer
        Assert.Equal(2, inventory.Count(ItemId.Bread)); // the stall's, and the villager's
        Assert.True(session.RiverPointedOut);

        var uses = session.Events.OfType<PhraseUse>().Select(static u => $"{u.PhraseId}/{PhraseUseKindNames.Of(u.Kind)}/{u.Npc}").ToArray();
        Assert.Equal(
            [
                "pedir-agua/supported/shopkeeper",
                "si-por-favor/supported/shopkeeper",
                "donde-esta-el-rio/supported/villager",
                "si-por-favor/transfer/guide",
                "pedir-agua/transfer/villager",
            ],
            uses);
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.FoundWithoutSpanish);
        var names = session.Events.OfType<WorldEvent>().Select(static e => e.Name).ToList();
        Assert.Contains(WorldEvents.BottleFilled, names);
        Assert.Contains(WorldEvents.BeaconSet, names);
        Assert.Contains(WorldEvents.ObjectiveComplete, names);
        Assert.Contains(WorldEvents.TransferOffered, names);
        Assert.True(names.IndexOf(WorldEvents.ObjectiveComplete) < names.IndexOf(WorldEvents.TransferOffered), "the offer comes after the objective");
        Assert.Contains(run.Log, static l => l == "objective: Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 1/1");
        Assert.Contains(run.Log, static l => l == "talk: guide (host turn, guide-offer): ¿Quieres agua?");
    }

    [Fact]
    public void TheFillTakesTheFillTicksAndCompletesTheWaterStepOnlyWithABottle()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        var water = Stage.NearestRiver(session);
        Stage.WalkToWater(session, water);

        // No bottle yet: holding on the water gives the hint once, blocks nothing, and the step stays open.
        var (yaw, pitch) = VoxelWalker.AimAt(session, water);
        session.Step([VoxelCommand.Look(yaw, pitch), VoxelCommand.Interact(water.X, water.Y, water.Z)]);
        Assert.Equal([session.Locale.Ui(VoxelSession.NeedBottleHint)], session.TakeHints());
        session.Step([VoxelCommand.Look(yaw, pitch), VoxelCommand.Interact(water.X, water.Y, water.Z)]);
        Assert.Empty(session.TakeHints());
        Assert.Equal(0, session.Objective.Step("water")!.Count);
        Assert.Contains(session.Events, static e => e is WorldEvent { Name: WorldEvents.WaterWithoutBottle });

        // With a bottle: half a second, and the bottle of water, and the step, and no Spanish was needed to find the river.
        session.Simulation.GrantItems(ItemId.Bottle, 1);
        var ticks = Stage.Fill(session, water);

        Assert.Equal(VoxelSimulation.BottleFillTicks, ticks);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.WaterBottle));
        Assert.Equal("Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 1/1", session.Objective.Summary);
        Assert.True(session.Objective.Complete);
        Assert.Equal("agua", Assert.Single(session.Completed).Label);
        var cards = session.TakeWordCards();
        Assert.Contains(cards, static c => c.Lemma == "agua" && c.Phrase == "el agua");
    }

    [Fact]
    public void WalkingToTheRiverWithoutAskingIsAllowedAndLoggedAsFoundWithoutSpanish()
    {
        var run = PlayRoute(askVillager: false);
        foreach (var line in run.Log)
        {
            output.WriteLine(line);
        }

        var session = run.Session;
        Assert.True(session.Objective.Complete);
        Assert.False(session.RiverPointedOut);
        var found = Assert.Single(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.FoundWithoutSpanish);
        Assert.Equal("río", found.Detail);
        Assert.DoesNotContain(session.Events.OfType<PhraseUse>(), static u => u.PhraseId == PhraseIds.WhereIsTheRiver);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.BottleFilled);
    }

    [Fact]
    public void TheNoBottleHintBlocksNothingAndTheBottleIsNeverWithheldWithoutLanguage()
    {
        // The whole route without one correct Spanish word at the stall: help, help (the second is the fail-forward), then the river.
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        Assert.True(Stage.WalkToNpc(session, "shopkeeper"));
        _ = session.OpenTalk("shopkeeper");
        _ = session.Say("no sé");
        var carried = session.Say("no sé");
        Assert.True(carried.FailForward);
        session.EndTalk();

        var water = Stage.NearestRiver(session);
        Stage.WalkToWater(session, water);
        Assert.True(Stage.Fill(session, water) > 0);

        Assert.True(session.Objective.Complete);
        Assert.Contains(session.Events.OfType<PhraseUse>(), static u => u.Kind == PhraseUseKind.FailForward);
        Assert.DoesNotContain(session.Events.OfType<PhraseUse>(), static u => u.PhraseId == PhraseIds.RequestWater && u.Kind is PhraseUseKind.Supported or PhraseUseKind.Unaided);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.FoundWithoutSpanish);
    }

    [Fact]
    public void TwoRunsGiveTheSameEventsLogAndDigest()
    {
        var first = PlayRoute();
        var second = PlayRoute();

        Assert.Equal(first.Log, second.Log);
        Assert.Equal(first.Digest, second.Digest);
        Assert.Equal(first.Session.Events, second.Session.Events);
        Assert.Equal(
            first.Session.Conversation.Records.Count,
            second.Session.Conversation.Records.Count);
        Assert.Equal(first.Session.Conversation.Context.HostStateHash, second.Session.Conversation.Context.HostStateHash);
        // Nothing in the events is a clock: the stored time is the start plus the ticks.
        Assert.All(first.Session.Events, e => Assert.Equal(GuideSession.DefaultStartedAt + (e.Tick / VoxelSimulation.TicksPerSecond), e.Time));
    }

    [Fact]
    public void TheGuideOffersWaterOnceAfterTheObjectiveAndAcceptingGivesAnotherBottleOfWater()
    {
        var run = PlayRoute();
        var session = run.Session;

        var offers = session.Events.OfType<WorldEvent>().Count(static e => e.Name == WorldEvents.TransferOffered);
        Assert.Equal(1, offers);
        Assert.Equal("talking", session.Conversation.GuideState.StateId);

        Stage.TeleportToNpc(session, "guide");
        var again = session.OpenTalk("guide"); // not offered a second time
        Assert.False(again.HostTurn);
        Assert.Equal(1, session.Events.OfType<WorldEvent>().Count(static e => e.Name == WorldEvents.TransferOffered));
    }

    [Fact]
    public void ADeclinedTransferOfferGivesNothingAndStillCountsAsATransferUse()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        session.HostEvent(WorldEvents.BottleFilled); // the objective is complete (the fill itself is tested above)
        Stage.TeleportToNpc(session, "guide");

        var opening = session.OpenTalk("guide");
        Assert.Equal(["¿Quieres agua?"], opening.Lines);
        var turn = session.Say("no, gracias");

        Assert.Equal("deny", turn.Intent);
        Assert.Empty(turn.HostEvents);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.WaterBottle));
        var use = session.Events.OfType<PhraseUse>().Last();
        Assert.Equal((PhraseIds.NoThanks, PhraseUseKind.Transfer, "guide"), (use.PhraseId, use.Kind, use.Npc));
        Assert.True(session.Objective.Complete);
    }

    [Fact]
    public void TheStandOffIsAboutTwoBlocksFromEachNpcWithAClearLineToIt()
    {
        var session = TestGame.NewSession();
        foreach (var npc in TestGame.Data.Npcs.Ids)
        {
            var feet = session.Scene.Npc(npc).Feet;
            var stand = Stage.StandOffOf(session, npc);
            var dx = stand.X - feet.X;
            var dz = stand.Z - feet.Z;
            var distance = Math.Sqrt((dx * dx) + (dz * dz));
            output.WriteLine($"{npc}: feet {feet.X},{feet.Y},{feet.Z}, stand {stand.X},{stand.Y},{stand.Z}, {distance.ToString("0.0", CultureInfo.InvariantCulture)} blocks");
            Assert.InRange(distance, 2.0, 3.2);
            Assert.False(session.Scene.Grid.IsSolid(stand.X, stand.Y, stand.Z));
            Assert.True(session.Scene.Grid.IsSolid(stand.X, stand.Y - 1, stand.Z));
        }
    }

    [Fact]
    public void TheStandOffPrefersTheSideThePlayerComesFrom()
    {
        var session = TestGame.NewSession();
        var feet = session.Scene.Npc("villager").Feet;
        var grid = session.Scene.Grid;

        var fromNorth = NpcStandOff.Choose(grid, feet, new BlockPos(feet.X, feet.Y, feet.Z - 20))!.Value;
        var fromSouth = NpcStandOff.Choose(grid, feet, new BlockPos(feet.X, feet.Y, feet.Z + 20))!.Value;

        Assert.True(fromNorth.Z < feet.Z, "a player coming from the north stops north of the villager");
        Assert.True(fromSouth.Z > feet.Z, "a player coming from the south stops south of the villager");
        Assert.Equal(fromNorth, NpcStandOff.Choose(grid, feet, new BlockPos(feet.X, feet.Y, feet.Z - 20))!.Value); // a pure function
    }
}
