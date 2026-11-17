using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>
/// The checks of <c>game/underground.json</c> that need more than its shape (B11): every block name one the game has and solid (lava is the fluid that stops a body), the strata's dither ordered
/// and inside the world, the blobs' blocks distinct, a floor of at least one block that cannot rise into the lava, ores with unique kebab-case ids whose veins replace only solid rock (never an ore, bedrock or
/// lava), bands that peak inside their range and lie inside the world, cave lattices that divide a chunk, thresholds inside the noise's range, the water margin no larger than the entrances' water reach
/// and the bed clearance no smaller than the margin, and the lava level above the floor and under the dither. B11b adds the entrances' passage (wide enough to walk, steps of one block), the cave linings
/// (known faces and zones, rocks the game has) and the surface rock (cliffs, scree and stream beds). <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static partial class UndergroundValidator
{
    private const string File = UndergroundData.RelativePath;
    private static readonly int[] Divisors = [1, 2, 4, 8, 16];
    private static readonly string[] LiningFaces = ["floor", "wall", "ceiling", "any"];
    private static readonly string[] LiningZones = ["any", "damp", "wet"];

    /// <summary>Every problem of the underground, in a fixed order; empty when the values hold together.</summary>
    /// <param name="underground">The underground.</param>
    /// <param name="world">The world's numbers (the vertical range).</param>
    /// <param name="scene">The valley's layout, to check the sites, paths and props the <c>valley</c> section names, or <see langword="null"/> to skip that check.</param>
    public static IReadOnlyList<string> Validate(UndergroundData underground, WorldGenData world, Layout.SceneLayout? scene = null)
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
        if (underground.Linings is { } linings)
        {
            CheckLinings(linings, v, problems);
        }

        if (underground.SurfaceRock is { } surfaceRock)
        {
            CheckSurfaceRock(surfaceRock, problems);
        }

        if (underground.Valley is { } valley)
        {
            CheckValley(valley, scene, problems);
        }

        return problems;
    }

    private static void CheckValley(ValleySpec valley, Layout.SceneLayout? scene, List<string> problems)
    {
        Range(problems, "valley.surfaceDistance", valley.SurfaceDistance, 6, 32); // the caves' own surface cover is 6: the valley keeps at least that
        Range(problems, "valley.waterDistance", valley.WaterDistance, 3, 64);
        if (valley.WaterDistance < valley.SurfaceDistance)
        {
            problems.Add($"{File}: valley.waterDistance must not be under surfaceDistance (they are {valley.WaterDistance} and {valley.SurfaceDistance}).");
        }

        Range(problems, "valley.borderMargin", valley.BorderMargin, 0, 64);
        Range(problems, "valley.siteMargin", valley.SiteMargin, 0, 64);
        Range(problems, "valley.pathMargin", valley.PathMargin, 0, 64);
        Range(problems, "valley.propMargin", valley.PropMargin, 0, 64);
        Range(problems, "valley.villageMargin", valley.VillageMargin, 0, 64);
        Range(problems, "valley.passMargin", valley.PassMargin, 0, 64);
        if (scene is null)
        {
            return;
        }

        if (!scene.Sites.Any(site => site.Name == valley.VillageSite))
        {
            problems.Add($"{File}: valley.villageSite '{valley.VillageSite}' is not a site of the scene.");
        }

        foreach (var path in valley.PassPaths.Where(path => !scene.Paths.Any(p => p.Name == path)))
        {
            problems.Add($"{File}: valley.passPaths names '{path}', which is not a path of the scene.");
        }

        foreach (var prop in valley.PassProps.Where(prop => !scene.Props.Any(p => p.Name == prop)))
        {
            problems.Add($"{File}: valley.passProps names '{prop}', which is not a prop of the scene.");
        }
    }

    private static void Range(List<string> problems, string name, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            problems.Add($"{File}: {name} must be from {min} to {max} blocks (it is {value}).");
        }
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

        foreach (var blob in s.Blobs)
        {
            Block(problems, $"strata.blobs '{blob.Block}'", blob.Block, solid: true);
            Noise(problems, $"strata.blobs '{blob.Block}'", blob.Cell, blob.CellY, blob.Octaves);
            Thousandths(problems, $"strata.blobs '{blob.Block}' threshold", blob.Threshold, -1000, 1000);
            foreach (var rock in blob.In ?? [])
            {
                Block(problems, $"strata.blobs '{blob.Block}' in", rock, solid: true);
            }

            if (blob.MinY > blob.MaxY || blob.MaxY < v.MinY || blob.MinY > v.MaxY)
            {
                problems.Add($"{File}: strata.blobs '{blob.Block}' needs minY not above maxY, the band inside the world (they are {blob.MinY} and {blob.MaxY}).");
            }

            if (blob.In is { Count: 0 })
            {
                problems.Add($"{File}: strata.blobs '{blob.Block}' replaces no rock (in is empty).");
            }
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
        Range(problems, "entrances.cell", e.Cell, 32, 1024);
        Thousandths(problems, "entrances.chance", e.Chance, 0, 1000);
        if (e.Margin < 0 || (2 * e.Margin) >= e.Cell)
        {
            problems.Add($"{File}: entrances.margin must leave room inside the site (it is {e.Margin} of a cell of {e.Cell}).");
        }

        Range(problems, "entrances.attempts", e.Attempts, 1, 16);
        if (e.MinHeightAboveSea < 0 || e.MinHeightAboveSea > 64)
        {
            problems.Add($"{File}: entrances.minHeightAboveSea must be from 0 to 64 blocks (it is {e.MinHeightAboveSea}).");
        }

        if (e.WaterReach < d.Caves.WaterMargin + 1 || e.WaterReach > 24)
        {
            problems.Add($"{File}: entrances.waterReach must be from one over the caves' waterMargin to 24 columns (it is {e.WaterReach}).");
        }

        if (e.MinDrop < 8 || e.MinDrop > 64)
        {
            problems.Add($"{File}: entrances.minDrop must be from 8 to 64 blocks (it is {e.MinDrop}).");
        }

        if (e.MaxLength < e.MinDrop || e.MaxLength > 240)
        {
            problems.Add($"{File}: entrances.maxLength must be from minDrop ({e.MinDrop}) to 240 blocks (it is {e.MaxLength}).");
        }

        Range(problems, "entrances.landing", e.Landing, 1, 12);
        if (e.WidthMin < 2 || e.WidthMax < e.WidthMin || e.WidthMax > 8)
        {
            problems.Add($"{File}: entrances needs 2 <= widthMin <= widthMax <= 8 blocks, wide enough to walk (they are {e.WidthMin} and {e.WidthMax}).");
        }

        if (e.HeadroomMin < 3 || e.HeadroomMax < e.HeadroomMin || e.HeadroomMax > 10)
        {
            problems.Add($"{File}: entrances needs 3 <= headroomMin <= headroomMax <= 10 blocks (they are {e.HeadroomMin} and {e.HeadroomMax}).");
        }

        Range(problems, "entrances.plunge", e.Plunge, 0, 16);
        if (e.SegmentMin < 2 || e.SegmentMax < e.SegmentMin || e.SegmentMax > 40)
        {
            problems.Add($"{File}: entrances needs 2 <= segmentMin <= segmentMax <= 40 blocks (they are {e.SegmentMin} and {e.SegmentMax}).");
        }

        if (e.RunMax < 1 || e.RunMax > 6)
        {
            problems.Add($"{File}: entrances.runMax must be from 1 to 6 blocks along for each block down, so no step is over one block (it is {e.RunMax}).");
        }

        Thousandths(problems, "entrances.levelShare", e.LevelShare, 0, 900);
        Range(problems, "entrances.bend", e.Bend, 0, 16);
        if (e.BendSegmentMin < 2 || e.BendSegmentMax < e.BendSegmentMin || e.BendSegmentMax > 60)
        {
            problems.Add($"{File}: entrances needs 2 <= bendSegmentMin <= bendSegmentMax <= 60 blocks (they are {e.BendSegmentMin} and {e.BendSegmentMax}).");
        }

        Range(problems, "entrances.maxTurn", e.MaxTurn, 0, 32);
        Range(problems, "entrances.roughness", e.Roughness, 0, 1500);
        Range(problems, "entrances.roughnessCell", e.RoughnessCell, 1, 16);
        Thousandths(problems, "entrances.ledges", e.Ledges, 0, 1000);
        Range(problems, "entrances.funnel", e.Funnel, 0, 2000);
        Range(problems, "entrances.funnelLength", e.FunnelLength, 0, 24);
        Range(problems, "entrances.bowlDepth", e.BowlDepth, 0, 10);
        Range(problems, "entrances.bowlSlope", e.BowlSlope, 500, 4000);
        Weights(problems, "entrances.floorBlocks", e.FloorBlocks, true);
    }

    private static void CheckLinings(LiningSpec linings, VerticalRange v, List<string> problems)
    {
        Range(problems, "linings.wetReach", linings.WetReach, 0, 16);
        Range(problems, "linings.dampReach", linings.DampReach, 0, 16);
        foreach (var rule in linings.Rules)
        {
            Block(problems, $"linings '{rule.Block}'", rule.Block, solid: true);
            if (rule.In.Count == 0)
            {
                problems.Add($"{File}: the lining of '{rule.Block}' replaces no rock (in is empty).");
            }

            foreach (var rock in rule.In)
            {
                Block(problems, $"linings '{rule.Block}' in", rock, solid: true);
            }

            if (Array.IndexOf(LiningFaces, rule.Face) < 0)
            {
                problems.Add($"{File}: the lining of '{rule.Block}' has the face '{rule.Face}'; it must be floor, wall, ceiling or any.");
            }

            if (Array.IndexOf(LiningZones, rule.Zone) < 0)
            {
                problems.Add($"{File}: the lining of '{rule.Block}' has the zone '{rule.Zone}'; it must be any, damp or wet.");
            }

            Thousandths(problems, $"linings '{rule.Block}' chance", rule.Chance, 1, 1000);
            if (rule.Patch is { } patch)
            {
                Noise(problems, $"linings '{rule.Block}' patch", patch.Cell, patch.CellY, patch.Octaves);
                Thousandths(problems, $"linings '{rule.Block}' patch threshold", patch.Threshold, -1000, 1000);
            }

            if (rule.MinY > rule.MaxY || rule.MaxY < v.MinY || rule.MinY > v.MaxY)
            {
                problems.Add($"{File}: the lining of '{rule.Block}' needs minY not above maxY, the band inside the world (they are {rule.MinY} and {rule.MaxY}).");
            }
        }
    }

    private static void CheckSurfaceRock(SurfaceRockSpec s, List<string> problems)
    {
        Range(problems, "surfaceRock.cliff.drop", s.Cliff.Drop, 2, 32);
        Range(problems, "surfaceRock.cliff.bandThickness", s.Cliff.BandThickness, 1, 16);
        Range(problems, "surfaceRock.cliff.zoneCell", s.Cliff.ZoneCell, 4, 256);
        Range(problems, "surfaceRock.cliff.tilt", s.Cliff.Tilt, 0, 16);
        Weights(problems, "surfaceRock.cliff.bands", s.Cliff.Bands, true);
        Range(problems, "surfaceRock.scree.steepDrop", s.Scree.SteepDrop, 1, 32);
        Range(problems, "surfaceRock.scree.footReach", s.Scree.FootReach, 0, 8);
        if (s.Scree.SteepDrop >= s.Cliff.Drop)
        {
            problems.Add($"{File}: surfaceRock.scree.steepDrop must be under the cliff's drop (they are {s.Scree.SteepDrop} and {s.Cliff.Drop}).");
        }

        Thousandths(problems, "surfaceRock.streambed.minWeight", s.Streambed.MinWeight, 1, 1000);
        Scatter(problems, "surfaceRock.scree", s.Scree.Rules);
        Scatter(problems, "surfaceRock.streambed", s.Streambed.Rules);
    }

    private static void Scatter(List<string> problems, string part, IReadOnlyList<ScatterRule> rules)
    {
        foreach (var rule in rules)
        {
            Block(problems, $"{part} '{rule.Block}'", rule.Block, solid: true);
            Thousandths(problems, $"{part} '{rule.Block}' chance", rule.Chance, 1, 1000);
            if (rule.Patch is { } patch)
            {
                Noise(problems, $"{part} '{rule.Block}' patch", patch.Cell, patch.Cell, patch.Octaves);
                Thousandths(problems, $"{part} '{rule.Block}' patch threshold", patch.Threshold, -1000, 1000);
            }
        }
    }

    private static void Weights(List<string> problems, string part, IReadOnlyList<BlockWeight> weights, bool required)
    {
        if (required && weights.Count == 0)
        {
            problems.Add($"{File}: {part} lists no block.");
        }

        foreach (var weight in weights)
        {
            Block(problems, $"{part} '{weight.Block}'", weight.Block, solid: true);
            if (weight.Weight < 1 || weight.Weight > 1000)
            {
                problems.Add($"{File}: {part} '{weight.Block}' has the weight {weight.Weight}; it must be from 1 to 1000.");
            }
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
