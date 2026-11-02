using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Language.Matching;

/// <summary>
/// Lines a learner's words up with the words of the answer phrase they matched (M70), so that a word of the line is compared with the word of the answer at its
/// place: the unknown word of a near miss takes that word (<see cref="AnswerCorrections"/>; <c>compré three manzanas</c> gives <c>tres</c>, not <c>compré</c>), and
/// a verb written without the accent of the answer's form gets a note (<see cref="AccentNotes"/>).
/// </summary>
/// <remarks>
/// The phrase is the one of the matched answer (its canonical phrase or an alias) nearest the line by the accent-insensitive Damerau-Levenshtein distance of the
/// whole texts, the canonical phrase and then the aliases in their order winning a tie. The words are aligned by a word-level edit distance: a word kept costs
/// nothing, a word left out or added costs one, and a word changed costs its accent-insensitive character distance over the longer word's length, times one and a
/// half, so a misspelt word pairs with its own (<c>ezpada</c>, <c>espada</c>), an unknown word pairs with the word at its place when the words around it are kept
/// (<c>three</c>, <c>tres</c>), and two unrelated words do not pair where a shift keeps the others in place (<c>glorp compré manzanas</c>: <c>glorp</c> is an
/// added word). On a tie a pair wins. The arithmetic is in integers, so the result is the same on every machine.
/// </remarks>
internal static class AnswerAlignment
{
    // The cost of a word left out or added.
    private const int GapCost = 1000;

    // The most a changed word costs: one and a half gaps, so that two unrelated words pair only where that keeps a word on each side in its place (compré three
    // manzanas and compré tres manzanas: three and tres pair), not where a shift would line a kept word up with its own (glorp compré manzanas: compré pairs compré).
    private const int MaxChangeCost = 1500;

    /// <summary>The phrase of the matched answer nearest the text, normalized as the text is, or null when nothing matched or the answer is not in the set.</summary>
    public static NormalizedText? NearestPhrase(NormalizedText text, MatchResult match, AnswerSet set, ILanguagePack language, NormalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(language);
        if (match.Type == MatchType.None || match.AnswerId is null)
        {
            return null;
        }

        var answer = set.Answers.FirstOrDefault(a => string.Equals(a.AnswerId, match.AnswerId, StringComparison.Ordinal));
        if (answer is null)
        {
            return null;
        }

        var input = language.Folding.MatchingKey(text.Text);
        NormalizedText? nearest = null;
        var nearestDistance = int.MaxValue;
        foreach (var raw in answer.Aliases.Prepend(answer.Canonical))
        {
            var phrase = language.Normalizer.Normalize(raw, options);
            if (phrase.Tokens.Count == 0)
            {
                continue;
            }

            var distance = DamerauLevenshtein.Distance(input, language.Folding.MatchingKey(phrase.Text));
            if (distance < nearestDistance)
            {
                nearest = phrase;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>For each word of the line, the index of the phrase word aligned with it, or -1 for a word the phrase does not have.</summary>
    public static int[] Align(IReadOnlyList<Token> line, IReadOnlyList<Token> phrase, ILexiconFolding folding)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(phrase);
        ArgumentNullException.ThrowIfNull(folding);
        var n = line.Count;
        var m = phrase.Count;
        var lineKeys = line.Select(t => folding.MatchingKey(t.Text)).ToArray();
        var phraseKeys = phrase.Select(t => folding.MatchingKey(t.Text)).ToArray();

        // cost[i, j]: the cheapest alignment of the first i words of the line with the first j words of the phrase.
        var cost = new int[n + 1, m + 1];
        for (var i = 1; i <= n; i++)
        {
            cost[i, 0] = i * GapCost;
        }

        for (var j = 1; j <= m; j++)
        {
            cost[0, j] = j * GapCost;
        }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var paired = cost[i - 1, j - 1] + ChangeCost(lineKeys[i - 1], phraseKeys[j - 1]);
                var dropped = cost[i - 1, j] + GapCost;
                var added = cost[i, j - 1] + GapCost;
                cost[i, j] = Math.Min(paired, Math.Min(dropped, added));
            }
        }

        // Back from the end; on a tie a pair wins over a word of the line left unpaired, which wins over a word of the phrase left out, so the words pair as late
        // as the trace allows from the end, which is as early as possible from the start.
        var aligned = Enumerable.Repeat(-1, n).ToArray();
        var li = n;
        var pj = m;
        while (li > 0 && pj > 0)
        {
            if (cost[li, pj] == cost[li - 1, pj - 1] + ChangeCost(lineKeys[li - 1], phraseKeys[pj - 1]))
            {
                aligned[li - 1] = pj - 1;
                li--;
                pj--;
            }
            else if (cost[li, pj] == cost[li - 1, pj] + GapCost)
            {
                li--;
            }
            else
            {
                pj--;
            }
        }

        return aligned;
    }

    // A word kept costs nothing; a word changed costs its character distance over the longer word's length, scaled to MaxChangeCost.
    private static int ChangeCost(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 0;
        }

        var longer = Math.Max(left.Length, right.Length);
        return longer == 0 ? 0 : Math.Min(MaxChangeCost, (DamerauLevenshtein.Distance(left, right) * MaxChangeCost) / longer);
    }
}
