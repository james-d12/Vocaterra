using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Physics;

/// <summary>K03: the body against the true shapes of the oak blocks: half-block steps, stairs, fences, gates, doors and trapdoors. Worlds are built with <c>SetBlock(x, y, z, id, state)</c>; nothing places oak blocks yet.</summary>
public sealed class ShapedBodyTests
{
    private const int Floor = GroundTop + 1; // the cell row the feet stand in
    private static int FloorFeet => Floor * Units;

    private static VoxelSimulation WallSim(BlockId id, byte state, int x = 36, int y = Floor, int ySecond = -1, BlockId second = BlockId.Air, byte secondState = 0) =>
        FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                g.SetBlock(x, y, z, id, state);
                if (second != BlockId.Air)
                {
                    g.SetBlock(x, ySecond, z, second, secondState);
                }
            }
        });

    private static (bool Stepped, int MaxRise) Walk(VoxelSimulation sim, int ticks, int dx = 1, int dz = 0)
    {
        var stepped = false;
        var maxRise = 0;
        for (var tick = 0; tick < ticks; tick++)
        {
            var before = sim.Player.FeetY;
            sim.Step([VoxelCommand.Move(dx, dz)]);
            stepped |= sim.Player.SteppedUp;
            maxRise = Math.Max(maxRise, sim.Player.FeetY - before);
        }

        return (stepped, maxRise);
    }

    [Fact]
    public void AGroundedBodyWalksOntoABottomSlabWithoutJumping()
    {
        // The slab covers everything east of x 36: the body climbs half a block and stays on its top.
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                for (var x = 36; x < 64; x++)
                {
                    g.SetBlock(x, Floor, z, BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Bottom));
                }
            }
        });
        var (stepped, _) = Walk(sim, 200);
        Assert.True(stepped);
        Assert.True(sim.Player.MinX >= 37 * Units, "the body walked over the slab's edge");
        Assert.Equal(FloorFeet + (Units / 2), sim.Player.FeetY);
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void WalkingDownOffASlabDropsNormally()
    {
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                for (var x = 34; x < 37; x++)
                {
                    g.SetBlock(x, Floor, z, BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Bottom));
                }
            }
        });
        var (stepped, _) = Walk(sim, 300);
        Assert.True(stepped); // up onto the slab row first
        Assert.True(sim.Player.MinX >= 37 * Units, "the body walked off the far edge");
        Assert.Equal(FloorFeet, sim.Player.FeetY);
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void ARunOfFourStairsIsClimbedInOneWalkInHalfBlockSteps()
    {
        // Stairs facing east (the high step towards +X), each one block higher and one block further east, on a filled column, ending on a full-block platform.
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                for (var k = 0; k < 4; k++)
                {
                    for (var y = Floor; y < Floor + k; y++)
                    {
                        g.SetBlock(36 + k, y, z, BlockId.Stone);
                    }

                    g.SetBlock(36 + k, Floor + k, z, BlockId.OakStairs, BlockState.Stairs.Pack(Facing.East, false));
                }

                for (var x = 40; x < 64; x++)
                {
                    for (var y = Floor; y < Floor + 4; y++)
                    {
                        g.SetBlock(x, y, z, BlockId.Stone);
                    }
                }
            }
        });
        var (stepped, maxRise) = Walk(sim, 600);
        Assert.True(stepped);
        Assert.True(sim.Player.MinX >= 41 * Units, "the body walked up the four stairs and onto the platform");
        Assert.Equal((Floor + 4) * Units, sim.Player.FeetY);
        Assert.True(maxRise <= Units / 2, $"each step is half a block (the largest rise in a tick was {maxRise} units)");
    }

    [Fact]
    public void AFullBlockStillClimbsAsBefore()
    {
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                for (var x = 36; x < 64; x++)
                {
                    g.SetBlock(x, Floor, z, BlockId.Stone);
                }
            }
        });
        var (stepped, _) = Walk(sim, 200);
        Assert.True(stepped);
        Assert.True(sim.Player.MinX >= 37 * Units);
        Assert.Equal(FloorFeet + Units, sim.Player.FeetY);
    }

    [Fact]
    public void AnOakFenceIsOneAndAHalfBlocksAndIsNeverClimbedOrJumped()
    {
        var sim = WallSim(BlockId.OakFence, 0);
        var (stepped, _) = Walk(sim, 300);
        Assert.False(stepped);
        Assert.Equal((36 * Units) + (6 * (Units / 16)) - VoxelPhysics.PlayerWidth, sim.Player.MinX); // stopped by the post's west face, at 6/16
        Assert.Equal(FloorFeet, sim.Player.FeetY);
        Run(sim, 300, VoxelCommand.Move(1, 0), VoxelCommand.Jump);
        Run(sim, 120);
        Assert.True(sim.Player.MaxX <= (36 * Units) + (6 * (Units / 16)), "a jump (about 1.3 blocks) does not clear 1.5");
        Assert.Equal(FloorFeet, sim.Player.FeetY);
    }

    [Fact]
    public void ATopSlabAtHeadHeightStopsTheStepOntoTheBlockBeneathIt()
    {
        // A full block with a top slab above it: lifted one block the body's head would be inside the slab.
        var sim = WallSim(BlockId.Stone, 0, ySecond: Floor + 1, second: BlockId.OakSlab, secondState: BlockState.Slab.Pack(SlabKind.Top));
        var (stepped, _) = Walk(sim, 300);
        Assert.False(stepped);
        Assert.Equal(36 * Units, sim.Player.MaxX);
        Assert.Equal(FloorFeet, sim.Player.FeetY);
    }

    [Fact]
    public void ATopSlabAloneAtHeadHeightIsNeverClimbed()
    {
        // A top slab in the cell the feet stand in covers 0.5 to 1 above the floor: it starts above the feet, so it is no step.
        var sim = WallSim(BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Top));
        var (stepped, _) = Walk(sim, 300);
        Assert.False(stepped);
        Assert.Equal(36 * Units, sim.Player.MaxX);
        Assert.Equal(FloorFeet, sim.Player.FeetY);
    }

    [Fact]
    public void AnUpsideDownStairIsNotClimbedFromItsOpenSide()
    {
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                g.SetBlock(36, Floor, z, BlockId.OakStairs, BlockState.Stairs.Pack(Facing.East, true));
            }
        });
        var (stepped, _) = Walk(sim, 300);
        Assert.False(stepped);
        Assert.Equal(FloorFeet, sim.Player.FeetY);
    }

    [Fact]
    public void AnOpenGateLetsTheBodyThroughAndAClosedOneDoesNot()
    {
        // A fence line along Z: gates facing east or west.
        var open = WallSim(BlockId.OakFenceGate, BlockState.Gate.Pack(Facing.East, true));
        Walk(open, 200);
        Assert.True(open.Player.MinX >= 37 * Units, "the body walked through the open gates");

        var closed = WallSim(BlockId.OakFenceGate, BlockState.Gate.Pack(Facing.East, false));
        var (stepped, _) = Walk(closed, 300);
        Assert.False(stepped);
        Assert.Equal((36 * Units) + (7 * (Units / 16)), closed.Player.MaxX); // the bar's west face, at 7/16
        Assert.Equal(FloorFeet, closed.Player.FeetY);
    }

    [Fact]
    public void AClosedTrapdoorOnTheFloorIsStoodOn()
    {
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                for (var x = 36; x < 64; x++)
                {
                    g.SetBlock(x, Floor, z, BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.North, false, false));
                }
            }
        });
        Walk(sim, 200);
        Assert.True(sim.Player.MinX >= 37 * Units);
        Assert.Equal(FloorFeet + (3 * (Units / 16)), sim.Player.FeetY);
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void AnOpenDoorsPanelBlocksOnlyItsStrip()
    {
        // A door facing north, open, the hinge on the left: the panel is the west 3 sixteenths of the cell, full height and full depth.
        var grid = FlatScene(g =>
        {
            g.SetBlock(36, Floor, 30, BlockId.OakDoor, BlockState.Door.Pack(Facing.North, true, false, false));
            g.SetBlock(36, Floor + 1, 30, BlockId.OakDoor, BlockState.Door.Pack(Facing.North, true, false, true));
        }).Grid;
        var panelEast = (36 * Units) + (3 * (Units / 16));
        var y = FloorFeet;
        var z = (30 * Units) + 100;
        Assert.False(VoxelCollider.IsFree(grid, panelEast - 1, y, z), "a body touching the strip is stopped");
        Assert.True(VoxelCollider.IsFree(grid, panelEast, y, z), "a body beside the strip, inside the same cell, is not");
        Assert.True(VoxelCollider.IsFree(grid, panelEast + 500, y, z));
    }

    [Fact]
    public void ABodyMovesThroughTheOpenSideOfAnOpenDoorsCell()
    {
        var grid = FlatScene(g =>
        {
            g.SetBlock(36, Floor, 28, BlockId.OakDoor, BlockState.Door.Pack(Facing.North, true, false, false));
            g.SetBlock(36, Floor + 1, 28, BlockId.OakDoor, BlockState.Door.Pack(Facing.North, true, false, true));
        }).Grid;
        // The body's west edge at 36 + 0.2: east of the 3-sixteenth strip, so a walk north along the cell is free.
        var x = (36 * Units) + 820;
        var z = (29 * Units) + 100;
        var y = FloorFeet;
        var blocked = false;
        for (var step = 0; step < 100; step++)
        {
            blocked |= VoxelCollider.MoveZ(grid, x, y, ref z, -294);
        }

        Assert.False(blocked);
        Assert.True(z < 27 * Units, "the body walked through the cell");
    }

    [Fact]
    public void ATopOfAFenceIsStoodOnHalfABlockAboveItsCell()
    {
        // The post's collision reaches 24 sixteenths: a body dropped onto it lands at 1.5 blocks, in the cell above the fence.
        var grid = FlatScene(g => g.SetBlock(32, Floor, 32, BlockId.OakFence, 0)).Grid;
        var x = (32 * Units) + (Units / 2) - (VoxelPhysics.PlayerWidth / 2);
        var z = x;
        var y = (Floor + 3) * Units;
        var stopped = VoxelCollider.MoveY(grid, x, ref y, z, -(3 * Units));
        Assert.True(stopped);
        Assert.Equal((Floor * Units) + (24 * (Units / 16)), y);
    }

    [Fact]
    public void ABodyStandsOnTheHighestBoxUnderItAStairsHighStepNotItsLowOne()
    {
        var grid = FlatScene(g => g.SetBlock(32, Floor, 32, BlockId.OakStairs, BlockState.Stairs.Pack(Facing.North, false))).Grid;
        var x = (32 * Units) + (Units / 2) - (VoxelPhysics.PlayerWidth / 2);
        var z = (32 * Units) + (Units / 2) - (VoxelPhysics.PlayerWidth / 2); // over the middle: both halves under the box
        var y = (Floor + 3) * Units;
        Assert.True(VoxelCollider.MoveY(grid, x, ref y, z, -(3 * Units)));
        Assert.Equal((Floor + 1) * Units, y);
    }

    [Fact]
    public void TheBlockRulesForPlanningReadTheShapes()
    {
        var grid = FlatScene(g =>
        {
            g.SetBlock(10, Floor, 10, BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Bottom));
            g.SetBlock(11, Floor, 10, BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Double));
            g.SetBlock(12, Floor, 10, BlockId.OakFenceGate, BlockState.Gate.Pack(Facing.East, true));
            g.SetBlock(13, Floor, 10, BlockId.OakFence, 0);
            g.SetBlock(14, Floor, 10, BlockId.OakLog, BlockState.Log.Pack(LogAxis.Y));
            g.SetBlock(15, Floor, 10, BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.North, false, false));
        }).Grid;
        Assert.True(grid.BlocksBody(10, Floor, 10), "a slab blocks (conservative)");
        Assert.True(grid.BlocksBody(11, Floor, 10));
        Assert.False(grid.BlocksBody(12, Floor, 10), "an open gate has no collision");
        Assert.True(grid.BlocksBody(13, Floor, 10));
        Assert.True(grid.BlocksBody(13, Floor + 1, 10), "a fence collides half a block into the cell above");
        Assert.False(grid.BlocksBody(13, Floor + 2, 10));
        Assert.True(grid.BlocksBody(14, Floor, 10));
        Assert.True(grid.BlocksBody(15, Floor, 10));
        Assert.False(grid.BlocksBody(10, Floor + 1, 10), "the cell above a slab is free");

        Assert.False(grid.CanStandOn(10, Floor, 10), "no NPC is planned onto a bottom slab");
        Assert.True(grid.CanStandOn(11, Floor, 10), "a double slab is a full cube");
        Assert.False(grid.CanStandOn(12, Floor, 10));
        Assert.False(grid.CanStandOn(13, Floor, 10));
        Assert.True(grid.CanStandOn(14, Floor, 10));
        Assert.False(grid.CanStandOn(15, Floor, 10));
        Assert.True(grid.CanStandOn(10, GroundTop, 10), "stone still can be stood on");
        Assert.True(grid.BlocksBody(10, GroundTop, 10));
    }

    [Fact]
    public void PlacementOverlapReadsTheTrueShape()
    {
        var sim = FlatSim();
        var cell = new BlockPos(32, Floor, 32); // the body stands in this cell: x 32.2 to 32.8, z 32.2 to 32.8
        Assert.True(sim.OverlapsPlayer(cell));
        Assert.True(sim.OverlapsPlayer(cell, BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Bottom)));
        Assert.True(sim.OverlapsPlayer(cell, BlockId.OakFence, 0));
        // A closed door facing north is the strip of z 13 to 16 sixteenths: the body ends at z 32.8, short of 32.8125.
        Assert.False(sim.OverlapsPlayer(cell, BlockId.OakDoor, BlockState.Door.Pack(Facing.North, false, false, false)));
        // A fence one cell east is clear of the body.
        Assert.False(sim.OverlapsPlayer(new BlockPos(33, Floor, 32), BlockId.OakFence, 0));
        // A trapdoor lying in the cell below the feet is clear.
        Assert.False(sim.OverlapsPlayer(new BlockPos(32, GroundTop, 32), BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.North, false, false)));
    }
}
