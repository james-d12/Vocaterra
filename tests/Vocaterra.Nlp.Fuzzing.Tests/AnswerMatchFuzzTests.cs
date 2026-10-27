using System.Globalization;
using CsCheck;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using Xunit.Abstractions;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O6: answer matching, checked three ways. Every input is compared with an independent model of the documented rules (exact on a canonical
/// phrase, then on an alias, then the smaller of the spelling and phonetic edit distance against the per-difficulty budget); canonical and alias
/// phrases must match exactly however they are cased, accented or punctuated; and sound-alike spellings must match with the pronunciation flag.
/// </summary>
public sealed class AnswerMatchFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O6";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    // The bundle sets plus one built to collide: an alias that is another answer's canonical phrase, sound-alike phrases, an alias that
    // normalizes to nothing, and numbers.
    private static readonly IReadOnlyList<AnswerSet> Sets =
    [
        .. Harness.AnswerSets,
        new AnswerSet(
            "synthetic-collisions",
            [
                new Answer("a-cow", "vaca", ["baca", "la vaca"], []),
                new Answer("b-wolf", "lobo", ["vaca", "¿?"], []),
                new Answer("c-two", "dos espadas", ["2 espadas", "veinte"], []),
                new Answer("d-ham", "hola", ["ola", "hoya", "olla"], []),
            ]),
    ];

    private static readonly IReadOnlyList<(string SetId, AnswerSet Set, List<Candidate> Candidates)> Models =
        [.. Sets.Select(static set => (set.AnswerSetId, set, BuildCandidates(set)))];

    [Fact]
    public void Match_AgreesWithTheDocumentedModel_AndTheMetamorphicRelations()
    {
        var phrases = Models.SelectMany(static m => m.Candidates.Select(c => (m.SetId, c.Raw, c.IsAlias, c.AnswerId))).ToArray();
        var lexiconWords = Gen.OneOfConst([.. Harness.Words.AllForms]);
        var phraseGen = Gen.OneOfConst(phrases);
        var gen = Gen.Select(
            Gen.Int[0, 3],
            phraseGen,
            Gen.Enum<Difficulty>(),
            SentenceGen.SafeNoise,
            Gen.ULong,
            lexiconWords,
            SpanishTextGen.Text(Harness.Words),
            static (kind, phrase, difficulty, noise, seed, word, text) => new MatchCase(kind, phrase.SetId, phrase.Raw, phrase.IsAlias, phrase.AnswerId, difficulty, noise, seed, word, text));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);
        var diagnostics = new FuzzDiagnostics();

        gen.Sample(
            (MatchCase input) =>
            {
                var (_, set, candidates) = Models.First(m => m.SetId == input.SetId);
                switch (input.Kind)
                {
                    case 0:
                        CheckPhrase(input, set, candidates, diagnostics);
                        break;
                    case 1:
                        CheckSoundAlike(input, set, candidates, diagnostics);
                        break;
                    case 2:
                        Check(input, set, candidates, input.Word, "a lexicon word");
                        diagnostics.Count("lexicon-word");
                        break;
                    default:
                        Check(input, set, candidates, input.Text, "free text");
                        diagnostics.Count("free-text");
                        break;
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Answer matching diagnostics:\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("phrase:exact") > 0 && diagnostics.Get("phrase:alias") > 0, "No canonical and alias phrases were exercised.\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("sound-alike:fuzzy") > 0, "No sound-alike phrase reached the fuzzy stage.\n" + diagnostics.Report());
    }

    private static void CheckPhrase(MatchCase input, AnswerSet set, List<Candidate> candidates, FuzzDiagnostics diagnostics)
    {
        // Casing, wrapping punctuation, whitespace and accent stripping must not change what the phrase matches.
        var noisy = input.Noise.Apply(input.Raw);
        var result = Check(input, set, candidates, noisy, "a noisy canonical or alias phrase");
        var plain = Stripped(input.Raw);
        if (plain.Length == 0)
        {
            return;
        }

        var exact = candidates.Where(c => !c.IsAlias && c.Phrase == plain).Select(static c => c.AnswerId).Order(StringComparer.Ordinal).FirstOrDefault();
        var alias = candidates.Where(c => c.IsAlias && c.Phrase == plain).Select(static c => c.AnswerId).Order(StringComparer.Ordinal).FirstOrDefault();
        var expectedType = exact is not null ? MatchType.Exact : MatchType.Alias;
        var expectedId = exact ?? alias;
        if (result.Type != expectedType || result.AnswerId != expectedId || result.Distance != 0 || result.Confidence != 1.0)
        {
            throw Determinism.Fail(Oracle, $"The phrase '{input.Raw}' (noisy '{noisy}') should match {expectedType} {expectedId} at distance 0, but gave {Describe(result)}.", new { input.SetId, input.Raw, noisy, input.Difficulty });
        }

        diagnostics.Count(expectedType == MatchType.Exact ? "phrase:exact" : "phrase:alias");
    }

    private static void CheckSoundAlike(MatchCase input, AnswerSet set, List<Candidate> candidates, FuzzDiagnostics diagnostics)
    {
        var original = Stripped(input.Raw);
        var perturbed = Perturb(original, new Vocaterra.Nlp.Core.Determinism.SeededRng(input.Seed));
        var result = Check(input, set, candidates, perturbed, "a sound-alike spelling");
        if (perturbed == original || perturbed.Length == 0 || SpanishPhoneticKey.Compute(perturbed) != SpanishPhoneticKey.Compute(original))
        {
            return;
        }

        // The spelling sounds like a phrase of the set, so something must match; if it is not literally a phrase of the set it is a
        // fuzzy match at distance 0 that carries the pronunciation flag.
        if (result.Type == MatchType.None)
        {
            throw Determinism.Fail(Oracle, $"'{perturbed}' sounds like '{original}' (same phonetic key) but matched nothing.", new { input.SetId, original, perturbed, input.Difficulty });
        }

        if (result.Type == MatchType.Fuzzy)
        {
            if (result.Distance != 0 || !result.LikelyPronunciation)
            {
                throw Determinism.Fail(Oracle, $"'{perturbed}' sounds like '{original}' but the fuzzy match is {Describe(result)}, not a distance-0 pronunciation match.", new { input.SetId, original, perturbed, input.Difficulty });
            }

            diagnostics.Count("sound-alike:fuzzy");
        }
    }

    private static MatchResult Check(MatchCase input, AnswerSet set, List<Candidate> candidates, string text, string what)
    {
        var normalized = TextNormalizer.Normalize(text, PipelineHarness.Normalization);
        var context = new { input.SetId, text, what, input.Difficulty };
        MatchResult actual;
        try
        {
            actual = AnswerMatcher.Match(normalized, set, input.Difficulty, SpanishLanguagePack.Instance, PipelineHarness.Normalization);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"AnswerMatcher.Match threw {ex.GetType().Name}: {ex.Message}", context);
        }

        if (double.IsNaN(actual.Confidence) || actual.Confidence is < 0.0 or > 1.0)
        {
            throw Determinism.Fail(Oracle, $"Confidence {actual.Confidence} is outside [0, 1].", context);
        }

        Determinism.AssertSame(Oracle, "AnswerMatcher.Match", context, () => actual, () => AnswerMatcher.Match(normalized, set, input.Difficulty, SpanishLanguagePack.Instance, PipelineHarness.Normalization));

        var expected = Model(Accents.Strip(normalized.Text), candidates, input.Difficulty);
        if (expected != actual)
        {
            throw Determinism.Fail(Oracle, $"The matcher gave {Describe(actual)} but the documented rules give {Describe(expected)}.", context);
        }

        return actual;
    }

    // The documented rules, written independently of the matcher: exact on a canonical phrase, then on an alias (ties by ordinal answer id),
    // else the smallest of the spelling and phonetic Damerau-Levenshtein distance within the budget floor(length / divisor), with equal distances
    // going to the first candidate in (answer id, canonical before alias, phrase) order. The budget is inclusive: a distance equal to it is still
    // a match, and every fuzzy match has confidence 1 - distance / length (an edit at the budget's edge is communicated like any other).
    private static MatchResult Model(string input, List<Candidate> candidates, Difficulty difficulty)
    {
        if (input.Length > 0)
        {
            var exact = candidates.Where(c => !c.IsAlias && c.Phrase == input).Select(static c => c.AnswerId).Order(StringComparer.Ordinal).FirstOrDefault();
            if (exact is not null)
            {
                return new MatchResult(exact, MatchType.Exact, 0, false, 1.0);
            }

            var alias = candidates.Where(c => c.IsAlias && c.Phrase == input).Select(static c => c.AnswerId).Order(StringComparer.Ordinal).FirstOrDefault();
            if (alias is not null)
            {
                return new MatchResult(alias, MatchType.Alias, 0, false, 1.0);
            }
        }

        var inputKey = SpanishPhoneticKey.Compute(input);
        Candidate? best = null;
        var bestDistance = 0;
        var bestSpelling = 0;
        var bestPhonetic = 0;
        var smallest = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var spelling = DamerauLevenshtein.Distance(input, candidate.Phrase);
            var phonetic = DamerauLevenshtein.Distance(inputKey, SpanishPhoneticKey.Compute(candidate.Phrase));
            var distance = Math.Min(spelling, phonetic);
            smallest = Math.Min(smallest, distance);
            if (distance > AnswerMatcher.MaxDistance(candidate.Phrase.Length, difficulty))
            {
                continue;
            }

            if (best is null || distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
                bestSpelling = spelling;
                bestPhonetic = phonetic;
            }
        }

        if (best is not { } winner)
        {
            return new MatchResult(null, MatchType.None, smallest == int.MaxValue ? 0 : smallest, false, 0.0);
        }

        var confidence = 1.0 - ((double)bestDistance / winner.Phrase.Length);
        return new MatchResult(winner.AnswerId, MatchType.Fuzzy, bestDistance, bestPhonetic == 0 && bestSpelling > 0, confidence);
    }

    // Candidates in the matcher's documented order: ordinal answer id, canonical before aliases, then ordinal phrase.
    private static List<Candidate> BuildCandidates(AnswerSet set)
    {
        var list = new List<Candidate>();
        foreach (var answer in set.Answers)
        {
            Add(list, answer.AnswerId, answer.Canonical, isAlias: false);
            foreach (var alias in answer.Aliases)
            {
                Add(list, answer.AnswerId, alias, isAlias: true);
            }
        }

        return [.. list.OrderBy(static c => c.AnswerId, StringComparer.Ordinal).ThenBy(static c => c.IsAlias).ThenBy(static c => c.Phrase, StringComparer.Ordinal)];
    }

    private static void Add(List<Candidate> list, string answerId, string raw, bool isAlias)
    {
        var phrase = Stripped(raw);
        if (phrase.Length > 0)
        {
            list.Add(new Candidate(answerId, raw, phrase, isAlias));
        }
    }

    private static string Stripped(string raw)
    {
        return Accents.Strip(TextNormalizer.Normalize(raw, PipelineHarness.Normalization).Text);
    }

    // Applies up to two edits that keep the spelling's sound: b and v swapped, ll to y, s and z swapped, c before e or i to s or z, and a dropped
    // h that is not part of ch. Only the edits that really keep the phonetic key are kept, so the caller can rely on it.
    private static string Perturb(string text, Vocaterra.Nlp.Core.Determinism.SeededRng rng)
    {
        var current = text;
        var edits = 1 + rng.NextInt(2);
        for (var i = 0; i < edits; i++)
        {
            var sites = new List<(int Index, int Length, string Replacement)>();
            for (var p = 0; p < current.Length; p++)
            {
                var c = current[p];
                var next = p + 1 < current.Length ? current[p + 1] : '\0';
                switch (c)
                {
                    case 'b':
                        sites.Add((p, 1, "v"));
                        break;
                    case 'v':
                        sites.Add((p, 1, "b"));
                        break;
                    case 'l' when next == 'l':
                        sites.Add((p, 2, "y"));
                        break;
                    case 's':
                        sites.Add((p, 1, "z"));
                        break;
                    case 'z':
                        sites.Add((p, 1, "s"));
                        break;
                    case 'c' when next is 'e' or 'i':
                        sites.Add((p, 1, rng.NextInt(2) == 0 ? "s" : "z"));
                        break;
                    case 'h' when p == 0 || current[p - 1] != 'c':
                        sites.Add((p, 1, string.Empty));
                        break;
                    default:
                        break;
                }
            }

            if (sites.Count == 0)
            {
                break;
            }

            var site = sites[rng.NextInt(sites.Count)];
            var edited = string.Concat(current.AsSpan(0, site.Index), site.Replacement, current.AsSpan(site.Index + site.Length));
            if (SpanishPhoneticKey.Compute(edited) == SpanishPhoneticKey.Compute(text))
            {
                current = edited;
            }
        }

        return current;
    }

    private static string Describe(MatchResult result)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{result.Type} id={result.AnswerId ?? "(none)"} distance={result.Distance} likelyPronunciation={result.LikelyPronunciation} confidence={result.Confidence}");
    }

    private sealed record Candidate(string AnswerId, string Raw, string Phrase, bool IsAlias);

    /// <summary>One generated input: which check to run, the phrase and set it draws on, the difficulty and the seeds and texts for the other checks.</summary>
    private sealed record MatchCase(int Kind, string SetId, string Raw, bool IsAlias, string AnswerId, Difficulty Difficulty, TextNoise Noise, ulong Seed, string Word, string Text);
}
