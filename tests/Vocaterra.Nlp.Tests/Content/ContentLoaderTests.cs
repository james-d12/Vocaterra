using System.Text;
using System.Text.Json.Nodes;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Tests for <see cref="ContentLoader"/>: success, hash verification and cross-reference validation.</summary>
public class ContentLoaderTests
{
    private static ContentValidationException LoadFails(string dir, ContentLoadOptions? options = null)
    {
        return Assert.Throws<ContentValidationException>(() => ContentLoader.Load(dir, options));
    }

    [Fact]
    public void LoadsTheSampleBundleWithHashVerification()
    {
        var bundle = ContentLoader.Load(TestSupport.BundleDir, new ContentLoadOptions { VerifyHashes = true });

        Assert.Equal(bundle.Manifest.BundleHash, bundle.ContentHash);
        Assert.Equal("game-v1", bundle.Manifest.BundleId);
        Assert.Equal(ContentManifest.CurrentSchemaVersion, bundle.Manifest.SchemaVersion);
        Assert.Equal(64, bundle.ContentHash.Length);
    }

    [Fact]
    public void VerifyHashesIsOnByDefault()
    {
        Assert.True(new ContentLoadOptions().VerifyHashes);
        Assert.True(ContentLoadOptions.Default.VerifyHashes);
        using var bundle = new TempBundle();
        bundle.Touch("answer-sets/yes-no.json");

        Assert.Contains(LoadFails(bundle.Directory).Problems, problem => problem.StartsWith("answer-sets/yes-no.json: hash mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadingTwiceGivesTheSameContent()
    {
        var first = ContentLoader.Load(TestSupport.BundleDir);
        var second = ContentLoader.Load(TestSupport.BundleDir);

        Assert.Equal(first.Manifest, second.Manifest);
        Assert.Equal(first.Intents.ContentHash(), second.Intents.ContentHash());
        Assert.Equal(first.Dialogue, second.Dialogue);
        Assert.Equal(first.Lexicon.ToJson(), second.Lexicon.ToJson());
        Assert.Equal(first.AnswerSets.Keys, second.AnswerSets.Keys);
    }

    [Fact]
    public void ACrlfCheckoutStillVerifies()
    {
        using var bundle = new TempBundle(full: true);
        var before = ContentManifest.Compute(bundle.Directory).BundleHash;
        foreach (var path in new[] { "intents/intents.json", "dialogue/dialogue.json", "cefr/a1.txt", "ATTRIBUTION.md" })
        {
            bundle.Write(path, bundle.Read(path).Replace("\n", "\r\n", StringComparison.Ordinal));
        }

        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.Equal(before, loaded.ContentHash);
    }

    [Fact]
    public void ATamperedFileIsRejectedAndNamed()
    {
        using var bundle = new TempBundle();
        bundle.Touch("prompts/prompts.json");

        var problems = LoadFails(bundle.Directory).Problems;

        var problem = Assert.Single(problems);
        Assert.StartsWith("prompts/prompts.json: hash mismatch", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExceptionMessageListsEveryProblem()
    {
        using var bundle = new TempBundle();
        bundle.Touch("prompts/prompts.json");
        bundle.Touch("effects/effects.json");
        bundle.Delete("answer-sets/places-basic.json");

        var exception = LoadFails(bundle.Directory);

        Assert.Contains(exception.Problems, p => p.StartsWith("prompts/prompts.json: hash mismatch", StringComparison.Ordinal));
        Assert.Contains(exception.Problems, p => p.StartsWith("effects/effects.json: hash mismatch", StringComparison.Ordinal));
        Assert.Contains(exception.Problems, p => p.StartsWith("answer-sets/places-basic.json: file is missing", StringComparison.Ordinal));
        foreach (var problem in exception.Problems)
        {
            Assert.Contains(problem, exception.Message, StringComparison.Ordinal);
        }

        Assert.StartsWith("Content bundle is invalid (", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TamperingIsIgnoredWhenHashesAreNotVerified()
    {
        using var bundle = new TempBundle();
        bundle.EditJson("answer-sets/yes-no.json", root => root["answers"]![0]!["aliases"]!.AsArray().Add("claro amigo"));

        var loaded = ContentLoader.Load(bundle.Directory, new ContentLoadOptions { VerifyHashes = false });

        Assert.Contains("claro amigo", loaded.AnswerSets["yes-no"].GrammarPhrases(SpanishLanguagePack.Instance));
    }

    [Fact]
    public void AMissingFileIsReported()
    {
        using var bundle = new TempBundle();
        bundle.Delete("answer-sets/yes-no.json");

        var problems = LoadFails(bundle.Directory).Problems;

        Assert.Contains("answer-sets/yes-no.json: file is missing (listed in manifest.json).", problems);
        Assert.Contains(problems, p => p.Contains("unknown answer set 'yes-no'", StringComparison.Ordinal));
    }

    [Fact]
    public void AFileThatIsNotInTheManifestIsReported()
    {
        using var bundle = new TempBundle();
        bundle.Write("extra/notes.txt", "not hashed");

        var problems = LoadFails(bundle.Directory).Problems;

        Assert.Contains("extra/notes.txt: file is not listed in manifest.json.", problems);
    }

    [Fact]
    public void DotFilesAreIgnored()
    {
        using var bundle = new TempBundle(full: true);
        bundle.Write(".DS_Store", "junk");

        var expected = ContentManifest.Compute(bundle.Directory).BundleHash;

        Assert.Equal(expected, ContentLoader.Load(bundle.Directory).ContentHash);
    }

    [Fact]
    public void ABundleHashThatDoesNotMatchTheFilesIsReported()
    {
        using var bundle = new TempBundle(full: true);
        var manifest = bundle.Read("manifest.json");
        bundle.Write("manifest.json", manifest.Replace(ContentManifest.FromJson(manifest).BundleHash, new string('0', 64), StringComparison.Ordinal));

        var problems = LoadFails(bundle.Directory).Problems;

        var problem = Assert.Single(problems);
        Assert.StartsWith("manifest.json: bundleHash mismatch", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ASizeThatDoesNotMatchIsReported()
    {
        using var bundle = new TempBundle();
        var entry = ContentManifest.Compute(bundle.Directory).Files.Single(file => file.Path == "cefr/a1.txt");
        var manifest = bundle.Read("manifest.json");
        bundle.Write("manifest.json", manifest.Replace(
            "\"sha256\":\"" + entry.Sha256 + "\",\"bytes\":" + entry.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "\"sha256\":\"" + entry.Sha256 + "\",\"bytes\":" + (entry.Bytes + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal));

        var problem = Assert.Single(LoadFails(bundle.Directory).Problems);

        Assert.StartsWith("cefr/a1.txt: size mismatch", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnsortedManifestIsReported()
    {
        using var bundle = new TempBundle();
        var manifest = ContentManifest.Compute(bundle.Directory);
        var shuffled = manifest with { Files = [.. manifest.Files.Reverse()] };
        bundle.Write("manifest.json", shuffled.ToJson());

        var problems = LoadFails(bundle.Directory).Problems;

        Assert.Contains(problems, p => p.StartsWith("manifest.json: files are not sorted by path", StringComparison.Ordinal));
        Assert.DoesNotContain(problems, p => p.Contains("bundleHash", StringComparison.Ordinal));
    }

    [Fact]
    public void AMissingManifestIsReported()
    {
        using var bundle = new TempBundle();
        bundle.Delete("manifest.json");

        var problem = Assert.Single(LoadFails(bundle.Directory).Problems);

        Assert.Equal("manifest.json: file is missing.", problem);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1,\"bundleId\":\"game-v1\"}")]
    public void AMalformedManifestIsReported(string manifest)
    {
        using var bundle = new TempBundle();
        bundle.Write("manifest.json", manifest);

        var problem = Assert.Single(LoadFails(bundle.Directory).Problems);

        Assert.StartsWith("manifest.json: ", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnsupportedSchemaVersionIsRejected()
    {
        using var bundle = new TempBundle();
        bundle.Edit("manifest.json", "{\"schemaVersion\":1,", "{\"schemaVersion\":2,");

        var problem = Assert.Single(LoadFails(bundle.Directory).Problems);

        Assert.Equal("manifest.json: unsupported schemaVersion 2; expected 1.", problem);
    }

    [Fact]
    public void AnUnsupportedLexiconSchemaVersionIsRejected()
    {
        using var bundle = new TempBundle();
        bundle.Edit("lexicon/game.json", "\"schemaVersion\": 1", "\"schemaVersion\": 7");
        bundle.Rehash();

        var problems = LoadFails(bundle.Directory).Problems;

        Assert.Contains(problems, p => p.StartsWith("lexicon/game.json: Unsupported lexicon schemaVersion 7", StringComparison.Ordinal));
    }

    [Fact]
    public void ADirectoryThatDoesNotExistIsReported()
    {
        var exception = LoadFails(Path.Combine(Path.GetTempPath(), "vocaterra-no-such-bundle-" + Guid.NewGuid().ToString("N")));

        Assert.Single(exception.Problems);
    }

    [Fact]
    public void ARequiredFileThatIsAbsentIsReportedEvenWithoutHashChecks()
    {
        using var bundle = new TempBundle();
        bundle.Delete("intents/intents.json");

        var problems = LoadFails(bundle.Directory, new ContentLoadOptions { VerifyHashes = false }).Problems;

        Assert.Contains("intents/intents.json: required file is missing.", problems);
    }

    [Fact]
    public void InvalidUtf8IsReportedWithoutCrashing()
    {
        using var bundle = new TempBundle();
        File.WriteAllBytes(bundle.PathOf("dialogue/dialogue.json"), [0x7B, 0xFF, 0xFE, 0x7D]);

        var problems = LoadFails(bundle.Directory, new ContentLoadOptions { VerifyHashes = false }).Problems;

        Assert.Contains("dialogue/dialogue.json: the file is not valid UTF-8 text.", problems);
    }

    [Fact]
    public void ABomAtTheStartOfAFileIsTolerated()
    {
        using var bundle = new TempBundle();
        var text = bundle.Read("effects/effects.json");
        File.WriteAllBytes(bundle.PathOf("effects/effects.json"), [.. new byte[] { 0xEF, 0xBB, 0xBF }, .. Encoding.UTF8.GetBytes(text)]);

        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.Equal(TestSupport.Bundle.Effects, loaded.Effects);
    }

    public static TheoryData<string, string> Damages()
    {
        return new TheoryData<string, string>
        {
            { "prompt-unknown-slot-grammar", "unknown slot grammar 'no such grammar'" },
            { "intent-renamed", "allows unknown intent 'greet'" },
            { "intent-renamed", "reacts to unknown intent 'greet'" },
            { "answer-set-renamed", "unknown answer set 'yes-no'" },
            { "answer-set-renamed", "answerSetId 'hellos' must equal the file name 'yes-no'" },
            { "agent-renamed", "references unknown agent 'guide'" },
            { "agent-renamed", "agentId 'agent' must equal the file name 'guide'" },
            { "dialogue-pool-renamed", "says unknown dialogue intent 'villager_thank'" },
            { "dialogue-no-not-understood", "needs a 'not_understood' pool" },
            { "template-unknown-modifier", "line 'guide.point.found.1' of 'guide.point.found' does not render" },
            { "template-unclosed-brace", "does not render" },
            { "template-unknown-slot", "line 'villager_thank.1' of 'villager_thank' does not render" },
            { "variant-pool-too-big", "variant pool 'farewell_reply' has 9 variants but dialogue intent 'farewell_reply' only has 3" },
            { "transition-to-undefined-state", "undefined state 'nowhere'" },
            { "effect-unknown-intent", "effect 'fx_effect' references unknown intent 'gone'" },
            { "invalid-slot-grammar", "intents/intents.json: Invalid slot grammar" },
            { "negative-epsilon", "evaluation/evaluation-config.json: " },
            { "zero-min-repeat", "feedback/feedback-policy.json: " },
            { "duplicate-answer-id", "answer-sets/yes-no.json: Duplicate answerId 'yes'" },
            { "empty-prompt-text", "prompt 'guide-offer' has a text that is empty or has leading or trailing whitespace" },
            { "blank-prompt-text", "prompt 'guide-offer' has a text that is empty or has leading or trailing whitespace" },
            { "padded-prompt-text", "prompt 'guide-offer' has a text that is empty or has leading or trailing whitespace" },
        };
    }

    // Each damage edits the parsed JSON of the bundle, so it keeps working when the lessons change the text of a file; it breaks only when the structure it names goes.
    private static void Damage(TempBundle bundle, string name)
    {
        switch (name)
        {
            case "prompt-unknown-slot-grammar":
                bundle.EditJson("prompts/prompts.json", root => root[0]!["slotGrammarIds"] = new JsonArray("no such grammar"));
                break;
            case "intent-renamed":
                bundle.EditJson("intents/intents.json", root => Intent(root, "greet")["intentId"] = "hello");
                break;
            case "answer-set-renamed":
                bundle.EditJson("answer-sets/yes-no.json", root => root["answerSetId"] = "hellos");
                break;
            case "agent-renamed":
                bundle.EditJson("agents/guide.json", root => root["agentId"] = "agent");
                break;
            case "dialogue-pool-renamed":
                bundle.EditJson("dialogue/dialogue.json", root => RenamePool(root, "villager_thank", "villager_gone"));
                break;
            case "dialogue-no-not-understood":
                bundle.EditJson("dialogue/dialogue.json", root => RenamePool(root, "not_understood", "not_heard"));
                break;
            case "template-unknown-modifier":
                bundle.EditJson("dialogue/dialogue.json", root => SetLine(root, "guide.point.found", 0, "{place:bogus} está aquí."));
                break;
            case "template-unclosed-brace":
                bundle.EditJson("dialogue/dialogue.json", root => SetLine(root, "guide.point.found", 1, "Mira: {place:article(def} está aquí."));
                break;
            case "template-unknown-slot":
                bundle.EditJson("dialogue/dialogue.json", root => SetLine(root, "villager_thank", 0, "Hola {nobody:bogus}."));
                break;
            case "variant-pool-too-big":
                bundle.EditJson(
                    "agents/guide.json",
                    root => root["variantPools"]!.AsArray().Single(pool => pool!["id"]!.GetValue<string>() == "farewell_reply")!["count"] = 9);
                break;
            case "transition-to-undefined-state":
                bundle.EditJson("agents/villager.json", root => root["states"]![0]!["transitions"]!.AsArray().First(t => t!["to"] is not null)!["to"] = "nowhere");
                break;
            case "effect-unknown-intent":
                bundle.Write(
                    "effects/effects.json",
                    "{\"effects\":[{\"effectId\":\"fx_effect\",\"intent\":\"gone\",\"kind\":\"k\",\"curve\":[{\"correctness\":0,\"value\":0},{\"correctness\":1,\"value\":1}],\"requiresCommunicated\":true}]}");
                break;
            case "invalid-slot-grammar":
                bundle.EditJson("intents/intents.json", root => Intent(root, "greet")["slotGrammars"]![0] = "hola [NOUN");
                break;
            case "negative-epsilon":
                bundle.EditJson("evaluation/evaluation-config.json", root => root["beginner"]!["epsilon"] = -1);
                break;
            case "zero-min-repeat":
                bundle.EditJson("feedback/feedback-policy.json", root => root["rules"]![2]!["minRepeat"] = 0);
                break;
            case "duplicate-answer-id":
                bundle.EditJson("answer-sets/yes-no.json", root => root["answers"]![1]!["answerId"] = "yes");
                break;
            case "empty-prompt-text":
                bundle.EditJson("prompts/prompts.json", root => PromptNamed(root, "guide-offer")["text"] = string.Empty);
                break;
            case "blank-prompt-text":
                bundle.EditJson("prompts/prompts.json", root => PromptNamed(root, "guide-offer")["text"] = "   ");
                break;
            case "padded-prompt-text":
                bundle.EditJson("prompts/prompts.json", root => PromptNamed(root, "guide-offer")["text"] = " " + PromptNamed(root, "guide-offer")["text"]!.GetValue<string>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }
    }

    private static JsonNode Intent(JsonNode root, string id)
    {
        return root["intents"]!.AsArray().Single(node => node!["intentId"]!.GetValue<string>() == id)!;
    }

    private static JsonNode PromptNamed(JsonNode root, string id)
    {
        return root.AsArray().Single(node => node!["promptId"]!.GetValue<string>() == id)!;
    }

    private static void RenamePool(JsonNode root, string from, string to)
    {
        var pools = root["pools"]!.AsObject();
        var pool = pools[from]!;
        pools.Remove(from);
        pool["poolId"] = to;
        pools[to] = pool;
    }

    private static void SetLine(JsonNode root, string poolId, int index, string text)
    {
        root["pools"]![poolId]!["variants"]![index]!["text"] = text;
    }

    [Theory]
    [MemberData(nameof(Damages))]
    public void CrossReferenceAndFormatProblemsAreReported(string damage, string expected)
    {
        using var bundle = new TempBundle();
        Damage(bundle, damage);
        bundle.Rehash();

        var problems = LoadFails(bundle.Directory).Problems;

        Assert.Contains(problems, problem => problem.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void APromptTextIsOptionalAndIsLoaded()
    {
        using var bundle = new TempBundle();
        bundle.EditJson("prompts/prompts.json", root => PromptNamed(root, "guide-offer")["text"] = null);
        bundle.Rehash();

        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.Null(loaded.Prompts["guide-offer"].Text);
        Assert.False(string.IsNullOrWhiteSpace(TestSupport.Bundle.Prompts["guide-offer"].Text));
    }

    [Fact]
    public void AnEmptyCefrListIsReported()
    {
        using var bundle = new TempBundle();
        bundle.Write("cefr/a2.txt", "# no lemmas\n");
        bundle.Rehash();

        Assert.Contains("cefr/a2.txt: the lemma list is empty.", LoadFails(bundle.Directory).Problems);
    }

    [Fact]
    public void ADuplicatePromptIdIsReported()
    {
        using var bundle = new TempBundle();
        bundle.EditJson("prompts/prompts.json", root => root[0]!["promptId"] = root[1]!["promptId"]!.GetValue<string>());
        bundle.Rehash();

        Assert.Contains(LoadFails(bundle.Directory).Problems, p => p.Contains("Duplicate promptId 'guide-open'", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownEnumNameIsReported()
    {
        using var bundle = new TempBundle();
        bundle.EditJson("prompts/prompts.json", root => root[0]!["mode"] = "Sideways");
        bundle.Rehash();

        Assert.Contains(LoadFails(bundle.Directory).Problems, p => p.StartsWith("prompts/prompts.json: ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }", "{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"llm-draft\", \"approvedBy\": null, \"approvedOn\": null }", "is a language-model draft and must be approved")]
    [InlineData("{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }", "{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"invented\", \"approvedBy\": null, \"approvedOn\": null }", "unknown generatedBy 'invented'")]
    [InlineData("{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }", "{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": \"Ana\", \"approvedOn\": null }", "must set approvedBy and approvedOn together")]
    [InlineData("{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }", "{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": \"Ana\", \"approvedOn\": \"20/10/2026\" }", "expected YYYY-MM-DD")]
    [InlineData("{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }", "{ \"path\": \"cefr/none.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }", "'cefr/none.txt' does not exist in the bundle")]
    [InlineData("{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null },\n", "", "no provenance item for 'cefr/a1.txt'")]
    public void ProvenanceIsChecked(string oldText, string newText, string expected)
    {
        using var bundle = new TempBundle();
        bundle.Edit("provenance.json", oldText, newText);
        bundle.Rehash();

        Assert.Contains(LoadFails(bundle.Directory).Problems, problem => problem.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void ALanguageModelDraftThatAPersonApprovedIsAccepted()
    {
        using var bundle = new TempBundle();
        bundle.Edit(
            "provenance.json",
            "{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }",
            "{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"llm-draft\", \"approvedBy\": \"Ana\", \"approvedOn\": \"2026-10-20\" }");
        bundle.Rehash();

        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.Contains(loaded.Provenance.Items, item => item.Path == "cefr/a1.txt" && item.ApprovedBy == "Ana" && item.ApprovedOn == "2026-10-20");
    }

    [Fact]
    public void AMissingAttributionNoticeIsReported()
    {
        using var bundle = new TempBundle();
        bundle.Delete("ATTRIBUTION.md");
        bundle.Edit("provenance.json", "{ \"path\": \"ATTRIBUTION.md\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null },\n", string.Empty);
        bundle.Rehash();

        Assert.Contains("ATTRIBUTION.md: required file is missing.", LoadFails(bundle.Directory).Problems);
    }

    [Fact]
    public void ImportedWiktionaryDataNeedsTheCcBySaNotice()
    {
        using var bundle = new TempBundle();
        bundle.Edit(
            "provenance.json",
            "{ \"path\": \"lexicon/game.json\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }",
            "{ \"path\": \"lexicon/game.json\", \"generatedBy\": \"wiktionary-import\", \"approvedBy\": null, \"approvedOn\": null }");
        bundle.Write("ATTRIBUTION.md", "# Attribution\n\nSome words came from somewhere.\n");
        bundle.Rehash();

        Assert.Contains(LoadFails(bundle.Directory).Problems, p => p.Contains("must state its CC BY-SA license", StringComparison.Ordinal));
    }

    [Fact]
    public void AnExtraLexiconFileIsMergedIntoTheBundleLexicon()
    {
        using var bundle = new TempBundle();
        bundle.Write(
            "lexicon/extra.json",
            "{\"schemaVersion\":1,\"generated_by\":\"hand-authored\",\"entries\":[{\"form\":\"yelmo\",\"lemma\":\"yelmo\",\"pos\":\"noun\",\"features\":{\"number\":\"sg\",\"gender\":\"m\"}}]}");
        bundle.Edit(
            "provenance.json",
            "{ \"path\": \"lexicon/game.json\", ",
            "{ \"path\": \"lexicon/extra.json\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null },\n    { \"path\": \"lexicon/game.json\", ");
        bundle.Rehash();

        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.True(loaded.Lexicon.ContainsLemma("yelmo"));
        Assert.True(loaded.Lexicon.ContainsLemma("espada"));
        Assert.True(loaded.Lexicon.ContainsLemma("lanzar"));
    }
}
