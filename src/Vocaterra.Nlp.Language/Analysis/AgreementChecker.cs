namespace Vocaterra.Nlp.Language.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;

/// <summary>Runs the grammar checks (subject-verb, tense, ser/estar, noun phrase agreement, word order) over analyzed tokens.</summary>
internal sealed class AgreementChecker
{
    private readonly IReadOnlyList<AnalyzedToken> _tokens;
    private readonly MorphologyGenerator _morphology;
    private readonly List<ErrorTag> _errors;

    public AgreementChecker(IReadOnlyList<AnalyzedToken> tokens, MorphologyGenerator morphology, List<ErrorTag> errors)
    {
        _tokens = tokens;
        _morphology = morphology;
        _errors = errors;
    }

    public void Run()
    {
        CheckSubjectVerb();
        CheckTense();
        CheckNearFuture();
        CheckIrWithoutA();
        CheckSerEstar();
        CheckNounPhrases();
    }

    private static bool IsFiniteVerb(LexicalFeatures reading)
    {
        return ReadingClasses.IsFiniteVerb(reading);
    }

    // A finite form that can follow an explicit subject: the imperative is left out, because imperatives share their spelling with the third person
    // of the present (habla, come, está) and would otherwise make "tú habla" agree with tú and "ella está feliz" a mistake about the form eres.
    private static bool IsDeclarativeFiniteVerb(LexicalFeatures reading)
    {
        return IsFiniteVerb(reading) && reading.Mood != LexiconSchema.Imperative;
    }

    private static bool Compatible(string? left, string? right)
    {
        return left is null || right is null || string.Equals(left, right, StringComparison.Ordinal);
    }

    private static bool IsDeterminerReading(LexicalFeatures reading)
    {
        return ReadingClasses.IsDeterminerReading(reading);
    }

    private static bool GenderCompatible(LexicalFeatures modifier, LexicalFeatures noun)
    {
        if (Compatible(modifier.Gender, noun.Gender))
        {
            return true;
        }

        // el agua, un arma: feminine nouns that start with a stressed a take the masculine singular article.
        return modifier.Pos is LexiconSchema.Article or LexiconSchema.Numeral
            && modifier.Gender == LexiconSchema.Masculine
            && modifier.Number == LexiconSchema.Singular
            && noun.Gender == LexiconSchema.Feminine
            && noun.Number == LexiconSchema.Singular
            && WordClasses.TakesElBeforeFeminine(noun.Lemma);
    }

    private static string? DeterminerForm(MorphologyGenerator morphology, LexicalFeatures determiner, string gender, string number, string nounLemma)
    {
        switch (determiner.Pos)
        {
            case LexiconSchema.Article:
                return morphology.Article(
                    determiner.Lemma == "el" ? Definiteness.Definite : Definiteness.Indefinite,
                    gender,
                    number,
                    nounLemma);
            case LexiconSchema.Numeral:
                return determiner.Lemma switch
                {
                    "1" => gender == LexiconSchema.Feminine ? "una" : "un",
                    "21" => gender == LexiconSchema.Feminine ? "veintiuna" : "veintiún",
                    _ => null,
                };
            case LexiconSchema.Adjective:
                return morphology.AgreeAdjective(determiner.Lemma, gender, number);
            default:
                return null;
        }
    }

    private LexicalFeatures? FirstReading(int index, Func<LexicalFeatures, bool> predicate)
    {
        foreach (var reading in _tokens[index].Readings)
        {
            if (predicate(reading))
            {
                return reading;
            }
        }

        return null;
    }

    private List<LexicalFeatures> Readings(int index, Func<LexicalFeatures, bool> predicate)
    {
        return _tokens[index].Readings.Where(predicate).ToList();
    }

    private TextSpan SpanOf(int first, int last)
    {
        var start = _tokens[first].Token.Span.Start;
        return new TextSpan(start, _tokens[last].Token.Span.End - start);
    }

