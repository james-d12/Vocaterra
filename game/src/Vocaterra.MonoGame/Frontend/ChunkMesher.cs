using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>Read access to the blocks of a world for the mesher; anything outside the world reads as air unless the source says otherwise.</summary>
public interface IBlockSource
{
    /// <summary>The block at the position.</summary>
    BlockId Get(int x, int y, int z);
}

/// <summary>
/// The blocks of a <see cref="VoxelGrid"/> as a mesher source. The floor under the world (y below zero) reads as stone so the underside of the
/// world is not meshed; the other three sides read as air, so the edge of the world shows its layers.
/// </summary>
/// <param name="Grid">The grid to read.</param>
public sealed record GridBlockSource(VoxelGrid Grid) : IBlockSource
{
    /// <inheritdoc />
    public BlockId Get(int x, int y, int z) => y < 0 ? BlockId.Stone : Grid.GetBlock(x, y, z);
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
}

/// <summary>
/// The mesher: turns the blocks of one 16 x 16 x 16 chunk into quads. A face of a cube is emitted unless the block next to it is an opaque cube
/// (or the same transparent kind: leaves against leaves, water against water); so a single block gives 6 faces, two blocks side by side 10, a
/// block shut in on all sides none, and stone next to water shows its face to the water while the water does not show its face to the stone.
/// Water goes into the translucent mesh, everything else into the opaque one. Each face is shaded by its direction (top brightest, the sides in
/// between, the bottom darkest) in the vertex colour and takes its picture from <see cref="BlockAtlasLayout"/>. Triangles run counter-clockwise
/// seen from outside.
/// </summary>
public static class ChunkMesher
{
    private const float WaterSurface = 14f / 16f;

    /// <summary>The faces in a fixed order.</summary>
    private static readonly BlockFace[] Faces = [BlockFace.East, BlockFace.West, BlockFace.Up, BlockFace.Down, BlockFace.South, BlockFace.North];

    /// <summary>How the renderer draws a block kind.</summary>
    public static BlockShape ShapeOf(BlockId id) => id switch
    {
        BlockId.Air => BlockShape.None,
        BlockId.Water => BlockShape.Liquid,
        BlockId.Bush or BlockId.BerryBush => BlockShape.Plant,
        BlockId.Torch => BlockShape.Torch,
        BlockId.Door => BlockShape.Door,
        BlockId.Fence => BlockShape.Fence,
        _ => BlockShape.Cube,
    };

    /// <summary>Whether the block is a cube that hides whatever lies behind it: a full cube that is not transparent (leaves are).</summary>
    public static bool IsOpaqueCube(BlockId id) => ShapeOf(id) == BlockShape.Cube && !BlockDefinition.Get(id).Transparent;

    /// <summary>The brightness of a face in the vertex colour: top 1.0, north and south 0.82, east and west 0.68, bottom 0.55.</summary>
    public static float FaceShade(BlockFace face) => face switch
    {
        BlockFace.Up => 1.0f,
        BlockFace.Down => 0.55f,
        BlockFace.North or BlockFace.South => 0.82f,
        _ => 0.68f,
    };

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

