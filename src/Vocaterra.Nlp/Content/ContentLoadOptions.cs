using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Content;

/// <summary>Options of <see cref="ContentLoader.Load(string, ContentLoadOptions?)"/>.</summary>
public sealed record ContentLoadOptions
{
    /// <summary>The default options: hashes are verified.</summary>
    public static ContentLoadOptions Default { get; } = new();

    /// <summary>When true (the default) every file hash, the file list and the bundle hash must match the manifest.</summary>
    public bool VerifyHashes { get; init; } = true;

    /// <summary>An optional cache of parsed lexicon files, for callers that load many bundles whose lexicon files are the same; null (the default) parses every file.</summary>
    public LexiconCache? LexiconCache { get; init; }

    /// <summary>The language packs a bundle's <c>language</c> is resolved against; empty by default (<see cref="Vocaterra.Nlp.Language.Packs.LanguagePacks.Default"/>), so a host registers the packs of the languages it loads (M53; the Spanish pack project offers <c>WithSpanish()</c>).</summary>
    public LanguagePacks LanguagePacks { get; init; } = LanguagePacks.Default;
}
