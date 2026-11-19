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
/// <param name="Repairs">Whether this row repairs <paramref name="Output"/> instead of making it (I07): it uses the ingredients, takes the most worn stack of the item (in the bag or worn) back to no wear and makes nothing.</param>
public sealed record Recipe(ItemId Output, int OutputCount, IReadOnlyList<Ingredient> Ingredients, BlockId? Station, bool Repairs = false);
