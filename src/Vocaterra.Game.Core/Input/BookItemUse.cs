using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Input;

/// <summary>Using the book item (S20): selected and right-clicked with the mouse captured, it opens the book as J does. The item is not placeable, so the press places nothing.</summary>
public static class BookItemUse
{
    /// <summary>Whether this frame's right click uses the book item.</summary>
    /// <param name="input">The frame's input.</param>
    /// <param name="captured">Whether the mouse is captured (the world has the input).</param>
    /// <param name="bookOpen">Whether the book is already open.</param>
    /// <param name="selected">The hotbar's selected stack.</param>
    public static bool Applies(IInputFrame input, bool captured, bool bookOpen, ItemStack selected) =>
        captured && input.RightClicked && !bookOpen && selected is { Item: ItemId.Book, Count: > 0 };
}
