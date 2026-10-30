using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Golden.Tests;

/// <summary>
/// The Spanish identity guard (M31). It recomputes, for every input of <c>Data/identity/corpus.jsonl</c> and every dialogue template of
/// <c>content/game-v1</c>, the SHA-256 of the canonical JSON of each stage's output (truncated to 16 hex characters), and compares it with the frozen
/// <c>Data/identity/baseline.jsonl</c>. Any difference fails the test, naming the input, its source and the stage. The corpus and the baseline are data:
/// the test never draws inputs, so no generator change can move them. Set <c>VOCATERRA_RECORD_IDENTITY=1</c> to rewrite the baseline; this is only for a
/// deliberate Spanish behaviour change, in its own commit. Re-recording the end-to-end goldens never touches the baseline (they use a different variable).
/// Phase A of the language-pack work (M32 to M35, M52, M53) must not re-record it. The bundle's content hash, which the verdicts stamp, is masked
/// before hashing (M54), so an edit of <c>content/game-v1</c> that changes no Spanish behaviour moves no hash. See <c>tests/golden/README.md</c>.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public class SpanishIdentityTests
{
    private const string RecordVariable = "VOCATERRA_RECORD_IDENTITY";
    private const string CorpusName = "corpus.jsonl";
    private const string BaselineName = "baseline.jsonl";
    private const int MaxReported = 20;
    private const int DeterminismSample = 300;
    private const int HashLength = 16;
    private const string ScriptedModel = "scripted-identity";
    private const ulong ContextSeed = 20_261_026UL;
    private const long FixedTime = 1_792_000_000;
    private const string BundleHashMask = "<bundle-hash>";

    private static readonly string[] StageOrder = ["normalize", "analyze", "match", "grade", "pipeline", "recast", "g2p"];

    private static readonly Lazy<PcmClip> ClipLazy = new(static () =>
    {
        // A fixed one-second tone. The recognizer is scripted, so only the clip's presence matters.
        var samples = new short[PcmClip.DefaultSampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Round(6000.0 * Math.Sin(2.0 * Math.PI * 220.0 * i / PcmClip.DefaultSampleRate));
        }

        return PcmClip.FromSamples(samples);
    });

    private static ContentBundle Bundle => GameBundleLocator.Bundle;

    [Fact]
    public void TheHashesMatchTheFrozenBaseline()
    {
        var actual = Compute(LoadCorpus());

        if (string.Equals(Environment.GetEnvironmentVariable(RecordVariable), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(SourcePath(BaselineName), Serialize(actual), new UTF8Encoding(false));
            return;
        }

        var expected = ReadBaseline();
        var differences = new List<string>();
        var checks = 0;
        foreach (var entry in actual)
        {
            if (!expected.TryGetValue(entry.Id, out var stages))
            {
                differences.Add($"{entry.Id} [{entry.Source}]: no baseline line for this input");
                continue;
            }

            foreach (var (stage, hash) in entry.Stages)
            {
                checks++;
                if (!stages.TryGetValue(stage, out var want))
                {
                    differences.Add($"{entry.Id} [{entry.Source}] stage '{stage}': no baseline hash (input: {Quote(entry.Text)})");
                }
                else if (!string.Equals(want, hash, StringComparison.Ordinal))
                {
                    differences.Add($"{entry.Id} [{entry.Source}] stage '{stage}': expected {want} but got {hash} (input: {Quote(entry.Text)})");
                }
            }
        }

        var known = actual.Select(static e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in expected.Keys.Where(id => !known.Contains(id)).Order(StringComparer.Ordinal))
        {
            differences.Add($"{id}: the baseline has a line for an input the corpus no longer holds");
        }

        Assert.True(
            differences.Count == 0,
            $"Spanish behaviour changed: {differences.Count} of {checks} stage checks differ (first {Math.Min(MaxReported, differences.Count)} shown).\n"
            + string.Join("\n", differences.Take(MaxReported))
            + "\nA deliberate Spanish change re-records the baseline in its own commit (VOCATERRA_RECORD_IDENTITY=1); a refactor must not.");
    }

    [Fact]
    public void TheHashesAreTheSameOnASecondRunOfTheSameInputs()
    {
        var sample = LoadCorpus().Take(DeterminismSample).ToList();
        var first = Compute(sample);
        var second = Compute(sample);
        var differing = first.Zip(second).Where(pair => !pair.First.Stages.SequenceEqual(pair.Second.Stages)).Select(static pair => pair.First.Id).ToList();
        Assert.True(differing.Count == 0, $"Nondeterministic stage output for: {string.Join(", ", differing.Take(MaxReported))}.");
    }

    private static List<Entry> Compute(IEnumerable<CorpusRow> corpus)
    {
        var entries = new List<Entry>();
        var grader = new TextGrader(Bundle);
        foreach (var row in corpus)
        {
            entries.Add(new Entry(row.Id, row.Source, row.Text, StagesOf(row, grader)));
        }

        foreach (var pool in Bundle.Dialogue.Pools.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            foreach (var template in pool.Value.Variants)
            {
                var rendered = Stage(() => Bundle.TemplateEngine.Render(template, SampleSlotsFor(template)));
                entries.Add(new Entry("dialogue:" + template.LineId, "dialogue", template.Text, [new("render", rendered.Hash)]));
            }
        }

        return entries;
    }

    private static List<KeyValuePair<string, string>> StagesOf(CorpusRow row, TextGrader grader)
    {
        var prompt = Bundle.Prompts[row.Prompt];
        var stages = new List<KeyValuePair<string, string>>();

        var normalize = Stage(() => CanonicalJson.Serialize(SpanishTextNormalizer.Normalize(row.Text)));
        stages.Add(new("normalize", normalize.Hash));

        var analysis = Stage(() => CanonicalJson.Serialize(Analyze(row, prompt)));
        stages.Add(new("analyze", analysis.Hash));

        var match = Stage(() => CanonicalJson.Serialize(AnswerMatcher.Match(SpanishTextNormalizer.Normalize(row.Text), Bundle.AnswerSets[prompt.AnswerSetId], prompt.Difficulty, SpanishLanguagePack.Instance)));
        stages.Add(new("match", match.Hash));

        var grade = Stage(() => CanonicalJson.Serialize(grader.Grade(row.Text, prompt)));
        stages.Add(new("grade", grade.Hash));

        var pipeline = Stage(() => CanonicalJson.Serialize(RunPipeline(row, prompt)));
        stages.Add(new("pipeline", pipeline.Hash));

        var recast = Stage(() => CanonicalJson.Serialize(RecastBuilder.Build(SpanishTextNormalizer.Normalize(row.Text), Analyze(row, prompt), SpanishLexicon.Empty, SpanishLanguagePack.Instance)));
        stages.Add(new("recast", recast.Hash));

        var g2p = Stage(() => CanonicalJson.Serialize(SpanishG2p.PhonemizeAll(row.Text)));
        stages.Add(new("g2p", g2p.Hash));

        return stages;
    }

    private static AnalysisResult Analyze(CorpusRow row, PromptSpec prompt)
    {
        var grammars = row.Grammars is null
            ? PromptGrammarsOf(prompt)
            : row.Grammars.Select(static pattern => SlotGrammar.Parse(pattern)).ToList();
        return new LinguisticAnalyzer(Bundle.Lexicon, Bundle.SpanishMorphology(), SpanishLanguagePack.Instance).Analyze(SpanishTextNormalizer.Normalize(row.Text), grammars);
    }

    private static PipelineResult RunPipeline(CorpusRow row, PromptSpec prompt)
    {
        var asr = new AsrResult(row.Text, [new AsrHypothesis(row.Text, 1.0)], [], 1.0, ScriptedModel, prompt.Mode);
        var agent = Bundle.Agents[prompt.AgentId ?? throw new InvalidOperationException($"Prompt '{prompt.PromptId}' names no agent.")];
        var context = ConversationContext.Create(
            ContextSeed,
            agent.AgentId,
            agent.InitialState(),
            new StoredTimestamp(FixedTime),
            LearnerModel.New(),
            new Dictionary<string, int>());
        var pipeline = new UtterancePipeline(new PipelineServices(new ScriptedWordRecognizer(asr), null, null, null, Bundle));
        return pipeline.Evaluate(ClipLazy.Value, prompt, context);
    }

    // The grammars a prompt is graded with. This copies the content library's rule (PromptGrammars, internal to Content) so that the baseline
    // stays fixed even if that helper changes: the prompt's named slot grammars first, then the grammars of its allowed intents, each once.
    private static List<SlotGrammar> PromptGrammarsOf(PromptSpec prompt)
    {
        var catalog = Bundle.Intents;
        var grammars = new List<SlotGrammar>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (prompt.SlotGrammarIds.Count > 0)
        {
            var wanted = new HashSet<string>(prompt.SlotGrammarIds, StringComparer.Ordinal);
            foreach (var grammar in catalog.Grammars)
            {
                if (wanted.Contains(grammar.Id) && seen.Add(grammar.Id))
                {
                    grammars.Add(grammar);
                }
            }
        }

        var allowed = new HashSet<string>(prompt.AllowedIntentIds, StringComparer.Ordinal);
        foreach (var intent in catalog.Intents)
        {
            if (!allowed.Contains(intent.Id.Value))
            {
                continue;
            }

            foreach (var grammar in intent.Grammars)
            {
                if (seen.Add(grammar.Id))
                {
                    grammars.Add(grammar);
                }
            }
        }

        return grammars;
    }

    // Sample slot values for rendering a template. This copies the rule of the content loader's sample slots (internal to Content), frozen here.
    private static SlotMap SampleSlotsFor(DialogueTemplate template)
    {
        var kinds = TemplateEngine.SlotKinds(template);
        return SlotMap.From(TemplateEngine.ReferencedSlots(template).Select(name => new KeyValuePair<string, string>(name, SampleValue(name, kinds))));
    }

    private static string SampleValue(string name, IReadOnlyDictionary<string, TemplateSlotKind> kinds)
    {
        var reserved = name is "tag" or "expected" or "actual" or "recast";
        if (kinds.TryGetValue(name, out var kind) && kind != TemplateSlotKind.Text && !reserved)
        {
            return kind == TemplateSlotKind.Number ? "2" : "tener";
        }

        return name switch
        {
            "tag" => "gender.agree",
            "expected" => "tengo",
            "actual" => "tiene",
            "recast" => "yo tengo dos espadas",
            "qty" or "count" or "number" or "amount" or "price" => "2",
            "verb" or "action" => "tener",
            "spell" => "fuego",
            _ => "espada",
        };
    }

    private static (string Hash, bool Failed) Stage(Func<string> produce)
    {
        try
        {
            // The verdicts carry the bundle's content hash in their version stamp. It is masked (M54): the guard freezes what Spanish does, not the
            // bytes of content/game-v1, so a content edit that changes no behaviour (a game file's $schema, a moved English string) keeps every hash.
            return (Digest(produce().Replace(Bundle.ContentHash, BundleHashMask, StringComparison.Ordinal)), false);
        }
        catch (Exception ex)
        {
            // A stage that throws is part of the behaviour: its exception type and message are hashed, so a change to a throw is caught too.
            return (Digest("exception " + ex.GetType().FullName + ": " + ex.Message), true);
        }
    }

    private static string Digest(string text) => DeterministicHash.OfText(text)[..HashLength];

    private static List<CorpusRow> LoadCorpus()
    {
        var rows = new List<CorpusRow>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "identity", CorpusName);
        Assert.True(File.Exists(path), $"Corpus '{CorpusName}' is missing from the test output.");
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var grammars = root.TryGetProperty("grammars", out var g)
                ? g.EnumerateArray().Select(static x => x.GetString() ?? string.Empty).ToArray()
                : null;
            var prompt = root.GetProperty("prompt").GetString() ?? string.Empty;
            if (!Bundle.Prompts.ContainsKey(prompt))
            {
                throw new InvalidDataException($"Corpus input '{root.GetProperty("id").GetString()}' names unknown prompt '{prompt}'.");
            }

            rows.Add(new CorpusRow(
                root.GetProperty("id").GetString() ?? string.Empty,
                root.GetProperty("source").GetString() ?? string.Empty,
                root.GetProperty("text").GetString() ?? string.Empty,
                prompt,
                grammars));
        }

        return rows;
    }

    private static Dictionary<string, Dictionary<string, string>> ReadBaseline()
    {
        var baseline = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "identity", BaselineName);
        Assert.True(File.Exists(path), $"Baseline '{BaselineName}' is missing; record it once with {RecordVariable}=1.");
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var stages = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.GetProperty("stages").EnumerateObject())
            {
                stages[property.Name] = property.Value.GetString() ?? string.Empty;
            }

            baseline[document.RootElement.GetProperty("id").GetString() ?? string.Empty] = stages;
        }

        return baseline;
    }

    // One line per input: the id, then the stage hashes in the fixed stage order. Ids and stage names are plain ASCII, so they need no escaping.
    private static string Serialize(IEnumerable<Entry> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            var stages = string.Join(",", entry.Stages.Select(static s => $"\"{s.Key}\":\"{s.Value}\""));
            builder.Append("{\"id\":\"").Append(entry.Id).Append("\",\"stages\":{").Append(stages).Append("}}\n");
        }

        return builder.ToString();
    }

    private static string SourcePath(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var data = Path.Combine(directory.FullName, "tests", "Vocaterra.Nlp.Golden.Tests", "Data", "identity");
            if (Directory.Exists(data))
            {
                return Path.Combine(data, fileName);
            }
        }

        throw new DirectoryNotFoundException("Could not find tests/Vocaterra.Nlp.Golden.Tests/Data/identity above the test binaries.");
    }

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private sealed record CorpusRow(string Id, string Source, string Text, string Prompt, string[]? Grammars);

    private sealed record Entry(string Id, string Source, string Text, List<KeyValuePair<string, string>> Stages);
}
