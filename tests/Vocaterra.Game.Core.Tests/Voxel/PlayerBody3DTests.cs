using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The player body on hand-built voxel worlds: landing, walls, steps, jumps, edges and the never-inside-a-solid property.</summary>
public sealed class PlayerBody3DTests
{
    private static int GroundFeet => (GroundTop + 1) * Units;

    [Fact]
    public void TheBodyHasTheDesignSize()
    {
        var player = FlatSim().Player;
        Assert.Equal(VoxelPhysics.PlayerWidth, player.MaxX - player.MinX);
        Assert.Equal(VoxelPhysics.PlayerHeight, player.MaxY - player.MinY);
        Assert.Equal(VoxelPhysics.PlayerWidth, player.MaxZ - player.MinZ);
        Assert.InRange(VoxelPhysics.PlayerWidth / (double)Units, 0.599, 0.601);
        Assert.InRange(VoxelPhysics.PlayerHeight / (double)Units, 1.799, 1.801);
        Assert.InRange(VoxelPhysics.EyeHeight / (double)Units, 1.619, 1.621);
        Assert.Equal(player.FeetY + VoxelPhysics.EyeHeight, player.EyeY);
    }

    [Fact]
    public void SpawnStandsOnTheGroundAtRest()
    {
        var sim = FlatSim();
        Assert.True(sim.Player.Grounded);
        Assert.Equal(GroundFeet, sim.Player.FeetY);
        Assert.Equal((32 * Units) + (Units / 2), sim.Player.CenterX);
        Run(sim, 120);
        Assert.Equal(GroundFeet, sim.Player.FeetY);
        Assert.Equal(0, sim.Player.VelocityY);
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void ARouteSceneSpawnStaysPutAndLooksTheWayTheLayoutSays()
    {
        var scene = BuildRoute(42);
        var sim = VoxelSimulation.Create(scene);
        var before = (sim.Player.MinX, sim.Player.MinY, sim.Player.MinZ);
        Run(sim, 120);
        Assert.Equal(before, (sim.Player.MinX, sim.Player.MinY, sim.Player.MinZ));
        Assert.True(sim.Player.Grounded);
        Assert.Equal(scene.Spawn.Feet, sim.Player.FeetBlock);
        Assert.Equal(Angles.FromDegrees(135), sim.Player.Yaw);
    }

    [Fact]
    public void ABodyDroppedFromTheAirLandsAndStops()
    {
        var sim = FlatSim();
        sim.TeleportTo(new BlockPos(32, GroundTop + 12, 32));
        Assert.False(sim.Player.Grounded);
        var landedAt = -1;
        var previousY = sim.Player.FeetY;
        for (var tick = 0; tick < 200; tick++)
        {
            sim.Step();
            Assert.True(sim.Player.FeetY <= previousY, "falling never rises");
            previousY = sim.Player.FeetY;
            if (sim.Events.Any(e => e.Kind == SimulationEventKind.PlayerLanded))
            {
                landedAt = tick;
                break;
            }
        }

        Assert.True(landedAt > 10, "a twelve-block fall takes a while");
        Assert.Equal(GroundFeet, sim.Player.FeetY);
        Assert.Equal(0, sim.Player.VelocityY);
        Assert.True(sim.Player.Grounded);
        Run(sim, 30);
        Assert.Equal(GroundFeet, sim.Player.FeetY);
    }

    [Fact]
    public void WalkingIntoAWallStopsAtItsFace()
    {
        var sim = FlatSim(g =>
        {
            for (var z = 28; z <= 36; z++)
            {
                for (var y = GroundTop + 1; y <= GroundTop + 3; y++)
                {
                    g.SetBlock(40, y, z, BlockId.Wall);
                }
            }
        });
        Run(sim, 300, VoxelCommand.Move(1, 0));
        Assert.Equal(40 * Units, sim.Player.MaxX);
        Assert.Equal(0, sim.Player.VelocityX);
        Assert.Equal(GroundFeet, sim.Player.FeetY);

        // And it slides along the wall when it pushes diagonally into it.
        var z0 = sim.Player.CenterZ;
        Run(sim, 60, VoxelCommand.Move(1, 1));
        Assert.Equal(40 * Units, sim.Player.MaxX);
        Assert.True(sim.Player.CenterZ > z0);
    }

    [Fact]
    public void AllFourWallsAndTheGridEdgeStopTheBody()
    {
        var sim = FlatSim();
        Run(sim, 2000, VoxelCommand.Move(-1, 0));
        Assert.Equal(0, sim.Player.MinX);
        Run(sim, 2000, VoxelCommand.Move(0, -1));
        Assert.Equal(0, sim.Player.MinZ);
        Run(sim, 2000, VoxelCommand.Move(1, 0));
        Assert.Equal(64 * Units, sim.Player.MaxX);
        Run(sim, 2000, VoxelCommand.Move(0, 1));
        Assert.Equal(64 * Units, sim.Player.MaxZ);
        Assert.Equal(GroundFeet, sim.Player.FeetY);
    }

    [Fact]
    public void AOneBlockStepIsClimbedByWalking()
    {
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                for (var x = 36; x < 64; x++)
                {
                    g.SetBlock(x, GroundTop + 1, z, BlockId.Stone);
                }
            }
        });
        var stepped = false;
        for (var tick = 0; tick < 200; tick++)
        {
            sim.Step([VoxelCommand.Move(1, 0)]);
            stepped |= sim.Player.SteppedUp;
        }

        Assert.True(stepped);
        Assert.True(sim.Player.MinX >= 37 * Units, "the body walked over the step");
        Assert.Equal(GroundFeet + Units, sim.Player.FeetY);
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void AStepUpWorksInEveryHorizontalDirection()
    {
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var sim = FlatSim(g =>
            {
                // A raised floor on the far side of a line four blocks away from the spawn.
                for (var z = 0; z < 64; z++)
                {
                    for (var x = 0; x < 64; x++)
                    {
                        if (((x - 32) * dx) + ((z - 32) * dz) >= 4)
                        {
                            g.SetBlock(x, GroundTop + 1, z, BlockId.Stone);
                        }
                    }
                }
            });
            Run(sim, 200, VoxelCommand.Move(dx, dz));
            Assert.Equal(GroundFeet + Units, sim.Player.FeetY);
        }
    }

    [Fact]
    public void ATwoBlockWallIsNotStepped()
    {
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                g.SetBlock(36, GroundTop + 1, z, BlockId.Stone);
                g.SetBlock(36, GroundTop + 2, z, BlockId.Stone);
            }
        });
        Run(sim, 300, VoxelCommand.Move(1, 0));
        Assert.Equal(36 * Units, sim.Player.MaxX);
        Assert.Equal(GroundFeet, sim.Player.FeetY);
        Run(sim, 300, VoxelCommand.Move(1, 0), VoxelCommand.Jump);
        Run(sim, 120); // let a hop in progress come down
        Assert.Equal(36 * Units, sim.Player.MaxX); // a jump rises about 1.3 blocks, not 2
        Assert.Equal(GroundFeet, sim.Player.FeetY);
    }

    [Fact]
    public void AStepIsNotTakenWithoutHeadRoom()
    {
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                g.SetBlock(36, GroundTop + 1, z, BlockId.Stone);
                g.SetBlock(35, GroundTop + 3, z, BlockId.Stone); // a low ceiling over the step-up
                g.SetBlock(34, GroundTop + 3, z, BlockId.Stone);
                g.SetBlock(33, GroundTop + 3, z, BlockId.Stone);
            }
        });
        Run(sim, 300, VoxelCommand.Move(1, 0));
        Assert.Equal(36 * Units, sim.Player.MaxX);
        Assert.Equal(GroundFeet, sim.Player.FeetY);
    }

    [Fact]
    public void AJumpClearsOneBlockAndFallsBack()
    {
        var sim = FlatSim();
        var peak = 0;
        var jumped = false;
        for (var tick = 0; tick < 120; tick++)
        {
            sim.Step(tick < 40 ? [VoxelCommand.Jump] : []);
            jumped |= sim.Events.Any(e => e.Kind == SimulationEventKind.PlayerJumped);
            peak = Math.Max(peak, sim.Player.FeetY - GroundFeet);
        }

        Assert.True(jumped);
        Assert.InRange(peak, (int)(1.15 * Units), (int)(1.6 * Units));
        Assert.Equal(GroundFeet, sim.Player.FeetY);
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void AJumpWithMotionCarriesOntoALedgeOfOneBlockButTwoIsTooHigh()
    {
        foreach (var (height, expectedClimb) in new[] { (1, Units), (2, 0) })
        {
            var sim = FlatSim(g =>
            {
                for (var z = 0; z < 64; z++)
                {
                    for (var x = 38; x < 64; x++)
                    {
                        for (var h = 1; h <= height; h++)
                        {
                            g.SetBlock(x, GroundTop + h, z, BlockId.Stone);
                        }
                    }
                }
            });
            Run(sim, 300, VoxelCommand.Move(1, 0), VoxelCommand.Jump);
            Run(sim, 120); // let a hop in progress come down
            Assert.Equal(GroundFeet + expectedClimb, sim.Player.FeetY);
            Assert.True(sim.Player.Grounded);
        }
    }

    [Fact]
    public void WalkingOffAnEdgeFallsToTheLowerGround()
    {
        var sim = FlatSim(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                for (var x = 40; x < 64; x++)
                {
                    for (var y = GroundTop - 3; y <= GroundTop; y++)
                    {
                        g.SetBlock(x, y, z, BlockId.Air);
                    }
                }
            }
        });
        var minVelocity = 0;
        var landed = false;
        for (var tick = 0; tick < 300; tick++)
        {
            sim.Step([VoxelCommand.Move(1, 0)]);
            minVelocity = Math.Min(minVelocity, sim.Player.VelocityY);
            landed |= sim.Events.Any(e => e.Kind == SimulationEventKind.PlayerLanded);
        }

        Assert.True(minVelocity < -100, "it fell");
        Assert.True(landed);
        Assert.Equal(GroundFeet - (4 * Units), sim.Player.FeetY);
        Assert.True(sim.Player.CenterX > 40 * Units);
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void ADiagonalWalkIsAsFastAsAStraightOne()
    {
        var straight = FlatSim();
        Run(straight, 60, VoxelCommand.Move(1, 0));
        var diagonal = FlatSim();
        Run(diagonal, 60, VoxelCommand.Move(1, 1));
        var straightDistance = straight.Player.CenterX - ((32 * Units) + (Units / 2));
        var dx = diagonal.Player.CenterX - ((32 * Units) + (Units / 2));
        var dz = diagonal.Player.CenterZ - ((32 * Units) + (Units / 2));
        Assert.Equal(dx, dz);
        var length = Math.Sqrt(((double)dx * dx) + ((double)dz * dz));
        Assert.InRange(length / straightDistance, 0.995, 1.005);
        Assert.InRange(straightDistance / (double)Units, 4.2, 4.4); // about 4.3 blocks in the second
    }

    [Fact]
    public void MoveRelativeWalksAlongTheFacing()
    {
        var cases = new[] { (0, 0, -1), (1024, 1, 0), (2048, 0, 1), (3072, -1, 0) };
        foreach (var (yaw, ex, ez) in cases)
        {
            var sim = FlatSim();
            var x0 = sim.Player.CenterX;
            var z0 = sim.Player.CenterZ;
            Run(sim, 30, VoxelCommand.Look(yaw, 0), VoxelCommand.MoveRelative(0, 1));
            Assert.Equal(ex, Math.Sign(sim.Player.CenterX - x0));
            Assert.Equal(ez, Math.Sign(sim.Player.CenterZ - z0));
            if (ex == 0)
            {
                Assert.Equal(x0, sim.Player.CenterX);
            }
        }

        // Strafing right while facing north is east.
        var strafe = FlatSim();
        var sx0 = strafe.Player.CenterX;
        Run(strafe, 30, VoxelCommand.MoveRelative(1, 0));
        Assert.True(strafe.Player.CenterX > sx0);
        Assert.Equal((32 * Units) + (Units / 2), strafe.Player.CenterZ);
    }

    [Fact]
    public void LookSetsTheFacingAndClampsThePitch()
    {
        var sim = FlatSim();
        sim.Step([VoxelCommand.Look(5000, 3000)]);
        Assert.Equal(5000 - Angles.StepsPerTurn, sim.Player.Yaw);
        Assert.Equal(Angles.MaxPitch, sim.Player.Pitch);
        sim.Step([VoxelCommand.Look(-1, -3000)]);
        Assert.Equal(Angles.StepsPerTurn - 1, sim.Player.Yaw);
        Assert.Equal(-Angles.MaxPitch, sim.Player.Pitch);
    }

    [Fact]
    public void TheBodyNeverEndsATickInsideASolidOverManyRandomCommandSequences()
    {
        for (ulong seed = 1; seed <= 24; seed++)
        {
            var rng = WorldRng.For(seed, "voxel-physics-property");
            var sim = FlatSim(g =>
            {
                // Scatter pillars, steps, ceilings and walls of random height around the spawn.
                for (var i = 0; i < 260; i++)
                {
                    var x = rng.NextInt(10, 54);
                    var z = rng.NextInt(10, 54);
                    var height = rng.NextInt(1, 4);
                    if (Math.Abs(x - 32) < 2 && Math.Abs(z - 32) < 2)
                    {
                        continue;
                    }

                    for (var h = 1; h <= height; h++)
                    {
                        g.SetBlock(x, GroundTop + h, z, BlockId.Stone);
                    }
                }

                for (var i = 0; i < 40; i++)
                {
                    g.SetBlock(rng.NextInt(10, 54), GroundTop + rng.NextInt(2, 5), rng.NextInt(10, 54), BlockId.Roof);
                }
            });

            var commandRng = WorldRng.For(seed, "voxel-physics-commands");
            var held = new List<VoxelCommand>();
            for (var tick = 0; tick < 2500; tick++)
            {
                if (tick % 20 == 0)
                {
                    held.Clear();
                    held.Add(VoxelCommand.Move(commandRng.NextInt(-1, 2), commandRng.NextInt(-1, 2)));
                    if (commandRng.NextChance(1, 2))
                    {
                        held.Add(VoxelCommand.MoveRelative(commandRng.NextInt(-1, 2), commandRng.NextInt(-1, 2)));
                    }

                    if (commandRng.NextChance(1, 3))
                    {
                        held.Add(VoxelCommand.Jump);
                    }

                    held.Add(VoxelCommand.Look(commandRng.NextInt(0, 4096), commandRng.NextInt(-1023, 1024)));
                }

                sim.Step(held);
                var p = sim.Player;
                Assert.True(
                    VoxelCollider.IsFree(sim.Grid, p.MinX, p.MinY, p.MinZ),
                    $"seed {seed} tick {tick}: the body is inside a solid block at ({p.MinX}, {p.MinY}, {p.MinZ})");
                Assert.True(p.MinX >= 0 && p.MaxX <= 64 * Units && p.MinZ >= 0 && p.MaxZ <= 64 * Units && p.MinY >= 0 && p.MaxY <= 32 * Units);
            }
        }
    }

    [Fact]
    public void TheBodyNeverEndsATickInsideASolidOnTheRealRouteEither()
    {
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var sim = VoxelSimulation.Create(BuildRoute(seed));
            var rng = WorldRng.For(seed, "route-walk");
            var held = new List<VoxelCommand>();
            for (var tick = 0; tick < 3000; tick++)
            {
                if (tick % 15 == 0)
                {
                    held.Clear();
                    held.Add(VoxelCommand.MoveRelative(rng.NextInt(-1, 2), rng.NextInt(0, 2)));
                    held.Add(VoxelCommand.Look(rng.NextInt(0, 4096), 0));
                    if (rng.NextChance(1, 4))
                    {
                        held.Add(VoxelCommand.Jump);
                    }
                }

                sim.Step(held);
                var p = sim.Player;
                Assert.True(VoxelCollider.IsFree(sim.Grid, p.MinX, p.MinY, p.MinZ), $"seed {seed} tick {tick}");
            }
        }
    }

    [Fact]
    public void ARunIsReproducibleAndAnyDifferenceShowsInTheStateDigest()
    {
        VoxelCommand[][] script =
        [
            [VoxelCommand.Move(1, 0)],
            [VoxelCommand.Move(1, 1), VoxelCommand.Jump],
            [VoxelCommand.Look(900, -200), VoxelCommand.MoveRelative(0, 1)],
            [],
        ];

        string Play(int extraTick)
        {
            var sim = VoxelSimulation.Create(BuildRoute(42));
            for (var round = 0; round < 40; round++)
            {
                foreach (var commands in script)
                {
                    Run(sim, 7, commands);
                }
            }

            for (var i = 0; i < extraTick; i++)
            {
                sim.Step();
            }

            return sim.ComputeStateDigest();
        }

        Assert.Equal(Play(0), Play(0));
        Assert.NotEqual(Play(0), Play(1));
    }
}
