using Vocaterra.Game.Items;
using Vocaterra.Game.Voxel;

namespace Vocaterra.MonoGame.Graphics;

/// <summary>What the first-person hand holds: nothing, a log, a torch, a bottle or a flat picture of a small item.</summary>
public enum HeldKind
{
    /// <summary>Nothing: the bare arm.</summary>
    None = 0,

    /// <summary>A short log (wood).</summary>
    Log,

    /// <summary>A torch with its flame.</summary>
    Torch,

    /// <summary>A glass bottle, empty or full of water.</summary>
    Bottle,

    /// <summary>A small flat item drawn from its icon (a berry, bread).</summary>
    Sprite,
}

/// <summary>The held item's model: its kind and the atlas picture it is drawn with.</summary>
/// <param name="Kind">What the hand holds.</param>
/// <param name="Cell">The picture of the item (a bottle's or a sprite's icon; the log and the torch use their block cells).</param>
public readonly record struct HeldChoice(HeldKind Kind, BlockCell Cell)
{
    /// <summary>The empty hand.</summary>
    public static HeldChoice Nothing => new(HeldKind.None, BlockCell.White);
}

/// <summary>Which model the arm holds for the selected hotbar item (S15): wood is a log, a torch is a torch with its flame, bottles are bottles.</summary>
public static class HeldItems
{
    /// <summary>The model for the selected slot: <paramref name="empty"/> when the slot holds nothing.</summary>
    public static HeldChoice For(bool empty, ItemId item)
    {
        if (empty)
        {
            return HeldChoice.Nothing;
        }

        return item switch
        {
            ItemId.Wood => new HeldChoice(HeldKind.Log, BlockCell.TrunkSide),
            ItemId.Torch => new HeldChoice(HeldKind.Torch, BlockCell.TorchStick),
            ItemId.Bottle => new HeldChoice(HeldKind.Bottle, BlockCell.IconBottle),
            ItemId.WaterBottle => new HeldChoice(HeldKind.Bottle, BlockCell.IconWaterBottle),
            ItemId.Berry => new HeldChoice(HeldKind.Sprite, BlockCell.IconBerry),
            ItemId.Bread => new HeldChoice(HeldKind.Sprite, BlockCell.IconBread),
            _ => HeldChoice.Nothing,
        };
    }

    /// <summary>The model for a stack.</summary>
    public static HeldChoice For(ItemStack stack) => For(stack.IsEmpty, stack.Item);
}

/// <summary>How the arm moves (S15): a lift and a push forward, and a sway in x and y, all in blocks of the view space and radians.</summary>
/// <param name="Pitch">The extra lift of the arm in radians (positive raises the hand, negative brings it down).</param>
/// <param name="Forward">How far the arm is pushed away from the eye in blocks.</param>
/// <param name="SwayX">The sideways sway in blocks.</param>
/// <param name="SwayY">The up-down sway in blocks.</param>
public readonly record struct ArmMotion(float Pitch, float Forward, float SwayX, float SwayY)
{
    /// <summary>How many ticks the swing of placing an item lasts.</summary>
    public const int PlaceTicks = 14;

    /// <summary>
    /// The arm's motion: a breath while still, a sway while walking, a chopping swing while a block is being gathered (<paramref name="gatherProgress"/> grows while the
    /// button is held) and one dip when an item has just been placed (<paramref name="ticksSincePlace"/>, or a large number when nothing was placed).
    /// </summary>
    public static ArmMotion At(long tick, bool moving, int gatherProgress, long ticksSincePlace)
    {
        var swayX = moving ? MathF.Sin(tick * 0.17f) * 0.014f : 0f;
        var swayY = moving ? -MathF.Abs(MathF.Cos(tick * 0.17f)) * 0.02f : MathF.Sin(tick * 0.03f) * 0.004f;
        var pitch = 0f;
        var forward = 0f;
        if (gatherProgress > 0)
        {
            var swing = MathF.Sin(gatherProgress * 0.55f);
            pitch += 0.42f * swing;
            forward += 0.07f * MathF.Max(0f, -swing);
        }

        if (ticksSincePlace is >= 0 and < PlaceTicks)
        {
            var dip = MathF.Sin(MathF.PI * ticksSincePlace / PlaceTicks);
            pitch -= 0.5f * dip;
            forward += 0.09f * dip;
        }

        return new ArmMotion(pitch, forward, swayX, swayY);
    }
}

