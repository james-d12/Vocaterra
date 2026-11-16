using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;

namespace Vocaterra.Game.Core.Items;

/// <summary>
/// The inventory panel (I, or E away from an NPC): every slot with its noun, the nouns the player carries learnt at a glance; Esc, I or E closes it. Clicks move items by hand (I02): left click picks up, puts down, swaps or merges,
/// right click takes half or puts one down, shift-click moves a stack to where it belongs, the sort button sorts the pack and the trash cell discards. Each is a <see cref="VoxelCommand"/> queued for the next tick
/// (<see cref="TakeMoves"/>), as the crafting panel's crafts are. Closing the panel queues the return of the held stack.
/// </summary>
public sealed class InventoryPanel : IScreen
{
    private readonly InventoryScreen _inventory;
    private readonly InputFocus _focus;
    private readonly VoxelSession _session;
    private readonly Func<bool> _bookOpen;
    private readonly Func<int, int, InventoryHit>? _hitAt;
    private readonly Queue<VoxelCommand> _pending = new();

    /// <summary>Creates the panel's behaviour.</summary>
    /// <param name="inventory">The panel's state.</param>
    /// <param name="focus">Where the keyboard goes.</param>
    /// <param name="session">The session whose inventory and words the panel shows.</param>
    /// <param name="bookOpen">Whether the book is open (the inventory does not open over it).</param>
    /// <param name="hitAt">What the screen has at a point of the canvas (the screen's layout, from the game), or <see langword="null"/> for a panel that takes no clicks.</param>
    public InventoryPanel(InventoryScreen inventory, InputFocus focus, VoxelSession session, Func<bool> bookOpen, Func<int, int, InventoryHit>? hitAt = null)
    {
        _hitAt = hitAt;
        _inventory = inventory;
        _focus = focus;
        _session = session;
        _bookOpen = bookOpen;
    }

    /// <inheritdoc />
    public string Name => "inventory";

    /// <inheritdoc />
    public bool IsOpen => _inventory.IsOpen;

    /// <inheritdoc />
    public bool FreesMouse => true;

    /// <summary>Opens the panel with the nouns of the items carried, unless the book is open.</summary>
    public void Open()
    {
        if (_bookOpen())
        {
            return;
        }

        var lemmas = _session.Simulation.Inventory.Slots
            .Where(static s => !s.IsEmpty)
            .Select(s => _session.Nouns.ForItem(s.Item)?.Lemma)
            .OfType<string>()
            .ToList();
        _inventory.Open(_session.Words, lemmas);
        _focus.InventoryOpen = true;
    }

    /// <summary>Closes the panel and queues the return of the held stack to the slot it came from, or the bag. When the bag is full what is left stays held until there is room (see <see cref="VoxelSimulation.ReturnHeld"/>).</summary>
    public void Close()
    {
        if (_inventory.IsOpen)
        {
            _pending.Enqueue(VoxelCommand.ReturnHeld());
        }

        _inventory.Close();
        _focus.InventoryOpen = false;
    }

    /// <summary>The panel's keys while it is open: Esc, I or E closes it.</summary>
    public void HandleInput(IInputFrame input)
    {
        if (input.Pressed(GameKey.Escape) || input.Pressed(InputKeys.Of("inventory")) || input.Pressed(InputKeys.Of("talk")))
        {
            Close();
        }
    }

    /// <summary>A click of the open panel: left click, right click or shift-click on a bag slot or the trash cell, or a left click on the sort button, queues its move for the next tick.</summary>
    /// <param name="input">The frame's input: which button went down, whether Shift is held and where the pointer is.</param>
    public void Click(IInputFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_hitAt is null || !_inventory.IsOpen || !(input.LeftClicked || input.RightClicked))
        {
            return;
        }

        var hit = _hitAt(input.PointerX, input.PointerY);
        if (hit.Kind == InventoryHitKind.Sort)
        {
            if (input.LeftClicked)
            {
                _pending.Enqueue(VoxelCommand.SortPack());
            }

            return;
        }

        var (container, slot) = hit.Kind switch
        {
            InventoryHitKind.Cell => (ContainerIds.Bag, hit.Slot),
            InventoryHitKind.Trash => (ContainerIds.Trash, 0),
            _ => (-1, 0),
        };
        if (container < 0)
        {
            return;
        }

        var action = input.LeftClicked ? (input.Shift ? MoveAction.QuickMove : MoveAction.LeftClick) : MoveAction.RightClick;
        _pending.Enqueue(VoxelCommand.SlotMove(action, container, slot));
    }

    /// <summary>The moves asked for since the last tick, as the tick's commands.</summary>
    /// <param name="commands">The tick's commands.</param>
    public void TakeMoves(List<VoxelCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        while (_pending.Count > 0)
        {
            commands.Add(_pending.Dequeue());
        }
    }
}
