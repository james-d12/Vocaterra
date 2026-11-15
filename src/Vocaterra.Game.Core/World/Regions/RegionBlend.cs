using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// The rules that ease the procedural land into the edge of an authored region (B06, biomes plan section 3.6), for every region of a catalog. The nearest region decides, by the distance from its
/// footprint (0 inside, 1 for the column beside the edge):
/// <list type="bullet">
/// <item>The ground of a column inside is the region's own. Outside, over <see cref="RegionBlendData.Margin"/> blocks, the procedural height eases (a smoothstep, flat at both ends) towards the
/// height of the region's edge at the nearest edge point, so the first column outside is the edge's height and the land beyond the margin is the free procedural land. The edge's own roughness
/// is eased away over <see cref="EdgeSmoothingSpec.Ease"/> blocks (to its blurred height), so it does not comb the land into ridges. The rivers' and lakes'
/// weights fade by the same weight, so the water gives way to the ground.</item>
/// <item>No trunk stands inside, or within <see cref="RegionBlendData.TreeClearance"/> columns outside, so a crown never crosses the border; across the margin the trees thin out in a straight line
/// from none at the clearance to the biome's own density at the margin.</item>
/// <item>The continentalness is never below a floor that falls in a straight line from the edge (<see cref="LandBiasSpec.Floor"/>) to the radius (<see cref="LandBiasSpec.ShoreFloor"/>), so no sea
/// opens at the region's feet.</item>
/// <item>An exit corridor keeps a road walkable beyond an exit (<see cref="BlendedHeightModel"/>).</item>
/// </list>
/// It is pure and immutable, and every number comes from the data and the regions.
/// </summary>
internal sealed class RegionBlend
{
    private readonly int _seaLevel;
    private readonly int _floor;
    private readonly int _shoreFloor;

    /// <summary>Creates the blend of a world.</summary>
    /// <param name="regions">The regions.</param>
    /// <param name="data">The blend's numbers.</param>
    /// <param name="seaLevel">The sea level.</param>
    public RegionBlend(RegionCatalog regions, RegionBlendData data, int seaLevel)
    {
        Regions = regions;
        Data = data;
        _seaLevel = seaLevel;
        _floor = FieldScale.FromThousandths(data.LandBias.Floor);
        _shoreFloor = FieldScale.FromThousandths(data.LandBias.ShoreFloor);
    }

    /// <summary>The regions the land is eased into.</summary>
    public RegionCatalog Regions { get; }

    /// <summary>The numbers.</summary>
    public RegionBlendData Data { get; }

    /// <summary>The sea level.</summary>
    public int SeaLevel => _seaLevel;

    /// <summary>The weight, 0 to <see cref="IntMath.One"/>, of the region's edge on a column that far outside (0 at the margin and beyond).</summary>
    /// <param name="distance">The distance from the footprint, at least 1.</param>
    public int EdgeWeight(int distance)
    {
        var margin = Data.Margin;
        return distance >= margin ? 0 : IntMath.Smooth((margin - distance) * IntMath.One / margin);
    }

    /// <summary>The continentalness of a column after the pull towards land: never below the floor that falls with the distance from the nearest region.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="continentalness">The field's own value.</param>
    public int PullTowardsLand(int x, int z, int continentalness)
    {
        var (region, distance) = Regions.Nearest(x, z);
        if (region is null)
        {
            return continentalness;
        }

        var floor = _floor - (int)((long)(_floor - _shoreFloor) * distance / Data.LandBias.Radius);
        return Math.Max(continentalness, floor);
    }
}

/// <summary>The climate with the continentalness pulled towards land round the regions (<see cref="RegionBlend"/>); the rest of the climate is the inner sampler's.</summary>
internal sealed class BlendedClimateSampler : IClimateSampler
{
    private readonly IClimateSampler _inner;
    private readonly RegionBlend _blend;

    public BlendedClimateSampler(IClimateSampler inner, RegionBlend blend)
    {
        _inner = inner;
        _blend = blend;
    }

    /// <inheritdoc />
    public ClimateSample At(int x, int z)
    {
        var c = _inner.At(x, z);
        return c with { Continentalness = _blend.PullTowardsLand(x, z, c.Continentalness) };
    }