/// <summary>
/// The light on the arm (S15): the arm is lit by the scene like the figures are. It is the sun by day, dimmer under a roof or a tree (a solid block close above the
/// head), and warm near a lantern or while the hand holds a torch. The result multiplies the arm's colours, so it is plain numbers, tested without a window.
/// </summary>
public static class ArmLight
{
    /// <summary>How far above the head a solid block counts as cover, in blocks.</summary>
    public const int CoverReach = 6;

    /// <summary>The share of light that stays under cover.</summary>
    public const float CoveredShare = 0.62f;

    /// <summary>How warmly the lanterns light a point, 0 to 1 (a lantern close by is nearly 1 at dusk and about half of that by day).</summary>
    public static float Warmth(IReadOnlyList<BlockPos> lights, float x, float y, float z, bool dusk)
    {
        ArgumentNullException.ThrowIfNull(lights);
        var warm = 0f;
        for (var i = 0; i < lights.Count; i++)
        {
            var light = lights[i];
            float dx = light.X + 0.5f - x, dy = light.Y + 0.5f - y, dz = light.Z + 0.5f - z;
            var d2 = (dx * dx) + (dy * dy) + (dz * dz);
            if (d2 < ChunkMesher.GlowRadius * ChunkMesher.GlowRadius)
            {
                var fall = 1f - (MathF.Sqrt(d2) / ChunkMesher.GlowRadius);
                warm += fall * fall * (dusk ? 1f : 0.5f);
            }
        }

        return MathF.Min(warm, 1f);
    }

    /// <summary>The colour multiplier for the arm.</summary>
    /// <param name="isSolid">Whether the block cell (x, y, z) is solid.</param>
    /// <param name="eye">The eye in blocks.</param>
    /// <param name="lights">The lanterns of the world.</param>
    /// <param name="holdingTorch">Whether the hand holds a torch (its flame lights the arm).</param>
    /// <param name="dusk">Whether it is dusk (the sun is dimmer and the lanterns brighter).</param>
    public static (float R, float G, float B) At(Func<int, int, int, bool> isSolid, (float X, float Y, float Z) eye, IReadOnlyList<BlockPos> lights, bool holdingTorch, bool dusk)
    {
        ArgumentNullException.ThrowIfNull(isSolid);
        ArgumentNullException.ThrowIfNull(lights);
        var ex = (int)MathF.Floor(eye.X);
        var ey = (int)MathF.Floor(eye.Y);
        var ez = (int)MathF.Floor(eye.Z);
        var covered = false;
        for (var dy = 1; dy <= CoverReach && !covered; dy++)
        {
            covered = isSolid(ex, ey + dy, ez);
        }

        var sun = covered ? CoveredShare : 1f;
        float r = sun * (dusk ? ChunkMesher.DuskLight.R : 1f), g = sun * (dusk ? ChunkMesher.DuskLight.G : 1f), b = sun * (dusk ? ChunkMesher.DuskLight.B : 1f);
        var warm = Warmth(lights, eye.X, eye.Y, eye.Z, dusk);
        if (holdingTorch)
        {
            warm += 0.45f;
        }

        warm = MathF.Min(warm, 1f);
        r += ChunkMesher.GlowColour.R * warm * 0.7f;
        g += ChunkMesher.GlowColour.G * warm * 0.7f;
        b += ChunkMesher.GlowColour.B * warm * 0.7f;
        return (MathF.Min(r, 1.25f), MathF.Min(g, 1.25f), MathF.Min(b, 1.25f));
    }
}