    private string TextOf(int index)
    {
        return _tokens[index].Token.Text;
    }

    private void Add(string tag, int first, int last, string expected, string actual)
    {
        _errors.Add(new ErrorTag(tag, SpanOf(first, last), expected, actual));
    }

    private bool IsSkippable(int index)
    {
        return ReadingClasses.IsSkippable(_tokens[index].Readings);
    }

    private int NextContentIndex(int from)
    {
        var index = from;
        var skipped = 0;
        while (index < _tokens.Count && skipped < ReadingClasses.MaxSkippedBetween && IsSkippable(index))
        {
            index++;
            skipped++;
        }

        return index < _tokens.Count ? index : -1;
    }

    private void CheckSubjectVerb()
    {
        for (var i = 0; i < _tokens.Count; i++)
        {
            var pronoun = FirstReading(i, static r => r.Pos == LexiconSchema.Pronoun && r.Person is not null && WordClasses.IsSubjectPronoun(r.Lemma));
            if (pronoun is null)
            {
                continue;
            }

            var verbIndex = NextContentIndex(i + 1);
            if (verbIndex < 0)
            {
                continue;
            }

            var finite = Readings(verbIndex, IsDeclarativeFiniteVerb);
            if (finite.Count == 0)
            {
                // A bare infinitive after a subject pronoun ("yo comprar") is a person error as well.
                var infinitive = FirstReading(verbIndex, static r => r.Pos == LexiconSchema.Verb && r.Person is null && r.Tense is null && r.Mood is null);
                if (infinitive is not null)
                {
                    AddPersonError(verbIndex, infinitive.Lemma, pronoun, LexiconSchema.Present);
                }

                continue;
            }

            if (finite.Any(r => r.Person == pronoun.Person && r.Number == pronoun.Number))
            {
                continue;
            }

            var representative = finite.FirstOrDefault(static r => r.Mood == LexiconSchema.Indicative) ?? finite[0];
            AddPersonError(verbIndex, representative.Lemma, pronoun, representative.Tense ?? LexiconSchema.Present);
        }
    }

    private void AddPersonError(int verbIndex, string lemma, LexicalFeatures pronoun, string tense)
    {
        var expected = _morphology.Conjugate(lemma, pronoun.Person!, pronoun.Number!, tense) ?? string.Empty;
        Add(ErrorTags.ConjPerson, verbIndex, verbIndex, expected, TextOf(verbIndex));
    }

    private void CheckTense()
    {
        if (!HasPastTimeMarker())
        {
            return;
        }

        // A finite verb right after "ir a" is already reported by CheckNearFuture, with the infinitive as the expected form ("ayer voy a como" -> "comer"); a second
        // conj.tense on the same word would be two errors with one tag and one span.
        var afterIrA = FormsAfterIrA();
        for (var i = 0; i < _tokens.Count; i++)
        {
            if (afterIrA.ContainsKey(i))
            {
                continue;
            }

            var finite = Readings(i, static r => IsFiniteVerb(r) && r.Tense is not null);
            if (finite.Count == 0
                || finite.Any(static r => r.Tense is LexiconSchema.Preterite or LexiconSchema.Imperfect or LexiconSchema.Conditional))
            {
                continue;
            }

            var present = finite.FirstOrDefault(static r => r.Tense == LexiconSchema.Present)
                ?? finite.FirstOrDefault(static r => r.Tense == LexiconSchema.Future);
            if (present is null)
            {
                continue;
            }

            var expected = _morphology.Conjugate(present.Lemma, present.Person!, present.Number!, LexiconSchema.Preterite) ?? string.Empty;
            Add(ErrorTags.ConjTense, i, i, expected, TextOf(i));
        }
    }

