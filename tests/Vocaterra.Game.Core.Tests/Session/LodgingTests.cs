using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>Lodging (Y05): the host action <c>grant_lodging</c>, the world event <c>lodging_granted</c>, a night and a death at the inn's bed, and the bed's prompt.</summary>
public sealed class LodgingTests
{
    private const string Table = """{ "schemaVersion": 2, "events": [ { "npc": "innkeeper", "event": "innkeeper.room", "action": { "kind": "grant_lodging" } } ] }""";

    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    // The innkeeper's talk turn is what grants lodging in play (Y06): the player stands at the counter, opens the talk and says yes.
    private static VoxelSession WithInnkeeper()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "innkeeper");
        return session;
    }

    private static void Grant(VoxelSession session)
    {
        _ = session.OpenTalk("innkeeper");
        _ = session.Say("sí");
        session.EndTalk();
    }

    private static BlockPos BedCell(VoxelSession session) => session.SleepPlaces.Find("inn.bed")!.Cell;

    private static int Granted(VoxelSession session) => session.Events.OfType<WorldEvent>().Count(e => e.Name == WorldEvents.LodgingGranted);

    private static void Sleep(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session));

    [Fact]
    public void Given_AGrantLodgingRow_When_TheTableIsParsedAndValidated_Then_TheKindIsKnownAndNoProblemIsFound()
    {
        var table = HostEventTable.Parse(Table, TestItems.Catalogue);

        Assert.Equal(NpcEffectKind.GrantLodging, table.Of("innkeeper", "innkeeper.room").Kind);
        Assert.Contains("grant_lodging", HostEventTable.Kinds);
        var problems = GameDataValidator.Validate(TestGame.Data.WithHostEvents(table)).Where(static p => p.Contains("innkeeper", StringComparison.Ordinal) || p.Contains("grant_lodging", StringComparison.Ordinal)).ToList();
        Assert.Empty(problems); // the innkeeper is an NPC since Y06, and the kind is accepted
    }

    [Fact]
    public void Given_NoLodging_When_TheInnkeepersTalkGrantsIt_Then_LodgingIsGivenAndTheEventIsRaisedOnce()
    {
        var session = WithInnkeeper();
        Assert.False(session.LodgingGiven);

        Grant(session);
        Grant(session);

        Assert.True(session.LodgingGiven);
        Assert.Equal(1, Granted(session));
        Assert.Contains(WorldEvents.LodgingGranted, WorldEvents.All);
        Assert.Contains(WorldEvents.LodgingGranted, Trigger.TipEvents);
    }

    [Fact]
    public void Given_TheLodgingGrantedEvent_When_TheAutosaveAsksWhyItIsDue_Then_ItSaysLodgingGranted()
    {
        var session = WithInnkeeper();
        Grant(session);

        var reasons = session.Events.Select(AutosavePolicy.ReasonOf).Where(static r => r is not null).ToList();

        Assert.Contains("lodging granted", reasons);
    }

    [Fact]
    public void Given_LodgingGivenInPlay_When_ThePlayerSleepsAndTheSaveIsLoadedFromAFile_Then_LodgingIsStillGiven()
    {
        var session = WithInnkeeper();
        Grant(session);
        session.Simulation.TeleportTo(BedCell(session));

        Sleep(session);
        Assert.True(session.LodgingGiven);

        var path = Path.Combine(Path.GetTempPath(), $"vocaterra-y05-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, SaveGame.Serialize(SaveGame.Capture(session)));
            var loaded = SaveGame.Load(File.ReadAllText(path), TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);
            Assert.True(loaded.LodgingGiven);
            Assert.Equal("inn.bed", loaded.LastSleepPlace?.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Given_TheInnsBed_When_LodgingIsGiven_Then_ItIsUsableOnlyAfterwards()
    {
        var session = TestGame.NewSession();
        var bed = session.SleepPlaces.Find("inn.bed")!;
        session.Simulation.TeleportTo(bed.Cell);

        Assert.False(session.SleepPlaces.IsUsable(bed));
        Assert.False(session.AtSleepPlace);
        Assert.False(session.CanSleep);

        session.GrantLodging();

        Assert.True(session.SleepPlaces.IsUsable(bed));
        Assert.Equal("inn.bed", session.CurrentSleepPlace?.Id);
        Assert.True(session.CanSleep);
    }

    [Fact]
    public void Given_ANightAtTheBedAndOneAtTheFire_When_BothSleep_Then_TheSessionAndTheNeedsAreTheSame()
    {
        var atBed = TestGame.NewSession();
        atBed.GrantLodging();
        atBed.Simulation.TeleportTo(BedCell(atBed));
        atBed.SetNeed(NeedMeter.Sleep, 20);
        var atFire = TestGame.NewSession();
        var ring = atFire.Scene.Point(VoxelSession.CampFireRing);
        var middle = atFire.Fires.Middles.Single(m => m.X == ring.X && m.Z == ring.Z);
        atFire.Scene.Grid.SetBlock(middle.X + 1, middle.Y + 1, middle.Z, BlockId.Torch); // a lit ring is a usable fire
        atFire.Simulation.TeleportTo(ring);
        atFire.SetNeed(NeedMeter.Sleep, 20);

        Sleep(atBed);
        Sleep(atFire);

        Assert.Equal(atFire.Day, atBed.Day);
        Assert.Equal(atFire.Sitting, atBed.Sitting);
        foreach (var meter in Enum.GetValues<NeedMeter>())
        {
            Assert.Equal(atFire.Needs.Value(meter), atBed.Needs.Value(meter));
        }

        Assert.Equal("inn.bed", atBed.LastSleepPlace?.Id);
        Assert.Equal("guide.fire", atFire.LastSleepPlace?.Id);
    }

    [Fact]
    public void Given_ANightAtTheBed_When_ThePlayerDiesLater_Then_TheyWakeOnFreeGroundBesideTheBedAndNotAtTheFire()
    {
        var session = TestGame.NewSession();
        session.GrantLodging();
        session.Simulation.TeleportTo(BedCell(session));
        Sleep(session);
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);
        session.Simulation.TeleportTo(session.Scene.Point(VoxelSession.CampFireRing));

        for (var i = 0; i < 60; i++)
        {
            session.Step([]);
        }

        Assert.Equal(1, session.DeathCount);
        var feet = session.Simulation.Player.FeetBlock;
        var bed = BedCell(session);
        var apart = Math.Sqrt((((long)bed.X - feet.X) * ((long)bed.X - feet.X)) + (((long)bed.Z - feet.Z) * ((long)bed.Z - feet.Z)));
        Assert.InRange(apart, 2, 5);
        Assert.True(VoxelWalker.IsStandable(session.Scene.Grid, feet));
    }

    [Fact]
    public void Given_ABedWhoseAccessIsNotMet_When_ThePlayerStandsAtIt_Then_ThePromptSaysTheBedIsTheInnsAndAnOpenBedOffersTheSleep()
    {
        var session = TestGame.NewSession();
        session.Simulation.TeleportTo(BedCell(session));

        Assert.True(SleepOffer.AtLockedBed(session, free: true));
        Assert.False(SleepOffer.AtLockedBed(session, free: false));
        Assert.Equal(session.Locale.Ui("sleepBedLocked"), SleepOffer.LockedBedText(session));
        Assert.Contains("inn", SleepOffer.LockedBedText(session), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sleep until morning", SleepOffer.LockedBedText(session), StringComparison.Ordinal);

        session.GrantLodging();

        Assert.False(SleepOffer.AtLockedBed(session, free: true));
        Assert.Contains("sleep until morning", SleepOffer.PromptText(session), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AwayFromEverySleepPlace_When_TheBedsPromptIsAsked_Then_NothingIsOffered()
    {
        var session = TestGame.NewSession();
        var bed = BedCell(session);
        session.Simulation.TeleportTo(new BlockPos(bed.X + 40, bed.Y, bed.Z));

        Assert.False(SleepOffer.AtLockedBed(session, free: true));
    }
}
