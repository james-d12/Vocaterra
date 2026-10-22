using Vocaterra.Game;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Frontend;

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
    public string Status => Result switch
    {
        CraftResult.Done => "Make",
        CraftResult.MissingStation => "Needs " + (Recipe.Station?.Lemma ?? "estación"),
        CraftResult.InventoryFull => "Inventory full",
        _ => "Missing material",
    };
}

/// <summary>
/// Where the crafting panel's pieces are for a viewport, in pixels, so that drawing and mouse hits use the same numbers (and a test can click
/// a row without a window). The panel is centred; rows are stacked under a header.
/// </summary>
public readonly record struct CraftingLayout
{
    /// <summary>The height of the header (title and rule) in pixels.</summary>
    public const int HeaderHeight = 44;

    /// <summary>The height of the footer (key hints) in pixels.</summary>
    public const int FooterHeight = 32;

    /// <summary>The widest panel, in pixels.</summary>
    public const int MaxWidth = 800;

    /// <summary>The tallest row, in pixels.</summary>
    public const int MaxRowHeight = 46;

    /// <summary>The space between rows, in pixels.</summary>
    public const int RowGap = 4;

    /// <summary>The padding inside the panel, in pixels.</summary>
    public const int Padding = 16;

    /// <summary>Computes the layout of a panel with <paramref name="rowCount"/> rows in a viewport.</summary>
    public CraftingLayout(int viewportWidth, int viewportHeight, int rowCount)
    {
        RowCount = Math.Max(rowCount, 0);
        Width = Math.Min(MaxWidth, Math.Max(viewportWidth - 32, 200));
        var room = Math.Max(viewportHeight - 16 - HeaderHeight - FooterHeight - (Padding * 2), 40);
        var perRow = RowCount == 0 ? MaxRowHeight : (room / RowCount) - RowGap;
        RowHeight = Math.Clamp(perRow, 24, MaxRowHeight);
        Height = (Padding * 2) + HeaderHeight + FooterHeight + (RowCount * (RowHeight + RowGap));
        X = (viewportWidth - Width) / 2;
        Y = Math.Max(8, (viewportHeight - Height) / 2);
    }

    /// <summary>The number of rows.</summary>
    public int RowCount { get; }

    /// <summary>The panel's left edge.</summary>
    public int X { get; }

    /// <summary>The panel's top edge.</summary>
    public int Y { get; }

    /// <summary>The panel's width.</summary>
    public int Width { get; }

    /// <summary>The panel's height.</summary>
    public int Height { get; }

    /// <summary>The height of one row.</summary>
    public int RowHeight { get; }

    /// <summary>The left edge of the rows.</summary>
    public int RowX => X + Padding;

    /// <summary>The width of the rows.</summary>
    public int RowWidth => Width - (Padding * 2);

    /// <summary>The top edge of the row at <paramref name="index"/>.</summary>
    public int RowY(int index) => Y + Padding + HeaderHeight + (index * (RowHeight + RowGap));

    /// <summary>The row under a point, or <see langword="null"/> when the point is on no row.</summary>
    public int? RowAt(int x, int y)
    {
        if (x < RowX || x >= RowX + RowWidth)
        {
            return null;
        }

        for (var i = 0; i < RowCount; i++)
        {
            var top = RowY(i);
            if (y >= top && y < top + RowHeight)
            {
                return i;
            }
        }

        return null;
    }
}

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

    /// <summary>The panel's rows for the 3D simulation's inventory and the stations in reach, in the book's order.</summary>
    public static IReadOnlyList<CraftingRow> Rows(VoxelSimulation simulation, RecipeBook book)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(book);
        var rows = new List<CraftingRow>();
        for (var i = 0; i < book.Entries.Count; i++)
        {
            var entry = book.Entries[i];
            var have = entry.Ingredients.Select(p => simulation.Inventory.Count(p.Item)).ToList();
            rows.Add(new CraftingRow(i, entry, have, simulation.CanCraft(i)));
        }

        return rows;
    }
}
