namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// The xUnit collection of the tests that hold code to a time budget. It is not parallel: its tests run one at a time after the rest of the
/// assembly, so a budget measures the code and not the other tests (since the route is built once per seed, a run's first seconds build it
/// for every seed at once; docs/TEST_PERFORMANCE_PLAN.md). The other test assembly of a -m:2 run may still be running beside it.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public static class TimingCollection
{
    /// <summary>The collection's name, as the tests refer to it.</summary>
    public const string Name = "Timing";
}