    // Rule M49 (the near future). Two checks, both conj.tense:
    //  1. A future-time marker (mañana, pasado mañana, la próxima semana, el año que viene) with a finite preterite or imperfect form: the expected form is the
    //     ir a + infinitive for that person ("mañana comí" -> "voy a comer"), the form the game teaches; "ir a" itself (mañana fui a la tienda) expects the
    //     present ("voy"). The present stays correct ("mañana como pan") and so does the future. A sentence that also has a past marker is left to CheckTense.
    //     luego and más tarde are not markers: "luego comí" is ordinary narration.
    //  2. A finite verb right after "ir a" ("voy a como", "vamos a tenemos"): the expected form is the infinitive. A noun phrase after a ("voy a la casa") or a
    //     word that can be a noun is left alone.
    private void CheckNearFuture()
    {
        var afterIrA = FormsAfterIrA();
        foreach (var (next, verb) in afterIrA.OrderBy(static pair => pair.Key).Select(static pair => (pair.Key, pair.Value)))
        {
            Add(ErrorTags.ConjTense, next, next, verb.Lemma, TextOf(next));
        }

        var marker = FutureTimeMarkerIndex();
        if (marker < 0 || HasPastTimeMarker())
        {
            return;
        }

        var seenQue = false;
        for (var i = 0; i < _tokens.Count; i++)
        {
            if (string.Equals(TextOf(i), "que", StringComparison.Ordinal))
            {
                seenQue = true;
            }

            // A verb that governs a reported clause ("dijo que mañana venía") is not in the time of the marker: a que between the verb and the marker ends the check for it.
            if (i < marker && Enumerable.Range(i + 1, marker - i - 1).Any(j => string.Equals(TextOf(j), "que", StringComparison.Ordinal)))
            {
                continue;
            }

            if (afterIrA.ContainsKey(i))
            {
                continue;
            }

            var finite = Readings(i, static r => IsDeclarativeFiniteVerb(r) && r.Tense is not null);
            if (finite.Count == 0
                || finite.Any(static r => r.Tense is LexiconSchema.Present or LexiconSchema.Future or LexiconSchema.Conditional)
                || (seenQue && finite.All(static r => r.Tense == LexiconSchema.Imperfect)))
            {
                continue;
            }

            var past = finite[0];
            string expected;
            if (past.Lemma == "ir" && i + 1 < _tokens.Count && string.Equals(TextOf(i + 1), "a", StringComparison.Ordinal))
            {
                expected = _morphology.Conjugate("ir", past.Person!, past.Number!, LexiconSchema.Present) ?? string.Empty;
            }
            else
            {
                var goer = _morphology.Conjugate("ir", past.Person!, past.Number!, LexiconSchema.Present);
                expected = goer is null ? string.Empty : goer + " a " + past.Lemma;
            }

            Add(ErrorTags.ConjTense, i, i, expected, TextOf(i));
        }
    }

    // The tokens that are a finite verb right after "ir a" ("voy a como"), each with the reading the error is reported for.
    private Dictionary<int, LexicalFeatures> FormsAfterIrA()
    {
        var found = new Dictionary<int, LexicalFeatures>();
        for (var i = 0; i + 2 < _tokens.Count; i++)
        {
            if (FirstReading(i, static r => IsDeclarativeFiniteVerb(r) && r.Lemma == "ir" && r.Tense is LexiconSchema.Present or LexiconSchema.Imperfect) is null
                || !string.Equals(TextOf(i + 1), "a", StringComparison.Ordinal))
            {
                continue;
            }

            var next = i + 2;
            var readings = _tokens[next].Readings;
            if (readings.Count == 0 || readings.Any(r => !IsDeclarativeFiniteVerb(r) && r.Pos != LexiconSchema.Adverb && r.Pos != LexiconSchema.Conjunction))
            {
                continue;
            }

            var verb = readings.FirstOrDefault(static r => IsDeclarativeFiniteVerb(r) && r.Mood == LexiconSchema.Indicative) ?? readings.FirstOrDefault(IsDeclarativeFiniteVerb);
            if (verb is not null)
            {
                found[next] = verb;
            }
        }

        return found;
    }

