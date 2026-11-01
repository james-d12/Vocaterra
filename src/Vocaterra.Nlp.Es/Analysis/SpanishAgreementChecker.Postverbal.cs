namespace Vocaterra.Nlp.Es.Analysis;

using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>The rules of M56: a subject that follows its verb (<c>conj.person</c>) and <c>ser</c> where a place takes <c>estar</c> (<c>ser_estar</c>).</summary>
internal sealed partial class SpanishAgreementChecker
{
    // Rule M56 (b), "a subject after its verb agrees with it". A noun phrase after the verb is its subject, and not its object, in three places: after estar, which
    // takes no object (¿dónde están las casas?, aquí está el pan); after an experiencer verb with its dative pronoun (me gustan las manzanas, me duele la cabeza, and
    // quedar after dónde: ¿dónde queda la herrería?); and after a verb of price in the price question (¿cuánto cuesta el pan?). A third-person verb whose number differs
    // from the phrase's gives conj.person with the form of that number (¿dónde están la herrería? -> está, me gusta las manzanas -> gustan). Left clean: a clause
    // that has its subject before the verb (a subject pronoun, a noun or a name that no preposition governs), a phrase that disagrees with itself (los manzana is
    // noun.number's), a phrase coordinated with y, o or ni (me gusta el pan y la fruta may agree with the first), a noun of part or quantity before de (la mayoría de),
    // a noun of time after estar or a verb of price (están el lunes en casa), a verb of the first or second person, the subjunctive, haber (hay dos panes) and a verb
    // of price outside the question (cuestan dos monedas: the phrase is the price).
    private void CheckPostverbalSubject()
    {
        for (var v = 0; v < _tokens.Count; v++)
        {
            var readings = _tokens[v].Readings;
            if (readings.Count == 0 || !readings.All(IsFiniteVerb))
            {
                continue;
            }

            var start = NextContentIndex(v + 1);
            var neuter = start >= 0 && IsLoneNeuterPronoun(start);
            var verb = FirstReading(v, r => r.Mood == LexiconSchema.Indicative && r.Person == "3" && r.Tense is not null
                && (TakesPostverbalSubject(v, r.Lemma) || (neuter && r.Lemma == "ser" && FollowsPriceQuestionWord(v))));
            if (verb is null || HasPreverbalSubject(v))
            {
                continue;
            }

            string? number;
            if (neuter)
            {
                // M66: a neuter pronoun is singular (¿cuánto cuestan todo? -> cuesta, ¿cuánto son todo? -> es; ¿cuánto cuesta eso? is clean).
                number = LexiconSchema.Singular;
            }
            else if (start < 0 || !TryReadNounPhrase(start, out var phrase))
            {
                continue;
            }
            else
            {
                var experiencer = SpanishAnalysisTables.IsExperiencerVerb(verb.Lemma) && HasDativePronounBefore(v);
                number = SubjectNumber(phrase, timeIsSubject: experiencer);
            }

            if (number is null || readings.Any(r => r.Lemma == verb.Lemma && r.Person == "3" && r.Number == number && r.Tense == verb.Tense))
            {
                continue;
            }

            var expected = _morphology.Conjugate(verb.Lemma, "3", number, verb.Tense!);
            if (string.IsNullOrEmpty(expected) || string.Equals(expected, TextOf(v), StringComparison.Ordinal))
            {
                continue;
            }

            Add(ErrorTags.ConjPerson, v, v, expected, TextOf(v));
        }
    }

