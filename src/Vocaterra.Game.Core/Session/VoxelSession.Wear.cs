using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Wearing;

namespace Vocaterra.Game.Core.Session;

// Durability in the session (I07): a tool that wears out is a world event (tool_worn, once) and a short line on the HUD, said by the path the cold's line takes (the needs HUD drains it).
public sealed partial class VoxelSession
{
    private readonly Queue<string> _wearLines = new();

    /// <summary>The HUD line key waiting to be shown (a tool just wore out), taken and cleared; <see langword="null"/> when none waits.</summary>
    public string? TakeWearLine() => _wearLines.Count > 0 ? _wearLines.Dequeue() : null;

    private void NoteToolWorn(ItemId tool)
    {
        LogWorld(WorldEvents.ToolWorn, null, Nouns.ForItem(tool)?.Lemma ?? Data.Items.Get(tool).Name);
        _wearLines.Enqueue(WearText.ToolWorn);
    }
}
