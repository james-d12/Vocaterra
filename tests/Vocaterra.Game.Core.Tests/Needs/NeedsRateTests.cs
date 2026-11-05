using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>The drive rate setting (N09): a multiplier on hunger, thirst and sleep only, kept in the save's settings, never in a meter, digest or replay; and its panel.</summary>
public sealed class NeedsRateTests
{
    private const int Tps = NeedsData.TicksPerSecond;

    private static VoxelSession Playing(double rate, bool relaxed = false)
    {
        var session = TestGame.NewSession();
        session.NeedsRate = rate;
        session.RelaxedNeeds = relaxed;
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
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

    [Theory]
    [InlineData(NeedMeter.Hunger)]
    [InlineData(NeedMeter.Thirst)]
    [InlineData(NeedMeter.Sleep)]
    public void ADriveFallsInProportionToTheRate(NeedMeter meter)
    {
        var one = Playing(1);
        var two = Playing(2);
        var half = Playing(0.5);
        Tick(one, 60 * Tps);
        Tick(two, 60 * Tps);
        Tick(half, 60 * Tps);

        var drop = 100 - one.Needs.Value(meter);
        Assert.True(drop > 0.5);
        Assert.Equal(drop * 2, 100 - two.Needs.Value(meter), 6);
        Assert.Equal(drop / 2, 100 - half.Needs.Value(meter), 6);
    }

    [Fact]
    public void StaminaAndTheFoodAreNotTouchedByTheRate()
    {
        var one = Playing(1);
        var four = Playing(4);
        Tick(one, 10 * Tps);
        Tick(four, 10 * Tps);
        Assert.Equal(one.Needs.Value(NeedMeter.Stamina), four.Needs.Value(NeedMeter.Stamina));
        Assert.Equal(one.Needs.Value(NeedMeter.Health), four.Needs.Value(NeedMeter.Health));
    }

    [Fact]
    public void TheRateMultipliesWithRelaxedNeeds()
    {
        var plain = Playing(1);
        var both = Playing(2, relaxed: true);
        Tick(plain, 60 * Tps);
        Tick(both, 60 * Tps);
        Assert.Equal(1.0, both.Needs.RateFactor);
        Assert.Equal(100 - plain.Needs.Value(NeedMeter.Hunger), 100 - both.Needs.Value(NeedMeter.Hunger), 6);
        Assert.True(both.RelaxedNeeds);
        both.RelaxedNeeds = false;
        Assert.Equal(2.0, both.Needs.RateFactor);
        Assert.Equal(2.0, both.NeedsRate);
    }

    [Fact]
    public void TheSameInputsAndRateGiveTheSameMetersAndASaveKeepsThemExact()
    {
        var a = Playing(1.75);
        var b = Playing(1.75);
        Tick(a, 30 * Tps);
        Tick(b, 30 * Tps);
        Assert.Equal(a.Needs.Value(NeedMeter.Thirst), b.Needs.Value(NeedMeter.Thirst));
        var saved = SaveGame.Serialize(SaveGame.Capture(a, TestGame.Route(TestGame.Seed), new SaveSettings(new SoundVolumes(), 100, NeedsRate: 1.75)));
        var restored = SaveGame.Parse(saved);
        Assert.Equal(1.75, restored.Settings!.NeedsRate);
        Assert.Equal(SaveGame.Parse(SaveGame.Serialize(SaveGame.Capture(b, TestGame.Route(TestGame.Seed), null))).StateDigest, restored.StateDigest);
    }

    [Fact]
    public void TheDefaultWritesNoFieldAndAHandEditedValueIsClampedAndSnapped()
    {
        var session = TestGame.NewSession();
        string Text(double rate) => SaveGame.Serialize(SaveGame.Capture(session, TestGame.Route(TestGame.Seed), new SaveSettings(new SoundVolumes(), 100, NeedsRate: rate)));

        Assert.DoesNotContain("needsRate", Text(0), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("needsRate", Text(1), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("needsRate", Text(1.75), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4.0, SaveGame.Parse(Text(9)).Settings!.EffectiveNeedsRate);
        Assert.Equal(0.5, SaveGame.Parse(Text(0.1)).Settings!.EffectiveNeedsRate);
        Assert.Equal(1.25, SaveGame.Parse(Text(1.3)).Settings!.EffectiveNeedsRate);
        Assert.Equal(1.0, SaveGame.Parse(Text(double.NaN)).Settings!.EffectiveNeedsRate);
        Assert.Equal(1.0, new SaveSettings(new SoundVolumes()).EffectiveNeedsRate);
    }

    [Fact]
    public void ThePanelStepsTheSliderAtBothEndsAndFlipsTheToggles()
    {
        var rate = 3.75;
        bool effects = true, relaxed = false;
        var panel = new SettingsPanel(() => rate, r => rate = r, () => effects, e => effects = e, () => relaxed, r => relaxed = r);
        panel.Open();
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Right));
        Assert.Equal(4.0, rate);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.OemPlus));
        Assert.Equal(4.0, rate);
        for (var i = 0; i < 20; i++)
        {
            panel.HandleInput(TestInputFrame.Pressing(GameKey.OemMinus));
        }

        Assert.Equal(0.5, rate);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Left));
        Assert.Equal(0.5, rate);
        Assert.Equal("×0.5", SettingsPanel.RateText(rate));
        Assert.Equal("×1.25", SettingsPanel.RateText(1.25));

        panel.HandleInput(TestInputFrame.Pressing(GameKey.Down));
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Enter));
        Assert.False(effects);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Down));
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Space));
        Assert.True(relaxed);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Down));
        Assert.Equal(0, panel.Selected);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.F8));
        Assert.False(panel.IsOpen);
    }

    [Fact]
    public void TheSettingsKeyIsF8AndIsBoundWhenACommandIsGiven()
    {
        Assert.Equal(GameKey.F8, InputKeys.Of("settings"));
    }
}
