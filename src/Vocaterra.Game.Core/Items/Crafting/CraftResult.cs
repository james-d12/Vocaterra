namespace Vocaterra.Game.Core.Items.Crafting;

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

    /// <summary>A repair row, and the player has no worn stack of that item in the bag or on the body (I07).</summary>
    NothingToRepair = 5,
}
