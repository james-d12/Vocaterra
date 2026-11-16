using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// The checks of <c>game/world.json</c> that need more than its shape (B01): a vertical range of whole chunk layers with the sea inside it, noise with a positive cell that
/// the octaves can halve, splines with rising inputs inside the fields' ranges and outputs inside the world, ramps whose ends are ordered, a river bed and a lake bed below the sea,
/// and a ceiling for the ground that the soft cap sits under. <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static class WorldGenValidator
{
    private const string File = WorldGenData.RelativePath;

    /// <summary>Every problem of the world's numbers, in a fixed order; empty when the values hold together.</summary>
    /// <param name="world">The world's numbers.</param>
    public static IReadOnlyList<string> Validate(WorldGenData world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var problems = new List<string>();
        var v = world.Vertical;
        if (v.MinY >= v.MaxY || v.MinY % 16 != 0 || v.MaxY % 16 != 0)
        {
            problems.Add($"{File}: vertical needs minY below maxY, both a multiple of 16 (whole chunk layers) (they are {v.MinY} and {v.MaxY}).");
        }

        if (v.SeaLevel <= v.MinY || v.SeaLevel >= v.MaxY)
        {
            problems.Add($"{File}: the seaLevel {v.SeaLevel} must lie inside the vertical range.");
        }

        CheckClimate(world.Climate, problems);
        CheckHeight(world, problems);
        var a = world.Altitude;
        Thousandths(problems, "altitude.snowBelowTemperature", a.SnowBelowTemperature);
        Thousandths(problems, "altitude.treeLineBelowTemperature", a.TreeLineBelowTemperature);
        CheckRegionBlend(world.Blend, problems);
        return problems;
    }

    private static void CheckRegionBlend(Regions.RegionBlendData blend, List<string> problems)
    {
        if (blend.Margin < 8 || blend.Margin > 512)
        {
            problems.Add($"{File}: regionBlend.margin must be from 8 to 512 blocks (it is {blend.Margin}).");
        }

        if (blend.TreeClearance < 0 || blend.TreeClearance >= blend.Margin)
        {
            problems.Add($"{File}: regionBlend.treeClearance must be from 0 to below the margin (it is {blend.TreeClearance}).");
        }

        var bias = blend.LandBias;
        Thousandths(problems, "regionBlend.landBias.floor", bias.Floor);
        Thousandths(problems, "regionBlend.landBias.shoreFloor", bias.ShoreFloor);
        if (bias.ShoreFloor > bias.Floor || bias.Radius < blend.Margin || bias.Radius > 8192)
        {
            problems.Add($"{File}: regionBlend.landBias needs a shoreFloor no higher than the floor and a radius from the margin to 8192 blocks (they are {bias.ShoreFloor}, {bias.Floor} and {bias.Radius}).");
        }

        if (bias.Erosion is { } e)
        {
            Thousandths(problems, "regionBlend.landBias.erosion.near", e.Near);
            Thousandths(problems, "regionBlend.landBias.erosion.far", e.Far);
            if (e.Far > e.Near || e.FromRadius < 0 || e.ToRadius <= e.FromRadius || e.FadeRadius <= e.ToRadius || e.FadeRadius > 16384)
            {
                problems.Add($"{File}: regionBlend.landBias.erosion needs a far no higher than the near and radii rising from 0 to at most 16384 blocks (they are {e.Near}, {e.Far}, {e.FromRadius}, {e.ToRadius} and {e.FadeRadius}).");
            }
        }

        var smoothing = blend.Smoothing;
        if (smoothing.BlurRadius < 0 || smoothing.BlurRadius > 64 || smoothing.Ease < 1 || smoothing.Ease > blend.Margin)
        {
            problems.Add($"{File}: regionBlend.edgeSmoothing needs a blurRadius from 0 to 64 columns and an ease from 1 to the margin (they are {smoothing.BlurRadius} and {smoothing.Ease}).");
        }

        if (blend.Headwater.Distance < 1 || blend.Headwater.Distance > 4096)
        {
            problems.Add($"{File}: regionBlend.headwater.distance must be from 1 to 4096 blocks (it is {blend.Headwater.Distance}).");
        }

        foreach (var corridor in blend.ExitCorridors)
        {
            var length = (int)Mathematics.IntMath.ISqrt(((long)(corridor.ToX - corridor.FromX) * (corridor.ToX - corridor.FromX)) + ((long)(corridor.ToZ - corridor.FromZ) * (corridor.ToZ - corridor.FromZ)));
            if (length < 1 || length > 1024 || corridor.HalfWidth < 1 || corridor.HalfWidth > 16 || corridor.Shoulder < 1 || corridor.Shoulder > 64 || corridor.Fade < 0 || corridor.Fade > length)
            {
                problems.Add($"{File}: regionBlend.exitCorridors '{corridor.Id}' needs a length from 1 to 1024 blocks, a halfWidth from 1 to 16, a shoulder from 1 to 64 and a fade from 0 to its length.");
            }
        }
    }

    private static void CheckClimate(ClimateData climate, List<string> problems)
    {
        if (climate.LatticeBlocks < 1 || climate.LatticeBlocks > 16)
        {
            problems.Add($"{File}: climate.latticeBlocks must be from 1 to 16 (it is {climate.LatticeBlocks}).");
        }

        CheckNoise(problems, "continentalness", climate.Continentalness);
        CheckNoise(problems, "erosion", climate.Erosion);
        CheckNoise(problems, "ridges", climate.Ridges);
        CheckNoise(problems, "temperature", climate.Temperature);
        CheckNoise(problems, "humidity", climate.Humidity);
        CheckNoise(problems, "river", climate.River);
        if (climate.Continents is { } c)
        {
            if (c.Cell < 1000 || c.Cell > 16384 || c.Jitter < 0 || c.Jitter > 900 || c.SeaBlock < 1 || c.SeaBlock > 4 || c.SeaPerBlock < 0 || c.SeaPerBlock >= c.SeaBlock * c.SeaBlock)
            {
                problems.Add($"{File}: climate.continents needs a cell from 1000 to 16384 blocks, a jitter from 0 to 900, a seaBlock from 1 to 4 and a seaPerBlock below the block's cells (they are {c.Cell}, {c.Jitter}, {c.SeaBlock} and {c.SeaPerBlock}).");
            }

            if (c.ChannelMin < 1 || c.Channel < c.ChannelMin || c.Channel > c.Cell / 4 || c.WidthCell < 16 || c.WidthCell > c.Cell || c.CoastCell < 16 || c.CoastCell > c.Cell || c.CoastBlocks < 0 || c.CoastBlocks * 5 > c.CoastCell || c.ScaleBlocks < 100 || c.ScaleBlocks > 32768)
            {
                problems.Add($"{File}: climate.continents needs a channelMin of at least 1 (so the land is never joined across a channel) and a channel from it to a quarter of the cell, a widthCell and a coastCell from 16 to the cell, coastBlocks of at most a fifth of the coastCell (so the warp never folds) and a scaleBlocks from 100 to 32768 (they are {c.ChannelMin}, {c.Channel}, {c.WidthCell}, {c.CoastCell}, {c.CoastBlocks} and {c.ScaleBlocks}).");
            }
        }

        if (climate.LapsePer100Blocks < 0 || climate.LapsePer100Blocks > 2000)
        {
            problems.Add($"{File}: climate.lapsePer100Blocks must be from 0 to 2000 thousandths (it is {climate.LapsePer100Blocks}).");
        }
    }

    private static void CheckNoise(List<string> problems, string name, NoiseSpec noise)
    {
        if (noise.Cell < 1 || noise.Cell > 16384)
        {
            problems.Add($"{File}: climate.{name}.cell must be from 1 to 16384 blocks (it is {noise.Cell}).");
        }

        if (noise.Round < 0 || noise.Round > 500)
        {
            problems.Add($"{File}: climate.{name}.round must be from 0 to 500 thousandths (it is {noise.Round}).");
        }

        if (noise.Octaves < 1 || noise.Octaves > 8)
        {
            problems.Add($"{File}: climate.{name}.octaves must be from 1 to 8 (it is {noise.Octaves}).");
        }
        else if (noise.Cell >> (noise.Octaves - 1) < 1)
        {
            problems.Add($"{File}: climate.{name}: {noise.Octaves} octaves halve the cell {noise.Cell} below one block.");
        }

        if (noise.WarpBlocks < 0 || noise.WarpBlocks > 4096 || (noise.WarpBlocks > 0 && noise.WarpCell < 1))
        {
            problems.Add($"{File}: climate.{name}: warpBlocks must be from 0 to 4096 and, above 0, come with a warpCell of at least 1 (they are {noise.WarpBlocks} and {noise.WarpCell}).");
        }
    }

    private static void CheckHeight(WorldGenData world, List<string> problems)
    {
        var h = world.Height;
        var v = world.Vertical;
        CheckSpline(problems, "height.base", h.Base, -1000, 1000, v.MinY, v.MaxY);
        CheckSpline(problems, "height.rangeFactor", h.RangeFactor, -1000, 1000, 0, 1000);
        CheckSpline(problems, "height.peaks", h.Peaks, 0, 1000, 0, v.MaxY - v.MinY);
        var d = h.Detail;
        if (d.HillsCell < 1 || d.HillsOctaves < 1 || d.HillsOctaves > 8 || d.HillsCell >> (d.HillsOctaves - 1) < 1 || d.CalmAmplitude < 0 || d.RuggedAmplitude < 0)
        {
            problems.Add($"{File}: height.detail needs a hillsCell of at least 1 that its 1 to 8 octaves can halve, and amplitudes of 0 or more.");
        }

        if (h.Clamp >= v.MaxY || h.Clamp <= v.SeaLevel || h.SoftCapAbove <= v.SeaLevel || h.SoftCapAbove >= h.Clamp)
        {
            problems.Add($"{File}: height needs the sea level below softCapAbove below clamp below the ceiling (they are {v.SeaLevel}, {h.SoftCapAbove}, {h.Clamp}, {v.MaxY}).");
        }

        var t = h.Terraces;
        if (t.Step < 0 || t.Step > 32 || t.FromHeight >= t.ToHeight)
        {
            problems.Add($"{File}: height.terraces needs a step from 0 to 32 and a fromHeight below toHeight (they are {t.Step}, {t.FromHeight}, {t.ToHeight}).");
        }

        Ordered(problems, "height.terraces.ridgesFrom/ridgesTo", t.RidgesFrom, t.RidgesTo, 0, 1000);
        var c = h.Crags;
        if (c.Amplitude < 0 || c.Cell < 1)
        {
            problems.Add($"{File}: height.crags needs an amplitude of 0 or more and a cell of at least 1 (they are {c.Amplitude} and {c.Cell}).");
        }

        Ordered(problems, "height.crags.ridgesFrom/ridgesTo", c.RidgesFrom, c.RidgesTo, 0, 1000);
        if (c.HeightFrom >= c.HeightTo)
        {
            problems.Add($"{File}: height.crags needs heightFrom below heightTo (they are {c.HeightFrom} and {c.HeightTo}).");
        }

        var r = h.Rivers;
        if (r.Width < 1 || r.Width > 1000 || r.BedHeight >= v.SeaLevel || r.BedHeight <= v.MinY || r.FadeFromHeight >= r.FadeToHeight)
        {
            problems.Add($"{File}: height.rivers needs a width from 1 to 1000, a bedHeight below the sea level and above the floor, and a fadeFromHeight below fadeToHeight.");
        }

        Ordered(problems, "height.rivers.seaFadeFrom/seaFadeTo", r.SeaFadeFrom, r.SeaFadeTo, -1000, 1000);
        if (r.Valley is { } valley && (valley.From < v.SeaLevel || valley.From >= r.FadeToHeight || valley.WidthPer10Blocks < 0 || valley.WidthPer10Blocks > 200 || valley.DepthGrowth < 0 || valley.DepthGrowth > 1000 || valley.WaterFrom >= valley.WaterTo || valley.WaterFrom < -1000 || valley.WaterTo > 1001))
        {
            problems.Add($"{File}: height.rivers.valley needs a from between the sea level and fadeToHeight, a widthPer10Blocks from 0 to 200 and a depthGrowth from 0 to 1000 and a waterFrom below a waterTo, both from -1000 to 1001 (they are {valley.From}, {valley.WidthPer10Blocks} and {valley.DepthGrowth}).");
        }

        if (h.PeakVariation is { } pv && (pv.Cell < 1 || pv.Octaves < 1 || pv.Octaves > 8 || pv.Cell >> (pv.Octaves - 1) < 1 || pv.MinShare < 0 || pv.MinShare > 1000))
        {
            problems.Add($"{File}: height.peakVariation needs a cell its 1 to 8 octaves can halve and a minShare from 0 to 1000 (they are {pv.Cell}, {pv.Octaves} and {pv.MinShare}).");
        }

        var l = h.Lakes;
        if (l.Cell < 1 || l.Octaves < 1 || l.Octaves > 8 || l.Cell >> (l.Octaves - 1) < 1 || l.Softness < 1 || l.BedHeight >= v.SeaLevel || l.BedHeight <= v.MinY || l.HeightFrom >= l.HeightTo)
        {
            problems.Add($"{File}: height.lakes needs a cell its octaves can halve, a softness of at least 1, a bedHeight below the sea level and above the floor, and a heightFrom below heightTo.");
        }

        Thousandths(problems, "height.lakes.threshold", l.Threshold);
        Ordered(problems, "height.lakes.calmFrom/calmTo", l.CalmFrom, l.CalmTo, -1000, 1000);
        Ordered(problems, "height.lakes.inlandFrom/inlandTo", l.InlandFrom, l.InlandTo, -1000, 1000);
        if (h.CoastData is { } coast)
        {
            Ordered(problems, "height.coast.peaksFrom/peaksTo", coast.PeaksFrom, coast.PeaksTo, -1000, 1000);
            Ordered(problems, "height.coast.hillsFrom/hillsTo", coast.HillsFrom, coast.HillsTo, -1000, 1000);
            if (coast.FlatFrom >= coast.FlatTo || coast.FlatFrom < 0)
            {
                problems.Add($"{File}: height.coast.flatFrom must be 0 or more and below flatTo (they are {coast.FlatFrom} and {coast.FlatTo}).");
            }

            if (coast.HillsFloor < 0 || coast.HillsFloor > 1000)
            {
                problems.Add($"{File}: height.coast.hillsFloor must be from 0 to 1000 thousandths (it is {coast.HillsFloor}).");
            }
        }
    }

    private static void CheckSpline(List<string> problems, string name, IReadOnlyList<SplinePoint> points, int minX, int maxX, int minY, int maxY)
    {
        if (points.Count < 2)
        {
            problems.Add($"{File}: {name} needs at least two control points.");
            return;
        }

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (i > 0 && p.X <= points[i - 1].X)
            {
                problems.Add($"{File}: {name}: the inputs must rise ({points[i - 1].X} is followed by {p.X}).");
            }

            if (p.X < minX || p.X > maxX)
            {
                problems.Add($"{File}: {name}: the input {p.X} must be from {minX} to {maxX}.");
            }

            if (p.Y < minY || p.Y > maxY)
            {
                problems.Add($"{File}: {name}: the output {p.Y} must be from {minY} to {maxY}.");
            }
        }
    }

    private static void Ordered(List<string> problems, string name, int from, int to, int min, int max)
    {
        if (from >= to || from < min || to > max)
        {
            problems.Add($"{File}: {name} must be two values rising from {min} to {max} (they are {from} and {to}).");
        }
    }

    private static void Thousandths(List<string> problems, string name, int value)
    {
        if (value < -1000 || value > 1000)
        {
            problems.Add($"{File}: {name} must be from -1000 to 1000 thousandths (it is {value}).");
        }
    }
}
