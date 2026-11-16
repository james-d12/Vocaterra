using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Diagnostics;
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

/// <summary>One built far-view tile: the vertices of its boxes (absolute world positions, each face its own four vertices in the colour it is lit, with a coordinate into the noise texture), their triangles in three runs (opaque, lit, water), its box for culling and what it cost to make.</summary>
/// <param name="Key">The tile.</param>
/// <param name="Vertices">The vertices, absolute world positions.</param>
/// <param name="Indices16">The triangles' indices when the tile has few enough vertices for 16 bits, otherwise <see langword="null"/>.</param>
/// <param name="Indices32">The triangles' indices when it has not, otherwise <see langword="null"/>.</param>
/// <param name="OpaqueIndices">How many of the first indices are the opaque faces'.</param>
/// <param name="LitIndices">How many indices after them are the self-lit faces'; the water's are the rest.</param>
/// <param name="Box">The box round every vertex.</param>
/// <param name="BuildMilliseconds">The time the sampling and the meshing took.</param>
public sealed record FarTileMesh(FarTileKey Key, VertexPositionColorTexture[] Vertices, ushort[]? Indices16, int[]? Indices32, int OpaqueIndices, int LitIndices, BoundingBox Box, double BuildMilliseconds)
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

    /// <summary>Adds a quad (four corners in order round it, with their noise coordinates) facing <paramref name="normal"/>.</summary>
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
    public FarTileMesh Finish(FarTileKey key, double milliseconds)
    {
        var total = _vertices[0].Count + _vertices[1].Count + _vertices[2].Count;
        var vertices = new VertexPositionColorTexture[total];
        var indices = new int[_indices[0].Count + _indices[1].Count + _indices[2].Count];
        int v = 0;
        int ix = 0;
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

            return new FarTileMesh(key, vertices, narrow, null, _indices[0].Count, _indices[1].Count, box, milliseconds);
        }

        return new FarTileMesh(key, vertices, null, indices, _indices[0].Count, _indices[1].Count, box, milliseconds);
    }
}

/// <summary>
/// Builds a far-view tile from level-of-detail columns (B07c; D-game-34 item 25, the way the Distant Horizons mod draws its far land): each column is a short list of runs of one block (<see cref="LodSegment"/>), and the tile is the
/// boxes of those runs, as blocks always. A run's top is a quad where nothing touches it from above, its underside where nothing touches it from below, its sides where the neighbouring column has no solid block at that height; runs of faces
/// of one colour that meet are one quad, so flat land and a calm sea are a few large faces. Each face is coloured from its block's own art (the average of the atlas tile), lit as the chunk mesher lights a face of that facing and by the sky
/// light of the data, with a little darkening at the foot of a step; the noise texture the renderer binds gives every cell its own small variation. Water is its own translucent list, darker with depth; blocks that give light are another, drawn in
/// no light tint. A column on the tile's edge has a skirt on its outer side, so two levels that meet leave no crack. Only boxes with whole-block heights and edges on the level's cells are made: nothing is smooth or sloped.
/// </summary>
public static class FarTileMesher
{
    /// <summary>The side of the noise texture in texels; one texel is one cell of the tile's level.</summary>
    public const int NoiseSize = 64;

    /// <summary>The factor that brings the noise texture's mean brightness (see <see cref="FarNoise"/>) back to 1.</summary>
    public const float NoiseGain = 1.075f;

    // Water this deep is opaque (and what lies under it is not drawn).
    private const int DeepWater = 12;

