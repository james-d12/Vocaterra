using CsCheck;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Evaluation;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Templates;
using Xunit.Abstractions;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O16: everything that flows through <see cref="CanonicalJson"/> survives serialize, deserialize, serialize byte for byte, and the decoded
/// value equals the original. The stage records come from the verdict generator and the real analyzer, the catalogs from the bundle and from
/// random content, and the replay envelope from real pipeline runs.
/// </summary>
public sealed class SerializationRoundTripFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O16";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    private static readonly string[] WellFormedAdversarial = [.. AdversarialCorpus.Strings.Where(static s => s.Length < 300 && IsWellFormed(s))];

    [Fact]
    public void StageRecords_SurviveCanonicalJson()
    {
        var gen = Gen.Select(VerdictGen.Inputs(Harness, allowNaN: false), SpanishTextGen.Text(Harness.Words), static (inputs, text) => (inputs, text));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);

        gen.Sample(
            ((VerdictInputs Inputs, string Text) input) =>
            {
                var inputs = input.Inputs;
                RoundTrip(inputs.Prompt, "PromptSpec", inputs.Prompt);
                RoundTrip(inputs.Asr, "AsrResult", inputs.Asr);
                RoundTrip(inputs.Match, "MatchResult", inputs.Match);
                RoundTrip(inputs.Analysis, "AnalysisResult", inputs.Analysis);
                RoundTrip(inputs.Intent, "IntentResolution", inputs.Intent);
                RoundTrip(inputs.Intent.Slots, "SlotMap", inputs.Intent.Slots);
                RoundTrip(inputs.Config, "EvaluationConfig", inputs.Config);
                RoundTrip(inputs.Versions, "VersionStamp", inputs.Versions);
                if (inputs.Pronunciation is not null)
                {
                    RoundTrip(inputs.Pronunciation, "PronunciationResult", inputs.Pronunciation);
                }

                RoundTrip(VerdictAssembler.Assemble(inputs), "Verdict", inputs);

                // The real stage outputs for arbitrary text, including the adversarial corpus.
                var normalized = PipelineHarness.Normalize(input.Text);
                RoundTrip(normalized, "NormalizedText", input.Text);
                var analysis = Harness.Analyzer.Analyze(normalized, Harness.Bundle.Intents.Grammars);
                RoundTrip(analysis, "AnalysisResult (analyzer output)", input.Text);
                var resolution = Harness.NewResolver(embeddings: false).Resolve(normalized, analysis, Harness.Bundle.Prompts.Values.First());
                RoundTrip(resolution, "IntentResolution (resolver output)", input.Text);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);
    }

    // Sentences of the grammars with a name ([WORD]): the analysis, the resolution and the slot map they bind carry the name as written, and round-trip.
    [Fact]
    public void NameSentences_SurviveCanonicalJson()
    {
        var names = Harness.Grammars.Grammars.Where(static g => g.Id.Contains("[WORD", StringComparison.Ordinal)).Select(static g => g.Pattern).ToArray();
        var gen = Gen.Select(Gen.OneOfConst(names), Gen.ULong, static (pattern, seed) => new SentenceRecipe("catalog", pattern, seed));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault) / 3;
        var realised = 0;

        gen.Sample(
            (SentenceRecipe recipe) =>
            {
                var sentence = SentenceGen.Realize(Harness, recipe);
                if (sentence is null)
                {
                    return;
                }

                realised++;
                var normalized = PipelineHarness.Normalize(sentence.Text);
                var grammar = SlotGrammarType.Parse(recipe.Pattern.Render());
                var analysis = Harness.Analyzer.Analyze(normalized, [grammar]);
                RoundTrip(normalized, "NormalizedText (with a name)", sentence.Text);
                RoundTrip(analysis, "AnalysisResult (with a name)", sentence.Text);
                var resolution = Harness.NewResolver(embeddings: false).Resolve(normalized, analysis, Harness.Bundle.Prompts.Values.First());
                RoundTrip(resolution, "IntentResolution (with a name)", sentence.Text);
                RoundTrip(resolution.Slots, "SlotMap (with a name)", sentence.Text);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        Assert.True(realised * 4 >= iterations, $"Only {realised} of {iterations} name sentences could be realised.");
    }

    [Fact]
    public void Catalogs_SurviveTheirOwnJson()
    {
        var gen = Gen.ULong;
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault) / 3;

        gen.Sample(
            (ulong seed) =>
            {
                var rng = new SeededRng(seed);
                AnswerSetRoundTrip(RandomAnswerSet(rng));
                DialogueCatalogRoundTrip(RandomDialogueCatalog(rng));
                LexiconRoundTrip(RandomLexicon(rng));
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);
    }

    [Fact]
    public void TheBundleCatalogs_SurviveTheirOwnJson()
    {
        var bundle = Harness.Bundle;
        foreach (var set in bundle.AnswerSets.Values)
        {
            AnswerSetRoundTrip(set);
        }

        DialogueCatalogRoundTrip(bundle.Dialogue);
        LexiconRoundTrip(bundle.Lexicon);
        LexiconRoundTrip(NlpLexicon.Seed);

        var catalog = IntentCatalog.Load(bundle.Intents.ToJson());
        if (catalog.ToJson() != bundle.Intents.ToJson() || catalog.ContentHash() != bundle.Intents.ContentHash())
        {
            throw Determinism.Fail(Oracle, "The intent catalog does not round-trip through its own JSON.", "bundle intents");
        }

        RoundTrip(bundle.EvaluationConfig, "EvaluationConfig", "bundle evaluation config");
        RoundTrip(bundle.FeedbackPolicy, "FeedbackPolicy", "bundle feedback policy");
        RoundTrip(bundle.Effects, "EffectTable", "bundle effects");
        RoundTrip(bundle.Provenance, "ContentProvenance", "bundle provenance");
        RoundTrip(bundle.Manifest, "ContentManifest", "bundle manifest");

        // M32: the manifest's language fields round-trip when present, and are absent from the JSON when null.
        foreach (var (language, support) in new (string?, string?)[] { ("es", null), ("de-DE", "en"), (null, "en"), ("es-419", string.Empty) })
        {
            var manifest = bundle.Manifest with { Language = language, SupportLanguage = support, BundleHash = ContentManifest.ComputeBundleHash(bundle.Manifest.Files, language) };
            RoundTrip(manifest, "ContentManifest", $"manifest language {language} support {support}");
            RoundTripManifestText(manifest);
        }
        foreach (var agent in bundle.Agents.Values)
        {
            // AgentDefinition is a plain record over lists, so it has reference equality for its members; only the JSON can be compared.
            RoundTrip(agent.Definition, "AgentDefinition", agent.AgentId, compareValues: false);
        }

        foreach (var prompt in bundle.Prompts.Values)
        {
            RoundTrip(prompt, "PromptSpec", prompt.PromptId);
        }
    }

    [Fact]
    public void ReplayEnvelopes_SurviveCanonicalJson_ForRealPipelineRuns()
    {
        var agents = Harness.Bundle.Agents.Values.Select(static machine => machine.Definition).OrderBy(static definition => definition.AgentId, StringComparer.Ordinal).ToArray();
        var prompts = Harness.Bundle.Prompts.Values.ToArray();
        var states = agents.SelectMany(static agent => agent.States.Select(state => (agent.AgentId, StateId: state.Id))).ToArray();
        var hostIntent = "intent." + Harness.Bundle.Intents.Intents[0].Id.Value;
        var gen = Gen.Select(
            SentenceGen.Recipe(Harness),
            SpanishTextGen.Text(Harness.Words),
            Gen.OneOfConst(prompts),
            Gen.OneOfConst(states),
            Gen.Int[0, 3],
            Gen.ULong,
            Gen.Bool,
            static (recipe, text, prompt, state, mode, seed, phonemes) => (recipe, text, prompt, state, mode, seed, phonemes));
        var iterations = FuzzOptions.Iterations(FuzzOptions.PipelineVar, FuzzOptions.PipelineDefault);

        gen.Sample(
            ((SentenceRecipe Recipe, string Text, Vocaterra.Nlp.Core.Prompts.PromptSpec Prompt, (string AgentId, string StateId) State, int Mode, ulong Seed, bool Phonemes) input) =>
            {
                // A recognizer returns well-formed text; canonical JSON writes a lone surrogate as U+FFFD, which is not the same text, so the
                // adversarial strings are made well-formed first (see the README, known findings).
                var transcript = ReplaceLoneSurrogates(input.Mode == 0 ? input.Text : SentenceGen.Realize(Harness, input.Recipe)?.Text ?? input.Text);

                // Pronunciation scoring grows roughly with the cube of the transcript length (0.4 s at 400 characters, over 20 s at 1 600; see the
                // README, known findings), so only a spoken-length transcript is paired with the phoneme recognizer.
                var phonemes = input.Phonemes && transcript.Length <= PipelineCase.MaxScoredTranscriptCharacters;
                var pipeline = Harness.NewPipeline(transcript, input.Prompt.Mode, phonemes);
                var withHost = input.Seed % 3 == 0;
                var context = Harness.NewContext(
                    input.State.AgentId,
                    input.State.StateId,
                    counter: (long)(input.Seed % 7),
                    worldSeed: input.Seed,
                    hostSlots: withHost ? SlotMap.From([new KeyValuePair<string, string>("target", "árbol"), new KeyValuePair<string, string>("distance", "3")]) : null,
                    extraSkills: withHost ? ["word.árbol", hostIntent] : null);
                var info = new { transcript, prompt = input.Prompt.PromptId, input.State, input.Seed, phonemes };

                PipelineTrace trace;
                try
                {
                    trace = pipeline.Trace(PipelineHarness.Clip(), input.Prompt, context);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"The pipeline threw {ex.GetType().Name} while producing a replay envelope: {ex.Message}", info);
                }

                RoundTrip(ReplayEnvelope.From(trace, context), "ReplayEnvelope", info);
                RoundTrip(trace.Result, "PipelineResult", info);
                RoundTrip(context, "ConversationContext", info);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);
    }

    private static void RoundTripManifestText(ContentManifest manifest)
    {
        // The manifest has its own writer (one file per line) next to CanonicalJson; both must carry the language fields.
        var text = manifest.ToJson();
        var back = ContentManifest.FromJson(text);
        if (!manifest.Equals(back) || !string.Equals(text, back.ToJson(), StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, "The manifest does not survive its own ToJson and FromJson with its language fields.", text);
        }

        if (text.Contains("\"language\"", StringComparison.Ordinal) != (manifest.Language is not null) || text.Contains("\"supportLanguage\"", StringComparison.Ordinal) != (manifest.SupportLanguage is not null))
        {
            throw Determinism.Fail(Oracle, "The manifest JSON writes a language field that is null, or leaves out one that is set.", text);
        }
    }

    private static void RoundTrip<T>(T value, string label, object? input, bool compareValues = true)
    {
        string first;
        try
        {
            first = CanonicalJson.Serialize(value);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"{label} could not be serialized: {ex.GetType().Name}: {ex.Message}", input);
        }

        T back;
        try
        {
            back = CanonicalJson.Deserialize<T>(first);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"{label} could not be read back from its own JSON: {ex.GetType().Name}: {ex.Message}\njson: {Shorten(first)}", input);
        }

        var second = CanonicalJson.Serialize(back);
        if (!string.Equals(first, second, StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, $"{label} does not survive serialize, deserialize, serialize; first difference at {Determinism.FirstDifference(first, second)}.\nfirst:  {Shorten(first)}\nsecond: {Shorten(second)}", input);
        }

        if (compareValues && !EqualityComparer<T>.Default.Equals(value, back))
        {
            throw Determinism.Fail(Oracle, $"{label} read back from its JSON is not equal to the original.\njson: {Shorten(first)}", input);
        }

        if (!string.Equals(CanonicalJson.Hash(value), CanonicalJson.Hash(back), StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, $"{label} has a different canonical hash after the round trip.", input);
        }
    }

    private static string ReplaceLoneSurrogates(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsHighSurrogate(chars[i]) && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(chars[i]))
            {
                chars[i] = '\ufffd';
            }
        }

        return new string(chars);
    }

    // JSON text cannot carry a lone surrogate, and no content does.
    private static bool IsWellFormed(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string Shorten(string json)
    {
        return json.Length <= 1500 ? json : string.Concat(json.AsSpan(0, 1500), "...");
    }

    private static void AnswerSetRoundTrip(AnswerSet set)
    {
        var json = set.ToJson();
        var back = AnswerSet.FromJson(json);
        if (!back.Equals(set) || back.ToJson() != json)
        {
            throw Determinism.Fail(Oracle, $"AnswerSet '{set.AnswerSetId}' does not round-trip through ToJson and FromJson.", json);
        }
    }

    private static void DialogueCatalogRoundTrip(DialogueCatalog catalog)
    {
        var json = catalog.ToJson();
        var back = DialogueCatalog.FromJson(json);
        if (!back.Equals(catalog) || back.ToJson() != json)
        {
            throw Determinism.Fail(Oracle, "The dialogue catalog does not round-trip through ToJson and FromJson.", json);
        }
    }

    private static void LexiconRoundTrip(NlpLexicon lexicon)
    {
        var json = lexicon.ToJson();
        var back = NlpLexicon.Load(json);
        if (back.ToJson() != json || back.Entries.Count != lexicon.Entries.Count)
        {
            throw Determinism.Fail(Oracle, "The lexicon does not round-trip through ToJson and Load.", json.Length > 3000 ? json[..3000] : json);
        }
    }

    private static string AnyText(SeededRng rng)
    {
        return rng.NextInt(3) == 0
            ? rng.Pick(WellFormedAdversarial.Where(static s => !string.IsNullOrWhiteSpace(s)).ToArray())
            : string.Join(' ', Enumerable.Range(0, 1 + rng.NextInt(3)).Select(_ => rng.Pick(Harness.Words.AllForms)));
    }

    private static AnswerSet RandomAnswerSet(SeededRng rng)
    {
        var answers = new List<Answer>();
        for (var i = 0; i < 1 + rng.NextInt(5); i++)
        {
            var aliases = Enumerable.Range(0, rng.NextInt(4)).Select(_ => AnyText(rng)).ToArray();
            var dialects = Enumerable.Range(0, rng.NextInt(3)).Select(_ => rng.Pick(["es-ES", "es-MX", "es-AR", "es-CO"])).ToArray();
            answers.Add(new Answer($"answer-{i}", AnyText(rng), aliases, dialects));
        }

        return new AnswerSet($"set-{rng.NextInt(100)}", answers);
    }

    private static DialogueCatalog RandomDialogueCatalog(SeededRng rng)
    {
        var pools = new List<KeyValuePair<string, VariantPool>>();
        var line = 0;
        for (var i = 0; i < 1 + rng.NextInt(4); i++)
        {
            var variants = Enumerable.Range(0, 1 + rng.NextInt(3)).Select(_ => new DialogueTemplate($"line.{line++}", AnyText(rng))).ToArray();
            pools.Add(new KeyValuePair<string, VariantPool>($"intent_{i}", new VariantPool($"pool_{i}", variants)));
        }

        return new DialogueCatalog(pools);
    }

    private static NlpLexicon RandomLexicon(SeededRng rng)
    {
        var entries = new List<LexiconEntry>();
        var source = Harness.Lexicon.Entries;
        for (var i = 0; i < 1 + rng.NextInt(40); i++)
        {
            entries.Add(source[rng.NextInt(source.Count)]);
        }

        for (var i = 0; i < rng.NextInt(10); i++)
        {
            var form = rng.Pick(Harness.Words.AllForms) + (char)('a' + rng.NextInt(26));
            entries.Add(new LexiconEntry(
                form,
                form,
                rng.Pick(LexiconSchema.PartsOfSpeech),
                new EntryFeatures(
                    rng.NextInt(2) == 0 ? null : rng.Pick(LexiconSchema.Persons),
                    rng.NextInt(2) == 0 ? null : rng.Pick(LexiconSchema.Numbers),
                    rng.NextInt(2) == 0 ? null : rng.Pick(LexiconSchema.Tenses),
                    rng.NextInt(2) == 0 ? null : rng.Pick(LexiconSchema.Moods),
                    rng.NextInt(2) == 0 ? null : rng.Pick(LexiconSchema.Genders))));
        }

        return NlpLexicon.FromEntries(entries);
    }
}
