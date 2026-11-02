namespace Vocaterra.Nlp.Es.Analysis;

using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>The rule of M70: <c>gustar</c> and <c>encantar</c> agree with the thing liked, and the one who likes is a dative pronoun (<c>conj.person</c>).</summary>
internal sealed partial class SpanishAgreementChecker
{
    // Rule M70 (QA-language-4), "gustar agrees with the thing liked". The learner who says I like the bread makes the verb agree with the one who likes. Two shapes,
    // both conj.person, because the verb's person is the mistake: it agrees with the wrong participant, and the expected form is the one of the thing liked.
    //  1. A form of the first or second person right behind its dative pronoun, with a noun phrase after it that would be its subject and no subject before it
    //     (me gusto el pan -> gusta, me gusto las manzanas -> gustan, te encantas la fruta -> encanta); the third person is M56's rule (me gusta las manzanas -> gustan).
    //  2. A subject pronoun before the verb with no dative pronoun at all, and a noun phrase after it (yo gusto el pan -> me gusta, tú gustas las manzanas -> te
    //     gustan, ella gusta el pan -> le gusta): the error spans the pronoun and the verb, and the expected text is the dative of the pronoun's person with the form
    //     of the phrase, so the recast says me gusta el pan; an adverb between them stays before the dative (yo no gusto el pan -> no me gusta). It replaces the
    //     person error that the pronoun's own check gives the verb (tú gusto el pan: te gusta, not gustas).
    // Clean: a subject after the verb that agrees with it (me gustas tú, te gusto yo), no phrase after the verb (me gusto, te gusto, ¿te gusta?, no me gusta), a dative
    // with a subject pronoun (tú me gustas, yo te gusto, a mí me gusta el pan), a phrase after a preposition (gusta de la música), and the other experiencer verbs,
    // which take a subject of the first or second person or an object in other senses (me quedo el libro, me preocupo).
    private void CheckLikingVerb()
    {
        for (var v = 0; v < _tokens.Count; v++)
        {
            var readings = _tokens[v].Readings;
            if (readings.Count == 0 || !readings.All(IsFiniteVerb))
            {
                continue;
            }

            var verb = FirstReading(v, static r => SpanishAnalysisTables.IsLikingVerb(r.Lemma) && r.Mood == LexiconSchema.Indicative && r.Tense is not null);
            var start = NextContentIndex(v + 1);
            if (verb is null || start < 0 || !TryReadNounPhrase(start, out var phrase) || SubjectNumber(phrase, timeIsSubject: true) is not { } number)
            {
                continue;
            }

            var expected = _morphology.Conjugate(verb.Lemma, "3", number, verb.Tense!);
            if (string.IsNullOrEmpty(expected))
            {
                continue;
            }

            if (HasDativePronounBefore(v))
            {
                if (verb.Person is "1" or "2" && !HasPreverbalSubject(v))
                {
                    Add(ErrorTags.ConjPerson, v, v, expected, TextOf(v));
                }

                continue;
            }

            var pronoun = SubjectPronounBefore(v);
            if (pronoun < 0 || FirstReading(pronoun, static r => r.Pos == LexiconSchema.Pronoun && WordClasses.IsSubjectPronoun(r.Lemma)) is not { } subject
                || SpanishAnalysisTables.DativeOf(subject.Lemma) is not { } dative)
            {
                continue;
            }

            var between = Enumerable.Range(pronoun + 1, v - pronoun - 1).Select(TextOf);
            var verbSpan = SpanOf(v, v);
            _errors.RemoveAll(e => e.Tag == ErrorTags.ConjPerson && e.Span == verbSpan);
            Add(
                ErrorTags.ConjPerson,
                pronoun,
                v,
                string.Join(' ', between.Append(dative).Append(expected)),
                string.Join(' ', Enumerable.Range(pronoun, v - pronoun + 1).Select(TextOf)));
        }
    }

    // The subject pronoun before the verb at index, with only adverbs between (yo gusto, yo no gusto), that no preposition governs; -1 when there is none.
    private int SubjectPronounBefore(int verbIndex)
    {
        var k = verbIndex - 1;
        while (k >= 0 && verbIndex - k <= ReadingClasses.MaxSkippedBetween && _tokens[k].Readings.Count > 0
            && _tokens[k].Readings.All(static r => r.Pos == LexiconSchema.Adverb))
        {
            k--;
        }

        return k >= 0 && IsSubjectPronounAt(k) ? k : -1;
    }
}
