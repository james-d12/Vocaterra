using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M62 widened O4 and O5: the <c>gender.agree</c> mutation of an adjective (<see cref="MutationKind.GenderAdjective"/>) also flips a predicate adjective whose copula has a
/// subject pronoun of a known gender before it (<c>ella es alta</c> -> <c>alto</c>), which the analyzer agrees with the pronoun since M62. The oracles draw it among the
/// attributive cases; this test shows that the draw reaches it and that each case it builds is one the analyzer flags as named, with the recast the sentence before.
/// </summary>
public sealed class PredicateAdjectiveMutationTests
{
    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void TheGenderMutationReachesPredicateAdjectives_AndTheAnalyzerFlagsEach()
    {
        var reached = 0;
        for (var seed = 1UL; seed <= 3000 && reached < 5; seed++)
        {
            var mutation = MutationGen.Build(Harness, new MutationCase(MutationKind.GenderAdjective, seed));
            if (mutation is null)
            {
                continue;
            }

            var tokens = mutation.Mutated.Tokens;
            var predicate = Enumerable.Range(0, tokens.Count)
                .Any(i => tokens[i] is { Role: TokenRole.Adjective, Head: < 0 } && mutation.Mutated.SpanOf(i, i) == mutation.Errors[0].Span);
            if (!predicate)
            {
                continue;
            }

            reached++;
            var grammar = SlotGrammarType.Parse(mutation.Source.PatternText);
            var normalized = PipelineHarness.Normalize(mutation.Mutated.Text);
            var analysis = Harness.Analyzer.Analyze(normalized, [grammar]);
            var names = UnboundNames.Expect(mutation.Mutated.Tokens, normalized, Harness.Words, analysis.MatchedGrammarId is not null);
            Assert.NotNull(names);
            var difference = MutationTagFuzzTests.Compare([.. mutation.Errors, .. names], analysis.Errors);
            Assert.True(difference is null, $"{difference} {mutation.Describe()}");
        }

        Assert.True(reached > 0, "No gender.agree mutation of a predicate adjective in 3000 seeds.");
    }
}
