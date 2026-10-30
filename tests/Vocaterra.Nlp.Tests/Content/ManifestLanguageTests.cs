using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.Packs.Tests;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>The manifest's <c>language</c> and <c>supportLanguage</c>, the bundle hash rule and the loader's language resolution (milestone M32, ADR 0011).</summary>
public class ManifestLanguageTests
{
    private static ContentManifest WriteManifest(TempBundle bundle, string? language, string? supportLanguage = null)
    {
        var manifest = ContentManifest.Compute(bundle.Directory, language: language, supportLanguage: supportLanguage);
        File.WriteAllText(bundle.PathOf(ContentPaths.Manifest), manifest.ToJson(), new UTF8Encoding(false));
        return manifest;
    }

    [Fact]
    public void WithoutALanguageTheHashIsExactlyTheOldCombineOfPathsAndHashes()
    {
        // game-v1 names no target language; it names its support language (en, the game's C03), which the hash leaves out.
        var manifest = TestSupport.Bundle.Manifest;
        var parts = manifest.Files.SelectMany(file => new[] { file.Path, file.Sha256 }).ToArray();

        Assert.Null(manifest.Language);
        Assert.Equal(DeterministicHash.Combine(parts), manifest.BundleHash);
        Assert.Equal(manifest.BundleHash, ContentManifest.ComputeBundleHash(manifest.Files, null));
    }

    [Fact]
    public void WithALanguageTheHashStartsWithLanguageAndTheTag()
    {
        var manifest = TestSupport.Bundle.Manifest;
        var parts = new[] { "language", "de-DE" }.Concat(manifest.Files.SelectMany(file => new[] { file.Path, file.Sha256 })).ToArray();

        Assert.Equal(DeterministicHash.Combine(parts), ContentManifest.ComputeBundleHash(manifest.Files, "de-DE"));
    }

    [Fact]
    public void ABundleWithLanguageEsHashesDifferentlyFromTheSameBundleWithoutIt()
    {
        using var bundle = new TempBundle();
        var without = ContentManifest.Compute(bundle.Directory);

        var with = WriteManifest(bundle, "es");

        Assert.Equal(without.Files, with.Files);
        Assert.NotEqual(without.BundleHash, with.BundleHash);
        Assert.NotEqual(with.BundleHash, ContentManifest.Compute(bundle.Directory, language: "es-MX").BundleHash);
        Assert.Equal(with.BundleHash, ContentManifest.Compute(bundle.Directory).BundleHash);
    }

    [Fact]
    public void TheSupportLanguageIsRecordedAndDoesNotChangeTheHash()
    {
        using var bundle = new TempBundle();
        // A support language the copied manifest names is kept by Compute, so the plain one is made without it.
        var plain = ContentManifest.Compute(bundle.Directory, language: "es") with { SupportLanguage = null };

        var withSupport = ContentManifest.Compute(bundle.Directory, language: "es", supportLanguage: "en");

        Assert.Equal("en", withSupport.SupportLanguage);
        Assert.Equal(plain.BundleHash, withSupport.BundleHash);
        Assert.NotEqual(plain, withSupport);
        Assert.NotEqual(plain.GetHashCode(), withSupport.GetHashCode());
    }

