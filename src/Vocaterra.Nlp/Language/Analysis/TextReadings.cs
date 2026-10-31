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
    public TextReadings(NormalizedText text, IReadOnlyList<AnalyzedToken> tokens, IReadOnlyList<ErrorTag?> irregular, IReadOnlyList<IReadOnlyList<LexicalFeatures>> accentVariants)
    {
        Text = text;
        Tokens = tokens;
        Irregular = irregular;
        AccentVariants = accentVariants;
    }

    /// <summary>The text that was read.</summary>
    public NormalizedText Text { get; }

    /// <summary>One token per token of the text, with every reading of the lexicon (a name keeps them here; the completion drops them).</summary>
    public IReadOnlyList<AnalyzedToken> Tokens { get; }

    /// <summary>For each token, the <c>conj.irregular</c> error its irregular verb form stands for, or null.</summary>
    public IReadOnlyList<ErrorTag?> Irregular { get; }

    /// <summary>For each token, the readings of the accented spellings of a plain lexicon form, or none (only the reading resolver reads them).</summary>
    public IReadOnlyList<IReadOnlyList<LexicalFeatures>> AccentVariants { get; }
}
