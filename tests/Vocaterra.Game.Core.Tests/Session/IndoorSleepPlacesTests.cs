using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>Sleep places indoors (Y05a): "at the place" and the wake after a death stop at walls, on game-v1's inn as the scene builds it.</summary>
public sealed class IndoorSleepPlacesTests
{
    private static BlockPos Standing(VoxelSession session, int x, int z, int nearY)
    {
        for (var y = nearY - 2; y <= nearY + 3; y++)
        {
            var feet = new BlockPos(x, y, z);
            if (VoxelWalker.IsStandable(session.Scene.Grid, feet))
            {
                return feet;
            }
        }

        throw new InvalidOperationException($"No standing room at {x}, {z}.");
    }

    // The inn's box: the village's west edge (village dx -16, dz 8; nine by seven).
    private static bool InsideInn(VoxelSession session, BlockPos cell)
    {
        var village = session.Scene.Point("village");
        var (x0, z0) = (village.X - 16, village.Z + 8);
        return cell.X >= x0 && cell.X <= x0 + 8 && cell.Z >= z0 && cell.Z <= z0 + 6;
    }

    [Fact]
    public void Given_ANightAtTheInnsBed_When_ThePlayerDiesLater_Then_TheyWakeInsideTheGuestRoomOnACellWalkedToFromTheBed()
    {
        var session = TestGame.NewSession();
        session.GrantLodging();
        var bed = session.SleepPlaces.Find("inn.bed")!;
        session.Simulation.TeleportTo(bed.Cell);
        SaveGame.Sleep(session, SaveGame.Capture(session, TestGame.Route()));
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);

        for (var i = 0; i < 60; i++)
        {
            session.Step([]);
        }

        Assert.Equal(1, session.DeathCount);
        var feet = session.Simulation.Player.FeetBlock;
        Assert.True(InsideInn(session, feet), $"woke at {feet}, outside the inn");
        var stand = Standing(session, bed.Cell.X, bed.Cell.Z, bed.Cell.Y);
        var path = VoxelWalker.FindPath(session.Scene.Grid, stand, cell => cell == feet);
        Assert.NotNull(path);
        Assert.All(path, cell => Assert.True(InsideInn(session, cell), $"the walk leaves the inn at {cell}"));
        Assert.True(path.Count <= 12);
    }

    [Fact]
    public void Given_ThePlayerOutsideTheGuestRoomsSouthWall_When_TheBedIsAsked_Then_TheyAreNotAtItAndInsideTheyAre()
    {
        var session = TestGame.NewSession();
        session.GrantLodging();
        var bed = session.SleepPlaces.Find("inn.bed")!;
        var inside = Standing(session, bed.Cell.X, bed.Cell.Z, bed.Cell.Y);
        var outside = Standing(session, bed.Cell.X, bed.Cell.Z + 2, bed.Cell.Y);

        Assert.True(SleepPlaces.IsWithinReach(bed, outside), "the radius alone would say yes, through the wall");
        Assert.False(InsideInn(session, outside));

        session.Simulation.TeleportTo(outside);
        Assert.False(session.AtSleepPlace);
        Assert.Null(session.CurrentSleepPlace);

        session.Simulation.TeleportTo(inside);
        Assert.Equal("inn.bed", session.CurrentSleepPlace?.Id);
    }

    [Fact]
    public void Given_ThePlayerOutsideTheSouthWallAndNoLodging_When_TheLockedBedIsAsked_Then_NoPromptOutsideAndThePromptInside()
    {
        var session = TestGame.NewSession();
        var bed = session.SleepPlaces.Find("inn.bed")!;

        session.Simulation.TeleportTo(Standing(session, bed.Cell.X, bed.Cell.Z + 2, bed.Cell.Y));
        Assert.False(session.AtLockedBed);

        session.Simulation.TeleportTo(Standing(session, bed.Cell.X, bed.Cell.Z, bed.Cell.Y));
        Assert.True(session.AtLockedBed);
    }

    [Fact]
    public void Given_ADeathAtTheOpenFire_When_ThePlayerWakes_Then_TheWakeCellIsTheStraightSearchsAsBefore()
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);
        var ring = session.Scene.Point(VoxelSession.CampFireRing);
        var expected = StraightWake(session, session.SleepPlaces.WakeAt!.Cell);

        for (var i = 0; i < 60; i++)
        {
            session.Step([]);
        }

        Assert.Equal(1, session.DeathCount);
        Assert.Equal(expected, session.Simulation.Player.FeetBlock);
        Assert.InRange(Math.Abs(ring.X - expected.X) + Math.Abs(ring.Z - expected.Z), 2, 10);
    }

    // What the pre-Y05a search picked: the nearest standable cell 2 to 5 blocks across, by distance, then height, then x, then z.
    private static BlockPos StraightWake(VoxelSession session, BlockPos middle)
    {
        BlockPos? best = null;
        (long D, int Dy, int X, int Z) bestKey = default;
        for (var x = middle.X - 5; x <= middle.X + 5; x++)
        {
            for (var z = middle.Z - 5; z <= middle.Z + 5; z++)
            {
                long d = ((long)(x - middle.X) * (x - middle.X)) + ((long)(z - middle.Z) * (z - middle.Z));
                if (d is > 25 or < 4)
                {
                    continue;
                }

                for (var y = middle.Y - 3; y <= middle.Y + 3; y++)
                {
                    var cell = new BlockPos(x, y, z);
                    if (!VoxelWalker.IsStandable(session.Scene.Grid, cell) || session.Fires.Middles.Contains(cell))
                    {
                        continue;
                    }

                    var key = (d, Math.Abs(y - middle.Y), x, z);
                    if (best is null || key.CompareTo(bestKey) < 0)
                    {
                        best = cell;
                        bestKey = key;
                    }
                }
            }
        }

        return best!.Value;
    }
}
