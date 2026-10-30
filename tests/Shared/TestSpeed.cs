namespace Vocaterra.Nlp.Testing;

/// <summary>
/// The trait that keeps a test out of the fast tier: <c>[Trait(TestSpeed.Trait, TestSpeed.Slow)]</c> on a class or a method, or
/// <c>[assembly: AssemblyTrait(TestSpeed.Trait, TestSpeed.Slow)]</c>. <c>scripts/test.sh fast</c> runs <c>--filter "Speed!=Slow"</c>; the merge
/// gate, CI and <c>scripts/test.sh full</c> run every test (docs/TEST_PERFORMANCE_PLAN.md, T4). One source file, linked into the core's test
/// projects that mark tests.
/// </summary>
internal static class TestSpeed
{
    /// <summary>The trait's name.</summary>
    public const string Trait = "Speed";

    /// <summary>The value of a test that only the full suite runs.</summary>
    public const string Slow = "Slow";
}
