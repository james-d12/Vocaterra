using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Physics;

/// <summary>The fly cheat: a flying body hovers, rises on the jump command, sinks on the descend command, flies fast at the slider's speed and is still stopped by blocks.</summary>
public sealed class FlyingBodyTests
{
    private static int GroundFeet => (GroundTop + 1) * Units;

    [Fact]
    public void Given_AFlyingBodyInTheAir_When_NothingIsHeld_Then_ItHovers()
    {
        var sim = FlatSim();
        sim.Flying = true;
        sim.TeleportTo(new BlockPos(32, GroundTop + 10, 32));
        var feet = sim.Player.FeetY;

        Run(sim, 120);

        Assert.Equal(feet, sim.Player.FeetY);
        Assert.False(sim.Player.Grounded);
    }

    [Fact]
    public void Given_AFlyingBody_When_JumpIsHeld_Then_ItRisesAndNeverReportsAJump()
    {
        var sim = FlatSim();
        sim.Flying = true;

        for (var tick = 0; tick < 60; tick++)
        {
            sim.Step([VoxelCommand.Jump]);
            Assert.DoesNotContain(sim.Events, static e => e.Kind == SimulationEventKind.PlayerJumped);
        }

        Assert.True(sim.Player.FeetY > GroundFeet + (5 * Units), "a second of rising climbs more than five blocks");
        Run(sim, 60);
        Assert.True(sim.Player.FeetY > GroundFeet + (5 * Units), "letting go hovers");
    }

    [Fact]
    public void Given_AFlyingBodyInTheAir_When_DescendIsHeld_Then_ItSinksAndLandsOnTheGround()
    {
        var sim = FlatSim();
        sim.Flying = true;
        sim.TeleportTo(new BlockPos(32, GroundTop + 10, 32));
        var landed = false;

        for (var tick = 0; tick < 240 && !landed; tick++)
        {
            sim.Step([VoxelCommand.Descend]);
            landed = sim.Events.Any(static e => e.Kind == SimulationEventKind.PlayerLanded);
        }

        Assert.True(landed);
        Assert.Equal(GroundFeet, sim.Player.FeetY);
        Assert.True(sim.Player.Grounded);
        Run(sim, 30, VoxelCommand.Descend);
        Assert.Equal(GroundFeet, sim.Player.FeetY);
    }

    [Fact]
    public void Given_AWalkingBody_When_DescendIsSent_Then_ItIsIgnored()
    {
        var sim = FlatSim();

        Run(sim, 30, VoxelCommand.Descend);

        Assert.Equal(GroundFeet, sim.Player.FeetY);
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void Given_TheFlySpeed_When_TheBodyFliesForward_Then_ItIsFarFasterThanWalkingAndTheSliderScalesIt()
    {
        var walked = Forward(flying: false, blocksPerSecond: 0);
        var flown = Forward(flying: true, blocksPerSecond: VoxelPhysics.DefaultFlyBlocksPerSecond);
        var slow = Forward(flying: true, blocksPerSecond: 10);

        Assert.True(flown > walked * 5, $"flying {flown} against walking {walked}");
        Assert.True(slow < flown);
        Assert.InRange(flown / (double)Units, 14, 16); // half a second at 30 blocks per second
        var top = FlatSim();
        top.Flying = true;
        top.FlySpeed = VoxelPhysics.FlySpeedOf(500);
        top.TeleportTo(new BlockPos(32, GroundTop + 5, 60));
        var before = top.Player.MinZ;
        top.Step([VoxelCommand.MoveRelative(0, 1)]);
        Assert.Equal(VoxelPhysics.FlySpeedOf(500), before - top.Player.MinZ); // the top speed: over eight blocks in one tick, with no block skipped by the sweep
    }

    [Fact]
    public void Given_AFlyingBody_When_AWallIsAhead_Then_ItStopsAtTheWall()
    {
        var sim = FlatSim(static grid =>
        {
            for (var x = 0; x < 64; x++)
            {
                for (var y = GroundTop + 1; y < 32; y++)
                {
                    grid.SetBlock(x, y, 28, BlockId.Stone); // a wall from the ground to the sky, north of the spawn
                }
            }
        });
        sim.Flying = true;
        sim.TeleportTo(new BlockPos(32, GroundTop + 5, 32));

        Run(sim, 120, VoxelCommand.MoveRelative(0, 1));

        Assert.Equal(29 * Units, sim.Player.MinZ);
    }

    [Fact]
    public void Given_TheFlySettings_When_Used_Then_TheyAreOffAndFastByDefaultSavedWithTheSettingsAndShownInTheMenu()
    {
        var settings = new PlayerSettings();
        Assert.False(settings.Fly);
        Assert.Equal(VoxelPhysics.DefaultFlyBlocksPerSecond, settings.FlySpeed);
        var off = settings.ToSave();
        Assert.False(off.FlyCheat);
        Assert.Null(off.FlySpeed);

        settings.Set(SettingId.Fly, 1);
        settings.Set(SettingId.FlySpeed, 62);

        Assert.True(settings.Fly);
        Assert.Equal(60, settings.FlySpeed);
        var saved = settings.ToSave();
        Assert.True(saved.FlyCheat);
        Assert.Equal(60, saved.FlySpeed);
        var resolved = PlayerSettings.Resolve(saved);
        Assert.True(resolved.Fly);
        Assert.Equal(60, resolved.FlySpeed);
        var rows = MenuRows.Settings(settings, null);
        Assert.Contains(rows, static row => row.Id == nameof(SettingId.Fly));
        Assert.Contains(rows, static row => row.Id == nameof(SettingId.FlySpeed));
        Assert.Equal("Fly (cheat)", TestGame.Locale.Ui(PlayerSettings.Spec(SettingId.Fly).LabelKey));
        settings.Set(SettingId.FlySpeed, 9999);
        Assert.Equal(500, settings.FlySpeed); // the slider's top
    }

    [Fact]
    public void Given_TheSettingsOfASave_When_TheFlyCheatIsOff_Then_NoFlyFieldIsWritten()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new PlayerSettings().ToSave());
        Assert.DoesNotContain("fly", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Given_TheLiveInput_When_TheDescendKeyIsDown_Then_ATickSendsDescend()
    {
        var live = new LiveVoxelInput();
        live.SetFrame(0, 0, jump: false, gatherBlock: null, placement: null, selectSlot: null, descend: true);
        var commands = new List<VoxelCommand>();

        live.Fill(commands);

        Assert.Contains(commands, static c => c.Kind == VoxelCommandKind.Descend);
    }

    // How far the body goes north in half a second with W held, in units.
    private static int Forward(bool flying, int blocksPerSecond)
    {
        var sim = FlatSim();
        sim.TeleportTo(new BlockPos(32, GroundTop + 5, 60));
        if (flying)
        {
            sim.Flying = true;
            sim.FlySpeed = VoxelPhysics.FlySpeedOf(blocksPerSecond);
        }
        else
        {
            sim.TeleportTo(new BlockPos(32, GroundTop + 1, 60));
        }

        var start = sim.Player.MinZ;
        Run(sim, 30, VoxelCommand.MoveRelative(0, 1));
        return start - sim.Player.MinZ;
    }
}
