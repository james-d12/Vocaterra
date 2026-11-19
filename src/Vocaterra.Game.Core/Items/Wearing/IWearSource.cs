namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>
/// One cause of wear (I07, inventory-plan 3.12): <c>use</c> for the tool in the hand, <c>time</c> for what is worn, <c>damage</c> for the armour that takes a hit. An item's <see cref="DurabilitySpec.Source"/> names the source that wears it; the
/// source says which slots an occurrence may reach, and <see cref="ItemWear.Apply"/> does the rest (the rate, the clamp, the worn-out notice). A new cause is a new class and one registration in <see cref="WearSources"/>: no other source and no item changes.
/// </summary>
public interface IWearSource
{
    /// <summary>The id an item's durability names it by.</summary>
    string Id { get; }

    /// <summary>The slots one occurrence of this cause reaches now. It reads only simulation state (no clock, no random numbers).</summary>
    /// <param name="wear">The wear engine, which knows the bag, the worn items and the selected hotbar slot.</param>
    IEnumerable<WearSlot> Targets(ItemWear wear);
}
