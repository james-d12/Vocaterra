using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Screens;

/// <summary>The inventory panel (I, or E away from an NPC): every slot with its noun, the nouns the player carries learnt at a glance; Esc, I or E closes it.</summary>
public sealed class InventoryPanel : IScreen
{
    private readonly InventoryScreen _inventory;
    private readonly InputFocus _focus;
    private readonly VoxelSession _session;
    private readonly Func<bool> _bookOpen;

    /// <summary>Creates the panel's behaviour.</summary>
    /// <param name="inventory">The panel's state.</param>
    /// <param name="focus">Where the keyboard goes.</param>
    /// <param name="session">The session whose inventory and words the panel shows.</param>
    /// <param name="bookOpen">Whether the book is open (the inventory does not open over it).</param>
    public InventoryPanel(InventoryScreen inventory, InputFocus focus, VoxelSession session, Func<bool> bookOpen)
    {
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

    /// <summary>Closes the panel.</summary>
    public void Close()
    {
        _inventory.Close();
        _focus.InventoryOpen = false;
    }

    /// <summary>The panel's keys while it is open: Esc, I or E closes it.</summary>
    public void HandleInput(IInputFrame input)
    {
        if (input.Pressed(GameKey.Escape) || input.Pressed(GameKey.I) || input.Pressed(GameKey.E))
        {
            Close();
        }
    }
}
