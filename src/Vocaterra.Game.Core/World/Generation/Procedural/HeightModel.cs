using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Drainage;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The tall world's ground (B01, biomes plan section 3.4): <c>height = base(C) + factor(E) x peaks(R) + detail</c>, from three data splines over the climate's continentalness,
/// erosion and ridges, then terraces on the steep middle slopes, crags on the highest ridges, a soft cap above 280 at half rate, a clamp, rivers carved where the river field is near
/// zero and lakes pulled into low calm basins. Rivers and lakes hold water at a level of their own (round 3, <see cref="WaterSpec"/>): the surface steps down a valley with the smooth ground, the bed is a depth under it. The splines are of the fields, never of a biome, so a border between biomes cannot make a cliff. Everything is fixed point (heights in
/// sixteenths of a block inside) and pure: the seed only keys the detail noise.
/// </summary>
public sealed class HeightModel : IHeightModel, IHeadwaterHeightModel, IDrainageSurface
{
    private const int One = IntMath.One;

    private readonly HeightData _data;
    private readonly int _minY;
    private readonly int _seaLevel;
    private readonly Spline _base;
    private readonly Spline _factor;
    private readonly Spline _peaks;
    private readonly NoiseStack _hills;
    private readonly NoiseStack _crags;
    private readonly NoiseStack _lakes;
    private readonly NoiseStack _summits;
    private readonly NoiseStack _relief;
    private readonly IClimateSampler _climate;
    private readonly Func<int, int, bool>? _excluded;
    private readonly DrainageNetwork? _network;

    /// <summary>Creates the height model of one world seed, over the free world's climate.</summary>
    /// <param name="world">The world's numbers.</param>
    /// <param name="seed">The world seed.</param>
    public HeightModel(WorldGenData world, ulong seed)
        : this(world, seed, null, null)
    {
    }

    /// <summary>Creates the height model of one world seed whose drainage network reads the given climate (the blended one round a region) and keeps clear of the places the callback names.</summary>
    /// <param name="world">The world's numbers.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="climate">The climate the drainage network reads at its nodes (the free world's when <see langword="null"/>): it must be the climate the columns are shaped with.</param>
    /// <param name="excluded">Whether no river and no lake may be at a block (the margin of a region and its clearance), or <see langword="null"/> where there is none.</param>
    internal HeightModel(WorldGenData world, ulong seed, IClimateSampler? climate, Func<int, int, bool>? excluded)
    {
        ArgumentNullException.ThrowIfNull(world);
        _data = world.Height;
        _minY = world.Vertical.MinY;
        _seaLevel = world.Vertical.SeaLevel;
        _base = new Spline([.. _data.Base.Select(static p => new SplinePoint(FieldScale.FromThousandths(p.X), p.Y * 16))]);
        _factor = new Spline([.. _data.RangeFactor.Select(static p => new SplinePoint(FieldScale.FromThousandths(p.X), FieldScale.FromThousandths(p.Y)))]);
        _peaks = new Spline([.. _data.Peaks.Select(static p => new SplinePoint(FieldScale.FromThousandths(p.X), p.Y * 16))]);
        _hills = new NoiseStack(seed, "surface/hills");
        _crags = new NoiseStack(seed, "surface/crags");
        _lakes = new NoiseStack(seed, "surface/lakes");
        _summits = new NoiseStack(seed, "surface/summits");
        _relief = new NoiseStack(seed, "drainage/relief");
        _climate = climate ?? new ClimateSampler(world, seed);
        _excluded = excluded;
        _network = _data.DrainageData is { } drainage ? new DrainageNetwork(drainage, _seaLevel, seed, _data.Rivers.Width, this) : null;
    }

    /// <inheritdoc />
    public ColumnShape Shape(int x, int z, in ClimateSample c) => Shape(x, z, c, One);

    /// <inheritdoc />
    ColumnShape IHeadwaterHeightModel.Shape(int x, int z, in ClimateSample c, int water) => Shape(x, z, c, water);

