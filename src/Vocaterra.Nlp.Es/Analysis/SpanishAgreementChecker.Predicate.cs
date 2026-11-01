namespace Vocaterra.Nlp.Es.Analysis;

using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>The rule of M62: an adjective after <c>ser</c> or <c>estar</c> agrees with the subject of the copula (<c>gender.agree</c>, <c>noun.number</c>).</summary>
internal sealed partial class SpanishAgreementChecker
{
    // Rule M62 (a), "a predicate adjective agrees with its subject". The adjective after a finite form of ser or estar (past at most two adverbs: muy, más, menos, tan),
    // and the adjectives joined to it by y, e, o, u or pero, agree with the copula's subject in gender and number, as an adjective agrees with its noun: la manzana es
    // rojo -> roja, el pan es grandes -> grande, las manzanas son caro -> caras (gender.agree and noun.number, as the attributive check gives for the same double slip),
    // la manzana es más barato que el pan -> barata. The subject is, in this order:
    //  1. a subject pronoun before the copula (past at most two skippable words) that no preposition governs, with its gender when it has one: ella es alto -> alta,
    //     nosotros somos alto -> altos; yo, tú, usted and ustedes have no gender, so only their number is checked (yo soy alto and yo soy alta are both clean); M64: a
    //     pronoun that ends a coordinated subject gives the plural, and the gender only when every member has one (ella y él son altos; él y yo somos altos);
    //  2. before a copula of the third person, the noun phrase that opens its clause (M59's subject, TryFindPreverbalSubject), with its noun's gender (the determiners
    //     decide for a noun of both genders) and its number; two phrases joined by y are plural, and masculine unless both are feminine. A lone name (one bare noun,
    //     Luz es alta) may be the person spoken to, and the core does not know a name's gender, so only the copula's number is checked after it; names joined by y
    //     give the plural and no gender; M64: an amount read as one sum before a singular ser gives the singular and no gender (dos panes es suficiente);
    //  3. after a copula of the third person, a noun phrase with a determiner, or a subject pronoun of the third person, right after the adjectives (es cara la manzana,
    //     ¿es alta ella?), unless it is a phrase of time (es caro el lunes) or coordinated;
    //  4. otherwise the copula's own number, and no gender: the subject that the verb agrees with, said or not (es caro and es muy caro are clean; es caros -> caro).
    // Left clean: an adjective before a noun after the copula (es buena gente: a noun phrase, whose adjective agrees with its noun), a determiner adjective (es mucho), a
    // preposition after the copula (la casa es de Luz, el pan es para Luz), an adverb (es lejos, ser_estar's), the pasado of pasado mañana, and a word the reading resolver
    // took for a noun (el pan es comida). The resolver reads a form of the copula that is also a noun (son, era) as the copula after a noun phrase with a determiner and
    // before an adjective, and a word after muy or tan as no noun (es muy cara).
    private void CheckPredicateAdjectives()
    {
        for (var c = 0; c < _tokens.Count; c++)
        {
            var readings = _tokens[c].Readings;
            if (readings.Count == 0 || !readings.All(IsFiniteVerb))
            {
                continue;
            }

            var copulas = Readings(c, static r => IsDeclarativeFiniteVerb(r) && r.Lemma is "ser" or "estar");
            if (copulas.Count == 0)
            {
                continue;
            }

            var adjectives = PredicateAdjectivesAfter(c);
            if (adjectives.Count == 0)
            {
                continue;
            }

            var subject = PredicateSubjectOf(c, copulas, adjectives[^1]);
            foreach (var adjective in adjectives)
            {
                CheckPredicateAdjective(adjective, subject);
            }
        }
    }

    // The gender and number a predicate adjective takes from its subject; either may be unknown (null), and then it is not checked.
    private readonly record struct PredicateSubject(string? Gender, string? Number);

    // The predicate adjectives of the copula at index: the first word after it (past at most two adverbs) when it is an adjective and starts no noun phrase, and those
    // joined to it by y, e, o, u or pero.
    private List<int> PredicateAdjectivesAfter(int copula)
    {
        var found = new List<int>();
        var index = NextContentIndex(copula + 1);
        while (index >= 0 && IsPredicateAdjective(index))
        {
            found.Add(index);
            var link = index + 1;
            if (link + 1 >= _tokens.Count || TextOf(link) is not ("y" or "e" or "o" or "u" or "pero"))
            {
                break;
            }

            index = NextContentIndex(link + 1);
        }

        return found;
    }

    // A descriptive adjective (no determiner adjective, no noun or finite verb reading left) that is not the first word of a noun phrase whose noun follows it (es buena
    // gente) and not the pasado of pasado mañana.
    private bool IsPredicateAdjective(int index)
    {
        if (!IsAdjunct(index) || IsDayAfterTomorrow(index))
        {
            return false;
        }

        return !(TryReadNounPhrase(index, out var phrase) && phrase.Before.Contains(index));
    }

