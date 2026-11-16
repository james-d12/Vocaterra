using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// What the grass layer is built from (B18): the quality's share of the density, the data, how to find the biome of a column, the sky's darkening and the light band the chunk's mesh was baked in.
/// </summary>
/// <param name="Cap">The most density this mesh holds, 0 to 1 (the quality's cap, <c>QualityPreset.GrassCapPercent</c>); the slider's own value is applied when the layer is drawn, so moving it never builds a mesh again.</param>
/// <param name="Data">The tops and the weights by biome.</param>
/// <param name="BiomeAt">The id of the biome of a block column, or <see langword="null"/> for a world that has no place query (every column then has <see cref="GrassBiomes.WeightOf"/> of <see langword="null"/>).</param>
/// <param name="Sky">The surface of the chunk column, to darken a tuft that stands under a roof of ground (B12), or <see langword="null"/>.</param>
/// <param name="Band">The light band whose sun is baked into the colours (the same as the chunk's own mesh, which the layer is drawn with).</param>
/// <param name="Grey">Whether the block's tuft is drawn from the grey copy of the atlas (the greyscale view).</param>
public sealed record GrassLayerOptions(float Cap, GrassBiomes Data, Func<int, int, string?>? BiomeAt, SkySurface? Sky, LightBand Band, bool Grey);

/// <summary>
/// The decorative grass of one chunk (B18): the quads of its tufts, in four quadrants of 8 x 8 columns, each quadrant ordered by tier (a tuft's rank in the density, so the first tiers hold the tufts a low
/// slider shows), and for each quadrant the number of quads that the first <c>t</c> tiers hold. The renderer draws, per quadrant, a prefix of its quads: the slider picks the tiers, the distance thins them.
/// </summary>
/// <param name="Mesh">The quads: the four quadrants one after another, each two quads a tuft.</param>
/// <param name="QuadrantStart">The first quad of each quadrant, and the total at the end (five numbers).</param>
/// <param name="Cumulative">For each quadrant, <see cref="GrassMesher.Tiers"/> + 1 numbers: the quads of the quadrant in the first <c>t</c> tiers.</param>
public sealed record GrassMesh(MeshBuffer Mesh, int[] QuadrantStart, int[] Cumulative)
{
    /// <summary>The number of quads the layer holds.</summary>
    public int Quads => QuadrantStart[GrassMesher.Quadrants];

    /// <summary>The quads of a quadrant that its first <paramref name="tiers"/> tiers hold.</summary>
    /// <param name="quadrant">The quadrant, 0 to 3.</param>
    /// <param name="tiers">How many tiers to count, 0 to <see cref="GrassMesher.Tiers"/>.</param>
    public int QuadsIn(int quadrant, int tiers) => Cumulative[(quadrant * (GrassMesher.Tiers + 1)) + Math.Clamp(tiers, 0, GrassMesher.Tiers)];
}

/// <summary>
/// Builds the decorative grass layer (B18, D-game-34 item 34): on the top face of a grass block (the tops named in <see cref="GrassBiomes"/>) whose cell above is air, a tuft of two crossed cut-out quads of the tall grass's
/// picture, three heights and three tints, jittered inside its own cell, placed where a hash of the cell says (a function of the position alone: it stands still as the eye moves and is the same in every run), with the
/// probability of the biome's weight. It is render only: no block, no collision, never edited or saved; a cell with a plant, a path, a farm, a crop, snow, a door or any block above holds none, because only a grass block's top
/// with open air over it carries one.
/// </summary>
public static class GrassMesher
{
    /// <summary>How many density tiers the mesh is ordered in (steps of 5 percent of the slider; the slider moves in tens).</summary>
    public const int Tiers = 20;

    /// <summary>The quadrants of a chunk: 8 x 8 columns each.</summary>
    public const int Quadrants = 4;

    private const int Half = VoxelChunk.Size / 2;

    private readonly record struct Tuft(int Quadrant, int Tier, int X, int Y, int Z);