    // Rule M51 (ir + a bare infinitive). A present or imperfect form of "ir" directly followed by an infinitive drops the "a": "voy comer pan" -> "voy a comer".
    // conj.tense (the same family as M49: the learner is building the near future), the span covers the pair so the expected text is the whole corrected phrase.
    // Left clean: any word between the two ("voy a comer", "voy contento", "vamos a la tienda"), a gerund or participle (not infinitives), a next word that can
    // also be a noun or adjective ("voy cantar" if cantar had a noun reading), and the preterite ("fui comer": fui is also ser, and the fix is not obvious).
    private void CheckIrWithoutA()
    {
        for (var i = 0; i + 1 < _tokens.Count; i++)
        {
            var go = FirstReading(i, static r => IsDeclarativeFiniteVerb(r) && r.Lemma == "ir" && r.Tense is LexiconSchema.Present or LexiconSchema.Imperfect);
            if (go is null)
            {
                continue;
            }

            var next = _tokens[i + 1].Readings;
            if (next.Count == 0
                || next.Any(static r => r.Pos is LexiconSchema.Noun or LexiconSchema.Adjective)
                || !next.Any(static r => r.Pos == LexiconSchema.Verb && r.Person is null && r.Tense is null && r.Mood is null))
            {
                continue;
            }

            var infinitive = next.First(static r => r.Pos == LexiconSchema.Verb && r.Person is null && r.Tense is null && r.Mood is null);
            Add(ErrorTags.ConjTense, i, i + 1, TextOf(i) + " a " + infinitive.Lemma, TextOf(i) + " " + TextOf(i + 1));
        }
    }

