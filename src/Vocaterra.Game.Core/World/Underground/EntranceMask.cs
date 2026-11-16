using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>
/// The mask of the cave entrances (<see cref="EntranceSpec"/>): a column lies in an entrance zone where a 2D fractal noise field passes the threshold. The mask is a function of the column
/// alone, so the trees and the undergrowth can keep clear of every zone without reading anything else; whether a zone really opens (dry procedural land, no water near, a cave under it) is the
/// cave pass's. Pure and immutable.
/// </summary>
internal sealed class EntranceMask
{
    private readonly EntranceSpec _spec;
    private readonly NoiseStack _noise;
    private readonly int _threshold;

    /// <summary>Creates the mask of one world seed.</summary>
    /// <param name="spec">The entrances.</param>
    /// <param name="seed">The world seed.</param>
    public EntranceMask(EntranceSpec spec, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _spec = spec;
        _noise = new NoiseStack(seed, "gen/entrances");
        _threshold = FieldScale.FromThousandths(spec.Threshold);
    }

    /// <summary>The numbers.</summary>
    public EntranceSpec Spec => _spec;

    /// <summary>Whether the column is in an entrance zone.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public bool InZone(int x, int z) => _noise.Fbm(x, z, _spec.Cell, _spec.Octaves) > _threshold;
}
