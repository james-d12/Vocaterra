using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M73, three sentences the realiser built as correct that the analyzer, and a reader, take another way; each is now re-rolled, a generator fix and not a narrowing
/// of an oracle (README, "Found and fixed in M73"). (1) V27's O5 seed <c>26dVDqElv1l2</c>: <c>estómago son vencido</c>, with the noun <c>son</c> (a tune) right after a
/// noun and before an adjective, where the analyzer reads the copula (M62's and M69's rules) and a reader does too (<c>skip:copula-homograph</c>). (2) M65's O4 seed
/// <c>8yc522XLB_W2</c>: <c>la joya unida rica desfilaron perdido</c>, with <c>rica</c> drawn as a noun past the adjective of <c>joya</c>, which a reader takes as
/// <c>joya</c>'s second adjective (M62's <c>skip:noun-reads-as-adjective</c>, now past the head's adjectives). (3) M72's O3 seed <c>92moRWz9NQ54</c>: <c>largáis suyo
/// involucras deportar les</c>, with <c>suyo</c> drawn for <c>[ADJ:item]</c> in <c>([PRON]|[VERB]|[ADJ:item])</c>: the matcher keeps the first of tied parses, the
/// pronoun, and yields no <c>item</c> (<c>skip:alternative-homograph</c>).
/// </summary>
public sealed class HomographRerollTests
{
    private const string CopulaPattern = "([NOUN:item] [NOUN:qty]|[NOUN:spell]|[NOUN:thing] [PRON]) [NOUN:who] [ADJ]";
    private const string AdjectivePattern = "([ART] [NOUN:item]|[VERB]|[VERB:encargar]) [ADJ] [NOUN:qty]? ([VERB:desfilar:spell] [ADJ]|[ART])?";
    private const string AlternativePattern = "[VERB:largar] ([PRON]|[VERB]|[ADJ:item]) [VERB]? [VERB:deportar] [PRON]?";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    // (1) The mutation case of the failing seed: its source sentence is no longer estómago son vencido, and its recast closes the loop.
    [Fact]
    public void TheCaseOfSeed26dVDqElv1l2_NoLongerDrawsTheNounSonBeforeAnAdjective()
    {
        var mutation = MutationGen.Build(Harness, new MutationCase(MutationKind.GenderAdjective, 671827055));
        if (mutation is null)
        {
            return;
        }

        Assert.NotEqual("estómago son vencido", mutation.Source.NormalizedText);
    }

    // Why the realiser may not build it: the analyzer reads the copula, with or without the grammar that binds son as a noun, and so does a reader.
    [Theory]
    [InlineData("estómago son vencido", CopulaPattern, "conj.person")]
    [InlineData("estómago son vencido", "", "conj.person")]
    [InlineData("estómago es vencido", "", "")]
    public void TheNounSonBetweenANounAndAnAdjectiveIsTheCopula(string sentence, string pattern, string tags)
    {
        var grammars = pattern.Length == 0 ? new List<SlotGrammarType>() : [SlotGrammarType.Parse(pattern)];
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence), grammars);
        Assert.Equal(tags, string.Join(';', analysis.Errors.Select(static e => e.Tag)));
    }

    // Over many draws of the copula pattern, no realised sentence puts a noun that is also a copula's form right after a noun and before an adjective, and each one
    // analyzes clean under its grammar.
    [Fact]
    public void TheCopulaPatternNoLongerRealisesTheHomographThere()
    {
        var pattern = GrammarPattern.Parse(CopulaPattern);
        var grammar = SlotGrammarType.Parse(pattern.Render());
        var realised = 0;
        for (var seed = 1UL; seed <= 300; seed++)
        {
            var sentence = SentenceGen.Realize(Harness, new SentenceRecipe("random", pattern, seed));
            if (sentence is null)
            {
                continue;
            }

            realised++;
            var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence.Text), [grammar]);
            Assert.True(analysis.Errors.Count == 0, $"{sentence.Text}: {string.Join("; ", analysis.Errors.Select(static e => e.Tag))}");
        }

        Assert.True(realised > 0, "The copula pattern realised nothing in 300 seeds.");
    }

    // (2) The mutation case of the recorded seed: its source sentence is no longer la joya unida rica desfilaron perdido.
    [Fact]
    public void TheCaseOfSeed8yc522XLB_W2_NoLongerDrawsANounThatReadsAsTheAdjectiveOfThePhrase()
    {
        var mutation = MutationGen.Build(Harness, new MutationCase(MutationKind.WordOrder, 670401880765));
        if (mutation is null)
        {
            return;
        }

        Assert.NotEqual("la joya unida rica desfilaron perdido", mutation.Source.NormalizedText);
    }

    // Why: without the grammar's binding of rica as a noun (which the word-order mutation breaks), rica right after joya is its adjective and the verb disagrees.
    [Theory]
    [InlineData("la joya unida rica desfilaron perdido", AdjectivePattern, "")]
    [InlineData("la unida joya rica desfilaron perdido", AdjectivePattern, "word_order;conj.person")]
    public void ANounAfterTheAdjectivesOfAPhraseIsReadAsOneOfThemWhenNothingBindsIt(string sentence, string pattern, string tags)
    {
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence), [SlotGrammarType.Parse(pattern)]);
        Assert.Equal(tags, string.Join(';', analysis.Errors.Select(static e => e.Tag)));
    }

    // (3) The recipe of the failing case: realised now, it is no longer the sentence that yields no slot, and its slots are those the realiser expects.
    [Fact]
    public void TheCaseOfSeed92moRWz9NQ54_YieldsTheSlotsTheRealiserExpects()
    {
        var pattern = GrammarPattern.Parse(AlternativePattern);
        var sentence = SentenceGen.Realize(Harness, new SentenceRecipe("random", pattern, 4372495956647UL));
        if (sentence is null)
        {
            return;
        }

        Assert.NotEqual("largáis suyo involucras deportar les", sentence.NormalizedText);
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence.Text), [SlotGrammarType.Parse(pattern.Render())]);
        Assert.Equal(
            string.Join(',', sentence.ExpectedSlots.OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => $"{p.Key}={p.Value}")),
            string.Join(',', analysis.Slots.OrderBy(static s => s.Name, StringComparer.Ordinal).Select(static s => $"{s.Name}={s.Value}")));
    }

    // Why: the tied parses read suyo as the pronoun and as the adjective, and the matcher keeps the first, which fills no slot.
    [Fact]
    public void TheMatcherKeepsTheFirstOfTiedAlternatives()
    {
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize("largáis suyo involucras deportar les"), [SlotGrammarType.Parse(AlternativePattern)]);
        Assert.Empty(analysis.Errors);
        Assert.Empty(analysis.Slots);
    }
}
