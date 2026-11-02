using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M69 widened O3, O4 and O5 with two skeletons and two mutations: <c>[ART] [NOUN:thing] [VERB:estar] en [ART] [NOUN:place]</c>, whose <c>estar</c> the
/// <c>ser_estar</c> mutation makes <c>ser</c> (<c>la casa está en la plaza</c> -> <c>es</c>, which the analyzer reports since M69), and <c>[ART] [NOUN:thing] de [ART]
/// [NOUN:owner] [VERB:ser] [ADJ:quality]</c>, whose predicate adjective the <c>gender.agree</c> mutation flips against the subject with <c>de</c> (<c>la casa de los
/// niños es bonita</c> -> <c>bonito</c>). The oracles draw them among the other cases; this test shows that the draw reaches each, and that each case it builds is one the
/// analyzer flags as named. It also holds the regression of M71's finding (buenas tardes), which M69 fixed in the realiser.
/// </summary>
public sealed class PlaceAndSubjectWithDeMutationTests
{
    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Theory]
    [InlineData(MutationKind.SerEstar, "[ART] [NOUN:thing] [VERB:estar] en [ART] [NOUN:place]")]
    [InlineData(MutationKind.GenderAdjective, "[ART] [NOUN:thing] de [ART] [NOUN:owner] [VERB:ser] [ADJ:quality]")]
    public void TheMutationReachesTheNewShape_AndTheAnalyzerFlagsEach(MutationKind kind, string skeleton)
    {
        var reached = 0;
        for (var seed = 1UL; seed <= 20000 && reached < 5; seed++)
        {
            var mutation = MutationGen.Build(Harness, new MutationCase(kind, seed));
            if (mutation is null || mutation.Source.PatternText != skeleton)
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

        Assert.True(reached > 0, $"No {kind} mutation of the skeleton {skeleton} in 20000 seeds.");
    }

    // M71's finding, fixed by M69: the realiser re-rolled every buenas tardes as literal-verb-subject (tardes is also tardar's, and it took buenas for a subject phrase
    // before it, though tardes is that phrase's own noun), so O3 never drew the greeting the analyzer finds correct. From 400 fixed recipes of the catalog's greeting
    // grammar it now draws all three greetings, and the analyzer finds each clean.
    [Fact]
    public void TheGreetingGrammarRealisesBuenasTardes()
    {
        var pattern = Harness.Grammars.Grammars.Single(static g => g.Id.Contains("tardes", StringComparison.Ordinal) && g.Id.Contains("buenas", StringComparison.Ordinal)).Pattern;
        var drawn = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var seed = 1UL; seed <= 400; seed++)
        {
            if (SentenceGen.Realize(Harness, new SentenceRecipe("catalog", pattern, seed)) is { } sentence)
            {
                drawn[sentence.NormalizedText] = drawn.GetValueOrDefault(sentence.NormalizedText) + 1;
            }
        }

        Assert.True(drawn.ContainsKey("buenas tardes"), $"Drawn: {string.Join(", ", drawn.Select(static d => $"{d.Key} {d.Value}"))}.");
        foreach (var text in drawn.Keys)
        {
            Assert.Empty(Harness.Analyzer.Analyze(PipelineHarness.Normalize(text), []).Errors);
        }
    }
}
