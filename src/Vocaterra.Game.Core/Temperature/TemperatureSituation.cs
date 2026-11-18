using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// Where and when a temperature is asked for (I06): everything a term may read, gathered once for the place and the moment, so no term reaches into the session. A later
/// term that needs more of the world (the biomes' climate at a point, a cave's pull) adds a member here and reads it; the terms before it are not touched.
/// </summary>
/// <param name="Atmosphere">The sky and weather now (its channels, never a kind's id).</param>
/// <param name="Feet">The block the player's feet are in.</param>
/// <param name="CoveredShare">The share of the columns round the feet that have cover above the head, 0 (open sky) to 1 (a roof over all of them): the very query the rain's drops and its sound use (<see cref="OverheadCover"/>).</param>
/// <param name="Fires">The world's fire rings (<see cref="FireRings.NearLit"/>), or <see langword="null"/> for a world with none.</param>
/// <param name="Places">The world's pure place query (the climate at a column and height, I06b), or <see langword="null"/> for a situation with no world (the climate term then adds nothing).</param>
public readonly record struct TemperatureSituation(AtmosphereSnapshot Atmosphere, BlockPos Feet, double CoveredShare, FireRings? Fires, World.IPlaceInfo? Places = null);
