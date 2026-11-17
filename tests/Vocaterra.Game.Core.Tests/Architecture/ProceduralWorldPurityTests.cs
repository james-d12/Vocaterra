using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// The procedural world's seams are pure (B01, biomes plan section 3.2): every implementation is immutable, integer only and safe to call from any thread. A reflection test holds
/// the implementations and the types they are built from to readonly fields and no static mutable state; a token ban keeps threads, locks, the clock, random classes and floating point
/// out of the folders that make the world.
/// </summary>
public sealed partial class ProceduralWorldPurityTests
{
    private static readonly string[] PureFolders =
    [
        "src/Vocaterra.Game.Core/World/Noise",
        "src/Vocaterra.Game.Core/World/Climate",
        "src/Vocaterra.Game.Core/World/Biomes",
        "src/Vocaterra.Game.Core/World/Underground",
        "src/Vocaterra.Game.Core/World/Streaming",
        "src/Vocaterra.Game.Core/World/Regions",
        "src/Vocaterra.Game.Core/World/Far",
        // B24 (review A1): the rest of what the generator reads, so a thread, a clock or a float cannot enter the world's blocks beside Procedural/.
        "src/Vocaterra.Game.Core/World/Generation",
        "src/Vocaterra.Game.Core/World/Layout",
        "src/Vocaterra.Game.Core/World/Pass",
        "src/Vocaterra.Game.Core/World/Drainage",
    ];

    /// <summary>
    /// Files allowed one <c>lock</c> and nothing else banned: the drainage network reads a landmass on first touch and the workers share the result, so its build is guarded (B02c). Floating point, clocks, random
    /// classes, threads and tasks stay banned in these files, so the arithmetic check still covers them.
    /// </summary>
    private static readonly string[] LockAllowedFiles = ["World/Drainage/DrainageNetwork.cs"];

    /// <summary>The folders of <c>World/</c> that are not pure on purpose, each with its reason; every other folder must be in <see cref="PureFolders"/>.</summary>
    private static readonly Dictionary<string, string> NotPureWorldFolders = new()
    {
        ["Lod"] = "the far view's store: locks, interlocked counters and a disk writer's queue are its job (B07f), and it is not read by the generator",
    };

    private static readonly string[] PureNamespaces =
    [
        "Vocaterra.Game.Core.World.Noise",
        "Vocaterra.Game.Core.World.Climate",
        "Vocaterra.Game.Core.World.Biomes",
        "Vocaterra.Game.Core.World.Underground",
        "Vocaterra.Game.Core.World.Generation.Procedural",
        "Vocaterra.Game.Core.World.Streaming",
        "Vocaterra.Game.Core.World.Regions",
        "Vocaterra.Game.Core.World.Far",
    ];

    private static IEnumerable<Type> PureTypes() =>
        typeof(ClimateSampler).Assembly.GetTypes()
            .Where(static t => t.Namespace is not null && PureNamespaces.Contains(t.Namespace))
            .Where(static t => t.GetCustomAttribute<CompilerGeneratedAttribute>() is null);

    [Fact]
    public void Given_TheSeamImplementations_When_TheyAreFound_Then_TheClimateHeightAndBiomeOnesAreAmongTheTypesChecked()
    {
        var checkedTypes = PureTypes().ToList();

        Assert.Contains(typeof(ClimateSampler), checkedTypes);
        Assert.Contains(typeof(HeightModel), checkedTypes);
        Assert.Contains(typeof(BiomeTable), checkedTypes);
        Assert.All(new[] { typeof(IClimateSampler), typeof(IHeightModel), typeof(IBiomeSelector) }, seam => Assert.Contains(checkedTypes, t => seam.IsAssignableFrom(t) && !t.IsInterface));
    }

    [Fact]
    public void Given_TheChunkSourceAndThePassesOfB03_When_TheTypesAreFound_Then_TheyAreAmongTheTypesChecked()
    {
        var checkedTypes = PureTypes().ToList();

        Assert.Contains(typeof(ProceduralChunkSource), checkedTypes);
        Assert.Contains(typeof(ChunkData), checkedTypes);
        Assert.Contains(typeof(StrategyRegistry), checkedTypes);
        Assert.All(new[] { typeof(IChunkSource), typeof(IColumnShaper), typeof(IChunkDecorator), typeof(ISurfaceStrategy), typeof(IDecorationStrategy) }, seam => Assert.Contains(checkedTypes, t => seam.IsAssignableFrom(t) && !t.IsInterface));
    }

    [Fact]
    public void Given_TheRegionsAndTheCompositeSourceOfB06_When_TheTypesAreFound_Then_TheyAreAmongTheTypesChecked()
    {
        var checkedTypes = PureTypes().ToList();

        Assert.Contains(typeof(AuthoredReliefRegion), checkedTypes);
        Assert.Contains(typeof(RegionCatalog), checkedTypes);
        Assert.Contains(typeof(CompositeChunkSource), checkedTypes);
        Assert.Contains(typeof(BandStreamer), checkedTypes);
        Assert.Contains(checkedTypes, t => typeof(IRegion).IsAssignableFrom(t) && !t.IsInterface);
        Assert.Contains(checkedTypes, t => typeof(IChunkSource).IsAssignableFrom(t) && t == typeof(CompositeChunkSource));
        Assert.Contains(checkedTypes, t => typeof(IClimateSampler).IsAssignableFrom(t) && t.Namespace == "Vocaterra.Game.Core.World.Regions");
        Assert.Contains(checkedTypes, t => typeof(IHeightModel).IsAssignableFrom(t) && t.Namespace == "Vocaterra.Game.Core.World.Regions");
    }

