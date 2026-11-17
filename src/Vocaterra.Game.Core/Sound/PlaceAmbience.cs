using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// What the world's pure model says about the sound at a place (B14, biomes plan section 3): the share of each place-driven bed (<see cref="AmbientBeds.ByPlace"/>) the biome's data names, blended over the
/// columns round the listener so a border is a slope and not a step, and how far underground the listener's head is (the cave bed, quiet, takes over as the ground closes overhead). It is read from
/// <see cref="IPlaceInfo"/> (a function of the seed, the data, the regions and the place, never of the edited blocks), so it may be asked from any thread. No biome id is named here: the data's
/// <c>ambient</c> field of the row names the beds, and the sound only reads the shares.
/// </summary>
public sealed class PlaceAmbience
{
    /// <summary>The height of the listener's eyes above the feet, in blocks, at which the ground is asked whether it closes overhead.</summary>
    public const double EyeHeight = 1.6;

    /// <summary>The distance in blocks of the columns read round the listener (so the biome's beds fade over about twice this much).</summary>
    public const int Spread = 6;

    /// <summary>The depth under the natural surface (in blocks) at which the cave bed starts to be heard.</summary>
    public const double CaveFrom = 2;

    /// <summary>The depth under the natural surface at which the cave bed is full and the surface beds are gone.</summary>
    public const double CaveFull = 10;

    private readonly double[] _shares;

    private PlaceAmbience(string name, bool procedural, double underground, double[] shares)
    {
        Name = name;
        Procedural = procedural;
        Underground = underground;
        _shares = shares;
    }

    /// <summary>A place with no biome and no ground over it: the open ground of the hand-crafted regions, whose beds the scene's places give.</summary>
    public static PlaceAmbience Open { get; } = new(string.Empty, false, 0, new double[AmbientBeds.ByPlace.Count]);

    /// <summary>The place's name for the sound log: the biome's id in the procedural land, <c>underground</c> under the ground, or empty in a region.</summary>
    public string Name { get; }

    /// <summary>Whether the biome's data sets the beds (the listener stands in the procedural land, not inside a hand-crafted region).</summary>
    public bool Procedural { get; }

    /// <summary>How far underground the listener's head is, 0 (under the sky) to 1 (the cave bed alone).</summary>
    public double Underground { get; }

    /// <summary>The share of a place-driven bed, 0 to 1 (0 when the place is a region's or the bed is none of <see cref="AmbientBeds.ByPlace"/>).</summary>
    /// <param name="bed">The bed's id.</param>
    public double Share(string bed)
    {
        for (var i = 0; i < AmbientBeds.ByPlace.Count; i++)
        {
            if (AmbientBeds.ByPlace[i] == bed)
            {
                return _shares[i];
            }
        }

        return 0;
    }

    /// <summary>Reads the place of a listener.</summary>
    /// <param name="places">The world's place query.</param>
    /// <param name="x">The feet's x in blocks.</param>
    /// <param name="y">The feet's height in blocks.</param>
    /// <param name="z">The feet's z in blocks.</param>
    public static PlaceAmbience At(IPlaceInfo places, double x, double y, double z)
    {
        ArgumentNullException.ThrowIfNull(places);
        var (bx, bz) = ((int)Math.Floor(x), (int)Math.Floor(z));
        var eye = (int)Math.Floor(y + EyeHeight);
        var depth = places.IsUnderground(bx, eye, bz) ? places.NaturalSurfaceAt(bx, bz) - (y + EyeHeight) : 0;
        var underground = AmbientBeds.Ramp(depth, CaveFrom, CaveFull);
        if (places.RegionAt(bx, bz) is not null)
        {
            return underground <= 0 ? Open : new PlaceAmbience("underground", false, underground, Open._shares);
        }

        var shares = new double[AmbientBeds.ByPlace.Count];
        // Five columns at distinct x and distinct z, so a border moves the blend by a fifth at a time (at most) as the listener walks, never by more.
        (int X, int Z)[] columns = [(bx, bz), (bx + Spread, bz + 2), (bx - Spread, bz - 2), (bx + 2, bz - Spread), (bx - 2, bz + Spread)];
        foreach (var (cx, cz) in columns)
        {
            if (places.RegionAt(cx, cz) is not null)
            {
                continue; // a region's columns add nothing: the procedural beds fade towards its edge
            }

            foreach (var share in places.BiomeAt(cx, cz).Ambient ?? [])
            {
                var index = IndexOf(share.Bed);
                if (index >= 0)
                {
                    shares[index] += Math.Clamp(share.Weight / 1000.0, 0, 1) / columns.Length;
                }
            }
        }

        return new PlaceAmbience(underground > 0 ? "underground" : places.BiomeAt(bx, bz).Id, true, underground, shares);
    }

    private static int IndexOf(string bed)
    {
        for (var i = 0; i < AmbientBeds.ByPlace.Count; i++)
        {
            if (AmbientBeds.ByPlace[i] == bed)
            {
                return i;
            }
        }

        return -1;
    }
}
