namespace Vocaterra.Nlp.Es.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;

/// <summary>Runs the Spanish grammar checks (subject-verb, tense, ser/estar, noun phrase agreement, word order) over analyzed tokens; part of the Spanish pack since M34.</summary>
/// <remarks>
/// The rules of M56 (a subject after its verb, <c>ser</c> where a place takes <c>estar</c>) are in <c>SpanishAgreementChecker.Postverbal.cs</c>, and those of M59 (a noun
/// subject before its verb, a reflexive clitic after <c>cómo</c>) in <c>SpanishAgreementChecker.Preverbal.cs</c>, and that of M62 (an adjective after <c>ser</c> or
/// <c>estar</c> agrees with the copula's subject) in <c>SpanishAgreementChecker.Predicate.cs</c>, and those of M64 (a subject pronoun joined by <c>y</c> ends a plural
/// subject, an amount before a singular <c>ser</c> is one sum) in <c>SpanishAgreementChecker.Coordination.cs</c>, and that of M70 (<c>gustar</c> agrees with the thing
/// liked) in <c>SpanishAgreementChecker.Liking.cs</c>.
/// </remarks>
internal sealed partial class SpanishAgreementChecker : IAgreementChecker
{
    private readonly IReadOnlyList<AnalyzedToken> _tokens;
    private readonly IMorphology _morphology;
    private readonly List<ErrorTag> _errors;
    private readonly IAnalysisRules _rules;

    // M73: the verbs CheckSubjectVerb agreed with a subject pronoun before them, so that CheckPostverbalSubject does not take a phrase after one of them as a second
    // subject (one conj.person per word).
    private readonly HashSet<int> _verbsWithPronounSubject = [];

    public SpanishAgreementChecker(IReadOnlyList<AnalyzedToken> tokens, IMorphology morphology, List<ErrorTag> errors, IAnalysisRules rules)
    {
        _tokens = tokens;
        _morphology = morphology;
        _errors = errors;
        _rules = rules;
    }

    public void Run()
    {
        CheckSubjectVerb();
        CheckPreverbalSubject();
        CheckReflexiveClitic();
        CheckPostverbalSubject();
        CheckLikingVerb();
        CheckTense();
        CheckNearFuture();
        CheckIrWithoutA();
        CheckSerEstar();
        CheckSerOfPlace();
        CheckNounPhrases();
        CheckPredicateAdjectives();
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

    private bool IsDeterminerReading(LexicalFeatures reading)
    {
        return ReadingClasses.IsDeterminerReading(reading, _rules);
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

    private static string? DeterminerForm(IMorphology morphology, LexicalFeatures determiner, string gender, string number, string nounLemma)
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
        return ReadingClasses.IsSkippable(_tokens[index].Readings, _rules);
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
            // M56: a pronoun that a preposition governs is no subject (a ella le gustan las manzanas: ella is the one who likes, the apples are the subject).
            if (pronoun is null || (i > 0 && IsPreposition(i - 1)))
            {
                continue;
            }

            // M69 (M56's left point): a subject pronoun put after the verb of a question (¿dónde estás tú?) agrees with it (CheckInvertedSubjectPronoun).
            if (CheckInvertedSubjectPronoun(i, pronoun))
            {
                continue;
            }

            var verbIndex = NextContentIndex(i + 1);
            if (verbIndex < 0)
            {
                continue;
            }

            // M66: a subject pronoun right after a finite verb that agrees with it is that verb's subject, put after it as a question puts it (¿puede usted vender
            // manzanas?, ¿quiere usted comprar pan?, ¿pueden ustedes venir mañana?, ¿sabes tú dónde está?), so the word after the pronoun is not its verb; and an
            // infinitive after a pronoun that follows any finite verb belongs to that verb (¿puedes usted vender?: the slip is puedes, not vender). With no verb before
            // it the pronoun is still the subject of what follows (usted vender manzanas and yo comprar pan are person errors), and a finite verb after a pronoun that
            // follows a word of another person is checked as before (mira, yo quiere pan -> quiero).
            if (i > 0 && HasReading(i - 1, IsFiniteVerb)
                && (HasReading(i - 1, r => IsDeclarativeFiniteVerb(r) && r.Person == pronoun.Person && r.Number == pronoun.Number)
                    || !HasReading(verbIndex, IsDeclarativeFiniteVerb)))
            {
                continue;
            }

            // M64: a pronoun joined by y to the members before it ends a plural subject (Luz y yo fuimos), whose cells the verb agrees with; a coordination that cannot
            // be told is not checked (SpanishAgreementChecker.Coordination.cs).
            if (SubjectCells(i, pronoun) is not { } cells)
            {
                continue;
            }

            // M66 (M63): a question word between the pronoun and the verb leaves the pronoun the subject (tú qué quieres, ellas dónde están; ella qué quieres -> quiere).
            var question = QuestionWordBetween(i, verbIndex);
            if (question >= 0)
            {
                verbIndex = question == verbIndex ? NextContentIndex(question + 1) : verbIndex;
                if (verbIndex >= 0)
                {
                    CheckVerbAfterQuestionWord(question, verbIndex, cells);
                }

                continue;
            }

            var finite = Readings(verbIndex, IsDeclarativeFiniteVerb);
            if (finite.Count > 0)
            {
                _verbsWithPronounSubject.Add(verbIndex);
            }
            else
            {
                // A bare infinitive after a subject pronoun ("yo comprar") is a person error as well.
                var infinitive = FirstReading(verbIndex, static r => r.Pos == LexiconSchema.Verb && r.Person is null && r.Tense is null && r.Mood is null);
                if (infinitive is not null)
                {
                    AddPersonError(verbIndex, infinitive.Lemma, cells[0], LexiconSchema.Present);
                }

                continue;
            }

            if (finite.Exists(r => cells.Any(c => c.Person == r.Person && c.Number == r.Number)))
            {
                continue;
            }

            var representative = finite.FirstOrDefault(static r => r.Mood == LexiconSchema.Indicative) ?? finite[0];
            AddPersonError(verbIndex, representative.Lemma, cells[0], representative.Tense ?? LexiconSchema.Present);
        }
    }

