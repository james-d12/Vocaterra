using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Es;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// The target languages the game supports (T01, D-game-27): the one place in the game that names a language pack. A bundle's manifest picks its pack
/// (a bundle with no <c>language</c> is Spanish, ADR 0011); every other game class works through the loaded bundle's neutral members.
/// </summary>
public static class GameLanguages
{
    /// <summary>The language packs the game supports (Spanish now).</summary>
    public static LanguagePacks Packs { get; } = LanguagePacks.Default.WithSpanish();

    /// <summary>The load options with every supported pack registered.</summary>
    public static ContentLoadOptions LoadOptions { get; } = new() { LanguagePacks = Packs };

    /// <summary>Loads the bundle in <paramref name="bundleDirectory"/> with the pack its manifest names.</summary>
    public static ContentBundle Load(string bundleDirectory, ContentLoadOptions? options = null) =>
        ContentLoader.Load(bundleDirectory, options is null ? LoadOptions : options with { LanguagePacks = Packs });
}
