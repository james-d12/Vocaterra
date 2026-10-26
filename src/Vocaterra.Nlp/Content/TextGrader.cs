using System.Collections.Immutable;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Evaluation;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.SlotGrammar;

namespace Vocaterra.Nlp.Content;

/// <summary>
/// Grades a written utterance against a prompt with the real language stages: normalization, answer matching, linguistic analysis with the prompt's grammars,
/// intent resolution (no embedding model) and verdict assembly. It is the text path of <c>UtterancePipeline</c> with a recognizer that returns the text
/// as it is and no pronunciation scoring, so its verdict carries the same grammar, vocabulary and answer errors as the pipeline's for the same text
/// (a test in the pipeline test project compares the two). The content lint uses it to check example expectations; a host can use it to grade typed input.
/// </summary>
public sealed class TextGrader
{
    /// <summary>The model hash a scripted transcript carries, as in the command-line tool's <c>--transcript</c> mode.</summary>
    public const string ScriptedModelHash = "scripted-transcript";

    private static readonly NormalizationOptions Normalization = new(ConvertIndefiniteArticles: false);

    private readonly ContentBundle _bundle;
    private readonly LinguisticAnalyzer _analyzer;
    private readonly IntentResolver _resolver;
    private readonly VersionStamp _versions;

    /// <summary>Creates a grader over a bundle's lexicon, morphology, intents, answer sets and evaluation configuration.</summary>
    public TextGrader(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        _bundle = bundle;
        _analyzer = new LinguisticAnalyzer(bundle.Lexicon, bundle.Morphology);
        _resolver = new IntentResolver(bundle.Intents);
        _versions = new VersionStamp(bundle.ContentHash, ImmutableSortedDictionary<string, string>.Empty);
    }

    /// <summary>Grades the text as the answer to the prompt.</summary>
    /// <exception cref="ArgumentException">The prompt names an answer set the bundle does not have.</exception>
    public Verdict Grade(string text, PromptSpec prompt)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(prompt);
        if (!_bundle.AnswerSets.TryGetValue(prompt.AnswerSetId, out var answerSet))
        {
            throw new ArgumentException($"Prompt '{prompt.PromptId}' names unknown answer set '{prompt.AnswerSetId}'.", nameof(prompt));
        }

        var asr = new AsrResult(text, [new AsrHypothesis(text, 1.0)], [], 1.0, ScriptedModelHash, prompt.Mode);
        var normalized = TextNormalizer.Normalize(CleanTranscript(asr.Transcript), Normalization);
        var match = AnswerMatcher.Match(normalized, answerSet, prompt.Difficulty, Normalization);
        var analysis = AnswerCorrections.WithCanonicalWords(
            _analyzer.Analyze(normalized, PromptGrammars.For(_bundle.Intents, prompt)),
            normalized,
            match,
            answerSet,
            Normalization);

        // A prompt that allows no intent only asks for an answer, exactly as in the pipeline.
        var intent = prompt.AllowedIntentIds.Count == 0
            ? new IntentResolution(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0)
            : _resolver.Resolve(normalized, analysis, prompt);
        return VerdictAssembler.Assemble(new VerdictInputs(
            prompt,
            asr,
            match,
            analysis,
            intent,
            null,
            _bundle.Intents.RequiredSlots(intent.Intent),
            KnownWords(analysis),
            _bundle.EvaluationConfig,
            _versions,
            [.. answerSet.Answers.Select(static answer => answer.Canonical)]));
    }

    private static string CleanTranscript(string transcript)
    {
        return transcript.Replace(GrammarBuilder.UnknownToken, " ", StringComparison.Ordinal);
    }

    private static List<string> KnownWords(AnalysisResult analysis)
    {
        var words = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var token in analysis.Tokens)
        {
            if (token.Readings.Count > 0)
            {
                words.Add(token.Token.Text);
            }
        }

        return [.. words];
    }
}

/// <summary>The grammars the analyzer is given for a prompt: the prompt's own first (they win ties), then every other grammar of the allowed intents.</summary>
internal static class PromptGrammars
{
    public static List<SlotGrammar> For(IntentCatalog catalog, PromptSpec prompt)
    {
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
}
