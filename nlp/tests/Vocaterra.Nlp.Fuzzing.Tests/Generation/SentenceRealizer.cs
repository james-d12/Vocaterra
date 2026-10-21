using System.Globalization;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>
/// Realises a grammar pattern as a sentence that is correct by construction: subject pronouns agree with their verbs, articles and
/// numerals agree with nouns in gender and number, adjectives agree and sit on the right side of the noun, and no past-time marker
/// appears next to a present-tense verb. The realiser mirrors the analyzer's noun-phrase and subject-verb scans on purpose: it is the
/// model the analyzer is compared with. All randomness comes from a <see cref="SeededRng"/>, so a CsCheck seed replays a sentence.
/// </summary>
internal sealed class SentenceRealizer
{
    private const int MaxAttempts = 8;
    private const int MaxSkippedBetween = 2;

    private static readonly string[] PostverbalSubjectVerbs = ["costar", "gustar"];

    private readonly LexiconCatalog _words;
    private readonly SeededRng _rng;
    private readonly SentenceOptions _options;
    private readonly List<Item> _items = [];
    private string _detail = string.Empty;
    private string _tense = LexiconSchema.Present;

    public SentenceRealizer(LexiconCatalog words, SeededRng rng, SentenceOptions options)
    {
        _words = words;
        _rng = rng;
        _options = options;
    }

    private enum Kind
    {
        Literal,
        Article,
        Determiner,
        Pronoun,
        Numeral,
        Noun,
        Adjective,
        Verb,
        Adverb,
        Preposition,
        Conjunction,
        Interjection,

        // A name ([WORD]): a word of letters with no readings the checks use, so it breaks the phrases and links around it as an unknown word does.
        Word,
    }

    /// <summary>Tries up to a handful of times to realise the pattern; returns null with the reason of the last failure when none works.</summary>
    public GeneratedSentence? TryRealize(GrammarPattern pattern, string source, out string skipReason)
    {
        skipReason = "none";
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var sentence = Attempt(pattern, source, out var reason);
            if (sentence is not null)
            {
                return sentence;
            }

            skipReason = reason;
        }

