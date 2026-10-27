using System.Text;
using CsCheck;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Decision.Scheduling;
using Vocaterra.Nlp.Evaluation;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;
using Xunit.Abstractions;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Fuzzing.Tests.Oracles;

/// <summary>
/// The per-language bodies of the oracles O1, O2, O7 and O14 (M35, research 4.8). Each takes a <see cref="FuzzLanguage"/> and states the property
/// once; the Spanish test classes and the toy-language test classes call the same body. Only the language-specific inputs and the coverage facts
/// differ, and a test class asserts its own coverage. Nothing here weakens an oracle: the checks are the ones the Spanish classes made before the
/// harness existed, with the pack's members in place of the Spanish statics they called.
/// </summary>
public static class LanguageOracles
{
    /// <summary>O1: the normalizer never crashes, is idempotent and returns well-formed tokens (see <see cref="NormalizerFuzzTests"/>).</summary>
    /// <param name="language">The language.</param>
    /// <param name="output">The test output.</param>
    /// <param name="entryPoints">Extra total entry points of the language to call on every input (Spanish: <c>Accents.Strip</c> and <c>SpanishPhoneticKey</c>).</param>
    /// <returns>The diagnostics of the run (the lone-surrogate count is always taken).</returns>
    public static FuzzDiagnostics Normalizer(FuzzLanguage language, ITestOutputHelper output, Action<string>? entryPoints = null)
    {
        const string oracle = "O1";
        var gen = Gen.Select(
            language.FreeText,
            Gen.Bool,
            Gen.Bool,
            Gen.Bool,
            static (text, contractions, numbers, articles) => (text, new NormalizationOptions(contractions, numbers, articles)));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);
        var diagnostics = new FuzzDiagnostics();

