using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Es.Analysis;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Normalization;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>The single change a mutation makes to a correct sentence, named by the error tag it must produce.</summary>
public enum MutationKind
{
    /// <summary>The verb takes another person or number than its subject pronoun (<c>conj.person</c>).</summary>
    ConjPerson,

    /// <summary>The noun is singular although a numeral or article says plural (<c>noun.number</c>).</summary>
    NounNumber,

    /// <summary>The noun is plural although an article or numeral says singular (<c>noun.number</c>).</summary>
    NounNumberPlural,

    /// <summary>The article has the other gender (<c>gender.agree</c>).</summary>
    GenderArticle,

    /// <summary>An adjective has the other gender (<c>gender.agree</c>).</summary>
    GenderAdjective,

    /// <summary>The copula is swapped against a classed adjective (<c>ser_estar</c>).</summary>
    SerEstar,

    /// <summary>A post-nominal adjective moves in front of its noun (<c>word_order</c>).</summary>
    WordOrder,

    /// <summary>An irregular verb form is replaced by its regular-paradigm cell (<c>conj.irregular</c>).</summary>
    ConjIrregular,

    /// <summary><c>ayer</c> is added to a present-tense sentence (<c>conj.tense</c>).</summary>
    ConjTense,

    /// <summary>A noun is replaced by a non-word (<c>vocab.unknown</c>).</summary>
    VocabUnknown,
}

/// <summary>One error the analyzer must report for a mutated sentence, in the coordinates of the mutated normalized text.</summary>
/// <param name="Tag">The error tag.</param>
/// <param name="Span">The span in the mutated normalized text.</param>
/// <param name="Expected">The form the analyzer must name as the correction (empty for <c>vocab.unknown</c>).</param>
/// <param name="Actual">The text the learner produced at the span.</param>
public sealed record ExpectedError(string Tag, TextSpan Span, string Expected, string Actual);

/// <summary>
/// A correct sentence with exactly one mutation, the errors the analyzer must report for it and the sentence the recast must restore.
/// </summary>
/// <param name="Source">The correct sentence.</param>
/// <param name="Mutated">The mutated sentence (same type, mutated tokens).</param>
/// <param name="Kind">The mutation.</param>
/// <param name="Errors">Every error the analyzer must report, and none besides these.</param>
/// <param name="CorrectedNormalized">The normalized text with every correction applied, or null when no correction is defined (a non-word).</param>
/// <param name="KeepsGrammar">True when the source grammar must still match the mutated sentence.</param>
public sealed record MutatedSentence(
    GeneratedSentence Source,
    GeneratedSentence Mutated,
    MutationKind Kind,
    IReadOnlyList<ExpectedError> Errors,
    string? CorrectedNormalized,
    bool KeepsGrammar)
{
    /// <summary>The tag the mutation must produce.</summary>
    public string Tag => Errors[0].Tag;

    /// <summary>The form the first error must name as the correction (<c>Expected</c>), which is the original form at that span.</summary>
    public string ExpectedForm => Errors[0].Expected;

    /// <summary>The span of the first error in the mutated normalized text.</summary>
    public TextSpan Span => Errors[0].Span;

    /// <summary>A compact description for failure messages.</summary>
    public string Describe()
    {
        return $"{Kind}: {Source.Describe()} => mutated=\"{Mutated.Text}\" normalized=\"{Mutated.NormalizedText}\" expected=[{string.Join("; ", Errors.Select(static e => $"{e.Tag}@{e.Span.Start}+{e.Span.Length} '{e.Expected}'/'{e.Actual}'"))}]";
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Describe();
    }
}

/// <summary>The input of a mutation: which mutation to apply and the seed that drives the search for a sentence it applies to.</summary>
/// <param name="Kind">The mutation.</param>
/// <param name="Seed">The seed of the search.</param>
public sealed record MutationCase(MutationKind Kind, ulong Seed);

/// <summary>
/// Applies exactly one mutation to a correct sentence and names the error it must produce. Sentences are drawn until one admits the
/// requested mutation; when none does in a bounded number of tries the case is counted as unavailable rather than silently skipped.
/// </summary>
public static class MutationGen
{
    private const int MaxSentenceTries = 40;
    private const int MaxSkippedBetween = 2;

    /// <summary>Draws a mutation kind and a search seed.</summary>
    public static Gen<MutationCase> Case { get; } = Gen.Select(Gen.Enum<MutationKind>(), Gen.ULong, static (kind, seed) => new MutationCase(kind, seed));

    /// <summary>
    /// Finds a sentence that admits the mutation and applies it; returns null (counted in <paramref name="diagnostics"/>) when none was found.
    /// </summary>
    public static MutatedSentence? Build(PipelineHarness harness, MutationCase mutationCase, FuzzDiagnostics? diagnostics = null)
    {
        var rng = new SeededRng(mutationCase.Seed);
        var fixedPatterns = FixedPatterns(harness);
        for (var attempt = 0; attempt < MaxSentenceTries; attempt++)
        {
            var pattern = PickPattern(harness, mutationCase.Kind, fixedPatterns, rng);
            var recipe = new SentenceRecipe(pattern.Source, pattern.Pattern, rng.NextUInt64());
            var sentence = SentenceGen.Realize(harness, recipe, OptionsFor(mutationCase.Kind), diagnostics);
            if (sentence is null)
            {
                continue;
            }

            var mutated = TryMutate(harness, sentence, mutationCase.Kind, rng);
            if (mutated is null)
            {
                continue;
            }

            var normalized = PipelineHarness.Normalize(mutated.Mutated.Text);
            if (!normalized.Tokens.Select(static t => t.Text).SequenceEqual(mutated.Mutated.Tokens.Select(static t => t.Norm), StringComparer.Ordinal))
            {
                throw Determinism.Fail(
                    "generator",
                    $"The normalizer disagrees with the mutated sentence: expected tokens [{string.Join(" | ", mutated.Mutated.Tokens.Select(static t => t.Norm))}] but got [{string.Join(" | ", normalized.Tokens.Select(static t => t.Text))}].",
                    mutated.Describe());
            }

            diagnostics?.Count($"mutation:{mutationCase.Kind}:applied");
            return mutated;
        }

        diagnostics?.Count($"mutation:{mutationCase.Kind}:unavailable");
        return null;
    }

