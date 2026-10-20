using Slg.Audio;
using Slg.Content;
using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Verdicts;
using Slg.Fuzzing.Tests.Generation;
using Slg.Fuzzing.Tests.Oracles;
using Slg.Phonology;
using Slg.Pronunciation;
using Slg.Response.Templates;
using Xunit.Sdk;
using SlotGrammarType = Slg.Nlp.SlotGrammar.SlotGrammar;

namespace Slg.Fuzzing.Tests;

/// <summary>Plain tests of the fuzz suite's own machinery, so a broken helper cannot turn an oracle into a silent pass.</summary>
public sealed class OracleInfrastructureTests
{
    [Fact]
    public void FirstDifference_NamesTheJsonPath()
    {
        Assert.Equal("$.errors[1].span.start", Determinism.FirstDifference(
            """{"errors":[{"span":{"start":1}},{"span":{"start":2}}]}""",
            """{"errors":[{"span":{"start":1}},{"span":{"start":3}}]}"""));
        Assert.Equal("$.errors.length", Determinism.FirstDifference("""{"errors":[1]}""", """{"errors":[1,2]}"""));
        Assert.Equal("(identical)", Determinism.FirstDifference("""{"a":1}""", """{"a":1}"""));
    }

    [Fact]
    public void AssertSame_FailsWithTheInputAndThePath_WhenTwoRunsDiffer()
    {
        var calls = 0;
        var failure = Assert.Throws<XunitException>(() =>
            Determinism.AssertSame("OX", "counter", "the input", () => new Counter(calls++), () => new Counter(calls++)));

        Assert.Contains("[OX] counter is not deterministic", failure.Message, StringComparison.Ordinal);
        Assert.Contains("$.value", failure.Message, StringComparison.Ordinal);
        Assert.Contains("\"the input\"", failure.Message, StringComparison.Ordinal);
        Determinism.AssertSame("OX", "constant", "x", () => new Counter(7), () => new Counter(7));
    }