    [Fact]
    public void TheLanguageFieldsRoundTripAndAreWrittenOnlyWhenPresent()
    {
        var absent = TestSupport.Bundle.Manifest with { SupportLanguage = null };
        var present = absent with { Language = "de-DE", SupportLanguage = "en", BundleHash = ContentManifest.ComputeBundleHash(absent.Files, "de-DE") };

        var absentJson = absent.ToJson();
        var presentJson = present.ToJson();

        using (var document = JsonDocument.Parse(absentJson))
        {
            Assert.Equal(["schemaVersion", "bundleId", "contentVersion", "files", "bundleHash"], document.RootElement.EnumerateObject().Select(p => p.Name));
        }

        using (var document = JsonDocument.Parse(presentJson))
        {
            Assert.Equal(["schemaVersion", "bundleId", "contentVersion", "language", "supportLanguage", "files", "bundleHash"], document.RootElement.EnumerateObject().Select(p => p.Name));
        }

        Assert.Equal(present, ContentManifest.FromJson(presentJson));
        Assert.Equal(presentJson, ContentManifest.FromJson(presentJson).ToJson());
        Assert.Equal(absent, ContentManifest.FromJson(absentJson));
        Assert.NotEqual(absent, present);

        // The canonical serializer (the replay and fuzz round trips use it) also leaves a null field out.
        Assert.DoesNotContain("language", CanonicalJson.Serialize(absent), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(present, CanonicalJson.Deserialize<ContentManifest>(CanonicalJson.Serialize(present)));
    }

    [Fact]
    public void ComputeKeepsTheLanguageOfTheExistingManifestAndTheArgumentsOverrideIt()
    {
        using var bundle = new TempBundle();
        WriteManifest(bundle, "es-MX", "en");

        var kept = ContentManifest.Compute(bundle.Directory);
        var overridden = ContentManifest.Compute(bundle.Directory, language: "es");

        Assert.Equal("es-MX", kept.Language);
        Assert.Equal("en", kept.SupportLanguage);
        Assert.Equal("es", overridden.Language);
        Assert.Equal("en", overridden.SupportLanguage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("es")]
    [InlineData("es-MX")]
    [InlineData("ES")]
    public void TheLoaderResolvesSpanish(string? language)
    {
        using var bundle = new TempBundle();
        WriteManifest(bundle, language);

        var loaded = bundle.Load(verifyHashes: true);

        Assert.Same(SpanishLanguagePack.Instance, loaded.Language);
        Assert.Equal("es", loaded.LanguageId);
        Assert.Equal(language, loaded.Manifest.Language);
    }

    [Fact]
    public void TheCommittedBundleIsSpanishWithoutALanguageField()
    {
        Assert.Same(SpanishLanguagePack.Instance, TestSupport.Bundle.Language);
        Assert.Equal("es", TestSupport.Bundle.LanguageId);
        Assert.Null(TestSupport.Bundle.Manifest.Language);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("de")]
    [InlineData("xx-YY")]
    public void ALanguageWithoutARegisteredPackIsRefused(string language)
    {
        using var bundle = new TempBundle();
        WriteManifest(bundle, language);

        foreach (var verify in new[] { true, false })
        {
            var ex = Assert.Throws<ContentValidationException>(() => bundle.Load(verify));
            var problem = Assert.Single(ex.Problems);
            Assert.StartsWith("manifest.json: no language pack is registered for language '" + language + "'", problem, StringComparison.Ordinal);
            Assert.Contains("registered: es", problem, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("e")]
    [InlineData("es_MX")]
    [InlineData("es-")]
    [InlineData("español")]
    [InlineData("1234")]
    [InlineData("es MX")]
    public void AMalformedLanguageIsRefused(string language)
    {
        using var bundle = new TempBundle();
        WriteManifest(bundle, language);

        var ex = Assert.Throws<ContentValidationException>(() => bundle.Load(verifyHashes: true));

        var problem = Assert.Single(ex.Problems);
        Assert.StartsWith("manifest.json: language '" + language + "' is not a well-formed BCP-47 language tag", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ACustomRegistryThroughTheOptionsIsUsed()
    {
        using var bundle = new TempBundle();
        WriteManifest(bundle, "xx-YY");
        var stub = new LanguagePacksTests.StubPack("xx");
        var options = new ContentLoadOptions { LanguagePacks = LanguagePacks.Default.WithSpanish().With(stub) };

        var loaded = ContentLoader.Load(bundle.Directory, options);

        Assert.Same(stub, loaded.Language);
        Assert.Equal("xx", loaded.LanguageId);
        Assert.Equal("xx-YY", loaded.Manifest.Language);
        Assert.Throws<ContentValidationException>(() => ContentLoader.Load(bundle.Directory));
    }

    [Fact]
    public void ARegistryWithoutSpanishRefusesABundleWithoutALanguage()
    {
        using var bundle = new TempBundle();
        var options = new ContentLoadOptions { LanguagePacks = LanguagePacks.Of(new LanguagePacksTests.StubPack("xx")) };

        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.Load(bundle.Directory, options));

        Assert.Contains("no language pack is registered for language 'es' (registered: xx)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultOptionsUseTheDefaultRegistryAndABundleWithoutAHostRegistryIsRefusedForWantingSpanish()
    {
        using var bundle = new TempBundle();
        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.Load(bundle.Directory));
        Assert.Contains("no language pack is registered for language 'es' (registered: )", ex.Message, StringComparison.Ordinal);
        Assert.Same(LanguagePacks.Default, ContentLoadOptions.Default.LanguagePacks);
        Assert.Same(LanguagePacks.Default, new ContentLoadOptions().LanguagePacks);
    }

    [Fact]
    public void AStaleHashAfterAddingALanguageIsReported()
    {
        // Adding the field without rehashing leaves the old hash, which no longer matches the language-prefixed one.
        using var bundle = new TempBundle();
        var old = ContentManifest.Compute(bundle.Directory);
        File.WriteAllText(bundle.PathOf(ContentPaths.Manifest), (old with { Language = "es" }).ToJson(), new UTF8Encoding(false));

        var ex = Assert.Throws<ContentValidationException>(() => bundle.Load(verifyHashes: true));

        Assert.Contains(ex.Problems, problem => problem.StartsWith("manifest.json: bundleHash mismatch", StringComparison.Ordinal));
    }
}
