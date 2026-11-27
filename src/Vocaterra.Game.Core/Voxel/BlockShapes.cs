namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The shapes of the blocks (K02, building plan 3.2): a pure function from a block, its state and (for a fence) its neighbours to the boxes the mesher draws, the ray
/// selects and a body bumps into, in sixteenths of a block (<see cref="ShapeBox"/>). Every shape is built once and cached, so a call allocates nothing: a full
/// cube is <see cref="VoxelShape.Cube"/>, a fence's sixteen arm combinations are cached, and the oak blocks have a table by id and state.
/// </summary>
/// <remarks>
/// <para><b>Orientation, one definition for collision (K03), drawing (K04) and placing (K06).</b> North is -Z, east +X, south +Z, west -X, and a quarter turn to the
/// right (clockwise seen from above) takes north to east. Every facing is the north shape turned: north to east to south to west.</para>
/// <list type="bullet">
/// <item><b>Stairs:</b> the facing is the side the high step lies towards; the player faced it when placing, so the stairs climb away from the player. A bottom half
/// (y 0 to 8) plus the half of the upper level (y 8 to 16) next to the facing's side; upside down mirrors both in y.</item>
/// <item><b>Slab:</b> bottom is y 0 to 8, top y 8 to 16, double is the full cube.</item>
/// <item><b>Fence:</b> a post (x, z 6 to 10; drawn y 0 to 16, collision y 0 to 24) and an arm towards each horizontal neighbour that is an oak fence, an oak fence gate
/// whose fence line runs that way, or a full solid opaque cube. Drawn arms are two rails (y 6 to 9 and 12 to 15, 2 wide), collision arms are 4 wide and 24 high.</item>
/// <item><b>Fence gate:</b> the facing is the side the gate opens to (the player faced it when placing). Facing north or south puts the fence line along X, east or
/// west along Z. Closed: two posts at the ends of the line and two bars between them drawn, one box 24 high and 2 thick at the cell's middle for collision. Open:
/// the posts drawn, a pair of bars swung from each post towards the facing's side, no collision.</item>
/// <item><b>Door</b> (each of its two cells, the upper-half bit changes nothing): the facing is the way the player faced when placing. Closed: a 3 thick panel along
/// the cell side nearer the player (the side opposite the facing). Open: the panel along the hinge side, the cell side to the right of the facing when the hinge
/// is on the right and to the left when it is not. Collision is the drawn panel.</item>
/// <item><b>Trapdoor:</b> closed: 3 thick at the bottom (y 0 to 3) or top (y 13 to 16). Open: a 3 thick full-height panel against the cell side opposite the facing
/// (the player's side), whichever half it was. Collision is the drawn panel.</item>
/// <item><b>Log:</b> a full cube; its axis only turns the texture.</item>
/// </list>
/// <para>A block with no shape of its own keeps its old behaviour: a solid one is the full cube, any other has no boxes (K03 and K04 decide the scene's door,
/// fence and the rest).</para>
/// <para>A state a block may not hold (<see cref="BlockState.IsValid"/>) reads as the block's state 0.</para>
/// </remarks>
public static class BlockShapes
{
    private const int FirstOak = (int)BlockId.OakLog;
    private const int OakCount = (int)BlockId.OakTrapdoor - FirstOak + 1;
    private const int StatesPerBlock = 32;

    private static readonly VoxelShape[] Oak = BuildOak();
    private static readonly VoxelShape[] Fences = BuildFences();

    /// <summary>The shape of a block in a state, with no neighbours known (a fence then has no arms).</summary>
    /// <param name="id">The block.</param>
    /// <param name="state">Its state byte.</param>
    public static VoxelShape Of(BlockId id, byte state)
    {
        var index = (int)id - FirstOak;
        if ((uint)index >= OakCount)
        {
            return BlockDefinition.IsSolid(id) ? VoxelShape.Cube : VoxelShape.Empty;
        }

        if (id == BlockId.OakFence)
        {
            return Fences[0];
        }

        var shape = state < StatesPerBlock ? Oak[(index * StatesPerBlock) + state] : null;
        return shape ?? Oak[index * StatesPerBlock];
    }

