using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;

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
        "src/Vocaterra.Game.Core/World/Generation/Procedural",
    ];

    private static readonly string[] PureNamespaces =
    [
        "Vocaterra.Game.Core.World.Noise",
        "Vocaterra.Game.Core.World.Climate",
        "Vocaterra.Game.Core.World.Biomes",
        "Vocaterra.Game.Core.World.Generation.Procedural",
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
    public void Given_EveryTypeOfTheProceduralFolders_When_ItsFieldsAreRead_Then_EveryInstanceFieldIsReadonlyAndNoStaticIsMutable()
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();
        foreach (var type in PureTypes().SelectMany(static t => new[] { t }.Concat(t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)))
                     .Where(static t => t.GetCustomAttribute<CompilerGeneratedAttribute>() is null))
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
                    found.AddRange(Banned().Matches(code).Select(m => $"{Path.GetRelativePath(Repo.Root, file)}:{number}: {m.Value}"));
                }
            }
        }

        Assert.True(found.Count == 0, "These belong outside the pure world code:\n" + string.Join("\n", found));
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