    // The ground with a share of the water (the headwaters: One is the free world's rivers and lakes).
    private ColumnShape Shape(int x, int z, in ClimateSample c, int share)
    {
        var g = SmoothGround(x, z, c);
        var smooth16 = g.Smooth16; // the ground without the hills, terraces and crags: the water's level follows it
        var h16 = smooth16;
        if (g.Amplitude > 0)
        {
            h16 += (int)((long)_hills.Fbm(x, z, _data.Detail.HillsCell, _data.Detail.HillsOctaves) * g.Amplitude * 16 / One);
        }

        h16 = Terrace(h16, c.Ridges, g.Factor);
        h16 = Crag(h16, x, z, c.Ridges);
        var cap = _data.SoftCapAbove * 16;
        if (h16 > cap)
        {
            h16 = cap + ((h16 - cap) / 2);
        }

        h16 = Math.Min(h16, (_data.Clamp * 16) + 15);
        var natural = h16;

        // The water the drainage network gives the column (B02c): a river along its line, at the level of the river there, and a lake where a basin is filled, at its outlet's level.
        var riverWeight = 0;
        var riverLevel = _seaLevel;
        var riverDepth = _data.Rivers.Depth;
        var lakeWeight = 0;
        var lakeLevel = _seaLevel;
        if (_network is not null)
        {
            if (_network.River(x, z, ValleyWidth16(smooth16), share, out var hit))
            {
                riverWeight = RiverWeight(hit.Core, c, smooth16, share, g.Added16); // by the ground without the hills: a shore and a bank do not follow every dip
                if (riverWeight > 0)
                {
                    riverLevel = Quantize(hit.Level16);
                    riverDepth = IntMath.Lerp(_data.Rivers.Depth, _data.DrainageData!.DepthMax, Math.Min(One, hit.Size));
                }
            }

            var basin = _data.DrainageData!.LakeBasin;
            if (basin > 0 && _network.Lake(x, z, out var lake))
            {
                // The lake is where the basin the drainage filled is deep enough, a smooth shore at the contour of the dented land: full once the water is half the dent deep.
                lakeWeight = (int)((long)IntMath.Ramp(lake.Depth16, 0, basin * 8) * share / One);
                lakeLevel = Quantize(lake.Level16);
            }
        }

        var carve = Math.Max(riverWeight, lakeWeight);
        var level = lakeWeight > riverWeight ? lakeLevel : riverLevel;
        if (carve > 0)
        {
            h16 = IntMath.Lerp(h16, smooth16, Math.Min(One, carve * 3)); // the hills fade out where water lies (all gone by a third of the carve), so a shore does not follow every dip of the ground (no slivers, no puddles on a lake's fringe)
        }

        if (riverWeight > 0)
        {
            h16 = IntMath.Lerp(h16, Math.Min(h16, (riverLevel - riverDepth) * 16), riverWeight);
        }

        if (lakeWeight > 0)
        {
            h16 = IntMath.Lerp(h16, Math.Min(h16, (lakeLevel - _data.Lakes.Depth) * 16), lakeWeight);
        }

        var height = Math.Clamp(IntMath.DivDown(h16, 16), _minY + 1, _data.Clamp);
        var surface = carve > 0 ? level : _seaLevel; // the water of a river or a lake stands at its own level, any other at the sea's
        var water = height < surface && (carve >= One / 4 || level <= _seaLevel || carve == 0); // on its fringe a river or a lake holds no water: a dip of the ground there would be a puddle
        return new ColumnShape(height, Math.Max(IntMath.DivDown(natural, 16), height), water, water ? surface : -1, riverWeight, lakeWeight, surface);
    }

    // The ground before the hills, terraces and crags: the smooth ground (the splines of the fields plus the ranges' peaks), the hills' amplitude, what the peaks added and the erosion's factor.
    private Ground SmoothGround(int x, int z, in ClimateSample c)
    {
        var coast = _data.Coast;
        var factor = _factor.Evaluate(c.Erosion);
        var inland = IntMath.Ramp(c.Continentalness, FieldScale.FromThousandths(coast.PeaksFrom), FieldScale.FromThousandths(coast.PeaksTo));
        var added16 = (int)((long)(factor * (long)inland / One) * Summit(x, z, _peaks.Evaluate(c.Ridges)) / One); // what the ranges' peaks add to the ground
        var smooth16 = _base.Evaluate(c.Continentalness) + added16;

        var detail = _data.Detail;
        var amplitude = IntMath.Lerp(detail.CalmAmplitude, detail.RuggedAmplitude, factor);
        var calm = IntMath.Ramp(c.Continentalness, FieldScale.FromThousandths(coast.HillsFrom), FieldScale.FromThousandths(coast.HillsTo));
        amplitude = (int)((long)amplitude * IntMath.Lerp(FieldScale.FromThousandths(coast.HillsFloor), One, calm) / One);
        amplitude = (int)((long)amplitude * IntMath.Ramp(smooth16, coast.FlatFrom * 16, coast.FlatTo * 16) / One); // none on the low ground just over the sea
        return new Ground(smooth16, added16, factor, amplitude);
    }

