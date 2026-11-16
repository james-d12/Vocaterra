using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>
/// The checks of <c>game/underground.json</c> that need more than its shape (B11): every block name one the game has and solid (lava is the fluid that stops a body), the strata's dither ordered
/// and inside the world, the blobs' blocks distinct, a floor of at least one block that cannot rise into the lava, ores with unique kebab-case ids whose veins replace only solid rock (never an ore, bedrock or
/// lava), bands that peak inside their range and lie inside the world, cave lattices that divide a chunk, thresholds inside the noise's range, the water margin no larger than the entrances' water reach
/// and the bed clearance no smaller than the margin, and the lava level above the floor and under the dither. <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static partial class UndergroundValidator
{
    private const string File = UndergroundData.RelativePath;
    private static readonly int[] Divisors = [1, 2, 4, 8, 16];

    /// <summary>Every problem of the underground, in a fixed order; empty when the values hold together.</summary>
    /// <param name="underground">The underground.</param>
    /// <param name="world">The world's numbers (the vertical range).</param>
    public static IReadOnlyList<string> Validate(UndergroundData underground, WorldGenData world)
    {
        ArgumentNullException.ThrowIfNull(underground);
        ArgumentNullException.ThrowIfNull(world);
        var problems = new List<string>();
        var v = world.Vertical;
        CheckStrata(underground.Strata, v, problems);
        CheckFloor(underground, v, problems);
        CheckCaves(underground, v, problems);
        CheckEntrances(underground, problems);
        CheckLava(underground, v, problems);
        CheckOres(underground, v, problems);
        return problems;
    }

    private static void CheckStrata(StrataSpec s, VerticalRange v, List<string> problems)
    {
        Block(problems, "strata.stone", s.Stone, solid: true);
        Block(problems, "strata.deep", s.Deep, solid: true);
        if (string.Equals(s.Stone, s.Deep, StringComparison.Ordinal))
        {
            problems.Add($"{File}: strata.stone and strata.deep must be different blocks.");
        }

        if (s.DitherFromY >= s.DitherToY || s.DitherFromY < v.MinY || s.DitherToY > v.MaxY)
        {
            problems.Add($"{File}: strata needs ditherFromY below ditherToY, both inside the world (they are {s.DitherFromY} and {s.DitherToY}).");
        }

        if (Array.IndexOf(Divisors, s.BlobLattice) < 0)
        {
            problems.Add($"{File}: strata.blobLattice must divide a chunk: 1, 2, 4, 8 or 16 (it is {s.BlobLattice}).");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal) { s.Stone, s.Deep };
        foreach (var blob in s.Blobs)
        {
            Block(problems, $"strata.blobs '{blob.Block}'", blob.Block, solid: true);
            if (!seen.Add(blob.Block))
            {
                problems.Add($"{File}: the block '{blob.Block}' is used twice in the strata.");
            }

            Noise(problems, $"strata.blobs '{blob.Block}'", blob.Cell, blob.CellY, blob.Octaves);
            Thousandths(problems, $"strata.blobs '{blob.Block}' threshold", blob.Threshold, -1000, 1000);
        }
    }

    private static void CheckFloor(UndergroundData d, VerticalRange v, List<string> problems)
    {
        var f = d.Floor;
        Block(problems, "floor.block", f.Block, solid: true);
        if (f.BottomY != v.MinY)
        {
            problems.Add($"{File}: floor.bottomY must be the world's lowest row {v.MinY} (it is {f.BottomY}).");
        }

        if (f.MinThickness < 1 || f.MaxThickness < f.MinThickness || f.MaxThickness > 8)
        {
            problems.Add($"{File}: floor needs 1 <= minThickness <= maxThickness <= 8 (they are {f.MinThickness} and {f.MaxThickness}).");
        }

        if (BlockDefinition.FromName(f.Block) is { } block && !block.Indestructible)
        {
            problems.Add($"{File}: the floor block '{f.Block}' must be indestructible (bedrock).");
        }
    }

    private static void CheckCaves(UndergroundData d, VerticalRange v, List<string> problems)
    {
        var c = d.Caves;
        if (Array.IndexOf(Divisors, c.LatticeXz) < 0 || Array.IndexOf(Divisors, c.LatticeY) < 0)
        {
            problems.Add($"{File}: caves.latticeXz and latticeY must divide a chunk: 1, 2, 4, 8 or 16 (they are {c.LatticeXz} and {c.LatticeY}).");
        }

        if (c.TopY < v.MinY || c.TopY >= v.MaxY)
        {
            problems.Add($"{File}: caves.topY {c.TopY} must lie inside the world.");
        }

        if (c.SurfaceCover < 1 || c.SurfaceCover > 32)
        {
            problems.Add($"{File}: caves.surfaceCover must be from 1 to 32 blocks (it is {c.SurfaceCover}).");
        }

        Noise(problems, "caves.cheese", c.Cheese.Cell, c.Cheese.CellY, c.Cheese.Octaves);
        Noise(problems, "caves.spaghetti", c.Spaghetti.Cell, c.Spaghetti.CellY, c.Spaghetti.Octaves);
        Thousandths(problems, "caves.cheese.deepThreshold", c.Cheese.DeepThreshold, 0, 1000);
        Thousandths(problems, "caves.cheese.shallowThreshold", c.Cheese.ShallowThreshold, 0, 1000);
        if (c.Cheese.ShallowThreshold < c.Cheese.DeepThreshold)
        {
            problems.Add($"{File}: caves.cheese.shallowThreshold must not be below deepThreshold, so the caverns lie deep (they are {c.Cheese.ShallowThreshold} and {c.Cheese.DeepThreshold}).");
        }

        if (c.Cheese.DeepY >= c.Cheese.ShallowY)
        {
            problems.Add($"{File}: caves.cheese.deepY must be below shallowY (they are {c.Cheese.DeepY} and {c.Cheese.ShallowY}).");
        }

        Thousandths(problems, "caves.spaghetti.thickness", c.Spaghetti.Thickness, 1, 500);
        if (c.UnderRegionCeiling < v.MinY || c.UnderRegionCeiling >= 0)
        {
            problems.Add($"{File}: caves.underRegionCeiling must be under the region's own chunks, from {v.MinY} to -1 (it is {c.UnderRegionCeiling}).");
        }

        if (c.WaterMargin < 1 || c.WaterMargin > 8)
        {
            problems.Add($"{File}: caves.waterMargin must be from 1 to 8 blocks (it is {c.WaterMargin}).");
        }

        if (c.BedClearance < c.WaterMargin || c.BedClearance > 64)
        {
            problems.Add($"{File}: caves.bedClearance must be from the waterMargin to 64 blocks (it is {c.BedClearance}).");
        }
    }

    private static void CheckEntrances(UndergroundData d, List<string> problems)
    {
        var e = d.Entrances;
        Noise(problems, "entrances", e.Cell, e.Cell, e.Octaves);
        Thousandths(problems, "entrances.threshold", e.Threshold, -1000, 1000);
        if (e.MinHeightAboveSea < 0 || e.MinHeightAboveSea > 64)
        {
            problems.Add($"{File}: entrances.minHeightAboveSea must be from 0 to 64 blocks (it is {e.MinHeightAboveSea}).");
        }

        if (e.WaterReach < d.Caves.WaterMargin || e.WaterReach > 8)
        {
            problems.Add($"{File}: entrances.waterReach must be from the caves' waterMargin to 8 columns (it is {e.WaterReach}).");
        }

        if (e.ProbeDepth < 1 || e.ProbeDepth > 32)
        {
            problems.Add($"{File}: entrances.probeDepth must be from 1 to 32 blocks (it is {e.ProbeDepth}).");
        }
    }

    private static void CheckLava(UndergroundData d, VerticalRange v, List<string> problems)
    {
        Block(problems, "lava.block", d.Lava.Block, solid: false);
        if (BlockDefinition.FromName(d.Lava.Block) is { } block && !block.Liquid)
        {
            problems.Add($"{File}: the lava block '{d.Lava.Block}' must be a liquid.");
        }

        var top = d.Floor.BottomY + d.Floor.MaxThickness - 1;
        if (d.Lava.Level <= top || d.Lava.Level >= d.Strata.DitherFromY)
        {
            problems.Add($"{File}: lava.level {d.Lava.Level} must lie above the floor's highest block ({top}) and under the strata's dither ({d.Strata.DitherFromY}).");
        }

        if (d.Lava.Level < v.MinY)
        {
            problems.Add($"{File}: lava.level {d.Lava.Level} lies under the world.");
        }
    }

    private static void CheckOres(UndergroundData d, VerticalRange v, List<string> problems)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var oreBlocks = new HashSet<string>(d.Ores.Select(static o => o.Block), StringComparer.Ordinal);
        foreach (var ore in d.Ores)
        {
            if (!Id().IsMatch(ore.Id))
            {
                problems.Add($"{File}: the ore id '{ore.Id}' is not lowercase kebab-case.");
            }

            if (!ids.Add(ore.Id))
            {
                problems.Add($"{File}: the ore id '{ore.Id}' is used twice.");
            }

            Block(problems, $"ore '{ore.Id}' block", ore.Block, solid: true);
            if (ore.Replaces.Count == 0)
            {
                problems.Add($"{File}: the ore '{ore.Id}' replaces no rock.");
            }

            foreach (var name in ore.Replaces)
            {
                Block(problems, $"ore '{ore.Id}' replaces", name, solid: true);
                if (oreBlocks.Contains(name) || string.Equals(name, d.Floor.Block, StringComparison.Ordinal))
                {
                    problems.Add($"{File}: the ore '{ore.Id}' may not replace '{name}' (an ore or the floor).");
                }
            }

            if (ore.SizeMin < 1 || ore.SizeMax < ore.SizeMin || ore.SizeMax > 64)
            {
                problems.Add($"{File}: the ore '{ore.Id}' needs 1 <= size.min <= size.max <= 64 (they are {ore.SizeMin} and {ore.SizeMax}).");
            }

            if (ore.Bands.Count == 0)
            {
                problems.Add($"{File}: the ore '{ore.Id}' has no band.");
            }

            foreach (var band in ore.Bands)
            {
                if (band.MinY >= band.MaxY || band.PeakY < band.MinY || band.PeakY > band.MaxY || band.MinY < v.MinY || band.MaxY > v.MaxY)
                {
                    problems.Add($"{File}: a band of the ore '{ore.Id}' needs minY below maxY inside the world and its peakY between them (they are {band.MinY}, {band.MaxY} and {band.PeakY}).");
                }

                if (band.Veins < 0 || band.Veins > 64)
                {
                    problems.Add($"{File}: a band of the ore '{ore.Id}' has veins outside 0 to 64 (it is {band.Veins}).");
                }
            }
        }
    }

    private static void Noise(List<string> problems, string name, int cell, int cellY, int octaves)
    {
        if (cell < 1 || cellY < 1 || octaves < 1 || octaves > 4 || (cell >> (octaves - 1)) < 1 || (cellY >> (octaves - 1)) < 1)
        {
            problems.Add($"{File}: {name} needs a positive cell and cellY that its 1 to 4 octaves can halve (they are {cell}, {cellY} and {octaves}).");
        }
    }

    private static void Thousandths(List<string> problems, string name, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            problems.Add($"{File}: {name} must be from {min} to {max} thousandths (it is {value}).");
        }
    }

    private static void Block(List<string> problems, string part, string name, bool solid)
    {
        var block = BlockDefinition.FromName(name);
        if (block is null)
        {
            problems.Add($"{File}: {part}: unknown block '{name}'.");
        }
        else if (block.Solid != solid)
        {
            problems.Add($"{File}: {part}: the block '{name}' must be {(solid ? "solid" : "a fluid that is not solid")}.");
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex Id();
}
