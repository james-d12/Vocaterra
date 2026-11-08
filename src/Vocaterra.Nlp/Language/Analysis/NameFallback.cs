using Vocaterra.Nlp.Core.Prompts;

namespace Vocaterra.Nlp.Language.Analysis;

/// <summary>
/// Whether the analysis reads a word the lexicon lacks as a name where no grammar binds it (M78). The pipeline asks for it at a prompt that takes a free line; the
/// content lint and the quizzes do not, so an example with a word nobody knows, or a misspelt answer, is still an unknown word.
/// </summary>
public enum NameFallback
{
    /// <summary>Only a word a grammar's <c>[WORD]</c> element binds is a name.</summary>
    None,

    /// <summary>Also a word after the words that introduce a name (<see cref="IAnalysisRules.IsNamePosition"/>): <c>soy pedro</c>, <c>hola pedro</c>.</summary>
    Introduced,

    /// <summary>Also the only word of the line when it is written with a capital: <c>Pedro</c>. A lone lower-case word stays unknown.</summary>
    IntroducedOrAlone,
}

/// <summary>The name reading a prompt asks for (M78): what the pipeline passes to <see cref="LinguisticAnalyzer"/>, for a caller that runs the stages by hand.</summary>
public static class NameFallbacks
{
    /// <summary>
    /// A prompt that takes a free line (it allows intents) reads a word nobody knows as a name where a name is said, or alone on the line; a prompt that only asks
    /// for an answer (a quiz) keeps it unknown, since a misspelt answer is the word it names (<c>AnswerCorrections</c>).
    /// </summary>
    public static NameFallback For(PromptSpec prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return prompt.AllowedIntentIds.Count == 0 ? NameFallback.None : NameFallback.IntroducedOrAlone;
    }
}
