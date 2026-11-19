using Vocaterra.Game.Core.Items.Crafting;

namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>
/// Checks the durability data (I07): every durability names a wear source the code has, and every repair row of <c>game/recipes.json</c> repairs an item that can wear, takes exactly one material and names a station.
/// </summary>
public static class WearValidator
{
    /// <summary>The problems with the bundle's durability data, as messages for a content author; empty when it is sound.</summary>
    /// <param name="items">The item catalogue.</param>
    /// <param name="recipes">The recipe book.</param>
    /// <param name="sources">The wear sources the code has.</param>
    public static IReadOnlyList<string> Validate(IItemCatalogue items, RecipeBook recipes, WearSources sources)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(sources);
        var problems = new List<string>();
        foreach (var item in items.All.Where(static i => i.Durability is not null))
        {
            if (sources.Find(item.Durability!.Source) is null)
            {
                problems.Add($"{ItemCatalogue.RelativePath}: '{item.Name}' wears by the source '{item.Durability.Source}', which is not one of {string.Join(", ", sources.Ids)}.");
            }
        }

        foreach (var entry in recipes.Entries.Where(static e => e.Rule.Repairs))
        {
            var name = entry.Output.Lemma;
            if (items.Find(entry.Rule.Output) is { Durability: null })
            {
                problems.Add($"{RecipeBook.RelativePath}: the repair row for '{name}' repairs an item that has no durability.");
            }

            if (entry.Rule.Ingredients.Count != 1)
            {
                problems.Add($"{RecipeBook.RelativePath}: the repair row for '{name}' must use exactly one material.");
            }

            if (entry.Rule.Station is null)
            {
                problems.Add($"{RecipeBook.RelativePath}: the repair row for '{name}' names no station (repairs are done at the workbench).");
            }
        }

        return problems;
    }
}
