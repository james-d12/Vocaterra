using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Items.Crafting;

/// <summary>
/// One recipe as the game knows it: the nouns for the guide and the panel, and the plain <see cref="Rule"/> the simulation crafts with.
/// </summary>
/// <param name="Output">The noun of the item made (<c>mesa</c>).</param>
/// <param name="OutputCount">How many one craft gives.</param>
/// <param name="Ingredients">What one craft uses up, in the file's order.</param>
/// <param name="Station">The noun of the station (<c>mesa</c>), or <see langword="null"/> when the hands do.</param>
/// <param name="Rule">The simulation's value for the same recipe.</param>
public sealed record RecipeEntry(
    NounEntry Output,
    int OutputCount,
    IReadOnlyList<RecipeIngredient> Ingredients,
    NounEntry? Station,
    Recipe Rule);
