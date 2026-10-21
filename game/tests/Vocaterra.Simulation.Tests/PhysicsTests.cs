using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Tests;

/// <summary>Gravity, walking, jumping and tile collision on hand-built and generated worlds.</summary>
public sealed class PhysicsTests
{
    private const int U = TestWorlds.Units;
    private const int FeetOnGround = TestWorlds.GroundRow * U;

    [Fact]
    public void ThePlayerStartsStandingOnTheSpawnTile()
    {
        var sim = GameSimulation.Create(42);
        var player = sim.Player;
        Assert.True(player.Grounded);
        Assert.Equal((sim.World.Spawn.Y + 1) * U, player.Bottom);
        Assert.Equal(sim.World.Spawn.X, player.FeetTile.X);
        sim.Step();
        Assert.Equal(player, sim.Player);
        Assert.Equal(1, sim.Tick);
    }

    [Fact]
    public void ThePlayerDroppedAtTheSpawnLandsAndStops()
    {
        var sim = GameSimulation.Create(42);
        var spawnPlayer = sim.Player;
        sim.PlacePlayer(spawnPlayer.X, spawnPlayer.Y - (10 * U));
        Assert.False(sim.Player.Grounded);

        var landedAt = -1L;
        for (var i = 0; i < 120; i++)
        {
            sim.Step();
            if (sim.Events.Any(e => e.Kind == SimulationEventKind.PlayerLanded))
            {
                landedAt = sim.Tick;
            }
        }

        Assert.True(landedAt > 0, "The player never landed.");
        Assert.InRange(landedAt, 20, 60);
        Assert.Equal(spawnPlayer, sim.Player);
        Assert.Equal(0, sim.Player.VelocityY);
    }

    [Fact]
    public void FallingNeverExceedsTheTerminalSpeed()
    {
        var sim = TestWorlds.StandingAt(50);
        var player = sim.Player;
        sim.PlacePlayer(player.X, 10 * U);
        var fastest = 0;
        for (var i = 0; i < 200; i++)
        {
            sim.Step();
            fastest = Math.Max(fastest, sim.Player.VelocityY);
        }

        Assert.Equal(PhysicsConstants.MaxFallSpeed, fastest);
        Assert.Equal(FeetOnGround, sim.Player.Bottom);
    }

    [Fact]
    public void WalkingIntoAWallStops()
    {
        // A wall three tiles high at column 60.
        var sim = TestWorlds.StandingAt(50, grid =>
        {
            for (var h = 1; h <= 3; h++)
            {
                grid.SetTile(60, TestWorlds.GroundRow - h, TileId.Stone);
            }
        });

        TestWorlds.Run(sim, 300, PlayerCommand.MoveRight);
        var player = sim.Player;
        Assert.Equal(60 * U, player.Right);
        Assert.Equal(0, player.VelocityX);
        Assert.True(player.Grounded);

        // Pushing on does not move it and does not sink it.
        TestWorlds.Run(sim, 60, PlayerCommand.MoveRight);
        Assert.Equal(player, sim.Player);
    }

    [Fact]
    public void WalkingLeftIntoAWallStopsAtItsRightFace()
    {
        var sim = TestWorlds.StandingAt(50, grid =>
        {
            for (var h = 1; h <= 3; h++)
            {
                grid.SetTile(40, TestWorlds.GroundRow - h, TileId.Stone);
            }
        });

        TestWorlds.Run(sim, 300, PlayerCommand.MoveLeft);
        Assert.Equal(41 * U, sim.Player.X);
        Assert.Equal(0, sim.Player.VelocityX);
        Assert.Equal(-1, sim.Player.Facing);
    }