    // Rule M69 (M56's left point, "the first and second persons after the verb"), "a subject pronoun after the verb of a question agrees with it". In a question or a
    // relative clause the subject goes after its verb (¿dónde estás tú?, ¿cómo está usted?, ¿qué quieres tú?, la casa donde vive él), so a subject pronoun right after a
    // finite verb that a question word or a relative opens (qué, dónde, adónde, cuándo, cómo, cuánto, quién, cuál, que, with or without the accent) is its subject, and a
    // verb of another person or number is the slip, reported with the pronoun's form: ¿dónde estás yo? -> estoy, ¿dónde está tú? -> estás, ¿cómo estás usted? -> está, ¿qué
    // quiere tú? -> quieres. Left clean: a pronoun that begins a clause of its own (¿qué quieres? yo te doy pan: a finite verb follows it), one joined to another by y, o or
    // ni (¿dónde están tú y Luz?), a pronoun before a noun or an adjective (tu as written: ¿cómo es tu casa?), a clitic between the question word and the verb (¿qué te
    // gusta?, whose subject is not the pronoun), haber, and a verb right after any other word (mira tú, the imperative; me gustas tú). Returns true when it reported.
    private bool CheckInvertedSubjectPronoun(int pronounIndex, LexicalFeatures pronoun)
    {
        var verbIndex = pronounIndex - 1;
        if (verbIndex < 1 || !SpanishAnalysisTables.IsClauseOpener(TextOf(verbIndex - 1)) || TextOf(verbIndex - 1) is "porque" or "si")
        {
            return false;
        }

        var readings = _tokens[verbIndex].Readings;
        var finite = Readings(verbIndex, static r => IsDeclarativeFiniteVerb(r) && r.Tense is not null);
        if (readings.Count == 0
            || !readings.All(IsFiniteVerb)
            || finite.Count == 0
            || finite.Exists(static r => r.Lemma == "haber")
            || finite.Exists(r => r.Person == pronoun.Person && r.Number == pronoun.Number))
        {
            return false;
        }

        var next = pronounIndex + 1;
        if (next < _tokens.Count
            && (TextOf(next) is "y" or "e" or "o" or "u" or "ni"
                || HasReading(next, static r => r.Pos is LexiconSchema.Noun or LexiconSchema.Adjective)
                || (NextContentIndex(next) is var after and >= 0 && HasReading(after, IsFiniteVerb))))
        {
            return false;
        }

        var representative = finite.FirstOrDefault(static r => r.Mood == LexiconSchema.Indicative) ?? finite[0];
        var expected = _morphology.Conjugate(representative.Lemma, pronoun.Person!, pronoun.Number!, representative.Tense!);
        if (string.IsNullOrEmpty(expected) || string.Equals(expected, TextOf(verbIndex), StringComparison.Ordinal))
        {
            return false;
        }

        Add(ErrorTags.ConjPerson, verbIndex, verbIndex, expected, TextOf(verbIndex));
        return true;
    }