    private readonly record struct Ground(int Smooth16, int Added16, int Factor, int Amplitude);

    /// <inheritdoc />
    DrainageNode IDrainageSurface.NodeAt(int x, int z)
    {
        var c = _climate.AtLatticePoint(x, z);
        var g = SmoothGround(x, z, c);
        var level = LevelOf(g.Smooth16, g.Amplitude);
        if (level <= _seaLevel * 16)
        {
            return new DrainageNode(_seaLevel * 16, 0, true, false);
        }

        var drainage = _data.DrainageData!;
        var dent = drainage.LakeBasin > 0 ? (int)((long)LakeWeight(x, z, c, g.Smooth16, One) * drainage.LakeBasin * 16 / One) : 0;
        var relief = drainage.Relief > 0 ? (int)((long)_relief.Fbm(x, z, drainage.ReliefCell, 2) * drainage.Relief * 16 / One) : 0;
        return new DrainageNode(level, level + relief - dent, false, _excluded?.Invoke(x, z) ?? false);
    }

    // The surface of the water a river or lake would have, in sixteenths of a block: the smooth ground less what the hills, the clearance and the valley take.
    private int LevelOf(int smooth16, int amplitude)
    {
        var under = (amplitude + _data.Water.Clearance) * 16;
        if (_data.Rivers.Valley is { } valley)
        {
            under += (int)((long)Math.Max(0, smooth16 - (valley.From * 16)) * valley.DepthGrowth / 1000);
        }

        return smooth16 - under;
    }

    // A water level in blocks: in steps above the sea, never under it.
    private int Quantize(int level16)
    {
        var raw = IntMath.DivDown(level16, 16);
        return raw <= _seaLevel ? _seaLevel : _seaLevel + (IntMath.DivDown(raw - _seaLevel, _data.Water.Step) * _data.Water.Step);
    }

    // How much wider the valley makes a river on high ground, in sixteenths of a block.
    private int ValleyWidth16(int smooth16) => _data.Rivers.Valley is { } valley ? (int)((long)Math.Max(0, smooth16 - (valley.From * 16)) * valley.WidthPer100Blocks / 100) : 0;

    // The peaks of a column scaled along the range: the summits stand where the noise is high and the saddles where it is low.
    private int Summit(int x, int z, int peaks16)
    {
        if (_data.PeakVariation is not { } v || peaks16 == 0)
        {
            return peaks16;
        }

        var t = Math.Clamp((One / 2) + (_summits.Fbm(x, z, v.Cell, v.Octaves) * 3 / 2), 0, One); // spread over the noise's usual range, so there are summits and saddles and not one middling height
        var share = FieldScale.FromThousandths(v.MinShare);
        return (int)((long)peaks16 * IntMath.Lerp(share, One, IntMath.Smooth(t)) / One);
    }

    // Shelves on the steep middle slopes: from the foot height up, only where the land is rugged, and fading out towards the crest (which has crags instead).
    private int Terrace(int h16, int ridges, int factor)
    {
        var t = _data.Terraces;
        if (t.Step <= 1)
        {
            return h16;
        }

        var strength = IntMath.Ramp(h16, t.FromHeight * 16, t.ToHeight * 16);
        strength = strength * (One - IntMath.Ramp(ridges, FieldScale.FromThousandths(t.RidgesFrom), FieldScale.FromThousandths(t.RidgesTo))) / One;
        strength = strength * IntMath.Ramp(factor, 1000, 2800) / One;
        return Terracing.Apply(h16, t.Step, strength);
    }

    // Jagged spikes, only on the highest ridges: the ridged noise is added on top of the ground.
    private int Crag(int h16, int x, int z, int ridges)
    {
        var crag = _data.Crags;
        if (crag.Amplitude <= 0)
        {
            return h16;
        }

        var weight = IntMath.Ramp(ridges, FieldScale.FromThousandths(crag.RidgesFrom), FieldScale.FromThousandths(crag.RidgesTo));
        weight = weight * IntMath.Ramp(h16, crag.HeightFrom * 16, crag.HeightTo * 16) / One;
        if (weight == 0)
        {
            return h16;
        }

        var spike = (long)_crags.Ridged(x, z, crag.Cell, 2) * crag.Amplitude * 16 / One;
        return h16 + (int)(spike * weight / One);
    }

