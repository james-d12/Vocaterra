using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>The drowning rule against a fake health.</summary>
public sealed class DrowningDamageTests
{
    private sealed class FakeHealth : IPlayerHealth
    {
        public int Value { get; private set; } = 100;

        public List<(int Amount, DamageCause Cause)> Hurts { get; } = [];

        public bool IsDead => Value <= 0;

        public void Damage(int amount, DamageCause cause)
        {
            Hurts.Add((amount, cause));
            Value -= amount;
        }
    }

    private static void Run(Breath breath, DrowningDamage rule, int ticks, bool under)
    {
        for (var i = 0; i < ticks; i++)
        {
            rule.Apply(breath.Step(under));
        }
    }

    [Fact]
    public void Given_AirLeft_When_UnderWater_Then_NoDamage()
    {
        var health = new FakeHealth();

        Run(new Breath(), new DrowningDamage(health), 899, true);

        Assert.Empty(health.Hurts);
    }

    [Fact]
    public void Given_NoAir_When_UnderWaterForThreeSeconds_Then_TenDamageASecondFromDrowning()
    {
        var health = new FakeHealth();
        var breath = new Breath();
        breath.Restore(0);

        Run(breath, new DrowningDamage(health), 180, true);

        Assert.Equal([(10, DamageCause.Drowning), (10, DamageCause.Drowning), (10, DamageCause.Drowning)], health.Hurts);
    }

    [Fact]
    public void Given_NoAir_When_TheBodySurfaces_Then_TheDamageStops()
    {
        var health = new FakeHealth();
        var breath = new Breath();
        var rule = new DrowningDamage(health);
        Run(breath, rule, 900 + 61, true);
        var before = health.Hurts.Count;

        Run(breath, rule, 600, false);

        Assert.Equal(2, before);
        Assert.Equal(before, health.Hurts.Count);
    }

    [Fact]
    public void Given_ADeadPlayer_When_OutOfAir_Then_NoMoreDamage()
    {
        var health = new FakeHealth();
        health.Damage(100, DamageCause.Drowning);
        var rule = new DrowningDamage(health);

        rule.Apply(BreathEvents.OutOfAir);

        Assert.Single(health.Hurts);
    }

    [Fact]
    public void Given_FullHealth_When_UnderWaterWithoutAir_Then_ThePlayerDiesAfterAboutTenSeconds()
    {
        var health = new FakeHealth();
        var breath = new Breath();
        var rule = new DrowningDamage(health);
        var ticks = 0;

        while (!health.IsDead && ticks < 5000)
        {
            Run(breath, rule, 1, true);
            ticks++;
        }

        Assert.Equal(900 + (9 * 60), ticks); // 15 s of air, then ten hurts: the first as the air goes, the tenth nine seconds later
        Assert.Equal(10, health.Hurts.Count);
    }

    [Fact]
    public void Given_NoPlayerHealth_When_Hurt_Then_ItIsNeverDead()
    {
        var health = new NoPlayerHealth();

        health.Damage(1000, DamageCause.Drowning);

        Assert.False(health.IsDead);
    }
}