    // M66 (M63): the index of a question word of IsQuestionWordAfterSubject after the pronoun at index, up to and including the word the pronoun's verb would be (the
    // adverbs dónde and cómo are skipped on the way to the verb; qué is that word), or -1.
    private int QuestionWordBetween(int pronounIndex, int verbIndex)
    {
        for (var k = pronounIndex + 1; k <= verbIndex; k++)
        {
            if (SpanishAnalysisTables.IsQuestionWordAfterSubject(TextOf(k)))
            {
                return k;
            }
        }

        return -1;
    }

    // Rule M66 (M63), "a subject pronoun before a question word is its verb's subject". In ellas dónde están, tú qué quieres, ella qué quiere and ellos adónde van the
    // pronoun put before the question is the subject of the verb after it (past at most two adverbs or clitics), which agrees with it as with a pronoun right before it:
    // ellas dónde está -> están, ella qué quieres -> quiere. Correct Spanish is never flagged, so two readings are left alone: a verb of the third person with a subject
    // after it (ellas, ¿dónde está la casa?: the pronoun said first is no subject), and a third person singular after qué, which may be the subject itself (ellos, ¿qué
    // pasa?). A word that is not only a finite verb, and a bare infinitive (yo qué hacer), are not checked.
    private void CheckVerbAfterQuestionWord(int questionIndex, int verbIndex, IReadOnlyList<(string Person, string Number)> cells)
    {
        var finite = Readings(verbIndex, IsDeclarativeFiniteVerb);
        if (finite.Count == 0
            || !_tokens[verbIndex].Readings.All(IsFiniteVerb)
            || finite.Exists(r => cells.Any(c => c.Person == r.Person && c.Number == r.Number))
            || (finite.Exists(static r => r.Person == "3") && HasThirdPersonSubjectAfter(verbIndex))
            || (TextOf(questionIndex) == "qué" && finite.Exists(static r => r is { Person: "3", Number: LexiconSchema.Singular })))
        {
            return;
        }

        var representative = finite.FirstOrDefault(static r => r.Mood == LexiconSchema.Indicative) ?? finite[0];
        AddPersonError(verbIndex, representative.Lemma, cells[0], representative.Tense ?? LexiconSchema.Present);
    }