    /// <inheritdoc />
    public int TemperatureAt(int x, int y, int z) => _inner.TemperatureAt(x, y, z);

    /// <inheritdoc />
    public void Fill(int x0, int z0, int width, int depth, Span<ClimateSample> into)
    {
        _inner.Fill(x0, z0, width, depth, into);
        for (var j = 0; j < depth; j++)
        {
            for (var i = 0; i < width; i++)
            {
                var k = (j * width) + i;
                into[k] = into[k] with { Continentalness = _blend.PullTowardsLand(x0 + i, z0 + j, into[k].Continentalness) };
            }
        }
    }
}

/// <summary>
/// The height model with the regions' edges blended in and the exit corridors laid (<see cref="RegionBlend"/>). A column inside a region has the region's own ground; one outside eases from the
/// edge's height to the inner model's over the margin; a corridor's road is a profile worked out once, at construction, that starts at the edge's height and follows the blended land at no more
/// than one block of rise a block along the road.
/// </summary>
internal sealed class BlendedHeightModel : IHeightModel
{
    private const int One = IntMath.One;

    private readonly IHeightModel _inner;
    private readonly RegionBlend _blend;
    private readonly Corridor[] _corridors;

    public BlendedHeightModel(IHeightModel inner, IClimateSampler climate, RegionBlend blend)
    {
        _inner = inner;
        _blend = blend;
        _corridors = [.. blend.Data.ExitCorridors.Select(spec => Corridor.Build(spec, this, climate, blend))];
    }

    /// <summary>The ground without the corridors: the region's own inside, eased to the edge across the margin, and the inner model's beyond.</summary>
    private ColumnShape Eased(int x, int z, in ClimateSample c)
    {
        var shape = _inner.Shape(x, z, c);
        var (region, distance) = _blend.Regions.Nearest(x, z);
        if (region is null || distance >= _blend.Data.Margin)
        {
            return shape;
        }

        var target = region.EdgeHeight(x, z);
        if (distance == 0)
        {
            return Own(target);
        }

        // Beside the border the ground is the edge's own height; a few blocks out it is the edge's height blurred along the border, so the edge's roughness does not comb the land into ridges.
        var ease = _blend.Data.Smoothing.Ease;
        if (distance < ease)
        {
            target = IntMath.Lerp(target, region.SmoothedEdgeHeight(x, z), IntMath.Smooth(distance * One / ease));
        }
        else
        {
            target = region.SmoothedEdgeHeight(x, z);
        }

        var weight = _blend.EdgeWeight(distance);
        return Toward(shape, target, weight);
    }

    private ColumnShape Own(int ground)
    {
        var water = ground < _blend.SeaLevel;
        return new ColumnShape(ground, ground, water, water ? _blend.SeaLevel : -1, 0, 0);
    }

    // The column eased by weight towards a ground height: the water's weights give way to the ground by the same weight.
    private ColumnShape Toward(ColumnShape shape, int ground, int weight)
    {
        var height = IntMath.Lerp(shape.Height, ground, weight);
        var natural = Math.Max(height, IntMath.Lerp(shape.NaturalHeight, ground, weight));
        var water = height < _blend.SeaLevel;
        var keep = One - weight;
        return new ColumnShape(height, natural, water, water ? _blend.SeaLevel : -1, (int)((long)shape.RiverWeight * keep / One), (int)((long)shape.LakeWeight * keep / One));
    }

    /// <inheritdoc />
    public ColumnShape Shape(int x, int z, in ClimateSample c)
    {
        var shape = Eased(x, z, c);
        foreach (var corridor in _corridors)
        {
            if (corridor.Reaches(x, z, out var road, out var weight))
            {
                shape = Toward(shape, road, weight);
            }
        }

        return shape;
    }