    private PredicateSubject PredicateSubjectOf(int copula, List<LexicalFeatures> copulas, int lastAdjective)
    {
        var copulaNumber = copulas.Select(static r => r.Number).Distinct(StringComparer.Ordinal).Count() == 1 ? copulas[0].Number : null;
        var thirdPerson = copulas.Exists(static r => r.Person == "3");

        // 1. A subject pronoun before the copula.
        var p = copula - 1;
        for (var skipped = 0; p >= 0; skipped++)
        {
            var pronoun = FirstReading(p, static r => r.Pos == LexiconSchema.Pronoun && r.Person is not null && WordClasses.IsSubjectPronoun(r.Lemma));
            if (pronoun is not null)
            {
                if (p > 0 && IsPreposition(p - 1))
                {
                    break;
                }

                // M64: the last pronoun of a coordinated subject gives the plural, and a gender only when every member has one (ella y él son altos, él y yo somos
                // altos); a coordination that cannot be told gives neither.
                return CoordinationOf(p, pronoun, out var coordinated) switch
                {
                    Coordination.Subject => new PredicateSubject(coordinated.Gender, LexiconSchema.Plural),
                    Coordination.Unknown => new PredicateSubject(null, null),
                    _ => new PredicateSubject(pronoun.Gender, pronoun.Number),
                };
            }

            if (skipped >= ReadingClasses.MaxSkippedBetween || !IsSkippable(p))
            {
                break;
            }

            p--;
        }

        // 2. The noun phrase that opens its clause before a copula of the third person.
        if (thirdPerson && TryFindPreverbalSubject(copula, out var preverbal))
        {
            // M64 (b): an amount read as one sum (dos panes es suficiente) takes the copula's singular and no gender, as a lone name does.
            if (preverbal.Lone || copulas.Exists(r => IsAmountReadAsUnit(copula, r, preverbal)))
            {
                return new PredicateSubject(null, copulaNumber);
            }

            return new PredicateSubject(PhrasesGender(preverbal.Phrases), preverbal.Number);
        }

        // 3. A noun phrase with a determiner or a subject pronoun of the third person after the adjectives.
        var after = NextContentIndex(lastAdjective + 1);
        if (thirdPerson && after >= 0)
        {
            var pronoun = FirstReading(after, static r => r.Pos == LexiconSchema.Pronoun && r.Person == "3" && WordClasses.IsSubjectPronoun(r.Lemma));
            if (pronoun is not null)
            {
                return new PredicateSubject(pronoun.Gender, pronoun.Number);
            }

            if (TryReadNounPhrase(after, out var phrase)
                && phrase.Determiners.Count > 0
                && IsSubjectCandidate(phrase)
                && SubjectNumber(phrase, timeIsSubject: false) is { } number)
            {
                return new PredicateSubject(PhrasesGender([phrase]), number);
            }
        }

        // 4. The copula's own number.
        return new PredicateSubject(null, copulaNumber);
    }

    // The gender of a subject of one or more noun phrases: that of the one noun (the determiners decide for a noun of both genders), and for phrases joined by y the
    // masculine unless all are feminine. A bare noun among them (a name) leaves it unknown.
    private string? PhrasesGender(IReadOnlyList<NounPhrase> phrases)
    {
        var genders = new List<string>();
        foreach (var phrase in phrases)
        {
            var gender = PhraseGender(phrase);
            if (gender is null || (phrases.Count > 1 && phrase.Determiners.Count == 0 && phrase.Before.Count == 0 && phrase.After.Count == 0))
            {
                return null;
            }

            genders.Add(gender);
        }

        return genders.Contains(LexiconSchema.Masculine) ? LexiconSchema.Masculine : genders[0];
    }

    private string? PhraseGender(NounPhrase phrase)
    {
        var genders = Readings(phrase.Head, static r => r.Pos == LexiconSchema.Noun)
            .Select(static n => n.Gender)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (genders.Count <= 1)
        {
            return genders.Count == 1 ? genders[0] : null;
        }

        // M64 (found by O4 at five times the default depth; older than M64): a feminine noun that takes el (el ala, el agua) is feminine whatever its article says. The
        // lexicon lists ala with both genders, and el made it masculine: el ala es tan sincera como una saeta gave gender.agree "sincero".
        if (Readings(phrase.Head, static r => r is { Pos: LexiconSchema.Noun, Gender: LexiconSchema.Feminine } && WordClasses.TakesElBeforeFeminine(r.Lemma)).Count > 0)
        {
            return LexiconSchema.Feminine;
        }

        foreach (var determiner in phrase.Determiners)
        {
            var own = Readings(determiner, IsDeterminerReading).Select(static r => r.Gender).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            if (own.Count == 1 && genders.Contains(own[0]))
            {
                return own[0];
            }
        }

        return null;
    }

    private void CheckPredicateAdjective(int index, PredicateSubject subject)
    {
        var readings = Readings(index, static r => r.Pos == LexiconSchema.Adjective && !WordClasses.IsDeterminerAdjective(r.Lemma));
        var genderOk = subject.Gender is null || readings.Exists(r => Compatible(r.Gender, subject.Gender));
        var numberOk = subject.Number is null || readings.Exists(r => Compatible(r.Number, subject.Number));
        if (genderOk && numberOk)
        {
            return;
        }

        var adjective = readings[0];
        var expected = _morphology.AgreeAdjective(
            adjective.Lemma,
            subject.Gender ?? adjective.Gender ?? LexiconSchema.Masculine,
            subject.Number ?? adjective.Number ?? LexiconSchema.Singular);
        if (string.IsNullOrEmpty(expected) || string.Equals(expected, TextOf(index), StringComparison.Ordinal))
        {
            return;
        }

        if (!genderOk)
        {
            Add(ErrorTags.GenderAgree, index, index, expected, TextOf(index));
        }

        if (!numberOk)
        {
            Add(ErrorTags.NounNumber, index, index, expected, TextOf(index));
        }
    }
}
