using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M64 (b), the one narrowing the owner's request to fix the review of <c>feature/village</c> allows (2026-11-01; README, "Where an oracle is narrower than the plan"):
/// the analyzer reads an amount before a singular <c>ser</c> with a singular predicate as one sum (<c>dos niños es un problema</c>, <c>diez monedas es mucho dinero</c>),
/// so M59's <c>conj.person</c> mutation after a noun subject is not made on that case. This test shows that the guard is needed (the analyzer finds the singular form
/// there correct), that the mutation is still made before a predicate of the plural, and that every mutation the pattern gets is flagged as named.
/// </summary>
public sealed class AmountMutationGuardTests
{
    private const string Pattern = "[NUM:qty] [NOUN:who] [VERB:ser] [ART] [NOUN:what]";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void AnAmountBeforeASingularSerIsNotMutated_AndTheOtherMutationsAreFlagged()
    {
        var pattern = GrammarPattern.Parse(Pattern);
        var grammar = SlotGrammarType.Parse(pattern.Render());
        var options = new SentenceOptions { OptionalProbability = 1.0, SubjectProbability = 1.0, PoliteProbability = 0.0, OneProbability = 0.0, PreteriteProbability = 0.5 };
        var guarded = 0;
        var mutated = 0;
        for (var seed = 1UL; seed <= 200; seed++)
        {
            var sentence = SentenceGen.Realize(Harness, new SentenceRecipe("skeleton", pattern, seed), options);
            if (sentence is not { Tokens: [{ Role: TokenRole.Numeral, Number: "pl" }, _, { Lemma: "ser", Number: "pl" } verb, var article, ..] })
            {
                continue;
            }

            // Before a predicate of the singular (dos niños son un problema), the singular form the mutation would make is right for the analyzer: an amount read as one
            // sum. Before one of the plural (ocho embajadores son unos poemas) the mutation is made as before.
            var singular = Harness.Words.Morphology.Conjugate("ser", "3", "sg", verb.Tense!)!;
            var sum = article.Head > 2 && sentence.Tokens[article.Head].Number == "sg";
            if (sum)
            {
                var words = sentence.Tokens.Select(static t => t.Written).ToArray();
                words[2] = singular;
                var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(string.Join(' ', words)), [grammar]);
                Assert.True(analysis.Errors.Count == 0, $"{string.Join(' ', words)}: {string.Join("; ", analysis.Errors.Select(static e => e.Tag))}");
                guarded++;
            }

            var mutation = MutationGen.TryMutate(Harness, sentence, MutationKind.ConjPerson, new SeededRng(seed));
            if (mutation is null)
            {
                continue;
            }

            if (sum)
            {
                Assert.NotEqual(singular, mutation.Mutated.Tokens[2].Norm);
            }
            else
            {
                mutated++;
            }

            var normalized = PipelineHarness.Normalize(mutation.Mutated.Text);
            var result = Harness.Analyzer.Analyze(normalized, [grammar]);
            var names = UnboundNames.Expect(mutation.Mutated.Tokens, normalized, Harness.Words, result.MatchedGrammarId is not null);
            Assert.NotNull(names);
            var difference = MutationTagFuzzTests.Compare([.. mutation.Errors, .. names], result.Errors);
            Assert.True(difference is null, $"{difference} {mutation.Describe()}");
        }

        Assert.True(guarded > 0, "No amount before ser and a singular predicate was realised in 200 seeds.");
        Assert.True(mutated > 0, "No person mutation before a plural predicate was made in 200 seeds.");
    }
}