    // Rule M56 (a), "where a thing is takes estar". Two places where ser is a mistake for estar, each giving ser_estar with the form of estar:
    //  1. ser with lejos or cerca, after at most two adverbs (es lejos, es muy cerca -> está; eran lejos -> estaban). Left clean: an event as the subject, before the
    //     verb or after the adverb in its clause (la fiesta es cerca, ¿es lejos la fiesta?: an event's place takes ser), cerca de with an hour or a number (son cerca de
    //     las tres), a preposition between (es de lejos), and a form that is also ir (fue lejos: he went far).
    //  2. dónde with a third-person ser and a noun phrase with a definite determiner (¿dónde es la herrería? -> está; ¿dónde son las casas? -> están; the form agrees with
    //     the phrase). Left clean: an event (¿dónde es la fiesta?, ¿dónde es la clase?), dónde after a preposition (¿de dónde es Luz?, origin), a bare or indefinite
    //     phrase and one with an adjective before its noun (¿dónde es profesor?, ¿dónde es el mejor sitio?: a predicate), and ¿dónde es? alone, whose subject may be an event.
    private void CheckSerOfPlace()
    {
        for (var i = 0; i < _tokens.Count; i++)
        {
            var finite = Readings(i, IsFiniteVerb);
            if (finite.Count == 0 || finite.Exists(static r => r.Lemma != "ser"))
            {
                continue;
            }

            // A form of two persons (era: the first and the third) is taken as the third, the person of a place or a thing.
            var copula = finite.Find(static r => r.Mood == LexiconSchema.Indicative && r.Tense is not null && r.Person == "3")
                ?? finite.Find(static r => r.Mood == LexiconSchema.Indicative && r.Tense is not null);
            if (copula is null || _errors.Exists(e => e.Tag == ErrorTags.SerEstar && e.Span == SpanOf(i, i)))
            {
                continue;
            }

            var adverb = LocationAdverbAfter(i);
            string? number = null;
            if (adverb >= 0)
            {
                if (HasEventSubject(i, adverb) || IsApproximateTime(adverb))
                {
                    continue;
                }

                number = copula.Number;
            }
            else if (copula.Person == "3" && i > 0 && SpanishAnalysisTables.IsLocationQuestionWord(TextOf(i - 1)) && !(i > 1 && IsPreposition(i - 2)))
            {
                var start = NextContentIndex(i + 1);
                if (start < 0
                    || !TryReadNounPhrase(start, out var phrase)
                    || phrase.Before.Count > 0
                    || !phrase.Determiners.Exists(IsDefiniteDeterminer)
                    || Readings(phrase.Head, static r => r.Pos == LexiconSchema.Noun).Exists(static n => SpanishAnalysisTables.IsEventNoun(n.Lemma)))
                {
                    continue;
                }

                number = SubjectNumber(phrase, timeIsSubject: true) ?? copula.Number;
            }

            if (number is null)
            {
                continue;
            }

            var expected = _morphology.Conjugate("estar", copula.Person!, number, copula.Tense!);
            if (!string.IsNullOrEmpty(expected))
            {
                Add(ErrorTags.SerEstar, i, i, expected, TextOf(i));
            }
        }
    }

    // The verbs whose noun phrase after them is the subject at this place: estar anywhere, an experiencer verb after its dative pronoun, quedar after dónde and a verb of
    // price after cuánto (with a dative pronoun between or not).
    private bool TakesPostverbalSubject(int verbIndex, string lemma)
    {
        if (lemma == "estar")
        {
            return true;
        }

        if (SpanishAnalysisTables.IsExperiencerVerb(lemma) && HasDativePronounBefore(verbIndex))
        {
            return true;
        }

        if (lemma == "quedar" && verbIndex > 0 && SpanishAnalysisTables.IsLocationQuestionWord(TextOf(verbIndex - 1)))
        {
            return true;
        }

        return SpanishAnalysisTables.IsPriceVerb(lemma) && FollowsPriceQuestionWord(verbIndex);
    }

    // The verb at index follows cuánto, with a dative pronoun between or not (¿cuánto cuesta?, ¿cuánto te cuesta?, ¿cuánto es?).
    private bool FollowsPriceQuestionWord(int verbIndex)
    {
        var before = HasDativePronounBefore(verbIndex) ? verbIndex - 2 : verbIndex - 1;
        return before >= 0 && TextOf(before) is "cuánto" or "cuanto";
    }

