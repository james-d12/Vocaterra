using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Far;

/// <summary>Which of a tile's three lists a face goes into: the opaque land, the self-lit blocks (lava, lanterns: drawn in no light tint, so they glow at night) and the translucent water.</summary>
public enum FarBucket
{
    /// <summary>The land and the trees.</summary>
    Opaque,

    /// <summary>Blocks that give light.</summary>
    Lit,

    /// <summary>Water.</summary>
    Water,
}

/// <summary>One built far-view tile: the vertices of its faces (absolute world positions, each face its own four vertices in the colour it is lit), their triangles in three runs (opaque, lit, water), its box for culling and what it cost to make.</summary>
/// <param name="Key">The tile.</param>
/// <param name="Vertices">The vertices, absolute world positions, with a coordinate into the block atlas (a textured tile) or the noise texture.</param>
/// <param name="Indices16">The triangles' indices when the tile has few enough vertices for 16 bits, otherwise <see langword="null"/>.</param>
/// <param name="Indices32">The triangles' indices when it has not, otherwise <see langword="null"/>.</param>
/// <param name="OpaqueIndices">How many of the first indices are the opaque faces'.</param>
/// <param name="LitIndices">How many indices after them are the self-lit faces'; the water's are the rest.</param>
/// <param name="Box">The box round every vertex.</param>
/// <param name="BuildMilliseconds">The time the section fetch and the meshing took.</param>
/// <param name="Textured">Whether the opaque faces carry the block atlas's own textures (the nearest levels) and not flat colours with the noise texture.</param>
public sealed record FarTileMesh(FarTileKey Key, VertexPositionColorTexture[] Vertices, ushort[]? Indices16, int[]? Indices32, int OpaqueIndices, int LitIndices, BoundingBox Box, double BuildMilliseconds, bool Textured = false)
{
    /// <summary>How many indices the tile has.</summary>
    public int IndexCount => Indices16?.Length ?? Indices32?.Length ?? 0;

    /// <summary>How many triangles the tile has.</summary>
    public int Triangles => IndexCount / 3;

    /// <summary>How many indices the water has.</summary>
    public int WaterIndices => IndexCount - OpaqueIndices - LitIndices;

    /// <summary>The bytes the vertices and indices take.</summary>
    public long Bytes => ((long)Vertices.Length * VertexPositionColorTexture.VertexDeclaration.VertexStride) + (Indices16 is not null ? Indices16.Length * sizeof(ushort) : (Indices32?.Length ?? 0) * sizeof(int));

    /// <summary>The index at a position of the index list.</summary>
    /// <param name="position">The position.</param>
    public int IndexAt(int position) => Indices16 is not null ? Indices16[position] : Indices32![position];
}

/// <summary>The faces of a tile as they are made: quads (four vertices of one colour) with the winding the back-face culling keeps, so a face shows from the side its normal points to and never from behind.</summary>
internal sealed class FarGeometry
{
    private readonly List<VertexPositionColorTexture>[] _vertices = [new(4096), new(256), new(256)];
    private readonly List<int>[] _indices = [new(8192), new(512), new(512)];

    /// <summary>Adds a quad (four corners in order round it, with their texture coordinates) facing <paramref name="normal"/>.</summary>
    public void Quad(FarBucket bucket, (Vector3 P, Vector2 T) a, (Vector3 P, Vector2 T) b, (Vector3 P, Vector2 T) c, (Vector3 P, Vector2 T) d, Color colour, Vector3 normal)
    {
        if (Vector3.Dot(Vector3.Cross(b.P - a.P, c.P - a.P), normal) < 0)
        {
            (b, d) = (d, b);
        }

        var vertices = _vertices[(int)bucket];
        var indices = _indices[(int)bucket];
        var first = vertices.Count;
        vertices.Add(new VertexPositionColorTexture(a.P, colour, a.T));
        vertices.Add(new VertexPositionColorTexture(b.P, colour, b.T));
        vertices.Add(new VertexPositionColorTexture(c.P, colour, c.T));
        vertices.Add(new VertexPositionColorTexture(d.P, colour, d.T));
        indices.Add(first);
        indices.Add(first + 1);
        indices.Add(first + 2);
        indices.Add(first);
        indices.Add(first + 2);
        indices.Add(first + 3);
    }

