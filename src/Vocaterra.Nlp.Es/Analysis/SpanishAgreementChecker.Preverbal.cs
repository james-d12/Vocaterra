namespace Vocaterra.Nlp.Es.Analysis;

using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>The rules of M59: a noun or a name before its verb is its subject (<c>conj.person</c>), and a reflexive clitic after <c>cómo</c> agrees with its verb.</summary>
internal sealed partial class SpanishAgreementChecker
{
    // Rule M59 (a), "a noun subject before its verb agrees with it". A noun phrase that opens its clause and is followed by a finite verb (past at most two adverbs or
    // object pronouns) is the verb's subject, of the third person: los niños comen, las manzanas son rojas. Two noun phrases joined by y or e are one plural subject (Luz y
    // Domingo comieron). A third-person verb of the other number gives conj.person with the form of that number (los niños come -> comen, la manzana son roja -> es, Luz y
    // Domingo comió -> comieron). Correct Spanish is never flagged, and two readings of the words before the verb are, so they are left alone:
    //  - a lone name, one bare noun with no determiner or adjective that nothing is joined to, may be the person spoken to, and a transcript has no comma (Luz, quiero pan;
    //    Luz, ¿tienes pan?; Luz, ¿tienen pan?: ustedes; Mar, ¿pescaste en el río?), so its verb is not checked in any person or number;
    //  - before a verb of the first or second person, phrases joined by y may be the people spoken to (Luz y Domingo, queremos pan), and a phrase with a determiner the
    //    object put first (el pan quiero), so no such verb is checked.
    // A phrase opens its clause when nothing stands before it but adverbs, sí or no, phrases of time (el lunes, esta mañana) and one of SubjectClauseOpeners (y, pero, cuando,
    // dónde...). Also left clean: a phrase that a preposition governs (en la herrería trabaja Domingo), a phrase of time (el domingo los niños comen), a phrase asked about
    // (¿qué frutas come Luz?, ¿cuántas manzanas compra Luz?), a noun after an interjection, a phrase that an object pronoun between stands for (las manzanas las compra Luz,
    // a los niños les gusta), phrases joined by o, u or ni, a coordination with a pronoun or a name the lexicon lacks, haber (hay), ser that agrees with a plural predicate
    // (mi comida favorita son las manzanas), an amount before a singular ser with a singular predicate (M64: dos panes es suficiente), the subjunctive and the imperative.
    // A name that a grammar binds or that the lexicon lacks has no reading at all. A coordination that ends in a subject pronoun (Luz y yo fuimos) is CheckSubjectVerb's
    // (M64).
    private void CheckPreverbalSubject()
    {
        for (var v = 1; v < _tokens.Count; v++)
        {
            var readings = _tokens[v].Readings;
            if (readings.Count == 0 || !readings.All(IsFiniteVerb))
            {
                continue;
            }

            var finite = Readings(v, static r => IsDeclarativeFiniteVerb(r) && r.Mood == LexiconSchema.Indicative && r.Tense is not null);
            if (finite.Count == 0 || finite.Exists(static r => r.Lemma == "haber") || !TryFindPreverbalSubject(v, out var subject))
            {
                continue;
            }

            if (finite.Exists(r => r.Person == "3" && r.Number == subject.Number))
            {
                continue;
            }

            // A lone name may be the person spoken to, and so may the people of a coordination before a verb of the first or second person; a phrase with a determiner may be
            // that verb's object put first. Only a third-person verb after a phrase that is surely its subject is checked.
            var third = finite.Find(static r => r.Person == "3");
            if (third is null || (subject.Lone && !PredicateShowsNumber(v, third, subject.Number)))
            {
                continue;
            }

            if (third is { Lemma: "ser", Number: LexiconSchema.Plural } && PluralPredicateFollows(v))
            {
                continue;
            }

            // M64 (b): an amount before a singular ser may be one sum (diez monedas es mucho dinero, dos panes es suficiente), so es is as right as son.
            if (finite.Exists(r => IsAmountReadAsUnit(v, r, subject)))
            {
                continue;
            }

            var expected = _morphology.Conjugate(third.Lemma, "3", subject.Number, third.Tense!);
            if (string.IsNullOrEmpty(expected) || string.Equals(expected, TextOf(v), StringComparison.Ordinal))
            {
                continue;
            }

            Add(ErrorTags.ConjPerson, v, v, expected, TextOf(v));
        }
    }