/// <summary>
/// The meshes of the first-person arm and of what it holds (S15), in the arm's own space: the elbow end of the arm at the origin, the arm running along -z (away
/// from the eye), x to the right and y up, in blocks. The arm is a blocky forearm in the player's sleeve colour with a hand in the player's skin, drawn with the same
/// cloth and skin pictures as the figures and shaded by face, with no outline. The renderer places it at the bottom right of the view with a matrix.
/// </summary>
public static class ArmMeshes
{
    /// <summary>The player's sleeve colour: the shirt of the player's figure.</summary>
    public static readonly (byte R, byte G, byte B) Sleeve = (64, 112, 208);

    /// <summary>The player's skin colour.</summary>
    public static readonly (byte R, byte G, byte B) SkinColour = (244, 200, 154);

    private static readonly Dictionary<HeldChoice, (MeshBuffer Body, MeshBuffer Glow)> Items = [];
    private static MeshBuffer? _arm;

    /// <summary>The length of the sleeve in blocks.</summary>
    public const float SleeveLength = 0.5f;

    /// <summary>The length of the hand in blocks.</summary>
    public const float HandLength = 0.3f;

    /// <summary>The arm: the sleeve with a cuff, then the hand.</summary>
    public static MeshBuffer Arm()
    {
        lock (Items)
        {
            if (_arm is null)
            {
                var buffer = new MeshBuffer();
                FigureMesher.AddBlock(buffer, new Box(-0.09f, -0.09f, -SleeveLength, 0.09f, 0.09f, 0.45f), BlockCell.Cloth, Sleeve);
                FigureMesher.AddBlock(buffer, new Box(-0.097f, -0.097f, -SleeveLength - 0.05f, 0.097f, 0.097f, -SleeveLength + 0.01f), BlockCell.Cloth, (232, 232, 224)); // the cuff
                FigureMesher.AddBlock(buffer, new Box(-0.075f, -0.075f, -SleeveLength - HandLength - 0.05f, 0.075f, 0.075f, -SleeveLength - 0.05f), BlockCell.Skin, SkinColour);
                _arm = buffer;
            }

            return _arm;
        }
    }

    /// <summary>The body and the glowing part (drawn unlit) of what the hand holds; both empty for the bare hand.</summary>
    public static (MeshBuffer Body, MeshBuffer Glow) Item(HeldChoice held)
    {
        lock (Items)
        {
            if (!Items.TryGetValue(held, out var meshes))
            {
                meshes = BuildItem(held);
                Items[held] = meshes;
            }

            return meshes;
        }
    }

    // The item sits above the hand's far end (z about -0.85), so the hand cups it from below.
    private static (MeshBuffer Body, MeshBuffer Glow) BuildItem(HeldChoice held)
    {
        var body = new MeshBuffer();
        var glow = new MeshBuffer();
        var z = -SleeveLength - 0.05f - (HandLength * 0.55f);
        var white = (byte.MaxValue, byte.MaxValue, byte.MaxValue);
        switch (held.Kind)
        {
            case HeldKind.Log:
                FigureMesher.AddBlock(body, new Box(-0.12f, 0.05f, z - 0.12f, 0.12f, 0.29f, z + 0.12f), BlockCell.TrunkSide, white, BlockCell.TrunkEnd);
                break;
            case HeldKind.Torch:
                FigureMesher.AddBlock(body, new Box(-0.03f, 0.05f, z - 0.03f, 0.03f, 0.42f, z + 0.03f), BlockCell.TorchStick, white);
                FigureMesher.AddBlock(glow, new Box(-0.05f, 0.42f, z - 0.05f, 0.05f, 0.54f, z + 0.05f), BlockCell.TorchFlame, white, null, true);
                FigureMesher.AddBlock(glow, new Box(-0.026f, 0.54f, z - 0.026f, 0.026f, 0.6f, z + 0.026f), BlockCell.TorchFlame, (255, 240, 160), null, true);
                break;
            case HeldKind.Bottle:
            case HeldKind.Sprite:
                // A flat picture standing in the hand, facing the eye, a little thick so it has an edge.
                FigureMesher.AddBlock(body, new Box(-0.14f, 0.03f, z - 0.012f, 0.14f, 0.31f, z + 0.012f), held.Cell, white);
                break;
            default:
                break;
        }

        return (body, glow);
    }
}
