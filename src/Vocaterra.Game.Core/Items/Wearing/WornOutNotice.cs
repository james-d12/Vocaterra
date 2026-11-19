namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>An item that has just reached its durability (I07): said once, the moment it wears out.</summary>
/// <param name="Item">The item that wore out.</param>
/// <param name="IsTool">Whether it is a tool (the HUD says so for a tool: <c>tool_worn</c>).</param>
public readonly record struct WornOutNotice(ItemId Item, bool IsTool);
