using System.Globalization;
using CsCheck;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Matching;
using Xunit.Abstractions;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O6: answer matching, checked three ways. Every input is compared with an independent model of the documented rules (exact on a canonical
/// phrase, then on an alias, then the smaller of the spelling and phonetic edit distance against the per-difficulty budget); canonical and alias
/// phrases must match exactly however they are cased, accented or punctuated; and sound-alike spellings must match with the pronunciation flag.
/// Since M75 the near miss of a line (<see cref="AnswerNearMisses"/>, the one word by which it misses the nearest answer phrase, a slip pair) is compared with
/// an independent model of its rule too, on lines made from the sets' phrases with one word changed, added or dropped, and on free text.
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

    // The Spanish question words in every form, without their accents (the rule counts a question word typed with or without its accent).
    private static readonly HashSet<string> QuestionWords = new(StringComparer.Ordinal)
    {
        "que", "cual", "cuales", "quien", "quienes", "donde", "adonde", "cuando", "como", "cuanto", "cuanta", "cuantos", "cuantas",
    };

    // The words of each closed class the line generator draws from: the question words, and the lexicon's articles, prepositions and pronouns (one word each).
    private static readonly Lazy<IReadOnlyList<(string Class, IReadOnlyList<string> Words)>> ClosedClassWords = new(static () =>
    [
        ("question", ["qué", "cuál", "quién", "dónde", "cuándo", "cómo", "cuánto", "cuántas", "cual", "donde"]),
        .. new[] { "art", "prep", "pron" }.Select(pos => (pos, (IReadOnlyList<string>)[.. Harness.Lexicon.Entries.Where(e => e.Pos == pos && !e.Form.Contains(' ')).Select(static e => e.Form).Distinct().Order(StringComparer.Ordinal)])),
    ]);

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

    /// <summary>
    /// M75: <see cref="AnswerNearMisses.Find"/> agrees with an independent model of its documented rule for any line: the phrases at the smallest distance (the
    /// matcher's), in the matcher's order, the first with as many words as the line and exactly one differing word (accents ignored) whose two words are a slip
    /// pair: within the answer word's own budget (the smaller of the spelling and phonetic distance), both question words (a list written here), or both with a
    /// lexicon reading as an article, a preposition or a pronoun. The near miss names a word of the line and a word of the answer, never an empty one.
    /// </summary>
    [Fact]
    public void NearMiss_AgreesWithTheDocumentedModel()
    {
        var phrases = Models.SelectMany(static m => m.Candidates.Select(c => (m.SetId, c.Raw))).ToArray();
        var gen = Gen.Select(
            Gen.Int[0, 5],
            Gen.OneOfConst(phrases),
            Gen.Enum<Difficulty>(),
            Gen.ULong,
            Gen.OneOfConst([.. Harness.Words.AllForms]),
            SpanishTextGen.Text(Harness.Words),
            static (kind, phrase, difficulty, seed, word, text) => new NearMissCase(kind, phrase.SetId, phrase.Raw, difficulty, seed, word, text));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);
        var diagnostics = new FuzzDiagnostics();

        gen.Sample(
            (NearMissCase input) =>
            {
                var (_, set, candidates) = Models.First(m => m.SetId == input.SetId);
                var line = NearMissLine(input, new Vocaterra.Nlp.Core.Determinism.SeededRng(input.Seed));
                var normalized = SpanishTextNormalizer.Normalize(line, PipelineHarness.Normalization);
                var context = new { input.SetId, input.Raw, line, input.Difficulty, input.Kind };
                AnswerNearMiss? actual;
                try
                {
                    actual = AnswerNearMisses.Find(normalized, set, input.Difficulty, SpanishLanguagePack.Instance, Harness.Lexicon, Harness.Bundle.AnalysisRules, PipelineHarness.Normalization);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"AnswerNearMisses.Find threw {ex.GetType().Name}: {ex.Message}", context);
                }

                var expected = NearMissModel(normalized, candidates, input.Difficulty);
                if (!Equals(expected, actual))
                {
                    throw Determinism.Fail(Oracle, $"The near miss is {Describe(actual)} but the documented rule gives {Describe(expected)}.", context);
                }

                if (actual is not null
                    && (actual.Expected.Length == 0 || !normalized.Tokens.Any(t => t.Span == actual.Span && t.Text == actual.Actual)))
                {
                    throw Determinism.Fail(Oracle, $"The near miss {Describe(actual)} does not name a word of the line and a word of the answer.", context);
                }

                diagnostics.Count(actual is null ? "near-miss:none" : "near-miss:" + actual.Pair);
                diagnostics.Count("near-miss-line:" + input.Kind);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Near miss diagnostics:\n" + diagnostics.Report());
        foreach (var pair in Enum.GetValues<SlipPair>())
        {
            Assert.True(diagnostics.Get("near-miss:" + pair) > 0, $"No near miss of the slip pair {pair} was reached.\n" + diagnostics.Report());
        }

        Assert.True(diagnostics.Get("near-miss:none") > 0, "No line without a near miss was reached.\n" + diagnostics.Report());
    }

    // A line made from a phrase of a set: 0 the phrase, 1 one word changed for a lexicon word, 2 one word changed for a word of the same closed class as a word of
    // the phrase where it has one (a question word, an article, a preposition, a pronoun), 3 one word with a letter edited, 4 a word added or dropped, 5 free text.
    private static string NearMissLine(NearMissCase input, Vocaterra.Nlp.Core.Determinism.SeededRng rng)
    {
        var words = SpanishTextNormalizer.Normalize(input.Raw, PipelineHarness.Normalization).Tokens.Select(static t => t.Original.ToLowerInvariant()).ToList();
        if (input.Kind == 5 || words.Count == 0)
        {
            return input.Text;
        }

        var at = rng.NextInt(words.Count);
        switch (input.Kind)
        {
            case 1:
                words[at] = input.Word;
                break;
            case 2:
                var classes = ClosedClassWords.Value.Where(c => c.Words.Contains(words[at])).ToList();
                var pool = classes.Count > 0 ? rng.Pick(classes).Words : rng.Pick(ClosedClassWords.Value).Words;
                words[at] = rng.Pick(pool);
                break;
            case 3:
                words[at] = OneLetterEdited(words[at], rng);
                break;
            case 4:
                if (words.Count > 1 && rng.NextInt(2) == 0)
                {
                    words.RemoveAt(at);
                }
                else
                {
                    words.Insert(at, input.Word);
                }

                break;
            default:
                break;
        }

        return string.Join(' ', words);
    }

    private static string OneLetterEdited(string word, Vocaterra.Nlp.Core.Determinism.SeededRng rng)
    {
        const string letters = "abcdeilmnoprstuáéñ";
        var i = rng.NextInt(word.Length + 1);
        return rng.NextInt(4) switch
        {
            0 when word.Length > 1 && i < word.Length => word.Remove(i, 1),
            1 when i + 1 < word.Length => string.Concat(word.AsSpan(0, i), word[i + 1].ToString(), word[i].ToString(), word.AsSpan(i + 2)),
            2 when i < word.Length => string.Concat(word.AsSpan(0, i), letters[rng.NextInt(letters.Length)].ToString(), word.AsSpan(i + 1)),
            _ => word.Insert(i, letters[rng.NextInt(letters.Length)].ToString()),
        };
    }

    // The near miss, written independently of AnswerNearMisses (M75): among the candidates at the smallest of the spelling and phonetic distance from the line (the
    // matcher's distance), in the matcher's order, the first whose normalized phrase has the line's number of words and differs from it, accents ignored, in
    // exactly one word, those two words being a slip pair. The expected word is the phrase's, a number in digits said in its words.
    private static AnswerNearMiss? NearMissModel(NormalizedText line, List<Candidate> candidates, Difficulty difficulty)
    {
        var input = Accents.Strip(line.Text);
        if (input.Length == 0 || line.Tokens.Count == 0 || candidates.Count == 0)
        {
            return null;
        }

        var inputKey = SpanishPhoneticKey.Compute(input);
        var distances = candidates.Select(c => Math.Min(DamerauLevenshtein.Distance(input, c.Phrase), DamerauLevenshtein.Distance(inputKey, SpanishPhoneticKey.Compute(c.Phrase)))).ToArray();
        var smallest = distances.Min();
        for (var i = 0; i < candidates.Count; i++)
        {
            if (distances[i] != smallest)
            {
                continue;
            }

            var phrase = SpanishTextNormalizer.Normalize(candidates[i].Raw, PipelineHarness.Normalization);
            if (phrase.Tokens.Count != line.Tokens.Count)
            {
                continue;
            }

            var differing = Enumerable.Range(0, line.Tokens.Count).Where(k => Accents.Strip(line.Tokens[k].Text) != Accents.Strip(phrase.Tokens[k].Text)).ToList();
            if (differing.Count != 1)
            {
                continue;
            }

            var word = line.Tokens[differing[0]];
            var answerWord = phrase.Tokens[differing[0]];
            if (SlipPairModel(word.Text, answerWord.Text, difficulty) is not { } pair)
            {
                continue;
            }

            var expected = answerWord.Text.All(char.IsAsciiDigit) ? answerWord.Original.Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant() : answerWord.Text;
            return new AnswerNearMiss(candidates[i].AnswerId, phrase.Text, word.Span, word.Text, expected, pair);
        }

        return null;
    }

    private static SlipPair? SlipPairModel(string word, string answerWord, Difficulty difficulty)
    {
        var a = Accents.Strip(word);
        var b = Accents.Strip(answerWord);
        var distance = Math.Min(DamerauLevenshtein.Distance(a, b), DamerauLevenshtein.Distance(SpanishPhoneticKey.Compute(a), SpanishPhoneticKey.Compute(b)));
        var divisor = difficulty switch { Difficulty.Intermediate => 5, Difficulty.Advanced => 6, _ => 4 };
        if (distance <= b.Length / divisor)
        {
            return SlipPair.Spelling;
        }

        if (QuestionWords.Contains(Accents.Strip(word)) && QuestionWords.Contains(Accents.Strip(answerWord)))
        {
            return SlipPair.QuestionWord;
        }

        foreach (var (pos, pair) in new[] { ("art", SlipPair.Article), ("prep", SlipPair.Preposition), ("pron", SlipPair.Pronoun) })
        {
            // M97: a pronoun pair needs a reading with a person on each side (a personal pronoun); tal, with none, is no slip of me.
            if (Harness.Lexicon.Lookup(word).Any(r => r.Pos == pos && (pos != "pron" || r.Person is not null)) && Harness.Lexicon.Lookup(answerWord).Any(r => r.Pos == pos && (pos != "pron" || r.Person is not null)))
            {
                return pair;
            }
        }

        return null;
    }

    private static string Describe(AnswerNearMiss? nearMiss)
    {
        return nearMiss is null ? "none" : $"{nearMiss.Pair} '{nearMiss.Actual}'@{nearMiss.Span.Start}+{nearMiss.Span.Length} for '{nearMiss.Expected}' of '{nearMiss.Phrase}' ({nearMiss.AnswerId})";
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
        var normalized = SpanishTextNormalizer.Normalize(text, PipelineHarness.Normalization);
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
        return Accents.Strip(SpanishTextNormalizer.Normalize(raw, PipelineHarness.Normalization).Text);
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

    /// <summary>One generated near-miss input (M75): how the line is made, the phrase and set it draws on, the difficulty, a seed, a lexicon word and a free text.</summary>
    private sealed record NearMissCase(int Kind, string SetId, string Raw, Difficulty Difficulty, ulong Seed, string Word, string Text);

    /// <summary>One generated input: which check to run, the phrase and set it draws on, the difficulty and the seeds and texts for the other checks.</summary>
    private sealed record MatchCase(int Kind, string SetId, string Raw, bool IsAlias, string AnswerId, Difficulty Difficulty, TextNoise Noise, ulong Seed, string Word, string Text);
}
