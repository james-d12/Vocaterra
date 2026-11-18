using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// What a temperature term reads (I06c): the moment (the sky, the player's block, the cover over them, the fires) and what the place makes of it (how far the sky and day reach, the
/// biome's adjustment, whether it snows, the climate under the feet). A new input is a new member here, set by <see cref="TemperatureContext"/>; no term that does not read it changes.
/// Derived values are worked out the first time a term asks, so a tick pays only for what its terms use.
/// </summary>
public interface ITemperatureContext
{
    /// <summary>The sky and the weather now.</summary>
    AtmosphereSnapshot Atmosphere { get; }

    /// <summary>The block under the player's feet.</summary>
    BlockPos Feet { get; }

    /// <summary>The share of the columns round the feet that have cover over them, 0 to 1.</summary>
    double CoveredShare { get; }

    /// <summary>The fire rings, or <see langword="null"/> where there are none.</summary>
    FireRings? Fires { get; }

    /// <summary>Whether the world's climate is known here (a world with a generator); when not, <see cref="Exposure"/> is full and the biome's adjustment is the default.</summary>
    bool HasWorld { get; }

    /// <summary>How far the sky and the day reach the player, in thousandths: 1 000 on the surface (and in a shallow dip), falling to 0 over the ground's fade depth below it.</summary>
    int Exposure { get; }

    /// <summary>The adjustment the place's biome gives (the default where the world is not known).</summary>
    BiomeEffect Biome { get; }

    /// <summary>The generator's climate temperature (fixed point) at the player's column, at the feet's height, or at the natural surface's when the feet are below it: height lowers it, depth does not warm it.</summary>
    int ClimateTemperature { get; }

    /// <summary>Whether precipitation here falls as snow: the climate temperature at the feet's own height is below the generator's snow line.</summary>
    bool Snows { get; }

    /// <summary>The steady temperature of the ground, in tenths of a degree, that a place below the surface settles to.</summary>
    int GroundTenths { get; }
}
