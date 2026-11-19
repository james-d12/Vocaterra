using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The context of one temperature reading (I06c): the moment's own values and, worked out when first asked, what the place makes of them (the natural surface and the biome of the column
/// through <see cref="IPlaceInfo"/>, from a <see cref="PlaceColumns"/> cache when the caller keeps one). Pure: the same moment and world give the same answers.
/// </summary>
public sealed class TemperatureContext : ITemperatureContext
{
    private readonly IPlaceInfo? _places;
    private readonly TemperatureEnvironment _environment;
    private readonly PlaceColumns? _columns;
    private PlaceColumn? _column;
    private int? _exposure;
    private int? _climate;
    private bool? _snows;

    /// <summary>A context.</summary>
    /// <param name="atmosphere">The sky and the weather.</param>
    /// <param name="feet">The block under the feet.</param>
    /// <param name="coveredShare">The share of cover round the feet.</param>
    /// <param name="fires">The fire rings, if any.</param>
    /// <param name="places">The world's place queries, or <see langword="null"/> for a place in no world.</param>
    /// <param name="environment">What the terms share (null for <see cref="TemperatureEnvironment.Plain"/>).</param>
    /// <param name="columns">A cache of the column's values the caller keeps from reading to reading (null for none).</param>
    public TemperatureContext(AtmosphereSnapshot atmosphere, BlockPos feet, double coveredShare, FireRings? fires, IPlaceInfo? places = null, TemperatureEnvironment? environment = null, PlaceColumns? columns = null)
    {
        Atmosphere = atmosphere;
        Feet = feet;
        CoveredShare = coveredShare;
        Fires = fires;
        _places = places;
        _environment = environment ?? TemperatureEnvironment.Plain;
        _columns = columns;
    }

    /// <inheritdoc />
    public AtmosphereSnapshot Atmosphere { get; }

    /// <inheritdoc />
    public BlockPos Feet { get; }

    /// <inheritdoc />
    public double CoveredShare { get; }

    /// <inheritdoc />
    public FireRings? Fires { get; }

    /// <inheritdoc />
    public bool HasWorld => _places is not null;

    /// <inheritdoc />
    public int GroundTenths => _environment.GroundTenths;

    /// <inheritdoc />
    public int Exposure => _exposure ??= _places is null ? 1000 : _environment.ExposureAt(Column.Surface - Feet.Y);

    /// <inheritdoc />
    public BiomeEffect Biome => _places is null ? BiomeEffect.Neutral : _environment.EffectOf(Column.BiomeId);

    /// <inheritdoc />
    public int ClimateTemperature => _climate ??= _places is null ? 0 : _places.TemperatureAt(Feet.X, Math.Max(Feet.Y, Column.Surface), Feet.Z);

    /// <inheritdoc />
    public bool Snows => _snows ??= _places is not null && _places.TemperatureAt(Feet.X, Feet.Y, Feet.Z) < _places.SnowBelowTemperature;

    private PlaceColumn Column => _column ??= (_columns ?? new PlaceColumns()).Read(_places!, Feet.X, Feet.Z);
}
