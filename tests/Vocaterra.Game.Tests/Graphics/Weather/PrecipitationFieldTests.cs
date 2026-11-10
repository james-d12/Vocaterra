using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Weather;

namespace Vocaterra.Game.Tests.Graphics.Weather;

/// <summary>The rain's drops (E06): deterministic, wrapping round the camera, never under the cover of their column, thickening with the intensity and slanted by the wind.</summary>
public sealed class PrecipitationFieldTests
{
    // The precipitation kind of the bundle's data (the tests never write its id: the style table is the one place it is written).
    private static readonly string Kind = TestGame.Data.Weather.Kinds.Select(static k => k.Channels.PrecipitationKind).First(static p => p.Length > 0);

    private const double CameraX = 60.5;
    private const double CameraY = 66;
    private const double CameraZ = 60.5;
    private const int Budget = 1600;

    private static PrecipitationState Rain(double intensity = 1, double wind = 0) => new(Kind, intensity, wind);

    private static List<Streak> Drops(PrecipitationField field, double x, double y, double z, long tick, PrecipitationState state, int budget = Budget)
    {
        var buffer = new Streak[PrecipitationField.MaxDrops];
        var count = field.Fill(buffer, x, y, z, tick, state, budget, out _);
        return buffer.Take(count).ToList();
    }

    private static PrecipitationField FieldOver(ColumnCover cover, Action<string>? log = null) => new(cover, log);