    // 1 in the middle of a river, 0 beyond its width (the profile squared), faded out on high land and towards the sea. The profile across the river is the drainage network's.
    // Near a region the river's width and its depth are scaled by the water's share: a narrow, shallow stream whose bed rises to the ground.
    private int RiverWeight(int core, in ClimateSample c, int natural16, int water, int peaks16)
    {
        var river = _data.Rivers;
        core = core * core / One; // the square narrows the water and leaves the banks a long gentle shoulder
        var weight = core * (One - IntMath.Ramp(natural16, river.FadeFromHeight * 16, river.FadeToHeight * 16)) / One;
        weight = weight * IntMath.Ramp(c.Continentalness, FieldScale.FromThousandths(river.SeaFadeFrom), FieldScale.FromThousandths(river.SeaFadeTo)) / One;
        weight = weight * (One - IntMath.Ramp(peaks16, river.PeakFadeFrom * 16, river.PeakFadeTo * 16)) / One; // a river does not cross a crest
        return (int)((long)weight * water / One);
    }

    // A lake where the lake field is high, the land inland, low and calm (the land as it was before the rivers).
    // Near a region the lake shrinks (its threshold rises towards the top of the field) and shallows by the water's share, so it is a pond with a round shore and not a lake cut by a line.
    private int LakeWeight(int x, int z, in ClimateSample c, int natural16, int water)
    {
        var lake = _data.Lakes;
        var calm = IntMath.Ramp(c.Erosion, FieldScale.FromThousandths(lake.CalmFrom), FieldScale.FromThousandths(lake.CalmTo));
        var low = One - IntMath.Ramp(natural16, lake.HeightFrom * 16, lake.HeightTo * 16); // by the land before any river carved it, so a river on high ground cannot open a deep pit
        var inland = IntMath.Ramp(c.Continentalness, FieldScale.FromThousandths(lake.InlandFrom), FieldScale.FromThousandths(lake.InlandTo));
        var weight = (long)calm * low / One * inland / One;
        if (weight == 0)
        {
            return 0;
        }

        var threshold = FieldScale.FromThousandths(lake.Threshold) + (int)((long)One * (One - water) / One);
        var basin = IntMath.Ramp(_lakes.Fbm(x, z, lake.Cell, lake.Octaves), threshold, threshold + FieldScale.FromThousandths(lake.Softness));
        var lake16 = (int)(weight * basin / One); // 0 to One: the lake field and the land's fitness for a lake
        lake16 = IntMath.Smooth(IntMath.Ramp(lake16, One * 3 / 10, One * 7 / 10)); // the lake is deep or it is not there: a lake whose fitness stops at 0.7 would sit with its bed at its own level and flicker between water and shore
        return (int)((long)lake16 * water / One);
    }

    /// <summary>Whether this model has a drainage network (the world's data has a <c>drainage</c> section): without one the world has no river and no lake.</summary>
    public bool HasDrainage => _network is not null;

    /// <summary>The river through the node nearest a block, from its source to the sea or the lake it ends in, one point to a node (B02c; for the preview tool and the tests). Empty when no river is within a few nodes of the block.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="maxNodes">The most nodes to follow each way.</param>
    public IReadOnlyList<RiverPoint> TraceRiver(int x, int z, int maxNodes)
    {
        if (_network is not { } net)
        {
            return [];
        }

        var (ci, cj) = net.NodeNear(x, z);
        var best = (I: 0, J: 0, Distance: long.MaxValue);
        for (var j = cj - 6; j <= cj + 6; j++)
        {
            for (var i = ci - 6; i <= ci + 6; i++)
            {
                var info = net.Info(i, j);
                if (!info.River || info.Lake)
                {
                    continue;
                }

                var (px, pz) = net.Position(i, j);
                var d = ((long)(px - x) * (px - x)) + ((long)(pz - z) * (pz - z));
                if (d < best.Distance)
                {
                    best = (i, j, d);
                }
            }
        }

        if (best.Distance == long.MaxValue)
        {
            return [];
        }

        var up = new List<RiverPoint>();
        var (ui, uj) = (best.I, best.J);
        for (var n = 0; n < maxNodes; n++)
        {
            (int I, int J, int Area)? upstream = null;
            for (var d = 0; d < 8; d++)
            {
                var (ni, nj) = (ui + Dx(d), uj + Dz(d));
                var info = net.Info(ni, nj);
                if (info.River && info.Next >= 0 && ni + Dx(info.Next) == ui && nj + Dz(info.Next) == uj && (upstream is null || info.Area > upstream.Value.Area))
                {
                    upstream = (ni, nj, info.Area);
                }
            }

            if (upstream is not { } found)
            {
                break;
            }

            (ui, uj) = (found.I, found.J);
            up.Add(Point(net, ui, uj));
        }

        up.Reverse();
        var path = up;
        var (ci2, cj2) = (best.I, best.J);
        for (var n = 0; n < maxNodes; n++)
        {
            var info = net.Info(ci2, cj2);
            path.Add(Point(net, ci2, cj2));
            if (info.Sea || info.Next < 0 || info.Excluded)
            {
                break;
            }

            (ci2, cj2) = (ci2 + Dx(info.Next), cj2 + Dz(info.Next));
        }

        return path;
    }

