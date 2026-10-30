using Vocaterra.Nlp.Core.Configuration;

// The whole suite is slow: scripts/test.sh fast leaves it out, and the gate, CI and scripts/test.sh full run it at its full depth.
[assembly: AssemblyTrait(TestSpeed.Trait, TestSpeed.Slow)]

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// Tunable knobs of the fuzz suite. Every <c>[Fact]</c> runs its full default depth on a plain <c>dotnet test</c>: the whole suite is in
/// process against fakes (no model files), so there is no shallower tier of the suite itself; the fast tier of <c>scripts/test.sh</c> leaves the
/// suite out instead (the assembly trait above). The environment variables still exist to push a one-off soak further; see README.md.
/// </summary>
public static class FuzzOptions
{
    /// <summary>Environment variable that overrides the depth of the general oracles (O1, O2, O6, O8, O10, O11, O16).</summary>
    public const string GeneralVar = "VOCATERRA_FUZZ_ITERATIONS";

    /// <summary>Environment variable that overrides the depth of the grammar and sentence oracles (O3, O4, O5, O7, O12).</summary>
    public const string GrammarVar = "VOCATERRA_FUZZ_ITERATIONS_GRAMMAR";

    /// <summary>Environment variable that overrides the depth of the signal oracles (pronunciation scoring O9 and the audio front-end O13).</summary>
    public const string SignalVar = "VOCATERRA_FUZZ_ITERATIONS_SIGNAL";

    /// <summary>Environment variable that overrides the depth of the end-to-end pipeline oracle (O14) and of the envelope round trip of O16.</summary>
    public const string PipelineVar = "VOCATERRA_FUZZ_ITERATIONS_PIPELINE";

    /// <summary>Environment variable that overrides the depth of the content-mutation oracle (O15).</summary>
    public const string ContentVar = "VOCATERRA_FUZZ_ITERATIONS_CONTENT";

    /// <summary>Out of 4 total: 1-in-4 chance that a free-text input is drawn from the adversarial corpus.</summary>
    public const int AdversarialWeight = 1;

    /// <summary>Out of 4 total: 3-in-4 chance that a free-text input is benign (lexicon-derived).</summary>
    public const int BenignWeight = 3;

    /// <summary>Default depth of O1, O2, O6, O8, O10, O11 and O16.</summary>
    public const int GeneralDefault = 3000;

    /// <summary>Default depth of O3, O4, O5, O7 and O12.</summary>
    public const int GrammarDefault = 2000;

    /// <summary>Default depth of O9 and O13.</summary>
    public const int SignalDefault = 1000;

    /// <summary>Default depth of O14 and of the envelope round trip of O16.</summary>
    public const int PipelineDefault = 300;

    /// <summary>Default depth of O15.</summary>
    public const int ContentDefault = 500;

    /// <summary>
    /// Reads an iteration count from <paramref name="envVar"/> when it is set to a positive integer, otherwise returns
    /// <paramref name="defaultIterations"/>, which is already a full-depth soak.
    /// </summary>
    public static int Iterations(string envVar, int defaultIterations)
    {
        // The deprecated SLGFUZZ_ name of a VOCATERRA_FUZZ_ variable is read only when the new one is unset.
        var value = EnvironmentSettings.Get(envVar, "SLGFUZZ_" + envVar["VOCATERRA_FUZZ_".Length..]);
        return int.TryParse(value, out var count) && count > 0
            ? count
            : defaultIterations;
    }
}
