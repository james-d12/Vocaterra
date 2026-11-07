using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.Physics;

/// <summary>A swimmer carried by a current (water-plan 4.3 and 4.6): downstream in a river, still in a lake, harder in a stream.</summary>
public sealed class CurrentDriftTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int Ground = VoxelTestSupport.GroundTop;
    private const int CentreX = (32 * U) + (U / 2);

    // A pool of water 3 deep in a 64 x 64 world, 20 blocks long north-south (z 20 to 44) and 8 wide.
    private static VoxelGrid Pool()
    {
        var grid = VoxelTestSupport.FlatScene().Grid;
        for (var z = 20; z <= 44; z++)
        {
            for (var x = 28; x <= 36; x++)
            {
                for (var y = Ground - 2; y <= Ground; y++)
                {
                    grid.SetBlock(x, y, z, y == Ground - 2 ? BlockId.Stone : BlockId.Water);
                }
            }
        }

        return grid;
    }

    private static PlayerBody Swimmer(VoxelGrid grid) => new(grid, CentreX, (Ground - 1) * U, (24 * U) + (U / 2), 0);

    private static void Run(PlayerBody body, VoxelGrid grid, RiverCurrents? currents, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            body.Step(grid, 0, 0, true, currents);
        }
    }

    [Fact]
    public void Given_ARiver_When_ASwimmerHoldsStill_Then_ItDriftsDownstreamAtTheRiverSpeed()
    {
        var grid = Pool();
        var river = new RiverSpec(4, 2, 2, [(32, 20), (32, 44)]);
        var currents = RiverCurrents.Build(river, grid);
        var body = Swimmer(grid);
        var start = body.CenterZ;

        Run(body, grid, currents, 120);

        var speed = (body.CenterZ - start) / (double)U / 2.0; // two seconds
        Assert.InRange(speed, 0.5, 0.7);
        Assert.InRange(Math.Abs(body.CenterX - CentreX), 0, U / 4);
    }

    [Fact]
    public void Given_ALake_When_ASwimmerHoldsStill_Then_ItStaysWhereItIs()
    {
        var grid = Pool();
        var body = Swimmer(grid);
        var start = (body.CenterX, body.CenterZ);

        Run(body, grid, RiverCurrents.Build(null, grid), 120);

        Assert.Equal(start, (body.CenterX, body.CenterZ));
    }

    [Fact]
    public void Given_FlowingWater_When_ASwimmerHoldsStill_Then_ItIsPushedFasterThanInARiver()
    {
        var grid = Pool();
        // A slope: the water thins to the east, along the whole length of the swimmer's drift.
        for (var x = 28; x <= 36; x++)
        {
            for (var z = 20; z <= 44; z++)
            {
                grid.SetWater(x, Ground, z, WaterLevel.Flowing(Math.Min(x - 27, 7)));
                grid.SetWater(x, Ground - 1, z, WaterLevel.Flowing(Math.Min(x - 27, 7)));
            }
        }

        var body = Swimmer(grid);
        var start = body.CenterX;

        Run(body, grid, null, 30);

        var speed = (body.CenterX - start) / (double)U / 0.5;
        Assert.InRange(speed, 0.8, 1.2);
    }

    [Fact]
    public void Given_TheShippedRiver_When_ASwimmerHoldsStill_Then_ItDriftsAlongTheRiverAndTheRunIsRepeatable()
    {
        (int X, int Z, string Digest) Drift()
        {
            var scene = RouteScene.Build();
            var sim = VoxelSimulation.Create(scene);
            var spot = scene.Point("river");
            sim.TeleportTo(new BlockPos(spot.X, spot.Y - 1, spot.Z));
            for (var i = 0; i < 180; i++)
            {
                sim.Step();
            }

            return (sim.Player.CenterX - ((spot.X * U) + (U / 2)), sim.Player.CenterZ - ((spot.Z * U) + (U / 2)), sim.ComputeStateDigest());
        }

        var first = Drift();
        var second = Drift();

        Assert.Equal(first, second);
        var metres = Math.Sqrt(((double)first.X * first.X) + ((double)first.Z * first.Z)) / U;
        Assert.InRange(metres, 1.2, 2.0); // three seconds at about 0.6 blocks a second
    }
}
