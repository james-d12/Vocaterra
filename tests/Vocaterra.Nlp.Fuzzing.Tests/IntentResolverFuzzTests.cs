using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O7: the intent resolver never crashes, respects the prompt's allowed intents whatever their order or content, follows the ladder (a
/// grammar the analyzer matched decides first; the rung it reports is one it really took), reports an unresolved result as margin 0 and
/// confidence 0, and gives the same resolution on a fresh instance. Since M72 it also checks the off-prompt intent: null whenever the intent is known,
/// otherwise an intent the prompt does not allow that the resolver gives with every intent allowed. The body is <see cref="LanguageOracles.Resolver"/>, run on
/// the Spanish language (M35); this class checks that every rung of the ladder is reached, and that lines with and without an off-prompt intent are drawn.
/// </summary>
public sealed class IntentResolverFuzzTests(ITestOutputHelper output)
{
    [Fact]
    public void Resolve_NeverCrashes_AndRespectsTheLadderAndTheAllowedIntents()
    {
        var diagnostics = LanguageOracles.Resolver(FuzzLanguage.Spanish, output);
        Assert.True(
            diagnostics.Get("rung:Grammar") > 0 && diagnostics.Get("rung:Rules") > 0 && diagnostics.Get("rung:Embedding") > 0 && diagnostics.Get("rung:Unresolved") > 0,
            "Every rung of the ladder must be reached.\n" + diagnostics.Report());
        Assert.True(
            diagnostics.Get("off-prompt:set") > 0 && diagnostics.Get("off-prompt:none") > 0,
            "Unknown lines with and without an off-prompt intent must be reached (M72).\n" + diagnostics.Report());
    }
}
