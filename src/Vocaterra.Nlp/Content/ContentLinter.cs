using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Content;

/// <summary>
/// The content lint: checks a loaded bundle for mistakes that the loader's reference checks cannot see, running the real linguistic analyzer, intent
/// resolver and grader on the bundle's own examples. The checks (ids in brackets) are:
/// <list type="number">
/// <item><description>Coverage (<c>coverage.uncovered</c>, <c>coverage.rule-only</c>, <c>coverage.unknown-word</c>): each intent example must resolve to its own intent; examples that only a keyword rule covers, or that nothing covers (with a suggested grammar), are reported.</description></item>
/// <item><description>Confusion (<c>confusion.prompt</c>, <c>confusion.catalog</c>): an example of one intent that another intent's grammar also matches, or that the resolver would give to another intent. Within a prompt's allow-list it is a warning (an error when the resolver gives the example to the other intent); when no prompt allows both intents it is info.</description></item>
/// <item><description>Grammar hazards (<c>hazard.optional-word</c>, <c>hazard.lexicon-gap</c>, <c>hazard.homograph</c>).</description></item>
/// <item><description>Reachability (<c>reach.pool</c>, <c>reach.intent</c>, <c>reach.answer-set</c>, <c>reach.agent</c>, <c>reach.state</c>), measured from the prompts.</description></item>
/// <item><description>CEFR (<c>cefr.dialogue</c>, <c>cefr.prompt</c>, <c>cefr.answer</c>), only when <see cref="LintOptions.Level"/> is set.</description></item>
/// <item><description>Example expectations (<c>expect.mismatch</c>, <c>expect.no-prompt</c>, <c>expect.unknown-prompt</c>, <c>expect.prompt-intent</c>): an example written as an object with <c>errors</c> is graded with <see cref="TextGrader"/> against a prompt that allows its intent, and the verdict's error tags must equal the expected set.</description></item>
/// </list>
/// Per-intent waivers (see <see cref="IntentLintWaivers"/>) mark findings as waived instead of hiding them. The report is deterministic: the same bundle and
/// options give the same findings in the same order.
/// </summary>
public static class ContentLinter
{
    /// <summary>Lints the bundle. The CEFR check needs <see cref="LintOptions.Level"/>; the bundle must have a list for it.</summary>
    /// <exception cref="ArgumentException">The level is not one the bundle has a CEFR list for, or a root prompt is not in the bundle.</exception>
    public static LintReport Lint(ContentBundle bundle, LintOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        options ??= LintOptions.Default;
        var context = new LintContext(bundle, options);
        LintCoverage.Run(context);
        LintConfusion.Run(context);
        LintHazards.Run(context);
        LintReachability.Run(context);
        LintCefr.Run(context);
        LintExpectations.Run(context);
        return context.ToReport();
    }
}

/// <summary>The analysis of one text in one prompt's context and what the resolver made of it.</summary>
internal sealed record Evaluated(AnalysisResult Analysis, IntentResolution Resolution);

/// <summary>Shared state of one lint run: the bundle, the real analyzer and resolver, a cache of evaluations and the findings so far.</summary>
internal sealed class LintContext
{
    private static readonly NormalizationOptions KeepArticles = new(ConvertIndefiniteArticles: false);

    private readonly LinguisticAnalyzer _analyzer;
    private readonly IntentResolver _resolver;
    private readonly Dictionary<(string Text, string PromptId), Evaluated> _cache = [];
    private readonly Dictionary<string, PromptSpec> _synthetic = new(StringComparer.Ordinal);
    private readonly List<LintFinding> _findings = [];

    public LintContext(ContentBundle bundle, LintOptions options)
    {
        Bundle = bundle;
        Options = options;
        _analyzer = new LinguisticAnalyzer(bundle.Lexicon, bundle.Morphology, bundle.Language);
        _resolver = new IntentResolver(bundle.Intents, bundle.Language);
        Prompts = [.. bundle.Prompts.Values.OrderBy(static prompt => prompt.PromptId, StringComparer.Ordinal)];
        if (options.RootPrompts is null)
        {
            RootPrompts = Prompts;
        }
        else
        {
            var unknown = options.RootPrompts.Where(id => !bundle.Prompts.ContainsKey(id)).ToList();
            if (unknown.Count > 0)
            {
                throw new ArgumentException($"The bundle has no prompt {string.Join(", ", unknown.Select(static id => $"'{id}'"))}.", nameof(options));
            }

            RootPrompts = [.. Prompts.Where(prompt => options.RootPrompts.Contains(prompt.PromptId, StringComparer.Ordinal))];
        }
    }

