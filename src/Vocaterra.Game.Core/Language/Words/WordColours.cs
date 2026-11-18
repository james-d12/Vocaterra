using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Language.Words;

/// <summary>
/// The grey set of the 3D world, from the learner's progress (D-game-6 as amended, the playtest A/B of <c>--greyscale</c>). A block kind is grey
/// while every catalogue noun that names it (<see cref="NounCatalog.NounsOfBlock"/>) is unknown (<see cref="WordRecord.Known"/>): one known
/// name is enough (the water block is named by <c>agua</c> and <c>río</c>, S12). A block no noun names is always in colour, and so are the
/// figures, the sky and the UI.
/// </summary>
public static class WordColours
{
    /// <summary>The grey set for the catalogue and the progress: one bit per block kind (<see cref="BlockKinds.Bit"/>).</summary>
    public static UInt128 GreyKinds(NounCatalog nouns, WordProgress words)
    {
        ArgumentNullException.ThrowIfNull(nouns);
        ArgumentNullException.ThrowIfNull(words);
        UInt128 grey = 0;
        foreach (var block in Enum.GetValues<BlockId>())
        {
            if (block == BlockId.Air)
            {
                continue;
            }

            var named = nouns.NounsOfBlock(block);
            if (named.Count > 0 && named.All(noun => !words.RecordOf(noun.Lemma).Known))
            {
                grey |= BlockKinds.Bit(block);
            }
        }

        return grey;
    }

    /// <summary>
    /// Counts the given nouns as known from the start (the test aid <c>--assume-known</c>): each keeps its other counts and gets full credit. A
    /// noun already known keeps its record. Nothing is said or toasted for these nouns, since they are known before the first frame.
    /// </summary>
    /// <param name="words">The progress to change; the other records are kept.</param>
    /// <param name="lemmas">Catalogue nouns, in any order.</param>
    /// <returns>The lemmas that are not catalogue nouns (they are skipped).</returns>
    public static IReadOnlyList<string> AssumeKnown(WordProgress words, IReadOnlyList<string> lemmas)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(lemmas);
        var skipped = new List<string>();
        var named = new Dictionary<string, WordRecord>(StringComparer.Ordinal);
        foreach (var lemma in lemmas)
        {
            if (words.Catalogue.ForLemma(lemma) is null)
            {
                skipped.Add(lemma);
                continue;
            }

            named[lemma] = words.RecordOf(lemma) with { CreditUnits = WordProgress.UnitsToKnow, CorrectTurns = WordProgress.UsesToKnow };
        }

        if (named.Count == 0)
        {
            return skipped;
        }

        var kept = words.Records.Values.Where(record => !named.ContainsKey(record.Lemma)).ToList();
        kept.AddRange(named.Values);
        words.Restore(kept);
        return skipped;
    }
}
