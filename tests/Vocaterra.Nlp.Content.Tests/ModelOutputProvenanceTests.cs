namespace Vocaterra.Nlp.Content.Tests;

/// <summary>M29g: the <c>model-output</c> provenance kind for host-owned files rendered by a model (pre-rendered speech).</summary>
public class ModelOutputProvenanceTests
{
    private const string SpeechPath = "speech/test_clip.1.wav";
    private const string Model = "kokoro-multi-lang-v1_0";

    private const string LexiconLine = "{ \"path\": \"lexicon/game.json\", ";

    // A bundle with one host file (the loader does not parse it) and a provenance item for it, written as the caller gives it.
    private static TempBundle BundleWithSpeechItem(string item)
    {
        var bundle = new TempBundle();
        bundle.Write(SpeechPath, "not really a wav\n");
        bundle.Edit("provenance.json", LexiconLine, item + ",\n    " + LexiconLine);
        bundle.Rehash();
        return bundle;
    }

    private static ContentValidationException LoadFails(string dir)
    {
        return Assert.Throws<ContentValidationException>(() => ContentLoader.Load(dir));
    }

    [Fact]
    public void TheKindIsListedAndTheExistingKindsKeepTheirValues()
    {
        Assert.Equal("model-output", GeneratedByKinds.ModelOutput);
        Assert.Equal("hand-authored", GeneratedByKinds.HandAuthored);
        Assert.Equal("llm-draft", GeneratedByKinds.LlmDraft);
        Assert.Equal("wiktionary-import", GeneratedByKinds.WiktionaryImport);
        string[] kinds = ["hand-authored", "llm-draft", "wiktionary-import", "model-output"];
        Assert.Equal(kinds, GeneratedByKinds.All);
    }

    [Fact]
    public void AHostFileWithAModelAndAVoiceIsAcceptedWithoutApproval()
    {
        var item = "{ \"path\": \"speech/test_clip.1.wav\", \"generatedBy\": \"model-output\", \"approvedBy\": null, \"approvedOn\": null, "
            + "\"model\": \"kokoro-multi-lang-v1_0\", \"voice\": \"af_bella\", \"speaker\": \"3\", \"source\": \"test_clip.1\" }";
        using var bundle = BundleWithSpeechItem(item);

        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.Contains(SpeechPath, loaded.HostFilePaths);
        Assert.True(loaded.TryReadHostFile(SpeechPath, out _));
        Assert.Contains(
            loaded.Provenance.Items,
            found => found == new ProvenanceItem(SpeechPath, GeneratedByKinds.ModelOutput, null, null, Model, "af_bella", "3", "test_clip.1"));
    }

    [Fact]
    public void AModelOutputWithOnlyTheModelIsAccepted()
    {
        using var bundle = BundleWithSpeechItem("{ \"path\": \"speech/test_clip.1.wav\", \"generatedBy\": \"model-output\", \"approvedBy\": null, \"approvedOn\": null, \"model\": \"kokoro-multi-lang-v1_0\" }");

        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.Contains(loaded.Provenance.Items, item => item.Path == SpeechPath && item.Model == Model && item.Voice is null);
    }

    [Theory]
    [InlineData("{ \"path\": \"speech/test_clip.1.wav\", \"generatedBy\": \"model-output\", \"approvedBy\": null, \"approvedOn\": null }")]
    [InlineData("{ \"path\": \"speech/test_clip.1.wav\", \"generatedBy\": \"model-output\", \"approvedBy\": null, \"approvedOn\": null, \"model\": \"\" }")]
    [InlineData("{ \"path\": \"speech/test_clip.1.wav\", \"generatedBy\": \"model-output\", \"approvedBy\": null, \"approvedOn\": null, \"model\": \"  \" }")]
    public void AModelOutputWithoutAModelIsRefusedWithAClearMessage(string item)
    {
        using var bundle = BundleWithSpeechItem(item);

        Assert.Contains(
            LoadFails(bundle.Directory).Problems,
            problem => problem == "provenance.json: 'speech/test_clip.1.wav' is 'model-output' and needs a model: the id of the model that rendered the file, for example 'kokoro-multi-lang-v1_0'.");
    }