        return null;
    }

    private static string Digits(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsDeterminerAdjective(LexicalFeatures reading)
    {
        return reading.Pos == LexiconSchema.Adjective && LexiconCatalog.DeterminerAdjectiveLemmas.Contains(reading.Lemma);
    }

    private GeneratedSentence? Attempt(GrammarPattern pattern, string source, out string skip)
    {
        _items.Clear();
        _detail = string.Empty;
        _tense = _rng.NextDouble() < _options.PreteriteProbability ? LexiconSchema.Preterite : LexiconSchema.Present;
        ExpandSequence(pattern.Nodes);
        if (_items.Count == 0)
        {
            skip = "empty";
            return null;
        }

        foreach (var item in _items)
        {
            if (item.Kind == Kind.Literal && item.Readings.Count == 0)
            {
                skip = "unknown-literal";
                _detail = item.Surface!;
                return null;
            }
        }

        AddImplicitElements();
        ChoosePronouns();
        if (!ChooseVerbLemmas(out skip) || !ChooseNouns(out skip) || !AssignNounPhrases(out skip) || !FinishVerbs(out skip)
            || !FinishStandaloneItems(out skip) || !CheckVerbForms(out skip))
        {
            return null;
        }

        var sentence = Build(pattern, source);
        skip = "none";
        return sentence;
    }

    // ---- Expansion ------------------------------------------------------------------------------------------------------------------

    private void ExpandSequence(IReadOnlyList<PatternNode> nodes)
    {
        foreach (var node in nodes)
        {
            ExpandNode(node);
        }
    }

    private void ExpandNode(PatternNode node)
    {
        switch (node)
        {
            case LiteralPattern literal:
                _items.Add(Literal(literal.Word));
                break;
            case TypedPattern typed:
                _items.Add(FromTyped(typed));
                break;
            case WordPattern word:
                _items.Add(FromWord(word));
                break;
            case OptionalPattern optional:
                if (_rng.NextDouble() < _options.OptionalProbability)
                {
                    ExpandNode(optional.Inner);
                }

                break;
            case GroupPattern group:
                ExpandSequence(_rng.Pick(group.Alternatives));
                break;
            default:
                throw new InvalidOperationException($"Unexpected pattern node {node.GetType().Name}.");
        }
    }

    private Item Literal(string word)
    {
        var item = new Item(Kind.Literal) { Surface = word, Norm = word };
        item.Readings.AddRange(_words.Lexicon.Lookup(word));
        return item;
    }

    private Item FromTyped(TypedPattern typed)
    {
        var kind = typed.Tag switch
        {
            "ART" => Kind.Article,
            "DET" => _rng.NextDouble() < 0.5 ? Kind.Article : Kind.Determiner,
            "PRON" => Kind.Pronoun,
            "NUM" => Kind.Numeral,
            "NOUN" => Kind.Noun,
            "ADJ" => Kind.Adjective,
            "VERB" => Kind.Verb,
            "ADV" => Kind.Adverb,
            "PREP" => Kind.Preposition,
            "CONJ" => Kind.Conjunction,
            "INTERJ" => Kind.Interjection,
            _ => throw new InvalidOperationException($"Unknown tag {typed.Tag}."),
        };
        var item = new Item(kind) { Slot = typed.Slot, PatternLemma = typed.Lemma, PatternTag = typed.Tag };
        switch (kind)
        {
            case Kind.Adverb:
                SetWord(item, _rng.Pick(_words.Adverbs));
                break;
            case Kind.Preposition:
                SetWord(item, _rng.Pick(_words.Prepositions));
                break;
            case Kind.Conjunction:
                SetWord(item, _rng.Pick(_words.Conjunctions));
                break;
            case Kind.Interjection:
                SetWord(item, _rng.Pick(_words.Interjections));
                break;
            default:
                break;
        }

        return item;
    }

    // A name is drawn from the seeded generator: two or three invented syllables, written with a capital and normalized in lower case.
    // A name whose spelling is a closed-class word of the lexicon once its accents are ignored (nadá folds to nada) cannot be bound by [WORD] (M19f), so another is drawn.
    private Item FromWord(WordPattern word)
    {
        string norm;
        var attempts = 0;
        do
        {
            norm = DrawName();
        }
        while (++attempts < MaxAttempts && IsClosedClassOnly(norm));

        var surface = char.ToUpperInvariant(norm[0]) + norm[1..];
        return new Item(Kind.Word) { Slot = word.Slot, PatternTag = "WORD", Surface = surface, Norm = norm };
    }

    private string DrawName()
    {
        var syllables = 2 + _rng.NextInt(2);
        var parts = new List<string>(syllables * 2);
        for (var i = 0; i < syllables; i++)
        {
            parts.Add(_rng.Pick(NameOnsets));
            parts.Add(_rng.Pick(NameNuclei));
        }

        return string.Concat(parts);
    }

    private bool IsClosedClassOnly(string form)
    {
        var readings = _words.Lexicon.Lookup(form);
        return readings.Count > 0
            && readings.All(static r => r.Pos is LexiconSchema.Article or LexiconSchema.Numeral or LexiconSchema.Pronoun or LexiconSchema.Preposition or LexiconSchema.Conjunction or LexiconSchema.Adverb or LexiconSchema.Interjection
                || IsDeterminerAdjective(r));
    }

    private static readonly string[] NameOnsets = ["b", "d", "j", "l", "m", "n", "r", "s", "t", "v", "z", "ñ"];

    private static readonly string[] NameNuclei = ["a", "e", "i", "o", "u", "á", "é"];

    private void SetWord(Item item, string word)
    {
        item.Surface = word;
        item.Norm = word;
        item.Readings.AddRange(_words.Lexicon.Lookup(word));
    }

    private void AddImplicitElements()
    {
        if (_items[0].Kind == Kind.Verb && _rng.NextDouble() < _options.SubjectProbability)
        {
            _items.Insert(0, new Item(Kind.Pronoun) { Implicit = true });
        }

        if (_rng.NextDouble() < _options.PoliteProbability && _words.Lexicon.Lookup("favor").Count > 0 && _words.Lexicon.Lookup("por").Count > 0)
        {
            _items.Add(Literal("por"));
            _items.Add(Literal("favor"));
        }
    }

    // ---- Classification (mirrors the analyzer) --------------------------------------------------------------------------------------

    private bool IsFiniteVerbReading(LexicalFeatures reading)
    {
        return reading.Pos == LexiconSchema.Verb && reading.Person is not null;
    }

    private bool IsSkippable(Item item)
    {
        switch (item.Kind)
        {
            case Kind.Adverb:
            case Kind.Interjection:
                return true;
            case Kind.Pronoun:
                return item.Surface is not null && !item.SubjectPronoun;
            case Kind.Literal:
                return item.Readings.Count > 0
                    && (item.Readings.All(static r => r.Pos is LexiconSchema.Adverb or LexiconSchema.Interjection)
                        || item.Readings.All(r => r.Pos == LexiconSchema.Pronoun && LexiconCatalog.CliticLemmas.Contains(r.Lemma)));
            default:
                return false;
        }
    }

    private int NextContent(int from)
    {
        var index = from + 1;
        var skipped = 0;
        while (index < _items.Count && skipped < MaxSkippedBetween && IsSkippable(_items[index]))
        {
            index++;
            skipped++;
        }

        return index < _items.Count ? index : -1;
    }

    private Item? PreviousContent(int from)
    {
        for (var index = from - 1; index >= 0; index--)
        {
            if (!IsSkippable(_items[index]))
            {
                return _items[index];
            }
        }

        return null;
    }

    private bool IsFiniteVerbItem(Item item)
    {
        return item.Kind switch
        {
            Kind.Verb => !item.Infinitive,
            Kind.Literal => item.Readings.Any(IsFiniteVerbReading),
            _ => false,
        };
    }

    private bool IsDeterminer(Item item)
    {
        return item.Kind switch
        {
            Kind.Article or Kind.Determiner or Kind.Numeral => true,
            Kind.Literal => item.Readings.Any(r => r.Pos is LexiconSchema.Article or LexiconSchema.Numeral || IsDeterminerAdjective(r)),
            _ => false,
        };
    }

    private bool IsAdjunct(Item item)
    {
        return item.Kind switch
        {
            Kind.Adjective => true,
            Kind.Literal => item.Readings.Any(r => r.Pos == LexiconSchema.Adjective && !IsDeterminerAdjective(r))
                && !item.Readings.Any(r => r.Pos == LexiconSchema.Noun || IsFiniteVerbReading(r)),
            _ => false,
        };
    }

    private bool IsNounHead(Item item, bool hasContext)
    {
        return item.Kind switch
        {
            Kind.Noun => true,
            Kind.Literal => item.Readings.Any(static r => r.Pos == LexiconSchema.Noun) && (hasContext || !item.Readings.Any(IsFiniteVerbReading)),
            _ => false,
        };
    }

    // ---- Pronouns and verbs ---------------------------------------------------------------------------------------------------------

    private void ChoosePronouns()
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            if (item.Kind != Kind.Pronoun)
            {
                continue;
            }

            var target = NextContent(i);
            var next = target >= 0 ? _items[target] : null;
            var generatedVerb = next is { Kind: Kind.Verb };
            var literalVerb = next is { Kind: Kind.Literal } && next.Readings.Any(IsFiniteVerbReading);

            bool subject;
            if (item.Implicit)
            {
                subject = true;
            }
            else if (generatedVerb)
            {
                subject = _rng.NextDouble() < 0.8;
            }
            else if (literalVerb)
            {
                subject = true;
            }
            else
            {
                subject = _rng.NextDouble() < 0.4;
            }

            IReadOnlyList<string> pool = subject ? _words.SubjectPronouns : _words.Clitics;
            if (literalVerb && subject)
            {
                var cells = next!.Readings.Where(r => IsFiniteVerbReading(r) && r.Mood != LexiconSchema.Imperative).Select(static r => (r.Person, r.Number)).ToHashSet();
                var matching = _words.SubjectPronouns.Where(p => cells.Contains(PersonNumber(p))).ToArray();
                if (matching.Length == 0)
                {
                    subject = false;
                    pool = _words.Clitics;
                }
                else
                {
                    pool = matching;
                }
            }

            // The word after a subject pronoun or a clitic is the verb, for a reader and for the analyzer alike, so a noun there is not one that can also be a verb.
            if (!generatedVerb && !literalVerb && next is { Kind: Kind.Noun })
            {
                next.ForbidHomograph = true;
            }

            if (subject && !generatedVerb && !literalVerb && next is { Kind: Kind.Literal } && next.Readings.Any(static r => r.Pos == LexiconSchema.Verb))
            {
                subject = false;
                pool = _words.Clitics;
            }

            var lemma = _rng.Pick(pool);
            SetWord(item, lemma);
            item.SubjectPronoun = subject;
            var reading = _words.FirstReading(lemma, LexiconSchema.Pronoun)!;
            item.Lemma = reading.Lemma;
            item.Person = reading.Person;
            item.Number = reading.Number;
            item.Gender = reading.Gender;
            if (subject && generatedVerb)
            {
                next!.Subject = item;
            }
        }
    }

    private (string?, string?) PersonNumber(string pronoun)
    {
        var reading = _words.FirstReading(pronoun, LexiconSchema.Pronoun)!;
        return (reading.Person, reading.Number);
    }

    private bool ChooseVerbLemmas(out string skip)
    {
        skip = "none";
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.Kind != Kind.Verb)
            {
                continue;
            }

            VerbLexeme? lexeme;
            if (item.PatternLemma is { } wanted)
            {
                var folded = Accents.Strip(wanted);
                lexeme = _words.VerbOfIgnoringAccents(folded);
                if (lexeme is null)
                {
                    skip = "verb-lemma-not-in-catalog";
                    _detail = wanted;
                    return false;
                }
            }
            else
            {
                lexeme = _rng.Pick(_words.Verbs);
            }

            item.Lemma = lexeme.Lemma;
            var previous = PreviousContent(i);
            item.Infinitive = previous is not null && IsFiniteVerbItem(previous);
        }

        return true;
    }

    private bool ChooseNouns(out string skip)
    {
        skip = "none";
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.Kind != Kind.Noun)
            {
                continue;
            }

            // Every noun may be drawn, whether or not it is also a verb form or a noun of the other gender: the analyzer resolves the part of speech of each word
            // (M18). The one place a verb form is not wanted is right behind a subject pronoun, where a noun is not what a reader (or the analyzer) expects.
            item.Noun = _rng.Pick(item.ForbidHomograph ? _words.PlainNouns : _words.Nouns);
        }

        return true;
    }

    // ---- Noun phrases ---------------------------------------------------------------------------------------------------------------

    private bool AssignNounPhrases(out string skip)
    {
        skip = "none";
        var index = 0;
        while (index < _items.Count)
        {
            if (!TryReadNounPhrase(index, out var phrase))
            {
                index++;
                continue;
            }

            if (!AssignNounPhrase(phrase, out skip))
            {
                return false;
            }

            index = phrase.Last + 1;
        }

        return true;
    }

    private bool TryReadNounPhrase(int start, out NounPhraseItems phrase)
    {
        var cursor = start;
        var determiners = new List<int>();
        while (cursor < _items.Count && IsDeterminer(_items[cursor]))
        {
            determiners.Add(cursor);
            cursor++;
        }

        var before = new List<int>();
        while (cursor < _items.Count && IsAdjunct(_items[cursor]))
        {
            before.Add(cursor);
            cursor++;
        }

        // A preposition does not govern a finite verb, so a literal that is a noun and a verb form right behind one is the noun (de meta; seed 8OeIOEx_Hfm3 failed O3 on main before M26b).
        var behindPreposition = start > 0
            && (_items[start - 1].Kind == Kind.Preposition
                || (_items[start - 1].Kind == Kind.Literal && _items[start - 1].Readings.Count > 0 && _items[start - 1].Readings.All(static r => r.Pos == LexiconSchema.Preposition)));
        if (cursor >= _items.Count || !IsNounHead(_items[cursor], determiners.Count + before.Count > 0 || behindPreposition))
        {
            phrase = default!;
            return false;
        }

        var head = cursor;
        cursor++;
        var after = new List<int>();
        while (cursor < _items.Count && IsAdjunct(_items[cursor]))
        {
            after.Add(cursor);
            cursor++;
        }

        phrase = new NounPhraseItems(determiners, before, head, after);
        return true;
    }

    // A grammar such as (buenos|buenas) (días|tardes|noches) lists its alternatives independently, so some combinations disagree in gender.
    // When every word of a phrase is a literal the realiser cannot repair that; it rejects the draw and the next attempt picks again.
    private bool LiteralPhraseAgrees(NounPhraseItems phrase)
    {
        var nounReadings = _items[phrase.Head].Readings.Where(static r => r.Pos == LexiconSchema.Noun).ToArray();
        foreach (var index in phrase.Determiners.Concat(phrase.Before).Concat(phrase.After))
        {
            var readings = _items[index].Readings.Where(r => r.Pos is LexiconSchema.Article or LexiconSchema.Numeral or LexiconSchema.Adjective).ToArray();
            var genderOk = readings.Any(d => nounReadings.Any(n => d.Gender is null || n.Gender is null || d.Gender == n.Gender));
            var numberOk = readings.Any(d => nounReadings.Any(n => d.Number is null || n.Number is null || d.Number == n.Number));
            if (!genderOk || !numberOk)
            {
                return false;
            }
        }

        return true;
    }

    private bool AssignNounPhrase(NounPhraseItems phrase, out string skip)
    {
        skip = "none";
        if (phrase.Determiners.Concat(phrase.Before).Concat(phrase.After).Append(phrase.Head).All(i => _items[i].Kind == Kind.Literal)
            && !LiteralPhraseAgrees(phrase))
        {
            skip = "literal-disagreement";
            return false;
        }

        var head = _items[phrase.Head];
        string gender;
        string? number = null;
        string lemma;
        if (head.Kind == Kind.Noun)
        {
            gender = head.Noun!.Gender;
            lemma = head.Noun.Lemma;
        }
        else
        {
            var reading = head.Readings.First(static r => r.Pos == LexiconSchema.Noun);
            gender = reading.Gender ?? LexiconSchema.Masculine;
            number = reading.Number;
            lemma = reading.Lemma;
        }

        // A literal determiner already fixes the number of the phrase.
        foreach (var determiner in phrase.Determiners.Select(i => _items[i]).Where(static d => d.Kind == Kind.Literal))
        {
            var fixedNumber = determiner.Readings.FirstOrDefault(r => r.Number is not null && (r.Pos is LexiconSchema.Article or LexiconSchema.Numeral || IsDeterminerAdjective(r)))?.Number;
            number ??= fixedNumber;
        }

        var numerals = phrase.Determiners.Select(i => _items[i]).Where(static d => d.Kind == Kind.Numeral).ToList();
        var hasArticle = phrase.Determiners.Select(i => _items[i]).Any(static d => d.Kind == Kind.Article);
        int? numeralValue = null;
        if (numerals.Count > 0)
        {
            if (number == LexiconSchema.Singular)
            {
                numeralValue = 1;
            }
            else if (number == LexiconSchema.Plural)
            {
                numeralValue = PickNumeralValue(allowOne: false);
            }
            else
            {
                numeralValue = PickNumeralValue(allowOne: !hasArticle);
                number = numeralValue == 1 ? LexiconSchema.Singular : LexiconSchema.Plural;
            }
        }

        number ??= _rng.NextDouble() < 0.6 ? LexiconSchema.Singular : LexiconSchema.Plural;
        if (head.Kind == Kind.Noun)
        {
            SetSurface(head, number == LexiconSchema.Singular ? head.Noun!.Singular : head.Noun!.Plural);
        }

        head.Lemma = lemma;
        head.Gender = gender;
        head.Number = number;
        MarkInPhrase(head, head, number);

        foreach (var index in phrase.Determiners)
        {
            var determiner = _items[index];
            MarkInPhrase(determiner, head, number);
            switch (determiner.Kind)
            {
                case Kind.Article:
                    SetSurface(determiner, _words.Morphology.Article(
                        _rng.NextDouble() < 0.6 ? Definiteness.Definite : Definiteness.Indefinite, gender, number, lemma));
                    break;
                case Kind.Determiner:
                    {
                        var forms = _words.DeterminerAdjectiveForms(gender, number);
                        if (forms.Count == 0)
                        {
                            skip = "no-determiner-form";
                            return false;
                        }

                        SetSurface(determiner, _rng.Pick(forms));
                        break;
                    }

                case Kind.Numeral:
                    SetNumeral(determiner, numeralValue ?? 1);
                    break;
                default:
                    break;
            }
        }

        foreach (var index in phrase.Before)
        {
            var adjunct = _items[index];
            MarkInPhrase(adjunct, head, number);
            if (adjunct.Kind == Kind.Adjective && !SetAdjective(adjunct, LexiconCatalog.PrenominalLemmas, gender, number, out skip))
            {
                return false;
            }
        }

        foreach (var index in phrase.After)
        {
            var adjunct = _items[index];
            MarkInPhrase(adjunct, head, number);
            if (adjunct.Kind == Kind.Adjective && !SetAdjective(adjunct, _words.DescriptiveAdjectives, gender, number, out skip))
            {
                return false;
            }
        }

        return true;
    }

    private void MarkInPhrase(Item item, Item head, string number)
    {
        item.Head = head;
        item.PhraseNumber = number;
    }

    private int PickNumeralValue(bool allowOne)
    {
        var roll = _rng.NextDouble();
        if (allowOne && roll < _options.OneProbability)
        {
            return 1;
        }

        return roll < 0.9 ? 2 + _rng.NextInt(29) : 31 + _rng.NextInt(999_969);
    }

    private void SetNumeral(Item item, int value)
    {
        item.Lemma = Digits(value);
        item.Norm = Digits(value);
        item.Number = value == 1 ? LexiconSchema.Singular : LexiconSchema.Plural;
        item.Surface = _rng.NextDouble() < 0.8 ? SpanishNumbers.ToWords(value) : Digits(value);
        item.Readings.Clear();
    }

    private void SetSurface(Item item, string surface)
    {
        item.Surface = surface;
        item.Norm = surface;
        item.Readings.Clear();
        item.Readings.AddRange(_words.Lexicon.Lookup(surface));
    }

    private bool SetAdjective(Item item, IReadOnlyList<string> lemmas, string gender, string number, out string skip)
    {
        skip = "none";
        var candidates = lemmas.Where(l => _words.DescriptiveAdjectiveSet.Contains(l) || LexiconCatalog.PrenominalLemmas.Contains(l)).ToArray();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var lemma = _rng.Pick(candidates);
            var form = _words.AdjectiveForm(lemma, gender, number);
            if (form is not null)
            {
                SetSurface(item, form);
                item.Lemma = lemma;
                item.Gender = gender;
                item.Number = number;
                return true;
            }
        }

        skip = "no-adjective-form";
        return false;
    }

    // ---- Verbs, standalone items and predicate adjectives ---------------------------------------------------------------------------

    private bool FinishVerbs(out string skip)
    {
        skip = "none";
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.Kind != Kind.Verb)
            {
                continue;
            }

            if (item.Infinitive)
            {
                SetSurface(item, item.Lemma!);
                continue;
            }

            string person;
            string number;
            if (item.Subject is { } subject)
            {
                person = subject.Person!;
                number = subject.Number!;
            }
            else if (PostverbalSubjectVerbs.Contains(item.Lemma!))
            {
                person = "3";
                number = _items.Skip(i + 1).FirstOrDefault(static x => x.Head is not null)?.PhraseNumber ?? LexiconSchema.Singular;
            }
            else
            {
                person = (1 + _rng.NextInt(3)).ToString(CultureInfo.InvariantCulture);
                number = _rng.NextDouble() < 0.6 ? LexiconSchema.Singular : LexiconSchema.Plural;
            }

            var lexeme = _words.VerbOf(item.Lemma!)!;
            var tense = lexeme.HasPreterite ? _tense : LexiconSchema.Present;
            var form = _words.Morphology.Conjugate(item.Lemma!, person, number, tense);
            if (form is null)
            {
                skip = "no-verb-form";
                return false;
            }

            SetSurface(item, form);
            item.Person = person;
            item.Number = number;
            item.Tense = tense;
        }

        return true;
    }

    private bool FinishStandaloneItems(out string skip)
    {
        skip = "none";
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.Surface is not null || item.Kind == Kind.Literal)
            {
                continue;
            }

            var gender = _rng.NextDouble() < 0.5 ? LexiconSchema.Masculine : LexiconSchema.Feminine;
            var number = _rng.NextDouble() < 0.6 ? LexiconSchema.Singular : LexiconSchema.Plural;
            switch (item.Kind)
            {
                case Kind.Article:
                    SetSurface(item, _words.Morphology.Article(_rng.NextDouble() < 0.6 ? Definiteness.Definite : Definiteness.Indefinite, gender, number));
                    break;
                case Kind.Determiner:
                    {
                        var forms = _words.DeterminerAdjectiveForms(gender, number);
                        if (forms.Count == 0)
                        {
                            skip = "no-determiner-form";
                            return false;
                        }

                        SetSurface(item, _rng.Pick(forms));
                        break;
                    }

                case Kind.Numeral:
                    SetNumeral(item, PickNumeralValue(allowOne: true));
                    break;
                case Kind.Adjective:
                    if (!SetPredicateAdjective(i, item, out skip))
                    {
                        return false;
                    }

                    break;
                case Kind.Noun:
                    skip = "noun-without-phrase";
                    return false;
                default:
                    break;
            }
        }

        return true;
    }

    private bool SetPredicateAdjective(int index, Item item, out string skip)
    {
        skip = "none";
        var previous = PreviousContent(index);
        var copula = previous is { Kind: Kind.Verb, Infinitive: false } && previous.Lemma is "ser" or "estar" ? previous : null;
        var serClass = _words.SerClassAdjectives;
        var estarClass = _words.EstarClassAdjectives;
        var neutral = _words.NeutralAdjectives;

        IReadOnlyList<string> pool;
        if (copula is not null && _rng.NextDouble() < 0.7)
        {
            pool = copula.Lemma == "ser" ? serClass : estarClass;
        }
        else if (copula is not null)
        {
            pool = neutral;
        }
        else
        {
            pool = [.. _words.DescriptiveAdjectives];
        }

        var subject = copula?.Subject;
        var gender = subject?.Gender ?? LexiconSchema.Masculine;
        var number = copula?.Number ?? subject?.Number ?? LexiconSchema.Singular;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var lemma = _rng.Pick(pool);
            var form = _words.AdjectiveForm(lemma, gender, number);
            if (form is not null)
            {
                SetSurface(item, form);
                item.Lemma = lemma;
                item.Gender = gender;
                item.Number = number;
                return true;
            }
        }

        skip = "no-adjective-form";
        return false;
    }

    // A function word that is also a finite verb form or a noun is re-rolled by retrying the whole sentence. So is a verb form that is a copula only by homograph
    // and is followed by an adjective, and one that stands right behind a determiner or a preposition. Verb forms that are also nouns, adjectives, articles or numerals elsewhere
    // are not: the analyzer resolves them.
    private bool CheckVerbForms(out string skip)
    {
        skip = "none";
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            // A function word that is also a finite verb form or a noun is re-rolled (the imported lexicon has few left, because the import leaves the spelling
            // of hand-authored function words alone; poco and pocos are determiners that also read as the noun poca): its place in the sentence is the one the
            // pattern gives it, and the checks should not mistake it for the verb or the noun.
            if (item.Kind is Kind.Article or Kind.Determiner or Kind.Pronoun or Kind.Numeral or Kind.Adverb or Kind.Preposition or Kind.Conjunction or Kind.Interjection
                && item.Readings.Any(r => IsFiniteVerbReading(r) || r.Pos == LexiconSchema.Noun))
            {
                skip = "function-word-homograph";
                _detail = item.Surface!;
                return false;
            }

            // A determiner or an article that is also another word once its accent is written (el and él, tu and tú, mi and mí, esta and está) is read as the
            // pronoun or the verb when nothing that can go on a noun phrase follows it (M26b), so a pattern that puts a verb, an adverb, a preposition or a conjunction
            // right behind one (a skeleton such as [DET] [VERB]), or leaves one at the end of the sentence (also a preposition or a conjunction: de and dé, que and qué),
            // names a sentence that a reader can take either way and is re-rolled.
            if (item.Kind is Kind.Article or Kind.Determiner or Kind.Preposition or Kind.Conjunction && i == _items.Count - 1 && HasAccentVariants(item.Norm))
            {
                skip = "accent-variant-homograph";
                _detail = item.Surface!;
                return false;
            }

            if (i > 0
                && _items[i - 1].Kind is Kind.Article or Kind.Determiner
                && item.Kind is Kind.Verb or Kind.Adverb or Kind.Preposition or Kind.Conjunction or Kind.Pronoun or Kind.Interjection or Kind.Article
                && HasAccentVariants(_items[i - 1].Norm))
            {
                skip = "accent-variant-homograph";
                _detail = _items[i - 1].Surface!;
                return false;
            }

            // An object pronoun that is another word once its accent is written (te and té, se and sé) is read as the noun or the verb when no verb follows it (the other object
            // pronouns right behind it do not count), so a skeleton that leaves one without a verb (a pattern ending in [PRON]) is re-rolled for the same reason.
            if (item.Kind == Kind.Pronoun && !item.SubjectPronoun && HasAccentVariants(item.Norm) && !VerbFollowsObjectPronoun(i))
            {
                skip = "accent-variant-homograph";
                _detail = item.Surface!;
                return false;
            }

            // A demonstrative or possessive with an accented sibling (esta, tu, mi) is read as the verb or the pronoun when its adjectives and determiners do not end in a noun
            // (a skeleton such as [DET] [DET], or [DET] [ADJ] at the end), and an article with one (el) when the next word cannot go on a noun phrase.
            if (item.Kind is Kind.Article or Kind.Determiner && HasAccentVariants(item.Norm) && !NounPhraseFollows(i, strict: item.Kind == Kind.Determiner))
            {
                skip = "accent-variant-homograph";
                _detail = item.Surface!;
                return false;
            }

            if (item.Kind != Kind.Verb)
            {
                continue;
            }

            // A verb form that is also a copula (fue, fueron: ir and ser) followed by an adjective is a copula by every reading the analyzer can choose among, since
            // all of them are finite verbs, so a ser/estar mistake could be reported for an ir that happens to be followed by an adjective. That is a homograph between
            // two verbs, not between parts of speech, and the sentence is re-rolled.
            var copulaByHomograph = item.Lemma is not ("ser" or "estar")
                && item.Readings.Any(r => r.Pos == LexiconSchema.Verb && r.Person is not null && r.Lemma is "ser" or "estar");
            if (copulaByHomograph && NextContent(i) is var next and >= 0 && _items[next].Kind == Kind.Adjective)
            {
                skip = "verb-form-homograph";
                _detail = item.Surface!;
                return false;
            }

            // A verb right behind an article, numeral, determiner or preposition, or behind an adjective that belongs to no noun (a skeleton pattern such as [ART] [VERB],
            // [PREP] [VERB] or [ADJ] [VERB]), is read by the analyzer as the noun or adjective that the determiner or the prenominal adjective asks for, and a preposition
            // does not govern a finite verb, whenever the form can be a noun or an adjective (la baño, 633540 ocultas, arruinado diseño, al adivinas); a reader could take
            // the article for a clitic, so the sentence is re-rolled rather than called correct.
            if (PreviousContent(i) is { } previous
                && (previous is { Kind: Kind.Article or Kind.Determiner or Kind.Numeral or Kind.Preposition } or { Kind: Kind.Adjective, Head: null } || IsOpeningLiteral(previous))
                && item.Readings.Any(static r => r.Pos is LexiconSchema.Noun or LexiconSchema.Adjective or LexiconSchema.Article or LexiconSchema.Numeral))
            {
                skip = "verb-form-homograph";
                _detail = item.Surface!;
                return false;
            }
        }

        return true;
    }

    private bool NounPhraseFollows(int index, bool strict)
    {
        var head = index + 1;
        while (strict && head < _items.Count && IsAdjunctOnly(_items[head]))
        {
            head++;
        }

        if (head >= _items.Count)
        {
            return false;
        }

        var found = _items[head];
        if (found.Kind is Kind.Noun or Kind.Word)
        {
            return true;
        }

        var readings = _words.Lexicon.Lookup(found.Norm ?? string.Empty);
        return strict
            ? readings.Any(static r => r.Pos == LexiconSchema.Noun)
            : head == index + 1 && (found.Kind is Kind.Adjective or Kind.Article or Kind.Determiner or Kind.Numeral || readings.Any(static r => r.Pos is LexiconSchema.Noun or LexiconSchema.Adjective or LexiconSchema.Article or LexiconSchema.Numeral));
    }

    // An item the analyzer skips on the way from a demonstrative to its noun: a word whose readings are all adjectives, articles or numerals.
    private bool IsAdjunctOnly(Item item)
    {
        var readings = _words.Lexicon.Lookup(item.Norm ?? string.Empty);
        return readings.Count > 0
            ? readings.All(static r => r.Pos is LexiconSchema.Adjective or LexiconSchema.Article or LexiconSchema.Numeral)
            : item.Kind is Kind.Numeral;
    }

    private bool VerbFollowsObjectPronoun(int index)
    {
        var next = index + 1;
        var skipped = 0;
        while (next < _items.Count && skipped < MaxSkippedBetween && _items[next].Kind == Kind.Pronoun && !_items[next].SubjectPronoun)
        {
            next++;
            skipped++;
        }

        return next < _items.Count && (_items[next].Kind == Kind.Verb || (_items[next].Kind == Kind.Literal && _items[next].Readings.Any(static r => r.Pos == LexiconSchema.Verb)));
    }

    private bool HasAccentVariants(string? form)
    {
        return form is not null && _words.Lexicon.LookupWithAccentVariants(form).Variants.Count > 0;
    }

    // A literal word of the grammar that the analyzer takes for a preposition, an article, a numeral, a determiner or an adjective (tras, la, mi, grande), so that the word
    // after it is read as a noun (or at least not as a finite verb).
    private static bool IsOpeningLiteral(Item item)
    {
        return item.Kind == Kind.Literal
            && item.Readings.Count > 0
            && (item.Readings.All(static r => r.Pos == LexiconSchema.Preposition)
                || item.Readings.All(static r => r.Pos is LexiconSchema.Article or LexiconSchema.Numeral or LexiconSchema.Adjective));
    }

    // ---- Output ---------------------------------------------------------------------------------------------------------------------

    private GeneratedSentence Build(GrammarPattern pattern, string source)
    {
        var indexOf = new Dictionary<Item, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < _items.Count; i++)
        {
            indexOf[_items[i]] = i;
        }

        var tokens = new List<SentenceToken>(_items.Count);
        var slots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in _items)
        {
            tokens.Add(new SentenceToken(
                item.Surface!,
                item.Norm!,
                RoleOf(item),
                item.Kind != Kind.Literal || item.Implicit,
                item.Lemma,
                item.Person,
                item.Number,
                item.Gender,
                item.Tense,
                item.Head is null ? -1 : indexOf[item.Head],
                item.Slot,
                item.Infinitive,
                item.SubjectPronoun));
            if (item.Slot is not null)
            {
                slots.TryAdd(item.Slot, ExpectedSlotValue(item));
            }
        }

        return new GeneratedSentence(source, pattern.Render(), tokens, slots);
    }

    private static TokenRole RoleOf(Item item)
    {
        return item.Kind switch
        {
            Kind.Literal => TokenRole.Literal,
            Kind.Article => TokenRole.Article,
            Kind.Determiner => TokenRole.Determiner,
            Kind.Pronoun => TokenRole.Pronoun,
            Kind.Numeral => TokenRole.Numeral,
            Kind.Noun => TokenRole.Noun,
            Kind.Adjective => TokenRole.Adjective,
            Kind.Verb => TokenRole.Verb,
            Kind.Adverb => TokenRole.Adverb,
            Kind.Preposition => TokenRole.Preposition,
            Kind.Conjunction => TokenRole.Conjunction,
            Kind.Word => TokenRole.Name,
            _ => TokenRole.Interjection,
        };
    }

    // The matcher stores the lemma of the first reading with the wanted part of speech; digits for numerals.
    private string ExpectedSlotValue(Item item)
    {
        if (item.Kind is Kind.Numeral or Kind.Word)
        {
            // A name is the token as written in the normalized text: there is no reading to take a lemma from.
            return item.Norm!;
        }

        string pos = item.PatternTag switch
        {
            "ART" => LexiconSchema.Article,
            "PRON" => LexiconSchema.Pronoun,
            "NOUN" => LexiconSchema.Noun,
            "ADJ" => LexiconSchema.Adjective,
            "VERB" => LexiconSchema.Verb,
            "ADV" => LexiconSchema.Adverb,
            "PREP" => LexiconSchema.Preposition,
            "CONJ" => LexiconSchema.Conjunction,
            "INTERJ" => LexiconSchema.Interjection,
            _ => string.Empty,
        };
        foreach (var reading in _words.Lexicon.Lookup(item.Norm!))
        {
            var matches = pos.Length == 0
                ? reading.Pos == LexiconSchema.Article || IsDeterminerAdjective(reading)
                : reading.Pos == pos && (pos != LexiconSchema.Verb || item.PatternLemma is null
                    || string.Equals(Accents.Strip(reading.Lemma), Accents.Strip(item.PatternLemma), StringComparison.Ordinal));
            if (matches)
            {
                return reading.Lemma;
            }
        }

        return item.Lemma ?? item.Norm!;
    }

    /// <summary>The detail of the last failure (for example the unknown literal), for diagnostics.</summary>
    public string Detail => _detail;

    private sealed record NounPhraseItems(List<int> Determiners, List<int> Before, int Head, List<int> After)
    {
        public int Last => After.Count > 0 ? After[^1] : Head;
    }

    private sealed class Item(Kind kind)
    {
        public Kind Kind { get; } = kind;

        public string? PatternLemma { get; set; }

        public string? PatternTag { get; set; }

        public string? Slot { get; set; }

        public bool Implicit { get; set; }

        public string? Surface { get; set; }

        public string? Norm { get; set; }

        public string? Lemma { get; set; }

        public string? Person { get; set; }

        public string? Number { get; set; }

        public string? Gender { get; set; }

        public string? Tense { get; set; }

        public bool Infinitive { get; set; }

        public bool SubjectPronoun { get; set; }

        public Item? Subject { get; set; }

        public NounLexeme? Noun { get; set; }

        public bool ForbidHomograph { get; set; }

        public Item? Head { get; set; }

        public string? PhraseNumber { get; set; }

        public List<LexicalFeatures> Readings { get; } = [];
    }
}
