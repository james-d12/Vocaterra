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

    // (4) M74, the merge gate's O3 seed 8vYtnrmXN4e2 (older than M74: the same recipe gave the same sentence on 45305998): cuánto sois cómoda, with cómoda (the chest
    // of drawers) drawn as the noun after the copula, which the analyzer reads as the adjective cómodo (M69's rule), and a reader too. The recipe no longer realises it.
    private const string HowMuchPattern = "cuánto [VERB:ser] [ART]? [NUM:qty]? [NOUN:item]?";

    [Fact]
    public void TheCaseOfSeed8vYtnrmXN4e2_NoLongerDrawsANounThatReadsAsThePredicateAdjective()
    {
        var pattern = GrammarPattern.Parse(HowMuchPattern);
        var sentence = SentenceGen.Realize(Harness, new SentenceRecipe("catalog", pattern, 1529467400985696UL));
        if (sentence is null)
        {
            return;
        }

        Assert.NotEqual("cuánto sois cómoda", sentence.NormalizedText);
        Assert.Empty(Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence.Text), [SlotGrammarType.Parse(pattern.Render())]).Errors);
    }

    // Why the realiser may not build it: right after the copula the analyzer reads the word as the predicate adjective, with or without the grammar that binds it as
    // a noun, and agrees it with the copula; where it agrees there is no error.
    [Theory]
    [InlineData("cuánto sois cómoda", HowMuchPattern, "noun.number")]
    [InlineData("cuánto sois cómoda", "", "noun.number")]
    [InlineData("cuánto eres claras", HowMuchPattern, "noun.number")]
    [InlineData("cuánto sois cómodas", HowMuchPattern, "")]
    [InlineData("cuánto es la cómoda", HowMuchPattern, "")]
    public void ANounThatIsAlsoACommonAdjectiveRightAfterACopulaIsReadAsThePredicate(string sentence, string pattern, string tags)
    {
        var grammars = pattern.Length == 0 ? new List<SlotGrammarType>() : [SlotGrammarType.Parse(pattern)];
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence), grammars);
        Assert.Equal(tags, string.Join(';', analysis.Errors.Select(static e => e.Tag)));
    }

    // Over many draws of the pattern, no realised sentence puts such a noun right after the copula, and each one analyzes clean under its grammar (before the fix,
    // seeds 1 to 4000 gave cuánto eres claras).
    [Fact]
    public void TheHowMuchPatternNoLongerRealisesAPredicateAdjectiveHomographAfterTheCopula()
    {
        var pattern = GrammarPattern.Parse(HowMuchPattern);
        var grammar = SlotGrammarType.Parse(pattern.Render());
        var realised = 0;
        for (var seed = 1UL; seed <= 4000; seed++)
        {
            var sentence = SentenceGen.Realize(Harness, new SentenceRecipe("catalog", pattern, seed));
            if (sentence is null)
            {
                continue;
            }

            realised++;
            var errors = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence.Text), [grammar]).Errors.Where(static e => !e.Tag.StartsWith("pron.", StringComparison.Ordinal));
            Assert.True(!errors.Any(), $"seed {seed}: '{sentence.NormalizedText}' gave {string.Join(", ", errors.Select(static e => e.Tag + " " + e.Expected))}");
        }

        Assert.True(realised > 3000, $"Only {realised} of 4000 draws were realised.");
    }

    // (5) M91, the merge gate's O4 seed 3m67O5Rpuya2: vosotras podáis rastrear opero, podar before an infinitive. The ConjPerson mutation lands on podo, which the analyzer
    // reads as poder's slip before an infinitive (M69), so the mutation is another sentence than the one it was built from. The realiser no longer draws podar there.
    private const string PodarPattern = "[VERB:podar:item] [VERB:rastrear]? [VERB:operar:qty]";

    private static readonly string[] PodarForms = ["podo", "podas", "poda", "podamos", "podáis", "podan", "podes", "pode", "podé", "podés"];

    [Fact]
    public void TheCaseOfSeed3m67O5Rpuya2_NoLongerDrawsPodarBeforeAnInfinitive()
    {
        var mutation = MutationGen.Build(Harness, new MutationCase(MutationKind.ConjPerson, 1735878));
        if (mutation is null)
        {
            return;
        }

        Assert.NotEqual("vosotras podáis rastrear opero", mutation.Source.NormalizedText);
    }

    // Why: podo before an infinitive is poder's slip, a single word with a person error and the missed stem change, whatever the person before it; podo before an object is podar's.
    [Theory]
    [InlineData("vosotras podo rastrear opero", "conj.person;conj.irregular")]
    [InlineData("vosotras podáis rastrear opero", "")]
    [InlineData("vosotras podo la espada", "conj.person")]
    public void ThePodoFormBeforeAnInfinitiveIsPoderAndOneBeforeAnObjectIsPodar(string sentence, string tags)
    {
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence), [SlotGrammarType.Parse(PodarPattern)]);
        Assert.Equal(tags, string.Join(';', analysis.Errors.Select(static e => e.Tag)));
    }

    // The failing pattern has no draw left (podar is always followed by an infinitive in it); this one draws podar before an infinitive or an object, and keeps the second.
    [Fact]
    public void ThePodarPatternNoLongerRealisesAFormOfPodarBeforeAnInfinitive()
    {
        var pattern = GrammarPattern.Parse("[VERB:podar] ([VERB:rastrear]|[ART] [NOUN:item])");
        var realised = 0;
        for (var seed = 1UL; seed <= 1000; seed++)
        {
            var sentence = SentenceGen.Realize(Harness, new SentenceRecipe("catalog", pattern, seed));
            if (sentence is null)
            {
                continue;
            }

            realised++;
            var words = sentence.NormalizedText.Split(' ');
            for (var i = 0; i + 1 < words.Length; i++)
            {
                Assert.False(Array.IndexOf(PodarForms, words[i]) >= 0 && words[i + 1] == "rastrear", $"seed {seed}: '{sentence.NormalizedText}'");
            }
        }

        Assert.True(realised > 100, $"Only {realised} of 1000 draws were realised.");
    }

    // (6) M97, O5 seed 2LxdNIrNHnQ6 (found on feature/biomes, 2026-11-16): cuanto almuerzos veintiocho señoras transportan, with cuanto (a noun, and the determiner cuánto)
    // drawn for [NOUN:item] right before the noun almuerzos. Clean through its grammar; once a mutation puts an unknown word where the grammar needs a noun, nothing
    // matches and cuanto is almuerzos' determiner (noun.number, almuerzo), and the recast loses the plural. A reader takes it that way too. The realiser no longer draws it.
    private const string CuantoPattern = "[NOUN:item] [NOUN:qty] [NUM] [NOUN:spell] [VERB:transportar]";

    [Fact]
    public void TheCaseOfSeed2LxdNIrNHnQ6_NoLongerDrawsANounThatReadsAsTheDeterminerOfTheNextNoun()
    {
        var mutation = MutationGen.Build(Harness, new MutationCase(MutationKind.VocabUnknown, 625984043339709867));
        if (mutation is null)
        {
            return;
        }

        Assert.NotEqual("cuanto almuerzos veintiocho señoras transportan", mutation.Source.NormalizedText);
    }

    // Why: the grammar binds cuanto as a noun, so the sentence is clean; with an unknown word in a noun's place nothing binds it and cuanto asks for the singular.
    // The neighbours stay as they are: the plural after a number, and cuántos before a plural noun, are clean.
    [Theory]
    [InlineData("cuanto almuerzos 28 señoras transportan", "")]
    [InlineData("cuanto almuerzos 28 zeponefu transportan", "noun.number;vocab.unknown")]
    [InlineData("tengo 28 almuerzos", "")]
    [InlineData("tengo 28 zeponefu almuerzos", "vocab.unknown")]
    [InlineData("cuantos almuerzos 28 señoras transportan", "")]
    public void ANounThatIsAlsoADeterminerBeforeANounIsItsDeterminerWhenNothingBindsIt(string sentence, string tags)
    {
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence), [SlotGrammarType.Parse(CuantoPattern), SlotGrammarType.Parse("tengo [NUM:qty] [NOUN:item]")]);
        Assert.Equal(tags, string.Join(';', analysis.Errors.Select(static e => e.Tag)));
    }

    // Over many draws of the pattern, no realised sentence puts such a noun right before another noun, and each one analyzes clean under its grammar.
    [Fact]
    public void TheCuantoPatternNoLongerRealisesANounThatReadsAsADeterminer()
    {
        var pattern = GrammarPattern.Parse(CuantoPattern);
        var grammar = SlotGrammarType.Parse(pattern.Render());
        var realised = 0;
        for (var seed = 1UL; seed <= 20000; seed++)
        {
            var sentence = SentenceGen.Realize(Harness, new SentenceRecipe("catalog", pattern, seed));
            if (sentence is null)
            {
                continue;
            }

            realised++;
            var words = sentence.NormalizedText.Split(' ');
            for (var i = 0; i + 1 < words.Length; i++)
            {
                Assert.False(words[i] == "cuanto" && !char.IsDigit(words[i + 1][0]), $"seed {seed}: '{sentence.NormalizedText}'");
            }

            var errors = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence.Text), [grammar]).Errors;
            Assert.True(errors.Count == 0, $"seed {seed}: '{sentence.NormalizedText}' gave {string.Join(", ", errors.Select(static e => e.Tag))}");
        }

        Assert.True(realised > 5000, $"Only {realised} of 20000 draws were realised.");
    }

    // I-fuzz (PR #71's CI, seed 1892 of the test above): «sueño biblias 1 cuanto transporta por favor» is clean, but M97's re-roll covered only a next noun, so cuanto
    // stayed in the noun pool and was drawn before a verb once the inventory content moved the pool. A noun that reads as the quantifier cuánto is now not in the pool.
    [Fact]
    public void TheNounCuantoIsNoLongerInTheNounPool()
    {
        Assert.DoesNotContain(Harness.Words.Nouns, static n => n.Lemma is "cuanto" or "cuánto");
        Assert.DoesNotContain(Harness.Words.PlainNouns, static n => n.Lemma is "cuanto" or "cuánto");
        Assert.True(Harness.Words.Diagnostics.GetValueOrDefault("nouns:excluded-reads-as-article-numeral-or-determiner") > 0);
    }

    // (7) M98, O3 seed 7q7l8dSNG6n6 (found on feature/biomes, 2026-11-17): [ADV]? [NOUN:item]? [VERB:aclarar] [VERB] realised «alrededor aclara construir» with alrededor
    // (a masculine noun, «los alrededores», and an adverb) as the item noun. The matcher gives the word to the optional [ADV] (clean either way) and yields no item slot.
    // The lexicon's noun reading is right and the analyzer's binding is a valid one, so the fault is the realiser's expectation: a noun that is also an adverb is
    // re-rolled in a pattern with an optional or grouped [ADV] (skip:noun-reads-as-adverb). The engine and the Spanish pack are unchanged.
    private const string AdverbPattern = "[ADV]? [NOUN:item]? [VERB:aclarar] [VERB]";

    private static Dictionary<string, string> SlotsOf(string sentence, string pattern)
    {
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence), [SlotGrammarType.Parse(pattern)]);
        Assert.Empty(analysis.Errors);
        return analysis.Slots.ToDictionary(static s => s.Name, static s => s.Value, StringComparer.Ordinal);
    }

    // Why: with an optional [ADV] the word is the adverb and no item is yielded; without one, or for a noun with no adverb reading, it is the item.
    [Fact]
    public void ANounThatIsAlsoAnAdverbIsTheAdverbWhenThePatternHasAnOptionalAdverb()
    {
        Assert.Empty(SlotsOf("alrededor aclara construir", AdverbPattern));
        Assert.Equal("casa", SlotsOf("casa aclara construir", AdverbPattern)["item"]);
        Assert.Equal("alrededor", SlotsOf("alrededor aclara construir", "[NOUN:item]? [VERB:aclarar] [VERB]")["item"]);
        Assert.Equal("alrededor", SlotsOf("hoy alrededor aclara construir", AdverbPattern)["item"]);
    }

    [Fact]
    public void TheAdverbPatternNoLongerRealisesANounThatIsAlsoAnAdverb_AndEachDrawYieldsItsSlots()
    {
        var pattern = GrammarPattern.Parse(AdverbPattern);
        var grammar = SlotGrammarType.Parse(pattern.Render());
        var realised = 0;
        var withItem = 0;
        for (var seed = 1UL; seed <= 5000; seed++)
        {
            var sentence = SentenceGen.Realize(Harness, new SentenceRecipe("catalog", pattern, seed));
            if (sentence is null)
            {
                continue;
            }

            realised++;
            var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence.Text), [grammar]);
            Assert.True(analysis.Errors.Count == 0, $"seed {seed}: '{sentence.NormalizedText}' gave {string.Join(", ", analysis.Errors.Select(static e => e.Tag))}");
            var slots = analysis.Slots.ToDictionary(static s => s.Name, static s => s.Value, StringComparer.Ordinal);
            Assert.True(
                slots.Count == sentence.ExpectedSlots.Count && sentence.ExpectedSlots.All(pair => slots.TryGetValue(pair.Key, out var value) && value == pair.Value),
                $"seed {seed}: '{sentence.NormalizedText}' expected {{{string.Join(", ", sentence.ExpectedSlots.Select(p => $"{p.Key}={p.Value}"))}}} got {{{string.Join(", ", slots.Select(p => $"{p.Key}={p.Value}"))}}}");
            withItem += slots.ContainsKey("item") ? 1 : 0;
        }

        Assert.True(realised > 4000 && withItem > 500, $"Only {realised} of 5000 draws were realised, {withItem} with an item.");
    }

    // The replay of the failing seed through the oracle's own path: its recipe is realised, or skipped, and never as alrededor with an item.
    [Fact]
    public void TheNeighbourPatternWithNoAdverbStillRealisesNounsThatAreAlsoAdverbs()
    {
        var pattern = GrammarPattern.Parse("[NOUN:item]? [VERB:aclarar] [VERB]");
        var drawn = false;
        for (var seed = 1UL; seed <= 20000 && !drawn; seed++)
        {
            drawn = SentenceGen.Realize(Harness, new SentenceRecipe("catalog", pattern, seed)) is { } s && s.NormalizedText.StartsWith("alrededor ", StringComparison.Ordinal);
        }

        Assert.True(drawn, "A noun that is also an adverb is no longer drawn where the pattern has no [ADV]: the re-roll is too broad.");
    }
}