    /// <summary>The reaches of river with a node in a box of blocks: from each river node (not under a lake) to the node the water runs to (B02c; for the preview tool and the tests).</summary>
    /// <param name="minX">The box's west edge.</param>
    /// <param name="minZ">The box's north edge.</param>
    /// <param name="maxX">The box's east edge.</param>
    /// <param name="maxZ">The box's south edge.</param>
    public IReadOnlyList<RiverReach> Reaches(int minX, int minZ, int maxX, int maxZ)
    {
        if (_network is not { } net)
        {
            return [];
        }

        var reaches = new List<RiverReach>();
        var (i0, j0) = net.NodeNear(minX, minZ);
        var (i1, j1) = net.NodeNear(maxX, maxZ);
        for (var j = j0; j <= j1; j++)
        {
            for (var i = i0; i <= i1; i++)
            {
                var info = net.Info(i, j);
                if (!info.River || info.Lake || info.Next < 0)
                {
                    continue;
                }

                var (x0, z0) = net.Position(i, j);
                var (x1, z1) = net.Position(i + Dx(info.Next), j + Dz(info.Next));
                var to = net.Info(i + Dx(info.Next), j + Dz(info.Next));
                reaches.Add(new RiverReach(x0, z0, x1, z1, info.Area, Quantize(info.Level16), Quantize(to.Level16), to.Lake, to.Sea));
            }
        }

        return reaches;
    }

    private RiverPoint Point(DrainageNetwork net, int i, int j)
    {
        var info = net.Info(i, j);
        var (x, z) = net.Position(i, j);
        return new RiverPoint(x, z, Quantize(info.Level16), info.Area, info.Lake, info.Sea);
    }

    private static int Dx(int direction) => direction switch { 0 or 1 or 7 => 1, 3 or 4 or 5 => -1, _ => 0 };

    private static int Dz(int direction) => direction switch { 1 or 2 or 3 => 1, 5 or 6 or 7 => -1, _ => 0 };
}

/// <summary>One node of a river's line (B02c): where it stands, the level of its water (blocks), the nodes that drain through it and whether it lies under a lake or is the sea.</summary>
/// <param name="X">The node's x.</param>
/// <param name="Z">The node's z.</param>
/// <param name="Level">The surface of the water at the node, in blocks (never higher than at the node upstream).</param>
/// <param name="Area">The nodes that drain through it.</param>
/// <param name="Lake">Whether the node lies under a lake.</param>
/// <param name="Sea">Whether the node is the sea.</param>
public readonly record struct RiverPoint(int X, int Z, int Level, int Area, bool Lake, bool Sea);

/// <summary>One reach of a river (B02c): the line from a node to the node the water runs to, the level of the water at each end (blocks) and what it runs into.</summary>
/// <param name="FromX">The upstream node's x.</param>
/// <param name="FromZ">The upstream node's z.</param>
/// <param name="ToX">The downstream node's x.</param>
/// <param name="ToZ">The downstream node's z.</param>
/// <param name="Area">The nodes that drain through the upstream node.</param>
/// <param name="FromLevel">The water's level at the upstream node, in blocks.</param>
/// <param name="ToLevel">The water's level at the downstream node, in blocks.</param>
/// <param name="IntoLake">Whether the downstream node lies under a lake.</param>
/// <param name="IntoSea">Whether the downstream node is the sea.</param>
public readonly record struct RiverReach(int FromX, int FromZ, int ToX, int ToZ, int Area, int FromLevel, int ToLevel, bool IntoLake, bool IntoSea);
