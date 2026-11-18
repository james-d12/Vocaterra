using System.Text.Json.Nodes;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// A writable copy of the bundle (<c>content/game-v1</c>) for lint fixtures: intents, prompts, dialogue and agents are edited as JSON and the bundle is
/// loaded without hash verification (the fixtures are never hashed). The fixture lints with the game's host pools by default, as the merge gate does.
/// </summary>
internal sealed class LintFixture : IDisposable
{
    private static readonly LexiconCache Cache = new(2);

    private readonly TempBundle _bundle = new(full: true);
    private readonly JsonObject _intents;
    private readonly JsonArray _prompts;

    public LintFixture()
    {
        _intents = JsonNode.Parse(_bundle.Read("intents/intents.json"))!.AsObject();
        _prompts = JsonNode.Parse(_bundle.Read("prompts/prompts.json"))!.AsArray();
    }

    public string Directory => _bundle.Directory;

    /// <summary>Adds an intent given as JSON.</summary>
    public LintFixture Intent(string json)
    {
        _intents["intents"]!.AsArray().Add(JsonNode.Parse(json));
        return this;
    }

    /// <summary>Replaces the examples of an intent of the bundle with the given JSON array.</summary>
    public LintFixture Examples(string intentId, string jsonArray)
    {
        Find(intentId)["examples"] = JsonNode.Parse(jsonArray);
        return this;
    }

    /// <summary>Sets one member of an existing intent to the given JSON.</summary>
    public LintFixture Member(string intentId, string member, string json)
    {
        Find(intentId)[member] = JsonNode.Parse(json);
        return this;
    }

    /// <summary>The lint options the merge gate uses for the bundle: the guide's host-voiced pools are rendered by the game, so they count as reachable.</summary>
    public static LintOptions GateOptions { get; } = new() { HostPools = ["guide.*"] };

    /// <summary>Adds an open-mode prompt that allows the intents, uses the yes-no answer set and names no agent.</summary>
    public LintFixture Prompt(string promptId, params string[] allowed)
    {
        _prompts.Add(JsonNode.Parse(
            $"{{\"promptId\":\"{promptId}\",\"mode\":\"Open\",\"difficulty\":\"Beginner\",\"answerSetId\":\"yes-no\",\"allowedIntentIds\":[{string.Join(",", allowed.Select(id => $"\"{id}\""))}],\"agentId\":null,\"slotGrammarIds\":[]}}"));
        return this;
    }

    /// <summary>Applies an edit to the file as text.</summary>
    public LintFixture Edit(string relative, string oldText, string newText)
    {
        _bundle.Edit(relative, oldText, newText);
        return this;
    }

    /// <summary>Writes a file of the bundle.</summary>
    public LintFixture Write(string relative, string text)
    {
        _bundle.Write(relative, text);
        return this;
    }

    public string Read(string relative)
    {
        return _bundle.Read(relative);
    }

    public ContentBundle Load()
    {
        _bundle.Write("intents/intents.json", _intents.ToJsonString());
        _bundle.Write("prompts/prompts.json", _prompts.ToJsonString());
        return SpanishContent.Load(_bundle.Directory, new ContentLoadOptions { VerifyHashes = false, LexiconCache = Cache });
    }

    public LintReport Lint(LintOptions? options = null)
    {
        return ContentLinter.Lint(Load(), options ?? GateOptions);
    }

    public void Dispose()
    {
        _bundle.Dispose();
    }

    private JsonObject Find(string intentId)
    {
        return _intents["intents"]!.AsArray().Select(static node => node!.AsObject()).Single(node => node["intentId"]!.GetValue<string>() == intentId);
    }
}