    // M66: a neuter pronoun (todo, eso, esto, aquello) that stands alone after the verb: no determiner, noun or adjective after it that it would be the determiner of
    // (todo el pan, todo junto is left to the phrase), and no y, o or ni joining it to another (eso y esto).
    private bool IsLoneNeuterPronoun(int index)
    {
        if (!SpanishAnalysisTables.IsNeuterPronoun(TextOf(index)))
        {
            return false;
        }

        var next = index + 1;
        return next >= _tokens.Count
            || !(IsDeterminer(next)
                || HasReading(next, static r => r.Pos is LexiconSchema.Noun or LexiconSchema.Adjective)
                || TextOf(next) is "y" or "e" or "o" or "u" or "ni");
    }

    private bool HasDativePronounBefore(int verbIndex)
    {
        return verbIndex > 0
            && SpanishAnalysisTables.IsDativePronoun(TextOf(verbIndex - 1))
            && FirstReading(verbIndex - 1, static r => r.Pos == LexiconSchema.Pronoun) is not null;
    }

    // True when the clause of the verb has a subject before it: a subject pronoun, a noun or a word without readings (a name) that no preposition governs. The clause
    // starts after the nearest conjunction or question word (que, y, dónde, cuánto), so "creo que me gusta el pan" is checked after que.
    private bool HasPreverbalSubject(int verbIndex)
    {
        for (var j = verbIndex - 1; j >= 0; j--)
        {
            var readings = _tokens[j].Readings;
            if (readings.Any(static r => r.Pos == LexiconSchema.Conjunction) || SpanishAnalysisTables.IsClauseOpener(TextOf(j)))
            {
                // M66 (M63): a subject pronoun right before the question word is the subject of the question's verb (ellas dónde están, ellas adónde van), which
                // CheckSubjectVerb agrees with it; the phrase after the verb is then no subject.
                return SpanishAnalysisTables.IsQuestionWordAfterSubject(TextOf(j)) && j > 0 && IsSubjectPronounAt(j - 1);
            }

            if (readings.Count == 0 || readings.Any(static r => r.Pos == LexiconSchema.Pronoun && WordClasses.IsSubjectPronoun(r.Lemma)))
            {
                if (!(j > 0 && IsPreposition(j - 1)))
                {
                    return true;
                }

                continue;
            }

            if (readings.Any(static r => r.Pos == LexiconSchema.Noun) && !IsInPrepositionalPhrase(j))
            {
                return true;
            }
        }

        return false;
    }

    // A subject pronoun at index that no preposition governs.
    private bool IsSubjectPronounAt(int index)
    {
        return HasReading(index, static r => r.Pos == LexiconSchema.Pronoun && r.Person is not null && WordClasses.IsSubjectPronoun(r.Lemma))
            && !(index > 0 && IsPreposition(index - 1));
    }

    // A noun inside a phrase that a preposition opens (a mi hermano, de la plaza): the words before it back to the preposition are determiners and adjectives.
    private bool IsInPrepositionalPhrase(int index)
    {
        var k = index - 1;
        while (k >= 0 && (IsDeterminer(k) || IsAdjunct(k)))
        {
            k--;
        }

        return k >= 0 && IsPreposition(k);
    }

    private bool IsPreposition(int index)
    {
        return _tokens[index].Readings.Any(static r => r.Pos == LexiconSchema.Preposition);
    }

    private bool IsDefiniteDeterminer(int index)
    {
        return _tokens[index].Readings.Any(static r => (r.Pos == LexiconSchema.Article && r.Lemma == "el")
            || (r.Pos == LexiconSchema.Adjective && WordClasses.IsDefiniteDeterminerAdjective(r.Lemma)));
    }

