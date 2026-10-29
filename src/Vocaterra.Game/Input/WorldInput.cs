using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Simulation;

namespace Vocaterra.Game.Input;

/// <summary>
/// The world's own input while no screen holds it (S2, S15, S20): the mouse is captured for the look (Esc lets it go, a click takes it back
/// without gathering), WASD or the arrows walk and Space jumps, 1 to 0 and the wheel pick the hotbar slot, the left button held gathers, the right
/// button places, and the book item opens the book. The frame's commands go to <see cref="LiveVoxelInput"/>.
/// </summary>
public sealed class WorldInput
{
    private static readonly Keys[] SlotKeys = [Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0];

    private readonly VoxelSimulation _sim;
    private readonly LookControl _look;
    private readonly LiveVoxelInput _live;
    private readonly IGameCommand _openBook;
    private readonly MouseLook _mouseLook = new();

    /// <summary>Creates the world's input.</summary>
    /// <param name="sim">The simulation: the hotbar's selection, the target block and the selected stack.</param>
    /// <param name="look">The player's look, which the mouse turns.</param>
    /// <param name="live">Where the frame's commands go.</param>
    /// <param name="openBook">Opens the book (the book item's use).</param>
    public WorldInput(VoxelSimulation sim, LookControl look, LiveVoxelInput live, IGameCommand openBook)
    {
        _sim = sim;
        _look = look;
        _live = live;
        _openBook = openBook;
    }

    /// <summary>Whether the player wants the mouse captured (Esc lets it go; a click in the world takes it back).</summary>
    public bool CaptureWanted { get; private set; } = true;

    /// <summary>Esc in the world: the mouse is let go.</summary>
    public void ReleaseMouse() => CaptureWanted = false;

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
            _look.Turn(dx, dy);
        }

        var (strafe, forward, jump) = captured ? Walk(input) : (0, 0, false);
        var slot = uiOpen ? null : Slot(input, _sim.SelectedSlot);
        var target = _sim.Target;
        if (active && BookItemUse.Applies(input, captured, bookOpen, _sim.SelectedStack))
        {
            _openBook.Execute(); // the book item opens the book, as J does (S20)
        }

        _live.SetFrame(
            strafe,
            forward,
            jump,
            captured && input.LeftDown && !swallowClick ? LiveVoxelInput.GatherFor(target) : null,
            captured && input.RightClicked ? LiveVoxelInput.PlacementFor(_sim.SelectedStack, target) : null,
            slot);
        return captured;
    }
}