    /// <summary>Builds a tile.</summary>
    /// <param name="source">The columns.</param>
    /// <param name="settings">The far view's settings (the cells and the tile's size).</param>
    /// <param name="key">The tile.</param>
    /// <param name="greyKinds">The block kinds drawn grey (the greyscale view, <see cref="ChunkMesher.KindBit"/>): the land they make is drawn grey from afar too, so the seam with the chunks has one colour.</param>
    public static FarTileMesh Build(ILodColumnSource source, FarViewSettings settings, FarTileKey key, UInt128 greyKinds = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);
        var started = MonotonicClock.Milliseconds();
        var n = settings.TileCells;
        // One cell beyond the tile on every side, so a step at the edge is the step the neighbouring tile sees and the two draw the same wall.
        var grid = new LodGrid(key.Level, (key.Tx * n) - 1, (key.Tz * n) - 1, n + 2, n + 2);
        source.Fill(grid);
        return Mesh(grid, settings, key, greyKinds, started);
    }

    /// <summary>The tile of a grid of columns that has one cell of border all round (the first interior cell is at (1, 1)).</summary>
    /// <param name="grid">The columns, (<see cref="FarViewSettings.TileCells"/> + 2) cells a side.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="key">The tile.</param>
    /// <param name="greyKinds">The block kinds drawn grey.</param>
    /// <param name="started">The clock when the work began.</param>
    public static FarTileMesh Mesh(LodGrid grid, FarViewSettings settings, FarTileKey key, UInt128 greyKinds = default, double? started = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settings);
        var begin = started ?? MonotonicClock.Milliseconds();
        var n = settings.TileCells;
        var cell = settings.CellOf(key.Level);
        var geometry = new FarGeometry();
        var context = new Context(grid, n, cell, key.MinX(settings), key.MinZ(settings), settings.SkirtOf(key.Level), greyKinds, Lights.Make());
        AddHorizontal(geometry, context);
        AddWalls(geometry, context);
        return geometry.Finish(key, MonotonicClock.Milliseconds() - begin);
    }

    /// <summary>The colour a block's top face has in a light, with the sky light of the data and no other shading: the average of its atlas tile times the light.</summary>
    /// <param name="block">The block.</param>
    /// <param name="light">The light on it, the multiplier of each channel (<see cref="ChunkMesher.SunTintFor"/>).</param>
    /// <param name="sky">The sky light, 0 to 15.</param>
    /// <param name="greyKinds">The block kinds drawn grey.</param>
    public static Color TopColour(BlockId block, (float R, float G, float B) light, int sky = 15, UInt128 greyKinds = default) =>
        Pack(Rgb(block, false, greyKinds), light, SkyShade(sky) * NoiseGain);

    private sealed record Context(LodGrid Grid, int N, int Cell, int X0, int Z0, int Skirt, UInt128 Grey, Lights Light)
    {
        public ReadOnlySpan<LodSegment> At(int i, int j) => Grid.Column(i + 1, j + 1);
    }

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

    // Water is the art's colour, deeper and darker with depth, more opaque with depth until it hides the bed.
    private static (Color Colour, float Alpha) WaterOf(int depth, (float R, float G, float B) light, UInt128 greyKinds)
    {
        var water = Rgb(BlockId.Water, false, greyKinds);
        var t = Math.Clamp((depth - 1) / 14f, 0f, 1f);
        var shade = 1f - (0.45f * t);
        var alpha = Math.Clamp(0.55f + (0.45f * Math.Min(1f, (depth - 1) / (float)(DeepWater - 1))), 0f, 1f);
        var colour = Pack(water, light, shade * NoiseGain);
        return (new Color(colour.R, colour.G, colour.B, (byte)(alpha * 255f)), alpha);
    }

    private readonly record struct Lights((float R, float G, float B) Up, (float R, float G, float B) Down, (float R, float G, float B) East, (float R, float G, float B) West, (float R, float G, float B) South, (float R, float G, float B) North)
    {
        // The tints the chunk mesher's faces take, by the same function.
        public static Lights Make() => new(ChunkMesher.SunTintFor(0, 1, 0), ChunkMesher.SunTintFor(0, -1, 0), ChunkMesher.SunTintFor(1, 0, 0), ChunkMesher.SunTintFor(-1, 0, 0), ChunkMesher.SunTintFor(0, 0, 1), ChunkMesher.SunTintFor(0, 0, -1));
    }

    private static Color Pack((float R, float G, float B) colour, (float R, float G, float B) light, float factor) =>
        new((byte)Math.Clamp(colour.R * light.R * factor, 0f, 255f), (byte)Math.Clamp(colour.G * light.G * factor, 0f, 255f), (byte)Math.Clamp(colour.B * light.B * factor, 0f, 255f), (byte)255);

    private static readonly (float R, float G, float B) NoTint = (1f, 1f, 1f);

    // The colour of a face of a run: a block that gives light is drawn as it is, the rest in the light of the face and the sky (the sky light of the air in front of the face).
    private static (Color Colour, FarBucket Bucket) FaceOf(in LodSegment run, bool side, (float R, float G, float B) tint, UInt128 greyKinds, float shade = 1f, int? sky = null)
    {
        if (run.Light > 0)
        {
            return (Pack(Rgb(run.Block, side, greyKinds), NoTint, 1f), FarBucket.Lit);
        }

        return (Pack(Rgb(run.Block, side, greyKinds), tint, SkyShade(sky ?? run.Sky) * shade * NoiseGain), FarBucket.Opaque);
    }

    // The sky light in front of the sides of a column's ground: that of the air over the ground's top (a run under the ground's top layer has no sky over it in its own column, but its side faces the open air).
    private static int GroundSky(ReadOnlySpan<LodSegment> column, int groundTop)
    {
        foreach (var run in column)
        {
            if (run.Top == groundTop)
            {
                return run.Sky;
            }
        }

        return 15;
    }

    // Which runs of a column are hidden under deep water: those whose top is more than the depth below the water's top.
    private static int DeepFloor(ReadOnlySpan<LodSegment> column)
    {
        foreach (var run in column)
        {
            if (run.Block == BlockId.Water)
            {
                return run.Top - DeepWater + 1;
            }
        }

        return int.MinValue;
    }

    // ---- the tops and undersides: one quad for each run of cells of the same height and colour

    private readonly record struct Flat(FarBucket Bucket, int Y, int I, int J, Color Colour, bool Up);

    private static void AddHorizontal(FarGeometry geometry, Context c)
    {
        var faces = new List<Flat>(c.N * c.N);
        for (var j = 0; j < c.N; j++)
        {
            for (var i = 0; i < c.N; i++)
            {
                var column = c.At(i, j);
                var floor = DeepFloor(column);
                for (var s = 0; s < column.Length; s++)
                {
                    var run = column[s];
                    var above = s > 0 ? column[s - 1] : default;
                    var touchesAbove = s > 0 && above.Bottom == run.Top + 1;
                    if (run.Block == BlockId.Water)
                    {
                        if (!touchesAbove)
                        {
                            var (colour, _) = WaterOf(run.Thickness, c.Light.Up, c.Grey);
                            faces.Add(new Flat(FarBucket.Water, run.Top + 1, i, j, colour, true));
                        }

                        continue;
                    }

                    // The top, unless something solid touches it, or deep water lies over it.
                    var hidden = touchesAbove && above.Block != BlockId.Water;
                    var deep = run.Top < floor;
                    if (!hidden && !deep)
                    {
                        var (colour, bucket) = FaceOf(run, false, c.Light.Up, c.Grey, 1f - Occlusion(c, i, j, run.Top + 1));
                        faces.Add(new Flat(bucket, run.Top + 1, i, j, colour, true));
                    }

                    // The underside, where there is air under it (the last run's is the world's floor).
                    if (s < column.Length - 1 && column[s + 1].Top + 1 != run.Bottom && !deep)
                    {
                        var (colour, bucket) = FaceOf(run, true, c.Light.Down, c.Grey);
                        faces.Add(new Flat(bucket, run.Bottom, i, j, colour, false));
                    }
                }
            }
        }

        faces.Sort(static (a, b) => a.Bucket != b.Bucket ? a.Bucket.CompareTo(b.Bucket) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Up != b.Up ? a.Up.CompareTo(b.Up) : 0);
        var colours = new Color[c.N * c.N];
        var has = new bool[c.N * c.N];
        var used = new bool[c.N * c.N];
        var start = 0;
        while (start < faces.Count)
        {
            var end = start;
            while (end < faces.Count && faces[end].Bucket == faces[start].Bucket && faces[end].Y == faces[start].Y && faces[end].Up == faces[start].Up)
            {
                end++;
            }

            Array.Clear(has);
            Array.Clear(used);
            for (var k = start; k < end; k++)
            {
                var face = faces[k];
                colours[(face.J * c.N) + face.I] = face.Colour;
                has[(face.J * c.N) + face.I] = true;
            }

            Greedy(geometry, c, faces[start], colours, has, used);
            start = end;
        }
    }

    private static readonly (int Di, int Dj)[] Around = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    // The foot of a step: the darker the more solid blocks stand beside the face at its own height (the corner shadow the chunks' occlusion gives).
    private static float Occlusion(Context c, int i, int j, int y)
    {
        var occlusion = 0f;
        foreach (var (di, dj) in Around)
        {
            foreach (var run in c.Grid.Column(i + 1 + di, j + 1 + dj))
            {
                if (run.Block != BlockId.Water && y >= run.Bottom && y <= run.Top)
                {
                    occlusion += Math.Min(run.Top + 1 - y, 4) * 0.04f;
                    break;
                }
            }
        }

        return Math.Min(0.28f, occlusion);
    }

    private static void Greedy(FarGeometry geometry, Context c, Flat sample, Color[] colours, bool[] has, bool[] used)
    {
        var n = c.N;
        for (var j = 0; j < n; j++)
        {
            for (var i = 0; i < n; i++)
            {
                var at = (j * n) + i;
                if (!has[at] || used[at])
                {
                    continue;
                }

                var colour = colours[at];
                var width = 1;
                while (i + width < n && has[at + width] && !used[at + width] && colours[at + width] == colour)
                {
                    width++;
                }

                var height = 1;
                while (j + height < n && RowMatches(has, used, colours, n, i, j + height, width, colour))
                {
                    height++;
                }

                for (var dj = 0; dj < height; dj++)
                {
                    for (var di = 0; di < width; di++)
                    {
                        used[((j + dj) * n) + i + di] = true;
                    }
                }

                float xa = c.X0 + (i * c.Cell);
                float xb = c.X0 + ((i + width) * c.Cell);
                float za = c.Z0 + (j * c.Cell);
                float zb = c.Z0 + ((j + height) * c.Cell);
                float y = sample.Y;
                var span = 1f / (c.Cell * NoiseSize);
                Vector2 T(float x, float z) => new(x * span, z * span);
                geometry.Quad(sample.Bucket, (new Vector3(xa, y, za), T(xa, za)), (new Vector3(xb, y, za), T(xb, za)), (new Vector3(xb, y, zb), T(xb, zb)), (new Vector3(xa, y, zb), T(xa, zb)), colour, sample.Up ? Vector3.UnitY : -Vector3.UnitY);
            }
        }
    }

    private static bool RowMatches(bool[] has, bool[] used, Color[] colours, int n, int i, int j, int width, Color colour)
    {
        for (var k = 0; k < width; k++)
        {
            var at = (j * n) + i + k;
            if (!has[at] || used[at] || colours[at] != colour)
            {
                return false;
            }
        }

        return true;
    }

    // ---- the sides: the part of each run that the neighbouring column does not cover, and a skirt on the tile's outer edge

    private readonly record struct Run(int Start, int YBottom, int YTop, Color Colour, FarBucket Bucket);

    private static void AddWalls(FarGeometry geometry, Context c)
    {
        // Direction d: 0 east (+x), 1 west, 2 south (+z), 3 north. A line of cells runs along the wall's own axis.
        var n = c.N;
        var open = new List<Run>(8);
        var following = new List<Run>(8);
        var segments = new List<Run>(8);
        Span<(int Bottom, int Top)> occluders = stackalloc (int, int)[LodGrid.MaxSegments];
        Span<(int Bottom, int Top)> pieces = stackalloc (int, int)[LodGrid.MaxSegments * 2];
        for (var d = 0; d < 4; d++)
        {
            var alongX = d >= 2;
            var (di, dj) = d switch { 0 => (1, 0), 1 => (-1, 0), 2 => (0, 1), _ => (0, -1) };
            var tint = d switch { 0 => c.Light.East, 1 => c.Light.West, 2 => c.Light.South, _ => c.Light.North };
            for (var line = 0; line < n; line++)
            {
                open.Clear();
                for (var along = 0; along <= n; along++)
                {
                    segments.Clear();
                    if (along < n)
                    {
                        var i = alongX ? along : line;
                        var j = alongX ? line : along;
                        var self = c.At(i, j);
                        var neighbour = c.At(i + di, j + dj);
                        var edge = (di == 1 && i == n - 1) || (di == -1 && i == 0) || (dj == 1 && j == n - 1) || (dj == -1 && j == 0);
                        var floor = DeepFloor(self);
                        var groundTop = LodMerge.GroundTop(self);
                        var groundSky = GroundSky(self, groundTop);
                        var skirted = edge && floor == int.MinValue;
                        foreach (var run in self)
                        {
                            var water = run.Block == BlockId.Water;
                            // What of the neighbour hides this run's side: its solid blocks (and, for water, its water too). The skirt: along the ground the neighbour's cover stops short of the ground's top by the skirt's depth.
                            var count = 0;
                            var ground = skirted && run.Top <= groundTop;
                            foreach (var other in neighbour)
                            {
                                if (other.Block == BlockId.Water && !water)
                                {
                                    continue;
                                }

                                var top = ground ? Math.Min(other.Top, groundTop - c.Skirt) : other.Top;
                                if (top >= other.Bottom)
                                {
                                    occluders[count++] = (other.Bottom, top);
                                }
                            }

                            var found = Uncovered(run.Bottom, run.Top, occluders[..count], pieces);
                            if (found == 0)
                            {
                                continue;
                            }

                            var (colour, bucket) = water ? (WaterOf(run.Thickness, tint, c.Grey).Colour, FarBucket.Water) : FaceOf(run, true, tint, c.Grey, 1f, run.Top <= groundTop ? groundSky : run.Sky);
                            for (var p = 0; p < found; p++)
                            {
                                var (bottom, top) = pieces[p];
                                if (!water)
                                {
                                    bottom = Math.Max(bottom, floor);
                                }

                                if (top >= bottom)
                                {
                                    segments.Add(new Run(along, bottom, top, colour, bucket));
                                }
                            }
                        }
                    }

                    // A run of the same extent and colour on the next cell goes on; the others end here and are one quad each.
                    following.Clear();
                    foreach (var segment in segments)
                    {
                        var at = open.FindIndex(r => r.YBottom == segment.YBottom && r.YTop == segment.YTop && r.Colour == segment.Colour && r.Bucket == segment.Bucket);
                        if (at >= 0)
                        {
                            following.Add(open[at]);
                            open.RemoveAt(at);
                        }
                        else
                        {
                            following.Add(segment);
                        }
                    }

                    foreach (var ended in open)
                    {
                        EmitWall(geometry, c, ended, along, d, line);
                    }

                    (open, following) = (following, open);
                }
            }
        }
    }

    // The parts of the blocks from bottom to top (inclusive) that none of the covers' blocks fill.
    private static int Uncovered(int bottom, int top, ReadOnlySpan<(int Bottom, int Top)> covers, Span<(int Bottom, int Top)> into)
    {
        var count = 0;
        into[count++] = (bottom, top);
        Span<(int, int)> kept = stackalloc (int, int)[into.Length];
        foreach (var (cb, ct) in covers)
        {
            var next = 0;
            for (var k = 0; k < count; k++)
            {
                var (b, t) = into[k];
                if (ct < b || cb > t)
                {
                    kept[next++] = (b, t);
                    continue;
                }

                if (b < cb)
                {
                    kept[next++] = (b, cb - 1);
                }

                if (t > ct)
                {
                    kept[next++] = (ct + 1, t);
                }
            }

            if (next > into.Length)
            {
                next = into.Length;
            }

            kept[..next].CopyTo(into);
            count = next;
            if (count == 0)
            {
                break;
            }
        }

        return count;
    }

    private static void EmitWall(FarGeometry geometry, Context c, Run run, int end, int d, int line)
    {
        float a = run.Start * c.Cell;
        float b = end * c.Cell;
        float yb = run.YBottom;
        float yt = run.YTop + 1;
        var span = 1f / (c.Cell * NoiseSize);
        if (d < 2)
        {
            float x = c.X0 + ((d == 0 ? line + 1 : line) * c.Cell);
            float za = c.Z0 + a;
            float zb = c.Z0 + b;
            Vector2 T(float z, float y) => new(z * span, y * span);
            geometry.Quad(run.Bucket, (new Vector3(x, yt, za), T(za, yt)), (new Vector3(x, yt, zb), T(zb, yt)), (new Vector3(x, yb, zb), T(zb, yb)), (new Vector3(x, yb, za), T(za, yb)), run.Colour, d == 0 ? Vector3.UnitX : -Vector3.UnitX);
        }
        else
        {
            float z = c.Z0 + ((d == 2 ? line + 1 : line) * c.Cell);
            float xa = c.X0 + a;
            float xb = c.X0 + b;
            Vector2 T(float x, float y) => new(x * span, y * span);
            geometry.Quad(run.Bucket, (new Vector3(xa, yt, z), T(xa, yt)), (new Vector3(xb, yt, z), T(xb, yt)), (new Vector3(xb, yb, z), T(xb, yb)), (new Vector3(xa, yb, z), T(xa, yb)), run.Colour, d == 2 ? Vector3.UnitZ : -Vector3.UnitZ);
        }
    }
}
