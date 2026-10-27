using Vocaterra.Nlp.Content;

namespace Vocaterra.Nlp.Es.Tests;

/// <summary>Locations and helpers shared by the Spanish pack's tests.</summary>
internal static class TestSupport
{
    /// <summary>The one content bundle, <c>content/game-v1</c>, found by walking up from the test assembly (not copied to the output).</summary>
    public static string BundleDir { get; } = GameBundleLocator.Directory;

    /// <summary>The bundle, loaded once with hash verification and the Spanish pack registered.</summary>
    public static ContentBundle Bundle { get; } = SpanishContent.Load(BundleDir, new ContentLoadOptions { VerifyHashes = true });
}
