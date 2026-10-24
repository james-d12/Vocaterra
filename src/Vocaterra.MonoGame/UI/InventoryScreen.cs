using Vocaterra.Game;

namespace Vocaterra.MonoGame.UI;

/// <summary>
/// The inventory screen (I or E): open or closed, and the words it shows. Opening it counts each shown item's word as read once (the journal's
/// rule: seeing a label is a "read", never a spoken credit), so the colour of the words does not change by merely looking at them again.
/// </summary>
public sealed class InventoryScreen
{
    /// <summary>Whether the inventory screen is shown. While it is, the player does not walk and the mouse does not mine or place.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Opens the screen and marks the given catalogue words as read.</summary>
    /// <param name="words">The learner's word progress.</param>
    /// <param name="lemmas">The lemmas of the nouns of the items in the inventory (any other lemma is ignored by the progress).</param>
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

    /// <summary>Closes the screen.</summary>
    public void Close() => IsOpen = false;
}
