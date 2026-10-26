using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Graphics;

/// <summary>The moving group a part of a figure belongs to: the pose turns each group about its own joint.</summary>
public enum FigureGroup
{
    /// <summary>The body and what is fixed to it (clothes, backpack, apron): it only bobs a little with the breath.</summary>
    Body = 0,

    /// <summary>The head with its hair and hat: it turns and nods about the neck.</summary>
    Head,

    /// <summary>The left arm (the figure's own left), swinging about the shoulder.</summary>
    ArmLeft,

    /// <summary>The right arm.</summary>
    ArmRight,

    /// <summary>The left leg, swinging about the hip.</summary>
    LegLeft,

    /// <summary>The right leg.</summary>
    LegRight,
}

/// <summary>
/// One part of a figure: a box in pixels of the figure's own frame (x to the figure's right, y up, z towards its back; the face looks to -z), its colour
/// (the picture of <see cref="Cell"/> times this colour), the group it moves with and, for a head or a backpack, a picture of its own for one face.
/// </summary>
/// <param name="Box">The box in figure pixels.</param>
/// <param name="Colour">The colour (red, green, blue) the picture is tinted with.</param>
/// <param name="Cell">The picture on every face: <see cref="BlockCell.Cloth"/>, <see cref="BlockCell.Skin"/>, <see cref="BlockCell.Hair"/>.</param>
/// <param name="Group">The group the part moves with.</param>
/// <param name="Special">A picture for one face (the face of a head, the flap of a backpack), or <see langword="null"/>.</param>
/// <param name="SpecialFace">The face the special picture is on (north is the front of the figure).</param>
public readonly record struct FigurePart(
    Box Box,
    (byte R, byte G, byte B) Colour,
    BlockCell Cell = BlockCell.Cloth,
    FigureGroup Group = FigureGroup.Body,
    BlockCell? Special = null,
    BlockFace SpecialFace = BlockFace.North);

/// <summary>A non-player character as it is drawn: a figure of textured boxes (head, body, arms, legs, clothes) placed in the scene.</summary>
/// <param name="Name">The scene's name for the NPC (<c>guide</c>, <c>shopkeeper</c>, <c>villager</c>).</param>
/// <param name="Label">The name shown over the head, in Spanish.</param>
/// <param name="Feet">The block cell the figure's feet are in.</param>
/// <param name="Yaw">The facing the scene gave it, in <see cref="Angles"/> steps (0 north, 1 024 east): where it stands when nobody is talking to it.</param>
public sealed record NpcFigure(string Name, string Label, BlockPos Feet, int Yaw)
{
    /// <summary>The height of one figure pixel in blocks: the figure is 32 pixels (1.8 blocks) tall without its hat.</summary>
    public const float PixelInBlocks = 0.05625f;

    /// <summary>The facing it has now, in angle steps (a real number: it turns smoothly to face the player when spoken to, see <see cref="FigureFacing"/>).</summary>
    public double FacingSteps { get; set; } = Yaw;

    /// <summary>Whether the figure is in a conversation now (it faces the player and gestures).</summary>
    public bool Talking { get; set; }

    /// <summary>The middle of the feet in blocks (east, up, south).</summary>
    public (float X, float Y, float Z) FeetPosition => (Feet.X + 0.5f, Feet.Y, Feet.Z + 0.5f);

    /// <summary>The point a name label hangs from, above the head (east, up, south in blocks).</summary>
    public (float X, float Y, float Z) LabelPosition => (Feet.X + 0.5f, Feet.Y + 2.25f, Feet.Z + 0.5f);

    /// <summary>The rest facing in radians.</summary>
    public float YawRadians => Yaw * MathF.Tau / Angles.StepsPerTurn;

    /// <summary>The facing it has now in radians.</summary>
    public float FacingRadians => (float)(FacingSteps * Math.Tau / Angles.StepsPerTurn);
}

/// <summary>
/// The look of the figures, as textured boxes (S15): the guide Luna with long hair, a green jacket, a yellow scarf and a pack; the shopkeeper in a white
/// shirt with rolled sleeves, a leather apron, a beard and a baker's hat; the villager in a blue tunic and a straw hat; and the player in a blue shirt with a
/// leather backpack, seen over the shoulder when F5 is pressed. They are drawn in code (D-game-5): the faces, the cloth, the skin and the hair are cells of
/// the block atlas (<see cref="BlockArtVillage"/>), tinted by the part's colour.
/// </summary>
public static class NpcFigures
{
    private static readonly (byte R, byte G, byte B) Skin = (244, 200, 154);
    private static readonly (byte R, byte G, byte B) Leather = (140, 92, 50);
    private static readonly (byte R, byte G, byte B) Boot = (74, 50, 34);
    private static readonly Dictionary<string, FigurePart[]> Cache = new(StringComparer.Ordinal);

