using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Lexicon;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>Everything that decides a generated sentence: the pattern and the seed of the realiser. This is the generated input oracles print.</summary>
/// <param name="Source">Where the pattern came from: <c>catalog</c>, <c>skeleton</c> or <c>random</c>.</param>
/// <param name="Pattern">The pattern tree.</param>
/// <param name="Seed">The seed of the realiser's random stream.</param>
public sealed record SentenceRecipe(string Source, GrammarPattern Pattern, ulong Seed)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Source} [{Pattern.Render()}] seed={Seed}";
    }
}

/// <summary>
/// Grammar-driven correct sentences: a pattern from <see cref="PatternGen"/> is realised with <c>MorphologyGenerator</c> so that agreement is
/// right by construction. A pattern that cannot be realised correctly from the lexicon is skipped and counted in the diagnostics.
/// </summary>
public static class SentenceGen
{
    /// <summary>Draws a recipe (pattern plus realiser seed).</summary>
    public static Gen<SentenceRecipe> Recipe(IGeneratorInputs harness)
    {
        return Gen.Select(PatternGen.Any(harness), Gen.ULong, static (pattern, seed) => new SentenceRecipe(pattern.Source, pattern.Pattern, seed));
    }

    /// <summary>
    /// Realises a recipe, or returns null when the pattern cannot be realised correctly from the lexicon (the reason is counted in
    /// <paramref name="diagnostics"/>). Throws an oracle failure when the DSL round trip or the normalizer disagrees with the realiser.
    /// </summary>
    public static GeneratedSentence? Realize(IGeneratorInputs harness, SentenceRecipe recipe, SentenceOptions? options = null, FuzzDiagnostics? diagnostics = null)
    {
        var rendered = recipe.Pattern.Render();
        SlotGrammarType compiled;
        try
        {
            compiled = SlotGrammarType.Parse(rendered);
        }
        catch (FormatException ex)
        {
            throw Determinism.Fail("DSL", $"A generated pattern was rejected by SlotGrammar.Parse: {ex.Message}", recipe.ToString());
        }

        if (!string.Equals(compiled.Pattern, rendered, StringComparison.Ordinal))
        {
            throw Determinism.Fail("DSL", $"Rendering and parsing disagree: rendered '{rendered}' but SlotGrammar.Pattern is '{compiled.Pattern}'.", recipe.ToString());
        }

        var realizer = new SentenceRealizer(harness.Words, new SeededRng(recipe.Seed), options ?? SentenceOptions.Default);
        var sentence = realizer.TryRealize(recipe.Pattern, recipe.Source, out var reason);
        if (sentence is null)
        {
            diagnostics?.Count($"skip:{reason}" + (reason == "unknown-literal" ? $":{realizer.Detail}" : string.Empty));
            return null;
        }

        // A numeral next to un or una reads as one longer number once it is spoken ("cuatrocientos una" is 401, "una mil" is 1000), which is the
        // language and not a defect, so such a sentence is not a usable correct sentence.
        var numeralIndexes = sentence.Tokens.Select((t, i) => (t, i)).Where(static x => x.t.Role == TokenRole.Numeral).Select(static x => x.i).ToArray();
        bool IsUno(int i) => i >= 0 && i < sentence.Tokens.Count && sentence.Tokens[i].Norm is "un" or "una" or "uno";
        if (numeralIndexes.Any(i => IsUno(i + 1) || IsUno(i - 1)))
        {
            diagnostics?.Count("skip:numeral-next-to-article");
            return null;
        }

        // Two numerals in a row are not a sentence: spoken ("un una corona") they are not correct Spanish either.
        if (numeralIndexes.Zip(numeralIndexes.Skip(1)).Any(static pair => pair.Second - pair.First == 1))
        {
            diagnostics?.Count("skip:adjacent-numerals");
            return null;
        }

        var normalized = PipelineHarness.Normalize(sentence.Text);
        if (!normalized.Tokens.Select(static t => t.Text).SequenceEqual(sentence.Tokens.Select(static t => t.Norm), StringComparer.Ordinal))
        {
            // Two numerals that sit close together can legitimately merge into one number ("treinta y cuatro"); anything else is a disagreement.
            if (numeralIndexes.Zip(numeralIndexes.Skip(1)).Any(static pair => pair.Second - pair.First <= 2))
            {
                diagnostics?.Count("skip:close-numerals");
                return null;
            }

            throw Determinism.Fail(
                "generator",
                $"The normalizer disagrees with the realiser: expected tokens [{string.Join(" | ", sentence.Tokens.Select(static t => t.Norm))}] but got [{string.Join(" | ", normalized.Tokens.Select(static t => t.Text))}].",
                sentence.Describe());
        }

        diagnostics?.Count($"sentence:{sentence.PatternSource}");
        CountHomographs(harness, sentence, diagnostics);
        if (recipe.Source == "catalog")
        {
            diagnostics?.Count($"catalog:{rendered}");
        }

        return sentence;
    }

    // Counts the sentences that hold a generated word the lexicon reads in more than one way, so a report shows that the homographs are generated and not only allowed:
    // a noun that is also a verb form, a verb form that is also a noun, a noun that is also a noun of the other gender.
    private static void CountHomographs(IGeneratorInputs harness, GeneratedSentence sentence, FuzzDiagnostics? diagnostics)
    {
        if (diagnostics is null)
        {
            return;
        }

        var nounAlsoVerb = false;
        var verbAlsoNoun = false;
        var nounBothGenders = false;
        foreach (var token in sentence.Tokens.Where(static t => t.Generated))
        {
            if (token.Role == TokenRole.Noun && harness.Words.NounOf(token.Lemma) is { } noun)
            {
                nounAlsoVerb |= noun.AlsoVerb;
                nounBothGenders |= noun.AlsoOtherGender;
            }
            else if (token.Role == TokenRole.Verb)
            {
                verbAlsoNoun |= harness.Words.Lexicon.Lookup(token.Norm).Any(static r => r.Pos == LexiconSchema.Noun);
            }
        }

        if (nounAlsoVerb)
        {
            diagnostics.Count("homograph:sentence-with-a-noun-that-is-also-a-verb-form");
        }

        if (verbAlsoNoun)
        {
            diagnostics.Count("homograph:sentence-with-a-verb-form-that-is-also-a-noun");
        }

        if (nounBothGenders)
        {
            diagnostics.Count("homograph:sentence-with-a-noun-of-both-genders");
        }
    }

    /// <summary>
    /// Noise that cannot change what the normalizer produces (casing, wrapping punctuation, commas between words, whitespace, NFD), so a
    /// noisy sentence must analyze exactly like the clean one.
    /// </summary>
    public static Gen<TextNoise> SafeNoise { get; } = Gen.Select(
        Gen.Frequency((3, (IGen<int>)Gen.Const(0)), (1, (IGen<int>)Gen.Int[1, 4])),
        Gen.Frequency((3, (IGen<bool>)Gen.Const(false)), (1, (IGen<bool>)Gen.Const(true))),
        Gen.Frequency((3, (IGen<int>)Gen.Const(0)), (1, (IGen<int>)Gen.Int[1, 3])),
        Gen.Frequency((3, (IGen<int>)Gen.Const(0)), (1, (IGen<int>)Gen.Int[1, 3])),
        Gen.Int,
        static (caseMode, nfd, punctuation, spaces, seed) => new TextNoise(caseMode, 0, nfd, punctuation, spaces, 0, -1, seed));
}