    // M69 (M62's left point, son after a lone name): a lone name may be the person spoken to (Luz, son altas), so its verb is not checked; but a predicate adjective
    // of the name's number and not of the copula's after ser or estar shows that the name is the subject (luz son alta, pan son caro), and the copula is then the slip
    // (conj.person es; the adjective is not checked against the copula's number, SpanishAgreementChecker.Predicate.cs).
    private bool PredicateShowsNumber(int copula, LexicalFeatures verb, string number)
    {
        if (verb.Lemma is not ("ser" or "estar") || verb.Number == number)
        {
            return false;
        }

        var adjectives = PredicateAdjectivesAfter(copula);
        return adjectives.Count > 0
            && adjectives.TrueForAll(a =>
            {
                var readings = Readings(a, static r => r.Pos == LexiconSchema.Adjective && !WordClasses.IsDeterminerAdjective(r.Lemma));
                return readings.Exists(r => r.Number == number) && !readings.Exists(r => r.Number == verb.Number);
            });
    }

    // Rule M59 (b), "a reflexive clitic agrees with its verb". In the question cómo + me, te, se, nos or os + a verb of ReflexiveQuestionVerbs (¿cómo te llamas?, ¿cómo se
    // llama?, ¿cómo te sientes?) the clitic is reflexive, so the verb takes its person: cómo te llama -> llamas, cómo se llamas -> llama. se stands for the third person of
    // either number. Left clean: me or te before a verb of another first or second person, which is the verb's object (¿cómo te llamo?, what shall I call you), and before a
    // third person with a subject after it (¿cómo te llama tu madre?); any other verb (¿cómo te va?), and a clitic with no cómo before it (te veo, me ves, te llamo mañana).
    private void CheckReflexiveClitic()
    {
        for (var c = 1; c + 1 < _tokens.Count; c++)
        {
            if (TextOf(c - 1) is not ("cómo" or "como")
                || SpanishAnalysisTables.ReflexivePerson(TextOf(c)) is not { } reflexive
                || FirstReading(c, static r => r.Pos == LexiconSchema.Pronoun) is null)
            {
                continue;
            }

            var v = c + 1;
            var readings = _tokens[v].Readings;
            var finite = Readings(v, static r => IsDeclarativeFiniteVerb(r) && r.Mood == LexiconSchema.Indicative && r.Tense is not null
                && SpanishAnalysisTables.IsReflexiveQuestionVerb(r.Lemma));
            if (readings.Count == 0 || !readings.All(IsFiniteVerb) || finite.Count == 0)
            {
                continue;
            }

            string number;
            if (reflexive.Number is null)
            {
                if (finite.Exists(static r => r.Person == "3"))
                {
                    continue;
                }

                number = finite[0].Number!;
            }
            else
            {
                if (finite.Exists(r => r.Person == reflexive.Person && r.Number == reflexive.Number)
                    || !finite.Exists(static r => r.Person == "3")
                    || HasThirdPersonSubjectAfter(v))
                {
                    continue;
                }

                number = reflexive.Number;
            }

            var representative = finite.Find(static r => r.Person != "3") ?? finite[0];
            var expected = _morphology.Conjugate(representative.Lemma, reflexive.Person, number, representative.Tense!);
            if (string.IsNullOrEmpty(expected) || string.Equals(expected, TextOf(v), StringComparison.Ordinal))
            {
                continue;
            }

            Add(ErrorTags.ConjPerson, v, v, expected, TextOf(v));
        }
    }

