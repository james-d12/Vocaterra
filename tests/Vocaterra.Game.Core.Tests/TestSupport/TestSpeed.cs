namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// The trait that keeps a test out of the fast tier: <c>[Trait(TestSpeed.Trait, TestSpeed.Slow)]</c> on a class or a method, or
/// <c>[assembly: AssemblyTrait(TestSpeed.Trait, TestSpeed.Slow)]</c>. <c>scripts/test.sh fast</c> runs <c>--filter "Speed!=Slow"</c>; <c>scripts/test.sh full</c> and the merge
/// gate run every test but the timing tests and the fuzz suite (below) (docs/process/test-performance-plan.md, T4). Linked into Game.Tests; the core's test projects
/// have their own (tests/Shared/TestSpeed.cs).
/// </summary>
internal static class TestSpeed
{
    /// <summary>The trait's name.</summary>
    public const string Trait = "Speed";

    /// <summary>The value of a test that only the full suite runs.</summary>
    public const string Slow = "Slow";

    /// <summary>The name of the trait that sorts a test into a suite of its own (B29a): the timing tests and the fuzz suite run apart from the rest.</summary>
    public const string Category = "Category";

    /// <summary>The value of a test that holds code to a time budget (the classes of the <c>TimingCollection</c>): <c>scripts/test.sh timing</c> and the merge gate's timing step run it, no other mode does.</summary>
    public const string Timing = "Timing";

    /// <summary>The value of the fuzz suite's assembly trait: it runs in CI only (<c>scripts/test.sh ci</c>).</summary>
    public const string Fuzz = "Fuzz";
}
