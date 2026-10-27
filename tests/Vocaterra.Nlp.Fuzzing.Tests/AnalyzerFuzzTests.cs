using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O2: the analyzer never crashes on any text, gives the same answer on a fresh instance, and its output is well-formed: errors ordered
/// by span start and tag rank without duplicates, tags from the taxonomy, spans inside the text, confidence in [0, 1]. The body is
/// <see cref="LanguageOracles.Analyzer"/>, run on the Spanish language (M35); this class checks that the Spanish inputs reach its error paths.
/// </summary>
public sealed class AnalyzerFuzzTests(ITestOutputHelper output)
{
    [Fact]
    public void Analyze_NeverCrashes_IsDeterministic_AndReturnsWellFormedResults()
    {
        var diagnostics = LanguageOracles.Analyzer(FuzzLanguage.Spanish, output);

        // The inputs must keep reaching the analyzer's main error paths, or this oracle is only a crash test.
        foreach (var tag in new[] { ErrorTags.VocabUnknown, ErrorTags.ConjPerson, ErrorTags.GenderAgree, ErrorTags.NounNumber })
        {
            Assert.True(diagnostics.Get($"tag:{tag}") > 0, $"No generated text produced {tag}.\n{diagnostics.Report()}");
        }
    }
}
