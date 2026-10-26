using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>Picking: the block ray from known poses.</summary>
public sealed class BlockRayTests
{
    // The flat scene's spawn: feet at (32.5, 10, 32.5), so the eye is at (32.5, 11.62, 32.5) and the ray at pitch 0 runs through layer 11.
    private static VoxelSimulation Sim(params (int X, int Y, int Z, BlockId Block)[] blocks) =>
        FlatSim(g =>
        {
            foreach (var (x, y, z, block) in blocks)
            {
                g.SetBlock(x, y, z, block);
            }
        });

    [Theory]
    [InlineData(0, 32, 11, 30, BlockFace.South)] // north: enters the south face
    [InlineData(1024, 35, 11, 32, BlockFace.West)] // east: enters the west face
    [InlineData(2048, 32, 11, 35, BlockFace.North)] // south: enters the north face
    [InlineData(3072, 29, 11, 32, BlockFace.East)] // west: enters the east face
    public void LookingAtABlockOnTheLevelHitsItThroughTheRightFace(int yaw, int x, int y, int z, BlockFace face)
    {
        var sim = Sim((x, y, z, BlockId.Stone));
        sim.Step([VoxelCommand.Look(yaw, 0)]);
        var hit = sim.Target;
        Assert.NotNull(hit);
        Assert.Equal(new BlockPos(x, y, z), hit.Value.Block);
        Assert.Equal(BlockId.Stone, hit.Value.Id);
        Assert.Equal(face, hit.Value.Face);
        Assert.Equal(new BlockPos(x, y, z).Offset(face), hit.Value.Adjacent);
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(hit.Value.Adjacent.X, hit.Value.Adjacent.Y, hit.Value.Adjacent.Z));
    }

    [Fact]
    public void TheRayReturnsTheNearestOfSeveralBlocksAndTheDistanceToItsFace()
    {
        var sim = Sim((32, 11, 29, BlockId.Stone), (32, 11, 30, BlockId.Wall), (32, 11, 31, BlockId.Dirt));
        var hit = BlockRay.Cast(sim.Grid, sim.Player);
        Assert.NotNull(hit);
        Assert.Equal(new BlockPos(32, 11, 31), hit.Value.Block); // face at z = 32, eye at z = 32.5
        Assert.Equal(Units / 2, hit.Value.DistanceUnits);
        Assert.Equal(BlockFace.South, hit.Value.Face);
    }

    [Fact]
    public void LookingStraightDownHitsTheGroundTopAndUpHitsNothing()
    {
        var sim = FlatSim();
        sim.Step([VoxelCommand.Look(0, -Angles.MaxPitch)]);
        var hit = sim.Target;
        Assert.NotNull(hit);
        Assert.Equal(new BlockPos(32, GroundTop, 32), hit.Value.Block);
        Assert.Equal(BlockFace.Up, hit.Value.Face);
        Assert.Equal(new BlockPos(32, GroundTop + 1, 32), hit.Value.Adjacent);
        Assert.InRange(hit.Value.DistanceUnits, VoxelPhysics.EyeHeight - 4, VoxelPhysics.EyeHeight + 4);

        sim.Step([VoxelCommand.Look(0, Angles.MaxPitch)]);
        Assert.Null(sim.Target);
    }

    [Fact]
    public void ALookingDownAndForwardRayLandsOnTheGroundAheadWithTheTopFace()
    {
        var sim = FlatSim();
        sim.Step([VoxelCommand.Look(1024, -300)]); // east, 26.4 degrees down
        var hit = sim.Target;
        Assert.NotNull(hit);
        Assert.Equal(BlockFace.Up, hit.Value.Face);
        Assert.Equal(GroundTop, hit.Value.Block.Y);
        Assert.True(hit.Value.Block.X > 32);
        Assert.Equal(32, hit.Value.Block.Z);
        // 1.62 blocks up and 26.4 degrees down: the top face is hit 1.62 / sin(26.4 degrees) = 3.65 blocks along the ray, which is in reach;
        // at 22.5 degrees it would be 4.23 blocks and out of reach.
        Assert.InRange(hit.Value.DistanceUnits, 3.6 * Units, 3.7 * Units);
        sim.Step([VoxelCommand.Look(1024, -256)]);
        Assert.Null(sim.Target);
    }

    [Fact]
    public void TheReachIsFourBlocksToTheFaceAndNotAUnitMore()
    {
        // Eye at x = 28.0 looking east; the block at x = 32 has its west face exactly four blocks away.
        var grid = FlatScene(g => g.SetBlock(32, 11, 32, BlockId.Stone)).Grid;
        var atReach = BlockRay.Cast(grid, 28 * Units, (11 * Units) + 2000, (32 * Units) + 2000, 1024, 0);
        Assert.NotNull(atReach);
        Assert.Equal(new BlockPos(32, 11, 32), atReach.Value.Block);
        Assert.Equal(4 * Units, atReach.Value.DistanceUnits);

        var beyond = BlockRay.Cast(grid, (28 * Units) - 1, (11 * Units) + 2000, (32 * Units) + 2000, 1024, 0);
        Assert.Null(beyond);

        // A longer reach finds it; a shorter one does not.
        Assert.NotNull(BlockRay.Cast(grid, (28 * Units) - 1, (11 * Units) + 2000, (32 * Units) + 2000, 1024, 0, 5));
        Assert.Null(BlockRay.Cast(grid, 28 * Units, (11 * Units) + 2000, (32 * Units) + 2000, 1024, 0, 3));
    }

    [Fact]
    public void FromTheSpawnABlockFourPointFiveBlocksAwayIsOutOfReachAndThreePointFiveIsIn()
    {
        var far = Sim((32, 11, 27, BlockId.Stone)); // face at z = 28; eye at z = 32.5: 4.5 away
        Assert.Null(far.Target);
        var near = Sim((32, 11, 28, BlockId.Stone)); // face at z = 29: 3.5 away
        Assert.Equal(new BlockPos(32, 11, 28), near.Target?.Block);
    }

    [Fact]
    public void ADiagonalRayFindsABlockOnTheDiagonal()
    {
        var sim = Sim((34, 11, 30, BlockId.Stone));
        sim.Step([VoxelCommand.Look(512, 0)]); // north-east
        Assert.Equal(new BlockPos(34, 11, 30), sim.Target?.Block);
    }

    [Fact]
    public void WalkThroughBlocksCanBeTargetedToo()
    {
        var sim = Sim((32, 11, 30, BlockId.BerryBush));
        Assert.Equal(BlockId.BerryBush, sim.Target?.Id);
        var water = Sim((32, 11, 30, BlockId.Water));
        Assert.Equal(BlockId.Water, water.Target?.Id);
    }

    [Fact]
    public void TheRayDoesNotStartInsideTheBlockAroundTheEye()
    {
        var sim = Sim((32, 11, 32, BlockId.Leaves), (32, 11, 30, BlockId.Stone));
        Assert.Equal(new BlockPos(32, 11, 30), sim.Target?.Block);
    }

    [Fact]
    public void ARayThatLeavesTheGridEndsWithNothing()
    {
        var grid = FlatScene().Grid;
        Assert.Null(BlockRay.Cast(grid, 1000, 11 * Units, 32 * Units, 3072, 0, 40));
        Assert.Null(BlockRay.Cast(grid, 32 * Units, 11 * Units, 32 * Units, 0, 0, 0, 4L * Units));
    }

    [Fact]
    public void ARayAcrossManyRandomPosesAgreesWithAFineMarch()
    {
        // Property: the exact grid walk finds the same block as marching along the ray in 1/256-block steps (the march can miss a corner that
        // the exact walk clips, so a handful of disagreements is allowed, and then the exact walk must be the nearer one).
        var rng = new WorldRng(5);
        var grid = FlatScene(g =>
        {
            for (var i = 0; i < 300; i++)
            {
                g.SetBlock(rng.NextInt(20, 44), rng.NextInt(GroundTop + 1, GroundTop + 5), rng.NextInt(20, 44), BlockId.Stone);
            }
        }).Grid;
        var hits = 0;
        var disagreements = 0;
        const int poses = 600;
        for (var i = 0; i < poses; i++)
        {
            var ox = (rng.NextInt(26, 38) * Units) + rng.NextInt(0, Units);
            var oy = ((GroundTop + 1) * Units) + rng.NextInt(0, 3 * Units);
            var oz = (rng.NextInt(26, 38) * Units) + rng.NextInt(0, Units);
            var yaw = rng.NextInt(0, Angles.StepsPerTurn);
            var pitch = rng.NextInt(-700, 700);
            var hit = BlockRay.Cast(grid, ox, oy, oz, yaw, pitch);
            var (dx, dy, dz) = Angles.Direction(yaw, pitch);
            var start = new BlockPos(ox / Units, oy / Units, oz / Units);
            BlockPos? marched = null;
            for (var step = 1; step <= 4 * 256 && marched is null; step++)
            {
                var t = step / 256.0 * Units;
                var cell = new BlockPos(
                    (int)Math.Floor((ox + (t * dx / Angles.One)) / Units),
                    (int)Math.Floor((oy + (t * dy / Angles.One)) / Units),
                    (int)Math.Floor((oz + (t * dz / Angles.One)) / Units));
                if (cell != start && grid.GetBlock(cell.X, cell.Y, cell.Z) != BlockId.Air)
                {
                    marched = cell;
                }
            }

            hits += hit is null ? 0 : 1;
            if (marched != hit?.Block)
            {
                disagreements++;
                Assert.True(hit is not null, "the march found a block the exact walk missed at pose " + i);
                Assert.True(marched is null || Dist(hit.Value.Block, ox, oy, oz) <= Dist(marched.Value, ox, oy, oz), "the exact walk must be the nearer one");
            }
        }

        Assert.True(hits > 100, $"only {hits} poses hit something");
        Assert.True(disagreements <= poses / 50, $"{disagreements} of {poses} poses disagree with the fine march");
    }

    private static double Dist(BlockPos block, int ox, int oy, int oz)
    {
        double dx = ((block.X + 0.5) * Units) - ox;
        double dy = ((block.Y + 0.5) * Units) - oy;
        double dz = ((block.Z + 0.5) * Units) - oz;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