    /// <summary>Applies the mutation to the sentence if it admits it, else returns null.</summary>
    public static MutatedSentence? TryMutate(PipelineHarness harness, GeneratedSentence sentence, MutationKind kind, SeededRng rng)
    {
        var context = new Context(harness, sentence, rng);
        return kind switch
        {
            MutationKind.ConjPerson => context.ConjPerson(),
            MutationKind.NounNumber => context.NounNumber(),
            MutationKind.NounNumberPlural => context.NounNumberPlural(),
            MutationKind.GenderArticle => context.GenderArticle(),
            MutationKind.GenderAdjective => context.GenderAdjective(),
            MutationKind.SerEstar => context.SerEstar(),
            MutationKind.WordOrder => context.WordOrder(),
            MutationKind.ConjIrregular => context.ConjIrregular(),
            MutationKind.ConjTense => context.ConjTense(),
            MutationKind.VocabUnknown => context.VocabUnknown(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown mutation."),
        };
    }

    private static List<SourcedPattern> FixedPatterns(PipelineHarness harness)
    {
        var all = harness.Grammars.Grammars.Select(static g => new SourcedPattern("catalog", g.Pattern)).ToList();
        all.AddRange(PatternGen.Skeletons.Select(static s => new SourcedPattern("skeleton", GrammarPattern.Parse(s))));
        return all;
    }

    private static SourcedPattern PickPattern(PipelineHarness harness, MutationKind kind, List<SourcedPattern> fixedPatterns, SeededRng rng)
    {
        var roll = rng.NextDouble();
        if (roll < 0.3)
        {
            return new SourcedPattern("random", PatternGen.Build(harness.Words, rng));
        }

        // The skeletons guarantee the shapes the structural mutations need, so they are drawn more often for those.
        var skeletons = fixedPatterns.Where(static p => p.Source == "skeleton").ToArray();
        var structural = kind is not (MutationKind.ConjIrregular or MutationKind.VocabUnknown);
        if (structural && roll < 0.65)
        {
            return rng.Pick(skeletons);
        }

        return rng.Pick(fixedPatterns);
    }

    private static SentenceOptions OptionsFor(MutationKind kind)
    {
        return kind switch
        {
            MutationKind.ConjPerson => new SentenceOptions { OptionalProbability = 0.8, SubjectProbability = 1.0, PoliteProbability = 0.0 },
            MutationKind.NounNumberPlural => SentenceOptions.Full with { OneProbability = 0.5 },
            MutationKind.NounNumber or MutationKind.GenderArticle or MutationKind.GenderAdjective or MutationKind.WordOrder => SentenceOptions.Full,
            MutationKind.SerEstar => new SentenceOptions { SubjectProbability = 0.8, PoliteProbability = 0.0 },
            MutationKind.ConjTense => new SentenceOptions { PreteriteProbability = 0.0, PoliteProbability = 0.0 },
            _ => SentenceOptions.Default,
        };
    }

    private sealed class Context(PipelineHarness harness, GeneratedSentence sentence, SeededRng rng)
    {
        private static readonly string[] Persons = ["1", "2", "3"];
        private static readonly string[] Numbers = [LexiconSchema.Singular, LexiconSchema.Plural];

        private readonly LexiconCatalog _words = harness.Words;
        private readonly IReadOnlyList<SentenceToken> _tokens = sentence.Tokens;

        // M26b, and since M65 (a) for the tense mutation too (the owner's yes, 2026-11-01): a grammar with both an alternation and a name can bind a mutated verb as the
        // name when an alternative with the name fits the mutated sentence, and a name has no reading to check (seed 1RTZdjQ2tFb2: nosotras freís against
        // ([VERB:freír]|[ART] [NOUN:item]|[PRON] [WORD]) ...; seed egVgLELrxyL4: ayer presiono abusar against ([VERB:presionar]|[ADV] [WORD:item]|[VERB]) [VERB:abusar],
        // where [ADV] [WORD:item] takes ayer presiono). Neither mutation is made on such a grammar.
        private bool NameBesideAnAlternation =>
            sentence.PatternText.Contains("[WORD", StringComparison.Ordinal) && sentence.PatternText.Contains('|', StringComparison.Ordinal);

        public MutatedSentence? ConjPerson()
        {
            if (NameBesideAnAlternation)
            {
                return null;
            }

            foreach (var i in Shuffled(Indexes(static t => t is { Role: TokenRole.Verb, Generated: true, Infinitive: false, Tense: not null })))
            {
                var subject = PersonSubjectOf(i);

                // M66 (M63): a subject pronoun before a question word (ella qué quiere, ellas dónde están) is the subject of the verb after it. The analyzer leaves a third
                // person there unchecked in two readings (a subject after the verb; qué as the subject), so through such a pronoun only a first or second person is made.
                var asked = subject >= 0 && Enumerable.Range(subject + 1, i - subject - 1).Any(k => LexiconCatalog.QuestionWordsAfterSubject.Contains(_tokens[k].Norm));
                if (subject < 0 && QuestionSubjectOf(i) is var questioned and >= 0)
                {
                    subject = questioned;
                    asked = true;
                }

                // M66: a pronoun right after a finite verb that agrees with it is that verb's subject (¿puede usted…?), and the analyzer checks no verb after it.
                if (subject > 0 && FollowsAgreeingVerb(subject))
                {
                    continue;
                }

                if (subject < 0)
                {
                    if (PostverbalSubject(i) is { } postverbal)
                    {
                        return postverbal;
                    }

                    if (PreverbalSubject(i) is { } preverbal)
                    {
                        return preverbal;
                    }

                    continue;
                }

                var pronoun = _tokens[subject];
                var verb = _tokens[i];
                if (AcceptedCells(pronoun, verb) is not { } accepted)
                {
                    continue;
                }

                var cells = Persons.SelectMany(static p => Numbers.Select(n => (Person: p, Number: n))).ToList();
                rng.Shuffle(cells);
                foreach (var (person, number) in cells)
                {
                    if (accepted.Contains((person, number)) || (asked && person == "3"))
                    {
                        continue;
                    }

                    var form = _words.Morphology.Conjugate(verb.Lemma!, person, number, verb.Tense!);
                    if (form is null || form == verb.Norm)
                    {
                        continue;
                    }

                    // The form must not agree with the subject through some other reading, and it must have exactly one reading, or the
                    // correction the analyzer names would depend on which reading it picks as representative.
                    var finite = _words.Lexicon.Lookup(form).Where(static r => r.Pos == LexiconSchema.Verb && r.Person is not null).ToArray();
                    if (finite.Length == 0
                        || finite.Any(r => accepted.Contains((r.Person!, r.Number!)))
                        || finite.Any(r => r.Lemma != verb.Lemma || r.Tense != verb.Tense)
                        || (asked && _words.Lexicon.Lookup(form).Any(static r => !(r.Pos == LexiconSchema.Verb && r.Person is not null))))
                    {
                        continue;
                    }

                    var mutated = Replace(i, verb with { Written = form, Norm = form, Person = person, Number = number });
                    return Result(MutationKind.ConjPerson, mutated, [Error(mutated, ErrorTags.ConjPerson, i, i, verb.Norm, form)], sentence.NormalizedText, keepsGrammar: true);
                }
            }

            return null;
        }

        // The person and number cells the analyzer accepts for a verb after its subject pronoun: the pronoun's own, or, for a pronoun that ends a coordinated subject
        // (M64: luz y yo comemos), the plural cells of the coordination, and after o the cell of the member nearest the verb (M66). A coordinated verb of the second
        // person plural (tú y ella coméis), or of the nearest member's singular (tú o yo voy), is not mutated: it is right, but the analyzer's correction of a mutation
        // there is the plural (comen, vamos), not the form the sentence had.
        private static List<(string, string)>? AcceptedCells(SentenceToken pronoun, SentenceToken verb)
        {
            if (pronoun.Coordination is not { } coordination)
            {
                return [(pronoun.Person!, pronoun.Number!)];
            }

            if (verb is { Person: "2", Number: LexiconSchema.Plural })
            {
                return null;
            }

            // M66: nor is one that agrees with the member nearest it after o (tú o yo voy, nosotras o ellos vuelan), unless that is the coordination's first plural cell:
            // the analyzer's correction of a mutation there is the first plural cell (vamos, volamos).
            var first = coordination.Person == "1" ? ("1", LexiconSchema.Plural) : ("3", LexiconSchema.Plural);
            if (coordination.NearestPerson is not null && verb.Person == coordination.NearestPerson && verb.Number == coordination.NearestNumber
                && (verb.Person, verb.Number) != first)
            {
                return null;
            }

            List<(string, string)> cells = coordination.Person switch
            {
                "1" => [("1", LexiconSchema.Plural)],
                "2" => [("2", LexiconSchema.Plural), ("3", LexiconSchema.Plural)],
                _ => [("3", LexiconSchema.Plural)],
            };

            // M66: o also accepts the cell of the member nearest the verb (tú o yo voy).
            if (coordination is { NearestPerson: { } person, NearestNumber: { } number })
            {
                cells.Add((person, number));
            }

            return cells;
        }

        // M56: a third-person verb whose subject follows it (¿dónde están las casas?, me gustan las manzanas) takes the other number; the analyzer reports conj.person
        // with the original form. The mutation is made only where the analyzer's reading of the sentence is certain: the verb is estar, or an experiencer verb right
        // behind a dative pronoun; no word before the verb can be a noun, a subject pronoun or a name, or is unknown; an article or numeral right after the verb opens
        // the phrase of a noun of one number that cannot be a verb, is no noun of time or quantity and has no y, e, o, u or ni anywhere after it; and the new form is
        // that verb and nothing else.
        private MutatedSentence? PostverbalSubject(int i)
        {
            var verb = _tokens[i];
            if (verb is not { Person: "3", Number: not null, Tense: not null, Lemma: not null })
            {
                return null;
            }

            var experiencer = LexiconCatalog.ExperiencerVerbs.Contains(verb.Lemma)
                && i > 0
                && LexiconCatalog.DativePronouns.Contains(_tokens[i - 1].Norm)
                && _words.Lexicon.Lookup(_tokens[i - 1].Norm).Any(static r => r.Pos == LexiconSchema.Pronoun);
            if (verb.Lemma != "estar" && !experiencer)
            {
                return null;
            }

            for (var j = 0; j < i; j++)
            {
                var readings = _words.Lexicon.Lookup(_tokens[j].Norm);
                if (_tokens[j].Role == TokenRole.Name
                    || readings.Count == 0
                    || readings.Any(static r => r.Pos == LexiconSchema.Noun || (r.Pos == LexiconSchema.Pronoun && LexiconCatalog.SubjectPronounLemmas.Contains(r.Lemma))))
                {
                    return null;
                }
            }

            if (i + 1 >= _tokens.Count || _tokens[i + 1] is not { Role: TokenRole.Article or TokenRole.Numeral, Head: > 0 } opener)
            {
                return null;
            }

            var head = _tokens[opener.Head];
            var nouns = _words.Lexicon.Lookup(head.Norm).Where(static r => r.Pos == LexiconSchema.Noun).ToList();
            if (head.Role != TokenRole.Noun
                || nouns.Count == 0
                || nouns.Select(static r => r.Number).Distinct().Count() != 1
                || nouns.Any(static r => LexiconCatalog.PostverbalNonSubjectNouns.Contains(r.Lemma))
                || _words.Lexicon.Lookup(head.Norm).Any(static r => r.Pos == LexiconSchema.Verb)
                || _tokens.Skip(opener.Head + 1).Any(static t => t.Norm is "y" or "e" or "o" or "u" or "ni"))
            {
                return null;
            }

            // M70 (a widening): gustar and encantar behind their dative pronoun agree with the thing liked, so a form of the first or second person is the slip too (me
            // gusto el pan, gusta); half the cases of those verbs try such a form first, the other half the third person of the other number, as before.
            var cells = new List<(string Person, string Number)> { ("3", verb.Number == LexiconSchema.Singular ? LexiconSchema.Plural : LexiconSchema.Singular) };
            if (experiencer && LexiconCatalog.LikingVerbs.Contains(verb.Lemma))
            {
                var speakers = new List<(string Person, string Number)> { ("1", LexiconSchema.Singular), ("2", LexiconSchema.Singular), ("1", LexiconSchema.Plural), ("2", LexiconSchema.Plural) };
                rng.Shuffle(speakers);
                cells = rng.NextDouble() < 0.5 ? [.. speakers, .. cells] : [.. cells, .. speakers];
            }

            foreach (var (person, number) in cells)
            {
                var form = _words.Morphology.Conjugate(verb.Lemma, person, number, verb.Tense);
                if (form is null || form == verb.Norm)
                {
                    continue;
                }

                var readingsOfForm = _words.Lexicon.Lookup(form);
                if (readingsOfForm.Count == 0
                    || readingsOfForm.Any(r => r.Pos != LexiconSchema.Verb || r.Person is null || r.Lemma != verb.Lemma)
                    || readingsOfForm.Any(r => r.Person == "3" && r.Number == verb.Number && r.Tense == verb.Tense))
                {
                    continue;
                }

                var mutated = Replace(i, verb with { Written = form, Norm = form, Person = person, Number = number });
                return Result(MutationKind.ConjPerson, mutated, [Error(mutated, ErrorTags.ConjPerson, i, i, verb.Norm, form)], sentence.NormalizedText, keepsGrammar: true);
            }

            return null;
        }

        // M59: a third-person verb right after a noun phrase with an article or numeral that opens the sentence is agreed with it (los niños comen pan, el pan está
        // aquí), so the other number gives conj.person with the original form. The mutation is made only where the analyzer's reading is certain: the phrase is the first
        // two words of the sentence, an article or numeral and its noun; every reading of the noun is a noun of one number, no noun of time or quantity; the verb follows
        // the noun directly; and the new form has no reading but that verb in that tense. A lone name (domingo hace un hacha) is never mutated: it may be the person spoken
        // to, so the analyzer does not check its verb; nor is a verb of the first or second person made, which the analyzer does not check after a noun phrase.
        private MutatedSentence? PreverbalSubject(int i)
        {
            var verb = _tokens[i];
            if (verb is not { Person: "3", Number: not null, Tense: not null, Lemma: not null } || i != 2)
            {
                return null;
            }

            // M65 (b), the owner's yes of 2026-11-01: the analyzer leaves haber out of this check by design (hay dos panes; M59), so the mutation leaves it out too and
            // models what the analyzer claims to check (seeds b3HSKSLCsL_1: el subtítulo ha peligroso made han; 4YuZ2yubxdC1: unas televisiones han made ha). The new form
            // has no reading but this verb's (below), so this is exactly the analyzer's exclusion. To widen when a lesson teaches the perfect: see the README.
            if (verb.Lemma == "haber")
            {
                return null;
            }

            var noun = _tokens[1];
            if (noun.Role is not (TokenRole.Noun or TokenRole.Literal) || noun.Head != 1 || _tokens[0] is not { Role: TokenRole.Article or TokenRole.Numeral, Head: 1 })
            {
                return null;
            }

            var nouns = _words.Lexicon.Lookup(noun.Norm);
            if (nouns.Count == 0
                || nouns.Any(static r => r.Pos != LexiconSchema.Noun)
                || nouns.Select(static r => r.Number).Distinct().Count() != 1
                || nouns[0].Number != verb.Number
                || nouns.Any(static r => LexiconCatalog.PostverbalNonSubjectNouns.Contains(r.Lemma)))
            {
                return null;
            }

            var number = verb.Number == LexiconSchema.Singular ? LexiconSchema.Plural : LexiconSchema.Singular;

            // The one guard the owner's request to fix the review's findings allows (2026-11-01, M64 (b)): an amount before a singular ser with a singular predicate is
            // read as one sum (dos panes es suficiente, diez monedas es mucho dinero), so the analyzer accepts es after it as well as son.
            if (_tokens[0].Role == TokenRole.Numeral && verb.Lemma == "ser" && number == LexiconSchema.Singular && SingularPredicateAfter(i))
            {
                return null;
            }
            var form = _words.Morphology.Conjugate(verb.Lemma, "3", number, verb.Tense);
            if (form is null || form == verb.Norm)
            {
                return null;
            }

            var readings = _words.Lexicon.Lookup(form);
            if (readings.Count == 0
                || readings.Any(r => r.Pos != LexiconSchema.Verb || r.Person is null || r.Lemma != verb.Lemma || r.Tense != verb.Tense)
                || readings.Any(r => r.Person == "3" && r.Number == verb.Number))
            {
                return null;
            }

            var mutated = Replace(i, verb with { Written = form, Norm = form, Number = number });
            return Result(MutationKind.ConjPerson, mutated, [Error(mutated, ErrorTags.ConjPerson, i, i, verb.Norm, form)], sentence.NormalizedText, keepsGrammar: true);
        }

        // M64 (b): the word after the verb at index (past at most two skippable words) is a word of LexiconCatalog.AmountPredicates or can open a noun phrase of the
        // singular (an article, a numeral or a determiner adjective of the singular): the predicate after which the analyzer reads an amount before a singular ser as one
        // sum (mirror of SpanishAgreementChecker.IsAmountReadAsUnit, which also asks the phrase for its noun).
        private bool SingularPredicateAfter(int verb)
        {
            for (var index = verb + 1; index < _tokens.Count && index - verb <= MaxSkippedBetween + 1; index++)
            {
                var token = _tokens[index];
                if (LexiconCatalog.AmountPredicates.Contains(token.Norm)
                    || token is { Role: TokenRole.Numeral, Number: LexiconSchema.Singular }
                    || _words.Lexicon.Lookup(token.Norm).Any(static r => r.Number == LexiconSchema.Singular
                        && (r.Pos is LexiconSchema.Article or LexiconSchema.Numeral
                            || (r.Pos == LexiconSchema.Adjective && LexiconCatalog.DeterminerAdjectiveLemmas.Contains(r.Lemma)))))
                {
                    return true;
                }

                if (!IsSkippable(token))
                {
                    return false;
                }
            }

            return false;
        }

        public MutatedSentence? NounNumber()
        {
            foreach (var h in Shuffled(Indexes(static t => t is { Role: TokenRole.Noun, Generated: true, Number: LexiconSchema.Plural })))
            {
                var noun = _tokens[h];
                var plural = _tokens.Where((t, j) => j != h && t.Head == h && t.Generated && t.Role is TokenRole.Article or TokenRole.Determiner or TokenRole.Numeral
                    && t.Number == LexiconSchema.Plural).Any();
                var lexeme = _words.NounOf(noun.Lemma);
                if (!plural || lexeme is null || lexeme.Singular == lexeme.Plural)
                {
                    continue;
                }

                var mutated = Replace(h, noun with { Written = lexeme.Singular, Norm = lexeme.Singular, Number = LexiconSchema.Singular });
                return Result(MutationKind.NounNumber, mutated, [Error(mutated, ErrorTags.NounNumber, h, h, noun.Norm, lexeme.Singular)], sentence.NormalizedText, keepsGrammar: true);
            }

            return null;
        }

        public MutatedSentence? NounNumberPlural()
        {
            foreach (var h in Shuffled(Indexes(static t => t is { Role: TokenRole.Noun, Generated: true, Number: LexiconSchema.Singular })))
            {
                var noun = _tokens[h];
                var singular = _tokens.Where((t, j) => j != h && t.Head == h && t.Generated && t.Role is TokenRole.Article or TokenRole.Determiner or TokenRole.Numeral
                    && t.Number == LexiconSchema.Singular).Any();
                var lexeme = _words.NounOf(noun.Lemma);
                if (!singular || lexeme is null || lexeme.Singular == lexeme.Plural)
                {
                    continue;
                }

                var mutated = Replace(h, noun with { Written = lexeme.Plural, Norm = lexeme.Plural, Number = LexiconSchema.Plural });
                return Result(MutationKind.NounNumberPlural, mutated, [Error(mutated, ErrorTags.NounNumber, h, h, noun.Norm, lexeme.Plural)], sentence.NormalizedText, keepsGrammar: true);
            }

            return null;
        }

        // A noun that also reads as a noun of the other gender (el estudiante, la estudiante) agrees with both articles and adjectives, so no gender mistake can be built
        // on it; the mutation looks for another noun.
        private bool ReadsAsBothGenders(SentenceToken noun)
        {
            return _words.NounOf(noun.Lemma) is { AlsoOtherGender: true };
        }

        public MutatedSentence? GenderArticle()
        {
            foreach (var a in Shuffled(Indexes(static t => t is { Role: TokenRole.Article, Generated: true, Head: >= 0 })))
            {
                var article = _tokens[a];
                var head = _tokens[article.Head];
                if (head.Role != TokenRole.Noun || !head.Generated || LexiconCatalog.FeminineNounsTakingEl.Contains(head.Lemma!) || ReadsAsBothGenders(head))
                {
                    continue;
                }

                var flipped = head.Gender == LexiconSchema.Masculine ? LexiconSchema.Feminine : LexiconSchema.Masculine;
                var definiteness = article.Norm is "el" or "la" or "los" or "las" ? Definiteness.Definite : Definiteness.Indefinite;
                var form = _words.Morphology.Article(definiteness, flipped, head.Number!);
                if (form == article.Norm)
                {
                    continue;
                }

                var mutated = Replace(a, article with { Written = form, Norm = form, Gender = flipped });
                return Result(MutationKind.GenderArticle, mutated, [Error(mutated, ErrorTags.GenderAgree, a, a, article.Norm, form)], sentence.NormalizedText, keepsGrammar: true);
            }

            return null;
        }

        public MutatedSentence? GenderAdjective()
        {
            // M62 widened the mutation to a predicate adjective (Head -1) whose copula has a subject pronoun of a known gender before it (ella es alta -> alto): the
            // analyzer agrees it with the pronoun, as it agrees an attributive adjective with its noun.
            foreach (var j in Shuffled(Indexes(static t => t is { Role: TokenRole.Adjective, Generated: true })))
            {
                var adjective = _tokens[j];
                string? gender;
                if (adjective.Head >= 0)
                {
                    var head = _tokens[adjective.Head];
                    if (head.Role != TokenRole.Noun || !head.Generated || ReadsAsBothGenders(head))
                    {
                        continue;
                    }

                    gender = head.Gender;
                }
                else
                {
                    // M69: or a predicate adjective whose copula follows a subject with de that opens the utterance (la casa de los niños es bonita -> bonito).
                    gender = PredicatePronounGender(j) ?? PredicateSubjectWithDeGender(j);
                    if (gender is null)
                    {
                        continue;
                    }
                }

                var flipped = gender == LexiconSchema.Masculine ? LexiconSchema.Feminine : LexiconSchema.Masculine;
                var form = _words.AdjectiveForm(adjective.Lemma!, flipped, adjective.Number!);
                if (form is null || form == adjective.Norm)
                {
                    continue;
                }

                // The new form must be an adjective and nothing else (alguno is also a pronoun, which a grammar with [PRON] beside [ADJ] can take it for, and which
                // agrees with nothing), and every adjective reading of it must name the other gender, or the analyzer finds it compatible.
                if (_words.Lexicon.Lookup(form).Any(static r => r.Pos != LexiconSchema.Adjective))
                {
                    continue;
                }

                // The word before the adjective may be a noun the new form agrees with (lata buena): the adjective is then read as that noun's, and there is no mistake.
                if (j > 0 && _words.Lexicon.Lookup(_tokens[j - 1].Norm).Any(r => r.Pos == LexiconSchema.Noun && (r.Gender is null || r.Gender == flipped)))
                {
                    continue;
                }

                var readings = _words.Lexicon.Lookup(form).Where(static r => r.Pos == LexiconSchema.Adjective).ToArray();
                if (readings.Length == 0 || readings.Any(r => r.Gender is null || r.Gender == gender) || readings[0].Lemma != adjective.Lemma)
                {
                    continue;
                }

                var mutated = Replace(j, adjective with { Written = form, Norm = form, Gender = flipped });
                return Result(MutationKind.GenderAdjective, mutated, [Error(mutated, ErrorTags.GenderAgree, j, j, adjective.Norm, form)], sentence.NormalizedText, keepsGrammar: true);
            }

            return null;
        }

        // M69: the gender of the subject with de of the copula that the standalone adjective at index follows right after (la casa de los niños es bonita: casa), or null:
        // the copula is a generated finite ser or estar of the third person; before it stand a phrase of an article and a noun, de, and the first phrase, an article and a
        // generated noun of one gender that opens the utterance, is no noun of part, quantity or time and has the copula's number (mirror of
        // SpanishAgreementChecker.TryFindSubjectWithDe, for the one shape of the skeleton).
        private string? PredicateSubjectWithDeGender(int index)
        {
            var copula = index - 1;
            if (copula < 5
                || _tokens[copula] is not { Role: TokenRole.Verb, Generated: true, Infinitive: false, Person: "3", Lemma: "ser" or "estar" } verb
                || _tokens[copula - 1] is not { Role: TokenRole.Noun }
                || _tokens[copula - 2] is not { Role: TokenRole.Article }
                || _tokens[copula - 3].Norm != "de"
                || _tokens[copula - 4] is not { Role: TokenRole.Noun, Generated: true } head
                || copula - 5 != 0
                || _tokens[0] is not { Role: TokenRole.Article }
                || head.Number != verb.Number
                || head.Gender is null
                || ReadsAsBothGenders(head)
                || LexiconCatalog.PostverbalNonSubjectNouns.Contains(head.Lemma!))
            {
                return null;
            }

            return head.Gender;
        }

        // M62: the gender of the subject pronoun of the copula that the standalone adjective at index follows (ella es alta, nosotros no somos altos), or null: the
        // copula is a generated finite ser or estar right before the adjective (past skippable words), and the pronoun the analyzer pairs with it has a gender and no
        // preposition before it (mirror of the first subject of SpanishAgreementChecker.CheckPredicateAdjectives).
        private string? PredicatePronounGender(int index)
        {
            var copula = PreviousContent(index);
            if (copula < 0
                || index - copula > 3
                || _tokens[copula] is not { Role: TokenRole.Verb, Generated: true, Infinitive: false, Lemma: "ser" or "estar" })
            {
                return null;
            }

            var pronoun = PersonSubjectOf(copula);
            if (pronoun < 0 || copula - pronoun > 3)
            {
                return null;
            }

            // M64: a pronoun that ends a coordinated subject gives the coordination's gender, known only when every member has one (ella y él son altos). M66: after
            // o, a copula of the nearest member's cell alone gives that member's gender (tú o ella es alta), and one of both cells a gender only where they share it.
            if (_tokens[pronoun].Coordination is not { } coordination)
            {
                return _tokens[pronoun].Gender;
            }

            return coordination.PredicateAgreement(_tokens[copula].Person, _tokens[copula].Number).Gender;
        }

        public MutatedSentence? SerEstar()
        {
            // M69: estar before en and a place, after a subject phrase that opens the utterance, becomes ser (la casa está en la plaza -> es), which the analyzer reports
            // as ser_estar with the form of estar; first, when the sentence has that shape.
            if (SerBeforeEnAndAPlace() is { } place)
            {
                return place;
            }

            foreach (var i in Shuffled(Indexes(static t => t is { Role: TokenRole.Verb, Generated: true, Infinitive: false, Lemma: "ser" or "estar" })))
            {
                var copula = _tokens[i];
                var next = NextContent(i);
                if (next < 0 || _tokens[next] is not { Role: TokenRole.Adjective, Generated: true, Head: -1 } adjective)
                {
                    continue;
                }

                var inClass = copula.Lemma == "ser"
                    ? LexiconCatalog.SerAdjectives.Contains(adjective.Lemma!)
                    : LexiconCatalog.EstarAdjectives.Contains(adjective.Lemma!);
                if (!inClass)
                {
                    continue;
                }

                var target = copula.Lemma == "ser" ? "estar" : "ser";
                var form = _words.Morphology.Conjugate(target, copula.Person!, copula.Number!, copula.Tense!);
                if (form is null)
                {
                    continue;
                }

                // The analyzer takes the first copula reading of the form, so a form that is also the other copula's would be ambiguous.
                var copulaReadings = _words.Lexicon.Lookup(form).Where(static r => r.Pos == LexiconSchema.Verb && r.Person is not null && r.Lemma is "ser" or "estar").ToArray();
                if (copulaReadings.Length == 0 || copulaReadings.Any(r => r.Lemma != target))
                {
                    continue;
                }

                // A form that is also a noun (son) is the copula only where the context forces it, right behind a subject pronoun: behind an adjective it is the noun of
                // the adjective (callado son), which is a word-order mistake and not a ser/estar one.
                if (SubjectOf(i) < 0 && _words.Lexicon.Lookup(form).Any(static r => r.Pos != LexiconSchema.Verb))
                {
                    continue;
                }

                var mutated = Replace(i, copula with { Written = form, Norm = form, Lemma = target });

                // The lemma changes, so a grammar that names ser or estar (such as [VERB:estar]) no longer matches; that is the lexical swap itself.
                return Result(MutationKind.SerEstar, mutated, [Error(mutated, ErrorTags.SerEstar, i, i, copula.Norm, form)], sentence.NormalizedText, keepsGrammar: false);
            }

            return null;
        }

        // M69: the ser_estar mutation of es en (mirror of SpanishAgreementChecker.CheckSerOfPlace, rule 3, for the shape of the skeleton): the generated estar of the
        // third person at index 2 after an article and a noun that open the utterance, no event, followed by en, an article and a noun that is a place (no noun of time
        // or of an adverbial phrase); the form of ser of the same cell must be ser's alone (fue is also ir) and no noun (son, era).
        private MutatedSentence? SerBeforeEnAndAPlace()
        {
            const int i = 2;
            if (_tokens.Count != 6
                || _tokens[0] is not { Role: TokenRole.Article }
                || _tokens[1] is not { Role: TokenRole.Noun } subject
                || _tokens[i] is not { Role: TokenRole.Verb, Generated: true, Infinitive: false, Lemma: "estar", Person: "3" } copula
                || _tokens[3].Norm != "en"
                || _tokens[4] is not { Role: TokenRole.Article }
                || _tokens[5] is not { Role: TokenRole.Noun } place
                || LexiconCatalog.EventNouns.Contains(subject.Lemma!)
                || LexiconCatalog.NonPlaceNouns.Contains(place.Lemma!)
                || _words.Lexicon.Lookup(subject.Norm).Any(static r => r.Pos == LexiconSchema.Noun && LexiconCatalog.EventNouns.Contains(r.Lemma))
                || _words.Lexicon.Lookup(place.Norm).Any(static r => r.Pos == LexiconSchema.Noun && LexiconCatalog.NonPlaceNouns.Contains(r.Lemma)))
            {
                return null;
            }

            var form = _words.Morphology.Conjugate("ser", copula.Person!, copula.Number!, copula.Tense!);
            if (form is null)
            {
                return null;
            }

            var readings = _words.Lexicon.Lookup(form);
            if (readings.Count == 0 || readings.Any(static r => r.Pos != LexiconSchema.Verb || r.Lemma != "ser"))
            {
                return null;
            }

            var mutated = Replace(i, copula with { Written = form, Norm = form, Lemma = "ser" });
            return Result(MutationKind.SerEstar, mutated, [Error(mutated, ErrorTags.SerEstar, i, i, copula.Norm, form)], sentence.NormalizedText, keepsGrammar: false);
        }

        public MutatedSentence? WordOrder()
        {
            foreach (var h in Shuffled(Indexes(static t => t is { Role: TokenRole.Noun, Generated: true })))
            {
                if (h + 1 >= _tokens.Count
                    || _tokens[h + 1] is not { Role: TokenRole.Adjective, Generated: true } adjective
                    || adjective.Head != h
                    || LexiconCatalog.PrenominalLemmas.Contains(adjective.Lemma!))
                {
                    continue;
                }

                // M49: próximo/a in front of a time noun is correct ("los treinta próximos domingos"), so moving it there makes no mistake; the mutation looks for another sentence.
                if (string.Equals(adjective.Lemma, SpanishTimeMarkers.UpcomingAdjective, StringComparison.Ordinal) && LexiconCatalog.UpcomingTimeNouns.Contains(_tokens[h].Lemma!))
                {
                    continue;
                }

                // Moved in front of the noun the adjective must start or extend the phrase; right behind the noun or adjunct of an earlier
                // phrase (or a literal word, which may be one) it would be read as belonging to that phrase instead.
                if (h > 0 && _tokens[h - 1].Head != h && _tokens[h - 1].Role is TokenRole.Noun or TokenRole.Adjective or TokenRole.Literal)
                {
                    continue;
                }

                // Right behind a word that can be a noun (a verb form such as asesinas, saludes), or behind adjectives that follow one, the adjective could belong to that word instead, and a pronoun or adverb
                // that is spelled like the adjective (suyo, temprano) in front of a noun that can be a verb could be read as that pronoun or adverb and the verb, and
                // an adverb that goes before nouns (incluso resultado) is no mistake at all: either way the sentence has a reading without the mistake, so the
                // mutation looks for another sentence.
                // M58 (c), by the owner's decision (2026-10-31, "go with option 1"): the walk reads the words' readings, not the pattern's roles, so it also passes over a
                // word that can be a descriptive adjective besides something else (plantar, a verb of the pattern and an adjective; seed ff2aNwHuJBk8: suelta plantar
                // satisfecha is one phrase to the analyzer). A word that can be a noun ends the walk, and so does a determiner, which opens a phrase of its own.
                var before = h - 1;
                while (before >= 0 && _words.Lexicon.Lookup(_tokens[before].Norm) is { Count: > 0 } readingsBefore
                    && (readingsBefore.All(static r => r.Pos == LexiconSchema.Adjective) || CanOnlyExtendAPhrase(readingsBefore)))
                {
                    before--;
                }

                if (before >= 0 && _words.Lexicon.Lookup(_tokens[before].Norm).Any(static r => r.Pos == LexiconSchema.Noun))
                {
                    continue;
                }

                var adjectiveReadings = _words.Lexicon.Lookup(adjective.Norm);
                if ((_words.Lexicon.Lookup(_tokens[h].Norm).Any(static r => r.Pos == LexiconSchema.Verb && r.Person is not null)
                        && adjectiveReadings.Any(static r => r.Pos != LexiconSchema.Adjective))
                    || adjectiveReadings.Any(static r => r.Pos == LexiconSchema.Adverb && LexiconCatalog.NounModifyingAdverbs.Contains(r.Lemma)))
                {
                    continue;
                }

                // In front of the noun the adjective may come to stand right behind a copula, where a ser or estar adjective is a different mistake.
                var copulaBefore = PreviousContent(h);
                if (copulaBefore >= 0
                    && _tokens[copulaBefore] is { Role: TokenRole.Verb, Generated: true, Infinitive: false, Lemma: "ser" or "estar" }
                    && (LexiconCatalog.SerAdjectives.Contains(adjective.Lemma!) || LexiconCatalog.EstarAdjectives.Contains(adjective.Lemma!)))
                {
                    continue;
                }

                var noun = _tokens[h];
                var list = _tokens.ToList();
                list[h] = adjective with { Head = h + 1 };
                list[h + 1] = noun with { Head = h + 1 };
                for (var k = 0; k < list.Count; k++)
                {
                    if (k != h && k != h + 1 && list[k].Head == h)
                    {
                        list[k] = list[k] with { Head = h + 1 };
                    }
                }

                var mutated = sentence with { Tokens = list };
                var expected = noun.Norm + " " + adjective.Norm;
                var actual = adjective.Norm + " " + noun.Norm;
                var error = new ExpectedError(ErrorTags.WordOrder, mutated.SpanOf(h, h + 1), expected, actual);
                return Result(MutationKind.WordOrder, mutated, [error], sentence.NormalizedText, keepsGrammar: false);
            }

            return null;
        }

        // M58 (c): a word that can be a descriptive adjective and cannot be a noun or a determiner (plantar, a verb and an adjective): the analyzer may read it as an
        // adjective of the noun before it, so an adjective after it may belong to that noun as well.
        private static bool CanOnlyExtendAPhrase(IReadOnlyList<LexicalFeatures> readings)
        {
            return readings.Any(static r => r.Pos == LexiconSchema.Adjective && !LexiconCatalog.DeterminerAdjectiveLemmas.Contains(r.Lemma))
                && !readings.Any(static r => r.Pos is LexiconSchema.Noun or LexiconSchema.Article or LexiconSchema.Numeral
                    || (r.Pos == LexiconSchema.Adjective && LexiconCatalog.DeterminerAdjectiveLemmas.Contains(r.Lemma)));
        }

        public MutatedSentence? ConjIrregular()
        {
            foreach (var i in Shuffled(Indexes(static t => t is { Role: TokenRole.Verb, Generated: true, Infinitive: false, Tense: not null })))
            {
                var verb = _tokens[i];
                foreach (var regular in RegularSpellings(verb))
                {
                    if (regular == verb.Norm)
                    {
                        continue;
                    }

                    // Only a regular cell that the morphology itself reports as the regularization of exactly this form counts.
                    if (!_words.Morphology.IsRegularizedIrregular(regular, out var lemma, out var expected) || lemma != verb.Lemma || expected != verb.Norm)
                    {
                        continue;
                    }

                    var mutated = Replace(i, verb with { Written = regular, Norm = regular });
                    return Result(MutationKind.ConjIrregular, mutated, [Error(mutated, ErrorTags.ConjIrregular, i, i, verb.Norm, regular)], sentence.NormalizedText, keepsGrammar: true);
                }
            }

            return null;
        }

        // The regular cell of the verb's paradigm and, since M70 (a widening), the first person of the preterite of a verb in -car, -gar or -zar written without its
        // spelling change before the é (buscar: buscé for busqué; an independent model of the analyzer's slip, which the morphology must report the same way).
        private IEnumerable<string> RegularSpellings(SentenceToken verb)
        {
            if (harness.RegularParadigm.Conjugate(verb.Lemma!, verb.Person!, verb.Number!, verb.Tense!) is { } regular)
            {
                yield return regular;
            }

            if (verb is { Tense: LexiconSchema.Preterite, Person: "1", Number: LexiconSchema.Singular } && verb.Lemma!.Length >= 5
                && (verb.Lemma.EndsWith("car", StringComparison.Ordinal) || verb.Lemma.EndsWith("gar", StringComparison.Ordinal) || verb.Lemma.EndsWith("zar", StringComparison.Ordinal)))
            {
                yield return verb.Lemma[..^2] + "é";
            }
        }

        public MutatedSentence? ConjTense()
        {
            if (NameBesideAnAlternation)
            {
                return null;
            }

            // M66, a widening of O4 and O5: a sentence with a modal and its infinitive (puedo pagar, tengo que ir) gets, on every other sentence (by the parity of its
            // length, so that no draw of the search moves), the learner's slip for that infinitive instead of the past marker: a finite form right after the modal
            // (puedo pago), which the analyzer reports as conj.tense with the infinitive expected, as after ir a.
            if (_tokens.Count % 2 == 0 && ModalSlip() is { } modalSlip)
            {
                return modalSlip;
            }

            // Only the generated verbs are flagged: a noun that is also a verb form (ayudas, lanza) is read as the noun, because the analyzer resolves the part of
            // speech of each word. The sentence must still hold a generated finite verb, and no literal word of the grammar may be a finite verb form (falta, after a
            // subject pronoun, is the verb), because the oracle names only the generated verbs.
            var finite = Indexes(static t => t is { Role: TokenRole.Verb, Infinitive: false }).Where(IsFiniteAt).ToList();
            var literalVerbForms = Indexes(static t => t.Role == TokenRole.Literal)
                .Where(i => _words.Lexicon.Lookup(_tokens[i].Norm).Any(static r => r.Pos == LexiconSchema.Verb && r.Person is not null && r.Tense is not null))
                .ToList();
            if (finite.Count == 0 || literalVerbForms.Count > 0)
            {
                return null;
            }

            // A generated verb whose form is also a noun or an adjective (parte, busca, oculto, despierto) is read as the verb only where the context forces it, right
            // behind a subject pronoun; anywhere else (intenciones parte) the sentence has a reading without the mistake, which is the charitable one, so the
            // mutation looks for another sentence. A sentence whose only finite verb is such a form still gets the mistake (ayer despierto temprano), because a sentence
            // without a verb costs the analyzer more than one with a tense mistake; the generator cannot know that, so it does not rely on it.
            if (finite.Any(i => SubjectOf(i) < 0 && _words.Lexicon.Lookup(_tokens[i].Norm).Any(static r => r.Pos != LexiconSchema.Verb)))
            {
                return null;
            }

            var corrected = new List<string>();
            var forms = new Dictionary<int, string>();
            foreach (var i in finite)
            {
                var verb = _tokens[i];
                var readings = _words.Lexicon.Lookup(verb.Norm).Where(static r => r.Pos == LexiconSchema.Verb && r.Person is not null).ToArray();
                if (verb.Tense != LexiconSchema.Present || readings.Any(r => r.Lemma != verb.Lemma || r.Tense != LexiconSchema.Present))
                {
                    return null;
                }

                var preterite = _words.Morphology.Conjugate(verb.Lemma!, verb.Person!, verb.Number!, LexiconSchema.Preterite);
                if (preterite is null)
                {
                    return null;
                }

                // ir and ser share their preterite (fueron), so the corrected sentence would read as a copula and could trip the ser/estar check.
                // That is a homograph between two verbs, which the analyzer does not resolve.
                var preteriteReadings = _words.Lexicon.Lookup(preterite);
                var copulaByHomograph = verb.Lemma is not ("ser" or "estar")
                    && preteriteReadings.Any(static r => r.Pos == LexiconSchema.Verb && r.Person is not null && r.Lemma is "ser" or "estar");
                if (copulaByHomograph)
                {
                    return null;
                }

                // The same goes for the corrected form (traje: the preterite of traer, and a noun): the recast must re-analyze clean, and only a subject pronoun forces the verb.
                if (SubjectOf(i) < 0 && preteriteReadings.Any(static r => r.Pos != LexiconSchema.Verb))
                {
                    return null;
                }

                forms[i] = preterite;
            }

            var list = new List<SentenceToken> { new("ayer", "ayer", TokenRole.Adverb, true, "ayer", null, null, null, null, -1, null, false, false) };
            list.AddRange(_tokens.Select(static t => t with { Head = t.Head >= 0 ? t.Head + 1 : -1 }));
            var mutated = sentence with { Tokens = list };

            // M65 (c), the owner's yes of 2026-11-01: a literal of the grammar that is another word once its accent is written (porque and porqué) is read as typed while
            // the grammar binds it, and the analyzer chooses between the two from the neighbouring words only when no grammar matches (ReadingResolver: a literal spelled
            // like the word keeps every reading as typed, so the accent is the one reading the match decides). The realiser leaves that choice to the grammar for a
            // literal. With ayer in front the grammar may no longer match (seed 2ldq2rIKLrG2: ayer las los despego unas porque against [ART] [ART]? [VERB]? [ART]?
            // porque?, where unas porque then reads as the article and the noun porqué). Such a case is kept only when the mutated sentence matches the original's grammar;
            // a sentence with no such literal, and one whose grammar still matches, are mutated as before.
            if (_tokens.Any(IsLiteralWithAnAccentVariant) && !MatchesTheGrammar(mutated))
            {
                return null;
            }

            var errors = finite.Select(i => new ExpectedError(ErrorTags.ConjTense, mutated.SpanOf(i + 1, i + 1), forms[i], _tokens[i].Norm)).ToArray();
            var fixedTokens = list.Select((t, j) => j > 0 && forms.TryGetValue(j - 1, out var form) ? form : t.Norm);
            corrected.AddRange(fixedTokens);
            return new MutatedSentence(sentence, mutated, MutationKind.ConjTense, errors, string.Join(' ', corrected), KeepsGrammar: false);
        }

        public MutatedSentence? VocabUnknown()
        {
            // An adjective behind the noun would attach to the next noun phrase once the noun is gone, which is a different mistake.
            var heads = Indexes(static t => t is { Role: TokenRole.Noun, Generated: true }).Where(h => !_tokens.Where((t, j) => j > h && t.Head == h).Any()).ToList();
            if (heads.Count == 0)
            {
                return null;
            }

            // A name ([WORD]) binds any word of letters, so a grammar that has one takes the non-word for the name and the sentence is still matched, with nothing unknown in it
            // (seed 2to272FZCXD3: religioso nosotros focatudil against [ADJ:item] [PRON] [NOUN:qty]? [WORD:spell]?).
            if (sentence.PatternText.Contains("[WORD", StringComparison.Ordinal))
            {
                return null;
            }

            var h = heads[rng.NextInt(heads.Count)];
            var word = NonWord();
            if (word is null)
            {
                return null;
            }

            var mutated = Replace(h, _tokens[h] with { Written = word, Norm = word, Lemma = null });
            return Result(MutationKind.VocabUnknown, mutated, [Error(mutated, ErrorTags.VocabUnknown, h, h, string.Empty, word)], correctedNormalized: null, keepsGrammar: false);
        }

        private static ExpectedError Error(GeneratedSentence mutated, string tag, int first, int last, string expected, string actual)
        {
            return new ExpectedError(tag, mutated.SpanOf(first, last), expected, actual);
        }

        private List<int> Indexes(Func<SentenceToken, bool> predicate)
        {
            var result = new List<int>();
            for (var i = 0; i < _tokens.Count; i++)
            {
                if (predicate(_tokens[i]))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private List<int> Shuffled(List<int> indexes)
        {
            rng.Shuffle(indexes);
            return indexes;
        }

        // M66: the finite form of the person and number of the modal, in the present, in place of a generated infinitive right after a finite modal (poder, querer,
        // deber, necesitar; tener and que): puedo pagar -> puedo pago, tienes que ir -> tienes que vas. Made only where the analyzer's reading is certain: the form is
        // that verb's indicative and nothing else, and has no accent variant (hablo, habló); the verb is no modal, copula, haber, tener or ir, which the analyzer reads
        // as a clause of its own or another construction (no puedo, estoy cansado; voy a). Or null.
        private MutatedSentence? ModalSlip()
        {
            for (var i = 1; i < _tokens.Count; i++)
            {
                var verb = _tokens[i];
                if (verb is not { Role: TokenRole.Verb, Generated: true, Infinitive: true, Lemma: { } lemma }
                    || LexiconCatalog.IsModalVerb(lemma, null)
                    || lemma is "ser" or "estar" or "haber" or "tener" or "ir"
                    || ModalCellBefore(i) is not { } cell)
                {
                    continue;
                }

                var form = _words.Morphology.Conjugate(lemma, cell.Person, cell.Number, LexiconSchema.Present);
                if (form is null || form == verb.Norm)
                {
                    continue;
                }

                var found = _words.Lexicon.LookupWithAccentVariants(form);
                if (found.Exact.Count == 0
                    || found.Variants.Count > 0
                    || found.Exact.Any(r => !(r.Pos == LexiconSchema.Verb && r.Person is not null && r.Mood == LexiconSchema.Indicative && r.Lemma == lemma)))
                {
                    continue;
                }

                var mutated = Replace(i, verb with { Written = form, Norm = form, Person = cell.Person, Number = cell.Number, Tense = LexiconSchema.Present, Infinitive = false });
                return Result(MutationKind.ConjTense, mutated, [Error(mutated, ErrorTags.ConjTense, i, i, verb.Norm, form)], sentence.NormalizedText, keepsGrammar: false);
            }

            return null;
        }

        // M66: the person and number of the finite modal right before the token at index (or before que, for tener), or null when there is none.
        private (string Person, string Number)? ModalCellBefore(int index)
        {
            if (ModalCell(index - 1, null) is { } direct)
            {
                return direct;
            }

            return index >= 2 && _tokens[index - 1].Norm == "que" ? ModalCell(index - 2, "que") : null;
        }

        private (string Person, string Number)? ModalCell(int index, string? linker)
        {
            // The analyzer takes the word for the modal only when it is a finite verb and nothing else (debe is also a noun) whose readings of the indicative are all
            // the modal's (podamos is podar's indicative).
            var token = _tokens[index];
            var readings = _words.Lexicon.Lookup(token.Norm);
            var indicative = readings.Where(static r => r.Mood == LexiconSchema.Indicative).ToList();
            if (readings.Count == 0
                || !readings.All(static r => r.Pos == LexiconSchema.Verb && r.Person is not null)
                || indicative.Count == 0
                || !indicative.TrueForAll(r => r.Lemma == indicative[0].Lemma && LexiconCatalog.IsModalVerb(r.Lemma, linker)))
            {
                return null;
            }

            if (token is { Role: TokenRole.Verb, Infinitive: false, Lemma: { } lemma, Person: { } person, Number: { } number })
            {
                return LexiconCatalog.IsModalVerb(lemma, linker) ? (person, number) : null;
            }

            if (token.Role != TokenRole.Literal)
            {
                return null;
            }

            var modal = readings.FirstOrDefault(r => r.Mood == LexiconSchema.Indicative && LexiconCatalog.IsModalVerb(r.Lemma, linker));
            return modal is null ? null : (modal.Person!, modal.Number!);
        }

        // M65 (c): a literal word of the grammar that is another word once its accent is written, whose reading the grammar decides while it matches.
        private bool IsLiteralWithAnAccentVariant(SentenceToken token)
        {
            return token.Role == TokenRole.Literal && _words.Lexicon.LookupWithAccentVariants(token.Norm).Variants.Count > 0;
        }

        // M65 (c): whether the analyzer matches the sentence to the grammar the sentence was realised from. Only the match is read, never the errors the oracles compare.
        private bool MatchesTheGrammar(GeneratedSentence candidate)
        {
            var grammar = SlotGrammarType.Parse(sentence.PatternText);
            return string.Equals(harness.Analyzer.Analyze(PipelineHarness.Normalize(candidate.Text), [grammar]).MatchedGrammarId, grammar.Id, StringComparison.Ordinal);
        }

        private bool IsFiniteAt(int index)
        {
            return _words.Lexicon.Lookup(_tokens[index].Norm).Any(static r => r.Pos == LexiconSchema.Verb && r.Person is not null);
        }

        private bool IsSkippable(SentenceToken token)
        {
            // A name has no reading the analyzer uses (M17b), so it is never skipped, whatever its spelling is in the lexicon.
            if (token.Role == TokenRole.Name)
            {
                return false;
            }

            var readings = _words.Lexicon.Lookup(token.Norm);
            return readings.Count > 0
                && (readings.All(static r => r.Pos is LexiconSchema.Adverb or LexiconSchema.Interjection)
                    || readings.All(static r => r.Pos == LexiconSchema.Pronoun && LexiconCatalog.CliticLemmas.Contains(r.Lemma)));
        }

        private int NextContent(int from)
        {
            var index = from + 1;
            var skipped = 0;
            while (index < _tokens.Count && skipped < MaxSkippedBetween && IsSkippable(_tokens[index]))
            {
                index++;
                skipped++;
            }

            return index < _tokens.Count ? index : -1;
        }

        private int PreviousContent(int from)
        {
            for (var index = from - 1; index >= 0; index--)
            {
                if (!IsSkippable(_tokens[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        // The subject pronoun the analyzer pairs with a verb: the nearest earlier subject pronoun whose next content word is the verb.
        private int SubjectOf(int verb)
        {
            for (var p = verb - 1; p >= 0; p--)
            {
                if (_tokens[p] is { Role: TokenRole.Pronoun, SubjectPronoun: true } && NextContent(p) == verb)
                {
                    return p;
                }
            }

            return -1;
        }

        // M66: the subject pronoun at index follows a word that can be a finite verb of the pronoun's person and number (puede usted), so the analyzer reads the pronoun
        // as that verb's subject put after it.
        private bool FollowsAgreeingVerb(int pronoun)
        {
            var token = _tokens[pronoun];
            return _words.Lexicon.Lookup(_tokens[pronoun - 1].Norm)
                .Any(r => r.Pos == LexiconSchema.Verb && r.Person == token.Person && r.Number == token.Number && r.Mood != LexiconSchema.Imperative);
        }

        // M66 (M63): a subject pronoun right before a question word of QuestionWordsAfterSubject that stands right before the verb (past skippable words), and no
        // preposition before the pronoun; -1 otherwise.
        private int QuestionSubjectOf(int verb)
        {
            for (var p = verb - 1; p >= 0; p--)
            {
                if (_tokens[p] is not { Role: TokenRole.Pronoun, SubjectPronoun: true }
                    || NextContent(p) is not (var question and >= 0)
                    || !LexiconCatalog.QuestionWordsAfterSubject.Contains(_tokens[question].Norm)
                    || NextContent(question) != verb)
                {
                    continue;
                }

                return p > 0 && _words.Lexicon.Lookup(_tokens[p - 1].Norm).Any(static r => r.Pos == LexiconSchema.Preposition) ? -1 : p;
            }

            return -1;
        }

        // The subject pronoun the person check agrees a verb with: SubjectOf, unless the pronoun is right behind a word that can be a preposition (a ella le gusta), which
        // M56 made no subject of the check; the reading resolver still takes the word after it for the verb, so SubjectOf stays as it was for the other mutations.
        private int PersonSubjectOf(int verb)
        {
            var p = SubjectOf(verb);
            return p > 0 && _words.Lexicon.Lookup(_tokens[p - 1].Norm).Any(static r => r.Pos == LexiconSchema.Preposition) ? -1 : p;
        }

        private GeneratedSentence Replace(int index, SentenceToken token)
        {
            var list = _tokens.ToList();
            list[index] = token;
            return sentence with { Tokens = list };
        }

        private MutatedSentence Result(MutationKind kind, GeneratedSentence mutated, IReadOnlyList<ExpectedError> errors, string? correctedNormalized, bool keepsGrammar)
        {
            return new MutatedSentence(sentence, mutated, kind, errors, correctedNormalized, keepsGrammar);
        }

        // A pronounceable non-word at least three edits away from every lexicon form (accents ignored), so no lookup, accent fallback or
        // near-miss reading can make the analyzer know it.
        private string? NonWord()
        {
            const string consonants = "bcdfglmnprstvz";
            const string vowels = "aeiou";
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var syllables = 2 + rng.NextInt(3);
                var chars = new List<char>();
                for (var s = 0; s < syllables; s++)
                {
                    chars.Add(consonants[rng.NextInt(consonants.Length)]);
                    chars.Add(vowels[rng.NextInt(vowels.Length)]);
                }

                if (rng.NextDouble() < 0.4)
                {
                    chars.Add(consonants[rng.NextInt(consonants.Length)]);
                }

                var word = new string([.. chars]);
                if (_words.Lexicon.Lookup(word).Count > 0
                    || harness.Words.FoldedForms.Contains(word)
                    || SpanishNumbers.TryParse(word, out _)
                    || _words.Morphology.IsRegularizedIrregular(word, out _, out _)
                    || PipelineHarness.Normalize(word).Text != word)
                {
                    continue;
                }

                if (_words.IsFarFromEveryForm(word, 3))
                {
                    return word;
                }
            }

            return null;
        }
    }
}