    [Fact]
    public void AOneTileLedgeIsWalkedOverButATwoTileWallIsNot()
    {
        var ledge = TestWorlds.StandingAt(50, grid =>
        {
            for (var x = 60; x < 100; x++)
            {
                grid.SetTile(x, TestWorlds.GroundRow - 1, TileId.Dirt);
            }
        });
        TestWorlds.Run(ledge, 120, PlayerCommand.MoveRight);
        Assert.True(ledge.Player.X > 61 * U, "The player should be past the ledge.");
        Assert.Equal(FeetOnGround - U, ledge.Player.Bottom);
        Assert.True(ledge.Player.Grounded);

        var wall = TestWorlds.StandingAt(50, grid =>
        {
            grid.SetTile(60, TestWorlds.GroundRow - 1, TileId.Dirt);
            grid.SetTile(60, TestWorlds.GroundRow - 2, TileId.Dirt);
        });
        TestWorlds.Run(wall, 120, PlayerCommand.MoveRight);
        Assert.Equal(60 * U, wall.Player.Right);
    }

    [Fact]
    public void AJumpReturnsToTheGround()
    {
        var sim = TestWorlds.StandingAt(50);
        var start = sim.Player;
        sim.Step([PlayerCommand.Jump]);
        Assert.Contains(sim.Events, e => e.Kind == SimulationEventKind.PlayerJumped);
        Assert.False(sim.Player.Grounded);

        var highest = sim.Player.Y;
        var landed = false;
        for (var i = 0; i < 200 && !landed; i++)
        {
            sim.Step();
            highest = Math.Min(highest, sim.Player.Y);
            landed = sim.Events.Any(e => e.Kind == SimulationEventKind.PlayerLanded);
        }

        Assert.True(landed);
        Assert.Equal(start.Y, sim.Player.Y);
        Assert.True(sim.Player.Grounded);
        Assert.InRange(start.Y - highest, U / 2, 3 * U);
    }

    [Fact]
    public void HoldingJumpRisesHigherThanTappingAndNeedsANewPressToRepeat()
    {
        var tap = TestWorlds.StandingAt(50);
        var hold = TestWorlds.StandingAt(50);
        var ground = tap.Player.Y;
        tap.Step([PlayerCommand.Jump]);
        var tapTop = tap.Player.Y;
        var holdTop = ground;
        for (var i = 0; i < 100; i++)
        {
            hold.Step([PlayerCommand.Jump]);
            holdTop = Math.Min(holdTop, hold.Player.Y);
            tap.Step();
            tapTop = Math.Min(tapTop, tap.Player.Y);
        }

        Assert.True(ground - holdTop > 4 * U, "A held jump should clear four tiles.");
        Assert.True(ground - holdTop < 6 * U, "A held jump should stay below six tiles.");
        Assert.True(ground - tapTop < ground - holdTop);

        // Holding the button through the landing does not hop again: the player ends standing.
        Assert.True(hold.Player.Grounded);
        Assert.Equal(ground, hold.Player.Y);
    }

    [Fact]
    public void CoyoteTimeAllowsAJumpJustAfterWalkingOffAnEdge()
    {
        // Ground only to the left of column 60: walking right off the edge.
        var sim = TestWorlds.StandingAt(55, grid =>
        {
            for (var y = TestWorlds.GroundRow; y < TestWorlds.Height; y++)
            {
                for (var x = 60; x < TestWorlds.Width; x++)
                {
                    grid.SetTile(x, y, TileId.Air);
                }
            }
        });

        var jumped = false;
        for (var i = 0; i < 120 && !jumped; i++)
        {
            sim.Step([PlayerCommand.MoveRight]);
            if (!sim.Player.Grounded && sim.Player.VelocityY > 0)
            {
                // Airborne for the first time: press jump now, within the coyote window.
                sim.Step([PlayerCommand.MoveRight, PlayerCommand.Jump]);
                jumped = sim.Events.Any(e => e.Kind == SimulationEventKind.PlayerJumped);
                break;
            }
        }

        Assert.True(jumped, "A jump right after leaving the ground should work.");
    }

    [Fact]
    public void ABufferedJumpPressedJustBeforeLandingFiresOnLanding()
    {
        var sim = TestWorlds.StandingAt(50);
        var ground = sim.Player.Y;
        sim.PlacePlayer(sim.Player.X, ground - (U / 8));
        Assert.False(sim.Player.Grounded);
        sim.Step([PlayerCommand.Jump]);
        var jumped = sim.Events.Any(e => e.Kind == SimulationEventKind.PlayerJumped);
        for (var i = 0; i < 5 && !jumped; i++)
        {
            sim.Step();
            jumped = sim.Events.Any(e => e.Kind == SimulationEventKind.PlayerJumped);
        }

        Assert.True(jumped);
    }

