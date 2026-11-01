namespace Vocaterra.Nlp.Es.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// The rules of M64 (the review of <c>feature/village</c>, R1 and R5): a subject pronoun joined by <c>y</c> to a phrase before it is the last member of a plural subject
/// (<c>conj.person</c>, and the predicate adjective's number and gender), and an amount before a singular <c>ser</c> is read as one sum.
/// </summary>
internal sealed partial class SpanishAgreementChecker
{
    private static readonly (string Person, string Number)[] FirstPersonPlural = [("1", LexiconSchema.Plural)];

    // The plural of a subject with a member of the second person and none of the first: ustedes (the third person plural) where vosotros is not used, which is all of
    // Latin America, and vosotros (the second) in Spain. Both are right; a correction proposes the first.
    private static readonly (string Person, string Number)[] SecondPersonPlural = [("3", LexiconSchema.Plural), ("2", LexiconSchema.Plural)];

    private static readonly (string Person, string Number)[] ThirdPersonPlural = [("3", LexiconSchema.Plural)];

    // What stands before a subject pronoun: nothing joined to it (None), a coordinated subject that it ends (Subject), or a coordination that cannot be told (Unknown).
    private enum Coordination
    {
        None,
        Subject,
        Unknown,
    }

    // The subject a subject pronoun ends when y or e joins it to the members before it: the person of its verb ("1" when a member is of the first person, else "2" when
    // one is of the second, else "3"; always plural) and its gender, known only when every member has one (ella y él: masculine; ella y ella: feminine; yo, tú, usted, a
    // name: none).
    //
    // M66: with o (or u) the verb may also agree with the member nearest it, the pronoun itself (tú o yo vamos, tú o yo voy), so Nearest holds that member's cell; with y
    // and ni it is null.
    private readonly record struct CoordinatedSubject(string Person, string? Gender, (string Person, string Number)? Nearest = null)
    {
        // The plural cells of a verb that agrees with the subject by the person rule of y.
        public IReadOnlyList<(string Person, string Number)> PluralCells => Person switch
        {
            "1" => FirstPersonPlural,
            "2" => SecondPersonPlural,
            _ => ThirdPersonPlural,
        };

        // The person and number cells of a verb that agrees with the subject, the plural ones and with o the nearest member's; a correction proposes the first.
        public IReadOnlyList<(string Person, string Number)> Cells =>
            Nearest is { } nearest && !PluralCells.Contains(nearest) ? [.. PluralCells, nearest] : PluralCells;
    }

    // A member of a coordination before y or e: where it starts, its person and gender, and whether it is surely a member (a pronoun or a noun phrase and nothing else).
    private readonly record struct CoordinationMember(int Start, string Person, string? Gender, bool Certain);

    // Rule M64 (a), "a subject pronoun joined by y is part of a plural subject". A subject pronoun after y or e and a member before it (a subject pronoun, a noun phrase, a
    // name the lexicon lacks) ends one subject when the first member opens its clause, as M59 reads a phrase that opens its clause: Luz y yo fuimos, mis amigos y yo fuimos,
    // tú y yo somos, usted y yo vamos, ella y él son. Its verb takes the first person plural when a member is of the first person; else the second or the third person
    // plural when one is of the second (tú y ella sois, tú y ella son); else the third person plural. A phrase with de after it is the member with it (el hermano de Luz y
    // yo fuimos). y also joins two clauses, and then the pronoun after it is the subject of its own clause: a member right after a word that can be a verb or a
    // preposition is in the clause before (luz hizo pan y yo fui, comí pan y tú compraste fruta, fui con Luz y yo comí), and a word before y that is no member ends the
    // clause before (mar pescó y yo comí pescado: None). A member that neither opens its clause nor follows a verb or a preposition (a list said without its commas, luz
    // mar y yo) and one that may be something else (a phrase of time, a bare noun that is also an adverb or an interjection, a pronoun that is also another word) cannot
    // be told: Unknown, and the verb is not checked.
    //
    // M66: ni and o (u) join a subject as y does. ni ... ni takes the person rule of y (ni tú ni yo fuimos, ni ella ni yo somos altas); o takes it too and also the
    // form of the member nearest the verb, the pronoun (tú o yo vamos, tú o yo voy, tú o ella son, tú o ella eres). Both may say their word before the first member as
    // well (ni tú ni yo, o tú o yo), and then that word is where the clause has to open. A coordination that mixes them (tú y ella o yo) cannot be told.
    private Coordination CoordinationOf(int pronounIndex, LexicalFeatures pronoun, out CoordinatedSubject subject)
    {
        subject = default;
        var persons = new List<string> { pronoun.Person! };
        var genders = new List<string?> { pronoun.Gender };
        var start = pronounIndex;
        string? kind = null;
        while (start >= 2 && CoordinatorKind(TextOf(start - 1)) is { } coordinator)
        {
            if (kind is not null && kind != coordinator)
            {
                return Coordination.Unknown;
            }

            kind = coordinator;
            if (!TryReadMemberEndingAt(start - 2, out var member) || FollowsVerbOrPreposition(member.Start))
            {
                break;
            }

            if (!member.Certain)
            {
                return Coordination.Unknown;
            }

            persons.Add(member.Person);
            genders.Add(member.Gender);
            start = member.Start;
        }

        if (persons.Count == 1)
        {
            return Coordination.None;
        }

        // The first member opens the clause (a member after y always does, y being an opener). The first member of ni ... ni (o ... o) may have the word before it
        // too, and the clause then opens before that word.
        var opening = start;
        if (kind is "ni" or "o" && opening > 0 && CoordinatorKind(TextOf(opening - 1)) == kind)
        {
            opening--;
        }

        if (!IsClauseStart(opening))
        {
            return Coordination.Unknown;
        }

        var person = persons.Contains("1") ? "1" : persons.Contains("2") ? "2" : "3";
        var gender = genders.Exists(static g => g is null) ? null : genders.Contains(LexiconSchema.Masculine) ? LexiconSchema.Masculine : LexiconSchema.Feminine;
        subject = new CoordinatedSubject(person, gender, kind == "o" ? (pronoun.Person!, pronoun.Number!) : null);
        return Coordination.Subject;
    }

