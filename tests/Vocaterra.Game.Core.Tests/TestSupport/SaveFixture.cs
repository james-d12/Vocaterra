using System.Text.Json.Nodes;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// The recorded saves under <c>Fixtures/</c>. A fixture's <c>bundleHash</c> is not pinned: it names the bundle the save was recorded from, and every
/// content edit moves the bundle's hash, so a pinned value would need a re-write after each edit (B29c). <see cref="Read"/> gives the text with the
/// current bundle's hash in its place, so a strict load still checks the world's digest and the save's own state. The hash the tests compare with is
/// <see cref="TestGame.Bundle"/>'s, which the loader checks against <c>content/game-v1/manifest.json</c> (GuideBundleTests, HostSmokeTests).
/// </summary>
internal static class SaveFixture
{
    /// <summary>The text of a fixture, with its bundle hash set to the current bundle's.</summary>
    /// <param name="name">The file name under <c>Fixtures/</c>, for example <c>save-v9.json</c>.</param>
    public static string Read(string name)
    {
        var node = JsonNode.Parse(File.ReadAllText(Repo.Path("tests", "Vocaterra.Game.Core.Tests", "Fixtures", name)))!;
        node["bundleHash"] = TestGame.Bundle.ContentHash;
        return node.ToJsonString();
    }
}
