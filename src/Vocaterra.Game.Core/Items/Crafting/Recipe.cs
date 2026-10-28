using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Items.Crafting;

/// <summary>
/// A crafting recipe as plain values. The game layer reads them from the bundle's <c>game/recipes.json</c> and hands them to the simulation,
/// which has no JSON and no packages; the numbers live in the data, never in code.
/// </summary>
/// <param name="Output">The item made.</param>
/// <param name="OutputCount">How many of it one craft gives (positive).</param>
/// <param name="Ingredients">What one craft uses up.</param>
/// <param name="Station">The placed block kind (the workbench is <see cref="BlockId.Table"/>) that must be within reach, or <see langword="null"/> when the hands do.</param>
public sealed record Recipe(ItemId Output, int OutputCount, IReadOnlyList<Ingredient> Ingredients, BlockId? Station);
