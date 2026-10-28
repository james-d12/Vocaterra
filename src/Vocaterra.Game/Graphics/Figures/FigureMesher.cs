using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Figures;

/// <summary>
/// The pose of a figure at one moment (S15), in radians and figure pixels: each group of parts turns about its own joint. Plain values, so the idle, talking
/// and walking poses are tested without a graphics card.
/// </summary>
/// <param name="Bob">The lift of the body, head and arms in figure pixels (the breath).</param>
/// <param name="HeadYaw">The head's turn to its right, about the neck.</param>
/// <param name="HeadPitch">The head's nod (positive looks down).</param>
/// <param name="ArmLeft">The left arm's swing about the shoulder (positive swings it forward).</param>
/// <param name="ArmRight">The right arm's swing (positive swings it forward).</param>
/// <param name="LegLeft">The left leg's swing about the hip (positive swings it forward).</param>
/// <param name="LegRight">The right leg's swing.</param>
public readonly record struct FigurePose(float Bob, float HeadYaw, float HeadPitch, float ArmLeft, float ArmRight, float LegLeft, float LegRight)
{
    /// <summary>Standing still, arms hanging.</summary>
    public static FigurePose Rest => default;
}

/// <summary>
/// The poses of the figures: an idle one that never stops (a slow breath, arms that sway a little, a head that looks about), a talking one (the head nods,
/// the right arm lifts and waves) and a walking one (legs and arms swing against each other). Functions of the tick and a per-figure phase only: no clock and no
/// random numbers, so a scripted run draws the same frame every time.
/// </summary>
public static class FigureAnimation
{
    /// <summary>The largest swing of a walking leg in radians.</summary>
    public const float WalkSwing = 0.6f;

    /// <summary>The pose of a standing figure at a tick. <paramref name="phase"/> keeps two figures from moving in step.</summary>
    public static FigurePose Idle(long tick, int phase)
    {
        var t = (tick * 0.045f) + (phase * 1.9f);
        var sway = 0.05f + (0.03f * MathF.Sin(t * 0.6f));
        return new FigurePose(
            Bob: MathF.Sin(t) * 0.3f,
            HeadYaw: MathF.Sin((tick * 0.011f) + (phase * 2.3f)) * 0.4f,
            HeadPitch: MathF.Sin((tick * 0.017f) + phase) * 0.05f,
            ArmLeft: sway * MathF.Sin(t),
            ArmRight: -sway * MathF.Sin(t),
            LegLeft: 0f,
            LegRight: 0f);
    }

    /// <summary>The pose of a figure in a conversation: it faces the player, nods, and its right arm is raised and waves.</summary>
    public static FigurePose Talking(long tick, int phase)
    {
        var t = (tick * 0.045f) + (phase * 1.9f);
        return new FigurePose(
            Bob: MathF.Sin(t) * 0.3f,
            HeadYaw: 0f,
            HeadPitch: 0.04f + (0.07f * MathF.Sin((tick * 0.2f) + phase)),
            ArmLeft: 0.04f * MathF.Sin(t),
            ArmRight: 2.3f + (0.28f * MathF.Sin((tick * 0.16f) + phase)),
            LegLeft: 0f,
            LegRight: 0f);
    }

    /// <summary>The pose of a walking figure; <paramref name="phase"/> is the stride (it grows with the distance walked).</summary>
    public static FigurePose Walking(float phase)
    {
        var s = MathF.Sin(phase);
        return new FigurePose(MathF.Abs(MathF.Cos(phase)) * 0.5f, 0f, 0f, -s * 0.5f, s * 0.5f, s * WalkSwing, -s * WalkSwing);
    }

    /// <summary>The pose of an NPC figure at a tick: talking while it is in a conversation, idle otherwise.</summary>
    public static FigurePose For(NpcFigure figure, long tick)
    {
        ArgumentNullException.ThrowIfNull(figure);
        var phase = Phase(figure.Name);
        return figure.Talking ? Talking(tick, phase) : Idle(tick, phase);
    }

    /// <summary>A small number from a name, so every figure has its own rhythm.</summary>
    public static int Phase(string name)
    {
        var h = 17;
        foreach (var c in name)
        {
            h = unchecked((h * 31) + c);
        }

        return h & 7;
    }
}

/// <summary>
/// Builds the mesh of a figure: every part is a box of the block atlas's cloth, skin or hair picture, tinted by the part's colour and shaded by the sun the way
/// the blocks are (the same tint table as <see cref="ChunkMesher"/>), turned about its joint by the pose and then about the vertical axis by the figure's
/// facing. It writes into a buffer the caller clears and reuses, so a frame allocates nothing.
/// </summary>
public static class FigureMesher
{
    private const float Pixel = NpcFigure.PixelInBlocks;
    private const float ShoulderY = 21.5f;
    private const float HipY = 10f;
    private const float NeckY = 22f;

