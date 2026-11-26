using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Input;

/// <summary>
/// The world's own input while no screen holds it (S2, S15, S20): the mouse is captured for the look (Esc opens the menu, which lets it go; a click takes it back
/// without gathering), WASD or the arrows walk and Space jumps, 1 to 0 and the wheel pick the hotbar slot, the left button held gathers, the right
/// button places (or eats a selected food), Left Shift sprints, and the book item opens the book. The frame's commands go to <see cref="LiveVoxelInput"/>.
/// </summary>
public sealed class WorldInput
{
    private static readonly Keys[] SlotKeys = [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0];

    private readonly VoxelSimulation _sim;
    private readonly LookControl _look;
    private readonly LiveVoxelInput _live;
    private readonly IGameCommand _openBook;
    private readonly IGameCommand? _eat;
    private readonly NeedsData? _needs;
    private readonly Func<BlockPos, VoxelSession.PlantResult>? _plant;
    private readonly Func<BlockPos, VoxelSession.StakeResult>? _stake;
    private readonly Func<BlockPos, VoxelSession.CampResult>? _camp;
    private readonly MouseLook _mouseLook = new();
    private bool _holdClick;

    /// <summary>Creates the world's input.</summary>
    /// <param name="sim">The simulation: the hotbar's selection, the target block and the selected stack.</param>
    /// <param name="look">The player's look, which the mouse turns.</param>
    /// <param name="live">Where the frame's commands go.</param>
    /// <param name="openBook">Opens the book (the book item's use).</param>
    /// <param name="eat">Eats or drinks the selected food (the food item's use, N02), or <see langword="null"/> for none.</param>
    /// <param name="needs">The foods, which say what the right click eats.</param>
    /// <param name="plant">Plants the held berry on the ground block (N08), or <see langword="null"/> for none.</param>
    /// <param name="stake">Marks the pending plot round the block aimed at with the held stake (Y10), or <see langword="null"/> for none.</param>
    /// <param name="camp">Sets up the camp with the held camp kit on the ground aimed at (Y12), or <see langword="null"/> for none.</param>
    public WorldInput(VoxelSimulation sim, LookControl look, LiveVoxelInput live, IGameCommand openBook, IGameCommand? eat = null, NeedsData? needs = null, Func<BlockPos, VoxelSession.PlantResult>? plant = null, Func<BlockPos, VoxelSession.StakeResult>? stake = null, Func<BlockPos, VoxelSession.CampResult>? camp = null)
    {
        _stake = stake;
        _camp = camp;
        _eat = eat;
        _needs = needs;
        _plant = plant;
        _sim = sim;
        _look = look;
        _live = live;
        _openBook = openBook;
    }

    /// <summary>Whether the player wants the mouse captured (a click in the world takes it back).</summary>
    public bool CaptureWanted { get; private set; } = true;

    /// <summary>Whether the sprint key is held with the mouse captured and a walking key down (the frame's, N02; the session decides whether it counts).</summary>
    public bool SprintHeld { get; private set; }

    /// <summary>How much one pixel of mouse movement turns the view, as a multiple of the normal turn (the menu's mouse sensitivity).</summary>
    public double MouseSensitivity { get; set; } = 1.0;

    /// <summary>Whether moving the mouse up looks down (the menu's Invert Y).</summary>
    public bool InvertY { get; set; }

    /// <summary>The left button is held down from a click that closed a panel (the menu's Resume): it gathers nothing until it has been let go.</summary>
    public void HoldClickUntilRelease() => _holdClick = true;

    /// <summary>A frame that a panel took: the mouse is free.</summary>
    public void FreeMouse(GameWindow window) => _mouseLook.SetCaptured(false, window);

    /// <summary>The walk of a frame: strafe to the right, forward and the jump, from WASD or the arrows and Space.</summary>
    public static (int Strafe, int Forward, bool Jump) Walk(InputSnapshot input) =>
        ((input.Down(Keys.D) || input.Down(Keys.Right) ? 1 : 0) - (input.Down(Keys.A) || input.Down(Keys.Left) ? 1 : 0),
            (input.Down(Keys.W) || input.Down(Keys.Up) ? 1 : 0) - (input.Down(Keys.S) || input.Down(Keys.Down) ? 1 : 0),
            input.Down(Keys.Space));

