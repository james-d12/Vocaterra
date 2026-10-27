using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Es.Morphology;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Es;

/// <summary>
/// Host-facing helpers for loading and using Spanish bundles (M53). The engine's default registry is empty, so a host registers the Spanish pack: either
/// <c>options with { LanguagePacks = LanguagePacks.Default.WithSpanish() }</c> or <see cref="Load"/>.
/// </summary>
public static class SpanishContent
{
    /// <summary>The default load options with the Spanish pack registered.</summary>
    public static ContentLoadOptions Options { get; } = new() { LanguagePacks = LanguagePacks.Default.WithSpanish() };

    /// <summary>Returns a new registry with <see cref="SpanishLanguagePack.Instance"/> added (an existing <c>es</c> pack is replaced).</summary>
    /// <param name="packs">The registry to extend.</param>
    public static LanguagePacks WithSpanish(this LanguagePacks packs)
    {
        ArgumentNullException.ThrowIfNull(packs);
        return packs.With(SpanishLanguagePack.Instance);
    }

    /// <summary>Loads a bundle with the Spanish pack registered (see <see cref="ContentLoader.Load(string, ContentLoadOptions?)"/>).</summary>
    /// <param name="bundleDir">The bundle folder.</param>
    /// <param name="options">The options; null means <see cref="Options"/>. When given, the Spanish pack is added to its registry.</param>
    public static ContentBundle Load(string bundleDir, ContentLoadOptions? options = null)
    {
        var effective = options is null ? Options : options with { LanguagePacks = options.LanguagePacks.WithSpanish() };
        return ContentLoader.Load(bundleDir, effective);
    }

    /// <summary>
    /// The Spanish morphology generator of a bundle: <see cref="ContentBundle.LanguageMorphology"/> as a <see cref="MorphologyGenerator"/>, for hosts of a
    /// Spanish bundle (the game's noun catalog and guide use it). It replaces the engine's <c>ContentBundle.Morphology</c> property, removed in M53.
    /// </summary>
    /// <param name="bundle">The bundle.</param>
    /// <exception cref="NotSupportedException">The bundle's language pack does not create a <see cref="MorphologyGenerator"/> (any pack but Spanish); use <see cref="ContentBundle.LanguageMorphology"/>.</exception>
    public static MorphologyGenerator SpanishMorphology(this ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.LanguageMorphology as MorphologyGenerator
            ?? throw new NotSupportedException($"SpanishMorphology is the Spanish MorphologyGenerator; the language '{bundle.LanguageId}' has none. Use LanguageMorphology.");
    }

    /// <summary>
    /// Creates a template engine over the embedded Spanish seed lexicon (<see cref="SpanishLexicon.Seed"/>) in Spanish: the former
    /// <c>TemplateEngine.CreateDefault()</c>. A bundle's engine is <c>bundle.TemplateEngine</c>.
    /// </summary>
    public static TemplateEngine CreateTemplateEngine()
    {
        var pack = SpanishLanguagePack.Instance;
        return new TemplateEngine(SpanishLexicon.Seed, pack.CreateMorphology(SpanishLexicon.Seed), pack);
    }
}
