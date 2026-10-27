# ADR 0011: Target language as a bundle property

- Status: accepted
- Date: 2026-10-27
- Deciders: repository owner (decision D14, the language-agnostic goal); design in `docs/nlp/research/2026-10-22-multilingual.md` sections 4.1 to 4.6 and 4.9,
  as amended by `docs/nlp/research/2026-10-26-engine-and-language-packs.md`; milestone M32 of `IMPLEMENTATION_PLAN.md`
- Relates to: [ADR 0009](0009-core-and-game-boundary.md) (the boundary), [ADR 0012](0012-one-engine-assembly-and-language-packs.md) (one engine, packs beside it).
  Number 0010 is reserved for D9's determinism record.

## Context

Everything the core reads or produces as the learner's language is Spanish today, in static classes (`TextNormalizer`, `SpanishNumbers`,
`SpanishPhoneticKey`, `SpanishG2p`, the punctuation fixer, the repair lines, the grammar defaults, the linter's pronoun list). The owner wants a second
language to be possible beside Spanish. Two things have to be settled before any code can be made neutral: which language a bundle is in, and how the
engine finds the code for it. Spanish must stay byte-identical while this is done (the Phase A safety rule, research 4.9).

## Decision

1. **The language is a property of the bundle.** `manifest.json` gains an optional `"language"`, a BCP-47 tag (`es`, `es-MX`, `de-DE`), and an optional
   `"supportLanguage"`. One bundle has one target language. An absent `language` means `es`, so `content/game-v1` needs no edit and keeps its hash.
   `ContentManifest.Language` and `ContentManifest.SupportLanguage` are `string?` with `JsonIgnore(WhenWritingNull)`, appended as optional trailing
   record members, so positional construction and deconstruction of the five old members compile and behave as before.
2. **The bundle hash includes the language only when the field is present.** With a language it is
   `Combine("language", tag, path1, sha1, ...)`, otherwise exactly `Combine(path1, sha1, ...)` as before. The tag is hashed as written, so `es` and `es-MX`
   give different hashes (they are different manifests; the pack is the same). `Combine` frames every part with its length, and in the old form the second part is a 64-digit hexadecimal
   digest while a tag has a hyphen after at most eight letters or is at most eight letters long, so a bundle with a file named `language` cannot make
   the two forms collide. `ContentManifest.ComputeBundleHash(files, language)` is the one place that implements the
   rule; the loader's verification, `ContentManifest.Compute` and therefore the ContentBuilder `hash` command all use it. `Compute` keeps the language
   fields of an existing manifest unless an argument overrides them, and `hash` takes `--language TAG` and `--support-language TAG`.
3. **`supportLanguage` is recorded and passed through, never interpreted, and is not hashed.** The core does not process the learner's first language,
   so no behaviour depends on it, and the hash identifies the behaviour of the bundle for replay. Hashing it would change the bundle hash, and with it
   every verdict stamp and replay record, for a change that alters no output. The same holds for `bundleId` and `contentVersion`, which are not hashed
   either; the manifest is not hashed as a file, only its language is folded in. The loader does not validate its syntax (it never reads it); the
   ContentBuilder does not either.
4. **A registry resolves the pack.** `LanguagePacks` (`Vocaterra.Nlp.Language.Packs`) maps a primary language subtag to an `ILanguagePack`.
   `LanguagePacks.Default` holds `es`; `LanguagePacks.Of(...)` and `With(...)` build another set; `TryResolve(tag)` reads the primary subtag of a
   well-formed tag and finds the pack, ordinal and case-insensitive on the subtag (`es-MX`, `ES` and `es` find the same pack). A host passes its own
   registry through `ContentLoadOptions.LanguagePacks`. The registry type knows no Spanish except the `Default` property, so M53 can move the Spanish
   registration out of the engine without touching it. A pack's id must be a lower case primary subtag; `With` refuses anything else.
5. **The loader validates and resolves.** A present `language` must be well-formed (RFC 5646 syntax: subtags of 1 to 8 ASCII letters or digits joined
   by single hyphens, the first of 2 to 8 letters; not checked against the IANA registry) and its primary subtag must have a pack. A malformed tag and an
   unregistered language are each a `ContentValidationException` with one problem that starts with `manifest.json:` and names the tag (and, for the
   unregistered one, the registered ids). This check runs before the hashes are verified and also when `VerifyHashes` is off, so a wrong language is
   reported as such and not as a hash mismatch. An absent language is looked up as `es`, so a registry without Spanish refuses a bundle without a language.
6. **`ContentBundle.Language` and `ContentBundle.LanguageId`.** `Language` is the resolved `ILanguagePack`; `LanguageId` is its id, the primary subtag
   (`es` for a manifest language of `es-MX`). The manifest keeps the tag as written.
