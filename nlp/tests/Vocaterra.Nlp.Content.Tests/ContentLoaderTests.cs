using System.Text;

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
        Assert.Equal("v1", bundle.Manifest.BundleId);
        Assert.Equal(ContentManifest.CurrentSchemaVersion, bundle.Manifest.SchemaVersion);
        Assert.Equal(64, bundle.ContentHash.Length);
    }

    [Fact]
    public void VerifyHashesIsOnByDefault()
    {
        Assert.True(new ContentLoadOptions().VerifyHashes);
        Assert.True(ContentLoadOptions.Default.VerifyHashes);
        using var bundle = new TempBundle();
        bundle.Edit("answer-sets/greetings.json", "\"hola amigo\"", "\"hola amiga\"");

        Assert.Contains(LoadFails(bundle.Directory).Problems, problem => problem.StartsWith("answer-sets/greetings.json: hash mismatch", StringComparison.Ordinal));
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
        foreach (var path in new[] { "intents/intents.json", "dialogue/dialogue.json", "cefr/a1.txt", "ATTRIBUTION.md" })
        {
            bundle.Write(path, bundle.Read(path).Replace("\n", "\r\n", StringComparison.Ordinal));
        }

        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.Equal(TestSupport.Bundle.ContentHash, loaded.ContentHash);
    }

    [Fact]
    public void ATamperedFileIsRejectedAndNamed()
    {
        using var bundle = new TempBundle();
        bundle.Edit("prompts/prompts.json", "\"promptId\": \"greet-open\"", "\"promptId\": \"greet-open2\"");

        var problems = LoadFails(bundle.Directory).Problems;

        var problem = Assert.Single(problems);
        Assert.StartsWith("prompts/prompts.json: hash mismatch", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExceptionMessageListsEveryProblem()
    {
        using var bundle = new TempBundle();
        bundle.Edit("prompts/prompts.json", "\"greet-open\"", "\"greet-open2\"");
        bundle.Edit("effects/effects.json", "\"effectId\": \"buy_item_price\"", "\"effectId\": \"buy_item_cost\"");
        bundle.Delete("answer-sets/spells-basic.json");

        var exception = LoadFails(bundle.Directory);

        Assert.Contains(exception.Problems, p => p.StartsWith("prompts/prompts.json: hash mismatch", StringComparison.Ordinal));
        Assert.Contains(exception.Problems, p => p.StartsWith("effects/effects.json: hash mismatch", StringComparison.Ordinal));
        Assert.Contains(exception.Problems, p => p.StartsWith("answer-sets/spells-basic.json: file is missing", StringComparison.Ordinal));
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
        bundle.Edit("answer-sets/greetings.json", "\"hola amigo\"", "\"hola amiga\"");

        var loaded = ContentLoader.Load(bundle.Directory, new ContentLoadOptions { VerifyHashes = false });

        Assert.Contains("hola amiga", loaded.AnswerSets["greetings"].GrammarPhrases());
    }

    [Fact]
    public void AMissingFileIsReported()
    {
        using var bundle = new TempBundle();
        bundle.Delete("answer-sets/greetings.json");

        var problems = LoadFails(bundle.Directory).Problems;

        Assert.Contains("answer-sets/greetings.json: file is missing (listed in manifest.json).", problems);
        Assert.Contains(problems, p => p.Contains("unknown answer set 'greetings'", StringComparison.Ordinal));
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

        Assert.Equal(TestSupport.Bundle.ContentHash, ContentLoader.Load(bundle.Directory).ContentHash);
    }

    [Fact]
    public void ABundleHashThatDoesNotMatchTheFilesIsReported()
    {
        using var bundle = new TempBundle(full: true);
        var manifest = bundle.Read("manifest.json");
        bundle.Write("manifest.json", manifest.Replace(TestSupport.Bundle.ContentHash, new string('0', 64), StringComparison.Ordinal));

        var problems = LoadFails(bundle.Directory).Problems;

        var problem = Assert.Single(problems);
        Assert.StartsWith("manifest.json: bundleHash mismatch", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ASizeThatDoesNotMatchIsReported()
    {
        using var bundle = new TempBundle();
        var entry = TestSupport.Bundle.Manifest.Files.Single(file => file.Path == "cefr/a1.txt");
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
    [InlineData("{\"schemaVersion\":1,\"bundleId\":\"v1\"}")]
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

    [Theory]
    [InlineData("prompts/prompts.json", "\"slotGrammarIds\": [\"hola [NOUN]?\"]", "\"slotGrammarIds\": [\"no such grammar\"]", "unknown slot grammar 'no such grammar'")]
    [InlineData("intents/intents.json", "\"intentId\": \"greet\"", "\"intentId\": \"hello\"", "allows unknown intent 'greet'")]
    [InlineData("intents/intents.json", "\"intentId\": \"greet\"", "\"intentId\": \"hello\"", "reacts to unknown intent 'greet'")]
    [InlineData("answer-sets/greetings.json", "\"answerSetId\": \"greetings\"", "\"answerSetId\": \"hellos\"", "unknown answer set 'greetings'")]
    [InlineData("answer-sets/greetings.json", "\"answerSetId\": \"greetings\"", "\"answerSetId\": \"hellos\"", "answerSetId 'hellos' must equal the file name 'greetings'")]
    [InlineData("agents/blacksmith.json", "\"agentId\": \"blacksmith\"", "\"agentId\": \"agent\"", "references unknown agent 'blacksmith'")]
    [InlineData("agents/blacksmith.json", "\"agentId\": \"blacksmith\"", "\"agentId\": \"agent\"", "agentId 'agent' must equal the file name 'blacksmith'")]
    [InlineData("dialogue/dialogue.json", "\"trade_done\": {", "\"trade_gone\": {", "says unknown dialogue intent 'trade_done'")]
    [InlineData("dialogue/dialogue.json", "\"not_understood\": {", "\"not_heard\": {", "needs a 'not_understood' pool")]
    [InlineData("dialogue/dialogue.json", "{item:article(def):cap} cuesta", "{item:bogus} cuesta", "line 'ask_price_reply.1' of 'ask_price_reply' does not render")]
    [InlineData("dialogue/dialogue.json", "Por {item:article(def)} pido", "Por {item:article(def} pido", "does not render")]
    [InlineData("dialogue/dialogue.json", "{ \"lineId\": \"greet_again.2\", \"text\": \"Hola de nuevo. ¿Qué quieres hoy?\" }", "{ \"lineId\": \"greet_again.2\", \"text\": \"Hola {nobody:words}.\" }", "line 'greet_again.2' of 'greet_again' does not render")]
    [InlineData("agents/blacksmith.json", "{ \"id\": \"trade_done\", \"count\": 2 }", "{ \"id\": \"trade_done\", \"count\": 9 }", "variant pool 'trade_done' has 9 variants but dialogue intent 'trade_done' only has 2")]
    [InlineData("agents/blacksmith.json", "\"to\": \"done\"", "\"to\": \"nowhere\"", "undefined state 'nowhere'")]
    [InlineData("effects/effects.json", "\"intent\": \"buy_item\"", "\"intent\": \"buy_gone\"", "effect 'buy_item_price' references unknown intent 'buy_gone'")]
    [InlineData("intents/intents.json", "\"hola [NOUN]?\"", "\"hola [NOUN\"", "intents/intents.json: Invalid slot grammar")]
    [InlineData("evaluation/evaluation-config.json", "\"epsilon\": 0.02,\n    \"unknownTagWeight\": 0.3\n  },\n  \"intermediate\"", "\"epsilon\": -1,\n    \"unknownTagWeight\": 0.3\n  },\n  \"intermediate\"", "evaluation/evaluation-config.json: ")]
    [InlineData("feedback/feedback-policy.json", "\"minRepeat\": 4", "\"minRepeat\": 0", "feedback/feedback-policy.json: ")]
    [InlineData("answer-sets/items-basic.json", "\"answerId\": \"arrow\"", "\"answerId\": \"bow\"", "answer-sets/items-basic.json: Duplicate answerId 'bow'")]
    public void CrossReferenceAndFormatProblemsAreReported(string file, string oldText, string newText, string expected)
    {
        using var bundle = new TempBundle();
        bundle.Edit(file, oldText, newText);
        bundle.Rehash();

        var problems = LoadFails(bundle.Directory).Problems;

        Assert.Contains(problems, problem => problem.Contains(expected, StringComparison.Ordinal));
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
        bundle.Edit("prompts/prompts.json", "\"promptId\": \"greet-open\"", "\"promptId\": \"greet-blacksmith\"");
        bundle.Rehash();

        Assert.Contains(LoadFails(bundle.Directory).Problems, p => p.Contains("Duplicate promptId 'greet-blacksmith'", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownEnumNameIsReported()
    {
        using var bundle = new TempBundle();
        bundle.Edit("prompts/prompts.json", "\"promptId\": \"ask-price-open\",\n    \"mode\": \"Open\"", "\"promptId\": \"ask-price-open\",\n    \"mode\": \"Sideways\"");
        bundle.Rehash();

        Assert.Contains(LoadFails(bundle.Directory).Problems, p => p.StartsWith("prompts/prompts.json: ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null },\n    { \"path\": \"answer-sets/greetings.json\"", "\"generatedBy\": \"llm-draft\", \"approvedBy\": null, \"approvedOn\": null },\n    { \"path\": \"answer-sets/greetings.json\"", "is a language-model draft and must be approved")]
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
