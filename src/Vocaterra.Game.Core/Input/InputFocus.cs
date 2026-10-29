namespace Vocaterra.Game.Core.Input;

/// <summary>
/// Who gets the keyboard: the player's body or a text box. While a text box has the focus, the movement keys are letters for it, not
/// commands for the player.
/// </summary>
public sealed class InputFocus
{
    /// <summary>Whether a text box has the keyboard. The dialogue panel sets it when it opens and clears it when it closes.</summary>
    public bool TextEntryActive { get; set; }

    /// <summary>Whether the journal screen is open: the player does not walk while it is.</summary>
    public bool BookOpen { get; set; }

    /// <summary>Whether the crafting panel is open: the player does not walk while it is.</summary>
    public bool CraftingOpen { get; set; }

    /// <summary>Whether the inventory screen is open: the player does not walk while it is (the number keys and the mouse still work).</summary>
    public bool InventoryOpen { get; set; }
}