    public ContentBundle Bundle { get; }

    public LintOptions Options { get; }

    /// <summary>The bundle's prompts in ordinal id order.</summary>
    public IReadOnlyList<PromptSpec> Prompts { get; }

    /// <summary>The prompts reachability is measured from: <see cref="LintOptions.RootPrompts"/>, or every prompt.</summary>
    public IReadOnlyList<PromptSpec> RootPrompts { get; }

    /// <summary>A prompt that allows exactly the given intents (and names no answer set or grammar), for asking the resolver about those intents alone.</summary>
    public PromptSpec Only(params string[] intentIds)
    {
        var allowed = intentIds.Order(StringComparer.Ordinal).ToArray();
        var id = "lint:" + string.Join("+", allowed);
        if (!_synthetic.TryGetValue(id, out var prompt))
        {
            prompt = new PromptSpec(id, RecognitionMode.Open, Difficulty.Beginner, string.Empty, allowed, null, []);
            _synthetic.Add(id, prompt);
        }

        return prompt;
    }

    /// <summary>Analyzes and resolves the text the way the pipeline does for the prompt (the prompt's grammars first, then those of its allowed intents).</summary>
    public Evaluated Evaluate(string text, PromptSpec prompt)
    {
        var key = (text, prompt.PromptId);
        if (!_cache.TryGetValue(key, out var evaluated))
        {
            var normalized = Bundle.Language.Normalizer.Normalize(text, KeepArticles);
            var analysis = _analyzer.Analyze(normalized, PromptGrammars.For(Bundle.Intents, prompt));
            evaluated = new Evaluated(analysis, _resolver.Resolve(normalized, analysis, prompt));
            _cache.Add(key, evaluated);
        }

        return evaluated;
    }

    /// <summary>The prompts that allow all the given intents, in ordinal id order.</summary>
    public List<PromptSpec> PromptsAllowing(params string[] intentIds)
    {
        return [.. Prompts.Where(prompt => intentIds.All(id => prompt.AllowedIntentIds.Contains(id, StringComparer.Ordinal)))];
    }

    public void Add(LintSeverity severity, string checkId, string path, IReadOnlyList<string> ids, string message, string? waivedBy = null, string? suggestion = null)
    {
        _findings.Add(new LintFinding(severity, checkId, path, ids, message, waivedBy, suggestion));
    }

    public LintReport ToReport()
    {
        var sorted = _findings
            .OrderBy(static f => f.Path, StringComparer.Ordinal)
            .ThenBy(static f => f.CheckId, StringComparer.Ordinal)
            .ThenBy(static f => string.Join("\u001f", f.Ids), StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ThenBy(static f => f.Severity)
            .ToList();
        return new LintReport(sorted);
    }

    /// <summary>The waiver text when either intent waives overlap with the other, else null.</summary>
    public static string? OverlapWaiver(IntentDefinition a, IntentDefinition b)
    {
        if (a.Lint.AllowOverlap?.Contains(b.Id.Value, StringComparer.Ordinal) == true)
        {
            return $"{a.Id.Value} lint.allowOverlap {b.Id.Value}";
        }

        return b.Lint.AllowOverlap?.Contains(a.Id.Value, StringComparer.Ordinal) == true
            ? $"{b.Id.Value} lint.allowOverlap {a.Id.Value}"
            : null;
    }

    public bool Knows(string word)
    {
        var lexicon = Bundle.Lexicon;
        return lexicon.Lookup(word).Count > 0 || lexicon.ContainsLemma(word) || lexicon.SpellingsIgnoringAccents(word).Count > 0;
    }
}
