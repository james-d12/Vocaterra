namespace Vocaterra.Game.Core.Language.Book;

/// <summary>What a <see cref="BookLine"/> is (U07): the layer picks its size, ink and spacing from the kind and the line's <see cref="BookTone"/>.</summary>
public enum BookLineKind
{
    /// <summary>A row of the left list that can be selected: an item with its lead, text, aside, pips or lamps and buttons.</summary>
    Entry,

    /// <summary>A band in the page's accent that names a section, with its count at the right. One row, never selected; it follows a <see cref="Gap"/> (none at the page's top).</summary>
    Section,

    /// <summary>A quiet note in the meta ink. One row, never selected.</summary>
    Meta,

    /// <summary>An empty row between two sections.</summary>
    Gap,

    /// <summary>A row of up to <see cref="BookLine.TilesPerRow"/> grey icons (the nouns not met yet), never selected.</summary>
    Tiles,

    /// <summary>A line of an entry's block: drawn on the right page only, never in the left list (<see cref="BookPages.ListFor"/> drops it).</summary>
    Detail,
}
