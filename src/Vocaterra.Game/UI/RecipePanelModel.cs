using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.UI;

/// <summary>
/// The workbench's recipe panel (S15, reference C): the item the recipe makes at the top, and below it the ingredients, each as an icon with its <c>have/need</c>
/// count. It is drawn whenever the player stands at a station that a recipe needs, so the player sees what to bring without opening the recipe book (C). A pure
/// model over the crafting rows: the numbers are the ones the simulation will check when the craft is tried.
/// </summary>
/// <param name="Index">The recipe's index in the book (the argument of a craft).</param>
/// <param name="Output">The item the recipe makes.</param>
/// <param name="OutputCount">How many of it one craft makes.</param>
/// <param name="Ingredients">The ingredients in the recipe's order.</param>
/// <param name="CanMake">Whether the craft would succeed right now.</param>
public sealed record RecipePanelModel(int Index, ItemId Output, int OutputCount, IReadOnlyList<RecipeIngredientCount> Ingredients, bool CanMake)
{
    /// <summary>The panel of one crafting row.</summary>
    public static RecipePanelModel From(CraftingRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var ingredients = new List<RecipeIngredientCount>();
        for (var i = 0; i < row.Recipe.Ingredients.Count; i++)
        {
            var part = row.Recipe.Ingredients[i];
            ingredients.Add(new RecipeIngredientCount(part.Item, row.Have[i], part.Count));
        }

        return new RecipePanelModel(row.Index, row.Recipe.Rule.Output, row.Recipe.OutputCount, ingredients, row.CanMake);
    }

    /// <summary>
    /// The panels to show: the recipes that need a station, when the station is within reach (whether or not the player has the material). Nothing is shown away from
    /// every station, and a recipe made by hand has no panel.
    /// </summary>
    /// <param name="rows">The crafting rows.</param>
    /// <param name="stationInReach">Whether a block of that kind is within the player's reach (<c>VoxelSimulation.StationInReach</c>).</param>
    public static IReadOnlyList<RecipePanelModel> AtStation(IReadOnlyList<CraftingRow> rows, Func<BlockId, bool> stationInReach)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(stationInReach);
        return rows.Where(r => r.Recipe.Rule.Station is { } station && stationInReach(station)).Select(From).ToList();
    }
}
