using CsCheck;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.De;
using Vocaterra.Nlp.De.Normalization;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using Xunit.Abstractions;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.De;

/// <summary>
/// The neutral oracles run on German (M37): O1 (the normalizer is total, idempotent and well formed) through <see cref="LanguageOracles.Normalizer(Vocaterra.Nlp.Language.Packs.ILanguagePack, Gen{string}, ITestOutputHelper, Action{string}?)"/>,
/// the body Spanish runs, with the German pack and <see cref="GermanFuzzText"/>; and O6, answer matching, by metamorphic relations (no independent model of the
/// German rules is written yet): a canonical or alias phrase matches exactly however it is cased, quoted, spaced, decomposed or spelled with <c>ae oe ue ss</c>;
/// a text and its respelled twin get the same result; a sound-alike spelling matches at distance 0 with the pronunciation flag. O2, O7 and O14 need a loadable
/// German bundle and wait for M38 to M40 (the pack's morphology, analysis rules and template functions are placeholders).
/// </summary>
public sealed class GermanOracleTests(ITestOutputHelper output)
{
    private const string Oracle = "O6";

    private static readonly NormalizationOptions Options = NormalizationOptions.Default;

    private static readonly Vocaterra.Nlp.Language.Packs.ILanguagePack Pack = GermanLanguagePack.Instance;

    private static readonly IReadOnlyList<AnswerSet> Sets =
    [
        new AnswerSet(
            "de-essen",
            [
                new Answer("brot", "Brot", ["das Brot"], []),
                new Answer("broetchen", "ein Brötchen", ["Brötchen"], []),
                new Answer("kaese", "Käse", ["der Käse", "ein Stück Käse"], []),
                new Answer("wasser", "Wasser", ["ein Glas Wasser"], []),
                new Answer("apfel", "ein Apfel", ["die Äpfel"], []),
            ]),
        new AnswerSet(
            "de-gruesse",
            [
                new Answer("hallo", "Hallo", ["Guten Tag"], []),
                new Answer("gott", "Grüß Gott", ["Grüezi", "Servus"], []),
                new Answer("tschuess", "Tschüss", ["Auf Wiedersehen"], []),
                new Answer("danke", "Danke schön", ["Vielen Dank"], []),
                new Answer("strasse", "Wo ist die Straße", ["Wo ist die Strasse nach Zürich"], []),
            ]),
        new AnswerSet(
            "de-zahlen",
            [
                new Answer("zwei", "zweihundert Meter", [], []),
                new Answer("einundzwanzig", "einundzwanzig Euro", [], []),
                new Answer("dreissig", "dreißig Minuten", [], []),
                new Answer("fuenf", "fünf Äpfel", [], []),
            ]),
    ];

    private static readonly IReadOnlyList<(string SetId, string Raw, bool IsAlias, string AnswerId)> Phrases =
    [
        .. Sets.SelectMany(static set => set.Answers.SelectMany(a => a.Aliases.Select(alias => (set.AnswerSetId, alias, true, a.AnswerId)).Prepend((set.AnswerSetId, a.Canonical, false, a.AnswerId)))),
    ];

    // Spelling pairs that sound alike in German: the first is replaced by the second, once, where it occurs.
    private static readonly (string From, string To)[] SoundAlikes =
    [
        ("ß", "s"),
        ("ss", "s"),
        ("tz", "z"),
        ("ck", "k"),
        ("ph", "f"),
        ("ie", "i"),
        ("eu", "äu"),
        ("V", "F"),
        ("ä", "e"),
        ("ai", "ei"),
    ];

    [Fact]
    public void O1_TheNormalizerIsTotalIdempotentAndWellFormed_OnGerman()
    {
        var diagnostics = LanguageOracles.Normalizer(Pack, GermanFuzzText.Text, output);

        Assert.True(diagnostics.Get("input:lone-surrogate") > 0);
    }

    [Fact]
    public void O1_TheGermanEntryPointsAreTotal_OnTheSameInputs()
    {
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);

