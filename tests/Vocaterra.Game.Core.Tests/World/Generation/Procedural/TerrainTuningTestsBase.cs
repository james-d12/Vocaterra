using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The shared parts of the test classes split out of TerrainTuningTests (B28: xUnit runs the tests of one class one after another, so the slow ones are in classes of their own): the fields and helpers.</summary>
public abstract class TerrainTuningTestsBase(ITestOutputHelper output)
{
    public static TheoryData<ulong> Seeds => new() { 42UL, 7UL, 2026UL };

    protected ITestOutputHelper Output => output;
}
