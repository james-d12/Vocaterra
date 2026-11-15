using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Biomes;

namespace Vocaterra.Game.Core.Tests.World.Biomes;

/// <summary>Biome data the biome tests share: the shipped file, and rows only the tests define.</summary>
internal static class BiomeTestData
{
    public static BiomeData Shipped => TestGame.Data.Biomes;

    public static WorldGenData World => TestGame.Data.WorldGen;

    /// <summary>A row with the given conditions and the plainest ground.</summary>
    public static BiomeRow Row(string id, BiomeConditions? when = null, string top = "grass", string filler = "dirt") =>
        new(0, id, when ?? BiomeConditions.None, top, filler, 3, null, TreeRule.None, DecorRule.None, false, null);

    /// <summary>Data made of rows, numbered by their place.</summary>
    public static BiomeData Of(params BiomeRow[] rows) => new(50, 50, [.. rows.Select(static (r, i) => r with { Index = i })]);

    public static BiomeConditions Where(IntRange? humidity = null, IntRange? temperature = null, bool? water = null, IntRange? height = null, IntRange? surface = null, IntRange? river = null) =>
        BiomeConditions.None with
        {
            Water = water,
            Humidity = humidity ?? IntRange.Any,
            Temperature = temperature ?? IntRange.Any,
            Height = height ?? IntRange.Any,
            SurfaceTemperature = surface ?? IntRange.Any,
            River = river ?? IntRange.Any,
        };
}
