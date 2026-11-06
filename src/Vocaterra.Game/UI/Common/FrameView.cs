using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Camera;
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.UI.Common;

/// <summary>
/// What a layer may read while drawing one frame: the simulation (read only by convention), the session, the first-person camera, the
/// viewport and the screens. The game owns and refreshes one instance per frame; layers must not keep it past the call.
/// </summary>
public sealed class FrameView
{
    /// <summary>The running game: the guide, the arrow, the words and the conversation (read only for layers).</summary>
    public required GuideSession Session { get; init; }

    /// <summary>The 3D simulation being shown: the body, the inventory, the tick and the events (read only for layers).</summary>
    public required VoxelSimulation Voxel { get; init; }

    /// <summary>The NPC figure the crosshair is on (any of the three, for the talk hint), or <see langword="null"/>.</summary>
    public NpcFigure? Aimed { get; set; }

    /// <summary>The dialogue panel's state.</summary>
    public required DialogueModel Dialogue { get; init; }

    /// <summary>The width of the window in pixels, for this frame.</summary>
    public int ViewportWidth { get; set; }

    /// <summary>The height of the window in pixels, for this frame.</summary>
    public int ViewportHeight { get; set; }

    /// <summary>The fonts (FontStashSharp over the committed TTF).</summary>
    public required FontProvider Fonts { get; init; }

    /// <summary>A 1 x 1 white texture: scale and tint it to draw rectangles.</summary>
    public required Texture2D Pixel { get; init; }

    /// <summary>The world seed.</summary>
    public required ulong Seed { get; init; }

    /// <summary>The journal screen's state.</summary>
    public required BookModel Book { get; init; }

    /// <summary>Which recording each row of the book plays (S20b); the book shows no button when it is <see cref="BookRecordings.None"/>.</summary>
    public BookRecordings Recordings { get; init; } = BookRecordings.None;

    /// <summary>The toasts waiting to be shown ("new word", the checklist's steps).</summary>
    public required ToastQueue Toasts { get; init; }

    /// <summary>The inventory screen's state (I or E).</summary>
    public required InventoryScreen Inventory { get; init; }

    /// <summary>The crafting panel's state (C, or E at the workbench).</summary>
    public required CraftingScreen Crafting { get; init; }

    /// <summary>The first-person camera of this frame.</summary>
    public required FirstPersonCamera Camera3D { get; init; }

    /// <summary>The icons of the items and blocks (the block atlas).</summary>
    public required IItemIcons VoxelIcons { get; init; }

    /// <summary>The beacon (where the player is being sent), or <see langword="null"/>.</summary>
    public Beacon? Beacon { get; init; }

    /// <summary>The NPC figures, whose names the label layer draws.</summary>
    public IReadOnlyList<NpcFigure> Figures { get; init; } = [];

    /// <summary>The camp objective (the checklist), or <see langword="null"/>.</summary>
    public Objective? Objective { get; init; }

    /// <summary>The mic check at the start (S7), or <see langword="null"/> when this run has none.</summary>
    public MicCheckModel? MicCheck { get; init; }

    /// <summary>The check battery's screen (S11) while it runs, or <see langword="null"/>.</summary>
    public CheckScreen? Check { get; set; }

    /// <summary>Whether the first-time tips are drawn (S16): an interactive run shows them, a scripted run only when asked.</summary>
    public bool ShowTips { get; set; }

    /// <summary>The bottom of the debug overlay this frame, or 0 when it is not drawn (U01): the tip box stands under it.</summary>
    public int DebugOverlayBottom { get; set; }

    /// <summary>The lesson strip's box this frame, or <see langword="null"/> (S22): it stands above the dialogue panel, and the conversation layout keeps the cards clear of it.</summary>
    public PixelBox? LessonStripBox { get; set; }

    /// <summary>The heading of the lesson step or offer in play (<c>Lesson · First words · step 1 of 7 · Say it</c>), or <see langword="null"/> (U02): the dialogue panel's header shows it.</summary>
    public string? LessonHeading { get; set; }

    /// <summary>Whether <c>--greyscale</c> is on (S22): the book draws the icon of a noun that is not known yet in grey, as the world draws its block.</summary>
    public bool Greyscale { get; init; }

    /// <summary>Whether the run is the playtest profile (S22): the facilitator's label has a reserved place and the bubble keeps below it.</summary>
    public bool Playtest { get; init; }

