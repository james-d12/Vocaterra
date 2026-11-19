namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>A container that claims some stacks for a quick move (I02): the equipment claims what can be worn, in the slot its <c>wear</c> names. A quick move from the bag asks each such container in registration order; the first that claims the stack decides.</summary>
public interface IQuickTarget : ISlotContainer
{
    /// <summary>Whether this container claims <paramref name="stack"/> for a quick move, and where it would go.</summary>
    /// <param name="stack">The stack.</param>
    /// <param name="slot">The slot it would go in, or -1 when the container claims the stack and refuses it.</param>
    /// <param name="refusal">Why it is refused, when <paramref name="slot"/> is -1.</param>
    /// <returns><see langword="false"/> when the container has no claim (a quick move goes elsewhere).</returns>
    bool Claims(ItemStack stack, out int slot, out MoveResult refusal);
}
