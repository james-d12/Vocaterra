using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame;

/// <summary>
/// What a layer may read while drawing one frame: the simulation (read only by convention), the camera for this frame, the interpolated
/// player box and the frame statistics. The game owns and refreshes one instance per frame; layers must not keep it past the call.
/// </summary>
public sealed class FrameView
{
    /// <summary>The simulation being shown.</summary>
    public required GameSimulation Simulation { get; init; }

    /// <summary>The running game: the guide, the arrow and the conversation (read only for layers).</summary>
    public required GameSession Session { get; init; }

    /// <summary>The dialogue panel's state.</summary>
    public required DialogueModel Dialogue { get; init; }

    /// <summary>The camera, already following the player for this frame.</summary>
    public required Camera Camera { get; init; }

    /// <summary>The fonts (FontStashSharp over the committed TTF).</summary>
    public required FontProvider Fonts { get; init; }

    /// <summary>A 1 x 1 white texture: scale and tint it to draw rectangles.</summary>
    public required Texture2D Pixel { get; init; }

    /// <summary>The world seed.</summary>
    public required ulong Seed { get; init; }

    /// <summary>The tile atlas (colour and grey), for the journal's icons.</summary>
    public required TileAtlas Atlas { get; init; }

    /// <summary>The journal screen's state.</summary>
    public required JournalModel Journal { get; init; }

    /// <summary>The toasts waiting to be shown ("new word").</summary>
    public required ToastQueue Toasts { get; init; }

    /// <summary>The inventory screen's state (I or E).</summary>
    public required InventoryScreen Inventory { get; init; }

    /// <summary>The crafting panel's state (C).</summary>
    public required CraftingScreen Crafting { get; init; }

    /// <summary>The player box between the previous and the latest tick (see <see cref="TickAccumulator.Alpha"/>), in simulation units.</summary>
    public Rectangle PlayerUnits { get; set; }

    /// <summary>The way the player faces: -1 left, 1 right.</summary>
    public int Facing { get; set; } = 1;

    /// <summary>Frames per second over the last half second.</summary>
    public double Fps { get; set; }

    /// <summary>The ticks dropped so far because a frame fell too far behind.</summary>
    public long DroppedTicks { get; set; }

    /// <summary>The text of the loading screen while the models load (the world is drawn behind it and nothing in it is interactive), or <see langword="null"/> when the game is running.</summary>
    public string? Loading { get; set; }

    /// <summary>The frames drawn so far while loading (animates the bar of the loading screen).</summary>
    public int LoadingFrame { get; set; }

    /// <summary>The mouse pointer in window pixels, or <see langword="null"/> when it is outside the window.</summary>
    public Point? Mouse { get; set; }

    /// <summary>
    /// The 3D simulation when the run is <c>--voxel</c>, or <see langword="null"/> for the 2D slice. Layers that do not depend on the 2D world
    /// (the hotbar, the toast, the inventory) read the inventory and the tick through <see cref="Items"/>, <see cref="SelectedSlot"/> and
    /// <see cref="Tick"/>, which follow it when it is set.
    /// </summary>
    public VoxelSimulation? Voxel { get; init; }

    /// <summary>The first-person camera of a 3D run, or <see langword="null"/>.</summary>
    public FirstPersonCamera? Camera3D { get; init; }

    /// <summary>The beacon of a 3D run (where the player is being sent), or <see langword="null"/>.</summary>
    public VoxelBeacon? Beacon { get; init; }

    /// <summary>The NPC figures of a 3D run, whose names the label layer draws.</summary>
    public IReadOnlyList<NpcFigure> Figures { get; init; } = [];

    /// <summary>The icons of a 3D run's items (the block atlas), or <see langword="null"/> for the 2D tile atlas.</summary>
    public IItemIcons? VoxelIcons { get; init; }

    /// <summary>The 3D eye position in blocks for this frame (east, up, south), interpolated between ticks.</summary>
    public Vector3 Eye { get; set; }

    /// <summary>The look of the 3D run in angle steps for this frame: yaw 0 north, pitch positive up.</summary>
    public (double Yaw, double Pitch) Look { get; set; }

    /// <summary>The inventory the hotbar and the inventory screen show: the 3D simulation's in a 3D run, the 2D one's otherwise.</summary>
    public Inventory Items => Voxel?.Inventory ?? Simulation.Inventory;

    /// <summary>The selected hotbar slot of whichever simulation is running.</summary>
    public int SelectedSlot => Voxel?.SelectedSlot ?? Simulation.SelectedSlot;

    /// <summary>The tick of whichever simulation is running (the toast and the cursor blink count it).</summary>
    public long Tick => Voxel?.Tick ?? Simulation.Tick;

    /// <summary>The saturation an item's icon is drawn at: the word's in the 2D slice, full colour in the 3D world (D-game-6 as amended).</summary>
    public double IconSaturation(ItemId item) => Voxel is null ? Session.Words.SaturationOf(item) : 1.0;

    /// <summary>Draws an item's icon with the atlas of whichever world is running.</summary>
    public void DrawIcon(SpriteBatch spriteBatch, ItemId item, Rectangle destination) =>
        (VoxelIcons ?? Atlas).DrawIcon(spriteBatch, item, destination, IconSaturation(item));
}

/// <summary>
/// A piece of the screen drawn after the world, in order: the guide, the arrow, the debug overlay, the dialogue panel (P3), the toast and the journal (P4); the inventory UI (P6) later.
/// A layer reads <see cref="FrameView"/> and draws with the <see cref="SpriteBatch"/> the game has already begun (point sampling, alpha blend).
/// </summary>
public interface IFrameLayer
{
    /// <summary>Draws the layer on top of what is already on screen.</summary>
    void Draw(SpriteBatch spriteBatch, FrameView view);
}
