using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The tall world's ground (B01, biomes plan section 3.4): <c>height = base(C) + factor(E) x peaks(R) + detail</c>, from three data splines over the climate's continentalness,
/// erosion and ridges, then terraces on the steep middle slopes, crags on the highest ridges, a soft cap above 280 at half rate, a clamp, rivers carved where the river field is near
/// zero and lakes pulled into low calm basins. Rivers and lakes hold water at a level of their own (round 3, <see cref="WaterSpec"/>): the surface steps down a valley with the smooth ground, the bed is a depth under it. The splines are of the fields, never of a biome, so a border between biomes cannot make a cliff. Everything is fixed point (heights in
/// sixteenths of a block inside) and pure: the seed only keys the detail noise.
/// </summary>
public sealed class HeightModel : IHeightModel, IHeadwaterHeightModel
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

    /// <summary>Creates the height model of one world seed.</summary>
    /// <param name="world">The world's numbers.</param>
    /// <param name="seed">The world seed.</param>
    public HeightModel(WorldGenData world, ulong seed)
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
    }

    /// <inheritdoc />
    public ColumnShape Shape(int x, int z, in ClimateSample c) => Shape(x, z, c, One);

    /// <inheritdoc />
    ColumnShape IHeadwaterHeightModel.Shape(int x, int z, in ClimateSample c, int water) => Shape(x, z, c, water);

    // The ground with a share of the water (the headwaters: One is the free world's rivers and lakes).
    private ColumnShape Shape(int x, int z, in ClimateSample c, int share)
    {
        var coast = _data.Coast;
        var factor = _factor.Evaluate(c.Erosion);
        var inland = IntMath.Ramp(c.Continentalness, FieldScale.FromThousandths(coast.PeaksFrom), FieldScale.FromThousandths(coast.PeaksTo));
        var added16 = (int)((long)(factor * (long)inland / One) * Summit(x, z, _peaks.Evaluate(c.Ridges)) / One); // what the ranges' peaks add to the ground
        var h16 = _base.Evaluate(c.Continentalness) + added16;
        var smooth16 = h16; // the ground without the hills, terraces and crags: the water's level follows it

        var detail = _data.Detail;
        var amplitude = IntMath.Lerp(detail.CalmAmplitude, detail.RuggedAmplitude, factor);
        var calm = IntMath.Ramp(c.Continentalness, FieldScale.FromThousandths(coast.HillsFrom), FieldScale.FromThousandths(coast.HillsTo));
        amplitude = (int)((long)amplitude * IntMath.Lerp(FieldScale.FromThousandths(coast.HillsFloor), One, calm) / One);
        amplitude = (int)((long)amplitude * IntMath.Ramp(h16, coast.FlatFrom * 16, coast.FlatTo * 16) / One); // none on the low ground just over the sea
        if (amplitude > 0)
        {
            h16 += (int)((long)_hills.Fbm(x, z, detail.HillsCell, detail.HillsOctaves) * amplitude * 16 / One);
        }

        h16 = Terrace(h16, c.Ridges, factor);
        h16 = Crag(h16, x, z, c.Ridges);
        var cap = _data.SoftCapAbove * 16;
        if (h16 > cap)
        {
            h16 = cap + ((h16 - cap) / 2);
        }

        h16 = Math.Min(h16, (_data.Clamp * 16) + 15);
        var natural = h16;

        var level = SurfaceLevel(smooth16, amplitude);
        var riverWeight = RiverWeight(c, smooth16, share, added16); // by the ground without the hills: a shore and a bank do not follow every dip
        var lakeWeight = LakeWeight(x, z, c, smooth16, share);
        var carve = Math.Max(riverWeight, lakeWeight);
        if (carve > 0)
        {
            h16 = IntMath.Lerp(h16, smooth16, Math.Min(One, carve * 3)); // the hills fade out where water lies (all gone by a third of the carve), so a shore does not follow every dip of the ground (no slivers, no puddles on a lake's fringe)
        }

        if (riverWeight > 0)
        {
            h16 = IntMath.Lerp(h16, Math.Min(h16, (level - _data.Rivers.Depth) * 16), riverWeight);
        }

        if (lakeWeight > 0)
        {
            h16 = IntMath.Lerp(h16, Math.Min(h16, (level - _data.Lakes.Depth) * 16), lakeWeight);
        }

        var height = Math.Clamp(IntMath.DivDown(h16, 16), _minY + 1, _data.Clamp);
        var surface = carve > 0 ? level : _seaLevel; // the water of a river or a lake stands at its own level, any other at the sea's
        var water = height < surface && (carve >= One / 4 || level <= _seaLevel || carve == 0); // on its fringe a river or a lake holds no water: a dip of the ground there would be a puddle
        return new ColumnShape(height, Math.Max(IntMath.DivDown(natural, 16), height), water, water ? surface : -1, riverWeight, lakeWeight, surface);
    }

    // The surface of the water a river or lake would have in a column (blocks): the smooth ground less what the hills, the clearance and the valley take, in steps above the sea, never under it.
    private int SurfaceLevel(int smooth16, int amplitude)
    {
        var water = _data.Water;
        var under = (amplitude + water.Clearance) * 16;
        if (_data.Rivers.Valley is { } valley)
        {
            under += (int)((long)Math.Max(0, smooth16 - (valley.From * 16)) * valley.DepthGrowth / 1000);
        }

        var raw = IntMath.DivDown(smooth16 - under, 16);
        return raw <= _seaLevel ? _seaLevel : _seaLevel + (IntMath.DivDown(raw - _seaLevel, water.Step) * water.Step);
    }

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

    // 1 in the middle of a river, 0 beyond its width (the profile squared), faded out on high land and towards the sea.
    // Near a region the river's width and its depth are scaled by the water's share: a narrow, shallow stream whose bed rises to the ground.
    private int RiverWeight(in ClimateSample c, int natural16, int water, int peaks16)
    {
        var river = _data.Rivers;
        var wide = FieldScale.FromThousandths(river.Width);
        if (river.Valley is { } valley)
        {
            wide += FieldScale.FromThousandths((int)((long)Math.Max(0, natural16 - (valley.From * 16)) * valley.WidthPer10Blocks / 160)); // per 10 blocks of ground: natural16 / 16 / 10
        }

        var width = (int)((long)wide * water / One);
        if (width == 0)
        {
            return 0;
        }

        var core = One - IntMath.Ramp(Math.Abs(c.River), 0, width);
        if (core == 0)
        {
            return 0;
        }

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
}