        GermanFuzzText.Text.Sample(
            text =>
            {
                var key = GermanFolding.Instance.MatchingKey(text);
                var number = GermanNumbers.DigitsToWords(text);
                var folded = GermanFolding.Instance.LenientKey(text);
                if (key.Any(char.IsSurrogate) && !text.Any(char.IsSurrogate))
                {
                    throw Determinism.Fail("O1", "MatchingKey made a surrogate out of text without one.", text);
                }

                if (folded.Any(static c => c is 'ä' or 'ö' or 'ü' or 'ß'))
                {
                    throw Determinism.Fail("O1", "LenientKey left an umlaut or ß.", text);
                }

                if (number is null)
                {
                    throw Determinism.Fail("O1", "DigitsToWords returned null.", text);
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);
    }

    [Fact]
    public void O6_AnswerMatching_IsInvariantUnderRespelling_AndKeepsSoundAlikes_OnGerman()
    {
        var gen = Gen.Select(
            Gen.Int[0, 2],
            Gen.OneOfConst([.. Phrases]),
            Gen.Enum<Difficulty>(),
            Gen.Select(Gen.Int[0, 5], Gen.Int[0, 6], Gen.Int[0, 2], Gen.Bool, Gen.Int[0, 2], static (caseMode, quoteMode, spaces, nfd, spell) => new Respelling(caseMode, quoteMode, spaces, nfd, spell)),
            Gen.Int,
            GermanFuzzText.Tame,
            static (kind, phrase, difficulty, respelling, seed, text) => new MatchCase(kind, phrase, difficulty, respelling, seed, text));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);
        var diagnostics = new FuzzDiagnostics();

        gen.Sample(
            (MatchCase input) =>
            {
                switch (input.Kind)
                {
                    case 0:
                        CheckPhraseSurvivesRespelling(input, diagnostics);
                        break;
                    case 1:
                        CheckSoundAlike(input, diagnostics);
                        break;
                    default:
                        CheckTextSameAsItsTwin(input, diagnostics);
                        break;
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("German answer matching diagnostics:\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("phrase:exact") > 0 && diagnostics.Get("phrase:alias") > 0, "No canonical and alias phrases were exercised.\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("sound-alike:fuzzy") > 0, "No sound-alike phrase reached the fuzzy stage.\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("twin:compared") > 0, "No text was compared with its twin.\n" + diagnostics.Report());
    }

    private static AnswerSet SetOf(string id) => Sets.First(s => s.AnswerSetId == id);

    private static MatchResult Match(string text, AnswerSet set, Difficulty difficulty, object context)
    {
        var normalized = Pack.Normalizer.Normalize(text, Options);
        MatchResult actual;
        try
        {
            actual = AnswerMatcher.Match(normalized, set, difficulty, Pack, Options);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"AnswerMatcher.Match threw {ex.GetType().Name}: {ex.Message}", context);
        }

        if (double.IsNaN(actual.Confidence) || actual.Confidence is < 0.0 or > 1.0)
        {
            throw Determinism.Fail(Oracle, $"Confidence {actual.Confidence} is outside [0, 1].", context);
        }

        Determinism.AssertSame(Oracle, "AnswerMatcher.Match", context, () => actual, () => AnswerMatcher.Match(normalized, set, difficulty, Pack, Options));
        return actual;
    }

    // A canonical or alias phrase, however cased, quoted, spaced, decomposed or spelled with ae oe ue ss, is that phrase: exact for a canonical one, alias for an alias.
    private static void CheckPhraseSurvivesRespelling(MatchCase input, FuzzDiagnostics diagnostics)
    {
        var (setId, raw, isAlias, answerId) = input.Phrase;
        var noisy = input.Respelling.Apply(raw);
        var result = Match(noisy, SetOf(setId), input.Difficulty, new { setId, raw, noisy, input.Difficulty });
        var expectedType = isAlias ? MatchType.Alias : MatchType.Exact;
        if (result.Type != expectedType || result.AnswerId != answerId || result.Distance != 0 || result.Confidence != 1.0)
        {
            throw Determinism.Fail(
                Oracle,
                $"The phrase '{raw}' (respelled '{noisy}') should match {expectedType} {answerId} at distance 0, but gave {result.Type} {result.AnswerId} at {result.Distance}.",
                new { setId, raw, noisy, input.Difficulty });
        }

        diagnostics.Count(isAlias ? "phrase:alias" : "phrase:exact");
    }

    // A spelling that sounds like a phrase (same phonetic key, other letters) matches something; a fuzzy match is at distance 0 and carries the pronunciation flag.
    private static void CheckSoundAlike(MatchCase input, FuzzDiagnostics diagnostics)
    {
        var (setId, raw, _, _) = input.Phrase;
        var swap = SoundAlikes.FirstOrDefault(s => raw.Contains(s.From, StringComparison.Ordinal));
        if (swap.From is null)
        {
            diagnostics.Count("sound-alike:no-swap");
            return;
        }

        var index = raw.IndexOf(swap.From, StringComparison.Ordinal);
        var perturbed = string.Concat(raw.AsSpan(0, index), swap.To, raw.AsSpan(index + swap.From.Length));
        var result = Match(perturbed, SetOf(setId), input.Difficulty, new { setId, raw, perturbed, input.Difficulty });
        var original = GermanFolding.Instance.MatchingKey(Pack.Normalizer.Normalize(raw, Options).Text);
        var changed = GermanFolding.Instance.MatchingKey(Pack.Normalizer.Normalize(perturbed, Options).Text);
        if (original == changed
            || GermanPhoneticKey.Key(original) != GermanPhoneticKey.Key(changed))
        {
            diagnostics.Count("sound-alike:not-alike");
            return;
        }

        if (result.Type == MatchType.None)
        {
            throw Determinism.Fail(Oracle, $"'{perturbed}' sounds like '{raw}' (same phonetic key) but matched nothing.", new { setId, raw, perturbed, input.Difficulty });
        }

        if (result.Type == MatchType.Fuzzy)
        {
            if (result.Distance != 0 || !result.LikelyPronunciation)
            {
                throw Determinism.Fail(Oracle, $"'{perturbed}' sounds like '{raw}' but the fuzzy match is {result.Type} at {result.Distance} (pronunciation {result.LikelyPronunciation}).", new { setId, raw, perturbed, input.Difficulty });
            }

            diagnostics.Count("sound-alike:fuzzy");
        }
        else
        {
            diagnostics.Count("sound-alike:exact-or-alias");
        }
    }

    // Respelling changes no letter the matching key keeps apart: a text and its twin get the same result in every set.
    private static void CheckTextSameAsItsTwin(MatchCase input, FuzzDiagnostics diagnostics)
    {
        var twin = new Respelling(input.Respelling.CaseMode, 0, input.Respelling.Spaces, input.Respelling.Nfd, 1).Apply(input.Text);
        var set = SetOf(input.Phrase.SetId);
        var plain = Match(input.Text, set, input.Difficulty, new { input.Text, input.Difficulty });
        var other = Match(twin, set, input.Difficulty, new { input.Text, twin, input.Difficulty });
        if (plain != other)
        {
            throw Determinism.Fail(
                Oracle,
                $"'{input.Text}' gave {plain.Type} {plain.AnswerId} at {plain.Distance}, its respelled twin '{twin}' gave {other.Type} {other.AnswerId} at {other.Distance}.",
                new { input.Text, twin, input.Difficulty });
        }

        diagnostics.Count("twin:compared");
    }

    private sealed record MatchCase(int Kind, (string SetId, string Raw, bool IsAlias, string AnswerId) Phrase, Difficulty Difficulty, Respelling Respelling, int Seed, string Text);

    // The respellings the matcher must not see: case, wrapping marks, spacing, decomposed umlauts and the ae oe ue ss spelling.
    private sealed record Respelling(int CaseMode, int QuoteMode, int Spaces, bool Nfd, int Spell)
    {
        public string Apply(string text)
        {
            var result = Spell == 1 ? GermanFuzzText.SpellOut(text) : text;
            result = CaseMode switch
            {
                1 => result.ToUpperInvariant(),
                2 => result.ToLowerInvariant(),
                3 => string.Join(' ', result.Split(' ').Select(static w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..])),
                4 => string.Concat(result.Select(static (c, i) => i % 2 == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c))),
                _ => result,
            };
            result = Spaces switch
            {
                1 => result.Replace(" ", "  ", StringComparison.Ordinal),
                2 => " " + result + "  ",
                _ => result,
            };
            result = QuoteMode switch
            {
                1 => "„" + result + "“",
                2 => "»" + result + "«",
                3 => result + "?",
                4 => result + "!",
                5 => "„" + result + "?“",
                6 => result + ".",
                _ => result,
            };
            return Nfd ? result.Normalize(System.Text.NormalizationForm.FormD) : result;
        }
    }
}
