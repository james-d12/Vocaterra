using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M64 (a) widened O3, O4 and O5: since the review of <c>feature/village</c> (R1) the analyzer reads a subject pronoun joined by <c>y</c> to members before it that open
/// their clause as the end of one plural subject (<c>luz y yo fuimos</c>), and the realiser models it (<c>SentenceRealizer.CoordinationBefore</c>) instead of agreeing the
/// verb with the last pronoun. The oracles draw these shapes rarely (a random pattern with a conjunction before a pronoun and a verb), so this test realises them from
/// patterns that have them and checks O3's and O4's claims on each: every sentence analyzes clean and matches its grammar, the coordinations of each person are reached,
/// and every person or gender mutation of one is flagged as named.
/// </summary>
public sealed class CoordinatedSubjectFuzzTests
{
    private const int SeedsPerPattern = 150;

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    private static readonly string[] Patterns =
    [
        "[PRON] y [PRON] [VERB] [ART] [NOUN:item]",
        "[ART] [NOUN:who] y [PRON] [VERB] [ART] [NOUN:item]",
        "[PRON] y [PRON] [VERB:ser] [ADJ]",
        "[PRON] y [PRON] [VERB:estar] [ADJ]",
        "[ART] [NOUN:who] y [PRON] [VERB:ser] [ADJ]",
        "[WORD:name] y [PRON] [VERB] [ART] [NOUN:item]",
        "[PRON] [VERB] [ART] [NOUN:item] y [PRON] [VERB] [ART] [NOUN:other]",
        "pero [PRON] y [PRON] [VERB] [ART] [NOUN:item]",
    ];

    [Fact]
    public void CoordinatedSentencesAnalyzeClean_AndTheirMutationsAreFlaggedAsNamed()
    {
        var persons = new HashSet<string>(StringComparer.Ordinal);
        var coordinated = 0;
        var personMutations = 0;
        var genderMutations = 0;
        var options = new SentenceOptions { OptionalProbability = 1.0, SubjectProbability = 1.0, PoliteProbability = 0.0, OneProbability = 0.0 };
        foreach (var text in Patterns)
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

                // O3: the sentence is correct for the analyzer and matches its grammar.
                var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence.Text), [grammar]);
                var errors = analysis.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).ToArray();
                Assert.True(errors.Length == 0, $"{string.Join("; ", errors.Select(static e => $"{e.Tag} '{e.Actual}' -> '{e.Expected}'"))} {sentence.Describe()}");
                Assert.True(analysis.MatchedGrammarId == grammar.Id, $"No match. {sentence.Describe()}");

                var coordination = sentence.Tokens.FirstOrDefault(static t => t.Coordination is not null)?.Coordination;
                if (coordination is null)
                {
                    continue;
                }

                coordinated++;
                persons.Add(coordination.Person);

                // O4: a person mutation of the verb, and a gender mutation of the predicate adjective, are flagged as named.
                foreach (var kind in new[] { MutationKind.ConjPerson, MutationKind.GenderAdjective })
                {
                    var mutation = MutationGen.TryMutate(Harness, sentence, kind, new SeededRng(seed));
                    if (mutation is null)
                    {
                        continue;
                    }

                    if (kind == MutationKind.ConjPerson)
                    {
                        personMutations++;
                    }
                    else
                    {
                        genderMutations++;
                    }

                    var normalized = PipelineHarness.Normalize(mutation.Mutated.Text);
                    var mutated = Harness.Analyzer.Analyze(normalized, [grammar]);
                    var names = UnboundNames.Expect(mutation.Mutated.Tokens, normalized, Harness.Words, mutated.MatchedGrammarId is not null);
                    Assert.NotNull(names);
                    var difference = MutationTagFuzzTests.Compare([.. mutation.Errors, .. names], mutated.Errors);
                    Assert.True(difference is null, $"{difference} {mutation.Describe()}");
                }
            }
        }

        Assert.True(coordinated >= 50, $"Only {coordinated} coordinated subjects were realised.");
        Assert.Equal(["1", "2", "3"], persons.Order(StringComparer.Ordinal));
        Assert.True(personMutations >= 20, $"Only {personMutations} person mutations of a coordinated subject.");
        Assert.True(genderMutations >= 1, "No gender mutation of a predicate adjective after a coordinated subject.");
    }
}