    /// <summary>Whether a trunk may stand in the column: not inside a region, not within the clearance of its edge, thinned across the margin by the candidate's own priority, and not on an exit corridor's road.</summary>
    /// <param name="x">The trunk's x.</param>
    /// <param name="z">The trunk's z.</param>
    /// <param name="priority">The candidate's random priority (any value is fine: it makes the thinning a function of the candidate).</param>
    public bool TreeAllowed(int x, int z, ulong priority)
    {
        var (region, distance) = _blend.Regions.Nearest(x, z);
        if (region is not null && distance < _blend.Data.Margin)
        {
            var clearance = _blend.Data.TreeClearance;
            if (distance <= clearance)
            {
                return false;
            }

            var keep = (distance - clearance) * 1000 / (_blend.Data.Margin - clearance);
            if ((int)(priority % 1000UL) >= keep)
            {
                return false;
            }
        }

        foreach (var corridor in _corridors)
        {
            if (corridor.IsOnRoad(x, z))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The corridors' road heights (for the tests): the profile of the first corridor, one height a block along the line.</summary>
    internal IReadOnlyList<int> RoadProfile(int corridor) => _corridors[corridor].Profile;

    private sealed class Corridor
    {
        private readonly ExitCorridorSpec _spec;
        private readonly int _length;
        private readonly int _dx;
        private readonly int _dz;
        private readonly int[] _profile;

        private Corridor(ExitCorridorSpec spec, int length, int[] profile)
        {
            _spec = spec;
            _length = length;
            _dx = spec.ToX - spec.FromX;
            _dz = spec.ToZ - spec.FromZ;
            _profile = profile;
        }

        public IReadOnlyList<int> Profile => _profile;

        // The road starts at the height of the region's edge and chases the blended land one block of rise at a time, never under the sea.
        public static Corridor Build(ExitCorridorSpec spec, BlendedHeightModel model, IClimateSampler climate, RegionBlend blend)
        {
            var (dx, dz) = (spec.ToX - spec.FromX, spec.ToZ - spec.FromZ);
            var length = (int)IntMath.ISqrt(((long)dx * dx) + ((long)dz * dz));
            var profile = new int[length + 1];
            var (region, _) = blend.Regions.Nearest(spec.FromX, spec.FromZ);
            var floor = blend.SeaLevel + 1;
            var previous = Math.Max(region?.EdgeHeight(spec.FromX, spec.FromZ) ?? floor, floor);
            profile[0] = previous;
            for (var s = 1; s <= length; s++)
            {
                var x = spec.FromX + (int)((long)dx * s / length);
                var z = spec.FromZ + (int)((long)dz * s / length);
                var target = model.Eased(x, z, climate.At(x, z)).Height;
                previous = Math.Max(Math.Clamp(target, previous - 1, previous + 1), floor);
                profile[s] = previous;
            }

            return new Corridor(spec, length, profile);
        }

        // Where the column is on the road or its bank: the road's height there and the weight, 0 to One (1 on the road, easing across the shoulder, giving way over the last fade blocks).
        public bool Reaches(int x, int z, out int road, out int weight)
        {
            road = 0;
            weight = 0;
            if (!Locate(x, z, out var along, out var lateral) || lateral > _spec.HalfWidth + _spec.Shoulder)
            {
                return false;
            }

            var side = lateral <= _spec.HalfWidth ? One : IntMath.Smooth((_spec.HalfWidth + _spec.Shoulder - lateral) * One / _spec.Shoulder);
            var end = _spec.Fade == 0 || along <= _length - _spec.Fade ? One : IntMath.Smooth((_length - along) * One / _spec.Fade);
            weight = (int)((long)side * end / One);
            road = _profile[along];
            return weight > 0;
        }

        public bool IsOnRoad(int x, int z) => Locate(x, z, out _, out var lateral) && lateral <= _spec.HalfWidth + 1;

        // The column's place along the line (0 to the length) and its distance from it, in whole blocks; false behind the start or beyond the end.
        private bool Locate(int x, int z, out int along, out int lateral)
        {
            var (px, pz) = ((long)x - _spec.FromX, (long)z - _spec.FromZ);
            var s = ((px * _dx) + (pz * _dz)) / _length;
            along = (int)Math.Clamp(s, 0, _length);
            lateral = (int)(Math.Abs((px * _dz) - (pz * _dx)) / _length);
            return s >= 0 && s <= _length;
        }
    }
}
