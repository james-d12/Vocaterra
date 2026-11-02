using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Items.Crafting;

/// <summary>One ingredient of a recipe with the noun that names it.</summary>
/// <param name="Noun">The catalogue noun of the ingredient (<c>madera</c>).</param>
/// <param name="Item">The item kind it stands for.</param>
/// <param name="Count">How many one craft uses.</param>
public sealed record RecipeIngredient(NounEntry Noun, ItemId Item, int Count);