    // The subject of a preverbal check: its number, whether it is a lone name (one bare noun that nothing is joined to), which may be the person spoken to, and its
    // phrases (two for a coordination; M62 reads their gender).
    private readonly record struct PreverbalSubject(string Number, bool Lone, IReadOnlyList<NounPhrase> Phrases);

    private bool TryFindPreverbalSubject(int verb, out PreverbalSubject subject)
    {
        subject = default;

        // Back over at most two adverbs, interjections or object pronouns (Domingo no trabaja, el pan lo compré).
        var end = verb - 1;
        var pronouns = new List<int>();
        for (var skipped = 0; end >= 0 && skipped < ReadingClasses.MaxSkippedBetween; skipped++)
        {
            if (IsObjectPronoun(end))
            {
                pronouns.Add(end);
            }
            else if (!IsSkippable(end))
            {
                break;
            }

            end--;
        }

        if (end < 0 || !TryFindPhraseEndingAt(end, out var last) || !IsSubjectCandidate(last) || pronouns.Exists(p => StandsFor(p, last)))
        {
            return false;
        }

        var phrases = new List<NounPhrase> { last };
        var start = StartOf(last);
        var opener = start > 0 ? TextOf(start - 1) : string.Empty;
        if (opener is "o" or "u" or "ni")
        {
            return false;
        }

        var coordinated = false;
        if (opener is "y" or "e" && start > 1)
        {
            var before = start - 2;
            if (TryFindPhraseEndingAt(before, out var other))
            {
                if (IsSubjectCandidate(other) && IsClauseStart(StartOf(other)))
                {
                    phrases.Add(other);
                    coordinated = true;
                }
                else if (!FollowsVerbOrPreposition(StartOf(other)))
                {
                    // A list (luz mar y domingo, without its commas) or a phrase that is no subject: the subject cannot be told.
                    return false;
                }

                // Otherwise y joins two clauses (como pan y Luz bebe agua) and opens this one.
            }
            else if (_tokens[before].Readings.Count == 0 || HasReading(before, static r => r.Pos == LexiconSchema.Pronoun))
            {
                // A pronoun or a name the lexicon lacks joined to the phrase (yo y Luz, Pedro y Luz).
                return false;
            }
        }

        if (!coordinated && !IsClauseStart(start))
        {
            return false;
        }

        var number = coordinated ? LexiconSchema.Plural : SubjectNumber(last, timeIsSubject: true);
        if (number is null)
        {
            return false;
        }

        var lone = !coordinated && last.Determiners.Count == 0 && last.Before.Count == 0 && last.After.Count == 0;
        subject = new PreverbalSubject(number, lone, phrases);
        return true;
    }

    // A noun phrase that can be the subject of a verb after it: not governed by a preposition, not a phrase of time and not one asked about (a question word among its
    // determiners: cuántas manzanas).
    private bool IsSubjectCandidate(NounPhrase phrase)
    {
        var start = StartOf(phrase);
        return !(start > 0 && IsPreposition(start - 1))
            && !IsTimePhrase(phrase)
            && !phrase.Determiners.Exists(d => SpanishAnalysisTables.IsClauseOpener(TextOf(d)));
    }

    // A phrase of time (el lunes, esta mañana, la semana pasada, todos los días, mañana): a noun of time with a determiner or an adjective, or one that is also an
    // adverb. A bare noun of time with no other reading is a lone name (Domingo), which is not checked either.
    private bool IsTimePhrase(NounPhrase phrase)
    {
        var head = _tokens[phrase.Head].Readings;
        if (!head.Any(static r => r.Pos == LexiconSchema.Noun && SpanishAnalysisTables.IsDurationNoun(r.Lemma)))
        {
            return false;
        }

        return phrase.Determiners.Count > 0 || phrase.Before.Count > 0 || phrase.After.Count > 0 || head.Any(static r => r.Pos != LexiconSchema.Noun);
    }

