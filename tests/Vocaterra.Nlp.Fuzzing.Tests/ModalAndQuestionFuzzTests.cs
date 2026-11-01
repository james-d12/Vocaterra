using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M66 widened O3, O4 and O5 twice. (a) A finite verb right after a modal (<c>puedo pago</c>, <c>tengo que voy</c>) is the learner's slip for the infinitive, which the
/// analyzer reports as <c>conj.tense</c>; the realiser makes the verb after <c>tener que</c> an infinitive, as after a modal and <c>ir a</c>, and the tense mutation
/// makes the slip on every other sentence with a modal and its infinitive. (b) A subject pronoun before a question word (<c>ella qué quiere</c>) is the subject of
/// the verb after it (M63); the realiser agrees that verb with it, and the person mutation makes a first or second person there. The oracles draw these shapes rarely,
/// so this test realises them from patterns that have them and checks O3's and O4's claims on each, as <see cref="CoordinatedSubjectFuzzTests"/> does.
/// </summary>
public sealed class ModalAndQuestionFuzzTests
{
    private const int SeedsPerPattern = 150;

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    private static readonly string[] ModalPatterns =
    [
        "[VERB:poder] [VERB] [ART] [NOUN:item]",
        "[PRON] [VERB:querer] [VERB] [ART] [NUM]? [NOUN:item]",
        "[VERB:tener] que [VERB] [ART] [NOUN:item]",
        "[VERB:deber] [VERB] [ART] [NOUN:item]",
        "[PRON] [VERB:necesitar] [VERB] [ART] [NOUN:item] [ADV]?",
    ];

    private static readonly string[] QuestionPatterns =
    [
        "[PRON] qué [VERB]",
        "[PRON] qué [VERB] [ART] [NOUN:item]",
        "[PRON] dónde [VERB]",
        "[PRON] cómo [VERB] [ART] [NOUN:item]",
    ];

    [Fact]
    public void ModalSentencesAnalyzeClean_AndTheSlipAfterTheModalIsFlaggedAsNamed()
    {
        var (sentences, mutations) = Run(ModalPatterns, MutationKind.ConjTense, static m => !m.Mutated.Text.StartsWith("ayer ", StringComparison.Ordinal));
        Assert.True(sentences >= 300, $"Only {sentences} sentences with a modal were realised.");
        Assert.True(mutations >= 50, $"Only {mutations} slips after a modal were made.");
    }

    [Fact]
    public void QuestionSentencesAnalyzeClean_AndTheirPersonMutationsAreFlaggedAsNamed()
    {
        var (sentences, mutations) = Run(QuestionPatterns, MutationKind.ConjPerson, static _ => true);
        Assert.True(sentences >= 300, $"Only {sentences} sentences with a pronoun before a question word were realised.");
        Assert.True(mutations >= 50, $"Only {mutations} person mutations through a pronoun before a question word were made.");
    }

    // Realises each pattern from fixed seeds: O3 (each sentence analyzes clean and matches its grammar) and O4 (the mutation of the kind, where it counts, is flagged as
    // named, with the errors a name left unbound adds). Returns the sentences realised and the mutations counted.
    private static (int Sentences, int Mutations) Run(string[] patterns, MutationKind kind, Func<MutatedSentence, bool> counts)
    {
        var sentences = 0;
        var mutations = 0;
        var options = new SentenceOptions { OptionalProbability = 0.5, SubjectProbability = 1.0, PoliteProbability = 0.0, OneProbability = 0.0, PreteriteProbability = 0.0 };
        foreach (var text in patterns)
        {
            var pattern = GrammarPattern.Parse(text);
            var grammar = SlotGrammarType.Parse(pattern.Render());
            for (var seed = 1UL; seed <= SeedsPerPattern; seed++)
            {
                var sentence = SentenceGen.Realize(Harness, new SentenceRecipe("skeleton", pattern, seed), options);
                if (sentence is null)
                {
                    continue;
                }

                sentences++;
                var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence.Text), [grammar]);
                var errors = analysis.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).ToArray();
                Assert.True(errors.Length == 0, $"{string.Join("; ", errors.Select(static e => $"{e.Tag} '{e.Actual}' -> '{e.Expected}'"))} {sentence.Describe()}");
                Assert.True(analysis.MatchedGrammarId == grammar.Id, $"No match. {sentence.Describe()}");

                var mutation = MutationGen.TryMutate(Harness, sentence, kind, new SeededRng(seed));
                if (mutation is null || !counts(mutation))
                {
                    continue;
                }

                mutations++;
                var normalized = PipelineHarness.Normalize(mutation.Mutated.Text);
                var mutated = Harness.Analyzer.Analyze(normalized, [grammar]);
                var names = UnboundNames.Expect(mutation.Mutated.Tokens, normalized, Harness.Words, mutated.MatchedGrammarId is not null);
                Assert.NotNull(names);
                var difference = MutationTagFuzzTests.Compare([.. mutation.Errors, .. names], mutated.Errors);
                Assert.True(difference is null, $"{difference} {mutation.Describe()}");
            }
        }

        return (sentences, mutations);
    }
}
