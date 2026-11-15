using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The tall world's ground (B01, biomes plan section 3.4): <c>height = base(C) + factor(E) x peaks(R) + detail</c>, from three data splines over the climate's continentalness,
/// erosion and ridges, then terraces on the steep middle slopes, crags on the highest ridges, a soft cap above 280 at half rate, a clamp, rivers carved where the river field is near
/// zero and lakes pulled into low calm basins. The splines are of the fields, never of a biome, so a border between biomes cannot make a cliff. Everything is fixed point (heights in
/// sixteenths of a block inside) and pure: the seed only keys the detail noise.
/// </summary>
public sealed class HeightModel : IHeightModel
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
    }

    /// <inheritdoc />
    public ColumnShape Shape(int x, int z, in ClimateSample c)
    {
        var factor = _factor.Evaluate(c.Erosion);
        var h16 = _base.Evaluate(c.Continentalness) + (int)((long)factor * _peaks.Evaluate(c.Ridges) / One);

        var detail = _data.Detail;
        var amplitude = IntMath.Lerp(detail.CalmAmplitude, detail.RuggedAmplitude, factor);
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

        var riverWeight = RiverWeight(c, natural);
        if (riverWeight > 0)
        {
            h16 = IntMath.Lerp(h16, Math.Min(h16, _data.Rivers.BedHeight * 16), riverWeight);
        }

        var lakeWeight = LakeWeight(x, z, c, h16);
        if (lakeWeight > 0)
        {
            h16 = IntMath.Lerp(h16, Math.Min(h16, _data.Lakes.BedHeight * 16), lakeWeight);
        }

        var height = Math.Clamp(IntMath.DivDown(h16, 16), _minY + 1, _data.Clamp);
        var water = height < _seaLevel;
        return new ColumnShape(height, Math.Max(IntMath.DivDown(natural, 16), height), water, water ? _seaLevel : -1, riverWeight, lakeWeight);
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
    private int RiverWeight(in ClimateSample c, int natural16)
    {
        var river = _data.Rivers;
        var core = One - IntMath.Ramp(Math.Abs(c.River), 0, FieldScale.FromThousandths(river.Width));
        if (core == 0)
        {
            return 0;
        }

        core = core * core / One; // the square narrows the water and leaves the banks a long gentle shoulder

        var weight = core * (One - IntMath.Ramp(natural16, river.FadeFromHeight * 16, river.FadeToHeight * 16)) / One;
        return weight * IntMath.Ramp(c.Continentalness, FieldScale.FromThousandths(river.SeaFadeFrom), FieldScale.FromThousandths(river.SeaFadeTo)) / One;
    }

    // A lake where the lake field is high, the land inland, low and calm.
    private int LakeWeight(int x, int z, in ClimateSample c, int h16)
    {
        var lake = _data.Lakes;
        var calm = IntMath.Ramp(c.Erosion, FieldScale.FromThousandths(lake.CalmFrom), FieldScale.FromThousandths(lake.CalmTo));
        var low = One - IntMath.Ramp(h16, lake.HeightFrom * 16, lake.HeightTo * 16);
        var inland = IntMath.Ramp(c.Continentalness, FieldScale.FromThousandths(lake.InlandFrom), FieldScale.FromThousandths(lake.InlandTo));
        var weight = (long)calm * low / One * inland / One;
        if (weight == 0)
        {
            return 0;
        }

        var threshold = FieldScale.FromThousandths(lake.Threshold);
        var basin = IntMath.Ramp(_lakes.Fbm(x, z, lake.Cell, lake.Octaves), threshold, threshold + FieldScale.FromThousandths(lake.Softness));
        return (int)(weight * basin / One);
    }
}
