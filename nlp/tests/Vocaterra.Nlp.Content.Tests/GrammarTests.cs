using System.Text.Json;
using Vocaterra.Nlp.ContentBuilder;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// Tests of the <c>grammar</c> command: string extraction, the JSONL reader, slot rendering, capitalisation, the cache, waivers, the learner-error
/// oracle and the exit codes. The LanguageTool server is replaced by <see cref="FakeLanguageTool"/>, so no Java runs here.
/// </summary>
public sealed class GrammarTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-grammar-" + Guid.NewGuid().ToString("N"));

    public GrammarTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string InFolder(string name)
    {
        return Path.Combine(_folder, name);
    }

    private string WriteJsonl(string name, params string[] lines)
    {
        var path = InFolder(name);
        File.WriteAllLines(path, lines);
        return path;
    }

    private static (int Code, string Output, string Error) Run(FakeLanguageTool? fake, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Program.Run(args, output, error, fake);
        return (code, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static FakeLanguageTool Rules(Func<string, IReadOnlyList<FakeMatch>> rules)
    {
        return new FakeLanguageTool { Rules = rules };
    }

    private static readonly FakeMatch Agreement = new("AGREEMENT_PRONOUNSUBJECT_VERB", "grammar", 0, 2, "tengo");

    [Fact]
    public void BundleStringsCarryTheirIdsPathsAndExampleFlags()
    {
        var items = GrammarStrings.FromBundle(TestSupport.Bundle);

        var example = items.Single(item => item.Id == "confirm.ex5");
        Assert.Equal("yo estás de acuerdo", example.Original);
        Assert.Equal(GrammarKind.Learner, example.Kind);
        Assert.Equal("intents/intents.json", example.Path);
        Assert.True(example.IsExample);
        Assert.Empty(example.ExpectedErrors!);
        Assert.Contains(items, item => item.Id.StartsWith("prompt.", StringComparison.Ordinal) && item.Path == "prompts/prompts.json" && item.Kind == GrammarKind.Npc);
        Assert.Contains(items, item => item.Path.StartsWith("answer-sets/", StringComparison.Ordinal) && item.Id.Contains(".alias", StringComparison.Ordinal));
        Assert.Contains(items, item => item.Path == "dialogue/dialogue.json" && item.Filling.Length > 0);
        Assert.DoesNotContain(items, item => string.IsNullOrWhiteSpace(item.Original));
    }

    [Fact]
    public void JsonlReaderReadsKindsErrorsAndSkipsBlankLines()
    {
        var path = WriteJsonl(
            "strings.jsonl",
            "{\"id\":\"a\",\"text\":\"Hola.\",\"kind\":\"npc\"}",
            string.Empty,
            "{\"id\":\"b\",\"text\":\"buenos día\",\"kind\":\"learner\",\"errors\":[\"noun.number\"]}",
            "{\"id\":\"c\",\"text\":\"sí\",\"kind\":\"learner\",\"errors\":[]}",
            "{\"id\":\"d\",\"text\":\"sí\",\"kind\":\"learner\"}");

        var items = GrammarStrings.FromJsonl(path, TestSupport.Bundle.TemplateEngine);

        Assert.Equal(new[] { "a", "b", "c", "d" }, items.Select(item => item.Id));
        Assert.Equal(GrammarKind.Npc, items[0].Kind);
        Assert.False(items[0].IsExample);
        Assert.True(items[1].IsExample);
        Assert.Equal(new[] { "noun.number" }, items[1].ExpectedErrors);
        Assert.True(items[2].IsExample);
        Assert.Empty(items[2].ExpectedErrors!);
        Assert.False(items[3].IsExample);
        Assert.Equal(GrammarKind.Learner, items[3].Kind);
    }

    [Theory]
    [InlineData("{not json", "not JSON")]
    [InlineData("[1,2]", "must be a JSON object")]
    [InlineData("{\"id\":\"a\",\"text\":\"x\",\"kind\":\"shouting\"}", "kind must be npc, learner or template")]
    [InlineData("{\"id\":\"a\",\"kind\":\"npc\"}", "'text' must be a non-empty string")]
    [InlineData("{\"id\":\"a\",\"text\":\"x\",\"kind\":\"npc\",\"errors\":\"conj.person\"}", "errors must be an array")]
    [InlineData("{\"id\":\"a\",\"text\":\"x\",\"kind\":\"npc\",\"errors\":[\"\"]}", "non-empty string")]
    public void JsonlReaderRefusesMalformedLines(string line, string message)
    {
        var path = WriteJsonl("bad.jsonl", line);

        var ex = Assert.Throws<GrammarInputException>(() => GrammarStrings.FromJsonl(path, TestSupport.Bundle.TemplateEngine));

        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
        Assert.Contains("bad.jsonl:1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonlReaderRefusesADuplicateId()
    {
        var path = WriteJsonl("dup.jsonl", "{\"id\":\"a\",\"text\":\"x\",\"kind\":\"npc\"}", "{\"id\":\"a\",\"text\":\"y\",\"kind\":\"npc\"}");

        var ex = Assert.Throws<GrammarInputException>(() => GrammarStrings.FromJsonl(path, TestSupport.Bundle.TemplateEngine));

        Assert.Contains("the id 'a' is given twice", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateRendersOncePerDistinctFilling()
    {
        var path = WriteJsonl("templates.jsonl", "{\"id\":\"t1\",\"text\":\"Aquí tienes {item:article(def)}.\",\"kind\":\"template\"}");

        var items = GrammarStrings.FromJsonl(path, TestSupport.Bundle.TemplateEngine);

        // Four fillings, but the article only changes with the noun's gender: masculine and feminine, the two repeats are dropped.
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Original.Contains("el libro", StringComparison.Ordinal));
        Assert.Contains(items, item => item.Original.Contains("la espada", StringComparison.Ordinal));
        Assert.All(items, item => Assert.Equal("t1", item.Id));
    }

    [Fact]
    public void TemplateWithANumberSlotRendersSingularAndPlural()
    {
        var path = WriteJsonl("plural.jsonl", "{\"id\":\"t2\",\"text\":\"Tengo {qty:words} {item:plural-if(qty)}.\",\"kind\":\"template\"}");

        var items = GrammarStrings.FromJsonl(path, TestSupport.Bundle.TemplateEngine);

        Assert.Equal(4, items.Count);
        Assert.Contains(items, item => item.Original.Contains("espadas", StringComparison.Ordinal));
        Assert.Contains(items, item => item.Original.Contains("libro.", StringComparison.Ordinal));
        Assert.All(items, item => Assert.Contains("qty=", item.Filling, StringComparison.Ordinal));
    }

    [Fact]
    public void TemplateThatDoesNotRenderIsRefused()
    {
        var path = WriteJsonl("broken.jsonl", "{\"id\":\"t3\",\"text\":\"Tengo {item:plural-if(qty,extra)}.\",\"kind\":\"template\"}");

        var ex = Assert.Throws<GrammarInputException>(() => GrammarStrings.FromJsonl(path, TestSupport.Bundle.TemplateEngine));

        Assert.Contains("t3: the template does not render", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sí", "Sí")]
    [InlineData("¿dónde está?", "¿Dónde está?")]
    [InlineData("¡hola!", "¡Hola!")]
    [InlineData("  mucho gracias", "  Mucho gracias")]
    [InlineData("Hola", "Hola")]
    [InlineData("123 cosas", "123 cosas")]
    [InlineData("", "")]
    public void LearnerStringsAreCapitalisedPastOpeningMarks(string input, string expected)
    {
        Assert.Equal(expected, GrammarStrings.Capitalise(input));
    }

    [Fact]
    public void CacheKeyChangesWithTheVersionTheSettingsAndTheText()
    {
        var key = GrammarCache.KeyFor("6.6", "language=es;level=picky;motherTongue=en", "Hola.");

        Assert.Equal(64, key.Length);
        Assert.Equal(key, GrammarCache.KeyFor("6.6", "language=es;level=picky;motherTongue=en", "Hola."));
        Assert.NotEqual(key, GrammarCache.KeyFor("6.5", "language=es;level=picky;motherTongue=en", "Hola."));
        Assert.NotEqual(key, GrammarCache.KeyFor("6.6", "language=es;level=default;motherTongue=en", "Hola."));
        Assert.NotEqual(key, GrammarCache.KeyFor("6.6", "language=es;level=picky;motherTongue=en", "Hola"));
    }

    [Fact]
    public void ServerAnswersAreReadWithTheRuleIssueTypeSpanAndThreeSuggestions()
    {
        const string body = """
            {"software":{"name":"LanguageTool","version":"6.6"},"matches":[
              {"message":"Error de concordancia.","offset":4,"length":6,
               "replacements":[{"value":"a"},{"value":"b"},{"value":"c"},{"value":"d"}],
               "rule":{"id":"AGREEMENT_DET_NOUN","issueType":"grammar"}}]}
            """;

        var (version, matches) = LanguageToolClient.Parse(body);

        Assert.Equal("6.6", version);
        var match = Assert.Single(matches);
        Assert.Equal("AGREEMENT_DET_NOUN", match.Rule);
        Assert.Equal("grammar", match.IssueType);
        Assert.Equal(4, match.Offset);
        Assert.Equal(6, match.Length);
        Assert.Equal(new[] { "a", "b", "c" }, match.Replacements);
    }

    [Fact]
    public void OnlineRunFillsTheCacheAndTheNextRunReplaysIt()
    {
        var cache = InFolder("cache.json");
        var fake = Rules(_ => []);

        var first = Run(fake, "grammar", TestSupport.BundleDir, "--cache", cache);
        var afterFirst = fake.Requests;
        var second = Run(fake, "grammar", TestSupport.BundleDir, "--cache", cache);

        Assert.Equal(0, first.Code);
        Assert.Contains("0 new", second.Error.Split('\n').First(line => line.StartsWith("cache:", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("unchanged", second.Error, StringComparison.Ordinal);
        // The second run asks the server once (the probe) and checks nothing else.
        Assert.Equal(afterFirst + 1, fake.Requests);
        Assert.Contains("grammar: 0 errors", second.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCacheFileIsSortedByKeyWithAStableFormat()
    {
        var cache = InFolder("sorted.json");
        Run(Rules(_ => []), "grammar", TestSupport.BundleDir, "--cache", cache);

        var document = JsonDocument.Parse(File.ReadAllText(cache));
        var keys = document.RootElement.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("key").GetString()!).ToList();

        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("6.6", document.RootElement.GetProperty("languageTool").GetString());
        Assert.NotEmpty(keys);
        Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
        Assert.EndsWith("\n", File.ReadAllText(cache), StringComparison.Ordinal);
    }

    [Fact]
    public void OfflineMissFailsClearlyAndAHitNeedsNoServer()
    {
        var cache = InFolder("offline.json");

        var miss = Run(null, "grammar", TestSupport.BundleDir, "--offline", "--cache", cache);
        Assert.Equal(Program.ExitInvalid, miss.Code);
        Assert.Contains("grammar.cache-miss", miss.Output, StringComparison.Ordinal);
        Assert.Contains("no cached result", miss.Output, StringComparison.Ordinal);

        // An online run fills the cache; then, with no server at all, the offline replay still passes.
        Assert.Equal(Program.ExitOk, Run(Rules(_ => []), "grammar", TestSupport.BundleDir, "--cache", cache).Code);
        var down = new FakeLanguageTool { Down = true };
        var hit = Run(down, "grammar", TestSupport.BundleDir, "--offline", "--cache", cache);
        Assert.Equal(Program.ExitOk, hit.Code);
        Assert.Equal(0, down.Requests);
    }

    [Fact]
    public void NoServerIsSkippedWithExitCodeThree()
    {
        var result = Run(new FakeLanguageTool { Down = true }, "grammar", TestSupport.BundleDir, "--cache", InFolder("none.json"));

        Assert.Equal(GrammarCommand.ExitNoServer, result.Code);
        Assert.Contains("skipped: no LanguageTool server", result.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(InFolder("none.json")));
    }

    [Fact]
    public void AServerOfAnotherVersionIsRefused()
    {
        var result = Run(new FakeLanguageTool { Version = "6.5" }, "grammar", TestSupport.BundleDir, "--cache", InFolder("v.json"));

        Assert.Equal(Program.ExitInvalid, result.Code);
        Assert.Contains("pinned to 6.6", result.Output + result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AWaivedMatchPrintsAsWaivedAndDoesNotFailStrict()
    {
        var strings = WriteJsonl("one.jsonl", "{\"id\":\"x1\",\"text\":\"mucho gracias\",\"kind\":\"learner\",\"errors\":[]}");
        var waivers = WriteJsonl(
            "waivers.json",
            "{\"schemaVersion\":1,\"waivers\":[{\"rule\":\"FAKE_GRACIAS\",\"id\":\"x1\",\"reason\":\"accepted in the test\"}]}");
        var fake = Rules(text => text.Contains("gracias", StringComparison.Ordinal) ? [new FakeMatch("FAKE_GRACIAS", "grammar", 0, 5, "x")] : []);

        var result = Run(fake, "grammar", TestSupport.BundleDir, "--strings-only", "--strings", strings, "--waivers", waivers, "--cache", InFolder("w.json"), "--strict");

        Assert.Equal(Program.ExitOk, result.Code);
        Assert.Contains("waived oracle.untagged-but-flagged", result.Output, StringComparison.Ordinal);
        Assert.Contains("waiver: accepted in the test", result.Output, StringComparison.Ordinal);
        Assert.Contains("grammar: 0 errors, 0 warnings, 0 info, 1 waived, 0 unchecked", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnusedWaiverIsAWarningAndFailsOnlyWithStrict()
    {
        var strings = WriteJsonl("none.jsonl", "{\"id\":\"x1\",\"text\":\"Hola.\",\"kind\":\"npc\"}");
        var waivers = WriteJsonl("unused.json", "{\"schemaVersion\":1,\"waivers\":[{\"rule\":\"NOPE\",\"id\":\"x1\",\"reason\":\"stale\"}]}");

        var lenient = Run(Rules(_ => []), "grammar", TestSupport.BundleDir, "--strings-only", "--strings", strings, "--waivers", waivers, "--cache", InFolder("u.json"));
        var strict = Run(Rules(_ => []), "grammar", TestSupport.BundleDir, "--strings-only", "--strings", strings, "--waivers", waivers, "--cache", InFolder("u.json"), "--strict");

        Assert.Equal(Program.ExitOk, lenient.Code);
        Assert.Contains("warning grammar.waiver-unused unused.json [NOPE, x1]", lenient.Output, StringComparison.Ordinal);
        Assert.Equal(Program.ExitInvalid, strict.Code);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"waivers\":[{\"rule\":\"NOPE\",\"id\":\"x1\"}]}", "has no reason")]
    [InlineData("{\"schemaVersion\":1,\"waivers\":[{\"rule\":\"NOPE\",\"reason\":\"why\"}]}", "exactly one of")]
    [InlineData("{\"schemaVersion\":1,\"waivers\":[{\"id\":\"x1\",\"reason\":\"why\"}]}", "needs a \"rule\"")]
    [InlineData("{\"schemaVersion\":2,\"waivers\":[]}", "schemaVersion")]
    public void AWaiverWithoutAReasonOrATargetIsRefused(string file, string message)
    {
        var strings = WriteJsonl("refuse.jsonl", "{\"id\":\"x1\",\"text\":\"Hola.\",\"kind\":\"npc\"}");
        var waivers = WriteJsonl("refuse-waivers.json", file);

        var result = Run(Rules(_ => []), "grammar", TestSupport.BundleDir, "--strings-only", "--strings", strings, "--waivers", waivers, "--cache", InFolder("r.json"));

        Assert.Equal(Program.ExitInvalid, result.Code);
        Assert.Contains(message, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExplicitWaiverFileMustExist()
    {
        var result = Run(Rules(_ => []), "grammar", TestSupport.BundleDir, "--offline", "--waivers", InFolder("missing.json"), "--cache", InFolder("m.json"));

        Assert.Equal(Program.ExitInvalid, result.Code);
        Assert.Contains("waiver file not found", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOracleClassifiesFlaggedAndTaggedBlindSpotsAndUntaggedFlags()
    {
        var strings = WriteJsonl(
            "oracle.jsonl",
            "{\"id\":\"a\",\"text\":\"yo quieres agua\",\"kind\":\"learner\",\"errors\":[\"conj.person\"]}",
            "{\"id\":\"b\",\"text\":\"yo nos vemos\",\"kind\":\"learner\",\"errors\":[\"conj.person\"]}",
            "{\"id\":\"c\",\"text\":\"yo quieres ir\",\"kind\":\"learner\",\"errors\":[]}",
            "{\"id\":\"d\",\"text\":\"yo quieres\",\"kind\":\"learner\"}");
        var fake = Rules(text => text.Contains("quieres", StringComparison.Ordinal) ? [Agreement] : []);

        var result = Run(fake, "grammar", TestSupport.BundleDir, "--strings-only", "--strings", strings, "--cache", InFolder("o.json"));

        Assert.Equal(Program.ExitOk, result.Code);
        Assert.Contains("info oracle.flagged-and-tagged oracle.jsonl [a] example \"yo quieres agua\" expects [conj.person]", result.Output, StringComparison.Ordinal);
        Assert.Contains("info oracle.tagged-but-silent oracle.jsonl [b] example \"yo nos vemos\" expects [conj.person]", result.Output, StringComparison.Ordinal);
        Assert.Contains("warning oracle.untagged-but-flagged oracle.jsonl [c] example \"yo quieres ir\" has no error tags", result.Output, StringComparison.Ordinal);
        Assert.Contains("the rule usually means [conj.person]", result.Output, StringComparison.Ordinal);
        // A plain string (not an example) is a grammar finding, not an oracle finding.
        Assert.Contains("warning grammar.AGREEMENT_PRONOUNSUBJECT_VERB oracle.jsonl [d]", result.Output, StringComparison.Ordinal);
        Assert.Contains("grammar: 0 errors, 2 warnings, 2 info, 0 waived, 0 unchecked", result.Output, StringComparison.Ordinal);
        Assert.Equal(Program.ExitInvalid, Run(fake, "grammar", TestSupport.BundleDir, "--strings-only", "--strings", strings, "--cache", InFolder("o.json"), "--strict").Code);
    }

    [Fact]
    public void JsonFormatCarriesTheSummaryAndTheFindings()
    {
        var strings = WriteJsonl("json.jsonl", "{\"id\":\"c\",\"text\":\"yo quieres ir\",\"kind\":\"learner\",\"errors\":[]}");
        var fake = Rules(text => text.Contains("quieres", StringComparison.Ordinal) ? [Agreement] : []);

        var result = Run(fake, "grammar", TestSupport.BundleDir, "--strings-only", "--strings", strings, "--cache", InFolder("j.json"), "--format", "json");

        var document = JsonDocument.Parse(result.Output);
        Assert.Equal(1, document.RootElement.GetProperty("summary").GetProperty("warnings").GetInt32());
        var finding = document.RootElement.GetProperty("findings")[0];
        Assert.Equal("warning", finding.GetProperty("severity").GetString());
        Assert.Equal("oracle.untagged-but-flagged", finding.GetProperty("check").GetString());
    }

    [Fact]
    public void UsageErrorsExitWithTwo()
    {
        Assert.Equal(Program.ExitUsage, Run(null, "grammar", TestSupport.BundleDir, "--level", "nonsense").Code);
        Assert.Equal(Program.ExitUsage, Run(null, "grammar", TestSupport.BundleDir, "--strings-only").Code);
        Assert.Equal(Program.ExitUsage, Run(null, "grammar", TestSupport.BundleDir, "--format", "xml").Code);
        Assert.Equal(Program.ExitUsage, Run(null, "grammar").Code);
    }

    [Fact]
    public void AMissingStringsFileExitsWithOne()
    {
        var result = Run(Rules(_ => []), "grammar", TestSupport.BundleDir, "--strings", InFolder("nothing.jsonl"), "--offline", "--cache", InFolder("s.json"));

        Assert.Equal(Program.ExitInvalid, result.Code);
        Assert.Contains("strings file not found", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommittedCacheReplaysTheSampleOfflineWithItsWaivers()
    {
        var committed = Path.Combine(AppContext.BaseDirectory, "grammar");
        var cache = Path.Combine(committed, "v1-cache.json");
        var waivers = Path.Combine(committed, "v1-waivers.json");

        var sample = Run(null, "grammar", TestSupport.BundleDir, "--offline", "--cache", cache, "--waivers", waivers, "--strict");
        var withStrings = Run(
            null,
            "grammar",
            TestSupport.BundleDir,
            "--offline",
            "--cache",
            cache,
            "--waivers",
            waivers,
            "--strings",
            Path.Combine(committed, "v1-learner-errors.jsonl"));

        // The three untagged examples of M27a2 are warnings, so --strict fails and the plain run passes.
        Assert.Equal(Program.ExitInvalid, sample.Code);
        Assert.Contains("grammar: 0 errors, 3 warnings, 0 info, 1 waived, 0 unchecked", sample.Output, StringComparison.Ordinal);
        Assert.Equal(Program.ExitOk, withStrings.Code);
        Assert.Contains("grammar: 0 errors, 3 warnings, 15 info, 1 waived, 0 unchecked", withStrings.Output, StringComparison.Ordinal);
        Assert.Contains("oracle.untagged-but-flagged intents/intents.json [confirm.ex5]", withStrings.Output, StringComparison.Ordinal);
    }
}
