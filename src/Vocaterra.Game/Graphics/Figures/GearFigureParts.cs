using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Figures;

/// <summary>
/// The boxes the player's gear adds to the figure (I09): for each covered body part, slightly inflated boxes over the body in the part's colour (a head piece is a cap that leaves the face bare), and the worn bag as one box on the back (the player's own figure has no pack, I09b). The shapes are a table of rows
/// (<see cref="Shapes"/>): a new visible part is a new row and a new look key in the items, with no other code. A part key with no row draws nothing.
/// </summary>
public static class GearFigureParts
{
    /// <summary>The colour of a bag whose look gives no <c>back</c> colour (the leather of the NPCs' packs).</summary>
    public static readonly (byte R, byte G, byte B) BagColour = (140, 92, 50);

    /// <summary>The look part key a bag may colour itself with.</summary>
    public const string BackKey = "back";

    /// <summary>The part keys of the shipped looks that have a shape; <c>sleeve</c> is the appearance's resolved sleeve colour.</summary>
    public static IReadOnlyCollection<string> Keys => Shapes.Keys;

    // Each row: the key, then the boxes in figure pixels with the group each moves with. About a quarter pixel larger than the body part it covers on every side.
    private static readonly Dictionary<string, (Box Box, FigureGroup Group)[]> Shapes = new(StringComparer.Ordinal)
    {
        // A cap, not a box: a crown over the top, and a back-and-sides piece from the middle of the head to the back; the front's lower part (the face) stays bare.
        ["head"] = [(new Box(-5f, 30.6f, -5f, 5f, 33f, 4.98f), FigureGroup.Head), (new Box(-4.95f, 28f, 0f, 4.95f, 32.95f, 5f), FigureGroup.Head)],
        ["body"] = [(new Box(-4.25f, 9.8f, -2.75f, 4.25f, 22.2f, 2.75f), FigureGroup.Body)],
        ["sleeve"] = [(new Box(-7.25f, 14.3f, -2.25f, -4.1f, 22.25f, 2.25f), FigureGroup.ArmLeft), (new Box(4.1f, 14.3f, -2.25f, 7.25f, 22.25f, 2.25f), FigureGroup.ArmRight)],
        ["legs"] = [(new Box(-4f, 2.95f, -2.25f, -0.15f, 10.25f, 2.25f), FigureGroup.LegLeft), (new Box(0.15f, 2.95f, -2.25f, 4f, 10.25f, 2.25f), FigureGroup.LegRight)],
        ["feet"] = [(new Box(-4.05f, 0f, -2.55f, -0.05f, 3.15f, 2.75f), FigureGroup.LegLeft), (new Box(0.05f, 0f, -2.55f, 4.05f, 3.15f, 2.75f), FigureGroup.LegRight)],
    };

    /// <summary>The pack on the back: the same shape for every bag (a per-item shape is later).</summary>
    private static readonly Box Pack = new(-3.5f, 11f, 2.6f, 3.5f, 21f, 6.4f);

    /// <summary>The parts of the figure with the appearance on: the figure's own, then the gear over them.</summary>
    /// <param name="figure">The figure's parts (shared; it is not changed).</param>
    /// <param name="appearance">What shows.</param>
    /// <returns>The same array when nothing shows, otherwise a new one.</returns>
    public static FigurePart[] Dress(FigurePart[] figure, Appearance appearance)
    {
        ArgumentNullException.ThrowIfNull(figure);
        ArgumentNullException.ThrowIfNull(appearance);
        if (appearance.IsNone || (appearance.Parts.Count == 0 && appearance.Back is null))
        {
            return figure;
        }

        var parts = new List<FigurePart>(figure);
        foreach (var (key, shapes) in Shapes)
        {
            var colour = key == "sleeve" ? appearance.Sleeve : appearance.Parts.TryGetValue(key, out var own) ? own : (LookColour?)null;
            if (colour is not { } c)
            {
                continue;
            }

            foreach (var (box, group) in shapes)
            {
                parts.Add(new FigurePart(box, (c.R, c.G, c.B), BlockCell.Cloth, group));
            }
        }

        if (appearance.Back is { } bag)
        {
            var c = bag.Look?.Parts.TryGetValue(BackKey, out var own) == true ? own : new LookColour(BagColour.R, BagColour.G, BagColour.B);
            parts.Add(new FigurePart(Pack, (c.R, c.G, c.B), BlockCell.Cloth, FigureGroup.Body, BlockCell.Backpack, BlockFace.South));
        }

        return [.. parts];
    }

    /// <summary>How many boxes the appearance adds to the figure (for the frame-cost count).</summary>
    /// <param name="appearance">What shows.</param>
    public static int BoxesAdded(Appearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var count = 0;
        foreach (var (key, shapes) in Shapes)
        {
            if (key == "sleeve" ? appearance.Sleeve is not null : appearance.Parts.ContainsKey(key))
            {
                count += shapes.Length;
            }
        }

        return count + (appearance.Back is null ? 0 : 1);
    }
}