    // The number of a noun phrase as the subject of a verb, or null when it cannot be told: the noun's own number, or its determiners' for a noun of both numbers. A
    // phrase whose determiners disagree with its noun, one coordinated with another (y, o, ni) and a noun of part or quantity before de give null; so does a noun of
    // time, unless the verb is an experiencer verb (me gusta la tarde is a subject, están la tarde en casa is not).
    private string? SubjectNumber(NounPhrase phrase, bool timeIsSubject)
    {
        var nouns = Readings(phrase.Head, static r => r.Pos == LexiconSchema.Noun);
        var next = phrase.Last + 1;
        var nextText = next < _tokens.Count ? TextOf(next) : string.Empty;
        if (nextText is "y" or "e" or "o" or "u" or "ni"
            || (!timeIsSubject && nouns.Exists(static n => SpanishAnalysisTables.IsDurationNoun(n.Lemma)))
            || (nextText == "de" && nouns.Exists(static n => SpanishAnalysisTables.IsQuantityNoun(n.Lemma))))
        {
            return null;
        }

        var numbers = nouns.Select(static n => n.Number).Where(static n => n is not null).Distinct(StringComparer.Ordinal).ToList();
        var number = numbers.Count == 1 ? numbers[0] : null;
        foreach (var determiner in phrase.Determiners)
        {
            var own = Readings(determiner, IsDeterminerReading).Select(static r => r.Number).Where(static n => n is not null).Distinct(StringComparer.Ordinal).ToList();
            if (own.Count == 0)
            {
                continue;
            }

            if (number is null)
            {
                number = own.Count == 1 ? own[0] : null;
            }
            else if (!own.Contains(number))
            {
                return null;
            }
        }

        return number;
    }

    // The index of lejos or cerca after a copula, past at most two adverbs (muy, tan), or -1.
    private int LocationAdverbAfter(int copulaIndex)
    {
        for (var j = copulaIndex + 1; j < _tokens.Count && j - copulaIndex <= ReadingClasses.MaxSkippedBetween + 1; j++)
        {
            if (FirstReading(j, static r => r.Pos == LexiconSchema.Adverb && SpanishAnalysisTables.IsLocationAdverb(r.Lemma)) is not null)
            {
                return j;
            }

            if (!IsSkippable(j))
            {
                return -1;
            }
        }

        return -1;
    }

    // An event as the subject of the copula: a noun of an event in its clause before it, or after the adverb, that no preposition governs (la fiesta es cerca, ¿es lejos
    // la fiesta?, but not es cerca de la fiesta).
    private bool HasEventSubject(int copulaIndex, int adverbIndex)
    {
        for (var j = copulaIndex - 1; j >= 0; j--)
        {
            if (_tokens[j].Readings.Any(static r => r.Pos == LexiconSchema.Conjunction) || SpanishAnalysisTables.IsClauseOpener(TextOf(j)))
            {
                break;
            }

            if (IsEventNounAt(j))
            {
                return true;
            }
        }

        for (var j = adverbIndex + 1; j < _tokens.Count; j++)
        {
            if (_tokens[j].Readings.Any(static r => r.Pos == LexiconSchema.Conjunction) || SpanishAnalysisTables.IsClauseOpener(TextOf(j)))
            {
                break;
            }

            if (IsEventNounAt(j))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsEventNounAt(int index)
    {
        return FirstReading(index, static r => r.Pos == LexiconSchema.Noun && SpanishAnalysisTables.IsEventNoun(r.Lemma)) is not null && !IsInPrepositionalPhrase(index);
    }

    // cerca de with an hour, a number or a time of day says about when or how many (son cerca de las tres, cerca de cien), which ser takes. M64 (the review of
    // feature/village, R6): del, the contraction with the article in it, is de as well (es cerca del mediodía), and a place after it is still a place (la tienda es cerca
    // del río -> está).
    private bool IsApproximateTime(int adverbIndex)
    {
        var de = adverbIndex + 1;
        if (de >= _tokens.Count || TextOf(de) is not ("de" or "del"))
        {
            return false;
        }

        var next = de + 1;
        if (TextOf(de) == "de" && next < _tokens.Count && FirstReading(next, static r => r.Pos == LexiconSchema.Article) is not null)
        {
            next++;
        }

        return next < _tokens.Count
            && (FirstReading(next, static r => r.Pos == LexiconSchema.Numeral) is not null
                || FirstReading(next, static r => r.Pos == LexiconSchema.Noun && SpanishAnalysisTables.IsDurationNoun(r.Lemma)) is not null);
    }
}
