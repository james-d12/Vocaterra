namespace Vocaterra.Nlp.Tests;

/// <summary>
/// The xUnit collection of the tests that hold code to a time budget (M100, the same rule as the game's <c>TimingCollection</c>, B24). It is not parallel:
/// its tests run one at a time, so a budget measures the code and not the other tests of the assembly. The budgets count CPU time, not wall time, so another
/// process's load does not make the code look slower.
/// </summary>
[CollectionDefinition(TimingCollection.Name, DisableParallelization = true)]
public static class TimingCollection
{
    /// <summary>The collection's name, as the tests refer to it.</summary>
    public const string Name = "Timing";
}
