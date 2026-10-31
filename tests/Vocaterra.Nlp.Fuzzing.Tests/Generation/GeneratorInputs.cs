using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>
/// What the pattern and sentence generators read: the lexicon through their eyes and the grammars they realise. <c>PipelineHarness</c> gives them the bundle's
/// (<c>content/game-v1</c>), which the oracles test; <see cref="FrozenGeneratorInputs"/> gives them a frozen copy, which the stream digests are recorded on.
/// </summary>
public interface IGeneratorInputs
{
    /// <summary>The lexicon seen through the generators' eyes.</summary>
    LexiconCatalog Words { get; }

    /// <summary>The grammars the catalog patterns are drawn from.</summary>
    PatternCatalog Grammars { get; }
}

/// <summary>
/// The fixed inputs of <c>FuzzLanguageStreamTests</c> (M57): the lexicon and the slot grammars of <c>content/game-v1</c> as they were at <c>0041eff</c>, copied into
/// <c>Fixtures/StreamDigest</c> and never edited, plus the suite's own <c>PipelineHarness.FixtureGrammars</c>. The bundle's hand-authored lexicon files are copied
/// (<c>seed</c>, <c>game</c>, <c>world</c>, <c>supplement</c>); the imported Wiktionary lexicon is not (4.7 MB of CC BY-SA data that only an import changes), so the fixture
/// is about 1 300 entries. A content edit therefore cannot move the stream digests; a change of the generators, of their draw order or of the realisation still does.
/// </summary>
public sealed class FrozenGeneratorInputs : IGeneratorInputs
{
    private FrozenGeneratorInputs(string directory)
    {
        Directory = directory;
        Lexicon = SpanishLexicon.Load(File.ReadAllText(Path.Combine(directory, "lexicon.json")));
        Words = new LexiconCatalog(Lexicon, new MorphologyGenerator(Lexicon));
        var patterns = File.ReadAllLines(Path.Combine(directory, "grammars.txt"))
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();
        GrammarCount = patterns.Length;
        Grammars = PatternCatalog.FromPatterns(patterns, PipelineHarness.FixtureGrammars);
    }

    /// <summary>The frozen inputs, loaded once from the test output folder.</summary>
    public static FrozenGeneratorInputs Instance { get; } = new(Path.Combine(AppContext.BaseDirectory, "Fixtures", "StreamDigest"));

    /// <summary>The folder the fixture was read from.</summary>
    public string Directory { get; }

    /// <summary>The frozen lexicon.</summary>
    public NlpLexicon Lexicon { get; }

    /// <summary>The number of frozen bundle grammars (lines of <c>grammars.txt</c>), before the suite's fixture grammars are appended.</summary>
    public int GrammarCount { get; }

    /// <inheritdoc />
    public LexiconCatalog Words { get; }

    /// <inheritdoc />
    public PatternCatalog Grammars { get; }
}
