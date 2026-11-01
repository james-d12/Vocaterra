namespace Vocaterra.Nlp.Language.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

/// <summary>
/// The first half of <see cref="LinguisticAnalyzer.Analyze(NormalizedText, IReadOnlyList{Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar})"/>: every lexicon reading of every token, which
/// depends on the text alone, before any grammar is matched (<see cref="LinguisticAnalyzer.Read"/>). It is never changed after it is made, so one text's readings can be
/// completed against any number of grammar matches (<see cref="LinguisticAnalyzer.Complete"/>); the content lint does that for each example.
/// </summary>
internal sealed class TextReadings
{
    public TextReadings(NormalizedText text, IReadOnlyList<AnalyzedToken> tokens, IReadOnlyList<WordSlip?> slips, IReadOnlyList<IReadOnlyList<LexicalFeatures>> accentVariants)
    {
        Text = text;
        Tokens = tokens;
        Slips = slips;
        AccentVariants = accentVariants;
        var withSlips = new AnalyzedToken[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            withSlips[i] = slips[i] is { IsAlternative: true } slip ? new AnalyzedToken(tokens[i].Token, [.. tokens[i].Readings, .. slip.Readings]) : tokens[i];
            HasAlternativeSlips |= slips[i] is { IsAlternative: true };
        }

        TokensWithSlips = withSlips;
    }

    /// <summary>The text that was read.</summary>
    public NormalizedText Text { get; }

    /// <summary>
    /// One token per token of the text, with every reading of the lexicon (a name keeps them here; the completion drops them), or, for a word the lexicon lacks that is
    /// a learner's slip (<c>costan</c>, <c>pans</c>), the readings of the slip.
    /// </summary>
    public IReadOnlyList<AnalyzedToken> Tokens { get; }

    /// <summary>For each token, the learner's slip it may stand for (M58; before M58 only the <c>conj.irregular</c> of a regularized verb form the lexicon lacks), or null.</summary>
    public IReadOnlyList<WordSlip?> Slips { get; }

    /// <summary>The tokens with the readings of the alternative slips added after the lexicon's (<c>costa</c>: the noun, then <c>costar</c>); <see cref="Tokens"/> when there are none.</summary>
    public IReadOnlyList<AnalyzedToken> TokensWithSlips { get; }

    /// <summary>True when a token has an alternative slip, a reading besides those of the lexicon.</summary>
    public bool HasAlternativeSlips { get; }

    /// <summary>For each token, the readings of the accented spellings of a plain lexicon form, or none (only the reading resolver reads them).</summary>
    public IReadOnlyList<IReadOnlyList<LexicalFeatures>> AccentVariants { get; }
}

/// <summary>How a learner's slip relates to the word's own readings (M58).</summary>
internal enum WordSlipKind
{
    /// <summary>The lexicon lacks the word, so the slip is its reading (<c>costan</c> for <c>cuestan</c>, <c>pans</c> for <c>panes</c>); its error is always reported.</summary>
    Only,

    /// <summary>
    /// Every lexicon reading of the word is a noun (<c>costa</c>); the slip is a second reading, which the reading resolver keeps where the sentence calls for it, and its
    /// error is reported only then.
    /// </summary>
    BesideNoun,

    /// <summary>
    /// The word is a lexicon word of another class (<c>podo</c>, a form of <c>podar</c>; <c>solo</c>); the slip is a reading only for a grammar that matches nothing
    /// without it, and its error is reported only where that grammar binds the word to it (<c>me [VERB:poder] [VERB:ayudar]</c> over <c>me pode ayudar</c>).
    /// </summary>
    ByGrammarOnly,
}

/// <summary>
/// A learner's slip a word may stand for (M58): the readings of the word the learner meant (<c>costar</c>, third person singular, for <c>costa</c>; the plural noun
/// <c>pan</c> for <c>pans</c>) and the error that reports it (<c>conj.irregular</c> expected <c>cuesta</c>; <c>noun.number</c> expected <c>panes</c>).
/// </summary>
/// <param name="Readings">The readings of the word the learner meant.</param>
/// <param name="Error">The error the slip is reported with, when the analysis reads the word as the slip.</param>
/// <param name="Kind">How the slip relates to the word's own readings.</param>
internal sealed record WordSlip(IReadOnlyList<LexicalFeatures> Readings, ErrorTag Error, WordSlipKind Kind)
{
    /// <summary>True when the slip is a reading besides those of the lexicon, not the word's only one.</summary>
    public bool IsAlternative => Kind != WordSlipKind.Only;

    /// <summary>True when the readings left to a word are the slip's and nothing else, so the analysis read the word as the slip.</summary>
    /// <param name="resolved">The readings the reading resolver left to the word.</param>
    public bool IsChosen(IReadOnlyList<LexicalFeatures> resolved)
    {
        return resolved.Count > 0 && resolved.All(Readings.Contains);
    }
}