    /// <summary>
    /// The hash of a cell the tuft's presence, variety and jitter come from (three values in, a stable 32-bit number out): the same cell gives the same number in every run, on every machine.
    /// </summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public static uint CellHash(int x, int y, int z) => BlockArtNature.Mix((uint)x, (uint)y + 0x6a09, (uint)z + 0xbb67);

    /// <summary>The rank of a cell in the density, 0 to just under 1: the cell carries a tuft at a slider share of <c>s</c> when <c>rank &lt; weight * s</c>.</summary>
    /// <param name="hash">The cell's hash (<see cref="CellHash"/>).</param>
    public static float RankOf(uint hash) => ((hash >> 8) & 0xFFFF) / 65536f;

    /// <summary>
    /// Builds the layer of the chunk at chunk coordinates (<paramref name="chunkX"/>, <paramref name="chunkY"/>, <paramref name="chunkZ"/>), or <see langword="null"/> when it holds no tuft.
    /// </summary>
    /// <param name="blocks">The blocks.</param>
    /// <param name="chunkX">The chunk's x.</param>
    /// <param name="chunkY">The chunk's y.</param>
    /// <param name="chunkZ">The chunk's z.</param>
    /// <param name="options">What the layer is built from.</param>
    public static GrassMesh? Build(IBlockSource blocks, int chunkX, int chunkY, int chunkZ, GrassLayerOptions options)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Cap <= 0f)
        {
            return null;
        }

        var x0 = chunkX * VoxelChunk.Size;
        var y0 = chunkY * VoxelChunk.Size;
        var z0 = chunkZ * VoxelChunk.Size;
        var weights = new float[16]; // the biome's weight at the middle of each 4 x 4 block of the chunk, read when first needed
        Array.Fill(weights, -1f);
        var tufts = new List<Tuft>();
        for (var lz = 0; lz < VoxelChunk.Size; lz++)
        {
            for (var lx = 0; lx < VoxelChunk.Size; lx++)
            {
                for (var ly = 0; ly < VoxelChunk.Size; ly++)
                {
                    var x = x0 + lx;
                    var y = y0 + ly;
                    var z = z0 + lz;
                    if (!options.Data.IsTop(blocks.Get(x, y, z)) || blocks.Get(x, y + 1, z) != BlockId.Air)
                    {
                        continue;
                    }

                    var rank = RankOf(CellHash(x, y, z));
                    var cell = ((lz >> 2) * 4) + (lx >> 2);
                    if (weights[cell] < 0f)
                    {
                        var biome = options.BiomeAt?.Invoke(x0 + ((lx >> 2) << 2) + 2, z0 + ((lz >> 2) << 2) + 2);
                        weights[cell] = options.Data.WeightOf(options.BiomeAt is null ? null : biome ?? string.Empty);
                    }

                    var reach = weights[cell] * options.Cap; // the densest the mesh holds: a tuft is in when its rank is below it
                    if (reach <= 0f || rank >= reach)
                    {
                        continue;
                    }

                    var tier = Math.Min(Tiers - 1, (int)(rank / reach * Tiers));
                    tufts.Add(new Tuft((lz >= Half ? 2 : 0) + (lx >= Half ? 1 : 0), tier, x, y, z));
                }
            }
        }

        if (tufts.Count == 0)
        {
            return null;
        }

        tufts.Sort(static (a, b) => a.Quadrant != b.Quadrant ? a.Quadrant.CompareTo(b.Quadrant) : a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : a.X != b.X ? a.X.CompareTo(b.X) : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.Y.CompareTo(b.Y));
        var mesh = new MeshBuffer();
        var start = new int[Quadrants + 1];
        var cumulative = new int[Quadrants * (Tiers + 1)];
        var sun = LightBands.BakedSun(options.Band);
        var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(BlockAtlasLayout.CellFor(BlockId.TallGrass, BlockFace.North), options.Grey);
        var quadrantQuads = new int[Quadrants];
        foreach (var tuft in tufts)
        {
            var before = mesh.FaceCount;
            AddTuft(mesh, tuft, sun, options.Sky, u0, v0, u1, v1);
            quadrantQuads[tuft.Quadrant] += mesh.FaceCount - before;
        }

        // The quads of each quadrant are counted by tier: the cumulative row of a quadrant says how many quads its first t tiers hold.
        var index = 0;
        for (var quadrant = 0; quadrant < Quadrants; quadrant++)
        {
            start[quadrant] = Sum(quadrantQuads, quadrant);
            var row = quadrant * (Tiers + 1);
            var count = 0;
            for (var tier = 0; tier < Tiers; tier++)
            {
                cumulative[row + tier] = count;
                while (index < tufts.Count && tufts[index].Quadrant == quadrant && tufts[index].Tier == tier)
                {
                    count += 2;
                    index++;
                }
            }

            cumulative[row + Tiers] = count;
        }

        start[Quadrants] = mesh.FaceCount;
        return new GrassMesh(mesh, start, cumulative);
    }

    private static int Sum(int[] counts, int below)
    {
        var sum = 0;
        for (var i = 0; i < below; i++)
        {
            sum += counts[i];
        }

        return sum;
    }

    // One tuft: two quads crossing on the diagonals of the cell's top, jittered and a little narrower than the cell so it stays over its own block (a path or a doorstep beside it is never covered),
    // one of three heights and one of three tints from the hash, brighter at the tip than at the root, in the band's sun and the sky's light.
    private static void AddTuft(MeshBuffer buffer, Tuft tuft, (float R, float G, float B) sun, SkySurface? sky, float u0, float v0, float u1, float v1)
    {
        var h = CellHash(tuft.X, tuft.Y, tuft.Z);
        var h2 = BlockArtNature.Mix((uint)tuft.Z, (uint)tuft.X + 0x3c6e, (uint)tuft.Y + 0xa54f);
        var jitterX = (((h2 & 15) / 15f) - 0.5f) * 0.24f; // at most 0.12 off the middle, and half at most 0.37: a tuft stays inside its own cell
        var jitterZ = ((((h2 >> 4) & 15) / 15f) - 0.5f) * 0.24f;
        var variant = (int)((h2 >> 8) % 3);
        var height = (variant switch { 0 => 0.34f, 1 => 0.5f, _ => 0.68f }) + ((((h2 >> 12) & 7) / 7f) * 0.1f);
        var half = 0.31f + ((((h2 >> 16) & 3) / 3f) * 0.06f);
        var tint = ((h >> 28) % 3) switch { 0 => (1f, 1f, 1f), 1 => (0.93f, 1f, 0.86f), _ => (0.86f, 0.97f, 0.92f) };
        var light = sky is null ? 1f : sky.FactorAt(tuft.X, tuft.Y + 1, tuft.Z);
        var root = MeshVertex.Pack(Channel(184, sun.R, tint.Item1, light), Channel(206, sun.G, tint.Item2, light), Channel(196, sun.B, tint.Item3, light));
        var tip = MeshVertex.Pack(Channel(255, sun.R, tint.Item1, light), Channel(252, sun.G, tint.Item2, light), Channel(240, sun.B, tint.Item3, light));
        var cx = tuft.X + 0.5f + jitterX;
        var cz = tuft.Z + 0.5f + jitterZ;
        var y = tuft.Y + 1f - 0.02f; // sunk a hair into the block's top so no gap shows under the root
        for (var plane = 0; plane < 2; plane++)
        {
            var ax = plane == 0 ? -half : half;
            var az = -half;
            var bx = -ax;
            var bz = half;
            buffer.AddQuad(
                new MeshVertex(cx + ax, y, cz + az, root, u0, v1),
                new MeshVertex(cx + bx, y, cz + bz, root, u1, v1),
                new MeshVertex(cx + bx, y + height, cz + bz, tip, u1, v0),
                new MeshVertex(cx + ax, y + height, cz + az, tip, u0, v0));
        }
    }

    private static byte Channel(float baseValue, float sun, float tint, float light) => (byte)Math.Clamp(baseValue * sun * tint * light, 0f, 255f);
}
