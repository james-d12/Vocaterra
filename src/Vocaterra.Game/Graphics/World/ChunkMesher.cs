using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;
using Vocaterra.Game.Core.Water.Currents;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Water;

namespace Vocaterra.Game.Graphics.World;

/// <summary>Read access to the blocks of a world for the mesher; anything outside the world reads as air unless the source says otherwise.</summary>
public interface IBlockSource
{
    /// <summary>The block at the position.</summary>
    BlockId Get(int x, int y, int z);

    /// <summary>The level of the water at the position (read only where <see cref="Get"/> says water); a source unless the source holds levels.</summary>
    WaterLevel GetWater(int x, int y, int z) => WaterLevel.Source;

    /// <summary>The way the water at the position runs (the surface's ripples follow it); zero for water that does not move.</summary>
    FlowVector FlowAt(int x, int y, int z) => FlowVector.Zero;
}

/// <summary>
/// The blocks of a <see cref="VoxelGrid"/> as a mesher source. The floor under the world (y below zero) reads as stone so the underside of the
/// world is not meshed; the other three sides read as air, so the edge of the world shows its layers.
/// </summary>
/// <param name="Grid">The grid to read.</param>
/// <param name="Currents">The river's currents (still-looking source water in a river runs downstream), or <see langword="null"/> for none.</param>
public sealed record GridBlockSource(VoxelGrid Grid, RiverCurrents? Currents = null) : IBlockSource
{
    /// <inheritdoc />
    public BlockId Get(int x, int y, int z) => y < 0 ? BlockId.Stone : Grid.GetBlock(x, y, z);

    /// <inheritdoc />
    public WaterLevel GetWater(int x, int y, int z) => Grid.GetWater(x, y, z);

    /// <inheritdoc />
    public FlowVector FlowAt(int x, int y, int z)
    {
        var flow = FlowVector.At(Grid, x, y, z);
        if (flow.IsHorizontal || Currents is null || Grid.GetBlock(x, y, z) != BlockId.Water || !Grid.GetWater(x, y, z).IsSource)
        {
            return flow;
        }

        return Currents.At(x, z);
    }
}

/// <summary>How the renderer draws a block kind: a full cube, a liquid, or a small shape of its own.</summary>
public enum BlockShape
{
    /// <summary>Nothing is drawn (air).</summary>
    None,

    /// <summary>A full cube whose faces are culled against neighbours.</summary>
    Cube,

    /// <summary>A liquid cube (water): translucent, its surface a little below the top of the cell.</summary>
    Liquid,

    /// <summary>A bush: a smaller cube.</summary>
    Plant,

    /// <summary>A torch: a stick with a flame.</summary>
    Torch,

    /// <summary>An open door: a thin panel against the side of the doorway.</summary>
    Door,

    /// <summary>A fence: a post with rails to the fences next to it.</summary>
    Fence,

    /// <summary>Undergrowth (S14): two crossed quads through the cell, with cut-out pixels; tall grass, ferns, reeds and flowers.</summary>
    Cross,

    /// <summary>A lantern (S15): a small glass box that glows (drawn without shading), and lights the faces near it.</summary>
    Lantern,

    /// <summary>A pile of goods (S15): a low box on the floor of the cell, the stall's bread, apples or bottles.</summary>
    Pile,
}

/// <summary>
/// What the mesher is asked to do besides the blocks: the quality's choices (S14).
/// </summary>
/// <param name="AmbientOcclusion">Whether every cube vertex is darkened by the solid blocks round it (per-vertex ambient occlusion).</param>
/// <param name="PlantKeepPercent">The percent of the undergrowth that is put in the plant mesh (a stable pick by position), 100 for all of it.</param>
/// <param name="Lights">The lanterns of the world (S15): the faces within <see cref="ChunkMesher.GlowRadius"/> blocks of one take a warm light colour; <see langword="null"/> for none.</param>
/// <param name="Band">The light band to bake (S15, E02): Day, the sun at full strength and the lanterns at half; Dusk, a lower, warmer sun and the lanterns at full strength; Night, a low blue sun and the lanterns at full strength (<see cref="LightBands"/>).</param>
public readonly record struct MeshOptions(bool AmbientOcclusion = true, int PlantKeepPercent = 100, IReadOnlyList<BlockPos>? Lights = null, LightBand Band = LightBand.Day)
{
    /// <summary>The default: ambient occlusion on, every plant, no lanterns.</summary>
    public static MeshOptions Full => new(true, 100);
}

/// <summary>
/// The mesher: turns the blocks of one 16 x 16 x 16 chunk into quads. A face of a cube is emitted unless the block next to it is an opaque cube
/// (or the same kind of transparent block: leaves against leaves, water against water); so a single block gives 6 faces, two blocks side by side 10, a
/// block shut in on all sides none, and stone next to water shows its face to the water while the water does not show its face to the stone.
/// Water goes into the translucent mesh, the undergrowth into the plant mesh (which the renderer draws only near the eye), everything else into the
/// opaque one (leaves and bushes included: their pictures have cut-out pixels, which the renderer's alpha test drops). The light is in the vertex
/// colours (S14): each face is shaded by the sun (from the north-west and above: a sunlit face is warm and bright, a face away from it is darker and
/// bluer), and every cube vertex is darkened by its ambient occlusion (0 to 3 solid blocks among the three that touch the corner). The picture a face
/// takes comes from <see cref="BlockAtlasLayout"/>. Triangles run counter-clockwise seen from outside.
/// </summary>
public static class ChunkMesher
{
    private const int Size = VoxelChunk.Size;

    // The border of blocks read round the chunk (the neighbours a vertex's occlusion looks at).
    private const int Edge = Size + 2;

    /// <summary>The faces in a fixed order.</summary>
    private static readonly BlockFace[] Faces = [BlockFace.East, BlockFace.West, BlockFace.Up, BlockFace.Down, BlockFace.South, BlockFace.North];

    /// <summary>The direction towards the sun (a unit vector: north-west and high), the one the face shades and the sky's sun agree on.</summary>
    public static readonly (float X, float Y, float Z) SunDirection = (-0.5f, 0.72f, -0.48f);

    // The colour multiplier of a face in full sun and of a face turned away from it: the lit side is warm, the shaded side blue (a blue sky fills the shadows).
    private static readonly (float R, float G, float B) SunTint = (1.0f, 0.98f, 0.92f);
    private static readonly (float R, float G, float B) ShadeTint = (0.68f, 0.74f, 0.84f);
    private static readonly (float R, float G, float B) BottomTint = (0.52f, 0.60f, 0.76f);

