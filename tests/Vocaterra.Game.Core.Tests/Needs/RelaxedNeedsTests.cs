using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>The relaxed-needs setting (N05, adaptation design decision 17): half the rates, a faint instead of a death, off by default, a setting and not the world's state.</summary>
public sealed class RelaxedNeedsTests
{
    private const int Tps = NeedsData.TicksPerSecond;

    private static VoxelSession Playing(bool relaxed)
    {
        var session = TestGame.NewSession();
        session.RelaxedNeeds = relaxed;
        for (var slot = 0; slot < session.Simulation.Inventory.SlotCount; slot++)
        {
            session.Simulation.Inventory.SetSlot(slot, ItemStack.Empty);
        }

        return session;
    }

    private static void Tick(VoxelSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
        }
    }

    [Fact]
    public void TheSettingIsOffByDefault()
    {
        Assert.False(TestGame.NewSession().RelaxedNeeds);
        Assert.Equal(1.0, new PlayerNeeds(TestGame.Data.Needs).RateFactor);
    }

    [Theory]
    [InlineData(NeedMeter.Hunger)]
    [InlineData(NeedMeter.Thirst)]
    [InlineData(NeedMeter.Sleep)]
    public void ADriveFallsAtHalfItsRateAMinuteOfPlayDropsHalfAsMuch(NeedMeter meter)
    {
        var normal = Playing(relaxed: false);
        var relaxed = Playing(relaxed: true);

        Tick(normal, 60 * Tps);
        Tick(relaxed, 60 * Tps);

        var dropNormal = 100 - normal.Needs.Value(meter);
        var dropRelaxed = 100 - relaxed.Needs.Value(meter);
        Assert.True(dropNormal > 0.5);
        Assert.Equal(dropNormal / 2, dropRelaxed, 6);
    }

    [Fact]
    public void ZeroHealthFaintsAndWakesAtTheCampWithTheBagKeptAndNoChest()
    {
        var session = Playing(relaxed: true);
        session.Simulation.Inventory.Add(ItemId.Wood, 12);
        session.Simulation.Inventory.Add(ItemId.Book, 1);
        session.Simulation.Inventory.Add(ItemId.Berry, 3);
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);
        var chestsBefore = session.Scene.Grid.BlocksOfKind(BlockId.Chest).Count();
        var feetBefore = session.Simulation.Player.FeetBlock;

        Tick(session, 2 * Tps);

        Assert.Equal(1, session.DeathCount);
        Assert.Null(session.Chest);
        Assert.Equal(12, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Book));
        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Berry));
        Assert.Equal(chestsBefore, session.Scene.Grid.BlocksOfKind(BlockId.Chest).Count()); // no chest block added
        Assert.Equal(50, session.Needs.Value(NeedMeter.Health), 0); // a faint tick of regrowth passes
        Assert.Equal(50, session.Needs.Value(NeedMeter.Thirst), 0);
        Assert.Equal("faint", WorldEventsWoke(session));
        Assert.NotEqual(feetBefore, session.Simulation.Player.FeetBlock);
        Assert.Contains("woke where you sleep. Your bag is with you.", session.DeathLine, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameFallWithoutTheSettingIsADeathWithAChestAndTheBagShortened()
    {
        var session = Playing(relaxed: false);
        session.Simulation.Inventory.Add(ItemId.Wood, 12);
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);

        Tick(session, 2 * Tps);

        Assert.Equal(1, session.DeathCount);
        Assert.NotNull(session.Chest);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal("death", WorldEventsWoke(session));
    }

    [Fact]
    public void ASaveOfTheDefaultSettingsIsWhatItWasAndTheSettingRoundTrips()
    {
        var session = Playing(relaxed: false);
        var plain = SaveGame.Save(session, new SaveSettings(new SoundVolumes(), 100));
        Assert.DoesNotContain("relaxedNeeds", plain, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("viewEffectsOff", plain, StringComparison.OrdinalIgnoreCase);

        var text = SaveGame.Save(session, new SaveSettings(new SoundVolumes(), 125, ViewEffectsOff: true, RelaxedNeeds: true));
        var settings = SaveGame.Parse(text).Settings!.Clamped();
        Assert.True(settings.RelaxedNeeds);
        Assert.True(settings.ViewEffectsOff);
        Assert.Equal(125, settings.TextScale);

        // The setting is the player's, not the world's: the save's world is the same either way, so it loads under either.
        var loaded = SaveGame.Parse(plain);
        Assert.False(loaded.Settings!.RelaxedNeeds);
        Assert.False(loaded.Settings.ViewEffectsOff);
        Assert.Equal(SaveGame.Parse(plain).StateDigest, SaveGame.Parse(SaveGame.Save(Playing(relaxed: true), new SaveSettings(new SoundVolumes(), 100))).StateDigest);
    }

    private static string? WorldEventsWoke(VoxelSession session) =>
        session.Events.OfType<WorldEvent>().Last(static e => e.Name == WorldEvents.PlayerWoke).Detail;
}
