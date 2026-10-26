using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Items;

/// <summary>An ingredient of a recipe: a number of one item kind.</summary>
/// <param name="Item">The item kind.</param>
/// <param name="Count">How many are used up (positive).</param>
public readonly record struct Ingredient(ItemId Item, int Count);

/// <summary>
/// A crafting recipe as plain values. The game layer reads them from the bundle's <c>game/recipes.json</c> and hands them to the simulation,
/// which has no JSON and no packages; the numbers live in the data, never in code.
/// </summary>
/// <param name="Output">The item made.</param>
/// <param name="OutputCount">How many of it one craft gives (positive).</param>
/// <param name="Ingredients">What one craft uses up.</param>
/// <param name="Station">The placed block kind (the workbench is <see cref="BlockId.Table"/>) that must be within reach, or <see langword="null"/> when the hands do.</param>
public sealed record Recipe(ItemId Output, int OutputCount, IReadOnlyList<Ingredient> Ingredients, BlockId? Station);

/// <summary>Why a craft did or did not happen.</summary>
public enum CraftResult
{
    /// <summary>The craft happened (or, for a query, can happen).</summary>
    Done = 0,

    /// <summary>The recipe index is not in the recipe list.</summary>
    NoSuchRecipe = 1,

    /// <summary>The inventory lacks an ingredient.</summary>
    MissingIngredients = 2,

    /// <summary>The recipe needs a station and none is within reach.</summary>
    MissingStation = 3,

    /// <summary>The output would not fit after the ingredients are taken out.</summary>
    InventoryFull = 4,
}

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
