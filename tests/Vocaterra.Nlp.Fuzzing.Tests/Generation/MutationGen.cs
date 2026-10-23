using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Normalization;

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

        public MutatedSentence? ConjPerson()
        {
            // A grammar with an alternative that has a name ([PRON] [WORD] beside [VERB:freír]) binds the mutated verb as the name when the alternative fits, and a name has no
            // reading to check (seed 1RTZdjQ2tFb2: nosotras freís against ([VERB:freír]|[ART] [NOUN:item]|[PRON] [WORD]) ...; it failed on main before M26b).
            if (sentence.PatternText.Contains("[WORD", StringComparison.Ordinal) && sentence.PatternText.Contains('|', StringComparison.Ordinal))
            {
                return null;
            }

            foreach (var i in Shuffled(Indexes(static t => t is { Role: TokenRole.Verb, Generated: true, Infinitive: false, Tense: not null })))
            {
                var subject = SubjectOf(i);
                if (subject < 0)
                {
                    continue;
                }

                var pronoun = _tokens[subject];
                var verb = _tokens[i];
                var cells = Persons.SelectMany(static p => Numbers.Select(n => (Person: p, Number: n))).ToList();
                rng.Shuffle(cells);
                foreach (var (person, number) in cells)
                {
                    if (person == pronoun.Person && number == pronoun.Number)
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
                        || finite.Any(r => r.Person == pronoun.Person && r.Number == pronoun.Number)
                        || finite.Any(r => r.Lemma != verb.Lemma || r.Tense != verb.Tense))
                    {
                        continue;
                    }

                    var mutated = Replace(i, verb with { Written = form, Norm = form, Person = person, Number = number });
                    return Result(MutationKind.ConjPerson, mutated, [Error(mutated, ErrorTags.ConjPerson, i, i, verb.Norm, form)], sentence.NormalizedText, keepsGrammar: true);
                }
            }

            return null;
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
            foreach (var j in Shuffled(Indexes(static t => t is { Role: TokenRole.Adjective, Generated: true, Head: >= 0 })))
            {
                var adjective = _tokens[j];
                var head = _tokens[adjective.Head];
                if (head.Role != TokenRole.Noun || !head.Generated || ReadsAsBothGenders(head))
                {
                    continue;
                }

                var flipped = head.Gender == LexiconSchema.Masculine ? LexiconSchema.Feminine : LexiconSchema.Masculine;
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
                if (readings.Length == 0 || readings.Any(r => r.Gender is null || r.Gender == head.Gender) || readings[0].Lemma != adjective.Lemma)
                {
                    continue;
                }

                var mutated = Replace(j, adjective with { Written = form, Norm = form, Gender = flipped });
                return Result(MutationKind.GenderAdjective, mutated, [Error(mutated, ErrorTags.GenderAgree, j, j, adjective.Norm, form)], sentence.NormalizedText, keepsGrammar: true);
            }

            return null;
        }

        public MutatedSentence? SerEstar()
        {
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
                if (string.Equals(adjective.Lemma, "próximo", StringComparison.Ordinal) && LexiconCatalog.UpcomingTimeNouns.Contains(_tokens[h].Lemma!))
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
                var before = h - 1;
                while (before >= 0 && _words.Lexicon.Lookup(_tokens[before].Norm) is { Count: > 0 } readingsBefore && readingsBefore.All(static r => r.Pos == LexiconSchema.Adjective))
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

        public MutatedSentence? ConjIrregular()
        {
            foreach (var i in Shuffled(Indexes(static t => t is { Role: TokenRole.Verb, Generated: true, Infinitive: false, Tense: not null })))
            {
                var verb = _tokens[i];
                var regular = harness.RegularParadigm.Conjugate(verb.Lemma!, verb.Person!, verb.Number!, verb.Tense!);
                if (regular is null || regular == verb.Norm)
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

            return null;
        }

        public MutatedSentence? ConjTense()
        {
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
