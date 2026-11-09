using Vocaterra.Nlp.Content;

namespace Vocaterra.Nlp.De;

/// <summary>
/// Host-facing helpers for loading and using German bundles (M37), as <c>SpanishContent</c> is for Spanish: register the German pack with
/// <c>options with { LanguagePacks = LanguagePacks.Default.WithGerman() }</c> or call <see cref="Load"/>. Until M38 to M40 give the pack its morphology, analysis
/// rules and template functions, loading a full German bundle throws <see cref="NotSupportedException"/>.
/// </summary>
public static class GermanContent
{
    /// <summary>The default load options with the German pack registered.</summary>
    public static ContentLoadOptions Options { get; } = new() { LanguagePacks = LanguagePacks.Default.WithGerman() };

    /// <summary>Returns a new registry with <see cref="GermanLanguagePack.Instance"/> added (an existing <c>de</c> pack is replaced).</summary>
    /// <param name="packs">The registry to extend.</param>
    public static LanguagePacks WithGerman(this LanguagePacks packs)
    {
        ArgumentNullException.ThrowIfNull(packs);
        return packs.With(GermanLanguagePack.Instance);
    }

    /// <summary>Loads a bundle with the German pack registered (see <see cref="ContentLoader.Load(string, ContentLoadOptions?)"/>).</summary>
    /// <param name="bundleDir">The bundle folder.</param>
    /// <param name="options">The options; null means <see cref="Options"/>. When given, the German pack is added to its registry.</param>
    public static ContentBundle Load(string bundleDir, ContentLoadOptions? options = null)
    {
        var effective = options is null ? Options : options with { LanguagePacks = options.LanguagePacks.WithGerman() };
        return ContentLoader.Load(bundleDir, effective);
    }
}