    [Fact]
    public void Describe_RendersStringsAsJsonLiterals_AndFallsBackForValuesCanonicalJsonRefuses()
    {
        Assert.Equal("\"a\\nb ñ\"", Determinism.Describe("a\nb ñ"));
        Assert.Contains("\"value\":3", Determinism.Describe(new Counter(3)), StringComparison.Ordinal);
        Assert.Contains("NaN", Determinism.Describe(new Counter(double.NaN)), StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyAMissingSlotIsTolerated()
    {
        var slots = SlotMap.From([new KeyValuePair<string, string>("item", "espada")]);

        Assert.True(KnownTolerances.IsMissingSlot(new TemplateException("qty", "Unknown slot 'qty'."), slots));
        Assert.False(KnownTolerances.IsMissingSlot(new TemplateException("item", "Unknown slot 'item'."), slots));
        Assert.False(KnownTolerances.IsMissingSlot(new TemplateException("{item:bogus}", "Unknown filter."), slots));
    }

    [Fact]
    public void EveryCatalogPatternAndSkeletonRendersBackToItself()
    {
        var patterns = PipelineHarness.Shared.Grammars.Grammars.Select(static g => g.Id).Concat(PatternGen.Skeletons);
        foreach (var text in patterns)
        {
            var tree = GrammarPattern.Parse(text);
            var compiled = SlotGrammarType.Parse(text);
            Assert.Equal(compiled.Pattern, tree.Render());
            Assert.Equal(tree.Render(), GrammarPattern.Parse(tree.Render()).Render());
        }
    }

    [Fact]
    public void SlotAmbiguityIsDetectedForCompetingOptionalTokens()
    {
        static IEnumerable<string> None(string word) => [];

        Assert.True(GrammarPattern.Parse("[NOUN:a]? [NOUN:b]?").HasAmbiguousSlots(None));
        Assert.True(GrammarPattern.Parse("[ADJ]? [ADJ:x]?").HasAmbiguousSlots(None));
        Assert.True(GrammarPattern.Parse("[DET]? [ADJ:x]").HasAmbiguousSlots(None));
        Assert.True(GrammarPattern.Parse("isla? [NOUN:x]").HasAmbiguousSlots(static _ => ["NOUN"]));
        Assert.False(GrammarPattern.Parse("[VERB:tener:verb] [NUM:qty] [NOUN:item]").HasAmbiguousSlots(None));
        Assert.False(GrammarPattern.Parse("[VERB] [ART] [NOUN:first] [PREP] [ART] [NOUN:second]").HasAmbiguousSlots(None));
    }

    [Fact]
    public void NoNoise_LeavesTheTextAlone_AndSafeNoiseNeverChangesTheNormalizedText()
    {
        Assert.Equal("yo tengo dos espadas", TextNoise.None.Apply("yo tengo dos espadas"));
        var noisy = new TextNoise(CaseMode: 4, AccentMode: 0, Nfd: true, Punctuation: 3, Spaces: 3, NumberMode: 0, AdversarialIndex: -1, Seed: 5).Apply("yo tengo dos espadas");

        Assert.NotEqual("yo tengo dos espadas", noisy);
        Assert.Equal("yo tengo dos espadas", PipelineHarness.Normalize(noisy).Text.Replace("2", "dos", StringComparison.Ordinal));
    }

    [Fact]
    public void OnlyTheDocumentedWavAndContentRejectionsAreTolerated()
    {
        Assert.True(KnownTolerances.IsWavRejection(new InvalidDataException("bad")));
        Assert.True(KnownTolerances.IsWavRejection(new NotSupportedException("bad")));
        Assert.False(KnownTolerances.IsWavRejection(new ArgumentException("bad")));
        Assert.False(KnownTolerances.IsWavRejection(new IndexOutOfRangeException()));

        var exception = new ContentValidationException(["prompts/prompts.json: prompt 'x' is wrong.", "manifest.json: bundleHash mismatch"]);
        Assert.True(KnownTolerances.NamesPath(exception, "prompts/prompts.json"));
        Assert.True(KnownTolerances.NamesPath(exception, "manifest.json"));
        Assert.False(KnownTolerances.NamesPath(exception, "intents/intents.json"));
    }

    [Fact]
    public void TheContentFieldToleranceListCoversOptionalFieldsOnly()
    {
        Assert.Equal(KnownTolerances.ContentFieldReason.Optional, KnownTolerances.AcceptedContentField("answer-sets/greetings.json", ["answers", "2", "aliases"]));
        Assert.Equal(KnownTolerances.ContentFieldReason.Optional, KnownTolerances.AcceptedContentField("lexicon/seed.json", ["entries", "40", "features", "gender"]));
        Assert.Equal(KnownTolerances.ContentFieldReason.Optional, KnownTolerances.AcceptedContentField("dialogue/dialogue.json", ["pools", "feedback.hint"]));
        Assert.Equal(KnownTolerances.ContentFieldReason.SilentDefault, KnownTolerances.AcceptedContentField("evaluation/evaluation-config.json", ["beginner", "epsilon"]));
        Assert.Equal(KnownTolerances.ContentFieldReason.SilentDefault, KnownTolerances.AcceptedContentField("prompts/prompts.json", ["3", "mode"]));

        // Fields the loader must read are not on the list.
        Assert.Null(KnownTolerances.AcceptedContentField("prompts/prompts.json", ["3", "promptId"]));
        Assert.Null(KnownTolerances.AcceptedContentField("prompts/prompts.json", ["3", "answerSetId"]));
        Assert.Null(KnownTolerances.AcceptedContentField("intents/intents.json", ["intents", "0", "intentId"]));
        Assert.Null(KnownTolerances.AcceptedContentField("dialogue/dialogue.json", ["pools", "not_understood"]));
        Assert.Null(KnownTolerances.AcceptedContentField("lexicon/seed.json", ["entries", "40", "lemma"]));
        Assert.Null(KnownTolerances.AcceptedContentField("manifest.json", ["bundleHash"]));
    }

    [Fact]
    public void TheWavBuilderWritesEveryShapeTheReaderMustDecode()
    {
        short[] samples = [0, 1, -1, 12345, -12345, short.MaxValue, short.MinValue, 7];
        foreach (var layout in Enum.GetValues<WavLayout>())
        {
            foreach (var encoding in Enum.GetValues<WavEncoding>())
            {
                foreach (var channels in new[] { 1, 2 })
                {
                    var spec = new AudioSpec(22050, channels, encoding, layout, 1, SignalKind.Silence, 440.0, 0.5, 0, false);
                    var bytes = WavBuilder.Build(spec, samples);

                    var read = WavFile.Read(new MemoryStream(bytes.Data));

                    Assert.Equal(22050, read.SampleRate);
                    Assert.Equal(channels, read.Channels);
                    Assert.Equal(samples, read.InterleavedSamples);
                    Assert.True(bytes.DataHeader > bytes.FmtHeader);
                }
            }
        }
    }

    [Fact]
    public void AudioCasesAreReproducibleFromTheirParametersAndNoiseIsTheGivenNoise()
    {
        var spec = new AudioSpec(8000, 2, WavEncoding.Pcm16, WavLayout.Canonical, 5, SignalKind.WhiteNoise, 440.0, 0.5, 1, false);
        short[] noise = [.. Enumerable.Range(0, spec.Frames).Select(static i => (short)((i * 977) - 15000))];

        var first = AudioGen.Synthesize(spec, noise);
        var second = AudioGen.Synthesize(spec, noise);

        Assert.Equal(first.Samples, second.Samples);
        Assert.Equal(spec.Frames * 2, first.Samples.Length);
        Assert.Equal((short)Math.Round(noise[3] * 0.5), first.Samples[6]);
        Assert.Contains("sha256=", first.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheAdversarialCorporaAreWellFormed()
    {
        foreach (var shape in AdversarialCorpus.AudioShapes)
        {
            Assert.Contains(shape.SampleRate, AudioGen.SampleRates);
            Assert.InRange(shape.Milliseconds, 0, 5000);
            Assert.InRange(shape.Channels, 1, 2);
        }

        foreach (var sequence in AdversarialCorpus.PhoneSequences)
        {
            Assert.InRange(sequence.Count, 1, PhoneGen.MaxSequenceLength);
            Assert.All(sequence, static phone => Assert.True(PhoneInventory.IsKnown(phone), phone));
        }

        foreach (var json in AdversarialCorpus.JsonKinds)
        {
            _ = System.Text.Json.Nodes.JsonNode.Parse(json);
        }
    }

    [Fact]
    public void ThePhoneCasesDescribeWhatTheRecognizerHears()
    {
        var fake = FakePhonemeRecognizer.ForSpanish();
        var perfect = new PhoneSequenceCase([PhoneInventory.A, PhoneInventory.Tap], new FrameShape(3, 1, 0), 0, 0, null, ScoringMode.Gop, Difficulty.Beginner);
        var confused = perfect with { Confusion = 1 };
        var inserted = perfect with { Confusion = 6, InsertAt = 1 };
        var collapsing = new PhoneSequenceCase([PhoneInventory.A, PhoneInventory.A], new FrameShape(3, 0, 0), 0, 0, null, ScoringMode.Greedy, Difficulty.Beginner);

        Assert.True(perfect.IsPerfect);
        Assert.Equal(["a", "ɹ"], confused.Spoken);
        Assert.Equal(["a", "h", "ɾ"], inserted.Spoken);
        Assert.False(collapsing.IsPerfect);
        Assert.Equal(7, perfect.Posteriors(fake).FrameCount);
    }

    [Fact]
    public void ContentMutationsAreFoundInTheFilesThemselves()
    {
        var catalog = ContentMutationGen.Shared;

        Assert.Contains("lexicon/seed.json", catalog.Files);
        Assert.Contains("manifest.json", catalog.Files);
        var pristine = catalog.Materialize(null);
        try
        {
            Assert.Equal(catalog.Files, Directory.EnumerateFiles(pristine, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(pristine, path).Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray());
            Assert.Equal(ContentLoader.Load(pristine).ContentHash, PipelineHarness.Shared.Bundle.ContentHash);

            var rng = new SeededRng(7);
            var kinds = Enumerable.Range(0, 400).Select(_ => catalog.Build(rng).Kind).Distinct().Order().ToArray();
            Assert.Equal(Enum.GetValues<ContentMutationKind>(), kinds);
        }
        finally
        {
            Directory.Delete(pristine, recursive: true);
        }
    }

    public sealed record Counter(double Value);
}