/// <summary>Tests of the content lint library: one fixture per check proving that it fires and that it does not fire.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public class LintTests
{
    private static readonly LintReport Sample = ContentLinter.Lint(TestSupport.Bundle, LintFixture.GateOptions);

    private const string BuyIntent =
        "{\"intentId\":\"fx_buy\",\"requiredSlots\":[\"item\"],\"slotGrammars\":[\"[VERB:comprar] [ART]? [NOUN:item]\"],\"examples\":[\"compro una espada\"]}";

    private static IEnumerable<LintFinding> Of(LintReport report, string checkId, string? id = null)
    {
        return report.Findings.Where(f => f.CheckId == checkId && (id is null || f.Ids.Contains(id)));
    }

    // ---- The bundle ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheBundleLintsCleanUnderTheGateOptionsAndEveryWaiverNamesItsReason()
    {
        Assert.Equal(0, Sample.Errors);
        Assert.Equal(0, Sample.Warnings);
        Assert.True(Sample.Passes(strict: false));
        Assert.True(Sample.Passes(strict: true));
        Assert.Equal(Sample.Waived, Sample.Findings.Count(f => f.IsWaived));
        Assert.All(Sample.Findings.Where(f => f.IsWaived), f => Assert.False(string.IsNullOrWhiteSpace(f.WaivedBy)));
    }

    [Fact]
    public void TheBundleReportsAnOverlapOnlyAsAnInfoOrAWaivedFinding()
    {
        // request_item and state_want both match "necesito agua"; no prompt allows both, so it is an info (waived here by the bundle's own lint block).
        var overlap = Of(Sample, "confusion.catalog").First(f => f.Ids.SequenceEqual(["request_item", "state_want"]));

        Assert.Equal(LintSeverity.Info, overlap.Severity);
        Assert.True(overlap.IsWaived);
        Assert.Equal("state_want lint.allowOverlap request_item", overlap.WaivedBy);
        Assert.Contains("tie-break", overlap.Message, StringComparison.Ordinal);
        Assert.All(Of(Sample, "confusion.prompt"), f => Assert.True(f.IsWaived, f.ToLine()));
    }

    [Fact]
    public void TheBundleHasNoGrammarHazardsOtherThanHomographInfo()
    {
        Assert.Empty(Of(Sample, "hazard.optional-word"));
        Assert.Empty(Of(Sample, "hazard.lexicon-gap"));
        Assert.All(Of(Sample, "hazard.homograph"), f => Assert.Equal(LintSeverity.Info, f.Severity));
        Assert.Contains(Of(Sample, "hazard.homograph"), f => f.Ids.SequenceEqual(["ask_craft", "hacer"]));
        Assert.DoesNotContain(Of(Sample, "hazard.homograph"), f => f.Ids.Contains("querer"));
    }

    [Fact]
    public void TheBundleIsFullyReachableAndReservedPoolsAreNeverReported()
    {
        Assert.Empty(Of(Sample, "reach.pool"));
        // M74 read the weather intents off the prompt (an info each); the game's V29 allows them at the NPCs' open prompts, so every intent is reachable.
        Assert.Empty(Of(Sample, "reach.intent"));
        Assert.Empty(Of(Sample, "reach.answer-set"));
        Assert.Empty(Of(Sample, "reach.agent"));
        Assert.Empty(Of(Sample, "reach.state"));
    }

    [Fact]
    public void TheBundleHasNoCefrFindingsWithoutALevelAndNoExpectationFindings()
    {
        Assert.DoesNotContain(Sample.Findings, f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal));
        Assert.DoesNotContain(Sample.Findings, f => f.CheckId.StartsWith("expect.", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoRunsGiveIdenticalOutputInOrdinalOrder()
    {
        var options = new LintOptions { Level = "a1" };
        var first = ContentLinter.Lint(TestSupport.Bundle, options);
        var second = ContentLinter.Lint(SpanishContent.Load(TestSupport.BundleDir), options);

        Assert.Equal(first.Format(), second.Format());
        Assert.Equal(first.ToJson(), second.ToJson());
        Assert.Equal(first, second);
        var keys = first.Findings.Select(f => f.Path + "\n" + f.CheckId).ToList();
        Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
        Assert.EndsWith(first.SummaryLine() + "\n", first.Format(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheBundleLintTakesUnderFiveSeconds()
    {
        // Read before the clock starts: loading the bundle has its own test and budget, and only the type initializer's timing kept it out of this one.
        var bundle = TestSupport.Bundle;
        var started = System.Diagnostics.Stopwatch.StartNew();
        ContentLinter.Lint(bundle, new LintOptions { Level = "a2" });

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5 * Vocaterra.Nlp.Tests.TimingFactor.Value), $"the lint took {started.Elapsed}");
    }

    // ---- 1. Coverage -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnExampleNoGrammarOrRuleCoversIsAnErrorWithASuggestedGrammarAndACoveredOneIsNot()
    {
        using var fixture = new LintFixture().Intent(BuyIntent.Replace("\"compro una espada\"", "\"compro una espada\",\"necesito un escudo\""));

        var report = fixture.Lint();

        var uncovered = Assert.Single(Of(report, "coverage.uncovered", "fx_buy"));
        Assert.Equal(LintSeverity.Error, uncovered.Severity);
        Assert.Contains("\"necesito un escudo\"", uncovered.Message, StringComparison.Ordinal);
        Assert.Equal("[VERB:necesitar] [ART] [NOUN:item]", uncovered.Suggestion);
        Assert.Contains("Suggested grammar", uncovered.ToLine(), StringComparison.Ordinal);
        Assert.False(report.Passes(strict: false));
        Assert.DoesNotContain(Of(report, "coverage.rule-only", "fx_buy"), _ => true);
    }

    [Fact]
    public void AnExampleCoveredByAGrammarIsNotReported()
    {
        using var fixture = new LintFixture().Intent(BuyIntent);

        var report = fixture.Lint();

        Assert.DoesNotContain(report.Findings, f => f.Ids.Contains("fx_buy") && f.CheckId.StartsWith("coverage.", StringComparison.Ordinal));
    }

    [Fact]
    public void AnExampleCoveredOnlyByAKeywordRuleIsAWarningAndAWaiverMarksItWaived()
    {
        const string Thanks =
            "{\"intentId\":\"fx_thx\",\"slotGrammars\":[\"gracias\"],\"keywordRules\":[{\"lemmas\":[\"gracias\"]}],\"examples\":[\"gracias\",\"muchas gracias por todo\"]%LINT%}";
        using (var plain = new LintFixture().Intent(Thanks.Replace("%LINT%", string.Empty)))
        {
            var only = Assert.Single(Of(plain.Lint(), "coverage.rule-only", "fx_thx"));
            Assert.Equal(LintSeverity.Warning, only.Severity);
            Assert.Contains("\"muchas gracias por todo\"", only.Message, StringComparison.Ordinal);
            Assert.False(only.IsWaived);
        }

        using var waived = new LintFixture().Intent(Thanks.Replace("%LINT%", ",\"lint\":{\"allowRuleOnly\":[\"muchas gracias por todo\"]}"));
        var report = waived.Lint();
        var finding = Assert.Single(Of(report, "coverage.rule-only", "fx_thx"));
        Assert.True(finding.IsWaived);
        Assert.Equal("fx_thx lint.allowRuleOnly \"muchas gracias por todo\"", finding.WaivedBy);
        Assert.StartsWith("waived coverage.rule-only", finding.ToLine(), StringComparison.Ordinal);
        Assert.Contains("(was warning; waiver: fx_thx lint.allowRuleOnly", finding.ToLine(), StringComparison.Ordinal);
        Assert.DoesNotContain(report.Active(LintSeverity.Info), f => f.Ids.Contains("fx_thx") && f.CheckId == "coverage.rule-only");
    }

    [Fact]
    public void AnExampleWithAWordTheLexiconLacksIsAWarningUnlessItExpectsThatError()
    {
        using var plain = new LintFixture().Intent(
            "{\"intentId\":\"fx_unk\",\"slotGrammars\":[\"hola [WORD:name]\"],\"examples\":[\"hola zzzqx\"]}")
            .Intent("{\"intentId\":\"fx_unk2\",\"slotGrammars\":[\"hola amigo\"],\"examples\":[\"hola amigo\",\"hola qqqwx\"]}");

        var report = plain.Lint();

        // A [WORD] element binds the unknown token as a name, so the first intent is clean; the second has no such element.
        Assert.Empty(Of(report, "coverage.unknown-word", "fx_unk"));
        var unknown = Assert.Single(Of(report, "coverage.unknown-word", "fx_unk2"));
        Assert.Equal(LintSeverity.Warning, unknown.Severity);
        Assert.Contains("'qqqwx'", unknown.Message, StringComparison.Ordinal);
    }

    // ---- 2. Confusion ----------------------------------------------------------------------------------------------------------------

    private const string Gracias =
        "{\"intentId\":\"fx_gr\",\"slotGrammars\":[\"gracias\"],\"keywordRules\":[{\"lemmas\":[\"gracias\"]}],\"examples\":[\"gracias\"]%LINT%}";

    private const string Spell =
        "{\"intentId\":\"fx_sp\",\"requiredSlots\":[\"spell\"],\"slotGrammars\":[\"[ART]? [NOUN:spell]\"],\"examples\":[\"una bola\"]%LINT%}";

    private static LintFixture Pair(string firstLint = "", string secondLint = "", bool sharedPrompt = true)
    {
        var fixture = new LintFixture().Intent(Gracias.Replace("%LINT%", firstLint)).Intent(Spell.Replace("%LINT%", secondLint));
        return sharedPrompt ? fixture.Prompt("fx-both", "fx_gr", "fx_sp") : fixture.Prompt("fx-gr", "fx_gr").Prompt("fx-sp", "fx_sp");
    }

    [Fact]
    public void AnExampleAnotherGrammarInTheSamePromptAlsoMatchesIsAWarningNamingTheGrammarAndThePrompt()
    {
        using var fixture = Pair();

        var finding = Assert.Single(Of(fixture.Lint(), "confusion.prompt", "fx_gr"));

        Assert.Equal(LintSeverity.Warning, finding.Severity);
        Assert.Equal(["fx_gr", "fx_sp", "fx-both"], finding.Ids);
        Assert.Contains("'fx_sp' grammar \"[ART]? [NOUN:spell]\" also matches it", finding.Message, StringComparison.Ordinal);
        Assert.Contains("fx-both", finding.Message, StringComparison.Ordinal);
        Assert.Contains("tie-break", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameOverlapIsInfoWhenNoPromptAllowsBothIntents()
    {
        using var fixture = Pair(sharedPrompt: false);

        var report = fixture.Lint();

        var finding = Assert.Single(Of(report, "confusion.catalog", "fx_gr"), f => f.Ids.Contains("fx_sp"));
        Assert.Equal(LintSeverity.Info, finding.Severity);
        Assert.Empty(Of(report, "confusion.prompt", "fx_gr"));
        Assert.Contains("no prompt allows both", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(",\"lint\":{\"allowOverlap\":[\"fx_sp\"]}", "", "fx_gr lint.allowOverlap fx_sp")]
    [InlineData("", ",\"lint\":{\"allowOverlap\":[\"fx_gr\"]}", "fx_sp lint.allowOverlap fx_gr")]
    public void AnOverlapWaiverOnEitherIntentMarksTheFindingWaivedWithoutHidingIt(string firstLint, string secondLint, string waiver)
    {
        using var fixture = Pair(firstLint, secondLint);

        var report = fixture.Lint();

        var finding = Assert.Single(Of(report, "confusion.prompt", "fx_gr"));
        Assert.True(finding.IsWaived);
        Assert.Equal(waiver, finding.WaivedBy);
        Assert.Equal(LintSeverity.Warning, finding.Severity);
        Assert.Equal(Sample.Waived + 1, report.Waived);
    }

    [Fact]
    public void AnOverlapWaiverForAnotherIntentWaivesNothing()
    {
        using var fixture = Pair(",\"lint\":{\"allowOverlap\":[\"greet\"]}");

        Assert.False(Assert.Single(Of(fixture.Lint(), "confusion.prompt", "fx_gr")).IsWaived);
    }

    [Fact]
    public void AnExampleThePromptGivesToTheOtherIntentIsAnError()
    {
        // fx_rule is covered by a keyword rule only, so the grammar rung hands "bola" to fx_sp when a prompt allows both. M74: the word is one no grammar of the
        // bundle names, since a grammar the prompt does not allow takes a word it names from one that reads only its class (gracias is thank's).
        using var fixture = new LintFixture()
            .Intent("{\"intentId\":\"fx_rule\",\"keywordRules\":[{\"lemmas\":[\"bola\"]}],\"examples\":[\"bola\"]}")
            .Intent(Spell.Replace("%LINT%", string.Empty))
            .Prompt("fx-both", "fx_rule", "fx_sp");

        var finding = Assert.Single(Of(fixture.Lint(), "confusion.prompt", "fx_rule"));

        Assert.Equal(LintSeverity.Error, finding.Severity);
        Assert.Contains("the resolver gives it to 'fx_sp'", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOverlapWaiverNeverHidesAnError()
    {
        using var fixture = new LintFixture()
            .Intent("{\"intentId\":\"fx_rule\",\"keywordRules\":[{\"lemmas\":[\"gracias\"]}],\"examples\":[\"gracias\"],\"lint\":{\"allowOverlap\":[\"fx_sp\"]}}")
            .Intent(Spell.Replace("%LINT%", string.Empty))
            .Prompt("fx-both", "fx_rule", "fx_sp");

        var finding = Assert.Single(Of(fixture.Lint(), "confusion.prompt", "fx_rule"));

        Assert.Equal(LintSeverity.Error, finding.Severity);
        Assert.False(finding.IsWaived);
    }

    [Fact]
    public void DisjointIntentsProduceNoConfusionFinding()
    {
        using var fixture = new LintFixture().Intent(BuyIntent).Intent(Gracias.Replace("%LINT%", string.Empty)).Prompt("fx-both", "fx_buy", "fx_gr");

        var report = fixture.Lint();

        Assert.DoesNotContain(report.Findings, f => f.CheckId.StartsWith("confusion.", StringComparison.Ordinal) && f.Ids.Contains("fx_buy") && f.Ids.Contains("fx_gr"));
    }

    // ---- 3. Grammar hazards ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnOptionalNameNextToAnOptionalElementIsAWarningAndAnObligatoryNameIsNot()
    {
        using var fixture = new LintFixture()
            .Intent("{\"intentId\":\"fx_h1\",\"requiredSlots\":[\"item\"],\"slotGrammars\":[\"[WORD:item]? [ART]? [NOUN:x]\"],\"examples\":[\"una espada\"]}")
            .Intent("{\"intentId\":\"fx_h2\",\"requiredSlots\":[\"name\"],\"slotGrammars\":[\"me [VERB:llamar] [WORD:name]\"],\"examples\":[\"me llamo Rosa\"]}");

        var report = fixture.Lint();

        var hazard = Assert.Single(Of(report, "hazard.optional-word", "fx_h1"));
        Assert.Equal(LintSeverity.Warning, hazard.Severity);
        Assert.Contains("[WORD:item]? next to [ART]?", hazard.Message, StringComparison.Ordinal);
        Assert.Empty(Of(report, "hazard.optional-word", "fx_h2"));
    }

    [Fact]
    public void AWordLemmaOrRuleLemmaTheLexiconLacksIsAnErrorAndKnownOnesAreNot()
    {
        using var fixture = new LintFixture()
            .Intent("{\"intentId\":\"fx_g1\",\"slotGrammars\":[\"hola zzzqx\",\"[VERB:qqqwx] casa\"],\"keywordRules\":[{\"lemmas\":[\"vvvzx\"]}],\"examples\":[]}")
            .Intent("{\"intentId\":\"fx_g2\",\"slotGrammars\":[\"hola casa\",\"[VERB:comprar] casa\"],\"keywordRules\":[{\"lemmas\":[\"hola\"]}],\"examples\":[]}");

        var report = fixture.Lint();

        var gaps = Of(report, "hazard.lexicon-gap", "fx_g1").ToList();
        Assert.Equal(3, gaps.Count);
        Assert.All(gaps, gap => Assert.Equal(LintSeverity.Error, gap.Severity));
        Assert.Contains(gaps, gap => gap.Message.Contains("'zzzqx'", StringComparison.Ordinal));
        Assert.Contains(gaps, gap => gap.Message.Contains("[VERB:qqqwx]", StringComparison.Ordinal));
        Assert.Contains(gaps, gap => gap.Message.Contains("'vvvzx'", StringComparison.Ordinal));
        Assert.Empty(Of(report, "hazard.lexicon-gap", "fx_g2"));
    }

    [Fact]
    public void AVerbWhoseFormsAreAlsoNounsIsReportedAndAWaiverMarksItWaived()
    {
        using var plain = new LintFixture();
        var report = plain.Lint();
        // One finding per grammar of the intent that binds the verb first ("cómo [VERB:hacer]" and "qué [VERB:hacer]").
        var hacer = Of(report, "hazard.homograph", "hacer").Where(f => f.Ids.Contains("ask_craft")).ToList();
        Assert.NotEmpty(hacer);
        Assert.All(hacer, f => Assert.Equal(["ask_craft", "hacer"], f.Ids));
        Assert.All(hacer, f => Assert.Contains("haces", f.Message, StringComparison.Ordinal));
        Assert.All(hacer, f => Assert.False(f.IsWaived));

        using var waived = new LintFixture().Member("ask_craft", "lint", "{\"allowHomograph\":[\"hacer\"]}");
        var waivedFindings = Of(waived.Lint(), "hazard.homograph", "hacer").Where(f => f.Ids.Contains("ask_craft")).ToList();
        Assert.Equal(hacer.Count, waivedFindings.Count);
        Assert.All(waivedFindings, f => Assert.True(f.IsWaived));
        Assert.All(waivedFindings, f => Assert.Equal("ask_craft lint.allowHomograph hacer", f.WaivedBy));
    }

    // ---- 4. Reachability -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void APoolNoAgentActionUsesIsAWarningButReservedPoolsAreNot()
    {
        using var fixture = new LintFixture();
        var dialogue = JsonNode.Parse(fixture.Read("dialogue/dialogue.json"))!.AsObject();
        var pools = dialogue["pools"]!.AsObject();
        pools["fx_unused"] = JsonNode.Parse("{\"poolId\":\"fx_unused\",\"variants\":[{\"lineId\":\"fx_unused.1\",\"text\":\"Hola.\"}]}");
        pools["feedback.bogus"] = JsonNode.Parse("{\"poolId\":\"feedback.bogus\",\"variants\":[{\"lineId\":\"feedback.bogus.1\",\"text\":\"Hola.\"}]}");
        pools["feedback.hint.fx.tag"] = JsonNode.Parse("{\"poolId\":\"feedback.hint.fx.tag\",\"variants\":[{\"lineId\":\"feedback.hint.fx.tag.1\",\"text\":\"Hola.\"}]}");
        fixture.Write("dialogue/dialogue.json", dialogue.ToJsonString());

        var report = fixture.Lint();

        var unused = Assert.Single(Of(report, "reach.pool", "fx_unused"));
        Assert.Equal(LintSeverity.Warning, unused.Severity);
        Assert.Contains("used by no agent action", unused.Message, StringComparison.Ordinal);
        var bogus = Assert.Single(Of(report, "reach.pool", "feedback.bogus"));
        Assert.Contains("not 'feedback.<kind>'", bogus.Message, StringComparison.Ordinal);
        Assert.Empty(Of(report, "reach.pool", "feedback.hint.fx.tag"));
        Assert.Empty(Of(report, "reach.pool", "not_understood"));
        Assert.Empty(Of(report, "reach.pool", "answer_accepted"));
        Assert.Equal(2, Of(report, "reach.pool").Count());
    }

    [Fact]
    public void HostPoolsCountAsReachableByExactIdOrPrefix()
    {
        using var fixture = new LintFixture();
        var dialogue = JsonNode.Parse(fixture.Read("dialogue/dialogue.json"))!.AsObject();
        var pools = dialogue["pools"]!.AsObject();
        foreach (var id in new[] { "fx_a.one", "fx_a.two", "fx_b" })
        {
            pools[id] = JsonNode.Parse($"{{\"poolId\":\"{id}\",\"variants\":[{{\"lineId\":\"{id}.1\",\"text\":\"Hola.\"}}]}}");
        }

        fixture.Write("dialogue/dialogue.json", dialogue.ToJsonString());

        Assert.Equal(3, Of(fixture.Lint(), "reach.pool").Count());
        Assert.Equal(["fx_b"], Of(fixture.Lint(new LintOptions { HostPools = ["guide.*", "fx_a.*"] }), "reach.pool").Select(f => f.Ids[0]));
        Assert.Empty(Of(fixture.Lint(new LintOptions { HostPools = ["guide.*", "fx_a.*", "fx_b"] }), "reach.pool"));
    }

    [Fact]
    public void ReachabilityFromSelectedPromptsListsTheContentOnlyOtherPromptsUse()
    {
        using var fixture = new LintFixture();

        var report = fixture.Lint(new LintOptions { RootPrompts = ["villager-open"] });

        // The villager prompt names the villager, so the guide's pools, the guide, the lesson answer sets and the intents it does not allow are unreachable from it.
        Assert.Contains(Of(report, "reach.pool"), f => f.Ids.SequenceEqual(["farewell_reply", "guide"]));
        Assert.Contains(Of(report, "reach.agent"), f => f.Ids.SequenceEqual(["guide"]));
        Assert.Contains(Of(report, "reach.answer-set"), f => f.Ids.SequenceEqual(["lesson-greet"]));
        Assert.DoesNotContain(Of(report, "reach.answer-set"), f => f.Ids.Contains("places-basic"));
        Assert.Contains(Of(report, "reach.intent"), f => f.Ids.SequenceEqual(["confirm"]));
        Assert.DoesNotContain(Of(report, "reach.intent"), f => f.Ids.Contains("ask_directions"));
        Assert.Empty(Of(report, "reach.pool", "not_understood"));
        Assert.Empty(Of(report, "reach.pool", "answer_accepted"));
        Assert.Contains("none of the selected prompts", Of(report, "reach.agent", "guide").Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownRootPromptIsRejected()
    {
        var ex = Assert.Throws<ArgumentException>(() => ContentLinter.Lint(TestSupport.Bundle, new LintOptions { RootPrompts = ["nope"] }));

        Assert.Contains("'nope'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIntentNoPromptAllowsIsAWarningUnlessItIsReadOffThePrompt()
    {
        // M74: an intent with a grammar or a keyword rule is read off the prompt at every prompt that lists its intents (M72), so it is an info; one with examples
        // alone is reached by nothing.
        using var fixture = new LintFixture()
            .Intent(BuyIntent)
            .Intent("{\"intentId\":\"fx_examples\",\"examples\":[\"compro una espada\"]}");

        var report = fixture.Lint();

        Assert.Equal(LintSeverity.Info, Assert.Single(Of(report, "reach.intent", "fx_buy")).Severity);
        Assert.Equal(LintSeverity.Warning, Assert.Single(Of(report, "reach.intent", "fx_examples")).Severity);
    }

    [Fact]
    public void AnAgentStateWithNoWayInIsAWarning()
    {
        using var fixture = new LintFixture().Edit("agents/villager.json", "\"states\": [", "\"states\": [{ \"id\": \"fx_island\", \"transitions\": [] },");

        var report = fixture.Lint();

        var state = Assert.Single(Of(report, "reach.state"));
        Assert.Equal(["villager", "fx_island"], state.Ids);
        Assert.Equal("agents/villager.json", state.Path);
    }

    // ---- 5. CEFR ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CefrFindingsCoverDialoguePromptTextAndAnswerSetsAndAreWarningsByDefault()
    {
        using var fixture = LevelFixture();

        var report = fixture.Lint(new LintOptions { Level = "A1", HostPools = ["guide.*"] });

        Assert.Contains(Of(report, "cefr.dialogue"), f => f.Message.Contains("'amigo'", StringComparison.Ordinal) && f.Ids.Contains("farewell_reply.1"));
        Assert.Contains(Of(report, "cefr.prompt"), f => f.Message.Contains("'agua'", StringComparison.Ordinal) && f.Ids.Contains("guide-offer"));
        Assert.Contains(Of(report, "cefr.answer"), f => f.Message.Contains("'agua'", StringComparison.Ordinal) && f.Ids.Contains("supplies"));
        Assert.All(report.Findings.Where(f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal)), f => Assert.Equal(LintSeverity.Warning, f.Severity));
        Assert.Equal(0, report.Errors);
    }

    // The bundle itself lints clean at its ceiling, A2 (the gate), so the CEFR checks are shown on a copy whose A1 list is cut down to one word and whose A2 list adds the water.
    private static LintFixture LevelFixture()
    {
        return new LintFixture().Write("cefr/a1.txt", "hola\n").Write("cefr/a2.txt", "agua\n");
    }

    [Fact]
    public void FailOnLevelTurnsUnlistedLemmasIntoErrors()
    {
        using var fixture = LevelFixture();

        var report = fixture.Lint(new LintOptions { Level = "a1", FailOnLevel = true, HostPools = ["guide.*"] });

        Assert.All(report.Findings.Where(f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal)), f => Assert.Equal(LintSeverity.Error, f.Severity));
        Assert.True(report.Errors > 0);
        Assert.False(report.Passes(strict: false));
    }

    [Fact]
    public void ALevelAboveTheContentHasFewerFindingsAndAnUnknownLevelIsRejected()
    {
        using var fixture = LevelFixture();

        var a1 = fixture.Lint(new LintOptions { Level = "a1", HostPools = ["guide.*"] });
        var a2 = fixture.Lint(new LintOptions { Level = "a2", HostPools = ["guide.*"] });

        Assert.True(a2.Findings.Count(f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal)) < a1.Findings.Count(f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal)));
        Assert.Throws<ArgumentException>(() => ContentLinter.Lint(TestSupport.Bundle, new LintOptions { Level = "b2" }));
    }

    [Fact]
    public void TheBundleLintsCleanAtItsCeilingAndA1IsAReport()
    {
        // The game's bundle has an A2 ceiling (the game's village-plan 3.9, D-game-22 (b), V08; core M61): with --fail-on-level at A2 it lints clean, and at A1
        // a lemma above A1 is only a warning of the level's own checks, a report for the game's owner (each lesson's own level is the game's validator's).
        var ceiling = ContentLinter.Lint(TestSupport.Bundle, new LintOptions { Level = "a2", FailOnLevel = true, HostPools = ["guide.*"] });

        Assert.Equal(0, ceiling.Errors);
        Assert.Equal(0, ceiling.Warnings);

        var report = ContentLinter.Lint(TestSupport.Bundle, new LintOptions { Level = "a1", HostPools = ["guide.*"] });

        Assert.Equal(0, report.Errors);
        Assert.All(report.Active(LintSeverity.Warning), f => Assert.StartsWith("cefr.", f.CheckId, StringComparison.Ordinal));
    }

    [Fact]
    public void AnswerSetCanonicalsAreCheckedButAliasesAreNot()
    {
        using var fixture = LevelFixture();
        var report = CefrChecker.CheckAnswerSets(fixture.Load(), "a1");

        Assert.Contains(report.Missing, m => m.Lemma == "agua" && m.LineIds.Contains("supplies"));
        Assert.True(report.CoveredLemmas > 0);
        Assert.Equal(report.TotalLemmas, report.CoveredLemmas + report.Missing.Count);
    }

    // ---- 6. Example expectations -----------------------------------------------------------------------------------------------------

    [Fact]
    public void AnExampleWhoseVerdictCarriesExactlyTheExpectedTagsPassesAndStringExamplesAreNotChecked()
    {
        using var fixture = new LintFixture()
            .Examples("state_have", "[\"tengo una fruta\",{\"text\":\"sí, yo tienes una fruta\",\"errors\":[\"conj.person\"],\"prompt\":\"lesson-have\"},{\"text\":\"tengo una fruta\",\"errors\":[],\"prompt\":\"lesson-have\"}]");

        var report = fixture.Lint();

        Assert.Empty(Of(report, "expect.mismatch"));
        Assert.Equal(0, report.Errors);
    }

    [Fact]
    public void TheDesignDocDemoSentenceExpectsConjPersonAndNounNumberAgainstTheOpenPrompt()
    {
        // The worked example of the design document, in the bundle's words: a wrong verb form and a plural noun missing after "dos", asked of an open prompt.
        using var fixture = new LintFixture().Examples(
            "request_item",
            "[\"quiero dos espadas\",{\"text\":\"yo quiere dos espada\",\"errors\":[\"noun.number\",\"conj.person\"],\"prompt\":\"villager-open\"}]");

        Assert.Empty(Of(fixture.Lint(), "expect.mismatch"));
    }

    [Theory]
    [InlineData("[\"conj.person\",\"gender.agree\"]", "expects error tags [conj.person, gender.agree] but prompt 'lesson-have' gives [conj.person]")]
    [InlineData("[]", "expects error tags [] but prompt 'lesson-have' gives [conj.person]")]
    [InlineData("[\"noun.number\"]", "expects error tags [noun.number] but prompt 'lesson-have' gives [conj.person]")]
    public void AnExampleWhoseVerdictDiffersFromTheExpectationIsAnError(string expected, string message)
    {
        using var fixture = new LintFixture().Examples("state_have", "[{\"text\":\"sí, yo tienes una fruta\",\"errors\":" + expected + ",\"prompt\":\"lesson-have\"}]");

        var finding = Assert.Single(Of(fixture.Lint(), "expect.mismatch"));

        Assert.Equal(LintSeverity.Error, finding.Severity);
        Assert.Equal(["state_have", "lesson-have"], finding.Ids);
        Assert.Contains(message, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExampleMayNameThePromptItIsGradedAgainst()
    {
        // The constrained prompt expects an answer-set phrase, so a near miss of one gets vocab.wrong there (cuál es esto for qué es esto, M67 and M75); the same
        // text passes against the open prompt. A sentence the constrained prompt does not take, far from its answers, gets no error there (M72: hola, quiero dos
        // espadas at the stall, which takes no greeting, was vocab.wrong before), and a sentence it understands gets none either (M67).
        using var fixture = new LintFixture()
            .Examples(
                "ask_what",
                "[{\"text\":\"cuál es esto\",\"errors\":[\"vocab.wrong\"],\"prompt\":\"lesson-what\"},{\"text\":\"cuál es esto\",\"errors\":[],\"prompt\":\"guide-open\"}]")
            .Examples(
                "request_item",
                "[{\"text\":\"hola, quiero dos espadas\",\"errors\":[],\"prompt\":\"stall-need\"},{\"text\":\"hola, quiero dos espadas\",\"errors\":[],\"prompt\":\"villager-open\"},"
                + "{\"text\":\"quiero dos espadas\",\"errors\":[],\"prompt\":\"stall-need\"}]");

        Assert.Empty(Of(fixture.Lint(), "expect.mismatch"));
    }

    [Fact]
    public void TheDefaultPromptIsTheFirstOpenPromptThatAllowsTheIntent()
    {
        // stall-need (constrained) sorts before villager-open, but the open prompt is the one that grades sentences.
        using var fixture = new LintFixture().Examples("request_item", "[{\"text\":\"quiero dos espadas\",\"errors\":[]}]");

        Assert.Empty(Of(fixture.Lint(), "expect.mismatch"));
    }

    [Fact]
    public void AnExpectationNamingAnUnknownPromptOrOneThatDoesNotAllowTheIntentIsAnError()
    {
        using var fixture = new LintFixture().Examples(
            "state_have",
            "[{\"text\":\"tengo una fruta\",\"errors\":[],\"prompt\":\"nope\"},{\"text\":\"tengo fruta\",\"errors\":[],\"prompt\":\"villager-open\"}]");

        var report = fixture.Lint();

        Assert.Equal(LintSeverity.Error, Assert.Single(Of(report, "expect.unknown-prompt")).Severity);
        Assert.Equal(LintSeverity.Error, Assert.Single(Of(report, "expect.prompt-intent")).Severity);
    }

    [Fact]
    public void AnExpectationOnAnIntentNoPromptAllowsIsAWarningNotAnError()
    {
        using var fixture = new LintFixture().Intent("{\"intentId\":\"fx_x\",\"slotGrammars\":[\"hola amigo\"],\"examples\":[{\"text\":\"hola amigo\",\"errors\":[]}]}");

        var finding = Assert.Single(Of(fixture.Lint(), "expect.no-prompt"));

        Assert.Equal(LintSeverity.Warning, finding.Severity);
        Assert.Equal(["fx_x"], finding.Ids);
    }

    // ---- Output ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void FindingLinesCarrySeverityCheckPathAndIds()
    {
        var overlap = Of(Sample, "confusion.catalog").First(f => f.Ids.SequenceEqual(["request_item", "state_want"]));

        Assert.StartsWith("waived confusion.catalog intents/intents.json [request_item, state_want] ", overlap.ToLine(), StringComparison.Ordinal);
        Assert.Equal(
            Sample.Findings.Count + 1,
            Sample.Format().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void TheJsonReportHoldsTheSummaryAndTheFindings()
    {
        using var document = System.Text.Json.JsonDocument.Parse(Sample.ToJson());

        var root = document.RootElement;
        Assert.Equal(0, root.GetProperty("summary").GetProperty("errors").GetInt32());
        Assert.Equal(Sample.Warnings, root.GetProperty("summary").GetProperty("warnings").GetInt32());
        Assert.Equal(Sample.Findings.Count, root.GetProperty("findings").GetArrayLength());
        var first = root.GetProperty("findings")[0];
        Assert.Equal(Sample.Findings[0].CheckId, first.GetProperty("checkId").GetString());
        Assert.Equal(Sample.Findings[0].Severity.ToString(), first.GetProperty("severity").GetString());
    }

    [Fact]
    public void MinimumSeverityLeavesOutLowerFindingsButKeepsTheSummary()
    {
        var text = Sample.Format(LintSeverity.Warning);

        Assert.DoesNotContain("info ", text, StringComparison.Ordinal);
        Assert.EndsWith(Sample.SummaryLine() + "\n", text, StringComparison.Ordinal);
        // Waived findings keep the severity they had before the waiver, so a waived warning is still a line here.
        Assert.Equal(Sample.Findings.Count(f => f.Severity != LintSeverity.Info) + 1, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void TheBundleCatalogLoadsPlainAndExpectationExamplesAndLintBlocksAndWritesThemBack()
    {
        var catalog = TestSupport.Bundle.Intents;

        // A string example is a plain example; an object example carries its expected tags; the plain text is the same either way.
        Assert.All(catalog.Intents, intent => Assert.Equal(intent.Examples, intent.ExampleSpecs.Select(example => example.Text)));
        Assert.Contains(catalog.Intents, intent => intent.ExampleSpecs.Any(example => !example.HasExpectation));
        Assert.Contains(catalog.Intents, intent => intent.ExampleSpecs.Any(example => example.HasExpectation));
        Assert.Contains(catalog.Intents, intent => !intent.Lint.IsEmpty);
        Assert.Contains("\"lint\"", catalog.ToJson(), StringComparison.Ordinal);
        Assert.Contains("{\"text\"", catalog.ToJson(), StringComparison.Ordinal);
    }
}