        gen.Sample(
            ((string Text, NormalizationOptions Options) input) =>
            {
                var (text, options) = input;
                if (HasLoneSurrogate(text))
                {
                    diagnostics.Count("input:lone-surrogate");
                }

                CheckEntryPointsAreTotal(language, text, entryPoints);
                var first = Normalize(language, text, options);

                CheckWellFormed(oracle, first, text, options);

                // Idempotence: feeding the normalized text back changes neither the text nor the token texts and spans. Token.Original
                // legitimately differs on the second pass (it is the first pass's output), so it is not compared.
                var second = Normalize(language, first.Text, options);
                if (!Same(first, second))
                {
                    throw Determinism.Fail(oracle, $"Normalize is not idempotent: '{first.Text}' became '{second.Text}'.", new { text, options });
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Normalizer diagnostics:\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("input:lone-surrogate") > 0, "The generators must feed raw lone surrogates.\n" + diagnostics.Report());
        return diagnostics;
    }

    /// <summary>
    /// O2: the analyzer never crashes on any text, gives the same answer on a fresh instance, and its output is well-formed. Returns the diagnostics
    /// (the error tags and the grammar matches seen), which the test class checks for coverage.
    /// </summary>
    public static FuzzDiagnostics Analyzer(FuzzLanguage language, ITestOutputHelper output)
    {
        const string oracle = "O2";
        var bundle = language.Bundle;
        var pack = language.Pack;

        // Free text three times in four; a realised grammar sentence (so the grammars actually match and slots are extracted) otherwise.
        var sentences = language.Sentences.Select(static sentence => sentence?.Text ?? string.Empty);
        var texts = Gen.Frequency((3, (IGen<string>)language.FreeText), (1, (IGen<string>)sentences));
        var gen = Gen.Select(texts, Gen.Bool, static (text, withGrammars) => (text, withGrammars));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);
        var analyzer = new LinguisticAnalyzer(bundle.Lexicon, bundle.LanguageMorphology, pack);
        var run = 0;
        var diagnostics = new FuzzDiagnostics();

        gen.Sample(
            ((string Text, bool WithGrammars) input) =>
            {
                var normalized = pack.Normalizer.Normalize(input.Text, PipelineHarness.Normalization);
                var grammars = input.WithGrammars ? bundle.Intents.Grammars : [];
                var context = new { text = input.Text, normalized = normalized.Text, withGrammars = input.WithGrammars };

                AnalysisResult first;
                try
                {
                    first = analyzer.Analyze(normalized, grammars);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(oracle, $"Analyze threw {ex.GetType().Name}: {ex.Message}", context);
                }

                CheckAnalysis(oracle, first, normalized, grammars.Select(static g => g.Id).ToHashSet(StringComparer.Ordinal), pack.ErrorTags, context);
                foreach (var error in first.Errors)
                {
                    diagnostics.Count($"tag:{error.Tag}");
                }

                diagnostics.Count(first.MatchedGrammarId is null ? "grammar:none" : "grammar:matched");

                // Two runs on the shared analyzer always; every eighth iteration also compares against an analyzer built from scratch, because
                // its morphology index is built lazily and hidden state would show there.
                Determinism.AssertSame(oracle, "Analyze", context, () => first, () => analyzer.Analyze(normalized, grammars));
                if (Interlocked.Increment(ref run) % 8 == 0)
                {
                    var fresh = NewAnalyzer(language);
                    Determinism.AssertSame(oracle, "Analyze on a fresh analyzer", context, () => first, () => fresh.Analyze(normalized, grammars));
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Analyzer input diagnostics:\n" + diagnostics.Report());
        return diagnostics;
    }

    /// <summary>A fresh analyzer over the language's lexicon, with its own morphology.</summary>
    public static LinguisticAnalyzer NewAnalyzer(FuzzLanguage language)
    {
        return new LinguisticAnalyzer(language.Bundle.Lexicon, language.Pack.CreateMorphology(language.Bundle.Lexicon), language.Pack);
    }

    /// <summary>
    /// O7: the intent resolver never crashes, respects the prompt's allowed intents whatever their order or content, follows the ladder, reports an
    /// unresolved result as margin 0 and confidence 0, and gives the same resolution on a fresh instance. Returns the diagnostics (the rungs reached).
    /// </summary>
    public static FuzzDiagnostics Resolver(FuzzLanguage language, ITestOutputHelper output)
    {
        const string oracle = "O7";
        var bundle = language.Bundle;
        var pack = language.Pack;
        var withoutEmbeddings = new IntentResolver(bundle.Intents, pack, null);
        var withEmbeddings = new IntentResolver(bundle.Intents, pack, new FakeEmbeddingModel());
        var gen = Gen.Select(
            language.Sentences,
            Gen.ULong,
            Gen.Int[0, 9],
            language.FreeText,
            static (sentence, promptSeed, mode, text) => new ResolveCase(sentence, promptSeed, mode, text));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);
        var diagnostics = new FuzzDiagnostics();
        var run = 0;

        gen.Sample(
            (ResolveCase input) =>
            {
                var prompt = MakePrompt(language, new SeededRng(input.PromptSeed));

                // Half the time a realised grammar sentence, a fifth free text, and three in ten an example utterance of the catalog with
                // its words shuffled or dropped, which no grammar matches but the embedding rung may still recognise.
                var sentence = input.Mode <= 4 ? input.Sentence : null;
                var text = input.Mode switch
                {
                    <= 4 => sentence?.Text ?? input.Text,
                    <= 6 => input.Text,
                    _ => PerturbedExample(language, new SeededRng(input.PromptSeed ^ 0x9E3779B97F4A7C15UL)),
                };
                var normalized = pack.Normalizer.Normalize(text, PipelineHarness.Normalization);

                var candidates = CandidatesOf(language, prompt);
                var grammars = candidates.SelectMany(static i => i.Grammars).DistinctBy(static g => g.Id).ToArray();
                var analysis = NewAnalyzer(language).Analyze(normalized, grammars);
                var context = new { text, prompt, matchedGrammar = analysis.MatchedGrammarId, sentence = sentence?.PatternText };

                foreach (var (resolver, label) in new[] { (withoutEmbeddings, "without embeddings"), (withEmbeddings, "with embeddings") })
                {
                    IntentResolution resolution;
                    try
                    {
                        resolution = resolver.Resolve(normalized, analysis, prompt);
                    }
                    catch (Exception ex)
                    {
                        throw Determinism.Fail(oracle, $"Resolve ({label}) threw {ex.GetType().Name}: {ex.Message}", context);
                    }

                    CheckResolution(oracle, pack, resolution, label == "with embeddings", candidates, grammars, analysis, sentence, context);
                    diagnostics.Count($"rung:{resolution.Rung}");

                    // The same prompt with its allowed intents reordered and repeated resolves identically; and every sixteenth time a resolver
                    // built from scratch agrees.
                    var reordered = prompt with { AllowedIntentIds = [.. prompt.AllowedIntentIds.Reverse().Concat(prompt.AllowedIntentIds)] };
                    Determinism.AssertSame(oracle, $"Resolve ({label}) with reordered allowed intents", context, () => resolution, () => resolver.Resolve(normalized, analysis, reordered));
                    if (Interlocked.Increment(ref run) % 16 == 0)
                    {
                        var fresh = new IntentResolver(bundle.Intents, pack, label == "with embeddings" ? new FakeEmbeddingModel() : null);
                        Determinism.AssertSame(oracle, $"Resolve ({label}) on a fresh resolver", context, () => resolution, () => fresh.Resolve(normalized, analysis, prompt));
                    }
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Resolver diagnostics:\n" + diagnostics.Report());
        return diagnostics;
    }

    /// <summary>
    /// O14: the pipeline end to end, with the recognizers scripted, for a language. It never throws; it gives the same result on a fresh instance;
    /// its verdict is the one the stages called by hand produce with the pack; its decision is the decision layer's; and its replay envelope round-trips
    /// through canonical JSON and replays clean. The Spanish O14 (<see cref="PipelineFuzzTests"/>) checks more (phonemes, two-phase, host turns, and
    /// the mutations of the envelope); this body is the part a language has without a pronunciation model.
    /// </summary>
    public static FuzzDiagnostics Pipeline(FuzzLanguage language, ITestOutputHelper output)
    {
        const string oracle = "O14";
        var bundle = language.Bundle;
        var answers = bundle.AnswerSets.Values.SelectMany(static set => set.Answers.Select(static answer => answer.Canonical)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var promptIds = bundle.Prompts.Keys.ToArray();
        var agentIds = bundle.Agents.Keys.Order(StringComparer.Ordinal).ToArray();
        var transcripts = Gen.Frequency(
            (4, (IGen<string>)language.FreeText),
            (3, (IGen<string>)language.Sentences.Select(static s => s?.Text ?? "nada")),
            (2, (IGen<string>)Gen.OneOf(answers.Select(static answer => Gen.Const(answer)).ToArray())));
        var gen = Gen.Select(
            transcripts,
            Gen.Int[0, promptIds.Length - 1],
            Gen.Int[0, agentIds.Length],
            Gen.Bool,
            Gen.ULong,
            static (transcript, prompt, agent, embeddings, seed) => new PipelineCaseLite(transcript, prompt, agent, embeddings, seed));
        var iterations = FuzzOptions.Iterations(FuzzOptions.PipelineVar, FuzzOptions.PipelineDefault);
        var diagnostics = new FuzzDiagnostics();

        gen.Sample(
            c =>
            {
                var prompt = bundle.Prompts[promptIds[c.Prompt]];
                var agentId = c.Agent < agentIds.Length ? agentIds[c.Agent] : "nobody";
                var (pipeline, context) = Prepare(language, c, prompt, agentId);
                PipelineTrace trace;
                try
                {
                    trace = pipeline.Trace(Clip, prompt, context);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(oracle, $"The pipeline threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}", c);
                }

                Determinism.AssertSame(oracle, "UtterancePipeline.Evaluate", c, () => trace.Result, () => Prepare(language, c, prompt, agentId).Pipeline.Evaluate(Clip, prompt, context));
                CheckStructure(c, trace, prompt, context);
                CheckVerdictByHand(language, c, trace, pipeline, prompt);
                CheckDecision(c, trace, pipeline, prompt, context);
                CheckReplay(c, trace, pipeline, context);
                diagnostics.Count(trace.Result.Verdict.Communicated ? "verdict:communicated" : "verdict:not-communicated");
                diagnostics.Count(trace.Result.Verdict.Errors.Count > 0 ? "verdict:errors" : "verdict:clean");
                diagnostics.Count(trace.Result.Decision.Agent is null ? "agent:none" : "agent:step");
                diagnostics.Count(trace.Result.Repair is null ? "repair:none" : "repair:step");
                diagnostics.Count($"mode:{prompt.Mode}");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Pipeline diagnostics:\n" + diagnostics.Report());
        return diagnostics;
    }

    // ---- O1 helpers -----------------------------------------------------------------------------------------------------------------

    private static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    // The raw text, lone surrogates included, goes straight to the entry points that call string.Normalize underneath.
    private static void CheckEntryPointsAreTotal(FuzzLanguage language, string text, Action<string>? entryPoints)
    {
        try
        {
            var stripped = language.Pack.Folding.LenientKey(text);
            var key = language.Pack.PhoneticKey.Compute(text);
            if (HasLoneSurrogate(stripped))
            {
                throw Determinism.Fail("O1", "The lenient key left a lone surrogate in its result.", text);
            }

            if (key.Any(static c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or ' ')))
            {
                throw Determinism.Fail("O1", $"The phonetic key '{key}' has a character outside a-z, 0-9 and space.", text);
            }
        }
        catch (ArgumentException ex)
        {
            throw Determinism.Fail("O1", $"The pack's folding or phonetic key threw {ex.GetType().Name}: {ex.Message}", text);
        }

        entryPoints?.Invoke(text);
    }

    private static NormalizedText Normalize(FuzzLanguage language, string text, NormalizationOptions options)
    {
        try
        {
            return language.Pack.Normalizer.Normalize(text, options);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail("O1", $"Normalize threw {ex.GetType().Name}: {ex.Message}", new { text, options });
        }
    }

    private static bool Same(NormalizedText left, NormalizedText right)
    {
        return string.Equals(left.Text, right.Text, StringComparison.Ordinal)
            && left.Tokens.Count == right.Tokens.Count
            && left.Tokens.Zip(right.Tokens).All(static pair => pair.First.Text == pair.Second.Text && pair.First.Span == pair.Second.Span);
    }

    private static void CheckWellFormed(string oracle, NormalizedText result, string input, NormalizationOptions options)
    {
        object Context() => new { input, options, result };

        if (!result.Text.IsNormalized(NormalizationForm.FormC))
        {
            throw Determinism.Fail(oracle, "Text is not in NFC.", Context());
        }

        if (string.Join(' ', result.Tokens.Select(static t => t.Text)) != result.Text)
        {
            throw Determinism.Fail(oracle, "Text is not the single-space join of the token texts.", Context());
        }

        var previousEnd = -1;
        foreach (var token in result.Tokens)
        {
            var span = token.Span;
            if (span.Start < 0 || span.Length <= 0 || span.End > result.Text.Length)
            {
                throw Determinism.Fail(oracle, $"Span {span} of '{token.Text}' is out of range for a text of length {result.Text.Length}.", Context());
            }

            if (span.Start <= previousEnd)
            {
                throw Determinism.Fail(oracle, $"Span {span} of '{token.Text}' overlaps or precedes the previous token.", Context());
            }

            previousEnd = span.End;
            if (!string.Equals(result.Text.Substring(span.Start, span.Length), token.Text, StringComparison.Ordinal))
            {
                throw Determinism.Fail(oracle, $"Span {span} does not cover token '{token.Text}'.", Context());
            }

            if (token.Text.Any(char.IsWhiteSpace))
            {
                throw Determinism.Fail(oracle, $"Token '{token.Text}' contains whitespace.", Context());
            }
        }

        CheckOriginals(oracle, result, input, options);
    }

    // The oracle's own model of the documented repair (a lone surrogate reads as U+FFFD); the normalizer never puts one inside a word,
    // so replacing them in this copy of the input does not change any Original it looks for.
    private static string ReplaceLoneSurrogates(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                builder.Append(text[i]).Append(text[i + 1]);
                i++;
            }
            else
            {
                builder.Append(char.IsSurrogate(text[i]) ? (char)0xFFFD : text[i]);
            }
        }

        return builder.ToString();
    }

    // Token.Original is the NFC form of the source substring a token came from. With number words converted, a multi-word number joins its
    // words with single spaces, so each space-separated piece of an Original must be found in the input, and the pieces must follow the
    // input order.
    private static void CheckOriginals(string oracle, NormalizedText result, string input, NormalizationOptions options)
    {
        var nfc = ReplaceLoneSurrogates(input).Normalize(NormalizationForm.FormC);
        var cursor = 0;
        string? previousOriginal = null;
        foreach (var token in result.Tokens)
        {
            // Contraction expansion turns one source word (al, del) into two tokens that share its Original.
            if (options.ExpandContractions && string.Equals(token.Original, previousOriginal, StringComparison.Ordinal))
            {
                continue;
            }

            previousOriginal = token.Original;
            foreach (var piece in token.Original.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var found = nfc.IndexOf(piece, cursor, StringComparison.Ordinal);
                if (found < 0)
                {
                    throw Determinism.Fail(oracle, $"Original '{token.Original}' of token '{token.Text}' is not an in-order substring of the input.", new { input, options, result });
                }

                cursor = found + piece.Length;
            }
        }
    }

    // ---- O2 helpers -----------------------------------------------------------------------------------------------------------------

    private static void CheckAnalysis(string oracle, AnalysisResult result, NormalizedText text, IReadOnlySet<string> grammarIds, ErrorTagSet tags, object context)
    {
        if (result.Tokens.Count != text.Tokens.Count || result.Tokens.Zip(text.Tokens).Any(static pair => pair.First.Token != pair.Second))
        {
            throw Determinism.Fail(oracle, "The analyzed tokens are not the normalized tokens.", context);
        }

        foreach (var token in result.Tokens)
        {
            foreach (var reading in token.Readings)
            {
                if (reading.Lemma.Length == 0 || !LexiconSchema.PartsOfSpeech.Contains(reading.Pos))
                {
                    throw Determinism.Fail(oracle, $"Token '{token.Token.Text}' has an invalid reading ({reading.Lemma}/{reading.Pos}).", context);
                }
            }
        }

        if (!(result.Confidence is >= 0.0 and <= 1.0))
        {
            throw Determinism.Fail(oracle, $"Confidence {result.Confidence} is outside [0, 1].", context);
        }

        var seen = new HashSet<(string Tag, int Start, int Length)>();
        ErrorTag? previous = null;
        foreach (var error in result.Errors)
        {
            if (!tags.IsKnown(error.Tag))
            {
                throw Determinism.Fail(oracle, $"Error tag '{error.Tag}' is not in the language's error tag set.", context);
            }

            if (error.Span.Start < 0 || error.Span.Length <= 0 || error.Span.End > text.Text.Length)
            {
                throw Determinism.Fail(oracle, $"Error {error.Tag} has span {error.Span} outside the text of length {text.Text.Length}.", context);
            }

            if (!seen.Add((error.Tag, error.Span.Start, error.Span.Length)))
            {
                throw Determinism.Fail(oracle, $"Two errors share the tag {error.Tag} and span {error.Span}.", context);
            }

            if (!string.Equals(text.Text.Substring(error.Span.Start, error.Span.Length), error.Actual, StringComparison.Ordinal))
            {
                throw Determinism.Fail(oracle, $"Error {error.Tag} reports actual '{error.Actual}' but its span covers '{text.Text.Substring(error.Span.Start, error.Span.Length)}'.", context);
            }

            if (previous is { } before && (before.Span.Start, tags.Rank(before.Tag)).CompareTo((error.Span.Start, tags.Rank(error.Tag))) > 0)
            {
                throw Determinism.Fail(oracle, $"Errors are not ordered by span start and tag rank: {before.Tag}@{before.Span.Start} precedes {error.Tag}@{error.Span.Start}.", context);
            }

            previous = error;
        }

        foreach (var slot in result.Slots)
        {
            if (slot.Name.Length == 0 || slot.Span.Start < 0 || slot.Span.Length <= 0 || slot.Span.End > text.Text.Length)
            {
                throw Determinism.Fail(oracle, $"Slot '{slot.Name}' has an invalid span {slot.Span}.", context);
            }
        }

        if (result.MatchedGrammarId is null ? result.Slots.Count > 0 : !grammarIds.Contains(result.MatchedGrammarId))
        {
            throw Determinism.Fail(oracle, $"Matched grammar '{result.MatchedGrammarId ?? "(none)"}' does not fit the slots or the grammars supplied.", context);
        }
    }

    // ---- O7 helpers -----------------------------------------------------------------------------------------------------------------

    private static List<IntentDefinition> CandidatesOf(FuzzLanguage language, PromptSpec prompt)
    {
        // The resolver's own documented rule: an empty list allows every intent, otherwise only the listed ones that the catalog has.
        var allowed = prompt.AllowedIntentIds;
        return [.. language.Bundle.Intents.Intents.Where(i => allowed.Count == 0 || allowed.Contains(i.Id.Value))];
    }

    private static void CheckResolution(
        string oracle,
        ILanguagePack pack,
        IntentResolution resolution,
        bool hasEmbeddings,
        List<IntentDefinition> candidates,
        IReadOnlyList<SlotGrammar> grammars,
        AnalysisResult analysis,
        FuzzSentence? sentence,
        object context)
    {
        if (double.IsNaN(resolution.Confidence) || double.IsNaN(resolution.Margin) || resolution.Confidence < 0 || resolution.Margin < 0 || resolution.Margin > 1.0 + 1e-9 || resolution.Confidence > 1.0 + 1e-9)
        {
            throw Determinism.Fail(oracle, $"Margin {resolution.Margin} or confidence {resolution.Confidence} is outside [0, 1].", context);
        }

        var unresolved = resolution.Rung == IntentRung.Unresolved;
        if (unresolved != resolution.Intent.IsUnknown)
        {
            throw Determinism.Fail(oracle, $"Rung {resolution.Rung} and intent '{resolution.Intent}' disagree about whether anything was resolved.", context);
        }

        if (unresolved)
        {
            if (resolution.Margin != 0.0 || resolution.Confidence != 0.0 || resolution.Slots.Count != 0)
            {
                throw Determinism.Fail(oracle, $"An unresolved result must have margin 0, confidence 0 and no slots, but has {resolution.Margin}, {resolution.Confidence} and {resolution.Slots.Count} slots.", context);
            }
        }
        else if (!candidates.Any(c => c.Id == resolution.Intent))
        {
            throw Determinism.Fail(oracle, $"The resolved intent '{resolution.Intent}' is not one of the allowed intents.", context);
        }

        var someGrammarMatches = grammars.Any(g => SlotGrammarMatcher.Match(analysis.Tokens, g, pack) is not null);
        switch (resolution.Rung)
        {
            case IntentRung.Grammar:
                if (!someGrammarMatches || resolution.Margin != 1.0)
                {
                    throw Determinism.Fail(oracle, "The grammar rung decided although no allowed grammar matches, or its margin is not 1.", context);
                }

                break;
            case IntentRung.Rules:
                if (someGrammarMatches || resolution.Margin != 1.0 || resolution.Confidence != IntentResolverOptions.Default.RulesConfidence)
                {
                    throw Determinism.Fail(oracle, "The rules rung decided although a grammar matches, or its margin or confidence is not the documented one.", context);
                }

                break;
            case IntentRung.Embedding:
                if (!hasEmbeddings || someGrammarMatches
                    || IntentResolverOptions.Default.EmbeddingTop1.Evaluate(resolution.Confidence) != MarginDecision.Accept
                    || IntentResolverOptions.Default.EmbeddingGap.Evaluate(resolution.Margin) != MarginDecision.Accept)
                {
                    throw Determinism.Fail(oracle, "The embedding rung decided without a model, past a matching grammar, or without clearing both margins.", context);
                }

                break;
            default:
                break;
        }

        // The ladder's first rung: a grammar the analyzer matched (it was given only the allowed intents' grammars) decides, and the intent
        // is the first allowed intent that owns it.
        if (analysis.MatchedGrammarId is { } matched)
        {
            var owner = candidates.FirstOrDefault(c => c.Grammars.Any(g => g.Id == matched));
            if (resolution.Rung != IntentRung.Grammar || owner is null || resolution.Intent != owner.Id)
            {
                throw Determinism.Fail(oracle, $"The analyzer matched '{matched}' (owned by '{owner?.Id}') but the resolution is {resolution.Rung} '{resolution.Intent}'.", context);
            }
        }
        else if (sentence is { PatternSource: "catalog" } && candidates.Any(c => c.Grammars.Any(g => g.Id == sentence.PatternText)))
        {
            throw Determinism.Fail(oracle, $"A sentence built from the allowed grammar '{sentence.PatternText}' matched no grammar.", context);
        }
    }

    private static string PerturbedExample(FuzzLanguage language, SeededRng rng)
    {
        var examples = language.Bundle.Intents.Intents.SelectMany(static i => i.Examples).ToArray();
        var words = rng.Pick(examples).Split(' ').ToList();
        switch (rng.NextInt(3))
        {
            case 0:
                words.Reverse();
                break;
            case 1:
                if (words.Count > 1)
                {
                    words.RemoveAt(rng.NextInt(words.Count));
                }

                break;
            default:
                rng.Shuffle(words);
                break;
        }

        return string.Join(' ', words);
    }

    private static PromptSpec MakePrompt(FuzzLanguage language, SeededRng rng)
    {
        var prompt = rng.Pick(language.Bundle.Prompts.Values.ToArray());
        var intents = language.Bundle.Intents.Intents.Select(static i => i.Id.Value).ToArray();
        List<string> allowed;
        switch (rng.NextInt(10))
        {
            case 0:
            case 1:
            case 2:
            case 3:
                allowed = [.. prompt.AllowedIntentIds];
                break;
            case 4:
                allowed = [];
                break;
            case 5:
            case 6:
            case 7:
                allowed = [.. Enumerable.Range(0, 1 + rng.NextInt(4)).Select(_ => rng.Pick(intents))];
                break;
            case 8:
                allowed = [.. Enumerable.Range(0, 1 + rng.NextInt(3)).Select(_ => rng.Pick(intents)), "no_such_intent"];
                break;
            default:
                allowed = ["no_such_intent", "unknown"];
                break;
        }

        rng.Shuffle(allowed);
        return prompt with { AllowedIntentIds = allowed, Difficulty = (Difficulty)rng.NextInt(3) };
    }

    private sealed record ResolveCase(FuzzSentence? Sentence, ulong PromptSeed, int Mode, string Text);

    // ---- O14 helpers ----------------------------------------------------------------------------------------------------------------

    /// <summary>One run of the neutral pipeline oracle: the transcript, the prompt and agent indices, the recognizer set and the seed.</summary>
    private sealed record PipelineCaseLite(string Transcript, int Prompt, int Agent, bool Embeddings, ulong WorldSeed);

    private static readonly PcmClip Clip = PipelineHarness.Clip();

    private static (UtterancePipeline Pipeline, ConversationContext Context) Prepare(FuzzLanguage language, PipelineCaseLite c, PromptSpec prompt, string agentId)
    {
        var asr = new AsrResult(c.Transcript, [new AsrHypothesis(c.Transcript, 1.0)], [], 1.0, "scripted-neutral", prompt.Mode);
        var services = new PipelineServices(new ScriptedWordRecognizer(asr), null, c.Embeddings ? new FakeEmbeddingModel() : null, new SilentSynthesizer(), language.Bundle);
        var pipeline = new UtterancePipeline(services);
        var context = ConversationContext.Create(c.WorldSeed, agentId, new AgentState(agentId, "idle", 0), new StoredTimestamp(PipelineHarness.FixedNow), LearnerModel.New(), null, null, null);
        return (pipeline, context);
    }

    private static void CheckStructure(PipelineCaseLite c, PipelineTrace trace, PromptSpec prompt, ConversationContext context)
    {
        var result = trace.Result;
        var replay = result.Replay;
        if (replay.ClipHash != Clip.ClipHash || replay.PromptId != prompt.PromptId || replay.HostStateHash != context.HostStateHash || !replay.Verdict.Equals(result.Verdict))
        {
            throw Determinism.Fail("O14", "The replay record does not carry the clip hash, prompt id, host state hash and verdict of the run.", c);
        }

        var verdict = result.Verdict;
        if (!(verdict.Correctness >= 0.0 && verdict.Correctness <= 1.0) || !(verdict.Confidence >= 0.0 && verdict.Confidence <= 1.0))
        {
            throw Determinism.Fail("O14", $"Correctness {verdict.Correctness} and confidence {verdict.Confidence} must be in [0, 1].", c);
        }

        if (trace.Posteriors is not null)
        {
            throw Determinism.Fail("O14", "Posteriors are recorded for a run without a phoneme recognizer.", c);
        }

        if (result.Response.Lines.Any(static l => l.Text.Contains('{', StringComparison.Ordinal) || l.Text.Contains('}', StringComparison.Ordinal)))
        {
            throw Determinism.Fail("O14", $"A response line has an unfilled placeholder: {string.Join(" | ", result.Response.Lines.Select(static l => l.Text))}.", c);
        }
    }

    // The verdict the stages give when called by hand with the pack, in the order the pipeline calls them (the pipeline has no pronunciation here).
    private static void CheckVerdictByHand(FuzzLanguage language, PipelineCaseLite c, PipelineTrace trace, UtterancePipeline pipeline, PromptSpec prompt)
    {
        var bundle = pipeline.Services.Content;
        var pack = language.Pack;
        var services = pipeline.Services;
        var asr = services.WordRecognizer.Recognize(Clip, RecognitionRequest.Open(pipeline.Options.MaxAlternatives));
        var answerSet = bundle.AnswerSets[prompt.AnswerSetId];
        var normalized = pack.Normalizer.Normalize(asr.Transcript.Replace(GrammarBuilder.UnknownToken, " ", StringComparison.Ordinal), PipelineHarness.Normalization);
        var match = AnswerMatcher.Match(normalized, answerSet, prompt.Difficulty, pack, PipelineHarness.Normalization);
        var analysis = AnswerCorrections.WithCanonicalWords(
            new LinguisticAnalyzer(bundle.Lexicon, bundle.LanguageMorphology, pack).Analyze(normalized, GrammarsFor(bundle, prompt)),
            normalized,
            match,
            answerSet,
            pack,
            PipelineHarness.Normalization);
        var intent = prompt.AllowedIntentIds.Count == 0 ? new IntentResolution(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0) : new IntentResolver(bundle.Intents, pack, services.Embeddings).Resolve(normalized, analysis, prompt);
        var known = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var token in analysis.Tokens.Where(static t => t.Readings.Count > 0))
        {
            known.Add(token.Token.Text);
        }

        var byHand = VerdictAssembler.Assemble(new VerdictInputs(
            prompt,
            asr,
            match,
            analysis,
            intent,
            null,
            bundle.Intents.RequiredSlots(intent.Intent),
            [.. known],
            bundle.EvaluationConfig,
            services.Versions,
            [.. answerSet.Answers.Select(static answer => answer.Canonical)],
            pack.ErrorTags));
        Determinism.AssertSame("O14", "The verdict (pipeline against the stages called by hand)", c, () => trace.Result.Verdict, () => byHand);
    }

    private static void CheckDecision(PipelineCaseLite c, PipelineTrace trace, UtterancePipeline pipeline, PromptSpec prompt, ConversationContext context)
    {
        var verdict = trace.Result.Verdict;
        var decision = trace.Result.Decision;
        var bundle = pipeline.Services.Content;
        if (bundle.Agents.TryGetValue(context.AgentId, out var machine))
        {
            var expectedStep = machine.Step(context.AgentState, verdict, SeededRng.ForInteraction(context.WorldSeed, context.AgentId, context.AgentState.InteractionCounter));
            if (decision.Agent is null || !decision.Agent.Equals(expectedStep))
            {
                throw Determinism.Fail("O14", $"The agent step {CanonicalJson.Serialize(decision.Agent)} differs from the state machine's {CanonicalJson.Serialize(expectedStep)}.", c);
            }
        }
        else if (decision.Agent is not null)
        {
            throw Determinism.Fail("O14", $"An agent step was taken for '{context.AgentId}', which the bundle has no machine for.", c);
        }

        var skills = verdict.Intent.IsUnknown ? Array.Empty<string>() : [$"intent.{verdict.Intent.Value}"];
        if (!decision.Learner.Equals(context.Learner.Update(verdict, skills)))
        {
            throw Determinism.Fail("O14", "The learner model is not the context's model updated with the verdict.", c);
        }

        if (decision.Rating != RatingMapper.FromVerdict(verdict, pipeline.Options.RatingThresholds))
        {
            throw Determinism.Fail("O14", $"The rating {decision.Rating} is not the mapper's.", c);
        }

        // The context holds the counts before this utterance; the policy reads the counts after it (RepeatCounts.Next), as the pipeline passes them.
        var counts = RepeatCounts.Next(context.RepeatCountsByTag, verdict);
        if (!decision.Feedback.Equals(bundle.FeedbackPolicy.Plan(verdict, counts, prompt.Difficulty)))
        {
            throw Determinism.Fail("O14", "The feedback plan is not the policy's plan for the verdict.", c);
        }
    }

    private static void CheckReplay(PipelineCaseLite c, PipelineTrace trace, UtterancePipeline pipeline, ConversationContext context)
    {
        var envelope = ReplayEnvelope.From(trace, context);
        var json = CanonicalJson.Serialize(envelope);
        var candidates = new List<ReplayEnvelope> { envelope };

        // A recognizer returns well-formed text. Canonical JSON writes a lone surrogate as an escaped U+FFFD but a real U+FFFD as the character itself, so a
        // transcript with a lone surrogate does not survive the round trip byte for byte (the same known finding as the Spanish oracle, README); the
        // in-memory envelope still has to replay.
        if (!HasLoneSurrogate(c.Transcript))
        {
            var back = CanonicalJson.Deserialize<ReplayEnvelope>(json);
            if (!string.Equals(CanonicalJson.Serialize(back), json, StringComparison.Ordinal))
            {
                throw Determinism.Fail("O14", $"The envelope does not round-trip through canonical JSON; first difference at {Determinism.FirstDifference(json, CanonicalJson.Serialize(back))}.", c);
            }

            candidates.Add(back);
        }

        // The replayer is served the recorded outputs; a pipeline whose recognizer would say something else must not change the replay.
        var other = new UtterancePipeline(new PipelineServices(
            new ScriptedWordRecognizer(new AsrResult("otra cosa", [new AsrHypothesis("otra cosa", 1.0)], [], 1.0, "scripted-neutral", RecognitionMode.Open)),
            null,
            c.Embeddings ? new FakeEmbeddingModel() : null,
            new SilentSynthesizer(),
            pipeline.Services.Content), pipeline.Options);
        foreach (var candidate in candidates)
        {
            ReplayDiff? diff;
            try
            {
                diff = Replayer.Replay(candidate, other, _ => Clip);
            }
            catch (Exception ex)
            {
                throw Determinism.Fail("O14", $"Replay threw {ex.GetType().Name}: {ex.Message}", c);
            }

            if (diff is not null)
            {
                throw Determinism.Fail("O14", $"The replay of an unmodified envelope differs from the recording at {diff}.", c);
            }
        }
    }

    // The prompt's own grammars first, then every other grammar of the allowed intents, each once.
    private static List<SlotGrammar> GrammarsFor(ContentBundle bundle, PromptSpec prompt)
    {
        var grammars = new List<SlotGrammar>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grammar in bundle.Intents.Grammars.Where(g => prompt.SlotGrammarIds.Contains(g.Id, StringComparer.Ordinal)))
        {
            if (seen.Add(grammar.Id))
            {
                grammars.Add(grammar);
            }
        }

        foreach (var intent in bundle.Intents.Intents.Where(i => prompt.AllowedIntentIds.Contains(i.Id.Value, StringComparer.Ordinal)))
        {
            foreach (var grammar in intent.Grammars.Where(g => seen.Add(g.Id)))
            {
                grammars.Add(grammar);
            }
        }

        return grammars;
    }
}