    [Fact]
    public void Given_TheUndergroundOfB11_When_TheTypesAreFound_Then_TheSeamImplementationsAreAmongTheTypesChecked()
    {
        var checkedTypes = PureTypes().ToList();

        Assert.Contains(typeof(StrataTable), checkedTypes);
        Assert.Contains(typeof(OreTable), checkedTypes);
        Assert.Contains(typeof(CaveField), checkedTypes);
        Assert.Contains(typeof(EntranceSites), checkedTypes);
        Assert.Contains(typeof(SurfaceRock), checkedTypes);
        Assert.Contains(typeof(BedrockFloor), checkedTypes);
        Assert.Contains(typeof(UndergroundWorld), checkedTypes);
        Assert.All(new[] { typeof(IStrataSelector), typeof(ICaveField) }, seam => Assert.Contains(checkedTypes, t => seam.IsAssignableFrom(t) && !t.IsInterface));
    }

    [Fact]
    public void Given_EveryTypeOfTheProceduralFolders_When_ItsFieldsAreRead_Then_EveryInstanceFieldIsReadonlyAndNoStaticIsMutable()
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();
        foreach (var type in PureTypes().SelectMany(static t => new[] { t }.Concat(t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)))
                     .Where(static t => t.GetCustomAttribute<CompilerGeneratedAttribute>() is null && !t.IsEnum))
        {
            foreach (var field in type.GetFields(All))
            {
                if (!field.IsInitOnly && !field.IsLiteral)
                {
                    offenders.Add($"{type.FullName}.{field.Name} is a {(field.IsStatic ? "static" : "instance")} field that is not readonly");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Fact]
    public void Given_TheSourcesOfTheProceduralFolders_When_TheyAreScanned_Then_NoThreadLockClockRandomOrFloatingPointAppears()
    {
        var found = new List<string>();
        foreach (var folder in PureFolders)
        {
            var path = Repo.Path(folder.Split('/'));
            Assert.True(Directory.Exists(path), $"{folder} exists");
            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                var number = 0;
                foreach (var line in File.ReadLines(file))
                {
                    number++;
                    var code = Strings().Replace(Comment().Replace(line, string.Empty), "\"\"");
                    var lockAllowed = LockAllowedFiles.Any(a => file.Replace('\\', '/').EndsWith(a, StringComparison.Ordinal));
                    found.AddRange(Banned().Matches(code).Where(m => !(lockAllowed && m.Value == "lock")).Select(m => $"{Path.GetRelativePath(Repo.Root, file)}:{number}: {m.Value}"));
                }
            }
        }

        Assert.True(found.Count == 0, "These belong outside the pure world code:\n" + string.Join("\n", found));
    }

    [Fact]
    public void Given_EveryFolderOfTheWorld_When_TheyAreListed_Then_EachIsPureOrNamedAsNotPureWithAReason()
    {
        // A new folder under World/ (the drainage network of B02c, say) fails here until it joins PureFolders or is named with its reason.
        var world = Repo.Path("src", "Vocaterra.Game.Core", "World");
        var unlisted = Directory.EnumerateDirectories(world)
            .Select(static d => Path.GetFileName(d))
            .Where(name => !PureFolders.Contains($"src/Vocaterra.Game.Core/World/{name}") && !NotPureWorldFolders.ContainsKey(name!)
                && !PureFolders.Any(f => f.StartsWith($"src/Vocaterra.Game.Core/World/{name}/", StringComparison.Ordinal)))
            .ToList();

        Assert.True(unlisted.Count == 0, "World folders that are neither pure nor named as not pure: " + string.Join(", ", unlisted));
    }

    [Fact]
    public void Given_BannedTokensInCodeAndInComments_When_TheScanRuns_Then_OnlyCodeIsFound()
    {
        Assert.NotEmpty(Banned().Matches("var t = new Thread(Run);"));
        Assert.NotEmpty(Banned().Matches("lock (gate) { }"));
        Assert.NotEmpty(Banned().Matches("double x = 1;"));
        Assert.NotEmpty(Banned().Matches("var r = new System.Random();"));
        Assert.Empty(Banned().Matches("var blocks = 1; var floating = 2; var threads = 3;"));
        Assert.Equal(string.Empty, Comment().Replace("   /// a lock on the Task", string.Empty).Trim());
    }

    [GeneratedRegex("\\b(Thread|Task|lock|Interlocked|DateTime|float|double)\\b|System\\.Random|\\bnew Random\\b")]
    private static partial Regex Banned();

    [GeneratedRegex("^\\s*(//|///).*$|\\s//\\s.*$")]
    private static partial Regex Comment();

    [GeneratedRegex("\"[^\"\\\\]*\"")]
    private static partial Regex Strings();
}