    /// <summary>The name of the player's figure for <see cref="PartsOf"/> (the figure drawn over the shoulder).</summary>
    public const string PlayerName = "player";

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

    /// <summary>The parts of a figure by its scene name (or <see cref="PlayerName"/>); an unknown name gets a plain grey figure. The array is shared: do not change it.</summary>
    public static FigurePart[] PartsOf(string npcName)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(npcName, out var parts))
            {
                parts = Build(npcName);
                Cache[npcName] = parts;
            }

            return parts;
        }
    }

    /// <summary>Builds the mesh of one figure at its rest pose, facing its scene yaw (a still picture for tests and tools; the renderer poses the figure each frame).</summary>
    public static MeshBuffer Build(NpcFigure figure)
    {
        ArgumentNullException.ThrowIfNull(figure);
        var buffer = new MeshBuffer();
        var (x, y, z) = figure.FeetPosition;
        FigureMesher.Append(buffer, PartsOf(figure.Name), FigurePose.Rest, x, y, z, figure.YawRadians, (1f, 1f, 1f));
        return buffer;
    }

    private static FigurePart[] Build(string name)
    {
        var (shirt, trousers, hair, face) = name switch
        {
            "guide" => (C(52, 168, 92), C(120, 84, 50), C(78, 48, 28), BlockCell.FaceGuide),
            "shopkeeper" => (C(250, 244, 230), C(196, 164, 112), C(60, 40, 26), BlockCell.FaceShopkeeper),
            "villager" => (C(58, 110, 224), C(108, 112, 124), C(176, 120, 60), BlockCell.FaceVillager),
            PlayerName => (C(64, 112, 208), C(104, 72, 48), C(92, 56, 34), BlockCell.FacePlayer),
            _ => (C(160, 160, 160), C(110, 110, 110), C(90, 90, 90), BlockCell.FaceVillager),
        };
        var rolled = name == "shopkeeper";
        var sleeveBottom = rolled ? 17f : 14.5f;
        var parts = new List<FigurePart>
        {
            // Legs and boots (indices 0 and 1 are the legs: the body is the third part, index 2).
            new(new Box(-3.8f, 3f, -2f, -0.2f, 10f, 2f), trousers, BlockCell.Cloth, FigureGroup.LegLeft),
            new(new Box(0.2f, 3f, -2f, 3.8f, 10f, 2f), trousers, BlockCell.Cloth, FigureGroup.LegRight),
            new(new Box(-4f, 10f, -2.5f, 4f, 22f, 2.5f), shirt, BlockCell.Cloth, FigureGroup.Body), // body
            new(new Box(-3.9f, 0f, -2.4f, -0.1f, 3f, 2.6f), Boot, BlockCell.Cloth, FigureGroup.LegLeft), // boots
            new(new Box(0.1f, 0f, -2.4f, 3.9f, 3f, 2.6f), Boot, BlockCell.Cloth, FigureGroup.LegRight),
            new(new Box(-4.1f, 9.4f, -2.6f, 4.1f, 10.4f, 2.6f), C(70, 48, 30), BlockCell.Cloth, FigureGroup.Body), // belt
            new(new Box(-7f, sleeveBottom, -2f, -4.05f, 22f, 2f), shirt, BlockCell.Cloth, FigureGroup.ArmLeft), // sleeves
            new(new Box(4.05f, sleeveBottom, -2f, 7f, 22f, 2f), shirt, BlockCell.Cloth, FigureGroup.ArmRight),
            new(new Box(-6.9f, 10.8f, -1.9f, -4.15f, sleeveBottom, 1.9f), Skin, BlockCell.Skin, FigureGroup.ArmLeft), // hands and forearms
            new(new Box(4.15f, 10.8f, -1.9f, 6.9f, sleeveBottom, 1.9f), Skin, BlockCell.Skin, FigureGroup.ArmRight),
            new(new Box(-1.6f, 21.8f, -1.8f, 1.6f, 23f, 1.8f), Skin, BlockCell.Skin, FigureGroup.Head), // neck
            new(new Box(-4.5f, 22.5f, -4.5f, 4.5f, 31.5f, 4.5f), Skin, BlockCell.Skin, FigureGroup.Head, face), // head, with its face on the front
            new(new Box(-4.85f, 29.6f, -4.7f, 4.85f, 32.2f, 4.85f), hair, BlockCell.Hair, FigureGroup.Head), // hair on top
            new(new Box(-4.85f, 24.5f, 3.9f, 4.85f, 32f, 4.85f), hair, BlockCell.Hair, FigureGroup.Head), // hair at the back
            new(new Box(-4.85f, 27f, -4.2f, -4.4f, 30.5f, 4.2f), hair, BlockCell.Hair, FigureGroup.Head), // hair over the ears
            new(new Box(4.4f, 27f, -4.2f, 4.85f, 30.5f, 4.2f), hair, BlockCell.Hair, FigureGroup.Head),
        };
        switch (name)
        {
            case "guide":
                parts.Add(new FigurePart(new Box(-4.85f, 14f, 3.9f, 4.85f, 24.5f, 4.85f), hair, BlockCell.Hair, FigureGroup.Head)); // long hair down the back
                parts.Add(new FigurePart(new Box(-3.5f, 11f, 2.6f, 3.5f, 21f, 6.4f), Leather, BlockCell.Cloth, FigureGroup.Body, BlockCell.Backpack, BlockFace.South)); // the pack
                parts.Add(new FigurePart(new Box(-3.4f, 17f, -2.9f, -2.2f, 22.1f, -2.4f), C(104, 68, 36), BlockCell.Cloth, FigureGroup.Body)); // pack straps
                parts.Add(new FigurePart(new Box(2.2f, 17f, -2.9f, 3.4f, 22.1f, -2.4f), C(104, 68, 36), BlockCell.Cloth, FigureGroup.Body));
                parts.Add(new FigurePart(new Box(-4.3f, 21f, -3f, 4.3f, 22.6f, 3f), C(232, 196, 60), BlockCell.Cloth, FigureGroup.Body)); // a yellow scarf
                break;
            case "shopkeeper":
                parts.Add(new FigurePart(new Box(-4.1f, 5.5f, -3.3f, 4.1f, 20.5f, -2.5f), C(112, 70, 42), BlockCell.Cloth, FigureGroup.Body)); // the leather apron
                parts.Add(new FigurePart(new Box(-3.6f, 20.5f, -3.3f, -2.4f, 22.1f, -2.4f), C(112, 70, 42), BlockCell.Cloth, FigureGroup.Body)); // its straps
                parts.Add(new FigurePart(new Box(2.4f, 20.5f, -3.3f, 3.6f, 22.1f, -2.4f), C(112, 70, 42), BlockCell.Cloth, FigureGroup.Body));
                parts.Add(new FigurePart(new Box(-2.6f, 9f, -3.8f, 2.6f, 13f, -3.2f), C(88, 54, 32), BlockCell.Cloth, FigureGroup.Body)); // the pocket
                parts.Add(new FigurePart(new Box(-4.9f, 32f, -4.9f, 4.9f, 33.4f, 4.9f), C(250, 250, 250), BlockCell.Cloth, FigureGroup.Head)); // the baker's band
                parts.Add(new FigurePart(new Box(-5.4f, 33.4f, -5.4f, 5.4f, 38f, 5.4f), C(252, 252, 252), BlockCell.Cloth, FigureGroup.Head)); // and its puffed top
                break;
            case "villager":
                parts.Add(new FigurePart(new Box(-3.6f, 11f, -2.8f, 3.6f, 14f, -2.4f), C(220, 214, 196), BlockCell.Cloth, FigureGroup.Body)); // a pale shirt front
                parts.Add(new FigurePart(new Box(-8f, 31.4f, -8f, 8f, 32.7f, 8f), C(238, 208, 76), BlockCell.Hair, FigureGroup.Head)); // the brim of the straw hat
                parts.Add(new FigurePart(new Box(-5.1f, 32.7f, -5.1f, 5.1f, 36.4f, 5.1f), C(238, 208, 76), BlockCell.Hair, FigureGroup.Head)); // the crown
                break;
            case PlayerName:
                parts.Add(new FigurePart(new Box(-4.2f, 10.5f, 2.6f, 4.2f, 22.5f, 7.2f), Leather, BlockCell.Cloth, FigureGroup.Body, BlockCell.Backpack, BlockFace.South)); // the backpack
                parts.Add(new FigurePart(new Box(-3.4f, 17f, -2.9f, -2.2f, 22.1f, -2.4f), C(104, 68, 36), BlockCell.Cloth, FigureGroup.Body)); // its straps
                parts.Add(new FigurePart(new Box(2.2f, 17f, -2.9f, 3.4f, 22.1f, -2.4f), C(104, 68, 36), BlockCell.Cloth, FigureGroup.Body));
                parts.Add(new FigurePart(new Box(-3.6f, 21.5f, 6.2f, 3.6f, 24.2f, 7.8f), C(228, 224, 208), BlockCell.Cloth, FigureGroup.Body)); // a bedroll on top of it
                break;
            default:
                break;
        }

        return [.. parts];
    }

    private static (byte R, byte G, byte B) C(int r, int g, int b) => ((byte)r, (byte)g, (byte)b);
}