    private void AddPersonError(int verbIndex, string lemma, (string Person, string Number) cell, string tense)
    {
        var expected = _morphology.Conjugate(lemma, cell.Person, cell.Number, tense) ?? string.Empty;
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
        var afterModal = FormsAfterModal();
        for (var i = 0; i < _tokens.Count; i++)
        {
            if (afterIrA.ContainsKey(i) || afterModal.ContainsKey(i))
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
    //     word that can be a noun is left alone. M59: a form that is also a preposition counts as the verb ("va a hace un hacha" -> "hacer"), since no preposition
    //     follows the a of "ir a".
    private void CheckNearFuture()
    {
        var afterIrA = FormsAfterIrA();
        foreach (var (next, verb) in afterIrA.OrderBy(static pair => pair.Key).Select(static pair => (pair.Key, pair.Value)))
        {
            Add(ErrorTags.ConjTense, next, next, verb.Lemma, TextOf(next));
        }

        // M66: a finite verb after a modal takes the same error, with the infinitive as the expected form (puedo pago -> pagar), so the learner meets one message.
        var afterModal = FormsAfterModal();
        foreach (var (next, verb) in afterModal.OrderBy(static pair => pair.Key).Select(static pair => (pair.Key, pair.Value)))
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

            if (afterIrA.ContainsKey(i) || afterModal.ContainsKey(i))
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
            if (readings.Count == 0
                || readings.Any(r => !IsDeclarativeFiniteVerb(r) && r.Pos is not (LexiconSchema.Adverb or LexiconSchema.Conjunction or LexiconSchema.Preposition)))
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

    // Rule M66, "a modal takes an infinitive". A finite verb of the indicative right after poder, querer, deber or necesitar, or after tener que, is the learner's
    // slip for the infinitive, which the error expects, as after ir a (puedo pago con fruta -> pagar, quiero vendo madera -> vender, tengo que voy al río -> ir, debo
    // como más fruta -> comer). Which word is the verb the reading resolver decides (a noun the modal may take stays the noun: quiero vino, necesito pago); here only a
    // word read as a finite verb alone counts. Left clean, as the start of a clause of its own that a comma would set off in writing: the modal again (quiero, quiero
    // pan), ser, estar, haber and tener (no puedo, estoy cansado; no quiero, tengo pan), and ir before a (no puedo, voy a dormir). A clause after que (quiero que
    // vengas) is no complement of this kind.
    private Dictionary<int, LexicalFeatures> FormsAfterModal()
    {
        var found = new Dictionary<int, LexicalFeatures>();
        for (var i = 0; i + 1 < _tokens.Count; i++)
        {
            var next = ModalComplementIndex(i, out var modal);
            if (next < 0)
            {
                continue;
            }

            var readings = _tokens[next].Readings;
            if (readings.Count == 0 || !readings.All(IsFiniteVerb))
            {
                continue;
            }

            var verb = readings.FirstOrDefault(static r => r.Mood == LexiconSchema.Indicative && r.Tense is not null);
            if (verb is null
                || verb.Lemma == modal
                || verb.Lemma is "ser" or "estar" or "haber" or "tener"
                || (verb.Lemma == "ir" && next + 1 < _tokens.Count && TextOf(next + 1) == "a"))
            {
                continue;
            }

            found[next] = verb;
        }

        return found;
    }

    // M66: the index of the word a modal at index governs, the next one (puedo pagar) or the one after its linking word (tengo que ir), with the modal's lemma; -1 when
    // the word at index is no finite verb whose readings of the indicative are all the modal's (podamos is poder's subjunctive but podar's indicative: los podamos
    // estudio is clean; podemos is poder's indicative, the modal).
    private int ModalComplementIndex(int index, out string modal)
    {
        modal = string.Empty;
        var readings = _tokens[index].Readings;
        var indicative = Readings(index, static r => r.Mood == LexiconSchema.Indicative);
        if (readings.Count == 0 || !readings.All(IsFiniteVerb) || indicative.Count == 0)
        {
            return -1;
        }

        var lemma = indicative[0].Lemma;
        if (!indicative.TrueForAll(r => r.Lemma == lemma))
        {
            return -1;
        }

        if (SpanishAnalysisTables.IsModalVerb(lemma))
        {
            modal = lemma;
            return index + 1;
        }

        if (index + 2 < _tokens.Count && SpanishAnalysisTables.IsModalWithLinker(lemma, TextOf(index + 1)))
        {
            modal = lemma;
            return index + 2;
        }

        return -1;
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

    /// <summary>
    /// The index of the first future-time marker (the time noun for a phrase), or -1: mañana (not "la mañana"), pasado mañana, próximo/a + a time noun, or a time noun
    /// + que viene. The words are those of <see cref="SpanishTimeMarkers"/>, which the fuzz suite's sentence generator models too.
    /// </summary>
    private int FutureTimeMarkerIndex()
    {
        for (var i = 0; i < _tokens.Count; i++)
        {
            if (IsFutureAdverb(i) && (i == 0 || !SpanishAnalysisTables.IsWordBeforeTheNounMañana(TextOf(i - 1))))
            {
                return i;
            }

            if (i > 0
                && FirstReading(i - 1, static r => r.Pos == LexiconSchema.Adjective && r.Lemma == SpanishTimeMarkers.UpcomingAdjective) is not null
                && FirstReading(i, static r => r.Pos == LexiconSchema.Noun && SpanishAnalysisTables.IsUpcomingTimeNoun(r.Lemma)) is not null)
            {
                return i;
            }

            if (i > 0
                && FirstReading(i - 1, static r => r.Pos == LexiconSchema.Noun && SpanishAnalysisTables.IsTimeNoun(r.Lemma)) is not null
                && i + 1 < _tokens.Count
                && string.Equals(TextOf(i), SpanishTimeMarkers.UpcomingClause[0], StringComparison.Ordinal)
                && string.Equals(TextOf(i + 1), SpanishTimeMarkers.UpcomingClause[1], StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// True when the sentence has a past-time adverb (ayer, anoche, anteayer) or a time noun followed by a form of <c>pasado</c> (la semana pasada). M57: the form is
    /// matched as written, because the charitable fallback of the reading resolver read <c>pasada</c> as the noun (una pasada) whenever the adjective showed a slip,
    /// so <c>compro pan la semana pasada</c> was clean; and the <c>pasado</c> of <c>pasado mañana</c> is no past marker (voy a ir el domingo pasado mañana).
    /// </summary>
    public bool HasPastTimeMarker()
    {
        for (var i = 0; i < _tokens.Count; i++)
        {
            if (FirstReading(i, static r => r.Pos == LexiconSchema.Adverb && SpanishAnalysisTables.IsPastTimeAdverb(r.Lemma)) is not null)
            {
                return true;
            }

            if (i > 0
                && SpanishAnalysisTables.IsPastTimeAdjectiveForm(TextOf(i))
                && !IsDayAfterTomorrow(i)
                && FirstReading(i - 1, static r => r.Pos == LexiconSchema.Noun && SpanishAnalysisTables.IsTimeNoun(r.Lemma)) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsFutureAdverb(int index)
    {
        return FirstReading(index, static r => r.Pos == LexiconSchema.Adverb && r.Lemma == SpanishTimeMarkers.FutureAdverb) is not null;
    }

    // M57: the pasado of "pasado mañana" (the day after tomorrow), one time phrase wherever it stands. It is no adjective of the noun before it (compro panes pasado
    // mañana gave noun.number, expected pasados) and no past marker after a time noun.
    private bool IsDayAfterTomorrow(int index)
    {
        return index + 1 < _tokens.Count
            && string.Equals(TextOf(index), SpanishTimeMarkers.DayAfterTomorrow[0], StringComparison.Ordinal)
            && IsFutureAdverb(index + 1);
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

            var takesEstar = adjectives.Exists(static a => SpanishAnalysisTables.TakesEstar(a.Lemma));
            var takesSer = adjectives.Exists(static a => SpanishAnalysisTables.TakesSer(a.Lemma));
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
        if (IsDayAfterTomorrow(index))
        {
            return false;
        }

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
        while (cursor < count && IsAdjunct(cursor) && !OpensFixedPluralExpression(cursor))
        {
            after.Add(cursor);
            cursor++;
        }

        phrase = new NounPhrase(determiners, before, head, after);
        return true;
    }

    // M58: an adjective that makes a fixed plural expression with the noun after it (buenos días, buenas noches) opens that expression and is no adjective of the noun
    // before it: in hola Luz buenos días (a name said to the listener, then the greeting) buenos is not luz's, which would make it buena.
    private bool OpensFixedPluralExpression(int index)
    {
        return index + 1 < _tokens.Count
            && Readings(index, static r => r.Pos == LexiconSchema.Adjective)
                .Exists(a => Readings(index + 1, static r => r.Pos == LexiconSchema.Noun).Exists(n => SpanishAnalysisTables.IsFixedPluralExpression(a.Lemma, n.Lemma)));
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
            var numbered = FirstReading(determiner, r => IsDeterminerReading(r) && r.Number is not null);
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
                head = nounReadings.Find(n => SpanishAnalysisTables.IsFixedPluralExpression(adjective.Lemma, n.Lemma));
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
        if (adjectives.Count == 0 || adjectives.Exists(static a => SpanishAnalysisTables.IsPrenominal(a.Lemma)))
        {
            return;
        }

        // "la próxima semana", "el próximo año": próximo goes before a time noun (but "unos próximos salones" is still a word-order mistake).
        if (adjectives.Exists(static a => a.Lemma == SpanishTimeMarkers.UpcomingAdjective) && Readings(phrase.Head, static r => r.Pos == LexiconSchema.Noun && SpanishAnalysisTables.IsUpcomingTimeNoun(r.Lemma)).Count > 0)
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
