using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>One part of a figure: a box in pixels of the figure's own frame (x to the figure's right, y up, z towards its back; the face looks to -z) and its colour.</summary>
/// <param name="Box">The box in figure pixels.</param>
/// <param name="Colour">The colour (red, green, blue).</param>
public readonly record struct FigurePart(Box Box, (byte R, byte G, byte B) Colour);

/// <summary>A non-player character as it is drawn: a cube assembly (head, body, arms, legs and a few details) placed in the scene.</summary>
/// <param name="Name">The scene's name for the NPC (<c>guide</c>, <c>shopkeeper</c>, <c>villager</c>).</param>
/// <param name="Label">The name shown over the head, in Spanish.</param>
/// <param name="Feet">The block cell the figure's feet are in.</param>
/// <param name="Yaw">The facing in <see cref="Angles"/> steps (0 north, 1 024 east).</param>
public sealed record NpcFigure(string Name, string Label, BlockPos Feet, int Yaw)
{
    /// <summary>The height of one figure pixel in blocks: the figure is 32 pixels (1.8 blocks) tall without its hat.</summary>
    public const float PixelInBlocks = 0.05625f;

    /// <summary>The middle of the feet in blocks (east, up, south).</summary>
    public (float X, float Y, float Z) FeetPosition => (Feet.X + 0.5f, Feet.Y, Feet.Z + 0.5f);

    /// <summary>The point a name label hangs from, above the head (east, up, south in blocks).</summary>
    public (float X, float Y, float Z) LabelPosition => (Feet.X + 0.5f, Feet.Y + 2.25f, Feet.Z + 0.5f);

    /// <summary>The yaw in radians.</summary>
    public float YawRadians => Yaw * MathF.Tau / Angles.StepsPerTurn;
}

/// <summary>
/// The look of the three NPCs of the route, as boxes: a guide in green with a pack, a shopkeeper in a red apron and a white cap, a villager in blue
/// under a straw hat. They are drawn in code (D-game-5) and share the block atlas's white cell, so the colours come from the vertex.
/// </summary>
public static class NpcFigures
{
    private static readonly (byte R, byte G, byte B) Skin = (244, 200, 154);
    private static readonly (byte R, byte G, byte B) Dark = (40, 30, 30);

    /// <summary>The Spanish name shown over an NPC.</summary>
    public static string LabelOf(string npcName) => npcName switch
    {
        "guide" => "Guía",
        "shopkeeper" => "Tendero",
        "villager" => "Aldeano",
        _ => npcName,
    };

    /// <summary>The figures of a scene's NPCs, in the scene's order.</summary>
    public static IReadOnlyList<NpcFigure> FromScene(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return scene.Npcs.Select(static n => new NpcFigure(n.Name, LabelOf(n.Name), n.Feet, n.Yaw)).ToList();
    }

    /// <summary>The parts of an NPC by its scene name; an unknown name gets a plain grey figure.</summary>
    public static IReadOnlyList<FigurePart> PartsOf(string npcName)
    {
        var (shirt, trousers, hair) = npcName switch
        {
            "guide" => (C(52, 168, 92), C(120, 84, 50), C(78, 48, 28)),
            "shopkeeper" => (C(250, 238, 212), C(196, 164, 112), C(150, 150, 156)),
            "villager" => (C(58, 110, 224), C(108, 112, 124), C(176, 120, 60)),
            _ => (C(160, 160, 160), C(110, 110, 110), C(90, 90, 90)),
        };
        var parts = new List<FigurePart>
        {
            new(new Box(-4, 0, -2, 0, 10, 2), trousers), // left leg
            new(new Box(0, 0, -2, 4, 10, 2), trousers), // right leg
            new(new Box(-4, 10, -2.5f, 4, 22, 2.5f), shirt), // body
            new(new Box(-7, 10.5f, -2, -4, 22, 2), shirt), // left arm
            new(new Box(4, 10.5f, -2, 7, 22, 2), shirt), // right arm
            new(new Box(-7, 10.5f, -2, -4, 13, 2), Skin), // left hand
            new(new Box(4, 10.5f, -2, 7, 13, 2), Skin), // right hand
            new(new Box(-4.5f, 22, -4.5f, 4.5f, 31, 4.5f), Skin), // head
            new(new Box(-2.9f, 26.2f, -4.8f, -1.3f, 27.8f, -4.5f), Dark), // left eye
            new(new Box(1.3f, 26.2f, -4.8f, 2.9f, 27.8f, -4.5f), Dark), // right eye
            new(new Box(-1.2f, 23.6f, -4.8f, 1.2f, 24.6f, -4.5f), C(170, 70, 70)), // mouth
            new(new Box(-4.9f, 29.5f, -4.9f, 4.9f, 32f, 4.9f), hair), // hair on top
            new(new Box(-4.9f, 24f, 4.5f, 4.9f, 32f, 4.9f), hair), // hair at the back
        };
        switch (npcName)
        {
            case "guide":
                parts.Add(new Box(-3.5f, 11f, 2.5f, 3.5f, 21f, 6.5f).WithColour(C(140, 98, 54))); // the pack
                parts.Add(new Box(-4.2f, 20f, -2.9f, 4.2f, 22f, 2.9f).WithColour(C(232, 196, 60))); // a yellow scarf
                break;
            case "shopkeeper":
                parts.Add(new Box(-4.1f, 10f, -3.2f, 4.1f, 20.5f, -2.5f).WithColour(C(220, 58, 50))); // the apron
                parts.Add(new Box(-5.2f, 31.5f, -5.2f, 5.2f, 34f, 5.2f).WithColour(C(252, 252, 252))); // the white cap
                break;
            case "villager":
                parts.Add(new Box(-8f, 31f, -8f, 8f, 32.5f, 8f).WithColour(C(238, 208, 76))); // the brim of the straw hat
                parts.Add(new Box(-5f, 32.5f, -5f, 5f, 36f, 5f).WithColour(C(238, 208, 76))); // the crown
                break;
            default:
                break;
        }

        return parts;
    }

    /// <summary>Builds the mesh of one figure at its place, facing its yaw.</summary>
    public static MeshBuffer Build(NpcFigure figure)
    {
        ArgumentNullException.ThrowIfNull(figure);
        var buffer = new MeshBuffer();
        var (x, y, z) = figure.FeetPosition;
        var turn = new Turn(figure.YawRadians, x, y, z);
        foreach (var part in PartsOf(figure.Name))
        {
            var s = NpcFigure.PixelInBlocks;
            var box = new Box(part.Box.X0 * s, part.Box.Y0 * s, part.Box.Z0 * s, part.Box.X1 * s, part.Box.Y1 * s, part.Box.Z1 * s);
            foreach (var face in new[] { BlockFace.East, BlockFace.West, BlockFace.Up, BlockFace.Down, BlockFace.South, BlockFace.North })
            {
                ChunkMesher.AddFace(buffer, face, box, (0f, 0f, 0f), BlockCell.White, (part.Colour.R, part.Colour.G, part.Colour.B, 255), turn);
            }
        }

        return buffer;
    }

    private static (byte R, byte G, byte B) C(int r, int g, int b) => ((byte)r, (byte)g, (byte)b);

    private static FigurePart WithColour(this Box box, (byte R, byte G, byte B) colour) => new(box, colour);
}
