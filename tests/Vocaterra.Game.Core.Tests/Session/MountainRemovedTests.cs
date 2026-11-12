using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>Y08: the mountain camp is gone from the world, the data and the code; Luna's fire is the sleep place a player without a room has, the console names where a night ended, and the innkeeper follows the lodging.</summary>
public sealed class MountainRemovedTests
{
    private static void Sleep(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session, TestGame.Route()));

    private static void LightLunasRing(VoxelSession session)
    {
        var ring = session.Scene.Point(VoxelSession.CampFireRing);
        var middle = session.Fires.Middles.Single(m => m.X == ring.X && m.Z == ring.Z);
        session.Scene.Grid.SetBlock(middle.X + 1, middle.Y + 1, middle.Z, BlockId.Torch);
    }

    [Fact]
    public void Given_ANewGameAndADeathBeforeAnySleep_When_ThePlayerWakes_Then_TheyWakeBesideLunasFireRing()
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);

        for (var i = 0; i < 60; i++)
        {
            session.Step([]);
        }

        Assert.Equal(1, session.DeathCount);
        Assert.Null(session.LastSleepPlace);
        Assert.Equal("guide.fire", session.SleepPlaces.WakeAt?.Id);
        var ring = session.Scene.Point("firering");
        var feet = session.Simulation.Player.FeetBlock;
        var apart = Math.Sqrt((((long)ring.X - feet.X) * ((long)ring.X - feet.X)) + (((long)ring.Z - feet.Z) * ((long)ring.Z - feet.Z)));
        Assert.InRange(apart, 2, 5);
        Assert.True(VoxelWalker.IsStandable(session.Scene.Grid, feet));
    }

    [Fact]
    public void Given_LunasLitFireAndNoLodging_When_ThePlayerSleepsThere_Then_TheNextDayBeginsAndTheFireIsTheLastPlace()
    {
        var session = TestGame.NewSession();
        LightLunasRing(session);
        session.Simulation.TeleportTo(session.Scene.Point(VoxelSession.CampFireRing));
        Assert.False(session.LodgingGiven);
        Assert.Equal("guide.fire", session.CurrentSleepPlace?.Id);
        Assert.True(session.CanSleep);

        Sleep(session);

        Assert.Equal(2, session.Day);
        Assert.Equal("guide.fire", session.LastSleepPlace?.Id);
        Assert.False(session.LodgingGiven);
    }

    [Fact]
    public void Given_LunasColdFireAndNoLodging_When_ThePlayerStandsAtIt_Then_NoSleepIsOffered()
    {
        var session = TestGame.NewSession();
        session.Simulation.TeleportTo(session.Scene.Point(VoxelSession.CampFireRing));

        Assert.False(session.AtSleepPlace);
        Assert.False(session.CanSleep);
    }

    [Fact]
    public void Given_TheShippedBundle_When_ItsSleepPlacesAndPointsAreRead_Then_NoneIsNamedFireringPlayerOrCampFire()
    {
        var data = TestGame.Data;
        var scene = TestGame.Route();

        Assert.DoesNotContain(data.Needs.SleepPlaces, static p => p.Id == "camp.fire" || p.Point == "firering.player");
        Assert.Equal("guide.fire", data.Needs.Death.WakeAt);
        foreach (var gone in new[] { "firering.player", "camp.fire", "chest.player", "tent" })
        {
            Assert.DoesNotContain(gone, scene.Points.Keys);
        }

        Assert.All(data.Needs.SleepPlaces, place => Assert.Contains(place.Point, scene.Points.Keys));
    }

    [Fact]
    public void Given_ADeathWakeAtThatNamesNoSleepPlace_When_TheNeedsAreValidated_Then_TheProblemIsReported()
    {
        var needs = TestGame.Data.Needs;
        var broken = needs with { Death = needs.Death with { WakeAt = "camp.fire" } };

        Assert.Contains(NeedsValidator.Validate(broken), static p => p.Contains("wakeAt 'camp.fire' is not a listed sleep place", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_ANightAtTheInnsBedAtLunasFireAndWhereThePlayerFell_When_TheConsoleNamesWhereItEnded_Then_ItSaysTheInnTheFireOrWhereTheyFell()
    {
        var session = TestGame.NewSession();
        var bed = session.SleepPlaces.Find("inn.bed")!;
        var fire = session.SleepPlaces.Find("guide.fire")!;

        Assert.Equal("at the inn", SleepOffer.EndedWhere(session, bed, collapse: false));
        Assert.Equal("at the fire", SleepOffer.EndedWhere(session, fire, collapse: false));
        Assert.Equal("where the player fell", SleepOffer.EndedWhere(session, fire, collapse: true));
        Assert.Equal("wherever the player stood", SleepOffer.EndedWhere(session, null, collapse: false)); // the scripts' aid away from any place
    }

    [Fact]
    public void Given_TheRoomGivenWithoutTheInnkeepersTalk_When_ShePicksTheTalkUp_Then_SheIsInTheGivenStateAndGreetsWithGoodNight()
    {
        var session = TestGame.NewSession();
        Assert.Equal("idle", session.Conversation.StateOf("innkeeper"));

        session.GrantLodging(); // the --lodging aid and the skip give the room this way

        Assert.Equal(NpcService.LodgingState, session.Conversation.StateOf("innkeeper"));
        Stage.TeleportToNpc(session, "innkeeper");
        var opening = session.OpenTalk("innkeeper");
        Assert.Equal(["Buenas noches."], opening.Lines);
        Assert.False(opening.HostTurn);
    }

    [Fact]
    public void Given_ASessionSkippedToSessionTwo_When_TheInnkeeperIsOpenedOn_Then_SheDoesNotOfferTheRoomAgain()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Lessons.CountAsCompleted("first-words", 1);
        session.SkipToEnding();

        Assert.Equal("given", session.Conversation.StateOf("innkeeper"));
        Stage.TeleportToNpc(session, "innkeeper");
        Assert.Equal(["Buenas noches."], session.OpenTalk("innkeeper").Lines);
    }

    [Fact]
    public void Given_ASaveWithTheRoomGivenAndTheInnkeeperStillIdle_When_ItLoads_Then_SheIsGiven()
    {
        var session = TestGame.NewSession();
        session.GrantLodging();
        var saved = SaveGame.Save(session, TestGame.Route());
        var node = System.Text.Json.Nodes.JsonNode.Parse(saved)!;
        foreach (var npc in node["npcs"]!.AsArray())
        {
            if ((string?)npc!["agent"] == "innkeeper")
            {
                npc["stateId"] = "idle"; // a save from before the follow
            }
        }

        var loaded = SaveGame.Load(node.ToJsonString(), TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.True(loaded.LodgingGiven);
        Assert.Equal("given", loaded.Conversation.StateOf("innkeeper"));
    }

    [Fact]
    public void Given_ARoomTakenInTheInnkeepersOwnTalk_When_ItIsGiven_Then_SheIsGivenOnceAndTheEventIsRaisedOnce()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "innkeeper");
        _ = session.OpenTalk("innkeeper");
        _ = session.Say("sí");
        session.EndTalk();

        Assert.Equal("given", session.Conversation.StateOf("innkeeper"));
        Assert.Equal(1, session.Events.OfType<WorldEvent>().Count(static e => e.Name == WorldEvents.LodgingGranted));
    }
}
