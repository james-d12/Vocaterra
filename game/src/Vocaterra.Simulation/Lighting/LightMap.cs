using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Lighting;

/// <summary>
/// The light of every tile, as integers from 0 (dark) to <see cref="MaxLevel"/> (full). Three sources, combined by the maximum:
/// <list type="bullet">
/// <item><description>Sky: a column open to the sky (no solid tile above it) is at full light down to its first solid tile, which is lit
/// on its face. Below that the column is dark.</description></item>
/// <item><description>Torches: a torch is a source at full light.</description></item>
/// <item><description>Spread: light flows from each lit cell to its four neighbours, losing <see cref="Falloff"/> per step, through cells that
/// are not solid. A solid cell takes light from its neighbours but does not pass it on. So the light under an overhang falls off
/// sideways, and a torch lights a disc of radius <see cref="TorchRadius"/>.</description></item>
/// </list>
/// Solid rock that receives no light is still drawn at <see cref="RockFloor"/> (see <see cref="Brightness"/>), so an unlit cave wall stays
/// readable while the open air of a dark cave is black. Everything is integer arithmetic over the tile grid, so the same tiles give the same
/// map on every machine. The map is derived data: it is not in the state digest and not saved.
/// </summary>
public sealed class LightMap
{
    /// <summary>The full light level (a torch, open sky).</summary>
    public const int MaxLevel = 16;

    /// <summary>The light lost per step of spread.</summary>
    public const int Falloff = 2;

    /// <summary>The brightness of solid rock that receives less light than this (so rock is never fully black).</summary>
    public const int RockFloor = 6;

    /// <summary>How many tiles a torch lights from its own tile (<see cref="MaxLevel"/> divided by <see cref="Falloff"/>).</summary>
    public const int TorchRadius = MaxLevel / Falloff;

    /// <summary>How far a change can reach: a change at one column alters light up to this many columns away.</summary>
    internal const int Reach = TorchRadius + 1;

    private readonly TileGrid _tiles;
    private readonly byte[] _levels;

    /// <summary>Computes the light of the whole grid.</summary>
    internal LightMap(TileGrid tiles)
    {
        _tiles = tiles;
        _levels = new byte[tiles.Width * tiles.Height];
        Recompute(0, tiles.Width - 1);
    }

    /// <summary>The light level of a tile (0 to <see cref="MaxLevel"/>); 0 outside the world.</summary>
    public int Level(int x, int y) => _tiles.InBounds(x, y) ? _levels[(y * _tiles.Width) + x] : 0;

    /// <summary>
    /// The brightness a front-end draws a tile at (0 to <see cref="MaxLevel"/>): its light level, and at least <see cref="RockFloor"/> for solid
    /// rock.
    /// </summary>
    public int Brightness(int x, int y)
    {
        var level = Level(x, y);
        return TileDefinition.IsSolid(_tiles.GetTile(x, y)) ? Math.Max(level, RockFloor) : level;
    }

    /// <summary>The light a tile of this kind gives off (a torch gives <see cref="MaxLevel"/>; other tiles give none).</summary>
    public static int EmissionOf(TileId tile) => tile == TileId.Torch ? MaxLevel : 0;

    /// <summary>
    /// Recomputes the light after tiles in columns <paramref name="firstColumn"/> to <paramref name="lastColumn"/> changed. Only the columns
    /// within <see cref="Reach"/> of them are recomputed, and the cells outside take their light from the unchanged map: the result is the
    /// same as a full recompute.
    /// </summary>
    internal void TilesChanged(int firstColumn, int lastColumn) =>
        Recompute(Math.Max(0, firstColumn - Reach), Math.Min(_tiles.Width - 1, lastColumn + Reach));

    /// <summary>Recomputes the light of the columns from <paramref name="x1"/> to <paramref name="x2"/> (inclusive), all rows.</summary>
    private void Recompute(int x1, int x2)
    {
        var width = _tiles.Width;
        var height = _tiles.Height;
        for (var y = 0; y < height; y++)
        {
            Array.Clear(_levels, (y * width) + x1, x2 - x1 + 1);
        }

        // Sky: each column is full light down to and including its first solid tile.
        for (var x = x1; x <= x2; x++)
        {
            for (var y = 0; y < height; y++)
            {
                _levels[(y * width) + x] = MaxLevel;
                if (TileDefinition.IsSolid(_tiles.GetTile(x, y)))
                {
                    break;
                }
            }
        }

        // Torches inside the window.
        var buckets = new List<int>[MaxLevel + 1];
        for (var level = 0; level <= MaxLevel; level++)
        {
            buckets[level] = [];
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = x1; x <= x2; x++)
            {
                var index = (y * width) + x;
                var emitted = EmissionOf(_tiles.GetTile(x, y));
                if (emitted > _levels[index])
                {
                    _levels[index] = (byte)emitted;
                }

                if (_levels[index] > 0)
                {
                    buckets[_levels[index]].Add(index);
                }
            }
        }

        // Light entering the window from the unchanged columns on either side.
        for (var y = 0; y < height; y++)
        {
            if (x1 > 0)
            {
                SeedFrom(x1, y, x1 - 1, y, buckets);
            }

            if (x2 < width - 1)
            {
                SeedFrom(x2, y, x2 + 1, y, buckets);
            }
        }

        // Spread, brightest first: a cell is handled at its final level, so each cell is expanded once per level it reaches.
        for (var level = MaxLevel; level > Falloff; level--)
        {
            var bucket = buckets[level];
            for (var i = 0; i < bucket.Count; i++)
            {
                var index = bucket[i];
                if (_levels[index] != level)
                {
                    continue;
                }

                var x = index % width;
                var y = index / width;
                if (TileDefinition.IsSolid(_tiles.GetTile(x, y)))
                {
                    continue;
                }

                var next = level - Falloff;
                Spread(x - 1, y, next, x1, x2, buckets);
                Spread(x + 1, y, next, x1, x2, buckets);
                Spread(x, y - 1, next, x1, x2, buckets);
                Spread(x, y + 1, next, x1, x2, buckets);
            }
        }
    }

    // A cell of the window takes light from an outside neighbour that is open to light.
    private void SeedFrom(int x, int y, int outsideX, int outsideY, List<int>[] buckets)
    {
        if (TileDefinition.IsSolid(_tiles.GetTile(outsideX, outsideY)))
        {
            return;
        }

        var offered = _levels[(outsideY * _tiles.Width) + outsideX] - Falloff;
        var index = (y * _tiles.Width) + x;
        if (offered > _levels[index])
        {
            _levels[index] = (byte)offered;
            buckets[offered].Add(index);
        }
    }

    private void Spread(int x, int y, int level, int x1, int x2, List<int>[] buckets)
    {
        if (level <= 0 || x < x1 || x > x2 || !_tiles.InBounds(x, y))
        {
            return;
        }

        var index = (y * _tiles.Width) + x;
        if (level > _levels[index])
        {
            _levels[index] = (byte)level;
            buckets[level].Add(index);
        }
    }
}
