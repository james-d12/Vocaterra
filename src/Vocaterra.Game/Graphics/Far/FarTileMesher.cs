using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Far;

/// <summary>One built far-view tile: the vertices of its land and its skirts (the same layout for every tile, see <see cref="FarTileTopology"/>), its box for culling and what it cost to make.</summary>
/// <param name="Key">The tile.</param>
/// <param name="Vertices">The vertices, absolute world positions.</param>
/// <param name="Box">The box round every vertex.</param>
/// <param name="BuildMilliseconds">The time the sampling and the meshing took.</param>
public sealed record FarTileMesh(FarTileKey Key, VertexPositionColor[] Vertices, BoundingBox Box, double BuildMilliseconds);

/// <summary>
/// The shape every far-view tile shares (B07): a grid of (N + 1) x (N + 1) vertices of land, then a skirt along each of the four edges, which is a row of N + 1 vertices dropped below the edge that the
/// edge's land vertices are joined to. The triangles are the same for every tile (the skirt's depth is in the vertices), so one index buffer serves them all; the winding is chosen so that the land
/// faces up and each skirt faces away from its tile, which the back-face culling then keeps and the inside of a mountain never shows.
/// </summary>
public static class FarTileTopology
{
    /// <summary>How many vertices a tile of <paramref name="cells"/> cells a side has.</summary>
    /// <param name="cells">The cells along a side.</param>
    public static int VertexCount(int cells) => ((cells + 1) * (cells + 1)) + (4 * (cells + 1));

    /// <summary>How many triangles a tile of <paramref name="cells"/> cells a side has.</summary>
    /// <param name="cells">The cells along a side.</param>
    public static int TriangleCount(int cells) => (2 * cells * cells) + (4 * cells * 2);

    /// <summary>The index of the land vertex at grid position (i, j).</summary>
    /// <param name="cells">The cells along a side.</param>
    /// <param name="i">The column, 0 to <paramref name="cells"/>.</param>
    /// <param name="j">The row.</param>
    public static int Land(int cells, int i, int j) => (j * (cells + 1)) + i;

    /// <summary>The index of the foot of the skirt under land vertex <paramref name="i"/> of an edge: 0 north (the row z = min), 1 south, 2 west (the column x = min), 3 east.</summary>
    /// <param name="cells">The cells along a side.</param>
    /// <param name="edge">The edge.</param>
    /// <param name="i">The position along the edge, from the west or the north.</param>
    public static int Foot(int cells, int edge, int i) => ((cells + 1) * (cells + 1)) + (edge * (cells + 1)) + i;

    /// <summary>The land vertex a skirt hangs from.</summary>
    /// <param name="cells">The cells along a side.</param>
    /// <param name="edge">The edge.</param>
    /// <param name="i">The position along the edge.</param>
    public static int Lip(int cells, int edge, int i) => edge switch
    {
        0 => Land(cells, i, 0),
        1 => Land(cells, i, cells),
        2 => Land(cells, 0, i),
        _ => Land(cells, cells, i),
    };

    /// <summary>The indices of a tile of <paramref name="cells"/> cells a side, 16-bit when the tile has few enough vertices.</summary>
    /// <param name="cells">The cells along a side.</param>
    public static ushort[] Indices(int cells)
    {
        if (VertexCount(cells) > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(cells), cells, "A tile that size has too many vertices for 16-bit indices.");
        }

        var template = Template(cells);
        var indices = new List<ushort>(TriangleCount(cells) * 3);
        void Add(int a, int b, int c, Vector3 outward)
        {
            var normal = Vector3.Cross(template[b] - template[a], template[c] - template[a]);
            if (Vector3.Dot(normal, outward) < 0)
            {
                (b, c) = (c, b);
            }

            indices.Add((ushort)a);
            indices.Add((ushort)b);
            indices.Add((ushort)c);
        }

        for (var j = 0; j < cells; j++)
        {
            for (var i = 0; i < cells; i++)
            {
                var a = Land(cells, i, j);
                var b = Land(cells, i + 1, j);
                var c = Land(cells, i, j + 1);
                var d = Land(cells, i + 1, j + 1);
                Add(a, c, b, Vector3.UnitY);
                Add(b, c, d, Vector3.UnitY);
            }
        }

        var outwards = new[] { -Vector3.UnitZ, Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitX };
        for (var edge = 0; edge < 4; edge++)
        {
            for (var i = 0; i < cells; i++)
            {
                var top0 = Lip(cells, edge, i);
                var top1 = Lip(cells, edge, i + 1);
                var foot0 = Foot(cells, edge, i);
                var foot1 = Foot(cells, edge, i + 1);
                Add(top0, foot0, top1, outwards[edge]);
                Add(top1, foot0, foot1, outwards[edge]);
            }
        }

        return [.. indices];
    }

    // The tile with all its land flat at height 0 and a skirt one block deep: the shape the winding is decided on.
    private static Vector3[] Template(int cells)
    {
        var positions = new Vector3[VertexCount(cells)];
        for (var j = 0; j <= cells; j++)
        {
            for (var i = 0; i <= cells; i++)
            {
                positions[Land(cells, i, j)] = new Vector3(i, 0, j);
            }
        }

        for (var edge = 0; edge < 4; edge++)
        {
            for (var i = 0; i <= cells; i++)
            {
                positions[Foot(cells, edge, i)] = positions[Lip(cells, edge, i)] - Vector3.UnitY;
            }
        }

        return positions;
    }
}