    // The six faces of a box with the normal of each, in the order used below.
    private static readonly BlockFace[] Faces = [BlockFace.North, BlockFace.South, BlockFace.East, BlockFace.West, BlockFace.Up, BlockFace.Down];

    /// <summary>
    /// Appends the figure's parts to <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The mesh to append to.</param>
    /// <param name="parts">The parts (<see cref="NpcFigures.PartsOf"/>).</param>
    /// <param name="pose">The pose.</param>
    /// <param name="x">The middle of the feet, east.</param>
    /// <param name="y">The feet, up.</param>
    /// <param name="z">The middle of the feet, south.</param>
    /// <param name="yawRadians">The facing about the vertical axis (0 north, a quarter turn east).</param>
    /// <param name="light">The colour the scene's light gives the figure on top of the sun's (a lantern's glow near it): 1 is the plain sun.</param>
    public static void Append(MeshBuffer buffer, FigurePart[] parts, in FigurePose pose, float x, float y, float z, float yawRadians, (float R, float G, float B) light)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(parts);
        var sin = MathF.Sin(yawRadians);
        var cos = MathF.Cos(yawRadians);
        foreach (var part in parts)
        {
            foreach (var face in Faces)
            {
                AddFace(buffer, part, face, pose, x, y, z, sin, cos, light);
            }
        }
    }

    // The angle a group turns about its joint, and the joint (a point in figure pixels, as y and z; the arms and legs turn about the x axis).
    private static (float Pitch, float PivotY) Joint(FigureGroup group, in FigurePose pose) => group switch
    {
        FigureGroup.ArmLeft => (pose.ArmLeft, ShoulderY),
        FigureGroup.ArmRight => (pose.ArmRight, ShoulderY),
        FigureGroup.LegLeft => (pose.LegLeft, HipY),
        FigureGroup.LegRight => (pose.LegRight, HipY),
        FigureGroup.Head => (-pose.HeadPitch, NeckY),
        _ => (0f, 0f),
    };

    // A point (or, with position false, a direction) of a part in figure pixels, posed: turned about its joint, then moved by the breath.
    private static (float X, float Y, float Z) Pose(FigureGroup group, in FigurePose pose, float px, float py, float pz, bool position)
    {
        var (pitch, pivotY) = Joint(group, pose);
        var oy = position ? py - pivotY : py;
        var c = MathF.Cos(pitch);
        var s = MathF.Sin(pitch);
        var ry = (oy * c) - (pz * s);
        var rz = (oy * s) + (pz * c);
        var rx = px;
        if (group == FigureGroup.Head)
        {
            var hc = MathF.Cos(pose.HeadYaw);
            var hs = MathF.Sin(pose.HeadYaw);
            (rx, rz) = ((rx * hc) - (rz * hs), (rx * hs) + (rz * hc));
        }

        if (position)
        {
            ry += pivotY;
            if (group is not (FigureGroup.LegLeft or FigureGroup.LegRight))
            {
                ry += pose.Bob;
            }
        }

        return (rx, ry, rz);
    }

    private static void AddFace(MeshBuffer buffer, in FigurePart part, BlockFace face, in FigurePose pose, float fx, float fy, float fz, float sin, float cos, (float R, float G, float B) light)
    {
        var box = part.Box;
        Span<float> k = stackalloc float[12];
        Corners(face, box, k);
        float ax = k[0], ay = k[1], az = k[2], bx = k[3], by = k[4], bz = k[5], cx = k[6], cy = k[7], cz = k[8], dx = k[9], dy = k[10], dz = k[11];

        // The shade: the face's normal, posed and turned, against the sun.
        var (nx0, ny0, nz0) = face.Normal();
        var (px, py, pz) = Pose(part.Group, pose, nx0, ny0, nz0, false);
        var wx = (px * cos) - (pz * sin);
        var wz = (px * sin) + (pz * cos);
        var (tr, tg, tb) = ChunkMesher.SunTintFor(wx, py, wz);
        var colour = MeshVertex.Pack(
            (byte)Math.Min(255f, part.Colour.R * tr * light.R),
            (byte)Math.Min(255f, part.Colour.G * tg * light.G),
            (byte)Math.Min(255f, part.Colour.B * tb * light.B));
        var cell = part.Special is { } special && part.SpecialFace == face ? special : part.Cell;
        var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(cell);
        buffer.AddQuad(
            Vertex(part.Group, pose, ax, ay, az, fx, fy, fz, sin, cos, colour, u0, v1),
            Vertex(part.Group, pose, bx, by, bz, fx, fy, fz, sin, cos, colour, u1, v1),
            Vertex(part.Group, pose, cx, cy, cz, fx, fy, fz, sin, cos, colour, u1, v0),
            Vertex(part.Group, pose, dx, dy, dz, fx, fy, fz, sin, cos, colour, u0, v0));
    }

    // The corners of a box's face, bottom left, bottom right, top right, top left seen from outside (the order the block mesher uses), as x, y, z each.
    private static void Corners(BlockFace face, in Box box, Span<float> k)
    {
        switch (face)
        {
            case BlockFace.North:
                Set(k, box.X1, box.Y0, box.Z0, box.X0, box.Y0, box.Z0, box.X0, box.Y1, box.Z0, box.X1, box.Y1, box.Z0);
                break;
            case BlockFace.South:
                Set(k, box.X0, box.Y0, box.Z1, box.X1, box.Y0, box.Z1, box.X1, box.Y1, box.Z1, box.X0, box.Y1, box.Z1);
                break;
            case BlockFace.East:
                Set(k, box.X1, box.Y0, box.Z1, box.X1, box.Y0, box.Z0, box.X1, box.Y1, box.Z0, box.X1, box.Y1, box.Z1);
                break;
            case BlockFace.West:
                Set(k, box.X0, box.Y0, box.Z0, box.X0, box.Y0, box.Z1, box.X0, box.Y1, box.Z1, box.X0, box.Y1, box.Z0);
                break;
            case BlockFace.Up:
                Set(k, box.X0, box.Y1, box.Z1, box.X1, box.Y1, box.Z1, box.X1, box.Y1, box.Z0, box.X0, box.Y1, box.Z0);
                break;
            default:
                Set(k, box.X0, box.Y0, box.Z0, box.X1, box.Y0, box.Z0, box.X1, box.Y0, box.Z1, box.X0, box.Y0, box.Z1);
                break;
        }
    }

    private static void Set(Span<float> k, float a, float b, float c, float d, float e, float f, float g, float h, float i, float j, float l, float m)
    {
        k[0] = a;
        k[1] = b;
        k[2] = c;
        k[3] = d;
        k[4] = e;
        k[5] = f;
        k[6] = g;
        k[7] = h;
        k[8] = i;
        k[9] = j;
        k[10] = l;
        k[11] = m;
    }

    /// <summary>
    /// Appends a box in blocks, unposed, with the whole picture on every face, shaded by face (the top brightest, the underside darkest) or not shaded at all: the first-person
    /// arm and what it holds (<see cref="ArmMeshes"/>) are made of these.
    /// </summary>
    /// <param name="buffer">The mesh to append to.</param>
    /// <param name="box">The box in blocks.</param>
    /// <param name="cell">The picture on the sides.</param>
    /// <param name="colour">The tint.</param>
    /// <param name="topBottom">The picture on the top and the underside, or <see langword="null"/> for the same as the sides.</param>
    /// <param name="unshaded">Whether the faces are drawn at full brightness (a flame).</param>
    public static void AddBlock(MeshBuffer buffer, in Box box, BlockCell cell, (byte R, byte G, byte B) colour, BlockCell? topBottom = null, bool unshaded = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Span<float> k = stackalloc float[12];
        foreach (var face in Faces)
        {
            Corners(face, box, k);
            var shade = unshaded ? 1f : face switch
            {
                BlockFace.Up => 1.0f,
                BlockFace.Down => 0.5f,
                BlockFace.West => 0.9f,
                BlockFace.North => 0.82f,
                BlockFace.South => 0.74f,
                _ => 0.68f,
            };
            var packed = MeshVertex.Pack((byte)(colour.R * shade), (byte)(colour.G * shade), (byte)(colour.B * shade));
            var use = face is BlockFace.Up or BlockFace.Down && topBottom is { } other ? other : cell;
            var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(use);
            buffer.AddQuad(
                new MeshVertex(k[0], k[1], k[2], packed, u0, v1),
                new MeshVertex(k[3], k[4], k[5], packed, u1, v1),
                new MeshVertex(k[6], k[7], k[8], packed, u1, v0),
                new MeshVertex(k[9], k[10], k[11], packed, u0, v0));
        }
    }

    private static MeshVertex Vertex(FigureGroup group, in FigurePose pose, float x, float y, float z, float fx, float fy, float fz, float sin, float cos, uint colour, float u, float v)
    {
        var (px, py, pz) = Pose(group, pose, x, y, z, true);
        px *= Pixel;
        py *= Pixel;
        pz *= Pixel;
        return new MeshVertex(fx + ((px * cos) - (pz * sin)), fy + py, fz + ((px * sin) + (pz * cos)), colour, u, v);
    }
}
