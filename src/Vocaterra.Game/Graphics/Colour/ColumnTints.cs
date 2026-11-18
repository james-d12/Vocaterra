using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.Colour;

/// <summary>
/// The tints of the grass and the oak leaves of the 16 x 16 columns of one chunk (B22): each is looked up in the <see cref="LandColours"/> map at the column's ground temperature and humidity, once, when the first block of the column asks. A chunk made
/// without a climate (a bare test, a tool) has the map's middle everywhere. Built and used by one thread (the mesher's).
/// </summary>
public sealed class ColumnTints
{
    private const int Side = VoxelChunk.Size;
    private readonly LandColours _map;
    private readonly Func<int, int, int, (int Temperature, int Humidity)>? _ground;
    private readonly int _x0;
    private readonly int _z0;
    private (float R, float G, float B)[]? _grass; // made when the first block of grass or leaves asks: most chunks hold neither
    private (float R, float G, float B)[]? _leaves;
    private bool[]? _known;

    /// <summary>Makes the tints of the chunk whose first block column is (<paramref name="x0"/>, <paramref name="z0"/>).</summary>
    /// <param name="map">The colour map.</param>
    /// <param name="ground">The ground temperature and humidity at a block (x, y, z), or <see langword="null"/> for none.</param>
    /// <param name="x0">The chunk's first x.</param>
    /// <param name="z0">The chunk's first z.</param>
    public ColumnTints(LandColours map, Func<int, int, int, (int Temperature, int Humidity)>? ground, int x0, int z0)
    {
        _map = map;
        _ground = ground;
        _x0 = x0;
        _z0 = z0;
    }

    /// <summary>The grass multiplier of a block column of the chunk.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y (the first block asked in a column sets the column's ground temperature).</param>
    /// <param name="z">The block's z.</param>
    public (float R, float G, float B) Grass(int x, int y, int z)
    {
        var i = Fill(x, y, z);
        return _grass![i];
    }

    /// <summary>The oak leaves' multiplier of a block column of the chunk.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public (float R, float G, float B) Leaves(int x, int y, int z)
    {
        var i = Fill(x, y, z);
        return _leaves![i];
    }

    private int Fill(int x, int y, int z)
    {
        var i = ((z - _z0) * Side) + (x - _x0);
        _grass ??= new (float R, float G, float B)[Side * Side];
        _leaves ??= new (float R, float G, float B)[Side * Side];
        _known ??= new bool[Side * Side];
        if (!_known[i])
        {
            var (t, h) = _ground?.Invoke(x, y, z) ?? (LandColours.NeutralTemperature, LandColours.NeutralHumidity);
            _grass[i] = _map.GrassTint(t, h);
            _leaves[i] = _map.LeafTint(t, h);
            _known[i] = true;
        }

        return i;
    }
}
