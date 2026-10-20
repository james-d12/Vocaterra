using CsCheck;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O15: content schema violations. A structurally damaged copy of <c>content/v1</c> (a property removed, a value of another JSON kind, a reference to
/// an id that does not exist, an unknown enumeration value, an emptied required string, a duplicated id, a flipped byte) makes
/// <c>ContentLoader.Load</c> throw a <see cref="ContentValidationException"/> and nothing else, and the exception names the file that was damaged.
/// Damage to a value the loader must read (a dangling reference, an unknown enumeration value, an empty required string, a duplicated id, a flipped
/// byte) is never accepted; dropping or retyping a field the loader does not require may be, and is counted. A flipped byte changes the bundle hash.
/// The unmutated bundle always loads.
/// </summary>
public sealed class ContentSchemaFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O15";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    // Every iteration writes, hashes and loads a whole copy of the bundle in a directory of its own, and the imported lexicon makes that a few
    // hundred milliseconds; the iterations share nothing, so a few of them run at the same time.
    private static readonly int Threads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    // Almost every damaged copy has the lexicon files of the committed bundle byte for byte (the damage is elsewhere), so the merged lexicon is parsed once.
    // A copy whose lexicon file is damaged has other bytes and is parsed and checked in full; nothing is served from the cache for it.
    private static readonly ContentLoadOptions LoadOptions = new() { LexiconCache = new LexiconCache() };

    [Fact]
    public void MutatedBundles_AreRejectedWithContentValidationExceptions_NamingTheMutatedFile()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.ContentVar, FuzzOptions.ContentDefault);
        var catalog = ContentMutationGen.Shared;

        ContentMutationGen.Mutation.Sample(
            mutation =>
            {
                var directory = catalog.Materialize(mutation);
                try
                {
                    CheckMutation(mutation, directory, diagnostics);
                }
                finally
                {
                    DeleteQuietly(directory);
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: Threads);

        output.WriteLine("Content mutation diagnostics:\n" + diagnostics.Report());
        var minimum = Math.Max(1, Math.Min(15, iterations / 30));
        foreach (var kind in Enum.GetValues<ContentMutationKind>())
        {
            Assert.True(diagnostics.Sum($"{kind}:") >= minimum, $"The generator no longer reaches {kind}.\n{diagnostics.Report()}");
        }

        foreach (var kind in new[] { ContentMutationKind.DanglingReference, ContentMutationKind.UnknownEnum, ContentMutationKind.EmptyRequiredString, ContentMutationKind.DuplicateId, ContentMutationKind.FlipByte })
        {
            Assert.Equal(0, diagnostics.Get($"{kind}:accepted"));
        }
    }

    // The empty-string mutation drawn at random above reaches these places only by chance; each required non-empty field is also checked on its own.
    public static TheoryData<ContentMutation> EmptyRequiredFields()
    {
        return new TheoryData<ContentMutation>
        {
            new(ContentMutationKind.EmptyRequiredString, ContentPaths.Effects, ["effects", "0", "kind"], "\"\""),
            new(ContentMutationKind.EmptyRequiredString, ContentPaths.Effects, ["effects", "0", "effectId"], "\"\""),
            new(ContentMutationKind.EmptyRequiredString, ContentPaths.Effects, ["effects", "0", "intent"], "\"\""),
            new(ContentMutationKind.EmptyRequiredString, ContentPaths.Prompts, ["0", "promptId"], "\"\""),
        };
    }

    [Theory]
    [MemberData(nameof(EmptyRequiredFields))]
    public void AnEmptyRequiredString_IsRefused_NamingTheDamagedFile(ContentMutation mutation)
    {
        var directory = ContentMutationGen.Shared.Materialize(mutation);
        try
        {
            var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.Load(directory, LoadOptions));
            Assert.Contains(ex.Problems, problem => problem.StartsWith(mutation.File + ":", StringComparison.Ordinal));
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    [Fact]
    public void TheUnmutatedBundle_AlwaysLoads_WithTheCommittedHash()
    {
        var catalog = ContentMutationGen.Shared;
        var directory = catalog.Materialize(null);
        try
        {
            var bundle = ContentLoader.Load(directory, LoadOptions);
            Assert.Equal(Harness.Bundle.ContentHash, bundle.ContentHash);
            Assert.Equal(Harness.Bundle.Manifest.BundleHash, ContentManifest.Compute(directory).BundleHash);
            Assert.Equal(catalog.Files.Count(static f => f != "manifest.json"), bundle.Manifest.Files.Count);
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    private static void CheckMutation(ContentMutation mutation, string directory, FuzzDiagnostics diagnostics)
    {
        if (mutation.Kind == ContentMutationKind.FlipByte && ContentManifest.Compute(directory).BundleHash == Harness.Bundle.ContentHash)
        {
            throw Determinism.Fail(Oracle, $"Flipping a byte of {mutation.File} did not change the bundle hash.", mutation);
        }

        try
        {
            _ = ContentLoader.Load(directory, LoadOptions);
        }
        catch (ContentValidationException ex)
        {
            // Every problem starts with the bundle-relative path of a file. Removing or retyping a definition (an intent's grammars, an answer set's id)
            // is reported where the definition is used, so for those kinds a problem naming another bundle file is the actionable message; for every
            // other kind the damaged file itself must be named.
            var cascades = mutation.Kind is ContentMutationKind.RemoveProperty or ContentMutationKind.ChangeKind or ContentMutationKind.DuplicateId;
            var files = ContentMutationGen.Shared.Files;
            if (ex.Problems.Count == 0 || ex.Problems.Any(problem => !files.Any(file => problem.StartsWith(file, StringComparison.Ordinal))))
            {
                throw Determinism.Fail(Oracle, $"A problem does not start with the path of a bundle file:\n{ex.Message}", mutation);
            }

            if (!cascades && !KnownTolerances.NamesPath(ex, mutation.File))
            {
                throw Determinism.Fail(Oracle, $"The loader rejected the bundle but no problem names {mutation.File}:\n{ex.Message}", mutation);
            }

            diagnostics.Count(cascades && !KnownTolerances.NamesPath(ex, mutation.File) ? $"{mutation.Kind}:reported-in-referring-file" : $"{mutation.Kind}:named-the-mutated-file");

            diagnostics.Count($"{mutation.Kind}:rejected");
            return;
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"The loader threw {ex.GetType().Name} instead of ContentValidationException: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}", mutation);
        }

        if (mutation.MustBeRejected)
        {
            throw Determinism.Fail(Oracle, $"The loader accepted a bundle with a {mutation.Kind} mutation in {mutation.File}.", mutation);
        }

        // Dropping or retyping a field the loader does not require may leave a loadable bundle, but only on the documented list of fields.
        var reason = KnownTolerances.AcceptedContentField(mutation.File, mutation.Path);
        if (reason is null)
        {
            throw Determinism.Fail(Oracle, $"The loader accepted a {mutation.Kind} mutation of {mutation.File} /{string.Join('/', mutation.Path)}, which is not a documented optional field (KnownTolerances).", mutation);
        }

        diagnostics.Count($"{mutation.Kind}:accepted");
        diagnostics.Count($"accepted-as:{reason}");
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory is not a test failure.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