    /// <summary>The index of the first future-time marker (the time noun for a phrase), or -1: mañana (not "la mañana"), pasado mañana, próximo/a + a time noun, or a time noun + que viene.</summary>
    private int FutureTimeMarkerIndex()
    {
        for (var i = 0; i < _tokens.Count; i++)
        {
            if (FirstReading(i, static r => r.Pos == LexiconSchema.Adverb && r.Lemma == "mañana") is not null
                && (i == 0 || TextOf(i - 1) is not ("la" or "esta" or "una" or "cada" or "toda" or "esa" or "aquella" or "de")))
            {
                return i;
            }

            if (i > 0
                && FirstReading(i - 1, static r => r.Pos == LexiconSchema.Adjective && r.Lemma == "próximo") is not null
                && FirstReading(i, static r => r.Pos == LexiconSchema.Noun && AnalysisTables.IsUpcomingTimeNoun(r.Lemma)) is not null)
            {
                return i;
            }

            if (i > 0
                && FirstReading(i - 1, static r => r.Pos == LexiconSchema.Noun && AnalysisTables.IsTimeNoun(r.Lemma)) is not null
                && string.Equals(TextOf(i), "que", StringComparison.Ordinal)
                && i + 1 < _tokens.Count
                && string.Equals(TextOf(i + 1), "viene", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>True when the sentence has a past-time adverb (ayer, anoche, anteayer) or a time noun with <c>pasado</c> (la semana pasada).</summary>
    public bool HasPastTimeMarker()
    {
        for (var i = 0; i < _tokens.Count; i++)
        {
            if (FirstReading(i, static r => r.Pos == LexiconSchema.Adverb && AnalysisTables.IsPastTimeAdverb(r.Lemma)) is not null)
            {
                return true;
            }

            if (i > 0
                && FirstReading(i, static r => r.Pos == LexiconSchema.Adjective && r.Lemma == "pasado") is not null
                && FirstReading(i - 1, static r => r.Pos == LexiconSchema.Noun && AnalysisTables.IsTimeNoun(r.Lemma)) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private void CheckSerEstar()
    {
        for (var i = 0; i < _tokens.Count; i++)
        {
            var copula = FirstReading(i, static r => IsDeclarativeFiniteVerb(r) && r.Lemma is "ser" or "estar");
            if (copula is null)
            {
                continue;
            }

            var adjectiveIndex = NextContentIndex(i + 1);
            if (adjectiveIndex < 0)
            {
                continue;
            }

            // A form can be an adjective under two lemmas (ocupada, a lemma of its own with the sense "pregnant", and a form of ocupado); the class of the
            // word is decided by all of them, not by whichever lemma sorts first.
            var adjectives = Readings(adjectiveIndex, static r => r.Pos == LexiconSchema.Adjective);
            if (adjectives.Count == 0)
            {
                continue;
            }

            var takesEstar = adjectives.Exists(static a => AnalysisTables.TakesEstar(a.Lemma));
            var takesSer = adjectives.Exists(static a => AnalysisTables.TakesSer(a.Lemma));
            var wrongCopula = copula.Lemma == "ser" && takesEstar && !takesSer;
            var wrongEstar = copula.Lemma == "estar" && takesSer && !takesEstar;
            if (!wrongCopula && !wrongEstar)
            {
                continue;
            }

            var target = wrongCopula ? "estar" : "ser";
            var expected = _morphology.Conjugate(target, copula.Person!, copula.Number!, copula.Tense ?? LexiconSchema.Present) ?? string.Empty;
            Add(ErrorTags.SerEstar, i, i, expected, TextOf(i));
        }
    }

    private void CheckNounPhrases()
    {
        var index = 0;
        while (index < _tokens.Count)
        {
            if (TryReadNounPhrase(index, out var phrase))
            {
                EvaluateNounPhrase(phrase);
                index = phrase.Last + 1;
            }
            else
            {
                index++;
            }
        }
    }

    private bool IsDeterminer(int index)
    {
        return _tokens[index].Readings.Any(IsDeterminerReading);
    }

    private bool IsAdjunct(int index)
    {
        var readings = _tokens[index].Readings;
        return readings.Any(static r => r.Pos == LexiconSchema.Adjective && !WordClasses.IsDeterminerAdjective(r.Lemma))
            && !readings.Any(static r => r.Pos == LexiconSchema.Noun || IsFiniteVerb(r));
    }

    private bool IsNounHead(int index, bool hasContext)
    {
        var readings = _tokens[index].Readings;
        return readings.Any(static r => r.Pos == LexiconSchema.Noun) && (hasContext || !readings.Any(IsFiniteVerb));
    }

    private bool TryReadNounPhrase(int start, out NounPhrase phrase)
    {
        var count = _tokens.Count;
        var cursor = start;
        var determiners = new List<int>();
        while (cursor < count && IsDeterminer(cursor))
        {
            determiners.Add(cursor);
            cursor++;
        }

        var before = new List<int>();
        while (cursor < count && IsAdjunct(cursor))
        {
            before.Add(cursor);
            cursor++;
        }

        if (cursor >= count || !IsNounHead(cursor, determiners.Count + before.Count > 0))
        {
            phrase = default!;
            return false;
        }

        var head = cursor;
        cursor++;
        var after = new List<int>();
        while (cursor < count && IsAdjunct(cursor))
        {
            after.Add(cursor);
            cursor++;
        }

        phrase = new NounPhrase(determiners, before, head, after);
        return true;
    }

    private void EvaluateNounPhrase(NounPhrase phrase)
    {
        var nounReadings = Readings(phrase.Head, static r => r.Pos == LexiconSchema.Noun);
        var noun = nounReadings.FirstOrDefault(static r => r.Gender is not null) ?? nounReadings[0];
        if (TryEvaluateFixedPluralExpression(phrase, nounReadings))
        {
            return;
        }

        foreach (var determiner in phrase.Determiners)
        {
            CheckDeterminer(determiner, phrase.Head, nounReadings, noun);
        }

        var effectiveNumber = noun.Number;
        var numberFromDeterminer = false;
        foreach (var determiner in phrase.Determiners)
        {
            var numbered = FirstReading(determiner, static r => IsDeterminerReading(r) && r.Number is not null);
            if (numbered is not null)
            {
                effectiveNumber = numbered.Number;
                numberFromDeterminer = true;
                break;
            }
        }

        foreach (var adjective in phrase.Before.Concat(phrase.After))
        {
            CheckAdjective(adjective, nounReadings, noun, effectiveNumber, numberFromDeterminer);
        }

        CheckWordOrder(phrase);
    }

    // Rule M29c, "a fixed plural expression keeps its plural and the noun's gender": in buenos días, buenas tardes, buenas noches and muchas gracias the phrase is
    // plural as a whole. When either word is already plural (and no other determiner fixes the number), the modifier is corrected to the noun's gender and the plural
    // ("mucho gracias" gives muchas, "buenos día" corrects the noun to días), never the plural noun back to the singular, which would turn muchas gracias into
    // "mucha gracia". Everything else keeps the generic rule: a determiner fixes the number of the noun, an adjective agrees with the noun.
    private bool TryEvaluateFixedPluralExpression(NounPhrase phrase, List<LexicalFeatures> nounReadings)
    {
        if (phrase.Determiners.Count > 1)
        {
            return false;
        }

        var modifier = -1;
        LexicalFeatures? head = null;
        foreach (var index in phrase.Determiners.Concat(phrase.Before))
        {
            foreach (var adjective in Readings(index, static r => r.Pos == LexiconSchema.Adjective))
            {
                head = nounReadings.Find(n => AnalysisTables.IsFixedPluralExpression(adjective.Lemma, n.Lemma));
                if (head is not null)
                {
                    modifier = index;
                    break;
                }
            }

            if (modifier >= 0)
            {
                break;
            }
        }

        if (modifier < 0 || head is null || (phrase.Determiners.Count == 1 && phrase.Determiners[0] != modifier))
        {
            return false;
        }

        var adjectives = Readings(modifier, static r => r.Pos == LexiconSchema.Adjective);
        var headReadings = nounReadings.Where(n => string.Equals(n.Lemma, head.Lemma, StringComparison.Ordinal)).ToList();
        if (!adjectives.Exists(static a => a.Number == LexiconSchema.Plural) && !headReadings.Exists(static n => n.Number == LexiconSchema.Plural))
        {
            return false;
        }

        var gender = head.Gender ?? adjectives[0].Gender ?? LexiconSchema.Masculine;
        var expected = _morphology.AgreeAdjective(adjectives[0].Lemma, gender, LexiconSchema.Plural);
        if (!string.Equals(expected, TextOf(modifier), StringComparison.Ordinal))
        {
            if (!adjectives.Any(a => headReadings.Any(n => GenderCompatible(a, n))))
            {
                Add(ErrorTags.GenderAgree, modifier, modifier, expected, TextOf(modifier));
            }

            if (!adjectives.Exists(static a => a.Number == LexiconSchema.Plural))
            {
                Add(ErrorTags.NounNumber, modifier, modifier, expected, TextOf(modifier));
            }
        }

        if (!headReadings.Exists(static n => n.Number == LexiconSchema.Plural))
        {
            var plural = _morphology.InflectNoun(head.Lemma, LexiconSchema.Plural);
            if (!string.Equals(plural, TextOf(phrase.Head), StringComparison.Ordinal))
            {
                Add(ErrorTags.NounNumber, phrase.Head, phrase.Head, plural, TextOf(phrase.Head));
            }
        }

        foreach (var adjective in phrase.Before.Concat(phrase.After).Where(i => i != modifier))
        {
            CheckAdjective(adjective, headReadings, head, LexiconSchema.Plural, numberFromDeterminer: true);
        }

        CheckWordOrder(phrase);
        return true;
    }

    private void CheckDeterminer(int determinerIndex, int headIndex, List<LexicalFeatures> nounReadings, LexicalFeatures noun)
    {
        var readings = Readings(determinerIndex, IsDeterminerReading);
        var genderOk = readings.Any(d => nounReadings.Any(n => GenderCompatible(d, n)));
        var numberOk = readings.Any(d => nounReadings.Any(n => Compatible(d.Number, n.Number)));
        if (genderOk && numberOk)
        {
            return;
        }

        var determiner = readings[0];
        if (!genderOk && noun.Gender is not null)
        {
            var form = DeterminerForm(_morphology, determiner, noun.Gender, determiner.Number ?? noun.Number ?? LexiconSchema.Singular, noun.Lemma);
            if (form is not null && !string.Equals(form, TextOf(determinerIndex), StringComparison.Ordinal))
            {
                Add(ErrorTags.GenderAgree, determinerIndex, determinerIndex, form, TextOf(determinerIndex));
            }
        }

        if (!numberOk && determiner.Number is not null)
        {
            var form = _morphology.InflectNoun(noun.Lemma, determiner.Number);
            if (!string.Equals(form, TextOf(headIndex), StringComparison.Ordinal))
            {
                Add(ErrorTags.NounNumber, headIndex, headIndex, form, TextOf(headIndex));
            }
        }
    }

    private void CheckAdjective(int adjectiveIndex, List<LexicalFeatures> nounReadings, LexicalFeatures noun, string? effectiveNumber, bool numberFromDeterminer)
    {
        var readings = Readings(adjectiveIndex, static r => r.Pos == LexiconSchema.Adjective);
        var genderOk = readings.Any(a => nounReadings.Any(n => GenderCompatible(a, n)));

        // A determiner that carries a number fixes the number of the phrase. Without one, a noun form that reads as singular and as plural
        // (corazones, the card suit, next to the plural of corazón) agrees with an adjective of either number.
        var numberOk = readings.Any(a => Compatible(a.Number, effectiveNumber)
            || (!numberFromDeterminer && nounReadings.Any(n => Compatible(a.Number, n.Number))));
        if (genderOk && numberOk)
        {
            return;
        }

        var adjective = readings[0];
        var gender = noun.Gender ?? adjective.Gender ?? LexiconSchema.Masculine;
        var number = effectiveNumber ?? adjective.Number ?? LexiconSchema.Singular;
        var expected = _morphology.AgreeAdjective(adjective.Lemma, gender, number);
        if (string.Equals(expected, TextOf(adjectiveIndex), StringComparison.Ordinal))
        {
            return;
        }

        if (!genderOk)
        {
            Add(ErrorTags.GenderAgree, adjectiveIndex, adjectiveIndex, expected, TextOf(adjectiveIndex));
        }

        if (!numberOk)
        {
            Add(ErrorTags.NounNumber, adjectiveIndex, adjectiveIndex, expected, TextOf(adjectiveIndex));
        }
    }

    private void CheckWordOrder(NounPhrase phrase)
    {
        if (phrase.Before.Count == 0)
        {
            return;
        }

        var adjectiveIndex = phrase.Before[^1];
        var adjectives = Readings(adjectiveIndex, static r => r.Pos == LexiconSchema.Adjective);
        if (adjectives.Count == 0 || adjectives.Exists(static a => AnalysisTables.IsPrenominal(a.Lemma)))
        {
            return;
        }

        // "la próxima semana", "el próximo año": próximo goes before a time noun (but "unos próximos salones" is still a word-order mistake).
        if (adjectives.Exists(static a => a.Lemma == "próximo") && Readings(phrase.Head, static r => r.Pos == LexiconSchema.Noun && AnalysisTables.IsUpcomingTimeNoun(r.Lemma)).Count > 0)
        {
            return;
        }

        var actual = TextOf(adjectiveIndex) + " " + TextOf(phrase.Head);
        var expected = TextOf(phrase.Head) + " " + TextOf(adjectiveIndex);
        Add(ErrorTags.WordOrder, adjectiveIndex, phrase.Head, expected, actual);
    }

    private sealed record NounPhrase(List<int> Determiners, List<int> Before, int Head, List<int> After)
    {
        public int Last => After.Count > 0 ? After[^1] : Head;
    }
}
