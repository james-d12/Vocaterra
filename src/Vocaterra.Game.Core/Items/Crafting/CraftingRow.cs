using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Items.Crafting;

/// <summary>One line of the crafting panel: a recipe, what the player has of each ingredient and whether it can be made now.</summary>
/// <param name="Index">The recipe's index in the book and the simulation (the argument of <c>PlayerCommand.Craft</c>).</param>
/// <param name="Recipe">The recipe with its nouns.</param>
/// <param name="Have">What the player carries of each ingredient, in the recipe's order.</param>
/// <param name="Result">What a craft would do now (<see cref="CraftResult.Done"/> when it can be made).</param>
public sealed record CraftingRow(int Index, RecipeEntry Recipe, IReadOnlyList<int> Have, CraftResult Result)
{
    /// <summary>Whether the recipe can be made right now, here.</summary>
    public bool CanMake => Result == CraftResult.Done;

    /// <summary>Whether the player carries enough of the ingredient at <paramref name="ingredient"/>.</summary>
    public bool HasEnough(int ingredient) => Have[ingredient] >= Recipe.Ingredients[ingredient].Count;

    /// <summary>
    /// The short status the row shows at its right edge: <c>Make</c> when it can be made, otherwise what is missing (<c>Missing material</c>,
    /// <c>Needs mesa</c>, <c>Inventory full</c>).
    /// </summary>
    /// <param name="ui">The support language (<c>ui.craft*</c>, C04; the station's lemma stays the target language).</param>
    public string Status(Content.LocaleText ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        return Result switch
        {
            CraftResult.Done => ui.Ui("craftMake"),
            CraftResult.MissingStation => ui.Ui("craftNeeds", ("station", Recipe.Station?.Lemma ?? ui.Ui("craftNeedsStation"))),
            CraftResult.InventoryFull => ui.Ui("craftFull"),
            _ => ui.Ui("craftMissing"),
        };
    }
}