    // True when the phrase that starts at index opens its clause: before it there are only adverbs, sí or no, and phrases of time, and then the start of the utterance or a
    // word of SubjectClauseOpeners.
    private bool IsClauseStart(int index)
    {
        var k = index - 1;
        while (k >= 0)
        {
            var word = TextOf(k);
            if (SpanishAnalysisTables.IsClauseOpener(word))
            {
                break;
            }

            var readings = _tokens[k].Readings;
            if (SpanishAnalysisTables.IsAnswerWord(word) || (readings.Count > 0 && readings.All(static r => r.Pos == LexiconSchema.Adverb)))
            {
                k--;
                continue;
            }

            if (TryFindPhraseEndingAt(k, out var time) && IsTimePhrase(time))
            {
                k = StartOf(time) - 1;
                continue;
            }

            break;
        }

        return k < 0 || SpanishAnalysisTables.IsSubjectClauseOpener(TextOf(k));
    }

    // The longest noun phrase that ends at index (its head or its last adjective), read as CheckNounPhrases reads phrases.
    private bool TryFindPhraseEndingAt(int index, out NounPhrase phrase)
    {
        phrase = default!;
        var found = false;
        for (var start = index; start >= 0 && index - start <= 6; start--)
        {
            if (TryReadNounPhrase(start, out var candidate) && candidate.Last == index)
            {
                phrase = candidate;
                found = true;
            }
        }

        return found;
    }

    private static int StartOf(NounPhrase phrase)
    {
        return phrase.Determiners.Count > 0 ? phrase.Determiners[0] : phrase.Before.Count > 0 ? phrase.Before[0] : phrase.Head;
    }

    // A pronoun that stands before a verb as its object (me, te, se, lo, la, los, las, le, les, nos, os) and has no other reading.
    private bool IsObjectPronoun(int index)
    {
        var readings = _tokens[index].Readings;
        return readings.Count > 0 && readings.All(r => r.Pos == LexiconSchema.Pronoun && _rules.IsPreverbalPronoun(r.Lemma) && !WordClasses.IsSubjectPronoun(r.Lemma));
    }

    // A third-person object pronoun that agrees with the phrase stands for it (el pan lo compré, las manzanas las compra, a Luz le doy: lo, la, los, las in gender and
    // number, le and les in number), so the phrase is the verb's object or the person it is for, and not its subject.
    private bool StandsFor(int pronoun, NounPhrase phrase)
    {
        var nouns = Readings(phrase.Head, static r => r.Pos == LexiconSchema.Noun);
        return _tokens[pronoun].Readings.Any(p => p.Person == "3" && nouns.Exists(n => Compatible(p.Number, n.Number) && Compatible(p.Gender, n.Gender)));
    }

    private bool FollowsVerbOrPreposition(int index)
    {
        return index > 0 && (IsPreposition(index - 1) || HasReading(index - 1, static r => r.Pos == LexiconSchema.Verb));
    }

    private bool HasReading(int index, Func<LexicalFeatures, bool> predicate)
    {
        return _tokens[index].Readings.Any(predicate);
    }

    // ser agrees with a plural predicate (mi comida favorita son las manzanas, el problema son los niños): a plural noun phrase after a plural form of ser.
    private bool PluralPredicateFollows(int verb)
    {
        var start = NextContentIndex(verb + 1);
        return start >= 0 && TryReadNounPhrase(start, out var predicate) && SubjectNumber(predicate, timeIsSubject: true) == LexiconSchema.Plural;
    }

    // A subject of the third person after the verb: a noun phrase, a name (no reading), or a subject pronoun of the third person (¿cómo te llama tu madre?).
    private bool HasThirdPersonSubjectAfter(int verb)
    {
        var next = NextContentIndex(verb + 1);
        return next >= 0
            && (_tokens[next].Readings.Count == 0
                || TryReadNounPhrase(next, out _)
                || HasReading(next, static r => r.Pos == LexiconSchema.Pronoun && r.Person == "3" && WordClasses.IsSubjectPronoun(r.Lemma)));
    }
}