    [Fact]
    public void AModelWithLeadingOrTrailingWhitespaceIsRefused()
    {
        using var bundle = BundleWithSpeechItem("{ \"path\": \"speech/test_clip.1.wav\", \"generatedBy\": \"model-output\", \"approvedBy\": null, \"approvedOn\": null, \"model\": \" kokoro \" }");

        Assert.Contains(LoadFails(bundle.Directory).Problems, problem => problem.Contains("has a model with leading or trailing whitespace", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"voice\": \"\"", "voice")]
    [InlineData("\"voice\": \" af_bella\"", "voice")]
    [InlineData("\"speaker\": \"\"", "speaker")]
    [InlineData("\"source\": \"  \"", "source")]
    public void AnEmptyOptionalFieldIsRefused(string field, string name)
    {
        var item = "{ \"path\": \"speech/test_clip.1.wav\", \"generatedBy\": \"model-output\", \"approvedBy\": null, \"approvedOn\": null, "
            + "\"model\": \"kokoro-multi-lang-v1_0\", " + field + " }";
        using var bundle = BundleWithSpeechItem(item);

        Assert.Contains(
            LoadFails(bundle.Directory).Problems,
            problem => problem.StartsWith("provenance.json: 'speech/test_clip.1.wav' has a " + name + " that is empty", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("cefr/a1.txt")]
    [InlineData("answer-sets/yes-no.json")]
    [InlineData("lexicon/seed.json")]
    [InlineData("intents/intents.json")]
    [InlineData("dialogue/dialogue.json")]
    [InlineData("prompts/prompts.json")]
    [InlineData("agents/guide.json")]
    [InlineData("ATTRIBUTION.md")]
    public void ModelOutputIsRefusedForLanguageData(string path)
    {
        using var bundle = new TempBundle();
        bundle.Edit(
            "provenance.json",
            $"{{ \"path\": \"{path}\", \"generatedBy\": \"hand-authored\"",
            $"{{ \"path\": \"{path}\", \"generatedBy\": \"model-output\", \"model\": \"{Model}\"");
        bundle.Rehash();

        Assert.Contains(
            LoadFails(bundle.Directory).Problems,
            problem => problem.StartsWith($"provenance.json: '{path}' is 'model-output', which only host-owned files may be", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null, \"model\": \"x\"")]
    [InlineData("\"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null, \"voice\": \"af_bella\"")]
    [InlineData("\"generatedBy\": \"llm-draft\", \"approvedBy\": \"Ana\", \"approvedOn\": \"2026-10-20\", \"source\": \"hola\"")]
    public void TheModelFieldsAreRefusedOnAnyOtherKind(string fields)
    {
        using var bundle = new TempBundle();
        bundle.Edit(
            "provenance.json",
            "{ \"path\": \"cefr/a1.txt\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }",
            "{ \"path\": \"cefr/a1.txt\", " + fields + " }");
        bundle.Rehash();

        Assert.Contains(
            LoadFails(bundle.Directory).Problems,
            problem => problem.StartsWith("provenance.json: 'cefr/a1.txt' sets model, voice, speaker or source, which only a 'model-output' item may set.", StringComparison.Ordinal));
    }

    [Fact]
    public void AModelOutputItemSerializesWithItsFieldsAndAnOrdinaryItemDoesNotChange()
    {
        var provenance = new ContentProvenance(
        [
            new ProvenanceItem("cefr/a1.txt", GeneratedByKinds.HandAuthored, null, null),
            new ProvenanceItem(SpeechPath, GeneratedByKinds.ModelOutput, null, null, Model, "af_bella"),
        ]);

        Assert.Equal(
            "{\"items\":["
            + "{\"path\":\"cefr/a1.txt\",\"generatedBy\":\"hand-authored\",\"approvedBy\":null,\"approvedOn\":null},"
            + "{\"path\":\"speech/test_clip.1.wav\",\"generatedBy\":\"model-output\",\"approvedBy\":null,\"approvedOn\":null,\"model\":\"kokoro-multi-lang-v1_0\",\"voice\":\"af_bella\"}"
            + "]}",
            provenance.ToJson());
        Assert.Equal(provenance, ContentProvenance.FromJson(provenance.ToJson()));
    }
}
