using System.Text.Json.Nodes;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// A writable copy of the sample bundle for lint fixtures: intents, prompts, dialogue and agents are edited as JSON and the bundle is loaded without
/// hash verification (the fixtures are never hashed).
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

    /// <summary>Replaces the examples of a sample intent with the given JSON array.</summary>
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

    /// <summary>Adds an open-mode prompt that allows the intents, uses the greetings answer set and names no agent.</summary>
    public LintFixture Prompt(string promptId, params string[] allowed)
    {
        _prompts.Add(JsonNode.Parse(
            $"{{\"promptId\":\"{promptId}\",\"mode\":\"Open\",\"difficulty\":\"Beginner\",\"answerSetId\":\"greetings\",\"allowedIntentIds\":[{string.Join(",", allowed.Select(id => $"\"{id}\""))}],\"agentId\":null,\"slotGrammarIds\":[]}}"));
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
        return ContentLoader.Load(_bundle.Directory, new ContentLoadOptions { VerifyHashes = false, LexiconCache = Cache });
    }

    public LintReport Lint(LintOptions? options = null)
    {
        return ContentLinter.Lint(Load(), options);
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
public class LintTests
{
    private static readonly LintReport Sample = ContentLinter.Lint(TestSupport.Bundle);

    private const string BuyIntent =
        "{\"intentId\":\"fx_buy\",\"requiredSlots\":[\"item\"],\"slotGrammars\":[\"[VERB:comprar] [ART]? [NOUN:item]\"],\"examples\":[\"compro una espada\"]}";

    private static IEnumerable<LintFinding> Of(LintReport report, string checkId, string? id = null)
    {
        return report.Findings.Where(f => f.CheckId == checkId && (id is null || f.Ids.Contains(id)));
    }

    // ---- The sample bundle ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSampleHasNoErrorsAndNoWarningsExceptTheTwoRuleOnlyExamples()
    {
        Assert.Equal(0, Sample.Errors);
        Assert.Equal(
            ["coverage.rule-only", "coverage.rule-only"],
            Sample.Findings.Where(f => f.Severity == LintSeverity.Warning).Select(f => f.CheckId));
        Assert.Contains(Of(Sample, "coverage.rule-only"), f => f.Message.Contains("\"muchas gracias por todo\"", StringComparison.Ordinal));
        Assert.Contains(Of(Sample, "coverage.rule-only"), f => f.Message.Contains("\"mucho gracias\"", StringComparison.Ordinal));
        Assert.True(Sample.Passes(strict: false));
        Assert.False(Sample.Passes(strict: true));
    }

    [Fact]
    public void TheSampleReportsTheCastSpellThankOverlapAsInfoBecauseNoPromptAllowsBoth()
    {
        var overlap = Assert.Single(Of(Sample, "confusion.catalog"));

        Assert.Equal(LintSeverity.Info, overlap.Severity);
        Assert.Equal(["thank", "cast_spell"], overlap.Ids);
        Assert.Contains("\"gracias\"", overlap.Message, StringComparison.Ordinal);
        Assert.Contains("[VERB:lanzar]? [ART]? [NOUN:spell] [ADJ]?", overlap.Message, StringComparison.Ordinal);
        Assert.Contains("tie-break", overlap.Message, StringComparison.Ordinal);
        Assert.Empty(Of(Sample, "confusion.prompt"));
    }

    [Fact]
    public void TheSampleHasNoGrammarHazardsOtherThanHomographInfo()
    {
        Assert.Empty(Of(Sample, "hazard.optional-word"));
        Assert.Empty(Of(Sample, "hazard.lexicon-gap"));
        Assert.All(Of(Sample, "hazard.homograph"), f => Assert.Equal(LintSeverity.Info, f.Severity));
        Assert.Contains(Of(Sample, "hazard.homograph"), f => f.Ids.SequenceEqual(["ask_price", "costar"]));
        Assert.DoesNotContain(Of(Sample, "hazard.homograph"), f => f.Ids.Contains("querer"));
    }

    [Fact]
    public void TheSampleIsFullyReachableAndReservedPoolsAreNeverReported()
    {
        Assert.Empty(Of(Sample, "reach.pool"));
        Assert.Empty(Of(Sample, "reach.intent"));
        Assert.Empty(Of(Sample, "reach.answer-set"));
        Assert.Empty(Of(Sample, "reach.agent"));
        Assert.Empty(Of(Sample, "reach.state"));
    }

    [Fact]
    public void TheSampleHasNoCefrFindingsWithoutALevelAndExpectationFindingsWithoutExpectations()
    {
        Assert.DoesNotContain(Sample.Findings, f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal));
        Assert.DoesNotContain(Sample.Findings, f => f.CheckId.StartsWith("expect.", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoRunsGiveIdenticalOutputInOrdinalOrder()
    {
        var options = new LintOptions { Level = "a1" };
        var first = ContentLinter.Lint(TestSupport.Bundle, options);
        var second = ContentLinter.Lint(ContentLoader.Load(TestSupport.BundleDir), options);

        Assert.Equal(first.Format(), second.Format());
        Assert.Equal(first.ToJson(), second.ToJson());
        Assert.Equal(first, second);
        var keys = first.Findings.Select(f => f.Path + "\n" + f.CheckId).ToList();
        Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
        Assert.EndsWith(first.SummaryLine() + "\n", first.Format(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSampleLintTakesUnderFiveSeconds()
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        ContentLinter.Lint(TestSupport.Bundle, new LintOptions { Level = "a2" });

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5), $"the lint took {started.Elapsed}");
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
        Assert.Equal(1, report.Waived);
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
        // fx_rule is covered by a keyword rule only, so the grammar rung hands "gracias" to fx_sp when a prompt allows both.
        using var fixture = new LintFixture()
            .Intent("{\"intentId\":\"fx_rule\",\"keywordRules\":[{\"lemmas\":[\"gracias\"]}],\"examples\":[\"gracias\"]}")
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
        var costar = Assert.Single(Of(report, "hazard.homograph", "costar"));
        Assert.Equal(["ask_price", "costar"], costar.Ids);
        Assert.Contains("cuesta", costar.Message, StringComparison.Ordinal);
        Assert.False(costar.IsWaived);

        using var waived = new LintFixture().Member("ask_price", "lint", "{\"allowHomograph\":[\"costar\"]}");
        var waivedFinding = Assert.Single(Of(waived.Lint(), "hazard.homograph", "costar"));
        Assert.True(waivedFinding.IsWaived);
        Assert.Equal("ask_price lint.allowHomograph costar", waivedFinding.WaivedBy);
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
        Assert.Equal(["fx_b"], Of(fixture.Lint(new LintOptions { HostPools = ["fx_a.*"] }), "reach.pool").Select(f => f.Ids[0]));
        Assert.Empty(Of(fixture.Lint(new LintOptions { HostPools = ["fx_a.*", "fx_b"] }), "reach.pool"));
    }

    [Fact]
    public void ReachabilityFromSelectedPromptsListsTheContentOnlyOtherPromptsUse()
    {
        using var fixture = new LintFixture();

        var report = fixture.Lint(new LintOptions { RootPrompts = ["spell-cast-open", "quiz-spell-name"] });

        // Neither prompt names the blacksmith, so all its pools, its agent, the other answer sets and the other intents are unreachable from them.
        Assert.Contains(Of(report, "reach.pool"), f => f.Ids.SequenceEqual(["greet_reply", "blacksmith"]));
        Assert.Contains(Of(report, "reach.agent"), f => f.Ids.SequenceEqual(["blacksmith"]));
        Assert.Contains(Of(report, "reach.answer-set"), f => f.Ids.SequenceEqual(["greetings"]));
        Assert.DoesNotContain(Of(report, "reach.answer-set"), f => f.Ids.Contains("spells-basic"));
        Assert.Contains(Of(report, "reach.intent"), f => f.Ids.SequenceEqual(["greet"]));
        Assert.DoesNotContain(Of(report, "reach.intent"), f => f.Ids.Contains("cast_spell"));
        Assert.Empty(Of(report, "reach.pool", "not_understood"));
        Assert.Empty(Of(report, "reach.pool", "answer_accepted"));
        Assert.Contains("none of the selected prompts", Of(report, "reach.agent").Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownRootPromptIsRejected()
    {
        var ex = Assert.Throws<ArgumentException>(() => ContentLinter.Lint(TestSupport.Bundle, new LintOptions { RootPrompts = ["nope"] }));

        Assert.Contains("'nope'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIntentNoPromptAllowsIsAWarning()
    {
        using var fixture = new LintFixture().Intent(BuyIntent);

        var report = fixture.Lint();

        Assert.Single(Of(report, "reach.intent", "fx_buy"));
        Assert.Single(Of(report, "reach.intent"));
    }

    [Fact]
    public void AnAgentStateWithNoWayInIsAWarning()
    {
        using var fixture = new LintFixture().Edit("agents/blacksmith.json", "\"states\": [", "\"states\": [{ \"id\": \"fx_island\", \"transitions\": [] },");

        var report = fixture.Lint();

        var state = Assert.Single(Of(report, "reach.state"));
        Assert.Equal(["blacksmith", "fx_island"], state.Ids);
        Assert.Equal("agents/blacksmith.json", state.Path);
    }

    // ---- 5. CEFR ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CefrFindingsCoverDialoguePromptTextAndAnswerSetsAndAreWarningsByDefault()
    {
        var report = ContentLinter.Lint(TestSupport.Bundle, new LintOptions { Level = "A1" });

        Assert.Contains(Of(report, "cefr.dialogue"), f => f.Message.Contains("'oro'", StringComparison.Ordinal) && f.Ids.Contains("ask_price_reply.1"));
        Assert.Contains(Of(report, "cefr.prompt"), f => f.Message.Contains("'espada'", StringComparison.Ordinal) && f.Ids.Contains("ask-price-open"));
        Assert.Contains(Of(report, "cefr.answer"), f => f.Message.Contains("'espada'", StringComparison.Ordinal) && f.Ids.Contains("items-basic"));
        Assert.All(report.Findings.Where(f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal)), f => Assert.Equal(LintSeverity.Warning, f.Severity));
        Assert.Equal(0, report.Errors);
    }

    [Fact]
    public void FailOnLevelTurnsUnlistedLemmasIntoErrors()
    {
        var report = ContentLinter.Lint(TestSupport.Bundle, new LintOptions { Level = "a1", FailOnLevel = true });

        Assert.All(report.Findings.Where(f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal)), f => Assert.Equal(LintSeverity.Error, f.Severity));
        Assert.True(report.Errors > 0);
        Assert.False(report.Passes(strict: false));
    }

    [Fact]
    public void ALevelAboveTheContentHasFewerFindingsAndAnUnknownLevelIsRejected()
    {
        var a1 = ContentLinter.Lint(TestSupport.Bundle, new LintOptions { Level = "a1" });
        var a2 = ContentLinter.Lint(TestSupport.Bundle, new LintOptions { Level = "a2" });

        Assert.True(a2.Findings.Count(f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal)) < a1.Findings.Count(f => f.CheckId.StartsWith("cefr.", StringComparison.Ordinal)));
        Assert.Throws<ArgumentException>(() => ContentLinter.Lint(TestSupport.Bundle, new LintOptions { Level = "b2" }));
    }

    [Fact]
    public void AnswerSetCanonicalsAreCheckedButAliasesAreNot()
    {
        var report = CefrChecker.CheckAnswerSets(TestSupport.Bundle, "a1");

        Assert.Contains(report.Missing, m => m.Lemma == "espada" && m.LineIds.Contains("items-basic"));
        Assert.True(report.CoveredLemmas > 0);
        Assert.Equal(report.TotalLemmas, report.CoveredLemmas + report.Missing.Count);
    }

    // ---- 6. Example expectations -----------------------------------------------------------------------------------------------------

    [Fact]
    public void AnExampleWhoseVerdictCarriesExactlyTheExpectedTagsPassesAndStringExamplesAreNotChecked()
    {
        using var fixture = new LintFixture()
            .Examples("introduce_self", "[\"me llamo Rosa\",{\"text\":\"yo es Rosa\",\"errors\":[\"conj.person\"]},{\"text\":\"soy Sol\",\"errors\":[]}]");

        var report = fixture.Lint();

        Assert.Empty(Of(report, "expect.mismatch"));
        Assert.Equal(0, report.Errors);
    }

    [Fact]
    public void TheReadmeDemoSentenceExpectsConjPersonAndNounNumberAgainstTheOpenPrompt()
    {
        using var fixture = new LintFixture().Examples("offer_item", "[\"tengo dos espadas\",{\"text\":\"yo tiene dos espada\",\"errors\":[\"noun.number\",\"conj.person\"]}]");

        Assert.Empty(Of(fixture.Lint(), "expect.mismatch"));
    }

    [Theory]
    [InlineData("[\"conj.person\",\"gender.agree\"]", "expects error tags [conj.person, gender.agree] but prompt 'introduce-open' gives [conj.person]")]
    [InlineData("[]", "expects error tags [] but prompt 'introduce-open' gives [conj.person]")]
    [InlineData("[\"noun.number\"]", "expects error tags [noun.number] but prompt 'introduce-open' gives [conj.person]")]
    public void AnExampleWhoseVerdictDiffersFromTheExpectationIsAnError(string expected, string message)
    {
        using var fixture = new LintFixture().Examples("introduce_self", "[{\"text\":\"yo es Rosa\",\"errors\":" + expected + "}]");

        var finding = Assert.Single(Of(fixture.Lint(), "expect.mismatch"));

        Assert.Equal(LintSeverity.Error, finding.Severity);
        Assert.Equal(["introduce_self", "introduce-open"], finding.Ids);
        Assert.Contains(message, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExampleMayNameThePromptItIsGradedAgainst()
    {
        // The constrained prompt expects an answer-set phrase, so a sentence gets vocab.wrong there; the same text passes against the open prompt.
        using var fixture = new LintFixture().Examples(
            "offer_item",
            "[{\"text\":\"tengo dos espadas\",\"errors\":[\"vocab.wrong\"],\"prompt\":\"offer-items-constrained\"},{\"text\":\"tengo tres escudos\",\"errors\":[],\"prompt\":\"offer-items-open\"}]");

        Assert.Empty(Of(fixture.Lint(), "expect.mismatch"));
    }

    [Fact]
    public void TheDefaultPromptIsTheFirstOpenPromptThatAllowsTheIntent()
    {
        // offer-items-constrained sorts before offer-items-open, but the open prompt is the one that grades sentences.
        using var fixture = new LintFixture().Examples("offer_item", "[{\"text\":\"tengo dos espadas\",\"errors\":[]}]");

        Assert.Empty(Of(fixture.Lint(), "expect.mismatch"));
    }

    [Fact]
    public void AnExpectationNamingAnUnknownPromptOrOneThatDoesNotAllowTheIntentIsAnError()
    {
        using var fixture = new LintFixture().Examples(
            "introduce_self",
            "[{\"text\":\"soy Sol\",\"errors\":[],\"prompt\":\"nope\"},{\"text\":\"soy Mar\",\"errors\":[],\"prompt\":\"greet-open\"}]");

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
        var overlap = Of(Sample, "confusion.catalog").Single();

        Assert.StartsWith("info confusion.catalog intents/intents.json [thank, cast_spell] ", overlap.ToLine(), StringComparison.Ordinal);
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
        Assert.Equal(Sample.Warnings + Sample.Errors + 1, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void TheSampleCatalogLoadsStringExamplesAsPlainExamples()
    {
        var catalog = TestSupport.Bundle.Intents;

        Assert.All(catalog.Intents, intent => Assert.All(intent.ExampleSpecs, example => Assert.False(example.HasExpectation)));
        Assert.All(catalog.Intents, intent => Assert.True(intent.Lint.IsEmpty));
        Assert.DoesNotContain("\"lint\"", catalog.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("{\"text\"", catalog.ToJson(), StringComparison.Ordinal);
    }
}
