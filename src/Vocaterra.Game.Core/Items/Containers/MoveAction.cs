namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>What a slot move command asks for (I02). The numbers are in commands and scripted runs: never changed or reused, a new action is appended.</summary>
public enum MoveAction
{
    /// <summary>Left click on a slot: pick up the stack, put the held stack down, swap with what is there, or merge into a stack of the same item.</summary>
    LeftClick = 0,

    /// <summary>Right click on a slot: pick up the larger half; with a held stack, put one down.</summary>
    RightClick = 1,

    /// <summary>Shift-click on a slot: a wearable to its slot, a worn or other container's stack to the bag, otherwise the hotbar to the pack and back.</summary>
    QuickMove = 2,

    /// <summary>Sort the pack (every bag slot after the hotbar): same-item stacks merged, then catalogue order, then count descending. The container and slot of the command are ignored.</summary>
    Sort = 3,

    /// <summary>Return the held stack to the slot it came from, or the first free slot of the bag (what closing the panel does). The container and slot of the command are ignored.</summary>
    ReturnHeld = 4,
}