        // Liquid against liquid, leaves against leaves: the faces between them would only be seen through the block.
        return !(neighbour == self && BlockDefinition.Get(self).Transparent);
    }

    /// <summary>Meshes the chunk at chunk coordinates (<paramref name="chunkX"/>, <paramref name="chunkY"/>, <paramref name="chunkZ"/>).</summary>
    public static ChunkMeshes Build(IBlockSource blocks, int chunkX, int chunkY, int chunkZ)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var opaque = new MeshBuffer();
        var alpha = new MeshBuffer();
        var x0 = chunkX * VoxelChunk.Size;
        var y0 = chunkY * VoxelChunk.Size;
        var z0 = chunkZ * VoxelChunk.Size;
        for (var ly = 0; ly < VoxelChunk.Size; ly++)
        {
            for (var lz = 0; lz < VoxelChunk.Size; lz++)
            {
                for (var lx = 0; lx < VoxelChunk.Size; lx++)
                {
                    var x = x0 + lx;
                    var y = y0 + ly;
                    var z = z0 + lz;
                    var id = blocks.Get(x, y, z);
                    if (id == BlockId.Air)
                    {
                        continue;
                    }

                    switch (ShapeOf(id))
                    {
                        case BlockShape.Cube:
                            AddCube(opaque, blocks, id, x, y, z);
                            break;
                        case BlockShape.Liquid:
                            AddLiquid(alpha, blocks, x, y, z);
                            break;
                        case BlockShape.Plant:
                            AddAllFaces(opaque, x, y, z, new Box(x + 0.125f, y, z + 0.125f, x + 0.875f, y + 0.75f, z + 0.875f), id);
                            break;
                        case BlockShape.Torch:
                            AddAllFaces(opaque, x, y, z, new Box(x + 0.4375f, y, z + 0.4375f, x + 0.5625f, y + 0.625f, z + 0.5625f), BlockId.Torch, BlockCell.TorchStick);
                            AddAllFaces(opaque, x, y, z, new Box(x + 0.375f, y + 0.625f, z + 0.375f, x + 0.625f, y + 0.8125f, z + 0.625f), BlockId.Torch, BlockCell.TorchFlame);
                            break;
                        case BlockShape.Door:
                            AddDoor(opaque, blocks, x, y, z);
                            break;
                        case BlockShape.Fence:
                            AddFence(opaque, blocks, x, y, z);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        return new ChunkMeshes(opaque, alpha);
    }

    /// <summary>Appends one face of a box to a mesh, optionally moved and turned about the vertical axis (for the figures).</summary>
    /// <param name="buffer">The mesh to append to.</param>
    /// <param name="face">The face of the box.</param>
    /// <param name="box">The box in the frame of the block or figure.</param>
    /// <param name="origin">The block corner the texture coordinates are measured from (the box lies inside the block at this corner).</param>
    /// <param name="cell">The picture on the face.</param>
    /// <param name="tint">The colour multiplier before the face shade (white for a block).</param>
    /// <param name="turn">The turn and move applied to every vertex.</param>
    public static void AddFace(MeshBuffer buffer, BlockFace face, in Box box, (float X, float Y, float Z) origin, BlockCell cell, (byte R, byte G, byte B, byte A) tint, Turn turn)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(cell);
        var (_, nz) = turn.Rotate(face.Normal().Dx, face.Normal().Dz);
        var shade = face switch
        {
            BlockFace.Up => 1.0f,
            BlockFace.Down => 0.55f,
            _ => 0.68f + (0.14f * nz * nz),
        };
        var colour = MeshVertex.Pack((byte)(tint.R * shade), (byte)(tint.G * shade), (byte)(tint.B * shade), tint.A);
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

    private static void AddCube(MeshBuffer buffer, IBlockSource blocks, BlockId id, int x, int y, int z)
    {
        var box = new Box(x, y, z, x + 1, y + 1, z + 1);
        foreach (var face in Faces)
        {
            var (dx, dy, dz) = face.Normal();
            if (FaceVisible(id, blocks.Get(x + dx, y + dy, z + dz)))
            {
                AddFace(buffer, face, box, (x, y, z), BlockAtlasLayout.CellFor(id, face), White, Turn.None);
            }
        }
    }

    private static void AddLiquid(MeshBuffer buffer, IBlockSource blocks, int x, int y, int z)
    {
        var above = blocks.Get(x, y + 1, z);
        var top = above == BlockId.Water ? 1f : WaterSurface;
        var box = new Box(x, y, z, x + 1, y + top, z + 1);
        foreach (var face in Faces)
        {
            var (dx, dy, dz) = face.Normal();
            var neighbour = blocks.Get(x + dx, y + dy, z + dz);
            if (neighbour == BlockId.Water || IsOpaqueCube(neighbour))
            {
                continue;
            }

            AddFace(buffer, face, box, (x, y, z), BlockCell.Water, White, Turn.None);
        }
    }

    private static void AddAllFaces(MeshBuffer buffer, int x, int y, int z, in Box box, BlockId id, BlockCell? cell = null)
    {
        foreach (var face in Faces)
        {
            AddFace(buffer, face, box, (x, y, z), cell ?? BlockAtlasLayout.CellFor(id, face), White, Turn.None);
        }
    }

    private static void AddDoor(MeshBuffer buffer, IBlockSource blocks, int x, int y, int z)
    {
        // An open door hangs against the side of the doorway, at right angles to the wall: if the wall runs east-west (solid blocks to the
        // east or west) the panel lies along the north-south axis at the west edge; otherwise it lies along the east-west axis at the north edge.
        var wallRunsEastWest = IsOpaqueCube(blocks.Get(x - 1, y, z)) || IsOpaqueCube(blocks.Get(x + 1, y, z));
        var box = wallRunsEastWest
            ? new Box(x, y, z, x + 0.1875f, y + 1, z + 1)
            : new Box(x, y, z, x + 1, y + 1, z + 0.1875f);
        AddAllFaces(buffer, x, y, z, box, BlockId.Door);
    }

    private static void AddFence(MeshBuffer buffer, IBlockSource blocks, int x, int y, int z)
    {
        AddAllFaces(buffer, x, y, z, new Box(x + 0.375f, y, z + 0.375f, x + 0.625f, y + 1, z + 0.625f), BlockId.Fence);
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            if (blocks.Get(x + dx, y, z + dz) != BlockId.Fence)
            {
                continue;
            }

            foreach (var (low, high) in new[] { (0.25f, 0.4375f), (0.625f, 0.8125f) })
            {
                var box = dx != 0
                    ? new Box(dx > 0 ? x + 0.625f : x, y + low, z + 0.4375f, dx > 0 ? x + 1 : x + 0.375f, y + high, z + 0.5625f)
                    : new Box(x + 0.4375f, y + low, dz > 0 ? z + 0.625f : z, x + 0.5625f, y + high, dz > 0 ? z + 1 : z + 0.375f);
                AddAllFaces(buffer, x, y, z, box, BlockId.Fence);
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

/// <summary>The two meshes of a chunk: the opaque faces and the translucent ones (water).</summary>
/// <param name="Opaque">The opaque quads (drawn first, with depth writes).</param>
/// <param name="Alpha">The translucent quads (drawn after, back to front by chunk, without depth writes).</param>
public sealed record ChunkMeshes(MeshBuffer Opaque, MeshBuffer Alpha)
{
    /// <summary>The number of quads in both meshes.</summary>
    public int FaceCount => Opaque.FaceCount + Alpha.FaceCount;
}
