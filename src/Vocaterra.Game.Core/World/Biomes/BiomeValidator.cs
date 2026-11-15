using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.World.Biomes;

/// <summary>
/// The checks of <c>game/biomes.json</c> that need more than its shape (B01): ids unique and well formed, ranges ordered and inside what their field can be, a final catch-all row so every
/// column has a biome, every block name one the game has (the scene layout's own table, <see cref="SceneLayoutParser.BlockNamed"/>) with a solid ground and soft undergrowth,
/// every tree species and crown one the code has, and densities and weights in range. <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static partial class BiomeValidator
{
    private const string File = BiomeData.RelativePath;

    /// <summary>Every problem of the biomes, in a fixed order; empty when the values hold together.</summary>
    /// <param name="biomes">The biomes.</param>
    /// <param name="world">The world's numbers (the heights the rows may name).</param>
    public static IReadOnlyList<string> Validate(BiomeData biomes, WorldGenData world)
    {
        ArgumentNullException.ThrowIfNull(biomes);
        ArgumentNullException.ThrowIfNull(world);
        var problems = new List<string>();
        if (biomes.Rows.Count == 0)
        {
            problems.Add($"{File}: there must be at least one biome.");
            return problems;
        }

        if (biomes.DitherTemperature < 0 || biomes.DitherTemperature > 500 || biomes.DitherHumidity < 0 || biomes.DitherHumidity > 500)
        {
            problems.Add($"{File}: dither must be from 0 to 500 thousandths (it is {biomes.DitherTemperature} and {biomes.DitherHumidity}).");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in biomes.Rows)
        {
            if (!Id().IsMatch(row.Id))
            {
                problems.Add($"{File}: the id '{row.Id}' is not lowercase kebab-case.");
            }

            if (!ids.Add(row.Id))
            {
                problems.Add($"{File}: the id '{row.Id}' is used twice.");
            }

            CheckRow(row, world, problems);
        }

        if (!biomes.Rows[^1].When.IsNone)
        {
            problems.Add($"{File}: the last biome '{biomes.Rows[^1].Id}' must have no conditions, so that every column has a biome.");
        }

        return problems;
    }

    private static void CheckRow(BiomeRow row, WorldGenData world, List<string> problems)
    {
        var w = row.When;
        Range(problems, row, "continentalness", w.Continentalness, -1000, 1000);
        Range(problems, row, "erosion", w.Erosion, -1000, 1000);
        Range(problems, row, "ridges", w.Ridges, 0, 1000);
        Range(problems, row, "temperature", w.Temperature, -1000, 1000);
        Range(problems, row, "humidity", w.Humidity, -1000, 1000);
        Range(problems, row, "river", w.River, 0, 1000);
        Range(problems, row, "height", w.Height, world.Vertical.MinY, world.Vertical.MaxY);
        Range(problems, row, "surfaceTemperature", w.SurfaceTemperature, -3000, 1000);

        CheckBlock(problems, row, "top", row.Top, solid: true);
        CheckBlock(problems, row, "filler", row.Filler, solid: true);
        if (row.Underwater is not null)
        {
            CheckBlock(problems, row, "underwater", row.Underwater, solid: true);
        }

        if (row.FillerDepth < 0 || row.FillerDepth > 16)
        {
            problems.Add($"{File}: '{row.Id}' has a fillerDepth of {row.FillerDepth}; it must be from 0 to 16.");
        }

        if (row.BaseTemperature is { } baseTemperature && (baseTemperature < -1000 || baseTemperature > 1000))
        {
            problems.Add($"{File}: the baseTemperature of '{row.Id}' must be from -1000 to 1000 (it is {baseTemperature}).");
        }

        CheckTrees(row, problems);
        CheckDecor(row, problems);
    }

    private static void CheckTrees(BiomeRow row, List<string> problems)
    {
        var trees = row.Trees;
        if (trees.Density < 0 || trees.Density > 100)
        {
            problems.Add($"{File}: the tree density of '{row.Id}' must be from 0 to 100 percent (it is {trees.Density}).");
        }

        if (trees.Density > 0 && trees.Species.Count == 0)
        {
            problems.Add($"{File}: '{row.Id}' has a tree density above zero and no species.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in trees.Species)
        {
            if (!TreeStrategies.Species.Contains(tree.Species, StringComparer.Ordinal))
            {
                problems.Add($"{File}: '{row.Id}' names the unknown tree species '{tree.Species}' (the code knows {string.Join(", ", TreeStrategies.Species)}).");
            }

            if (tree.Crown is not null && !TreeStrategies.Crowns.Contains(tree.Crown, StringComparer.Ordinal))
            {
                problems.Add($"{File}: '{row.Id}' names the unknown crown '{tree.Crown}' (the code knows {string.Join(", ", TreeStrategies.Crowns)}).");
            }

            if (tree.Weight <= 0)
            {
                problems.Add($"{File}: the weight of '{tree.Species}' in '{row.Id}' must be positive (it is {tree.Weight}).");
            }

            if (!seen.Add(tree.Species))
            {
                problems.Add($"{File}: '{row.Id}' lists the species '{tree.Species}' twice.");
            }
        }
    }

    private static void CheckDecor(BiomeRow row, List<string> problems)
    {
        var decor = row.Decor;
        if (decor.Density < 0 || decor.Density > 1000)
        {
            problems.Add($"{File}: the undergrowth density of '{row.Id}' must be from 0 to 1000 thousandths (it is {decor.Density}).");
        }

        if (decor.Density > 0 && decor.Blocks.Count == 0)
        {
            problems.Add($"{File}: '{row.Id}' has an undergrowth density above zero and no blocks.");
        }

        foreach (var entry in decor.Blocks)
        {
            CheckBlock(problems, row, "undergrowth", entry.Block, solid: false);
            if (entry.Weight <= 0)
            {
                problems.Add($"{File}: the weight of '{entry.Block}' in the undergrowth of '{row.Id}' must be positive (it is {entry.Weight}).");
            }
        }
    }

    private static void CheckBlock(List<string> problems, BiomeRow row, string part, string name, bool solid)
    {
        try
        {
            var id = SceneLayoutParser.BlockNamed(name, $"{File}: '{row.Id}' {part}");
            if (BlockDefinition.IsSolid(id) != solid)
            {
                problems.Add($"{File}: the {part} block '{name}' of '{row.Id}' must be {(solid ? "solid" : "soft undergrowth that a body walks through")}.");
            }
        }
        catch (SceneLayoutException ex)
        {
            problems.Add(ex.Message);
        }
    }

    private static void Range(List<string> problems, BiomeRow row, string name, IntRange range, int min, int max)
    {
        if (range.Min != int.MinValue && range.Max != int.MaxValue && range.Min > range.Max)
        {
            problems.Add($"{File}: '{row.Id}' has {name} with min {range.Min} above max {range.Max}.");
        }

        if ((range.Min != int.MinValue && (range.Min < min || range.Min > max)) || (range.Max != int.MaxValue && (range.Max < min || range.Max > max)))
        {
            problems.Add($"{File}: '{row.Id}' has {name} outside {min} to {max}.");
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex Id();
}