    [Fact]
    public void TheWorldEdgesAreWalls()
    {
        var sim = TestWorlds.StandingAt(2);
        TestWorlds.Run(sim, 200, PlayerCommand.MoveLeft);
        Assert.Equal(0, sim.Player.X);

        var right = TestWorlds.StandingAt(TestWorlds.Width - 3);
        TestWorlds.Run(right, 200, PlayerCommand.MoveRight);
        Assert.Equal(TestWorlds.Width * U, right.Player.Right);
    }

    [Fact]
    public void TheCeilingStopsAJump()
    {
        var sim = TestWorlds.StandingAt(50, grid => grid.SetTile(50, TestWorlds.GroundRow - 6, TileId.Stone));
        TestWorlds.Run(sim, 120, PlayerCommand.Jump);
        Assert.True(sim.Player.Y >= ((TestWorlds.GroundRow - 5) * U) - U, "The head hit the ceiling tile.");
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void LeftAndRightTogetherCancelAndCommandOrderDoesNotMatter()
    {
        var a = TestWorlds.StandingAt(50);
        a.Step([PlayerCommand.MoveLeft, PlayerCommand.MoveRight]);
        Assert.Equal(0, a.Player.VelocityX);

        var one = TestWorlds.StandingAt(50);
        var two = TestWorlds.StandingAt(50);
        for (var i = 0; i < 30; i++)
        {
            one.Step([PlayerCommand.MoveRight, PlayerCommand.Jump]);
            two.Step([PlayerCommand.Jump, PlayerCommand.MoveRight, PlayerCommand.MoveRight]);
        }

        Assert.Equal(one.ComputeStateDigest(), two.ComputeStateDigest());
        Assert.Equal(one.Player, two.Player);
    }

    [Fact]
    public void UnknownCommandKindsAreIgnored()
    {
        var plain = TestWorlds.StandingAt(50);
        var odd = TestWorlds.StandingAt(50);
        odd.Step([new PlayerCommand((PlayerCommandKind)99, 5, 6)]);
        plain.Step();
        Assert.Equal(plain.ComputeStateDigest(), odd.ComputeStateDigest());
    }

    [Fact]
    public void AScriptedRunOnGeneratedTerrainNeverEntersSolidTiles()
    {
        foreach (ulong seed in new ulong[] { 42, 7, 1000 })
        {
            var sim = GameSimulation.Create(seed, new WorldSize(512, 256));
            for (var i = 0; i < 3000; i++)
            {
                var phase = (i / 300) % 4;
                var commands = phase switch
                {
                    0 => new[] { PlayerCommand.MoveRight },
                    1 => new[] { PlayerCommand.MoveRight, PlayerCommand.Jump },
                    2 => new[] { PlayerCommand.MoveLeft },
                    _ => new[] { PlayerCommand.MoveLeft, PlayerCommand.Jump },
                };
                sim.Step(commands);

                var p = sim.Player;
                Assert.True(
                    TileCollider.IsFree(sim.World.Tiles, p.X, p.Y, PhysicsConstants.PlayerWidth, PhysicsConstants.PlayerHeight),
                    $"Seed {seed}: the player overlaps a solid tile at tick {sim.Tick}.");
                Assert.InRange(p.X, 0, (sim.World.Width * U) - PhysicsConstants.PlayerWidth);
            }
        }
    }

    [Fact]
    public void ReplayingTheSameCommandsGivesTheSameStateDigest()
    {
        static string Run()
        {
            var sim = GameSimulation.Create(42);
            for (var i = 0; i < 1200; i++)
            {
                var jump = i % 90 < 8;
                sim.Step(jump ? [PlayerCommand.MoveRight, PlayerCommand.Jump] : [PlayerCommand.MoveRight]);
            }

            return sim.ComputeStateDigest();
        }

        Assert.Equal(Run(), Run());
        Assert.NotEqual(GameSimulation.Create(42).ComputeStateDigest(), Run());
    }
}
