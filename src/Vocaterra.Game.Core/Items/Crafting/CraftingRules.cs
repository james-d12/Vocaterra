namespace Vocaterra.Game.Core.Items.Crafting;

/// <summary>The pure crafting rule over an inventory: no world, no clock, integer arithmetic.</summary>
public static class CraftingRules
{
    /// <summary>Whether <paramref name="recipe"/> can be crafted now, given the inventory and whether its station is within reach.</summary>
    public static CraftResult Check(Recipe recipe, Inventory inventory, bool stationInReach)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(inventory);
        foreach (var ingredient in recipe.Ingredients)
        {
            if (inventory.Count(ingredient.Item) < ingredient.Count)
            {
                return CraftResult.MissingIngredients;
            }
        }

        if (recipe.Station is not null && !stationInReach)
        {
            return CraftResult.MissingStation;
        }

        var scratch = inventory.Copy();
        foreach (var ingredient in recipe.Ingredients)
        {
            scratch.Remove(ingredient.Item, ingredient.Count);
        }

        return scratch.CanAdd(recipe.Output, recipe.OutputCount) ? CraftResult.Done : CraftResult.InventoryFull;
    }

    /// <summary>Crafts <paramref name="recipe"/> into <paramref name="inventory"/> when <see cref="Check"/> says so; otherwise changes nothing.</summary>
    public static CraftResult Craft(Recipe recipe, Inventory inventory, bool stationInReach)
    {
        var result = Check(recipe, inventory, stationInReach);
        if (result != CraftResult.Done)
        {
            return result;
        }

        foreach (var ingredient in recipe.Ingredients)
        {
            inventory.Remove(ingredient.Item, ingredient.Count);
        }

        inventory.Add(recipe.Output, recipe.OutputCount);
        return CraftResult.Done;
    }
}
