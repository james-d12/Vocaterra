using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O7: the intent resolver never crashes, respects the prompt's allowed intents whatever their order or content, follows the ladder (a
/// grammar the analyzer matched decides first; the rung it reports is one it really took), reports an unresolved result as margin 0 and
/// confidence 0, and gives the same resolution on a fresh instance. The body is <see cref="LanguageOracles.Resolver"/>, run on the Spanish
/// language (M35); this class checks that every rung of the ladder is reached.
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
    }
}
