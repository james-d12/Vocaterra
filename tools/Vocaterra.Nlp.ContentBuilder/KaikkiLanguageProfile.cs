using System.Text.Json;
using Vocaterra.Nlp.Es;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>
/// The per-language part of the wiktextract import (M36): which <c>lang_code</c> to read, how one entry becomes lexicon entries, which closed vocabularies
/// the result is validated against, and how the lexicon is built. <see cref="Spanish"/> is the original importer, unchanged; <see cref="German"/> is new.
/// </summary>
public abstract class KaikkiLanguageProfile
{
    /// <summary>The Spanish profile (the importer as it was before M36).</summary>
    public static KaikkiLanguageProfile Spanish { get; } = new SpanishKaikkiProfile();

    /// <summary>The German profile.</summary>
    public static KaikkiLanguageProfile German { get; } = new GermanKaikkiProfile();

    /// <summary>The English name of the language.</summary>
    public abstract string Name { get; }

    /// <summary>The wiktextract <c>lang_code</c> that is read.</summary>
    public abstract string LanguageCode { get; }

    /// <summary>The importer version the sidecar records (bumped when the profile can write different output for the same input).</summary>
    public abstract string ImporterVersion { get; }

    /// <summary>The closed vocabularies the entries are validated against.</summary>
    public abstract LexiconProfile Profile { get; }

    /// <summary>The folding the lexicon is built with.</summary>
    protected abstract ILexiconFolding Folding { get; }

    /// <summary>The key under which a form is looked up in the frequency list (the list is folded; the identity for Spanish, whose forms are lower case).</summary>
    internal virtual string FrequencyKey(string form) => form;

    internal abstract string? ImportEntry(JsonElement root, List<LexiconEntry> entries, HandAuthoredLemmas? handAuthored);

    internal virtual HashSet<string> PluralLookalikes(IEnumerable<LexiconEntry> entries) => [];

    internal virtual NlpLexicon BuildLexicon(IEnumerable<LexiconEntry> entries) => NlpLexicon.FromEntries(entries, Folding, Profile);

    private sealed class SpanishKaikkiProfile : KaikkiLanguageProfile
    {
        public override string Name => "Spanish";

        public override string LanguageCode => "es";

        public override string ImporterVersion => KaikkiImporter.ImporterVersion;

        public override LexiconProfile Profile => SpanishLanguagePack.Instance.LexiconProfile;

        protected override ILexiconFolding Folding => SpanishLanguagePack.Instance.Folding;

        internal override string? ImportEntry(JsonElement root, List<LexiconEntry> entries, HandAuthoredLemmas? handAuthored)
            => KaikkiImporter.ImportSpanishEntry(root, entries, handAuthored);

        internal override HashSet<string> PluralLookalikes(IEnumerable<LexiconEntry> entries) => KaikkiImporter.SpanishPluralLookalikes(entries);

        internal override NlpLexicon BuildLexicon(IEnumerable<LexiconEntry> entries) => SpanishLexicon.FromEntries(entries);
    }
}