    [Fact]
    public void Given_TheSameTickAndCamera_When_TwoFieldsFill_Then_TheDropsAreTheSame()
    {
        var a = Drops(FieldOver(new ColumnCover(WeatherTestWorld.Flat())), CameraX, CameraY, CameraZ, 300, Rain(0.8, 0.6));
        var b = Drops(FieldOver(new ColumnCover(WeatherTestWorld.Flat())), CameraX, CameraY, CameraZ, 300, Rain(0.8, 0.6));

        Assert.NotEmpty(a);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Given_ALaterTick_When_Filled_Then_TheDropsHaveFallen()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));

        var now = Drops(field, CameraX, CameraY, CameraZ, 300, Rain());
        var later = Drops(field, CameraX, CameraY, CameraZ, 330, Rain()); // half a second

        Assert.NotEqual(now, later);
        // The same drop is lower by the style's speed times the time (the first drop that is in both, by its x).
        PrecipitationStyles.TryFor(Kind, out var style);
        var fallen = now.Zip(later).Where(static p => Math.Abs(p.First.HeadX - p.Second.HeadX) < 0.001f).Select(static p => p.First.HeadY - p.Second.HeadY).ToList();
        Assert.NotEmpty(fallen);
        Assert.Contains(fallen, f => Math.Abs(f - (style.FallSpeed * 0.5f)) < style.FallSpeed * 0.5f * 0.2f);
    }

    [Fact]
    public void Given_ACameraMovedByTheBoxsSize_When_Filled_Then_TheDropsAreTheSameRelativeToIt()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));
        var home = Drops(field, CameraX, CameraY, CameraZ, 300, Rain(1, 0.4));

        var east = Drops(field, CameraX + PrecipitationField.BoxWidth, CameraY, CameraZ, 300, Rain(1, 0.4));
        var south = Drops(field, CameraX, CameraY, CameraZ + PrecipitationField.BoxWidth, 300, Rain(1, 0.4));
        var high = Drops(field, CameraX, 100, CameraZ, 300, Rain(1, 0.4)); // high above the ground, so no drop is under the cover at either height
        var higher = Drops(field, CameraX, 100 + PrecipitationField.BoxHeight, CameraZ, 300, Rain(1, 0.4));

        // The ground is flat and the same everywhere, so the cover does not differ: only the pattern is compared (drop by drop; the sums differ by float noise at the wrap).
        AssertSamePattern(home, CameraX, CameraY, CameraZ, east, CameraX + PrecipitationField.BoxWidth, CameraY, CameraZ);
        AssertSamePattern(home, CameraX, CameraY, CameraZ, south, CameraX, CameraY, CameraZ + PrecipitationField.BoxWidth);
        AssertSamePattern(high, CameraX, 100, CameraZ, higher, CameraX, 100 + PrecipitationField.BoxHeight, CameraZ);
    }

    private static void AssertSamePattern(List<Streak> a, double ax, double ay, double az, List<Streak> b, double bx, double by, double bz)
    {
        Assert.NotEmpty(a);
        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].HeadX - ax, b[i].HeadX - bx, 0.01);
            Assert.Equal(a[i].HeadY - ay, b[i].HeadY - by, 0.01);
            Assert.Equal(a[i].HeadZ - az, b[i].HeadZ - bz, 0.01);
            Assert.Equal(a[i].Alpha, b[i].Alpha, 0.001);
        }
    }

    [Fact]
    public void Given_TheBoxWrapsRoundTheCamera_When_Filled_Then_EveryDropIsInsideTheBox()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));

        foreach (var (x, z) in new[] { (60.5, 60.5), (61.9, 60.1), (13.2, 100.7) })
        {
            var drops = Drops(field, x, CameraY, z, 123, Rain(1, 0.5));

            Assert.NotEmpty(drops);
            Assert.All(drops, d =>
            {
                Assert.InRange(d.HeadX - x, -(PrecipitationField.BoxWidth / 2) - 1, (PrecipitationField.BoxWidth / 2) + 1);
                Assert.InRange(d.HeadZ - z, -(PrecipitationField.BoxWidth / 2) - 1, (PrecipitationField.BoxWidth / 2) + 1);
            });
        }
    }

    [Fact]
    public void Given_ARoof_When_Filled_Then_NoDropIsDrawnBelowItOverItsColumns()
    {
        var grid = WeatherTestWorld.Flat();
        WeatherTestWorld.Roof(grid, 50, 50, 70, 70); // the roof's top face is at 71; the ground's at 64
        var field = FieldOver(new ColumnCover(grid));
        var inside = 0;
        var beside = 0;
        for (long tick = 0; tick < 600; tick += 37)
        {
            foreach (var d in Drops(field, CameraX, CameraY, CameraZ, tick, Rain(1, 0.3)))
            {
                var under = d.HeadX is > 51.5f and < 68.5f && d.HeadZ is > 51.5f and < 68.5f; // well inside the roof's footprint (a streak is cut at its own column and may lean a little)
                var open = d.HeadX is < 48f or > 73f || d.HeadZ is < 48f or > 73f;
                if (under)
                {
                    inside++;
                    Assert.True(d.HeadY >= 71f - 0.001f, $"a drop at y {d.HeadY} under the roof at ({d.HeadX:0.0}, {d.HeadZ:0.0})");
                }

                if (open)
                {
                    beside++;
                    Assert.True(d.HeadY >= 64f - 0.001f, $"a drop under the ground at y {d.HeadY}");
                }
            }
        }

        Assert.True(inside > 0, "drops fall on the roof");
        Assert.True(beside > 0, "drops fall beside the roof, down to the ground");
    }

    [Fact]
    public void Given_ARoofOverTheCamera_When_Filled_Then_ThereIsLessRainThanInTheOpen()
    {
        var grid = WeatherTestWorld.Flat();
        WeatherTestWorld.Roof(grid, 50, 50, 70, 70);
        var open = Drops(FieldOver(new ColumnCover(WeatherTestWorld.Flat())), CameraX, 66, CameraZ, 100, Rain()).Count;

        var covered = Drops(FieldOver(new ColumnCover(grid)), CameraX, 66, CameraZ, 100, Rain()).Count;

        Assert.True(covered < open, $"{covered} under a roof, {open} in the open");
    }

    [Fact]
    public void Given_ARoofRemoved_When_Filled_Then_TheRainFallsThroughItsColumns()
    {
        var grid = WeatherTestWorld.Flat();
        WeatherTestWorld.Roof(grid, 50, 50, 70, 70);
        var field = FieldOver(new ColumnCover(grid));
        Assert.DoesNotContain(Drops(field, CameraX, CameraY, CameraZ, 90, Rain()), static d => d.HeadX is > 51.5f and < 68.5f && d.HeadZ is > 51.5f and < 68.5f && d.HeadY < 70f);

        for (var z = 50; z <= 70; z++)
        {
            for (var x = 50; x <= 70; x++)
            {
                grid.SetBlock(x, WeatherTestWorld.RoofBlock, z, BlockId.Air);
            }
        }

        var drops = Drops(field, CameraX, CameraY, CameraZ, 90, Rain());
        Assert.Contains(drops, static d => d.HeadX is > 51.5f and < 68.5f && d.HeadZ is > 51.5f and < 68.5f && d.HeadY < 70f);
    }

    [Fact]
    public void Given_RisingIntensity_When_Filled_Then_TheDropsAliveNeverFallAndStartAtZero()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));
        var previous = -1;
        var counts = new List<int>();
        for (var step = 0; step <= 100; step++)
        {
            var count = Drops(field, CameraX, CameraY, CameraZ, 200, Rain(step / 100.0)).Count;
            counts.Add(count);
            Assert.True(count >= previous, $"intensity {step / 100.0}: {count} after {previous}");
            previous = count;
        }

        Assert.Equal(0, counts[0]);
        Assert.True(counts[100] > counts[50] && counts[50] > counts[10] && counts[10] > 0);
    }

    [Fact]
    public void Given_ARisingIntensity_When_Filled_Then_TheDropsThickenInSmallSteps()
    {
        // No pop: the number alive changes by a few drops between two near intensities, never by a clump.
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));
        var previous = Drops(field, CameraX, CameraY, CameraZ, 200, Rain(0.4)).Count;
        for (var step = 41; step <= 100; step++)
        {
            var count = Drops(field, CameraX, CameraY, CameraZ, 200, Rain(step / 100.0)).Count;
            Assert.InRange(count - previous, 0, 40);
            previous = count;
        }
    }

    [Fact]
    public void Given_TheLastDropAlive_When_TheIntensityRises_Then_ItFadesInAndDoesNotAppearAtFullOpacity()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));
        var buffer = new Streak[PrecipitationField.MaxDrops];
        PrecipitationStyles.TryFor(Kind, out var style);

        var n = field.Fill(buffer, CameraX, CameraY, CameraZ, 200, Rain(0.5), 1000, out _);
        var more = field.Fill(buffer, CameraX, CameraY, CameraZ, 200, Rain(0.5 + 0.0004), 1000, out _);

        Assert.True(more >= n);
        Assert.All(buffer.Take(more), d => Assert.InRange(d.Alpha, 0f, style.Opacity + 0.0001f));
    }

    [Fact]
    public void Given_NoWind_When_Filled_Then_StreaksAreVertical_AndWithWindTheyLeanDownWindByTheStylesSlant()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));
        PrecipitationStyles.TryFor(Kind, out var style);

        var calm = Drops(field, CameraX, CameraY, CameraZ, 100, Rain(1, 0));
        var gale = Drops(field, CameraX, CameraY, CameraZ, 100, Rain(1, 1));

        Assert.All(calm, d => Assert.Equal(0f, Math.Abs(d.TailX - d.HeadX) + Math.Abs(d.TailZ - d.HeadZ), 3));
        foreach (var d in gale.Where(static d => d.TailY - d.HeadY > 0.5f))
        {
            var rise = d.TailY - d.HeadY;
            var along = ((d.HeadX - d.TailX) * PrecipitationField.WindHeading.X) + ((d.HeadZ - d.TailZ) * PrecipitationField.WindHeading.Z); // how far the head is down wind of the tail
            Assert.True(along > 0, "the head is down wind of the tail");
            Assert.Equal(style.WindSlant, along / rise, 2);
        }
    }

    [Fact]
    public void Given_AStrongerWind_When_Filled_Then_TheSlantGrowsWithIt()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));
        double Lean(double wind)
        {
            var drops = Drops(field, CameraX, CameraY, CameraZ, 100, Rain(1, wind)).Where(static d => d.TailY - d.HeadY > 0.5f).ToList();
            return drops.Average(static d => (d.HeadX - d.TailX) / (d.TailY - d.HeadY));
        }

        Assert.True(Lean(0.2) < Lean(0.6));
        Assert.True(Lean(0.6) < Lean(1.0));
    }

    [Fact]
    public void Given_AnUnknownKind_When_Filled_Then_NothingIsDrawnAndItIsReportedOnce()
    {
        var lines = new List<string>();
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()), lines.Add);
        var state = new PrecipitationState("drizzle", 1, 0.5); // a kind of the tests' data that has no style

        var first = Drops(field, CameraX, CameraY, CameraZ, 10, state);
        var second = Drops(field, CameraX, CameraY, CameraZ, 20, state);

        Assert.Empty(first);
        Assert.Empty(second);
        var line = Assert.Single(lines);
        Assert.Contains("drizzle", line);
    }

    [Fact]
    public void Given_NoKindOrNoBudget_When_Filled_Then_NothingIsDrawn()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));

        Assert.Empty(Drops(field, CameraX, CameraY, CameraZ, 10, PrecipitationState.None));
        Assert.Empty(Drops(field, CameraX, CameraY, CameraZ, 10, Rain(), budget: 0));
    }

    [Fact]
    public void Given_ABudget_When_TheIntensityIsFull_Then_NoMoreThanTheBudgetIsDrawn()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));
        var drops = Drops(field, CameraX, CameraY, CameraZ, 10, Rain(), 700);

        Assert.InRange(drops.Count, 1, 700);
        Assert.Equal(700, PrecipitationField.AliveCount(700, 1f, 1));
        Assert.Equal(350, PrecipitationField.AliveCount(700, 0.5f, 1));
        Assert.Equal(0, PrecipitationField.AliveCount(700, 1f, 0));
    }

    [Fact]
    public void Given_EveryStreak_When_Filled_Then_TheTailIsAboveTheHeadAndTheLengthIsTheStyles()
    {
        var field = FieldOver(new ColumnCover(WeatherTestWorld.Flat()));
        PrecipitationStyles.TryFor(Kind, out var style);

        foreach (var d in Drops(field, CameraX, CameraY, CameraZ, 50, Rain(1, 0.5)))
        {
            var length = MathF.Sqrt(MathF.Pow(d.TailX - d.HeadX, 2) + MathF.Pow(d.TailY - d.HeadY, 2) + MathF.Pow(d.TailZ - d.HeadZ, 2));
            Assert.True(d.TailY > d.HeadY);
            Assert.InRange(length, 0.01f, style.Length * 1.25f + 0.001f);
        }
    }
}