    /// <summary>The tile made of what was added.</summary>
    public FarTileMesh Finish(FarTileKey key, double milliseconds, bool textured)
    {
        var total = _vertices[0].Count + _vertices[1].Count + _vertices[2].Count;
        var vertices = new VertexPositionColorTexture[total];
        var indices = new int[_indices[0].Count + _indices[1].Count + _indices[2].Count];
        var v = 0;
        var ix = 0;
        for (var b = 0; b < 3; b++)
        {
            _vertices[b].CopyTo(vertices, v);
            foreach (var index in _indices[b])
            {
                indices[ix++] = index + v;
            }

            v += _vertices[b].Count;
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var vertex in vertices)
        {
            min = Vector3.Min(min, vertex.Position);
            max = Vector3.Max(max, vertex.Position);
        }

        if (total == 0)
        {
            min = max = Vector3.Zero;
        }

        var box = new BoundingBox(min, max);
        if (total <= ushort.MaxValue)
        {
            var narrow = new ushort[indices.Length];
            for (var i = 0; i < narrow.Length; i++)
            {
                narrow[i] = (ushort)indices[i];
            }

            return new FarTileMesh(key, vertices, narrow, null, _indices[0].Count, _indices[1].Count, box, milliseconds, textured);
        }

        return new FarTileMesh(key, vertices, null, indices, _indices[0].Count, _indices[1].Count, box, milliseconds, textured);
    }
}

/// <summary>
/// Builds a far-view tile from the store's three-dimensional sections (B07c; D-game-34 item 25, the way the Voxy mod draws its far world): the stack of sections of a tile's footprint, from the world's floor to its ceiling, is one volume of voxels of the
/// tile's level (a voxel is 2^level blocks a side), and its faces are the tile: a face of a solid voxel wherever the next voxel is air or water, so an overhang, an arch, a crown over a trunk or a cave mouth keep their shape at every level. The nearest levels
/// (<see cref="FarViewSettings.TexturedLevels"/>) draw each face with its block's own atlas texture, mipmapped; further out, where a block is a few pixels, faces are the average colour of the block's art with a small noise texture, and runs of faces of one colour are
/// merged into one quad. Each face is lit as the chunk mesher lights a face of that facing and by the light stored in the voxel in front of it, with a little darkening at the foot of a step; water is its own translucent list, darker and more opaque with depth;
/// blocks that give light are another, drawn in no light tint. Along the tile's outer edge only the faces near the surface are drawn (a skirt), so two levels that meet leave no crack and no wall of buried stone is built. Only quads with whole-voxel edges are made.
/// </summary>
public static class FarTileMesher
{
    /// <summary>The side of the noise texture in texels; one texel is one voxel of the tile's level.</summary>
    public const int NoiseSize = 64;

    /// <summary>The factor that brings the noise texture's mean brightness (see <see cref="FarNoise"/>) back to 1.</summary>
    public const float NoiseGain = 1.075f;

    // Water this deep (in blocks) is opaque, and what lies under it is not drawn.
    private const int DeepWater = 12;

