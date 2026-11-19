using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Simulation;

namespace Vocaterra.Game.Core.Items.Crafting;

/// <summary>
/// The crafting panel (key C): open or closed, its rows and the words it shows. Opening it counts each shown noun as read once (the same rule
/// as the inventory and the journal: seeing a label is a "read", never a spoken credit), so looking at the recipes again does not move any
/// colour. Making an item is a <c>PlayerCommand.Craft</c> the game sends when a row is clicked.
/// </summary>
public sealed class CraftingScreen
{
    /// <summary>Whether the panel is shown. While it is, the player does not walk and the mouse does not mine or place.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Opens the panel and marks the given catalogue words as read, once each.</summary>
    /// <param name="words">The learner's word progress.</param>
    /// <param name="lemmas">The lemmas the panel shows (see <see cref="LemmasShown"/>); any other lemma is ignored by the progress.</param>
    public void Open(WordProgress words, IEnumerable<string> lemmas)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(lemmas);
        foreach (var lemma in lemmas.Distinct(StringComparer.Ordinal))
        {
            words.MarkRead(lemma);
        }

        IsOpen = true;
    }

    /// <summary>Closes the panel.</summary>
    public void Close() => IsOpen = false;

    /// <summary>The nouns the panel shows: every output, ingredient and station of the book, each once, in the order they first appear.</summary>
    public static IReadOnlyList<string> LemmasShown(RecipeBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var lemmas = new List<string>();
        foreach (var entry in book.Entries)
        {
            lemmas.Add(entry.Output.Lemma);
            lemmas.AddRange(entry.Ingredients.Select(static i => i.Noun.Lemma));
            if (entry.Station is not null)
            {
                lemmas.Add(entry.Station.Lemma);
            }
        }

        return lemmas.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>The panel's rows for the 3D simulation's inventory and the stations in reach, in the book's order. A repair row (I07) is left out while nothing of its item is worn, so a row's <see cref="CraftingRow.Index"/> (its recipe) is not its place on the panel.</summary>
    public static IReadOnlyList<CraftingRow> Rows(VoxelSimulation simulation, RecipeBook book)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(book);
        var rows = new List<CraftingRow>();
        for (var i = 0; i < book.Entries.Count; i++)
        {
            var entry = book.Entries[i];
            if (entry.Rule.Repairs && !simulation.Wear.NeedsRepair(entry.Rule.Output))
            {
                continue; // I07: a repair row is on the panel only while something of its item is worn
            }

            var have = entry.Ingredients.Select(p => simulation.Inventory.Count(p.Item)).ToList();
            rows.Add(new CraftingRow(i, entry, have, simulation.CanCraft(i)));
        }

        return rows;
    }
}