    // M66: the kind of the word that joins two members of a subject: y (y, e), o (o, u) or ni; null for any other word.
    private static string? CoordinatorKind(string word)
    {
        return word switch
        {
            "y" or "e" => "y",
            "o" or "u" => "o",
            "ni" => "ni",
            _ => null,
        };
    }

    // The member of a coordination that ends at index: a word the lexicon lacks (a name), a subject pronoun or the noun phrase that ends there, with the phrases that
    // de joins it to (el hermano de Luz, la hija del panadero: the member is the first phrase, of the third person).
    private bool TryReadMemberEndingAt(int end, out CoordinationMember member)
    {
        member = default;
        var readings = _tokens[end].Readings;
        var pronoun = FirstReading(end, static r => r.Pos == LexiconSchema.Pronoun && r.Person is not null && WordClasses.IsSubjectPronoun(r.Lemma));
        if (readings.Count == 0)
        {
            member = new CoordinationMember(end, "3", null, Certain: true);
        }
        else if (pronoun is not null)
        {
            member = new CoordinationMember(end, pronoun.Person!, pronoun.Gender, Certain: readings.All(static r => r.Pos == LexiconSchema.Pronoun));
        }
        else if (TryFindPhraseEndingAt(end, out var phrase))
        {
            member = PhraseMember(phrase);
        }
        else
        {
            return false;
        }

        while (member.Start >= 2 && TextOf(member.Start - 1) is "de" or "del" && TryFindPhraseEndingAt(member.Start - 2, out var owner))
        {
            member = PhraseMember(owner);
        }

        return true;
    }

    // A noun phrase as a member: of the third person, with its gender unless it is one bare noun (a name gives none), and surely a member when it can be a subject and
    // is not one bare noun that is also another word (bien, an adverb and a noun).
    private CoordinationMember PhraseMember(NounPhrase phrase)
    {
        var bare = phrase.Determiners.Count == 0 && phrase.Before.Count == 0 && phrase.After.Count == 0;
        var certain = IsSubjectCandidate(phrase) && !(bare && _tokens[phrase.Head].Readings.Any(static r => r.Pos != LexiconSchema.Noun));
        return new CoordinationMember(StartOf(phrase), "3", bare ? null : PhraseGender(phrase), certain);
    }

    // The person and number cells a verb after the subject pronoun at index agrees with: the pronoun's own, or those of the coordinated subject it ends; null when the
    // coordination cannot be told (CoordinationOf).
    private IReadOnlyList<(string Person, string Number)>? SubjectCells(int pronounIndex, LexicalFeatures pronoun)
    {
        return CoordinationOf(pronounIndex, pronoun, out var coordinated) switch
        {
            Coordination.Subject => coordinated.Cells,
            Coordination.Unknown => null,
            _ => [(pronoun.Person!, pronoun.Number!)],
        };
    }

    // Rule M64 (b), "an amount before a singular ser is one sum". A noun phrase whose determiners are cardinal numbers of the plural (dos panes, diez monedas) before a
    // singular form of ser with a singular predicate after it is read as one sum, which takes the singular: diez monedas es mucho dinero, dos panes es suficiente, dos
    // panes es poco, quince monedas es mucho, dos panes no es nada. son is as right (dos panes son suficientes). The predicate is a word of
    // SpanishAnalysisTables.AmountPredicates (mucho, poco, suficiente, caro...), past at most two adverbs, or a noun phrase of the singular with a determiner (mucho dinero,
    // un buen precio). Still flagged: a phrase with no number (las manzanas es roja), another verb (dos niños come pan), a predicate of the plural (dos panes es
    // suficientes) and one that says no amount (dos manzanas es roja).
    private bool IsAmountReadAsUnit(int verb, LexicalFeatures copula, PreverbalSubject subject)
    {
        if (copula is not { Lemma: "ser", Number: LexiconSchema.Singular }
            || subject.Number != LexiconSchema.Plural
            || subject.Phrases.Count != 1
            || subject.Phrases[0].Determiners is not { Count: > 0 } determiners
            || !determiners.TrueForAll(d => _tokens[d].Readings.Count > 0
                && _tokens[d].Readings.All(static r => r is { Pos: LexiconSchema.Numeral, Number: LexiconSchema.Plural })))
        {
            return false;
        }

        var index = verb + 1;
        for (var skipped = 0; index < _tokens.Count; skipped++, index++)
        {
            if (SpanishAnalysisTables.IsAmountPredicate(TextOf(index)))
            {
                return true;
            }

            if (TryReadNounPhrase(index, out var predicate))
            {
                return predicate.Determiners.Count > 0 && SubjectNumber(predicate, timeIsSubject: true) == LexiconSchema.Singular;
            }

            if (skipped >= ReadingClasses.MaxSkippedBetween || !IsSkippable(index))
            {
                return false;
            }
        }

        return false;
    }
}