    // How much of the colour a vertex keeps by its ambient-occlusion level: 3 is an open corner (all of it), 0 a corner shut in by two sides.
    private static readonly float[] AoKeep = [0.50f, 0.66f, 0.82f, 1.0f];

    // The corners of each face, bottom left, bottom right, top right, top left seen from outside (as AddFace builds them), as offsets in the block.
    private static readonly int[][] FaceCorners =
    [
        [1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 1], // east
        [0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 0], // west
        [0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0], // up
        [0, 0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1], // down
        [0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1], // south
        [1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 0], // north
    ];

    private static readonly int[] FaceOrder = [0, 1, 2, 3, 4, 5]; // index into FaceCorners for Faces

    // Per kind flags, so the hot loops do not go through BlockDefinition.
    private const byte IsCube = 1;
    private const byte IsOpaque = 2;
    private const byte IsLeafKind = 4;
    private static readonly byte[] KindFlags = BuildKindFlags();

    private static byte[] BuildKindFlags()
    {
        var flags = new byte[64];
        foreach (var id in Enum.GetValues<BlockId>())
        {
            byte f = 0;
            if (ShapeOf(id) == BlockShape.Cube)
            {
                f |= IsCube;
                if (!BlockDefinition.Get(id).Transparent)
                {
                    f |= IsOpaque;
                }

                if (BlockDefinition.IsLeaves(id))
                {
                    f |= IsLeafKind;
                }
            }

            flags[(int)id] = f;
        }

        return flags;
    }

    /// <summary>How the renderer draws a block kind.</summary>
    public static BlockShape ShapeOf(BlockId id) => id switch
    {
        BlockId.Air => BlockShape.None,
        BlockId.Water => BlockShape.Liquid,
        BlockId.Bush or BlockId.BerryBush => BlockShape.Plant,
        BlockId.TallGrass or BlockId.Fern or BlockId.Reeds or BlockId.YellowFlower or BlockId.WhiteFlower => BlockShape.Cross,
        BlockId.Torch or BlockId.PostTorch => BlockShape.Torch,
        BlockId.Lantern => BlockShape.Lantern,
        BlockId.Loaves or BlockId.Apples or BlockId.Bottles => BlockShape.Pile,
        BlockId.Wheat => BlockShape.Cross,
        BlockId.Door => BlockShape.Door,
        BlockId.Fence => BlockShape.Fence,
        _ => BlockShape.Cube,
    };

    /// <summary>Whether the block is a cube that hides whatever lies behind it: a full cube that is not transparent (leaves are).</summary>
    public static bool IsOpaqueCube(BlockId id) => (KindFlags[(int)id] & IsOpaque) != 0;

    private static bool IsCubeKind(BlockId id) => (KindFlags[(int)id] & IsCube) != 0;

    /// <summary>
    /// The share of sun a face gets, 0 to 1: the dot product of its normal with <see cref="SunDirection"/> over the sun's height, so the top is 1, the
    /// west and north faces (towards the sun) about two thirds, and the east, south and bottom faces 0.
    /// </summary>
    public static float SunShare(BlockFace face)
    {
        var (dx, dy, dz) = face.Normal();
        var dot = (dx * SunDirection.X) + (dy * SunDirection.Y) + (dz * SunDirection.Z);
        return Math.Clamp(dot / SunDirection.Y, 0f, 1f);
    }

    private static (float R, float G, float B) FaceTint(BlockFace face)
    {
        if (face == BlockFace.Down)
        {
            return BottomTint;
        }

        var t = SunShare(face);
        return (ShadeTint.R + ((SunTint.R - ShadeTint.R) * t), ShadeTint.G + ((SunTint.G - ShadeTint.G) * t), ShadeTint.B + ((SunTint.B - ShadeTint.B) * t));
    }

    /// <summary>The tint the sun gives a surface whose normal is (<paramref name="nx"/>, <paramref name="ny"/>, <paramref name="nz"/>) (a unit vector): the figures and the arm are shaded with the same table as the blocks (S15).</summary>
    public static (float R, float G, float B) SunTintFor(float nx, float ny, float nz)
    {
        if (ny < -0.5f)
        {
            return BottomTint;
        }

        var t = Math.Clamp(((nx * SunDirection.X) + (ny * SunDirection.Y) + (nz * SunDirection.Z)) / SunDirection.Y, 0f, 1f);
        return (ShadeTint.R + ((SunTint.R - ShadeTint.R) * t), ShadeTint.G + ((SunTint.G - ShadeTint.G) * t), ShadeTint.B + ((SunTint.B - ShadeTint.B) * t));
    }

    /// <summary>The brightness of a face in the vertex colour (the red channel of its tint): 1.0 on top, less on the sides towards the sun, least underneath.</summary>
    public static float FaceShade(BlockFace face) => FaceTint(face).R;

    /// <summary>Whether a cube of kind <paramref name="self"/> shows its face towards a neighbour of kind <paramref name="neighbour"/>.</summary>
    public static bool FaceVisible(BlockId self, BlockId neighbour)
    {
        if (neighbour == BlockId.Air)
        {
            return true;
        }

        if (IsOpaqueCube(neighbour))
        {
            return false;
        }

        // Liquid against liquid, leaves against leaves (of any tree): the faces between them would only be seen through the block.
        if (neighbour == self && BlockDefinition.Get(self).Transparent)
        {
            return false;
        }

        return !(BlockDefinition.IsLeaves(self) && BlockDefinition.IsLeaves(neighbour));
    }

    // The kinds that are not an opaque cube: a chunk holding one of them is never buried.
    private static readonly ulong NotOpaqueCubes = NonOpaqueKindMask();

    /// <summary>
    /// Whether a chunk is fully buried: all of its 4 096 blocks are opaque cubes and every neighbour block across its six faces is an opaque cube too, so
    /// its mesh would have no face at all. The renderer skips it without meshing. (A chunk at the edge of the world is never buried on the sides
    /// where the world ends, because beyond the edge reads as air.)
    /// </summary>
    public static bool IsBuried(IBlockSource blocks, VoxelChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.NonAirCount != VoxelChunk.Volume || (chunk.KindMask & NotOpaqueCubes) != 0)
        {
            return false;
        }

