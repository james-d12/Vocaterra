using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M65, the three narrowings of O4 and O5 the owner allowed on 2026-11-01 (README, "Where an oracle is narrower than the plan"). Each fact replays the pattern of the
/// failing seed and shows that the guard is needed (what the mutation would build, the analyzer rightly reads another way) and how narrow it is: a close neighbour
/// without the property is still mutated, and every mutation made is flagged as named (O4's comparison).
/// </summary>
public sealed class NarrowedMutationGuardTests
{
    private const int Seeds = 200;

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    private static readonly SentenceOptions TenseOptions = new() { OptionalProbability = 1.0, PreteriteProbability = 0.0, PoliteProbability = 0.0 };

    private static readonly SentenceOptions PersonOptions = new() { OptionalProbability = 1.0, SubjectProbability = 1.0, PoliteProbability = 0.0 };

    // (a) Seed egVgLELrxyL4: ayer presiono abusar against ([VERB:presionar]|[ADV] [WORD:item]|[VERB]) [VERB:abusar]. With ayer in front, [ADV] [WORD:item] takes ayer and
    // the verb, so the verb is a name, which has no reading to check. The tense mutation shares M26b's guard (a grammar with both an alternation and a name); the same
    // pattern with a noun where the name was is still mutated.
    [Fact]
    public void A_TheTenseMutationIsNotMadeBesideANameInAnAlternation_ButIsMadeBesideANoun()
    {
        var guarded = 0;
        foreach (var sentence in Realised("([VERB:presionar]|[ADV] [WORD:item]|[VERB]) [VERB:abusar]", TenseOptions))
        {
            if (sentence.Tokens[0] is not { Role: TokenRole.Verb, Infinitive: false, Tense: not null } verb)
            {
                continue;
            }

            // What the mutation would build: the analyzer binds the verb as the name and checks nothing on it.
            var analysis = Analyze("ayer " + sentence.NormalizedText, sentence.PatternText);
            Assert.True(analysis.MatchedGrammarId is not null, $"ayer {sentence.NormalizedText}: no match");
            Assert.DoesNotContain(analysis.Errors, e => e.Tag == ErrorTags.ConjTense && e.Actual == verb.Norm);
            Assert.Null(MutationGen.TryMutate(Harness, sentence, MutationKind.ConjTense, new SeededRng(1)));
            guarded++;
        }

        Assert.True(guarded > 0, "The guarded pattern never realised a finite verb in its first place.");
        Assert.True(MutatedAndFlagged("([VERB:presionar]|[ADV] [NOUN:item]|[VERB]) [VERB:abusar]", MutationKind.ConjTense, TenseOptions) > 0, "The neighbour was never mutated.");
    }

    // (b) Seeds b3HSKSLCsL_1 (el subtítulo ha peligroso made han) and 4YuZ2yubxdC1 (unas televisiones han made ha). The analyzer leaves haber out of M59's check after
    // a noun subject by design (hay dos panes), so the person mutation after a noun subject leaves it out too; the same pattern with estar is still mutated.
    [Fact]
    public void B_ThePersonMutationAfterANounSubjectLeavesHaberOut_ButNotEstar()
    {
        var guarded = 0;
        foreach (var sentence in Realised("[ART] [NOUN:item] [VERB:haber] [ADJ]", PersonOptions))
        {
            if (sentence.Tokens[2] is not { Lemma: "haber", Person: "3", Number: { } number, Tense: { } tense } verb)
            {
                continue;
            }

            // What the mutation would build: the other number, which the analyzer does not check after a noun subject.
            var other = Harness.Words.Morphology.Conjugate("haber", "3", number == "sg" ? "pl" : "sg", tense)!;
            var words = sentence.Tokens.Select(static t => t.Norm).ToArray();
            words[2] = other;
            var analysis = Analyze(string.Join(' ', words), sentence.PatternText);
            Assert.DoesNotContain(analysis.Errors, static e => e.Tag == ErrorTags.ConjPerson);
            var mutation = MutationGen.TryMutate(Harness, sentence, MutationKind.ConjPerson, new SeededRng(1));
            Assert.True(mutation is null, $"{verb.Norm} was mutated: {mutation?.Describe()}");
            guarded++;
        }

        Assert.True(guarded > 0, "The guarded pattern never realised haber.");
        Assert.True(MutatedAndFlagged("[ART] [NOUN:item] [VERB:estar] [ADJ]", MutationKind.ConjPerson, PersonOptions) > 0, "The neighbour was never mutated.");
    }