    /// <summary>The shape of the block at a cell, reading the four horizontal neighbours where the shape needs them (the fence's arms).</summary>
    /// <param name="id">The block.</param>
    /// <param name="state">Its state byte.</param>
    /// <param name="reader">The world, for the neighbours.</param>
    /// <param name="x">The cell's x.</param>
    /// <param name="y">The cell's y.</param>
    /// <param name="z">The cell's z.</param>
    public static VoxelShape Of(BlockId id, byte state, IVoxelReader reader, int x, int y, int z) =>
        id == BlockId.OakFence ? Fence(FenceArms(reader, x, y, z)) : Of(id, state);

    /// <summary>The shape of a fence with the given arms (bit <c>1 &lt;&lt; (int)<see cref="Facing"/></c> for each: north 1, east 2, south 4, west 8).</summary>
    public static VoxelShape Fence(int arms) => Fences[arms & 15];

    /// <summary>The arms the fence at a cell has: a bit (<c>1 &lt;&lt; (int)<see cref="Facing"/></c>) for each horizontal neighbour it joins.</summary>
    public static int FenceArms(IVoxelReader reader, int x, int y, int z)
    {
        var arms = 0;
        for (var d = 0; d < 4; d++)
        {
            var (dx, dz) = ((Facing)d).Step();
            if (JoinsFence(reader, x + dx, y, z + dz, d))
            {
                arms |= 1 << d;
            }
        }

        return arms;
    }

    /// <summary>How much of the cell's face the block's drawn boxes cover (a fence is taken with no arms).</summary>
    public static FaceCover CoverOf(BlockId id, byte state, BlockFace face) => Of(id, state).CoverOf(face);

    private static bool JoinsFence(IVoxelReader reader, int nx, int ny, int nz, int direction)
    {
        var id = reader.GetBlock(nx, ny, nz);
        switch (id)
        {
            case BlockId.OakFence:
                return true;
            case BlockId.OakFenceGate:
                // A gate facing north or south has its line along X, so it joins the fence arms that run east or west, and the other way round.
                return (((int)BlockState.Gate.FacingOf(reader.GetState(nx, ny, nz))) & 1) != (direction & 1);
            default:
                return IsFullOpaqueCube(id, reader.GetState(nx, ny, nz));
        }
    }

    private static bool IsFullOpaqueCube(BlockId id, byte state)
    {
        if (!BlockDefinition.IsSolid(id) || id == BlockId.Air)
        {
            return false;
        }

        if (!ReferenceEquals(Of(id, state), VoxelShape.Cube))
        {
            return false;
        }

        // A double slab is a full cube although the slab's row says transparent (a bottom or top slab never reaches here: it is not the cube).
        return !BlockDefinition.Get(id).Transparent || id == BlockId.OakSlab;
    }

    private static VoxelShape[] BuildOak()
    {
        var table = new VoxelShape[OakCount * StatesPerBlock];
        for (var i = 0; i < OakCount; i++)
        {
            var id = (BlockId)(FirstOak + i);
            if (id == BlockId.OakFence)
            {
                continue; // its shapes are the sixteen in Fences
            }

            foreach (var state in BlockState.ValidStates(id))
            {
                table[(i * StatesPerBlock) + state] = Build(id, state);
            }
        }

        return table;
    }