    /// <summary>The hotbar slot a frame picks: a number key (1 to 9, 0 for the tenth), or the wheel (a turn up picks the slot before), else none.</summary>
    public static int? Slot(InputSnapshot input, int selected)
    {
        int? slot = null;
        for (var i = 0; i < SlotKeys.Length; i++)
        {
            if (input.Pressed(SlotKeys[i]))
            {
                slot = i;
            }
        }

        if (input.Wheel != 0)
        {
            slot = (((selected - Math.Sign(input.Wheel)) % Inventory.HotbarSize) + Inventory.HotbarSize) % Inventory.HotbarSize;
        }

        return slot;
    }

    /// <summary>
    /// The world's input of a frame. Returns whether the mouse is captured (the pointer is hidden then).
    /// </summary>
    /// <param name="input">The frame's input.</param>
    /// <param name="active">Whether the window has the focus.</param>
    /// <param name="uiOpen">Whether a screen frees the mouse and holds the world's input.</param>
    /// <param name="bookOpen">Whether the book is open.</param>
    /// <param name="window">The window the mouse is captured in.</param>
    public bool Read(InputSnapshot input, bool active, bool uiOpen, bool bookOpen, GameWindow window)
    {
        var swallowClick = false;
        if (_holdClick)
        {
            _holdClick = input.LeftDown; // the click that closed the menu does not gather
            swallowClick = _holdClick;
        }

        if (active && !uiOpen && !CaptureWanted && input.LeftClicked)
        {
            CaptureWanted = true; // a click takes the mouse back; it does not gather
            swallowClick = true;
        }

        var captured = active && !uiOpen && CaptureWanted;
        _mouseLook.SetCaptured(captured, window);
        if (captured)
        {
            var (dx, dy) = _mouseLook.Read(window);
            _look.Turn(dx, InvertY ? -dy : dy, MouseSensitivity);
        }

        var (strafe, forward, jump) = captured ? Walk(input) : (0, 0, false);
        SprintHeld = captured && (strafe != 0 || forward != 0) && input.Down(InputKeys.Of("sprint"));
        var slot = uiOpen ? null : Slot(input, _sim.SelectedSlot);
        var target = _sim.Target;
        if (active && BookItemUse.Applies(input, captured, bookOpen, _sim.SelectedStack))
        {
            _openBook.Execute(); // the book item opens the book, as J does (S20)
        }

        // A berry on grass or dirt is planted at the camp (N08); the click in the air still eats it (N02).
        var planted = active && _plant is not null && _needs is not null && target is { } ground
            && PlantingUse.Applies(input, captured, _sim.SelectedStack, target, _needs, _sim.Inventory.Items) && _plant(ground.Block) != VoxelSession.PlantResult.NotApplied;
        if (active && _stake is not null && target is { } aimed && HeldItemUse.Applies(input, captured, _sim.SelectedStack, target, ItemId.Stake))
        {
            _ = _stake(aimed.Block); // the stake marks the pending plot round the aimed column (Y10); the session says the answer on the hint line
        }

        if (active && _camp is not null && target is { } campAim && HeldItemUse.Applies(input, captured, _sim.SelectedStack, target, ItemId.CampKit))
        {
            _ = _camp(campAim.Block); // the kit sets the camp up on the aimed ground (Y12); the session says the answer on the hint line
        }

        if (!planted && active && _eat is not null && _needs is not null && FoodItemUse.Applies(input, captured, _sim.SelectedStack, _needs, _sim.Inventory.Items))
        {
            _eat.Execute(); // a food item is eaten, the water bottle drunk (N02)
        }

        _live.SetFrame(
            strafe,
            forward,
            jump,
            captured && input.LeftDown && !swallowClick ? LiveVoxelInput.GatherFor(_sim.InteractTarget, _sim.BuildRules, _sim.BreakAll) : null,
            captured && input.RightClicked ? LiveVoxelInput.PlacementFor(_sim.SelectedStack, target) : null,
            slot,
            captured && input.Down(InputKeys.Of("flyDown")));
        return captured;
    }
}