    // (c) Seed 2ldq2rIKLrG2: las los despego unas porque against [ART] [ART]? [VERB]? [ART]? porque?. The grammar binds porque as typed, the conjunction; with ayer in
    // front it no longer matches, and the analyzer reads unas porque as the article and the noun porqué. The tense mutation keeps a sentence with a literal that is
    // another word once its accent is written only when the mutated sentence still matches the grammar: the same pattern with a literal of no accent variant (aunque),
    // drawn from the same seeds, and the same pattern with an adverb in front that takes ayer, are still mutated.
    [Fact]
    public void C_TheTenseMutationNeedsTheGrammarToMatch_OnlyBesideALiteralWithAnAccentVariant()
    {
        const string Guarded = "[ART] [ART]? [VERB]? [ART]? porque?";
        var failing = Analyze("ayer las los despego unas porque", Guarded);
        Assert.Null(failing.MatchedGrammarId);
        Assert.Contains(failing.Errors, static e => e.Tag != ErrorTags.ConjTense);
        Assert.NotEmpty(Harness.Words.Lexicon.LookupWithAccentVariants("porque").Variants);
        Assert.Empty(Harness.Words.Lexicon.LookupWithAccentVariants("aunque").Variants);

        var guarded = 0;
        foreach (var sentence in Realised(Guarded, TenseOptions))
        {
            if (sentence.Tokens is [.., { Role: TokenRole.Verb, Infinitive: false, Tense: not null }, _, { Norm: "porque" }])
            {
                Assert.Null(MutationGen.TryMutate(Harness, sentence, MutationKind.ConjTense, new SeededRng(1)));
                guarded++;
            }
        }

        Assert.True(guarded > 0, "The guarded pattern never realised a verb and porque.");
        Assert.True(MutatedAndFlagged("[ART] [ART]? [VERB]? [ART]? aunque?", MutationKind.ConjTense, TenseOptions) > 0, "The neighbour with aunque was never mutated.");
        var halfOptional = TenseOptions with { OptionalProbability = 0.5 };
        Assert.True(MutatedAndFlagged("[ADV]? [ART] [ART]? [VERB]? [ART]? porque?", MutationKind.ConjTense, halfOptional) > 0, "The neighbour that takes ayer was never mutated.");
    }

    private static IEnumerable<GeneratedSentence> Realised(string pattern, SentenceOptions options)
    {
        var parsed = GrammarPattern.Parse(pattern);
        for (var seed = 1UL; seed <= Seeds; seed++)
        {
            if (SentenceGen.Realize(Harness, new SentenceRecipe("skeleton", parsed, seed), options) is { } sentence)
            {
                yield return sentence;
            }
        }
    }

    private static AnalysisResult Analyze(string text, string pattern)
    {
        return Harness.Analyzer.Analyze(PipelineHarness.Normalize(text), [SlotGrammarType.Parse(pattern)]);
    }

    // Mutates every realisation of the pattern that admits the mutation and checks O4's comparison on each; returns how many were mutated.
    private static int MutatedAndFlagged(string pattern, MutationKind kind, SentenceOptions options)
    {
        var mutated = 0;
        var seed = 0UL;
        foreach (var sentence in Realised(pattern, options))
        {
            var mutation = MutationGen.TryMutate(Harness, sentence, kind, new SeededRng(++seed));
            if (mutation is null)
            {
                continue;
            }

            var normalized = PipelineHarness.Normalize(mutation.Mutated.Text);
            var analysis = Harness.Analyzer.Analyze(normalized, [SlotGrammarType.Parse(pattern)]);
            var names = UnboundNames.Expect(mutation.Mutated.Tokens, normalized, Harness.Words, analysis.MatchedGrammarId is not null);
            Assert.NotNull(names);
            var difference = MutationTagFuzzTests.Compare([.. mutation.Errors, .. names], analysis.Errors);
            Assert.True(difference is null, $"{difference} {mutation.Describe()}");
            mutated++;
        }

        return mutated;
    }
}
