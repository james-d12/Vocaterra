using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The shared parts of the test classes split out of DrainageNetworkTests (B28: xUnit runs the tests of one class one after another, so the slow ones are in classes of their own): the fields and helpers.</summary>
public abstract class DrainageNetworkTestsBase(ITestOutputHelper output)
{
    internal const int Box = 4096;

    internal static WorldGenData World => TestGame.Data.WorldGen;

    public static TheoryData<ulong> Seeds => new() { 42UL, 7UL, 2026UL };

    protected ITestOutputHelper Output => output;
}