    /// <summary>The closing moment of the session (S17), or <see langword="null"/> when this view has none.</summary>
    public SessionEnding? Ending { get; init; }

    /// <summary>The bug and feature report panel (S21), or <see langword="null"/> when the view has none.</summary>
    public ReportPanel? Report { get; set; }

    /// <summary>Whether the settings panel (F8, N09) is open.</summary>
    public bool SettingsOpen { get; set; }

    /// <summary>The settings panel's chosen row.</summary>
    public int SettingsSelected { get; set; }

    /// <summary>The drive rate the settings panel shows.</summary>
    public double SettingsRate { get; set; } = 1;

    /// <summary>Whether the settings panel shows the view effects on.</summary>
    public bool SettingsEffectsOn { get; set; }

    /// <summary>Whether the settings panel shows the relaxed needs on.</summary>
    public bool SettingsRelaxed { get; set; }

    /// <summary>Whether the controls help (F1, S16) is open.</summary>
    public bool ControlsOpen { get; set; }

    /// <summary>The first control the help is scrolled to (X08), when the controls do not all fit the canvas.</summary>
    public int ControlsScroll { get; set; }

    /// <summary>The word cards waiting to be shown (the first pickup of each noun).</summary>
    public WordCardQueue Cards { get; init; } = new();

    /// <summary>The loading screen's text while the models load (the world is drawn behind it), or <see langword="null"/> when the game is running.</summary>
    public string? Loading { get; set; }

    /// <summary>Frames per second over the last half second.</summary>
    public double Fps { get; set; }

    /// <summary>The frames drawn so far while loading (animates the bar of the loading screen).</summary>
    public int LoadingFrame { get; set; }

    /// <summary>The prompt of the player's own lit fire for this frame (V12c: <c>E: sleep until morning (session 3)</c>), or <see langword="null"/> when it does not offer the sleep.</summary>
    public string? SleepPrompt { get; set; }

    /// <summary>The closing card's line offering the sleep for this frame (V12c: <c>Enter: sleep until morning and start session 2</c>), or <see langword="null"/>.</summary>
    public string? EndingSleep { get; set; }

    /// <summary>How black the world is for this frame, 0 (clear) to 1 (black): the fade of a collapse (N03).</summary>
    public float FadeAlpha { get; set; }

    /// <summary>What the HUD shows of the player's needs (N05), or <see langword="null"/> when this view has none.</summary>
    public NeedsHud? Needs { get; set; }

    /// <summary>The short screen after a death or a faint (N05): «Estás en tu campamento.» with its English and the cause line, or <see langword="null"/> when it is not up.</summary>
    public DeathScreenView? DeathScreen { get; set; }

    /// <summary>The mouse pointer in window pixels, or <see langword="null"/> when it is outside the window.</summary>
    public Point? Mouse { get; set; }

    /// <summary>The 3D eye position in blocks for this frame (east, up, south), interpolated between ticks.</summary>
    public Vector3 Eye { get; set; }

    /// <summary>The look for this frame in angle steps: yaw 0 north, pitch positive up.</summary>
    public (double Yaw, double Pitch) Look { get; set; }

    /// <summary>The inventory the hotbar and the inventory screen show.</summary>
    public Core.Items.Inventory Items => Voxel.Inventory;

    /// <summary>The selected hotbar slot.</summary>
    public int SelectedSlot => Voxel.SelectedSlot;

    /// <summary>The tick of the simulation (the toast and the cursor blink count it).</summary>
    public long Tick => Voxel.Tick;

    /// <summary>Draws an item's icon with the block atlas, in full colour (the world is in colour, D-game-6 as amended).</summary>
    public void DrawIcon(SpriteBatch spriteBatch, ItemId item, Rectangle destination) =>
        VoxelIcons.DrawIcon(spriteBatch, item, destination, 1.0);
}

/// <summary>
/// A piece of the screen drawn after the world, in order. A layer reads <see cref="FrameView"/> and draws with the <see cref="SpriteBatch"/> the
/// game has already begun (point sampling, alpha blend).
/// </summary>
public interface IFrameLayer
{
    /// <summary>Draws the layer on top of what is already on screen.</summary>
    void Draw(SpriteBatch spriteBatch, FrameView view);
}