    /// <summary>Builds a tile.</summary>
    /// <param name="source">The sections.</param>
    /// <param name="settings">The far view's settings (the cells and the tile's size).</param>
    /// <param name="key">The tile.</param>
    /// <param name="greyKinds">The block kinds drawn grey (the greyscale view, <see cref="ChunkMesher.KindBit"/>): the land they make is drawn grey from afar too, so the seam with the chunks has one colour.</param>
    public static FarTileMesh Build(ILodSectionSource source, FarViewSettings settings, FarTileKey key, UInt128 greyKinds = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);
        var started = MonotonicClock.Milliseconds();
        return Mesh(source.Stack(key.Level, key.Tx, key.Tz), settings, key, greyKinds, started);
    }

    /// <summary>The tile of a stack of sections.</summary>
    /// <param name="stack">The stack of the tile's footprint.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="key">The tile.</param>
    /// <param name="greyKinds">The block kinds drawn grey.</param>
    /// <param name="started">The clock when the work began.</param>
    public static FarTileMesh Mesh(LodStack stack, FarViewSettings settings, FarTileKey key, UInt128 greyKinds = default, double? started = null)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(settings);
        var begin = started ?? MonotonicClock.Milliseconds();
        var textured = key.Level < settings.TexturedLevels;
        var cell = settings.CellOf(key.Level);
        var context = new Context(new Volume(stack), cell, key.MinX(settings), key.MinZ(settings), (settings.SkirtOf(key.Level) + cell - 1) / cell, textured, greyKinds, Lights.Make());
        var geometry = new FarGeometry();
        for (var axis = 0; axis < 3; axis++)
        {
            Sweep(geometry, context, axis, true);
            Sweep(geometry, context, axis, false);
        }

        return geometry.Finish(key, MonotonicClock.Milliseconds() - begin, textured);
    }

    /// <summary>The colour a block's top face has in a light, with the sky light of the data and no other shading: the average of its atlas tile times the light.</summary>
    /// <param name="block">The block.</param>
    /// <param name="light">The light on it, the multiplier of each channel (<see cref="ChunkMesher.SunTintFor"/>).</param>
    /// <param name="sky">The sky light, 0 to 15.</param>
    /// <param name="greyKinds">The block kinds drawn grey.</param>
    public static Color TopColour(BlockId block, (float R, float G, float B) light, int sky = 15, UInt128 greyKinds = default) =>
        Pack(Rgb(block, false, greyKinds), light, SkyShade(sky) * NoiseGain);

    // ---- the volume: a stack of sections as one array of voxels

    private sealed class Volume
    {
        private readonly int[] _surface = new int[LodSection.Size * LodSection.Size];

        public Volume(LodStack stack)
        {
            Height = stack.Sections.Length * LodSection.Size;
            YBase = stack.SyMin * LodSection.Size;
            Blocks = new BlockId[Height * LodSection.Size * LodSection.Size];
            Light = new byte[Blocks.Length];
            Top = int.MinValue;
            Bottom = int.MaxValue;
            Array.Fill(_surface, int.MaxValue);
            for (var s = 0; s < stack.Sections.Length; s++)
            {
                var section = stack.Sections[s];
                var offset = s * LodSection.Volume;
                if (section.IsUniform)
                {
                    if (section.FillBlock != BlockId.Air)
                    {
                        Array.Fill(Blocks, section.FillBlock, offset, LodSection.Volume);
                        Top = Math.Max(Top, ((s + 1) * LodSection.Size) - 1);
                        Bottom = Math.Min(Bottom, s * LodSection.Size);
                    }

                    Array.Fill(Light, section.LightAt(0), offset, LodSection.Volume);
                    continue;
                }

                for (var i = 0; i < LodSection.Volume; i++)
                {
                    var block = section.BlockAt(i);
                    Blocks[offset + i] = block;
                    Light[offset + i] = section.LightAt(i);
                    if (block != BlockId.Air)
                    {
                        var y = (s * LodSection.Size) + (i >> 10);
                        Top = Math.Max(Top, y);
                        Bottom = Math.Min(Bottom, y);
                    }
                }
            }
        }

        public int Height { get; }

        public int YBase { get; }

        public BlockId[] Blocks { get; }

        public byte[] Light { get; }

        // The highest and lowest rows that hold anything but air.
        public int Top { get; }

        public int Bottom { get; }

        public bool In(int x, int y, int z) => (uint)x < LodSection.Size && (uint)y < (uint)Height && (uint)z < LodSection.Size;

        public int At(int x, int y, int z) => (((y << 5) | z) << 5) | x;

        // A voxel's block, with air above the volume and stone under it (the world's floor).
        public BlockId BlockAt(int x, int y, int z) => y >= Height ? BlockId.Air : y < 0 ? BlockId.Stone : In(x, y, z) ? Blocks[At(x, y, z)] : BlockId.Air;

        // The y of the highest solid voxel of a column (found once).
        public int Surface(int x, int z)
        {
            ref var found = ref _surface[(z << 5) | x];
            if (found != int.MaxValue)
            {
                return found;
            }

            found = int.MinValue / 2;
            for (var y = Top; y >= Bottom; y--)
            {
                var block = Blocks[At(x, y, z)];
                if (block != BlockId.Air && block != BlockId.Water)
                {
                    found = y;
                    break;
                }
            }

            return found;
        }
    }

    private sealed record Context(Volume Volume, int Cell, int X0, int Z0, int SkirtCells, bool Textured, UInt128 Grey, Lights Light);

    // ---- colours

    private static (float R, float G, float B) Rgb(BlockId block, bool side, UInt128 greyKinds)
    {
        var (r, g, b) = side ? FarPalette.SideOf(block) : FarPalette.Of(block);
        if (!ChunkMesher.IsGrey(block, greyKinds))
        {
            return (r, g, b);
        }

        var luma = Greyscale.Luma(r, g, b);
        return (luma, luma, luma);
    }

    // Under a crown or water the air gets less sky light; the shade it gives the land is a little, never black.
    private static float SkyShade(int sky) => 0.45f + (0.55f * Math.Clamp(sky, 0, 15) / 15f);

    // Water is the art's colour, deeper and darker with depth (in blocks), more opaque with depth until it hides the bed.
    private static Color WaterOf(int depth, (float R, float G, float B) light, UInt128 greyKinds)
    {
        var water = Rgb(BlockId.Water, false, greyKinds);
        var t = Math.Clamp((depth - 1) / 14f, 0f, 1f);
        var shade = 1f - (0.45f * t);
        var alpha = Math.Clamp(0.55f + (0.45f * Math.Min(1f, (depth - 1) / (float)(DeepWater - 1))), 0f, 1f);
        var colour = Pack(water, light, shade * NoiseGain);
        return new Color(colour.R, colour.G, colour.B, (byte)(alpha * 255f));
    }

    private readonly record struct Lights((float R, float G, float B) Up, (float R, float G, float B) Down, (float R, float G, float B) East, (float R, float G, float B) West, (float R, float G, float B) South, (float R, float G, float B) North)
    {
        // The tints the chunk mesher's faces take, by the same function.
        public static Lights Make() => new(ChunkMesher.SunTintFor(0, 1, 0), ChunkMesher.SunTintFor(0, -1, 0), ChunkMesher.SunTintFor(1, 0, 0), ChunkMesher.SunTintFor(-1, 0, 0), ChunkMesher.SunTintFor(0, 0, 1), ChunkMesher.SunTintFor(0, 0, -1));

        public (float R, float G, float B) Of(BlockFace face) => face switch
        {
            BlockFace.Up => Up,
            BlockFace.Down => Down,
            BlockFace.East => East,
            BlockFace.West => West,
            BlockFace.South => South,
            _ => North,
        };
    }

    private static Color Pack((float R, float G, float B) colour, (float R, float G, float B) light, float factor) =>
        new((byte)Math.Clamp(colour.R * light.R * factor, 0f, 255f), (byte)Math.Clamp(colour.G * light.G * factor, 0f, 255f), (byte)Math.Clamp(colour.B * light.B * factor, 0f, 255f), (byte)255);

    private static readonly (float R, float G, float B) NoTint = (1f, 1f, 1f);

    // ---- the faces

    // What a face is, for merging: its colour and list, and (textured) the block it shows, so that none is merged with another.
    private readonly record struct Face(Color Colour, FarBucket Bucket, BlockId Block, bool Merge);

    private static readonly (int X, int Y, int Z)[] Axes = [(1, 0, 0), (0, 1, 0), (0, 0, 1)];

    private static BlockFace FaceOf(int axis, bool positive) => axis switch
    {
        0 => positive ? BlockFace.East : BlockFace.West,
        1 => positive ? BlockFace.Up : BlockFace.Down,
        _ => positive ? BlockFace.South : BlockFace.North,
    };

    // One direction: every voxel face of the volume that looks that way, merged where it may be, into quads.
    private static void Sweep(FarGeometry geometry, Context c, int axis, bool positive)
    {
        var v = c.Volume;
        if (v.Top == int.MinValue)
        {
            return;
        }

        // The axes of the plane the faces lie in: u and w.
        var (ua, wa) = axis switch { 0 => (2, 1), 1 => (0, 2), _ => (0, 1) };
        var face = FaceOf(axis, positive);
        var tint = c.Light.Of(face);
        int[] lo = [0, Math.Max(0, v.Bottom - 1), 0];
        int[] hi = [LodSection.Size - 1, Math.Min(v.Height - 1, v.Top + 1), LodSection.Size - 1];
        var pos = new int[3];
        var nu = hi[ua] - lo[ua] + 1;
        var nw = hi[wa] - lo[wa] + 1;
        var mask = new Face?[nu * nw];
        var step = positive ? 1 : -1;
        for (var slice = lo[axis]; slice <= hi[axis]; slice++)
        {
            Array.Clear(mask);
            var any = false;
            for (var w = lo[wa]; w <= hi[wa]; w++)
            {
                for (var u = lo[ua]; u <= hi[ua]; u++)
                {
                    pos[axis] = slice;
                    pos[ua] = u;
                    pos[wa] = w;
                    var block = v.BlockAt(pos[0], pos[1], pos[2]);
                    if (block == BlockId.Air)
                    {
                        continue;
                    }

                    var made = FaceAt(c, block, (pos[0], pos[1], pos[2]), (pos[0] + (Axes[axis].X * step), pos[1] + (Axes[axis].Y * step), pos[2] + (Axes[axis].Z * step)), axis, positive, face, tint);
                    if (made is not null)
                    {
                        mask[((w - lo[wa]) * nu) + (u - lo[ua])] = made;
                        any = true;
                    }
                }
            }

            if (any)
            {
                Merge(geometry, c, mask, nu, nw, lo[ua], lo[wa], slice, axis, ua, wa, positive, face);
            }
        }
    }

    // The face of the voxel at `at` looking at the neighbour `to`, or null when it is not seen.
    private static Face? FaceAt(Context c, BlockId block, (int X, int Y, int Z) at, (int X, int Y, int Z) to, int axis, bool positive, BlockFace face, (float R, float G, float B) tint)
    {
        var v = c.Volume;
        var inside = v.In(to.X, to.Y, to.Z);
        if (!inside && to.Y >= 0 && to.Y < v.Height && (block == BlockId.Water || at.Y < v.Surface(at.X, at.Z) - c.SkirtCells))
        {
            return null; // over the tile's edge: only near the surface (a skirt), and never a wall of water
        }

        var other = v.BlockAt(to.X, to.Y, to.Z);
        var nearLight = inside ? v.Light[v.At(to.X, to.Y, to.Z)] : LodSection.SkyFull;
        if (block == BlockId.Water)
        {
            if (other != BlockId.Air || (axis == 1 && !positive))
            {
                return null;
            }

            var depth = 1;
            for (var k = 1; k <= DeepWater && v.BlockAt(at.X, at.Y - k, at.Z) == BlockId.Water; k++)
            {
                depth++;
            }

            return new Face(WaterOf(depth * c.Cell, tint, c.Grey), FarBucket.Water, BlockId.Water, true);
        }

        if ((other != BlockId.Air && other != BlockId.Water) || (other == BlockId.Water && DeepBed(c, to)))
        {
            return null;
        }

        var side = face != BlockFace.Up && face != BlockFace.Down;
        if ((v.Light[v.At(at.X, at.Y, at.Z)] & 0xF) > 0)
        {
            return c.Textured
                ? new Face(Pack(NoTint, NoTint, 1f), FarBucket.Lit, block, false)
                : new Face(Pack(Rgb(block, side, c.Grey), NoTint, 1f), FarBucket.Lit, BlockId.Air, true);
        }

        var shade = SkyShade(nearLight >> 4);
        if (face == BlockFace.Up)
        {
            shade *= 1f - Occlusion(v, at.X, at.Y, at.Z);
        }

        return c.Textured
            ? new Face(Pack(NoTint, tint, shade), FarBucket.Opaque, block, false)
            : new Face(Pack(Rgb(block, side, c.Grey), tint, shade * NoiseGain), FarBucket.Opaque, BlockId.Air, true);
    }

    // Whether the water over a bed is deep enough (in blocks) to hide it: the water voxels standing over the neighbour voxel.
    private static bool DeepBed(Context c, (int X, int Y, int Z) at)
    {
        var count = 0;
        while (c.Volume.BlockAt(at.X, at.Y + count, at.Z) == BlockId.Water)
        {
            count++;
            if (count * c.Cell >= DeepWater)
            {
                return true;
            }
        }

        return false;
    }

    private static readonly (int Dx, int Dz)[] Around = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    // The foot of a step: a little darker for each solid voxel beside the one over a top face.
    private static float Occlusion(Volume v, int x, int y, int z)
    {
        var n = 0;
        foreach (var (dx, dz) in Around)
        {
            var b = v.BlockAt(x + dx, y + 1, z + dz);
            n += b != BlockId.Air && b != BlockId.Water ? 1 : 0;
        }

        return Math.Min(0.24f, n * 0.06f);
    }

    // The quads of one slice's mask: merged runs of equal faces (flat colours), or one quad a face (textured).
    private static void Merge(FarGeometry geometry, Context c, Face?[] mask, int nu, int nw, int u0, int w0, int slice, int axis, int ua, int wa, bool positive, BlockFace face)
    {
        var used = new bool[mask.Length];
        for (var w = 0; w < nw; w++)
        {
            for (var u = 0; u < nu; u++)
            {
                var at = (w * nu) + u;
                if (mask[at] is not { } first || used[at])
                {
                    continue;
                }

                var width = 1;
                var height = 1;
                if (first.Merge)
                {
                    while (u + width < nu && !used[at + width] && mask[at + width] is { } next && next == first)
                    {
                        width++;
                    }

                    while (w + height < nw && RowMatches(mask, used, nu, u, w + height, width, first))
                    {
                        height++;
                    }
                }

                for (var dw = 0; dw < height; dw++)
                {
                    for (var du = 0; du < width; du++)
                    {
                        used[((w + dw) * nu) + u + du] = true;
                    }
                }

                Emit(geometry, c, first, axis, ua, wa, positive, face, slice, u0 + u, w0 + w, width, height);
            }
        }
    }

    private static bool RowMatches(Face?[] mask, bool[] used, int nu, int u, int w, int width, Face first)
    {
        for (var k = 0; k < width; k++)
        {
            var at = (w * nu) + u + k;
            if (used[at] || mask[at] is not { } other || other != first)
            {
                return false;
            }
        }

        return true;
    }

    // One quad: the voxel faces from (u, w) over width x height, on the plane between slices.
    private static void Emit(FarGeometry geometry, Context c, Face made, int axis, int ua, int wa, bool positive, BlockFace face, int slice, int u, int w, int width, int height)
    {
        // The plane's coordinate along the axis: the far side of the voxel when the face looks along +axis.
        var plane = slice + (positive ? 1 : 0);
        var cell = (float)c.Cell;
        Vector3 P(int pu, int pw)
        {
            var p = new int[3];
            p[axis] = plane;
            p[ua] = pu;
            p[wa] = pw;
            return new Vector3(c.X0 + (p[0] * cell), (c.Volume.YBase + p[1]) * cell, c.Z0 + (p[2] * cell));
        }

        var a = P(u, w);
        var b = P(u + width, w);
        var d = P(u, w + height);
        var e = P(u + width, w + height);
        var normal = axis switch
        {
            0 => positive ? Vector3.UnitX : -Vector3.UnitX,
            1 => positive ? Vector3.UnitY : -Vector3.UnitY,
            _ => positive ? Vector3.UnitZ : -Vector3.UnitZ,
        };
        var textured = c.Textured && made.Bucket != FarBucket.Water;
        if (textured)
        {
            // One voxel face with the atlas tile of its block and facing: the top's texture along x and z, a side's top edge up (the plane's w axis is y).
            var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(BlockAtlasLayout.CellFor(made.Block, face), ChunkMesher.IsGrey(made.Block, c.Grey));
            if (axis == 1)
            {
                geometry.Quad(made.Bucket, (a, new Vector2(u0, v0)), (b, new Vector2(u1, v0)), (e, new Vector2(u1, v1)), (d, new Vector2(u0, v1)), made.Colour, normal);
            }
            else
            {
                geometry.Quad(made.Bucket, (a, new Vector2(u0, v1)), (b, new Vector2(u1, v1)), (e, new Vector2(u1, v0)), (d, new Vector2(u0, v0)), made.Colour, normal);
            }

            return;
        }

        // The noise: one texel to a voxel, wrapped.
        var span = 1f / (cell * NoiseSize);
        Vector2 Uv(Vector3 p) => axis == 1 ? new Vector2(p.X * span, p.Z * span) : axis == 0 ? new Vector2(p.Z * span, p.Y * span) : new Vector2(p.X * span, p.Y * span);
        geometry.Quad(made.Bucket, (a, Uv(a)), (b, Uv(b)), (e, Uv(e)), (d, Uv(d)), made.Colour, normal);
    }
}
