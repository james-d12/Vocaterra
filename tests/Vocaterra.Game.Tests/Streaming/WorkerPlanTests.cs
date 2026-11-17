using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The worker count of a run (B07d, D-game-34 item 42): Auto is the processor count less four held to 1 to 8, a setting is held to 1 to 8, a scripted run, the tests and <c>--prefetch</c> keep their small counts.</summary>
public sealed class WorkerPlanTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(8, 4)]
    [InlineData(12, 8)]
    [InlineData(16, 8)]
    [InlineData(64, 8)]
    [InlineData(0, 1)]
    public void Given_AProcessorCount_When_AutoIsAsked_Then_ItIsTheCountLessFourHeldToOneToEight(int processors, int expected)
    {
        Assert.Equal(expected, WorkerPlan.Auto(processors));
        Assert.Equal(expected, WorkerPlan.OfSetting(0, processors));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(8, 8)]
    [InlineData(20, 8)]
    public void Given_ASetting_When_ItIsAFixedCount_Then_ItIsHeldToOneToEight(int setting, int expected)
    {
        Assert.Equal(expected, WorkerPlan.OfSetting(setting, 16));
        Assert.Equal(expected, WorkerPlan.OfSetting(setting, 2)); // a fixed count does not depend on the machine
    }

    [Fact]
    public void Given_ARunThatIsScripted_When_TheCountIsChosen_Then_ItStaysTwoWhateverTheSettingSays()
    {
        Assert.Equal(JobPool.ScriptedWorkers, WorkerPlan.For(null, null, scripted: true, setting: 0, processors: 16));
        Assert.Equal(2, WorkerPlan.For(null, null, scripted: true, setting: 8, processors: 16)); // a save's setting never changes a scripted run
        Assert.Equal(6, WorkerPlan.For(null, 6, scripted: true, setting: 0, processors: 16)); // unless --workers asks
        Assert.Equal(8, WorkerPlan.For(null, 0, scripted: true, setting: 0, processors: 16));
    }

    [Fact]
    public void Given_AnInteractiveRun_When_TheCountIsChosen_Then_ItIsTheSettingAndPrefetchWinsOverIt()
    {
        Assert.Equal(8, WorkerPlan.For(null, null, scripted: false, setting: 0, processors: 16));
        Assert.Equal(3, WorkerPlan.For(null, null, scripted: false, setting: 3, processors: 16));
        Assert.Equal(0, WorkerPlan.For(0, null, scripted: false, setting: 3, processors: 16));
        Assert.Equal(2, WorkerPlan.For(2, 8, scripted: false, setting: 0, processors: 16));
        Assert.Equal(2, WorkerPlan.For(9, null, scripted: false, setting: 0, processors: 16)); // --prefetch is 0 to 2
    }

    [Fact]
    public void Given_ARunHeldToACount_When_TheSettingChanges_Then_NothingIsResized()
    {
        Assert.Null(WorkerPlan.Change(2, scripted: false, setting: 5, processors: 16));
        Assert.Null(WorkerPlan.Change(null, scripted: true, setting: 5, processors: 16));
        Assert.Equal(5, WorkerPlan.Change(null, scripted: false, setting: 5, processors: 16));
        Assert.Equal(8, WorkerPlan.Change(null, scripted: false, setting: 0, processors: 16));
    }

    [Fact]
    public void Given_TheBound_When_ItIsRead_Then_ItIsTheSettingsEightAndTheScriptedCountIsTwo()
    {
        Assert.Equal(8, JobPool.MaxWorkers);
        Assert.Equal(2, JobPool.ScriptedWorkers);
    }
}
