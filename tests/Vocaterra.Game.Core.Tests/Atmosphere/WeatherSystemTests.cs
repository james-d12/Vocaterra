using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Core.Tests.Atmosphere;

/// <summary>The weather's spells, draws, morning roll, forced kinds and state (E04).</summary>
public sealed class WeatherSystemTests
{
    private const int Second = DayClock.TicksPerSecond;

    private static WeatherData Shipped => WeatherTestData.Shipped;

    /// <summary>The (tick, kind) of every change over a number of ticks.</summary>
    private static List<(long Tick, string Kind)> Run(WeatherSystem weather, long ticks)
    {
        var found = new List<(long, string)>();
        for (long tick = 1; tick <= ticks; tick++)
        {
            if (weather.Advance(tick))
            {
                found.Add((tick, weather.State.Current));
            }
        }

        return found;
    }

    [Fact]
    public void ANewWeatherIsInTheStartKindWithItsFirstSpellFromTheRange()
    {
        var weather = new WeatherSystem(Shipped, 42);

        Assert.Equal("sunny", weather.Current.Id);
        Assert.Equal("sunny", weather.Previous.Id);
        Assert.True(weather.IsInitial);
        Assert.InRange(weather.State.SpellEnd, 240 * Second, 600 * Second);
        Assert.Equal(0, weather.State.SpellStart);
        Assert.Equal(1, weather.BlendAt(0));
    }

    [Fact]
    public void ASpellEndsAtItsTickAndNotBefore()
    {
        var weather = new WeatherSystem(Shipped, 42);
        var end = weather.State.SpellEnd;

        Assert.False(weather.Advance(end - 1));
        Assert.True(weather.IsInitial);
        Assert.True(weather.Advance(end));

        Assert.Equal("cloudy", weather.Current.Id); // sunny is followed by cloudy only
        Assert.Equal("sunny", weather.Previous.Id);
        Assert.Equal(end, weather.State.SpellStart);
        Assert.Equal(1, weather.State.Spells);
        Assert.False(weather.IsInitial);
    }

    [Fact]
    public void TheNextKindAndLengthComeOnlyFromTheSeedAndTheCounter()
    {
        var a = new WeatherSystem(Shipped, 7);
        var b = new WeatherSystem(Shipped, 7);
        var c = new WeatherSystem(Shipped, 8);

        var runA = Run(a, 200_000);
        var runB = Run(b, 200_000);
        var runC = Run(c, 200_000);

        Assert.Equal(runA, runB);
        Assert.Equal(a.State, b.State);
        Assert.NotEqual(runA, runC);
    }