        var x0 = chunk.Coord.X * VoxelChunk.Size;
        var y0 = chunk.Coord.Y * VoxelChunk.Size;
        var z0 = chunk.Coord.Z * VoxelChunk.Size;
        const int N = VoxelChunk.Size;
        for (var a = 0; a < N; a++)
        {
            for (var b = 0; b < N; b++)
            {
                if (!IsOpaqueCube(blocks.Get(x0 - 1, y0 + a, z0 + b)) || !IsOpaqueCube(blocks.Get(x0 + N, y0 + a, z0 + b))
                    || !IsOpaqueCube(blocks.Get(x0 + a, y0 - 1, z0 + b)) || !IsOpaqueCube(blocks.Get(x0 + a, y0 + N, z0 + b))
                    || !IsOpaqueCube(blocks.Get(x0 + a, y0 + b, z0 - 1)) || !IsOpaqueCube(blocks.Get(x0 + a, y0 + b, z0 + N)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static ulong NonOpaqueKindMask()
    {
        var mask = 0UL;
        foreach (var kind in Enum.GetValues<BlockId>())
        {
            if (!IsOpaqueCube(kind))
            {
                mask |= KindBit(kind);
            }
        }

        return mask;
    }

    /// <summary>The bit of a block kind in a kinds mask (one bit per <see cref="BlockId"/> value; every kind is below 64).</summary>
    public static ulong KindBit(BlockId id) => BlockKinds.Bit(id);

    /// <summary>Whether a block kind is drawn grey: its bit is set in the grey mask (the greyscale A/B, see <see cref="GreyscaleWorld"/>).</summary>
    public static bool IsGrey(BlockId id, ulong greyKinds) => (greyKinds & KindBit(id)) != 0;

    // The blocks of the chunk and one layer round it, read once: the hot loops index this array instead of asking the source for each neighbour.
    private sealed class Neighbourhood
    {
        private readonly BlockId[] _blocks = new BlockId[Edge * Edge * Edge];
        private readonly byte[] _levels = new byte[Edge * Edge * Edge]; // the encoded water level of the water cells (0, a source, elsewhere)

        public Neighbourhood(IBlockSource source, int x0, int y0, int z0)
        {
            var i = 0;
            for (var y = -1; y <= Size; y++)
            {
                for (var z = -1; z <= Size; z++)
                {
                    for (var x = -1; x <= Size; x++)
                    {
                        var id = source.Get(x0 + x, y0 + y, z0 + z);
                        _blocks[i] = id;
                        if (id == BlockId.Water)
                        {
                            _levels[i] = source.GetWater(x0 + x, y0 + y, z0 + z).Encoded;
                        }

                        i++;
                    }
                }
            }
        }

        // The block at a position relative to the chunk's corner, from -1 to 16 on every axis.
        public BlockId Get(int x, int y, int z) => _blocks[((((y + 1) * Edge) + (z + 1)) * Edge) + x + 1];

        // The level of the water cell at a position relative to the chunk's corner.
        public WaterLevel Level(int x, int y, int z) => WaterLevel.FromEncoded(_levels[((((y + 1) * Edge) + (z + 1)) * Edge) + x + 1]);
    }

    /// <summary>
    /// Meshes the chunk at chunk coordinates (<paramref name="chunkX"/>, <paramref name="chunkY"/>, <paramref name="chunkZ"/>). The blocks whose kind
    /// is in <paramref name="greyKinds"/> take their pictures from the grey copies of the atlas; with the default 0 every block is drawn in colour.
    /// The result says which kinds the chunk holds (<see cref="ChunkMeshes.Kinds"/>).
    /// </summary>
    public static ChunkMeshes Build(IBlockSource blocks, int chunkX, int chunkY, int chunkZ, ulong greyKinds = 0, MeshOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var settings = options ?? MeshOptions.Full;
        var opaque = new MeshBuffer();
        var alpha = new MeshBuffer();
        var plants = new MeshBuffer();
        var lit = new MeshBuffer(); // the pieces that are their own light (a lantern's glass, a torch's flame): drawn with no light tint, so the day cycle never dims them (E02)
        var kinds = 0UL;
        var x0 = chunkX * Size;
        var y0 = chunkY * Size;
        var z0 = chunkZ * Size;
        var near = new Neighbourhood(blocks, x0, y0, z0);
        var glow = Glow.For(settings, x0, y0, z0);
        var bakedSun = LightBands.BakedSun(settings.Band); // the sun's multiplier of the band, in every colour that takes the sun's light (E02)
        var sunTint = ((byte)Math.Round(255f * bakedSun.R), (byte)Math.Round(255f * bakedSun.G), (byte)Math.Round(255f * bakedSun.B), (byte)255);
        for (var ly = 0; ly < Size; ly++)
        {
            for (var lz = 0; lz < Size; lz++)
            {
                for (var lx = 0; lx < Size; lx++)
                {
                    var id = near.Get(lx, ly, lz);
                    if (id == BlockId.Air)
                    {
                        continue;
                    }

                    kinds |= KindBit(id);
                    var x = x0 + lx;
                    var y = y0 + ly;
                    var z = z0 + lz;
                    var grey = IsGrey(id, greyKinds);
                    switch (ShapeOf(id))
                    {
                        case BlockShape.Cube:
                            AddCube(opaque, near, id, lx, ly, lz, x, y, z, grey, settings.AmbientOcclusion, glow);
                            break;
                        case BlockShape.Liquid:
                            AddLiquid(alpha, near, blocks, id, lx, ly, lz, x, y, z, grey, bakedSun);
                            break;
                        case BlockShape.Cross:
                            AddCross(plants, id, x, y, z, grey, settings.PlantKeepPercent, bakedSun);
                            break;
                        case BlockShape.Plant:
                            AddAllFaces(opaque, x, y, z, new Box(x + 0.125f, y, z + 0.125f, x + 0.875f, y + 0.75f, z + 0.875f), id, null, grey, sunTint);
                            break;
                        case BlockShape.Torch:
                            AddAllFaces(opaque, x, y, z, new Box(x + 0.4375f, y, z + 0.4375f, x + 0.5625f, y + 0.625f, z + 0.5625f), BlockId.Torch, BlockCell.TorchStick, grey, sunTint);
                            AddAllFaces(lit, x, y, z, new Box(x + 0.375f, y + 0.625f, z + 0.375f, x + 0.625f, y + 0.8125f, z + 0.625f), BlockId.Torch, BlockCell.TorchFlame, grey, White);
                            break;
                        case BlockShape.Lantern:
                            AddAllFaces(lit, x, y, z, new Box(x + 0.3125f, y + 0.1875f, z + 0.3125f, x + 0.6875f, y + 0.8125f, z + 0.6875f), BlockId.Lantern, BlockCell.Lantern, grey, White, true);
                            break;
                        case BlockShape.Pile:
                            AddAllFaces(opaque, x, y, z, new Box(x + 0.1f, y, z + 0.1f, x + 0.9f, y + 0.42f, z + 0.9f), id, null, grey, sunTint);
                            break;
                        case BlockShape.Door:
                            AddDoor(opaque, near, lx, ly, lz, x, y, z, grey, sunTint);
                            break;
                        case BlockShape.Fence:
                            AddFence(opaque, near, lx, ly, lz, x, y, z, grey, sunTint);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        return new ChunkMeshes(opaque, alpha, kinds, plants, lit);
    }

    /// <summary>Appends one face of a box to a mesh, optionally moved and turned about the vertical axis (for the figures).</summary>
    /// <param name="buffer">The mesh to append to.</param>
    /// <param name="face">The face of the box.</param>
    /// <param name="box">The box in the frame of the block or figure.</param>
    /// <param name="origin">The block corner the texture coordinates are measured from (the box lies inside the block at this corner).</param>
    /// <param name="cell">The picture on the face.</param>
    /// <param name="tint">The colour multiplier before the face shade (white for a block).</param>
    /// <param name="turn">The turn and move applied to every vertex.</param>
    /// <param name="grey">Whether the picture is the cell's grey copy (a block drawn grey; figures and overlays are never grey).</param>
    /// <param name="unshaded">Whether the face takes no sun shading (the falling water shows its foam in full light).</param>
    public static void AddFace(MeshBuffer buffer, BlockFace face, in Box box, (float X, float Y, float Z) origin, BlockCell cell, (byte R, byte G, byte B, byte A) tint, Turn turn, bool grey = false, bool unshaded = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(cell, grey);
        var (_, nz) = turn.Rotate(face.Normal().Dx, face.Normal().Dz);
        var faceTint = FaceTint(face);
        var spread = face is BlockFace.Up or BlockFace.Down ? 0f : nz * nz; // the figures turn, so a side's shade follows its direction after the turn
        float sr, sg, sb;
        if (unshaded)
        {
            sr = sg = sb = 1f;
        }
        else if (turn.IsIdentity)
        {
            (sr, sg, sb) = faceTint;
        }
        else
        {
            var shade = face switch
            {
                BlockFace.Up => 1.0f,
                BlockFace.Down => 0.55f,
                _ => 0.68f + (0.14f * spread),
            };
            sr = sg = sb = shade;
        }

        var colour = MeshVertex.Pack((byte)(tint.R * sr), (byte)(tint.G * sg), (byte)(tint.B * sb), tint.A);
        var (ox, oy, oz) = origin;
        MeshVertex V(float x, float y, float z, float fr, float fu)
        {
            var (rx, rz) = turn.Rotate(x, z);
            return new MeshVertex(rx + turn.X, y + turn.Y, rz + turn.Z, colour, u0 + ((u1 - u0) * Math.Clamp(fr, 0f, 1f)), v1 - ((v1 - v0) * Math.Clamp(fu, 0f, 1f)));
        }

        float fx0 = box.X0 - ox, fx1 = box.X1 - ox, fy0 = box.Y0 - oy, fy1 = box.Y1 - oy, fz0 = box.Z0 - oz, fz1 = box.Z1 - oz;
        switch (face)
        {
            case BlockFace.South:
                buffer.AddQuad(V(box.X0, box.Y0, box.Z1, fx0, fy0), V(box.X1, box.Y0, box.Z1, fx1, fy0), V(box.X1, box.Y1, box.Z1, fx1, fy1), V(box.X0, box.Y1, box.Z1, fx0, fy1));
                break;
            case BlockFace.North:
                buffer.AddQuad(V(box.X1, box.Y0, box.Z0, 1 - fx1, fy0), V(box.X0, box.Y0, box.Z0, 1 - fx0, fy0), V(box.X0, box.Y1, box.Z0, 1 - fx0, fy1), V(box.X1, box.Y1, box.Z0, 1 - fx1, fy1));
                break;
            case BlockFace.East:
                buffer.AddQuad(V(box.X1, box.Y0, box.Z1, 1 - fz1, fy0), V(box.X1, box.Y0, box.Z0, 1 - fz0, fy0), V(box.X1, box.Y1, box.Z0, 1 - fz0, fy1), V(box.X1, box.Y1, box.Z1, 1 - fz1, fy1));
                break;
            case BlockFace.West:
                buffer.AddQuad(V(box.X0, box.Y0, box.Z0, fz0, fy0), V(box.X0, box.Y0, box.Z1, fz1, fy0), V(box.X0, box.Y1, box.Z1, fz1, fy1), V(box.X0, box.Y1, box.Z0, fz0, fy1));
                break;
            case BlockFace.Up:
                buffer.AddQuad(V(box.X0, box.Y1, box.Z1, fx0, 1 - fz1), V(box.X1, box.Y1, box.Z1, fx1, 1 - fz1), V(box.X1, box.Y1, box.Z0, fx1, 1 - fz0), V(box.X0, box.Y1, box.Z0, fx0, 1 - fz0));
                break;
            default:
                buffer.AddQuad(V(box.X0, box.Y0, box.Z0, fx0, fz0), V(box.X1, box.Y0, box.Z0, fx1, fz0), V(box.X1, box.Y0, box.Z1, fx1, fz1), V(box.X0, box.Y0, box.Z1, fx0, fz1));
                break;
        }
    }

    private static uint Light(BlockFace face, int ao, (float R, float G, float B) baseTint, Glow glow, float vx, float vy, float vz)
    {
        var (r, g, b) = FaceTint(face);
        var keep = AoKeep[ao];
        var k = glow.At(vx, vy, vz, face);

        // A lantern both warms the face (less green, much less blue: it shows even on a face the sun already lights in full) and brightens it.
        return MeshVertex.Pack(
            (byte)Math.Min(255f, 255f * ((r * keep * baseTint.R * glow.Evening.R) + (GlowAdd.R * k))),
            (byte)Math.Min(255f, 255f * (((g * keep * baseTint.G * glow.Evening.G) * (1f - (GlowCool.G * k))) + (GlowAdd.G * k))),
            (byte)Math.Min(255f, 255f * (((b * keep * baseTint.B * glow.Evening.B) * (1f - (GlowCool.B * k))) + (GlowAdd.B * k))));
    }

    /// <summary>How far a lantern's light reaches, in blocks.</summary>
    public const float GlowRadius = 9f;

    /// <summary>What the sun's light is multiplied by at dusk (S15): lower and redder.</summary>
    public static readonly (float R, float G, float B) DuskLight = (0.84f, 0.66f, 0.6f);

    /// <summary>What the sun's light is multiplied by at night (E02): low and blue, the light of a clear night sky (dark, but the ground and the figures can be seen).</summary>
    public static readonly (float R, float G, float B) NightLight = (0.28f, 0.36f, 0.58f);

    /// <summary>The colour a lantern adds to a face it lights, at full strength (a warm amber).</summary>
    public static readonly (float R, float G, float B) GlowColour = (0.95f, 0.55f, 0.20f);

    private static readonly (float R, float G, float B) GlowAdd = (0.55f, 0.30f, 0.08f);
    private static readonly (float R, float G, float B) GlowCool = (0f, 0.16f, 0.42f);

    // The lanterns near one chunk, ready to light its vertices (S15). The light falls off with the square of the distance and is strongest on faces
    // that look at the lantern. Nothing here reads a clock or a random number: the same blocks give the same colours.
    private sealed class Glow
    {
        private readonly (float X, float Y, float Z)[] _lights;
        private readonly float _strength;

        private Glow((float X, float Y, float Z)[] lights, float strength, (float R, float G, float B) evening)
        {
            _lights = lights;
            _strength = strength;
            Evening = evening;
        }

        /// <summary>What the sun's light is multiplied by in this band: nothing by day, a dimmer, warmer light at dusk, a low blue one at night.</summary>
        public (float R, float G, float B) Evening { get; }

        public static Glow For(MeshOptions options, int x0, int y0, int z0)
        {
            var evening = LightBands.BakedSun(options.Band);
            if (options.Lights is not { Count: > 0 } all)
            {
                return new Glow([], 0f, evening);
            }

            List<(float X, float Y, float Z)>? near = null;
            foreach (var light in all)
            {
                if (light.X + 0.5f >= x0 - GlowRadius && light.X + 0.5f <= x0 + Size + GlowRadius
                    && light.Y + 0.5f >= y0 - GlowRadius && light.Y + 0.5f <= y0 + Size + GlowRadius
                    && light.Z + 0.5f >= z0 - GlowRadius && light.Z + 0.5f <= z0 + Size + GlowRadius)
                {
                    (near ??= []).Add((light.X + 0.5f, light.Y + 0.5f, light.Z + 0.5f));
                }
            }

            return new Glow(near?.ToArray() ?? [], LightBands.LanternStrength(options.Band), evening);
        }

        // How much a lantern lights a corner of a face, from 0 (none in reach) to the strength (1 at dusk and by night, one half by day).
        public float At(float vx, float vy, float vz, BlockFace face)
        {
            if (_lights.Length == 0)
            {
                return 0f;
            }

            var (nx, ny, nz) = face.Normal();
            var sum = 0f;
            foreach (var (lx, ly, lz) in _lights)
            {
                float dx = lx - vx, dy = ly - vy, dz = lz - vz;
                var d2 = (dx * dx) + (dy * dy) + (dz * dz);
                if (d2 >= GlowRadius * GlowRadius)
                {
                    continue;
                }

                var d = MathF.Sqrt(d2);
                var fall = 1f - (d / GlowRadius);
                var facing = d < 0.01f ? 1f : Math.Clamp(((nx * dx) + (ny * dy) + (nz * dz)) / d, 0f, 1f);
                sum += 1.6f * fall * fall * (0.3f + (0.7f * facing));
            }

            return Math.Min(sum, 1f) * _strength;
        }
    }

    private static readonly (float R, float G, float B) NoTint = (1f, 1f, 1f);

    /// <summary>
    /// The tint of a rock block by its height (S14): the lower crags are warm and earthy, the middle ones neutral grey and the high ones cool and bright, so a
    /// mountain face has bands of colour, as in the brief's reference, and the same stone texture serves them all. Everything but stone is untinted.
    /// </summary>
    public static (float R, float G, float B) RockTint(BlockId id, int y)
    {
        if (id != BlockId.Stone)
        {
            return NoTint;
        }

        var t = Math.Clamp((y - 56) / 56f, 0f, 1f); // 0 at the foothills, 1 on the peaks
        return t < 0.5f
            ? (1.0f - (t * 0.1f), 0.90f + (t * 0.16f), 0.80f + (t * 0.34f)) // warm, towards neutral
            : (0.95f - ((t - 0.5f) * 0.06f), 0.98f, 0.97f + ((t - 0.5f) * 0.06f)); // neutral, towards cool
    }

    // Whether the block at the (relative) position shuts light out: any cube (leaves too, so a forest floor is dim under the crowns).
    private static int Occludes(Neighbourhood near, int x, int y, int z) => IsCubeKind(near.Get(x, y, z)) ? 1 : 0;

    private static void AddCube(MeshBuffer buffer, Neighbourhood near, BlockId id, int lx, int ly, int lz, int x, int y, int z, bool grey, bool occlusion, Glow glow)
    {
        Span<int> ao = stackalloc int[4];
        var baseTint = RockTint(id, y);
        for (var f = 0; f < Faces.Length; f++)
        {
            var face = Faces[f];
            var (dx, dy, dz) = face.Normal();
            if (!FaceVisible(id, near.Get(lx + dx, ly + dy, lz + dz)))
            {
                continue;
            }

            var cell = BlockAtlasLayout.CellFor(id, face);
            var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(cell, grey);
            var corners = FaceCorners[FaceOrder[f]];
            for (var k = 0; k < 4; k++)
            {
                if (!occlusion)
                {
                    ao[k] = 3;
                    continue;
                }

                // The two blocks beside the face's corner and the one diagonal to it, on the layer in front of the face.
                int cx = corners[(k * 3)] == 1 ? 1 : -1, cy = corners[(k * 3) + 1] == 1 ? 1 : -1, cz = corners[(k * 3) + 2] == 1 ? 1 : -1;
                int ax = dx == 0 ? cx : 0, ay = dy == 0 ? cy : 0, az = dz == 0 ? cz : 0; // the sides of the corner, on the axes that are not the normal
                int s1, s2, c;
                if (dx != 0)
                {
                    s1 = Occludes(near, lx + dx, ly + ay, lz);
                    s2 = Occludes(near, lx + dx, ly, lz + az);
                    c = Occludes(near, lx + dx, ly + ay, lz + az);
                }
                else if (dy != 0)
                {
                    s1 = Occludes(near, lx + ax, ly + dy, lz);
                    s2 = Occludes(near, lx, ly + dy, lz + az);
                    c = Occludes(near, lx + ax, ly + dy, lz + az);
                }
                else
                {
                    s1 = Occludes(near, lx + ax, ly, lz + dz);
                    s2 = Occludes(near, lx, ly + ay, lz + dz);
                    c = Occludes(near, lx + ax, ly + ay, lz + dz);
                }

                ao[k] = s1 == 1 && s2 == 1 ? 0 : 3 - (s1 + s2 + c);
            }

            var bl = new MeshVertex(x + corners[0], y + corners[1], z + corners[2], Light(face, ao[0], baseTint, glow, x + corners[0], y + corners[1], z + corners[2]), u0, v1);
            var br = new MeshVertex(x + corners[3], y + corners[4], z + corners[5], Light(face, ao[1], baseTint, glow, x + corners[3], y + corners[4], z + corners[5]), u1, v1);
            var tr = new MeshVertex(x + corners[6], y + corners[7], z + corners[8], Light(face, ao[2], baseTint, glow, x + corners[6], y + corners[7], z + corners[8]), u1, v0);
            var tl = new MeshVertex(x + corners[9], y + corners[10], z + corners[11], Light(face, ao[3], baseTint, glow, x + corners[9], y + corners[10], z + corners[11]), u0, v0);
            if (ao[0] + ao[2] > ao[1] + ao[3])
            {
                buffer.AddQuad(br, tr, tl, bl); // the other diagonal, so the shade does not break along the darker corners
            }
            else
            {
                buffer.AddQuad(bl, br, tr, tl);
            }
        }
    }

    // The depth of the water under a block of water, counting the block itself (1 to 4: a bed one block down is 1).
    private static int WaterDepth(Neighbourhood near, IBlockSource source, int lx, int ly, int lz, int x, int y, int z)
    {
        var depth = 1;
        for (var d = 1; d < 4; d++)
        {
            var below = ly - d >= -1 ? near.Get(lx, ly - d, lz) : source.Get(x, y - d, z);
            if (below != BlockId.Water)
            {
                break;
            }

            depth++;
        }

        return depth;
    }

    private static void AddLiquid(MeshBuffer buffer, Neighbourhood near, IBlockSource source, BlockId id, int lx, int ly, int lz, int x, int y, int z, bool grey, (float R, float G, float B) sun)
    {
        var aboveIsWater = near.Get(lx, ly + 1, lz) == BlockId.Water;
        var level = near.Level(lx, ly, lz);
        var depth = WaterDepth(near, source, lx, ly, lz, x, y, z);
        var deep = depth > 1;
        Span<float> heights = stackalloc float[4]; // the surface at the four corners, in the order of the up face's corners
        var haveHeights = false;
        foreach (var face in Faces)
        {
            var (dx, dy, dz) = face.Normal();
            var neighbour = near.Get(lx + dx, ly + dy, lz + dz);
            if (neighbour == BlockId.Water || IsOpaqueCube(neighbour))
            {
                continue;
            }

            if (!haveHeights)
            {
                SurfaceHeights(near, lx, ly, lz, heights);
                haveHeights = true;
            }

            if (face == BlockFace.Up)
            {
                var flow = source.FlowAt(x, y, z);
                AddWaterSurface(buffer, near, lx, ly, lz, x, y, z, heights, depth, flow, grey, sun);
                continue;
            }

            if (face == BlockFace.Down)
            {
                AddWaterBottom(buffer, x, y, z, deep, grey, sun);
                continue;
            }

            // The side of a column of water that falls: the waterfall's sheet, with its own streaky picture.
            var falling = (aboveIsWater || level.Falling) && neighbour == BlockId.Air;
            AddWaterSide(buffer, face, x, y, z, heights, falling ? WaterTile.Falling : WaterTileLayout.For(deep, false), falling, grey, sun);
        }
    }

    // The height of the water's top at the four corners of the cell, in blocks above its floor (water-plan 4.8): each corner averages the four cells that meet there.
    // The order is that of the up face's corners: south-west, south-east, north-east, north-west.
    private static void SurfaceHeights(Neighbourhood near, int lx, int ly, int lz, Span<float> heights)
    {
        for (var k = 0; k < 4; k++)
        {
            int cx = FaceCorners[2][k * 3], cz = FaceCorners[2][(k * 3) + 2];
            var h = WaterCorners.Height(
                CornerOf(near, lx + cx - 1, ly, lz + cz - 1),
                CornerOf(near, lx + cx, ly, lz + cz - 1),
                CornerOf(near, lx + cx - 1, ly, lz + cz),
                CornerOf(near, lx + cx, ly, lz + cz));
            heights[k] = h / (float)WaterCorners.PerBlock;
        }
    }

    private static CornerCell CornerOf(Neighbourhood near, int x, int y, int z)
    {
        var block = near.Get(x, y, z);
        var above = near.Get(x, y + 1, z) == BlockId.Water;
        if (block == BlockId.Water)
        {
            return CornerCell.Water(near.Level(x, y, z), above);
        }

        return new CornerCell(IsCubeKind(block) ? CornerKind.Solid : CornerKind.Open, default, above);
    }

    // The index in the up face's corner order of the corner at (cx, cz), each 0 or 1.
    private static int CornerIndex(int cx, int cz) => cz == 1 ? cx : 3 - cx;

    // The water's top face: the picture by depth (pale and clear over the shallow bed, deeper blue over deep water), turned so its ripples run downstream, and the
    // transparency of each corner, clearer where a solid block touches it, so the bed shows through and the shore reads lighter.
    private static void AddWaterSurface(MeshBuffer buffer, Neighbourhood near, int lx, int ly, int lz, int x, int y, int z, ReadOnlySpan<float> heights, int depth, FlowVector flow, bool grey, (float R, float G, float B) sun)
    {
        var tile = WaterTileLayout.For(depth > 1, flow.IsHorizontal);
        var corners = FaceCorners[2];
        Span<MeshVertex> v = stackalloc MeshVertex[4];
        for (var k = 0; k < 4; k++)
        {
            int cx = corners[k * 3] == 1 ? 1 : -1, cz = corners[(k * 3) + 2] == 1 ? 1 : -1;
            var land = 0;
            for (var j = 0; j < 3; j++)
            {
                var ox = j == 1 ? 0 : cx;
                var oz = j == 0 ? 0 : cz;
                if (IsCubeKind(near.Get(lx + ox, ly, lz + oz)))
                {
                    land++;
                }
            }

            var clarity = Math.Min(3, land + (depth <= 1 ? 1 : 0));
            var alpha = (byte)(depth > 2 ? 250 - (land * 26) : depth == 2 ? 240 - (land * 34) : 215 - (clarity * 30));
            var (p, q) = WaterTileLayout.Along(cx * 0.5f, cz * 0.5f, flow.X, flow.Z);
            if (!flow.IsHorizontal)
            {
                (p, q) = WaterTileLayout.Vary(BlockArtNature.Mix((uint)x, (uint)y, (uint)z + 0x77), p, q); // still water: a different turn in each block
            }

            var (u, w) = WaterTileLayout.Uv(tile, grey, p, q);
            v[k] = new MeshVertex(x + corners[k * 3], y + heights[k], z + corners[(k * 3) + 2], MeshVertex.Pack((byte)(255 * sun.R), (byte)(255 * sun.G), (byte)(255 * sun.B), alpha), u, w);
        }

        buffer.AddQuad(v[0], v[1], v[2], v[3]);

        // H05: the same surface seen from below, on the same four corners (H09: they move together). The water pass culls back faces, so without this quad the top
        // face is invisible from under the water and the world above (leaves, undergrowth, far terrain) shows through it as white speckles. It starts at the third
        // corner so both quads split along the same diagonal.
        buffer.AddQuad(v[2], v[1], v[0], v[3]);
    }

    // The underside of a water cell that has air (or a plant) below it: flat, at the floor of the cell.
    private static void AddWaterBottom(MeshBuffer buffer, int x, int y, int z, bool deep, bool grey, (float R, float G, float B) sun)
    {
        var tile = WaterTileLayout.For(deep, false);
        var corners = FaceCorners[3];
        var (r, g, b) = FaceTint(BlockFace.Down);
        var colour = MeshVertex.Pack((byte)(255 * r * sun.R), (byte)(255 * g * sun.G), (byte)(255 * b * sun.B), 235);
        MeshVertex V(int k)
        {
            var (u, w) = WaterTileLayout.Uv(tile, grey, corners[k * 3], corners[(k * 3) + 2]);
            return new MeshVertex(x + corners[k * 3], y, z + corners[(k * 3) + 2], colour, u, w);
        }

        buffer.AddQuad(V(0), V(1), V(2), V(3));
    }

    // A side of a water cell that borders air: the bottom edge is the floor of the cell, the top edge follows the surface's corners so the sides close the
    // surface at any slope. The picture hangs from the cell's top, so it is continuous down a column; the sheet of a waterfall takes no shading.
    private static void AddWaterSide(MeshBuffer buffer, BlockFace face, int x, int y, int z, ReadOnlySpan<float> heights, WaterTile tile, bool sheet, bool grey, (float R, float G, float B) sun)
    {
        var corners = FaceCorners[Array.IndexOf(Faces, face)];
        var topRight = heights[CornerIndex(corners[6], corners[8])];
        var topLeft = heights[CornerIndex(corners[9], corners[11])];
        uint colour;
        if (sheet)
        {
            colour = MeshVertex.Pack(255, 255, 255, 255);
        }
        else
        {
            var (r, g, b) = FaceTint(face);
            colour = MeshVertex.Pack((byte)(255 * r * sun.R), (byte)(255 * g * sun.G), (byte)(255 * b * sun.B), 235);
        }

        MeshVertex V(int k, float height, float p, float down)
        {
            var (u, w) = WaterTileLayout.Uv(tile, grey, p, down);
            return new MeshVertex(x + corners[k * 3], y + height, z + corners[(k * 3) + 2], colour, u, w);
        }

        buffer.AddQuad(V(0, 0f, 0f, 1f), V(1, 0f, 1f, 1f), V(2, topRight, 1f, 1f - topRight), V(3, topLeft, 0f, 1f - topLeft));
    }


    // Undergrowth: two quads crossing on the diagonals of the cell, a little off centre, a little different in height from plant to plant (a stable
    // pick from the position), brighter at the tip than at the root, in the sun's light of the mesh's band. Drawn with no back-face culling, so one quad of each pair is enough.
    private static void AddCross(MeshBuffer buffer, BlockId id, int x, int y, int z, bool grey, int keepPercent, (float R, float G, float B) sun)
    {
        var h = BlockArtNature.Mix((uint)x, (uint)y, (uint)z + 0x5151);
        if (keepPercent < 100 && (h >> 24) % 100 >= (uint)keepPercent)
        {
            return;
        }

        var cell = BlockAtlasLayout.CellFor(id, BlockFace.North);
        var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(cell, grey);
        var jitterX = (((h & 15) / 15f) - 0.5f) * 0.28f;
        var jitterZ = ((((h >> 4) & 15) / 15f) - 0.5f) * 0.28f;
        var height = id switch
        {
            BlockId.Reeds => 1.0f,
            BlockId.Fern => 0.62f + ((((h >> 8) & 7) / 7f) * 0.2f),
            BlockId.TallGrass => 0.7f + ((((h >> 8) & 7) / 7f) * 0.3f),
            BlockId.Wheat => 0.82f + ((((h >> 8) & 7) / 7f) * 0.14f),
            _ => 0.42f + ((((h >> 8) & 7) / 7f) * 0.14f), // flowers stay low: the eye of a player on a ledge looks over them
        };
        var half = id == BlockId.Fern ? 0.62f : 0.5f;
        var cx = x + 0.5f + jitterX;
        var cz = z + 0.5f + jitterZ;
        // The sun's multiplier of the band is in the plants' colours as it is in the faces' (E02), so the tint of the band brings them to the light of the moment too.
        var root = MeshVertex.Pack((byte)(184 * sun.R), (byte)(206 * sun.G), (byte)(196 * sun.B));
        var tip = MeshVertex.Pack((byte)(255 * sun.R), (byte)(252 * sun.G), (byte)(240 * sun.B));
        for (var plane = 0; plane < 2; plane++)
        {
            float ax = plane == 0 ? -half : half;
            float az = -half;
            float bx = -ax;
            float bz = half;
            buffer.AddQuad(
                new MeshVertex(cx + ax, y, cz + az, root, u0, v1),
                new MeshVertex(cx + bx, y, cz + bz, root, u1, v1),
                new MeshVertex(cx + bx, y + height, cz + bz, tip, u1, v0),
                new MeshVertex(cx + ax, y + height, cz + az, tip, u0, v0));
        }
    }

    private static void AddAllFaces(MeshBuffer buffer, int x, int y, int z, in Box box, BlockId id, BlockCell? cell, bool grey, (byte R, byte G, byte B, byte A) tint, bool unshaded = false)
    {
        foreach (var face in Faces)
        {
            AddFace(buffer, face, box, (x, y, z), cell ?? BlockAtlasLayout.CellFor(id, face), tint, Turn.None, grey, unshaded);
        }
    }

    private static void AddDoor(MeshBuffer buffer, Neighbourhood near, int lx, int ly, int lz, int x, int y, int z, bool grey, (byte R, byte G, byte B, byte A) tint)
    {
        // An open door hangs against the side of the doorway, at right angles to the wall: if the wall runs east-west (solid blocks to the
        // east or west) the panel lies along the north-south axis at the west edge; otherwise it lies along the east-west axis at the north edge.
        var wallRunsEastWest = IsOpaqueCube(near.Get(lx - 1, ly, lz)) || IsOpaqueCube(near.Get(lx + 1, ly, lz));
        var box = wallRunsEastWest
            ? new Box(x, y, z, x + 0.1875f, y + 1, z + 1)
            : new Box(x, y, z, x + 1, y + 1, z + 0.1875f);
        AddAllFaces(buffer, x, y, z, box, BlockId.Door, null, grey, tint);
    }

    private static void AddFence(MeshBuffer buffer, Neighbourhood near, int lx, int ly, int lz, int x, int y, int z, bool grey, (byte R, byte G, byte B, byte A) tint)
    {
        AddAllFaces(buffer, x, y, z, new Box(x + 0.375f, y, z + 0.375f, x + 0.625f, y + 1, z + 0.625f), BlockId.Fence, null, grey, tint);
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            if (near.Get(lx + dx, ly, lz + dz) != BlockId.Fence)
            {
                continue;
            }

            foreach (var (low, high) in new[] { (0.25f, 0.4375f), (0.625f, 0.8125f) })
            {
                var box = dx != 0
                    ? new Box(dx > 0 ? x + 0.625f : x, y + low, z + 0.4375f, dx > 0 ? x + 1 : x + 0.375f, y + high, z + 0.5625f)
                    : new Box(x + 0.4375f, y + low, dz > 0 ? z + 0.625f : z, x + 0.5625f, y + high, dz > 0 ? z + 1 : z + 0.375f);
                AddAllFaces(buffer, x, y, z, box, BlockId.Fence, null, grey, tint);
            }
        }
    }

    private static readonly (byte R, byte G, byte B, byte A) White = (255, 255, 255, 255);
}

/// <summary>An axis-aligned box: minimum and maximum corner in blocks.</summary>
/// <param name="X0">The west edge.</param>
/// <param name="Y0">The bottom.</param>
/// <param name="Z0">The north edge.</param>
/// <param name="X1">The east edge.</param>
/// <param name="Y1">The top.</param>
/// <param name="Z1">The south edge.</param>
public readonly record struct Box(float X0, float Y0, float Z0, float X1, float Y1, float Z1);

/// <summary>A turn about the vertical axis (clockwise seen from above, as a yaw) followed by a move, applied to the vertices of a figure.</summary>
public readonly struct Turn
{
    private readonly float _sin;
    private readonly float _cos;

    /// <summary>Creates the turn: a yaw in radians and the move that follows it.</summary>
    public Turn(float yawRadians, float x, float y, float z)
    {
        _sin = MathF.Sin(yawRadians);
        _cos = MathF.Cos(yawRadians);
        X = x;
        Y = y;
        Z = z;
        IsIdentity = false;
    }

    private Turn(float sin, float cos)
    {
        _sin = sin;
        _cos = cos;
        X = 0;
        Y = 0;
        Z = 0;
        IsIdentity = true;
    }

    /// <summary>No turn and no move.</summary>
    public static Turn None { get; } = new(0f, 1f);

    /// <summary>The east move.</summary>
    public float X { get; }

    /// <summary>The up move.</summary>
    public float Y { get; }

    /// <summary>The south move.</summary>
    public float Z { get; }

    /// <summary>Whether this is <see cref="None"/>.</summary>
    public bool IsIdentity { get; }

    /// <summary>Turns a horizontal point (east, south) clockwise seen from above.</summary>
    public (float X, float Z) Rotate(float x, float z) => IsIdentity ? (x, z) : ((x * _cos) - (z * _sin), (x * _sin) + (z * _cos));
}

/// <summary>The meshes of a chunk: the opaque faces, the translucent ones (water) and the undergrowth.</summary>
/// <param name="Opaque">The opaque quads (drawn first, with depth writes; leaves and bushes are in it, their cut-out pixels dropped by the alpha test).</param>
/// <param name="Alpha">The translucent quads (drawn after, back to front by chunk, without depth writes).</param>
/// <param name="Kinds">The block kinds the chunk holds (<see cref="ChunkMesher.KindBit"/>): a change in the grey set of one of them makes the chunk due.</param>
/// <param name="Plants">The crossed quads of the undergrowth (drawn without culling, only near the eye), or <see langword="null"/> when there are none.</param>
/// <param name="Lit">The self-lit pieces (E02): a lantern's glass and a torch's flame, which the renderer draws with no light tint (drawn with the opaque pass), or <see langword="null"/> when there are none.</param>
public sealed record ChunkMeshes(MeshBuffer Opaque, MeshBuffer Alpha, ulong Kinds = 0, MeshBuffer? Plants = null, MeshBuffer? Lit = null)
{
    /// <summary>The self-lit quads (empty when the chunk has none).</summary>
    public MeshBuffer LitMesh { get; } = Lit ?? new MeshBuffer();

    /// <summary>The undergrowth's quads (empty when the chunk has none).</summary>
    public MeshBuffer PlantMesh { get; } = Plants ?? new MeshBuffer();

    /// <summary>The number of quads in all the meshes.</summary>
    public int FaceCount => Opaque.FaceCount + Alpha.FaceCount + PlantMesh.FaceCount + LitMesh.FaceCount;
}
