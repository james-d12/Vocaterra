using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>
/// The opening acceptance (N05, adaptation design 9.8 and 9.13): day 1's route, played with the needs on, is safe at the intended pace and at a slower novice pace. The route is
/// the camp objectives (five trunks, three berries, the torch at the ring), the walk to the river and the bottle filled there, and the way back to the fire. The bot walks
/// without a pause, so its active time is the intended pace; the novice pace is that time stretched by <see cref="NoviceFactor"/> (the extra active time spent standing about,
/// which costs the drives as much as walking does). The talks are not in it: the needs stop under a conversation. The numbers are printed.
/// </summary>
public sealed class DayOneSafetyTests(ITestOutputHelper output)
{
    /// <summary>How much longer a novice's day 1 is than the bot's route: 2.5 times the active time.</summary>
    private const double NoviceFactor = 2.5;

    private sealed record Day(double ActiveSeconds, double Lowest, string LowestMeter, int Deaths, int Collapses, int Recoveries, int[] Stages, double[] Meters);

    private static Day Play(double factor)
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();
        var water = Stage.NearestRiver(session);
        Stage.WalkToWater(session, water);
        Assert.True(Stage.Fill(session, water) > 0, "the bottle fills at the river");
        var ring = session.Scene.Point("firering");
        Assert.True(VoxelWalker.WalkTo(session, cell => Math.Abs(cell.X - ring.X) <= 3 && Math.Abs(cell.Z - ring.Z) <= 3), "the way back to the fire");

        var routeTicks = session.Simulation.Tick;
        var extra = (long)Math.Round(routeTicks * (factor - 1));
        var lowestMeter = "none";
        var lowest = double.MaxValue;
        var drives = new[] { NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep };
        for (long tick = 0; tick <= extra; tick++)
        {
            foreach (var meter in drives)
            {
                var value = session.Needs.Value(meter);
                if (value < lowest)
                {
                    (lowest, lowestMeter) = (value, meter.ToString());
                }
            }

            if (tick < extra)
            {
                session.Step([]);
            }
        }

        var names = session.Events.OfType<WorldEvent>().Select(static e => e.Name).ToList();
        return new Day(
            session.Simulation.Tick / (double)NeedsData.TicksPerSecond,
            lowest,
            lowestMeter,
            session.DeathCount,
            names.Count(static n => n == WorldEvents.PlayerCollapsed),
            names.Count(static n => n is WorldEvents.MealEaten or WorldEvents.WaterDrunk),
            [session.Needs.StageOf(Drive.Hunger), session.Needs.StageOf(Drive.Thirst), session.Needs.StageOf(Drive.Sleep), session.Needs.HealthStage],
            [session.Needs.Value(NeedMeter.Hunger), session.Needs.Value(NeedMeter.Thirst), session.Needs.Value(NeedMeter.Sleep), session.Needs.Value(NeedMeter.Health)]);
    }

    private void Report(string pace, Day day)
    {
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{pace}: active time {day.ActiveSeconds:0} s, lowest drive {day.LowestMeter} {day.Lowest:0.0}, hunger/thirst/sleep/health {day.Meters[0]:0.0}/{day.Meters[1]:0.0}/{day.Meters[2]:0.0}/{day.Meters[3]:0.0} (stages {string.Join('/', day.Stages)}), deaths {day.Deaths}, collapses {day.Collapses}, recovery actions {day.Recoveries}"));
    }

    private static void AssertSafe(Day day)
    {
        Assert.Equal(0, day.Deaths);
        Assert.Equal(0, day.Collapses);
        Assert.All(day.Stages[..3], static stage => Assert.True(stage < 2, "no drive reaches stage 2 on day 1"));
        Assert.Equal(0, day.Stages[3]);
    }

    [Fact]
    public void Day1IsSafeAtTheIntendedPace()
    {
        var day = Play(1.0);
        Report("intended pace", day);
        AssertSafe(day);
    }

    [Fact]
    public void Day1IsSafeAtANovicePaceOfTwoAndAHalfTimesTheActiveTime()
    {
        var day = Play(NoviceFactor);
        Report($"novice pace ({NoviceFactor}x)", day);
        AssertSafe(day);
    }

    [Fact]
    public void ADayOneFarSlowerThanThatIsStillNoDeathAtThreeTimes()
    {
        // Information for the needs check, and a guard: at three times the active time nobody has died or collapsed, with no berry eaten and no drink taken.
        var day = Play(3.0);
        Report("slow pace (3x)", day);
        Assert.Equal(0, day.Deaths);
        Assert.Equal(0, day.Collapses);
    }
}
