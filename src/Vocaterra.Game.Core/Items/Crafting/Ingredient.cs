using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Items.Crafting;

/// <summary>An ingredient of a recipe: a number of one item kind.</summary>
/// <param name="Item">The item kind.</param>
/// <param name="Count">How many are used up (positive).</param>
public readonly record struct Ingredient(ItemId Item, int Count);
