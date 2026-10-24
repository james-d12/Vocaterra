using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Response.Templates;

/// <summary>Chooses one variant of a pool with a seeded generator, so the same seed always yields the same line.</summary>
public static class DialogueSelector
{
    /// <summary>Picks the variant at index <c>rng.NextInt(count)</c> of the pool for the dialogue intent; throws <see cref="KeyNotFoundException"/> for an unknown intent.</summary>
    public static DialogueTemplate Select(DialogueCatalog catalog, string dialogueIntent, SeededRng rng)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dialogueIntent);
        if (!catalog.TryGetPool(dialogueIntent, out var pool))
        {
            throw new KeyNotFoundException($"No dialogue pool for intent '{dialogueIntent}'.");
        }

        return Select(pool, rng);
    }

    /// <summary>Picks the variant at index <c>rng.NextInt(count)</c>; always consumes exactly one draw.</summary>
    public static DialogueTemplate Select(VariantPool pool, SeededRng rng)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(rng);
        return pool.Variants[rng.NextInt(pool.Variants.Count)];
    }
}