    [Fact]
    public void AGameRestoredLongAfterASpellsEndChangesOnceAndNotOncePerTick()
    {
        var weather = new WeatherSystem(Shipped, 42);
        var end = weather.State.SpellEnd;

        var first = weather.Advance(end + 100_000);
        var second = weather.Advance(end + 100_001);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, weather.State.Spells);
        Assert.Equal(end + 100_000, weather.State.SpellStart);
    }

    [Fact]
    public void OverManySpellsAndSeedsEveryKindOccursEachSpellIsInRangeAndOnlyListedKindsFollow()
    {
        var seen = new HashSet<string>();
        var data = Shipped;
        for (ulong seed = 0; seed < 25; seed++)
        {
            var weather = new WeatherSystem(data, seed);
            var previous = weather.Current;
            var spellStart = 0L;
            var spellEnd = weather.State.SpellEnd;
            for (var spell = 0; spell < 40; spell++)
            {
                Assert.InRange(spellEnd - spellStart, (long)(previous.MinSeconds * Second), (long)(previous.MaxSeconds * Second));
                Assert.True(weather.Advance(spellEnd));
                var current = weather.Current;
                Assert.Contains(current.Id, previous.Next.Select(static n => n.Id));
                seen.Add(current.Id);
                spellStart = weather.State.SpellStart;
                spellEnd = weather.State.SpellEnd;
                previous = current;
            }
        }

        Assert.Equal(data.Kinds.Select(static k => k.Id).Order(), seen.Order());
    }

    [Fact]
    public void RainNeverFollowsSunnyDirectly()
    {
        for (ulong seed = 0; seed < 25; seed++)
        {
            var weather = new WeatherSystem(Shipped, seed);
            var before = weather.Current.Id;
            for (var i = 0; i < 60; i++)
            {
                weather.Advance(weather.State.SpellEnd);
                Assert.False(before == "sunny" && weather.Current.Id == "rain");
                before = weather.Current.Id;
            }
        }
    }

    [Fact]
    public void TheNextKindFollowsTheWeights()
    {
        var counts = new Dictionary<string, int> { ["sunny"] = 0, ["rain"] = 0 };
        for (ulong seed = 0; seed < 600; seed++)
        {
            var weather = new WeatherSystem(Shipped, seed);
            weather.Force("cloudy", 0);
            weather.Release(0);
            weather.Advance(weather.State.SpellEnd);
            counts[weather.Current.Id]++;
        }

        // cloudy goes to sunny 3 : rain 2, so about 60 percent of 600 are sunny.
        Assert.InRange(counts["sunny"], 300, 420);
        Assert.Equal(600, counts["sunny"] + counts["rain"]);
    }

    [Fact]
    public void TheBlendRunsAcrossTheTransitionFromTheSpellsStart()
    {
        var weather = new WeatherSystem(Shipped, 42);
        var end = weather.State.SpellEnd;
        weather.Advance(end);

        Assert.Equal(0, weather.BlendAt(end));
        Assert.Equal(0.5, weather.BlendAt(end + (long)(22.5 * Second)), 9);
        Assert.Equal(1, weather.BlendAt(end + (45 * Second)));
        Assert.Equal(1, weather.BlendAt(end + 99_999));
        Assert.Equal(weather.Previous.Channels, weather.ChannelsAt(end));
        Assert.Equal(weather.Current.Channels, weather.ChannelsAt(end + (45 * Second)));
    }

    [Fact]
    public void AMorningRollFollowsTheMorningWeightsAndCutsWithNoTransition()
    {
        var counts = Shipped.Kinds.ToDictionary(static k => k.Id, static _ => 0);
        for (ulong seed = 0; seed < 1000; seed++)
        {
            var weather = new WeatherSystem(Shipped, seed);
            weather.RollMorning(5000);
            counts[weather.Current.Id]++;
            Assert.Equal(weather.Current.Id, weather.Previous.Id);
            Assert.Equal(1, weather.BlendAt(5000));
            Assert.Equal(5000, weather.State.SpellStart);
            Assert.True(weather.State.SpellEnd > 5000);
            Assert.Equal(1, weather.State.Mornings);
        }

        // 6 : 3 : 1 of 1000.
        Assert.InRange(counts["sunny"], 520, 680);
        Assert.InRange(counts["cloudy"], 230, 370);
        Assert.InRange(counts["rain"], 50, 160);
    }

    [Fact]
    public void AMorningRollIsAFunctionOfTheSeedAndTheCounter()
    {
        var a = new WeatherSystem(Shipped, 9);
        var b = new WeatherSystem(Shipped, 9);
        var kinds = new List<string>();
        var again = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            a.RollMorning(i * 10_000L);
            b.RollMorning(i * 10_000L);
            kinds.Add(a.Current.Id);
            again.Add(b.Current.Id);
        }

        Assert.Equal(kinds, again);
        Assert.Equal(12, a.State.Mornings);
        Assert.Contains(kinds.Distinct().Count(), new[] { 2, 3 });
    }

    [Fact]
    public void AForcedKindIsHeldUntilReleasedAndSkipsItsSpellEnd()
    {
        var weather = new WeatherSystem(Shipped, 42);

        Assert.True(weather.Force("rain", 100));
        Assert.False(weather.Force("rain", 101));
        Assert.True(weather.IsHeld);
        Assert.False(weather.Advance(long.MaxValue - 1));
        Assert.False(weather.RollMorning(500));
        Assert.Equal("rain", weather.Current.Id);
        Assert.Equal(1, weather.BlendAt(100)); // a force is a cut
        weather.Release(1_000);

        Assert.False(weather.IsHeld);
        Assert.Equal("rain", weather.Current.Id);
        Assert.Equal(1_000, weather.State.SpellStart);
        Assert.InRange(weather.State.SpellEnd - 1_000, 120 * Second, 300 * Second);
        Assert.True(weather.Advance(weather.State.SpellEnd));
        Assert.NotEqual("rain", weather.Current.Id); // rain is followed by cloudy or sunny
    }

    [Fact]
    public void ReleasingAKindThatIsNotHeldChangesNothing()
    {
        var weather = new WeatherSystem(Shipped, 42);

        weather.Release(500);

        Assert.True(weather.IsInitial);
    }

    [Fact]
    public void AnUnknownKindThrows()
    {
        var weather = new WeatherSystem(Shipped, 42);

        Assert.Throws<ArgumentException>(() => weather.Force("hail", 0));
        Assert.Throws<ArgumentException>(() => Shipped.Kind("hail"));
    }

    [Fact]
    public void TheStateRoundTripsAndContinuesTheSameWay()
    {
        var a = new WeatherSystem(Shipped, 42);
        Run(a, 70_000);
        var b = new WeatherSystem(Shipped, 42);
        b.Restore(a.State);

        Assert.Equal(Run(a, 200_000), Run(b, 200_000));
        Assert.Equal(a.State, b.State);
    }

    [Fact]
    public void RestoringNothingGivesTheStateOfANewGame()
    {
        var weather = new WeatherSystem(Shipped, 42);
        weather.Force("rain", 10);

        weather.Restore(null);

        Assert.True(weather.IsInitial);
    }

    [Fact]
    public void AStateThatNamesAnUnknownKindOrHasNegativeCountersDoesNotRestore()
    {
        var weather = new WeatherSystem(Shipped, 42);

        Assert.Throws<ArgumentOutOfRangeException>(() => weather.Restore(weather.State with { Current = "hail" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => weather.Restore(weather.State with { Previous = "hail" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => weather.Restore(weather.State with { Spells = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => weather.Restore(weather.State with { SpellEnd = -5 }));
    }

    [Fact]
    public void AZeroTransitionBlendsAtOnce()
    {
        var weather = new WeatherSystem(Shipped with { TransitionSeconds = 0 }, 42);
        weather.Advance(weather.State.SpellEnd);

        Assert.Equal(1, weather.BlendAt(weather.State.SpellStart));
    }

    [Fact]
    public void AFourthKindOnlyTheTestsDefineRunsThroughTheSystemAndTheSnapshotWithNoCodeNamingIt()
    {
        var data = WeatherTestData.WithMist();
        Assert.Empty(WeatherValidator.Validate(data));
        var clock = new DayClock(DayData.Default);
        var seen = new HashSet<string>();
        var mistSnapshot = default(AtmosphereSnapshot);
        for (ulong seed = 0; seed < 40 && mistSnapshot.KindId is null; seed++)
        {
            var weather = new WeatherSystem(data, seed);
            for (var i = 0; i < 40; i++)
            {
                weather.Advance(weather.State.SpellEnd);
                seen.Add(weather.Current.Id);
                if (weather.Current.Id == "mist")
                {
                    var tick = weather.State.SpellStart + (60 * Second);
                    mistSnapshot = AtmosphereModel.At(clock, weather, tick);
                    break;
                }
            }
        }

        Assert.Contains("mist", seen);
        Assert.Equal("mist", mistSnapshot.KindId);
        Assert.Equal(new WeatherChannels(0.6, 0.8, 0.95, 0.05, "drizzle", 0.2).Fog, mistSnapshot.Channels.Fog, 9); // past the 45 s transition: the kind's own channels
        Assert.Equal("drizzle", mistSnapshot.Channels.PrecipitationKind);
        Assert.Equal(0.2, mistSnapshot.Channels.PrecipitationIntensity, 9);
    }

    [Fact]
    public void AFourthKindCanBeForcedAndBlendedWithItsOwnChannels()
    {
        var weather = new WeatherSystem(WeatherTestData.WithMist(), 42);
        weather.Force("mist", 0);

        var channels = weather.ChannelsAt(10);

        Assert.Equal(0.95, channels.Fog, 9);
        Assert.Equal("drizzle", channels.PrecipitationKind);
    }
}
