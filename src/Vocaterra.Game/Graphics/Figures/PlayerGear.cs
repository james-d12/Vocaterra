using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Figures;

/// <summary>
/// What the player's gear looks like, rebuilt only when the equipment changes (I09; <see cref="Equipment.Revision"/> moves): the <see cref="Appearance"/>, the figure's parts with the gear's boxes on them, and the
/// off-hand item's mesh. A frame with nothing changed reads these and allocates nothing. Nothing worn gives the figure's own parts (the same array as without gear) and no off-hand mesh.
/// </summary>
public sealed class PlayerGear
{
    /// <summary>The origin of the off-hand item in the figure's own pixels: the middle of the left hand, a little below its bottom, so the hand cups the item.</summary>
    public static readonly (float X, float Y, float Z) OffHandOrigin = (-5.5f, 11.2f, -1.2f);

    private readonly Equipment _equipment;
    private readonly AppearanceRoles _roles;
    private int _revision = -1;

    /// <summary>Watches <paramref name="equipment"/>.</summary>
    /// <param name="equipment">The player's equipment.</param>
    /// <param name="roles">The words the appearance looks for, or <see langword="null"/> for the shipped ones.</param>
    public PlayerGear(Equipment equipment, AppearanceRoles? roles = null)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        _equipment = equipment;
        _roles = roles ?? AppearanceRoles.Default;
        Appearance = Appearance.None;
        Parts = NpcFigures.PartsOf(NpcFigures.PlayerName);
    }

    /// <summary>What shows now (call <see cref="Refresh"/> first).</summary>
    public Appearance Appearance { get; private set; }

    /// <summary>The figure's parts with the gear on them.</summary>
    public FigurePart[] Parts { get; private set; }

    /// <summary>The off-hand item extruded from its icon in blocks, or <see langword="null"/> when the hand is empty.</summary>
    public MeshBuffer? OffHand { get; private set; }

    /// <summary>The off-hand item's picture and shape in the first-person hand, or <see langword="null"/> when the hand is empty.</summary>
    public HeldChoice? OffHandHeld { get; private set; }

    /// <summary>How many boxes the gear adds to the figure (the off-hand item's voxel faces are in <see cref="OffHand"/>).</summary>
    public int BoxesAdded { get; private set; }

    /// <summary>Rebuilds what the equipment's change made stale; nothing happens when it did not change.</summary>
    public void Refresh()
    {
        if (_equipment.Revision == _revision)
        {
            return;
        }

        _revision = _equipment.Revision;
        Appearance = AppearanceBuilder.Of(_equipment, _roles);
        Parts = GearFigureParts.Dress(NpcFigures.PartsOf(NpcFigures.PlayerName), Appearance);
        BoxesAdded = GearFigureParts.BoxesAdded(Appearance);
        if (Appearance.OffHand is { } item)
        {
            var held = HeldItems.For(item);
            OffHandHeld = held;
            var mesh = new MeshBuffer();
            ItemModels.AppendExtruded(mesh, BlockAtlasLayout.IconFor(item), ArmMeshes.ItemPixel, 0f, 0f);
            OffHand = mesh;
        }
        else
        {
            OffHand = null;
            OffHandHeld = null;
        }
    }
}
