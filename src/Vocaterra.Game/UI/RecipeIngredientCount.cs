using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.UI;

/// <summary>One ingredient of the workbench's recipe panel (S15): its icon's item and how many the player has of the number the recipe needs.</summary>
/// <param name="Item">The ingredient.</param>
/// <param name="Have">How many the player carries.</param>
/// <param name="Need">How many one craft takes.</param>
public readonly record struct RecipeIngredientCount(ItemId Item, int Have, int Need)
{
    /// <summary>Whether the player has enough of it.</summary>
    public bool Enough => Have >= Need;

    /// <summary>The count as the panel writes it under the icon: <c>have/need</c>.</summary>
    public string Counts => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Have}/{Need}");
}