    private static VoxelShape Build(BlockId id, byte state)
    {
        switch (id)
        {
            case BlockId.OakLog:
                return VoxelShape.Cube;
            case BlockId.OakSlab:
                return BlockState.Slab.Kind(state) switch
                {
                    SlabKind.Bottom => Same(new ShapeBox(0, 0, 0, 16, 8, 16)),
                    SlabKind.Top => Same(new ShapeBox(0, 8, 0, 16, 16, 16)),
                    _ => VoxelShape.Cube,
                };
            case BlockId.OakStairs:
                {
                    var facing = BlockState.Stairs.FacingOf(state);
                    ShapeBox[] boxes = [new(0, 0, 0, 16, 8, 16), new(0, 8, 0, 16, 16, 8)];
                    if (BlockState.Stairs.UpsideDown(state))
                    {
                        boxes = [boxes[0].FlippedY(), boxes[1].FlippedY()];
                    }

                    return Same(Turn(boxes, facing));
                }

            case BlockId.OakFenceGate:
                return BuildGate(BlockState.Gate.FacingOf(state), BlockState.Gate.Open(state));
            case BlockId.OakDoor:
                {
                    var facing = BlockState.Door.FacingOf(state);
                    ShapeBox panel;
                    if (!BlockState.Door.Open(state))
                    {
                        panel = new ShapeBox(0, 0, 13, 16, 16, 16); // the south side for a door facing north: the player's side
                    }
                    else
                    {
                        // For a door facing north the right-hand side is east; the panel lies along the hinge side.
                        panel = BlockState.Door.HingeRight(state) ? new ShapeBox(13, 0, 0, 16, 16, 16) : new ShapeBox(0, 0, 0, 3, 16, 16);
                    }

                    return Same(panel.Turned((int)facing));
                }

            case BlockId.OakTrapdoor:
                {
                    var facing = BlockState.Trapdoor.FacingOf(state);
                    ShapeBox panel;
                    if (BlockState.Trapdoor.Open(state))
                    {
                        panel = new ShapeBox(0, 0, 13, 16, 16, 16); // against the side opposite the facing (south for north)
                    }
                    else
                    {
                        panel = BlockState.Trapdoor.Top(state) ? new ShapeBox(0, 13, 0, 16, 16, 16) : new ShapeBox(0, 0, 0, 16, 3, 16);
                    }

                    return Same(panel.Turned((int)facing));
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(id), id, "No shape for this block.");
        }
    }

    private static VoxelShape BuildGate(Facing facing, bool open)
    {
        // The north shape: the fence line along X, opening towards -Z.
        ShapeBox[] posts = [new(0, 5, 6, 2, 16, 10), new(14, 5, 6, 16, 16, 10)];
        if (open)
        {
            ShapeBox[] drawn =
            [
                .. posts,
                new(0, 6, 0, 2, 9, 6), new(0, 12, 0, 2, 15, 6),
                new(14, 6, 0, 16, 9, 6), new(14, 12, 0, 16, 15, 6),
            ];
            return new VoxelShape(Turn(drawn, facing), []);
        }

        ShapeBox[] closed = [.. posts, new(2, 6, 7, 14, 9, 9), new(2, 12, 7, 14, 15, 9)];
        return new VoxelShape(Turn(closed, facing), Turn([new ShapeBox(0, 0, 7, 16, 24, 9)], facing));
    }

    private static VoxelShape[] BuildFences()
    {
        var shapes = new VoxelShape[16];
        for (var arms = 0; arms < 16; arms++)
        {
            List<ShapeBox> drawn = [new(6, 0, 6, 10, 16, 10)];
            List<ShapeBox> collides = [new(6, 0, 6, 10, 24, 10)];
            for (var d = 0; d < 4; d++)
            {
                if ((arms & (1 << d)) == 0)
                {
                    continue;
                }

                // The north arm, turned.
                drawn.Add(new ShapeBox(7, 6, 0, 9, 9, 6).Turned(d));
                drawn.Add(new ShapeBox(7, 12, 0, 9, 15, 6).Turned(d));
                collides.Add(new ShapeBox(6, 0, 0, 10, 24, 6).Turned(d));
            }

            shapes[arms] = new VoxelShape([.. drawn], [.. collides]);
        }

        return shapes;
    }

    private static VoxelShape Same(params ShapeBox[] boxes) => new(boxes, boxes);

    private static ShapeBox[] Turn(ShapeBox[] boxes, Facing facing)
    {
        var turned = new ShapeBox[boxes.Length];
        for (var i = 0; i < boxes.Length; i++)
        {
            turned[i] = boxes[i].Turned((int)facing);
        }

        return turned;
    }
}
