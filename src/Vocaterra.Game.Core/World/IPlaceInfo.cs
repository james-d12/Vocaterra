using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// What a consumer asks of a place (B06, biomes plan section 3.2), read from the world's pure model and never from the grid's edited blocks, so the guide's worker, the weather, the sound and the
/// future temperature system may all ask from any thread without a snapshot. Every answer is a function of the seed, the data, the regions and the place.
/// </summary>
public interface IPlaceInfo
{
    /// <summary>The climate of the column (x, z), with the continentalness pulled towards land round a region.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    ClimateSample ClimateAt(int x, int z);

    /// <summary>The climate temperature at a point: the column's sea-level temperature lowered by the lapse with height (biomes plan section 3.3).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The height.</param>
    /// <param name="z">The block's z.</param>
    int TemperatureAt(int x, int y, int z);

    /// <summary>The biome row of the column, chosen on the natural ground.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    BiomeRow BiomeAt(int x, int z);

    /// <summary>The region whose footprint holds the column, or <see langword="null"/> in the procedural land.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    IRegion? RegionAt(int x, int z);

    /// <summary>The y of the topmost solid block of the column as the world made it: the region's own ground inside a region, the blended procedural ground outside (the bed under water; trees and edits are not part of it).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    int NaturalSurfaceAt(int x, int z);

    /// <summary>Whether the point lies below the natural surface of its column (inside the ground, with no sky over it).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    bool IsUnderground(int x, int y, int z);
}
