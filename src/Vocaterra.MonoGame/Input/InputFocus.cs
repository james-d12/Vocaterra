namespace Vocaterra.MonoGame.Input;

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

/// <summary>
/// Key auto-repeat for an editing key, counted in frames so it needs no clock: the first press acts at once, then nothing for
/// <see cref="DelayFrames"/> frames, then once every <see cref="IntervalFrames"/> frames while the key stays down.
/// </summary>
public sealed class KeyRepeater
{
    /// <summary>Frames a held key waits before it repeats.</summary>
    public const int DelayFrames = 24;

    /// <summary>Frames between repeats.</summary>
    public const int IntervalFrames = 3;

    private int _held = -1;

    /// <summary>Call once per frame with whether the key is down; returns whether the key acts this frame.</summary>
    public bool Step(bool down)
    {
        if (!down)
        {
            _held = -1;
            return false;
        }

        _held++;
        return _held == 0 || (_held >= DelayFrames && (_held - DelayFrames) % IntervalFrames == 0);
    }
}