7. **The pack interface, text side only.** `ILanguagePack` has `LanguageId`, `Normalizer` (`ITextNormalizer`), `Folding` (`ILexiconFolding`: lookup key,
   lenient key and the matching key), `Numbers` (`INumberWords`), `PhoneticKey` (`IPhoneticKey`), `Punctuation` (`IPunctuation`), `G2p` (`IG2p?`, null
   when pronunciation scoring is off), `GrammarDefaults` (optional subject pronoun, the pronoun list, the trailing polite words), `Lint`
   (`LintVocabulary`: subject pronouns, polite phrase, article and numeral homographs) and `RepairFallback` (`RepairLines`: ask again, slow repeat, give
   up). These are the members the call sites of M33 are routed through. **Left out, for M34:** `CreateMorphology`, `CreateAnalysisRules`,
   `CreateTemplateFunctions`, `LexiconProfile`, `ErrorTagSet` and the recast policy. They wrap types (`MorphologyGenerator`, the analyzer rules, the
   template engine) whose construction depends on the lexicon and whose neutral interfaces do not exist yet; adding them now would freeze interfaces
   that M34 has to design with the German needs in view.
8. **`SpanishLanguagePack.Instance` wraps the statics and moves nothing.** Each member forwards to the static it names; the statics keep their public
   entry points, their behaviour and their Spanish meaning, so the game compiles unchanged. A test per member checks the forward returns exactly what the
   static returns. The pack does not own the Spanish word lists: the pronoun list comes from `WordClasses` (made enumerable, in listing order), and the
   polite words and homographs are literals in the pack beside the literals in `LintCoverage`, `SlotGrammarMatcher` and `AnalysisSlotFinder`. M33 removes
   those duplicates when it routes the call sites. No call site uses the pack yet.
9. **Layering.** The packs live in `Vocaterra.Nlp.Language.Packs`, in the `Language` folder, but wrap code of `Phonology` and `Response`, which
   `Language` may not use (ADR 0012). `LayerRules` therefore gives the packs a rule of their own (they may use `Core`, `Language`, `Phonology` and
   `Response`; nothing else, so not `Content` or `Pipeline`) and the `Language` rule no longer covers `Language.Packs`. `Content` already depends on
   everything but `Pipeline`, so `ContentLoader` and `ContentBundle` may use the registry. Note for M33: `Response` is allowed to depend on `Language`,
   and the pack depends on `Response` for the punctuation fixer, so a `Response` call site that takes an `ILanguagePack` makes a namespace cycle that the
   rules permit; M33 should move the punctuation behind the interface, or keep the cycle on purpose, and say which.
10. **Replay needs no new field.** The bundle hash identifies the bundle and the bundle names its language, as research 4.6 says. The envelope's
    optional `language` is not added in this milestone.

## Consequences

- A Spanish bundle without `language` loads, hashes and replays exactly as before: `content/game-v1` keeps hash `aab525f9...`, the goldens and the
  Spanish identity baseline are untouched.
- A bundle may now declare `language`; today only Spanish tags load. Adding `"language":"es"` to a manifest changes its bundle hash (and so every
  verdict stamp), which is the intended property: a language can never be changed silently under a hash. Hosts that want the old hash leave the field out.
- All additions are public API and additive (docs/nlp/API.md section 1, "The bundle's language"): `ContentManifest.Language`, `.SupportLanguage`,
  `.DefaultLanguage`, `ComputeBundleHash(files, language)` and two optional parameters of `Compute`; `ContentLoadOptions.LanguagePacks`;
  `ContentBundle.Language` and `.LanguageId`; and the types of `Vocaterra.Nlp.Language.Packs`. The `ContentBundle` constructor is internal.
- `ContentManifest` is a positional record: `with` expressions, equality (now including both fields) and `ToJson` handle the new members; a consumer
  that deconstructs the manifest into five variables would break (none found in the core, the tools, the tests or the game).
- `LanguagePacks.TryGetPrimarySubtag` is a syntax check only. A tag such as `zz` is well-formed and fails as "no pack registered", not as malformed.

## Alternatives considered

- **Hash the language always (`Combine("language", "es", ...)`).** It would change the hash of every existing bundle and every recorded verdict and
  replay, and force a re-record of goldens and the identity baseline. Rejected by the Phase A rule.
- **Put the language in the options of the loader instead of the manifest.** A host could then load a German bundle as Spanish. The manifest, and the
  hash that covers it, make the language part of the bundle's identity.
- **Hash the normalized primary subtag instead of the tag as written.** It would make `es` and `ES` one bundle, but two manifests that differ are two
  bundles to a reader of the files, and the tag is data the bundle author controls. The tag is hashed as written.
- **Interpret `supportLanguage` now** (support templates keyed by tag). Research 4.4 treats it as optional and later; nothing needs it.
- **Include the analysis-side members in `ILanguagePack` now.** See decision 7.
