using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Language.Matching;

/// <summary>
/// The notes of an answer match (M70, D-game-22 item 3): a verb of the line written without the accent of the answer's word at its place, where the two are
/// forms of one verb in another tense or person (<c>luz compro pescado</c> for the answer <c>luz compró pescado</c>; <c>compre dos panes</c> for <c>compré dos
/// panes</c>). The answer matcher ignores accents, so the line matched as it is; the note shows the learner the accented form.
/// </summary>
internal static class AccentNotes
{
    /// <summary>
    /// The notes of the line against the matched answer's phrase nearest it (<see cref="AnswerAlignment"/>), in token order: one for each word that differs from its
    /// aligned word only in accents, is a lexicon form as written, and has no reading of the tense, mood, person and number of a verb reading of the answer's word
    /// with the same lemma. None when nothing matched, and none at a word the analysis already noted or read otherwise.
    /// </summary>
    public static IReadOnlyList<AccentNote> FromAnswer(
        NormalizedText text,
        AnalysisResult analysis,
        MatchResult match,
        AnswerSet set,
        NlpLexicon lexicon,
        ILanguagePack language,
        NormalizationOptions options)
    {
        var phrase = AnswerAlignment.NearestPhrase(text, match, set, language, options);
        if (phrase is null || text.Tokens.Count == 0)
        {
            return [];
        }

        var folding = language.Folding;
        var aligned = AnswerAlignment.Align(text.Tokens, phrase.Tokens, folding);
        var notes = new List<AccentNote>();
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            var token = text.Tokens[i];
            if (aligned[i] < 0 || (analysis.Notes?.Any(n => n.Span == token.Span) ?? false))
            {
                continue;
            }

            var accented = phrase.Tokens[aligned[i]].Text;
            if (string.Equals(accented, token.Text, StringComparison.Ordinal)
                || !string.Equals(folding.LenientKey(accented), folding.LenientKey(token.Text), StringComparison.Ordinal))
            {
                continue;
            }

            var written = lexicon.Lookup(token.Text);
            var answerForm = lexicon.Lookup(accented);
            if (written.IsAccentInsensitive || written.Count == 0 || answerForm.IsAccentInsensitive || !IsReadAsAWord(analysis, i))
            {
                continue;
            }

            if (NoteFor(token, accented, written, answerForm) is { } note)
            {
                notes.Add(note);
            }
        }

        return notes;
    }

    /// <summary>The analysis's notes followed by the answer's, or null when there is none (what <c>PipelineResult.Notes</c> carries).</summary>
    public static IReadOnlyList<AccentNote>? Merge(IReadOnlyList<AccentNote>? analysis, IReadOnlyList<AccentNote> answer)
    {
        var all = (analysis ?? []).Concat(answer).OrderBy(static n => n.Span.Start).ToList();
        return all.Count == 0 ? null : all;
    }

    // The first verb reading of the answer's word whose lemma the written word shares and whose tense, mood, person and number no reading of the written word has.
    private static AccentNote? NoteFor(Token token, string accented, IReadOnlyList<LexicalFeatures> written, IReadOnlyList<LexicalFeatures> answerForm)
    {
        foreach (var reading in answerForm.Where(ReadingClasses.IsFiniteVerb))
        {
            var sameVerb = written.Where(w => ReadingClasses.IsFiniteVerb(w) && string.Equals(w.Lemma, reading.Lemma, StringComparison.Ordinal)).ToList();
            if (sameVerb.Count == 0
                || sameVerb.Exists(w => w.Tense == reading.Tense && w.Mood == reading.Mood && w.Person == reading.Person && w.Number == reading.Number))
            {
                continue;
            }

            var marks = sameVerb.Exists(w => w.Tense == reading.Tense && w.Mood == reading.Mood) ? AccentMark.Person : AccentMark.Tense;
            return new AccentNote(token.Span, token.Text, accented, marks, reading);
        }

        return null;
    }

    // A word that the analysis reports as written (or as one of its readings): not a name, not unknown.
    private static bool IsReadAsAWord(AnalysisResult analysis, int index)
    {
        return analysis.Words is not { } words || (index < words.Count && words[index].Role == WordRole.Word);
    }
}