/// <summary>
/// Builds a far-view tile from the pure land sampler (B07): the heights, the colour of the top block, the water, the snow and the forest of a grid of columns, as vertices of a mesh with its skirts. It uses
/// the sampler and the block palette only, so it runs on any thread and is tested without a window. The land is shaded by its slope, so a far range is modelled and not flat; the whole is baked in the
/// full light of day and drawn with the tint of the moment, as the voxel chunks are.
/// </summary>
public static class FarTileMesher
{
    // The share of a column's colour that its trees give it at full density: the crowns hide most of the ground from afar, but never all of it.
    private const double ForestShare = 0.6;

    /// <summary>Builds a tile.</summary>
    /// <param name="sampler">The land.</param>
    /// <param name="settings">The far view's settings (the cells and the tile's size).</param>
    /// <param name="key">The tile.</param>
    /// <param name="greyKinds">The block kinds drawn grey (the greyscale view, <see cref="ChunkMesher.KindBit"/>): the land they make is drawn grey from afar too, so the seam with the chunks has one colour.</param>
    public static FarTileMesh Build(FarTerrainSampler sampler, FarViewSettings settings, FarTileKey key, UInt128 greyKinds = default)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        ArgumentNullException.ThrowIfNull(settings);
        var started = MonotonicClock.Milliseconds();
        var n = settings.TileCells;
        var cell = settings.CellOf(key.Level);
        var x0 = key.MinX(settings);
        var z0 = key.MinZ(settings);
        // The samples are taken one cell beyond the tile on every side, so a slope at its edge is the slope the neighbour sees and the two tiles shade alike.
        var side = n + 3;
        var samples = new FarSample[side * side];
        sampler.Fill(x0 - cell, z0 - cell, cell, side, side, samples);
        FarSample At(int i, int j) => samples[((j + 1) * side) + i + 1];
        float Surface(int i, int j) => At(i, j).SurfaceY + 1f;

        var vertices = new VertexPositionColor[FarTileTopology.VertexCount(n)];
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (var j = 0; j <= n; j++)
        {
            for (var i = 0; i <= n; i++)
            {
                var sample = At(i, j);
                var y = Surface(i, j);
                var slopeX = (Surface(i + 1, j) - Surface(i - 1, j)) / (2f * cell);
                var slopeZ = (Surface(i, j + 1) - Surface(i, j - 1)) / (2f * cell);
                // The light is the one the chunks' faces take (ChunkMesher.SunTintFor): a flat top is the top of a block, a slope turns towards or away from the sun like a side face does.
                var length = MathF.Sqrt(1f + (slopeX * slopeX) + (slopeZ * slopeZ));
                var light = sample.IsWater ? ChunkMesher.SunTintFor(0f, 1f, 0f) : ChunkMesher.SunTintFor(-slopeX / length, 1f / length, -slopeZ / length);
                vertices[FarTileTopology.Land(n, i, j)] = new VertexPositionColor(new Vector3(x0 + (i * cell), y, z0 + (j * cell)), ColourOf(sample, light, greyKinds));
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        var skirt = settings.SkirtOf(key.Level);
        for (var edge = 0; edge < 4; edge++)
        {
            for (var i = 0; i <= n; i++)
            {
                var lip = vertices[FarTileTopology.Lip(n, edge, i)];
                vertices[FarTileTopology.Foot(n, edge, i)] = new VertexPositionColor(lip.Position - new Vector3(0, skirt, 0), lip.Color);
                minY = Math.Min(minY, lip.Position.Y - skirt);
            }
        }

        var size = n * cell;
        var box = new BoundingBox(new Vector3(x0, minY, z0), new Vector3(x0 + size, maxY, z0 + size));
        return new FarTileMesh(key, vertices, box, MonotonicClock.Milliseconds() - started);
    }

    /// <summary>The colour of a column in a light: its top block's, the sea's under water, white under snow, mixed with the leaves' by the forest on it, grey where the greyscale view has the block grey.</summary>
    /// <param name="sample">The column.</param>
    /// <param name="light">The light on it, the multiplier of each channel (<see cref="ChunkMesher.SunTintFor"/>).</param>
    /// <param name="greyKinds">The block kinds drawn grey.</param>
    public static Color ColourOf(FarSample sample, (float R, float G, float B) light, UInt128 greyKinds = default)
    {
        var (r, g, b) = Of(sample.IsWater ? BlockId.Water : sample.Top, greyKinds);
        if (!sample.IsWater && !sample.Snow && sample.Forest > 0)
        {
            var (lr, lg, lb) = Of(BlockId.Leaves, greyKinds);
            var t = (float)(ForestShare * sample.Forest / 100.0);
            (r, g, b) = ((byte)(r + ((lr - r) * t)), (byte)(g + ((lg - g) * t)), (byte)(b + ((lb - b) * t)));
        }

        return new Color((byte)Math.Min(255f, r * light.R), (byte)Math.Min(255f, g * light.G), (byte)Math.Min(255f, b * light.B), (byte)255);
    }

    private static (byte R, byte G, byte B) Of(BlockId block, UInt128 greyKinds)
    {
        var (r, g, b) = FarPalette.Of(block);
        if (!ChunkMesher.IsGrey(block, greyKinds))
        {
            return (r, g, b);
        }

        var luma = Greyscale.Luma(r, g, b);
        return (luma, luma, luma);
    }
}
