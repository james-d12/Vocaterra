# The host API

This is what a host calls, in order, to evaluate a learner's utterance and act on the result: load a content bundle, build the services,
build a pipeline, build a conversation context, evaluate an utterance, plan the response, and record and replay a log. The snippets compile
against the renamed engine (`Vocaterra.Nlp`, one assembly, ADR 0012); they are the shape of the calls, not a complete program. The boundary they respect is in
[ADR 0009](adr/0009-core-and-game-boundary.md). The calls are exercised by the demo CLI (`tools/Vocaterra.Nlp.Cli`) and by the game, which
references the same projects; there is no packaged copy of the API to check.

Every step is tagged with its determinism tier. **D0** is plain code with fixed-order math: rules, matching, verdict assembly, the agent
state machine, the feedback policy and the response templates. **D1** is a frozen model behind a core interface (speech recognition,
phoneme recognition, embeddings, speech synthesis): pinned by SHA-256, recorded in every verdict's `versions`, and loaded only when the host
gives it a models directory. With the scripted recognizer and the fakes a host runs entirely at D0, with no model file at all.

The host owns what the core cannot know: the world seed, the stored time, the learner model it persists between utterances, the repeat counts
per error tag, and a digest of the rest of its state (`HostStateHash`). The core copies every one of them into the replay record.

## 1. Load a content bundle (D0)

```csharp
using Vocaterra.Nlp.Content;

ContentBundle bundle = ContentLoader.Load("content/game-v1");   // verifies every file's hash and the cross-references
Console.WriteLine(bundle.ContentHash);                      // the bundle hash, recorded in every verdict
```

`ContentLoader.Load` throws `ContentValidationException` for a bundle that fails validation. The bundle carries the prompts
(`bundle.Prompts`), the agents (`bundle.Agents`), the effect table (`bundle.Effects`) and the lexicon; nothing is read again after loading.

### The bundle's language

A bundle teaches one language. `manifest.json` may carry `"language"` (a BCP-47 tag such as `es`, `es-MX` or `de-DE`) and `"supportLanguage"`
(the learner's own language, for example `en`). Both are optional; an absent `language` means Spanish (`es`), so existing bundles need no edit.
All of this is additive ([ADR 0011](adr/0011-target-language-as-a-bundle-property.md)).

```csharp
using Vocaterra.Nlp.Language.Packs;

ContentBundle bundle = ContentLoader.Load("content/game-v1");
ILanguagePack pack = bundle.Language;          // SpanishLanguagePack.Instance for a bundle without "language"
string id = bundle.LanguageId;                 // "es" (the primary subtag; also "es" for a manifest language of "es-MX")
string? tag = bundle.Manifest.Language;        // null when the manifest has none; the tag as written otherwise
string? support = bundle.Manifest.SupportLanguage;   // recorded and passed through, never interpreted

// A host with its own packs passes its own registry.
var options = new ContentLoadOptions { LanguagePacks = LanguagePacks.Default.With(myGermanPack) };
ContentBundle german = ContentLoader.Load("content/my-german-bundle", options);
```

- **Hash rule.** With `language` present the bundle hash is `Combine("language", tag, path1, sha1, ...)`; without it, exactly `Combine(path1, sha1, ...)`
  as before. `supportLanguage` is not hashed. `ContentManifest.ComputeBundleHash(files, language)` implements it, and the ContentBuilder's
  `hash <dir> [--language TAG] [--support-language TAG]` follows it, keeping the fields of an existing manifest unless an option overrides them.
  Adding `language` to a manifest therefore changes its bundle hash.
- **Resolution.** `LanguagePacks.TryResolve(tag, out pack)` reads the primary subtag of a well-formed tag and finds the pack, ordinal and
  case-insensitive on the subtag. `LanguagePacks.Default` holds `es`; `LanguagePacks.Empty`, `Of(...)` and `With(...)` build other sets;
  `Languages` lists the ids. `LanguagePacks.TryGetPrimarySubtag` is the syntax check (RFC 5646 shape, not the IANA registry).
- **Refusal.** A malformed `language` or one with no registered pack makes `ContentLoader.Load` throw `ContentValidationException` with a problem
  `manifest.json: language '...' is not a well-formed BCP-47 language tag ...` or `manifest.json: no language pack is registered for language '...'
  (registered: ...)`, with or without hash verification.
- **`ILanguagePack`** (text side, M32): `LanguageId`, `Normalizer`, `Folding`, `Numbers`, `PhoneticKey`, `Punctuation`, `G2p` (null when pronunciation
  scoring is off for the language), `GrammarDefaults`, `Lint` and `RepairFallback`. `SpanishLanguagePack.Instance` wraps the static Spanish classes
  (`TextNormalizer`, `SpanishNumbers`, `SpanishPhoneticKey`, `SpanishG2p`, ...), which keep their entry points and their Spanish meaning; the
  pipeline and the content tools call through the pack (M33, below). The analysis-side members (`LexiconProfile`, `ErrorTags`, `CreateMorphology`,
  `CreateAnalysisRules`, `CreateTemplateFunctions`) joined in M34 (see "The analysis side of the pack").
- **Routed through the pack (M33).** The engine reads its text-side Spanish through `bundle.Language` (ADR 0011, amendment M33): the normalizer, the
  answer matcher and corrections, the intent resolver, the analyzer's grammar defaults, the template engine (numbers, agreement, punctuation), the
  recast (accent policy and number words), the speech requests (digits and phones; no phones when `G2p` is null), the pronunciation digits and G2P,
  the repair lines, and the content linter, `TextGrader`, `cefr-check` and `BenchMetrics`. Signatures that take a pack are listed in the next bullet.
  A host that calls these itself passes `bundle.Language`, for example `TextNormalizer` becomes `bundle.Language.Normalizer.Normalize(text, options)`.
- **Signatures that take the pack (M33; the core is not published, so these are breaking changes of the 0.x surface).**
  `AnswerMatcher.Match(text, set, difficulty, language, options = null)`;
  `AnswerCorrections.WithCanonicalWords(analysis, text, match, set, language, options = null)`;
  `AnswerSet.GrammarPhrases(language)` and `CanonicalPhrases(language)`;
  `IntentResolver(catalog, language, embeddings = null, options = null)`;
  `LinguisticAnalyzer(lexicon, language)` and `LinguisticAnalyzer(lexicon, morphology, language)`;
  `SlotGrammarMatcher.Match(tokens, grammar, language)`;
  `TemplateEngine(lexicon, morphology, language)`;
  `RecastBuilder.Build(text, analysis, lexicon, language)`;
  `SpeechRequests.Create(text, voiceId, language, dialect = null)`;
  `CefrChecker.Check(dialogue, lexicon, language, allowed, level, prompts = null)`.
  `bundle.Language` is the pack of a loaded bundle; `bundle.TemplateEngine` and `bundle.Language` are what a host needs.
- **Spanish compatibility entry points (M33).** These keep their signatures and their Spanish meaning, and the game calls some of them:
  `TextNormalizer`, `SpanishNumbers`, `SpanishPhoneticKey`, `SpanishG2p`, `MorphologyGenerator`, `RecastBuilder.Build(text, analysis[, lexicon])`,
  `SpeechRequests.Create(text, voiceId[, dialect])`, `TemplateEngine.CreateDefault()`, and the Spanish pack `SpanishLanguagePack.Instance`. Each one is
  named in its XML remarks. A neutral caller uses the pack members instead; the grep guard (`SpanishCallSiteTests`) checks the engine for this.
- New public members: `ContentManifest.Language`, `.SupportLanguage`, `.DefaultLanguage`, `ComputeBundleHash(files, language)`, two optional trailing
  parameters of `Compute` and of the record constructor; `ContentLoadOptions.LanguagePacks`; `ContentBundle.Language`, `.LanguageId`; the types of
  `Vocaterra.Nlp.Language.Packs`.

### The analysis side of the pack (M34)

All of this is additive unless it says breaking. The game compiles and passes unchanged.

- **`ILanguagePack`** gained `LexiconProfile`, `ErrorTags`, `CreateMorphology(Lexicon)`, `CreateAnalysisRules(Lexicon, IMorphology)` and
  `CreateTemplateFunctions(Lexicon, IMorphology)` (breaking for a host that implements `ILanguagePack`; the only pack is `SpanishLanguagePack`).
- **`ILanguagePack.Samples`** (M35, breaking for a host that implements `ILanguagePack`): a `SampleWords` record (`Noun`, `Verb`, `Spell`, `Expected`, `Actual`,
  `Recast`, `Tag`) that the content validator fills dialogue slots with when it proves a template renders. Spanish: `espada`, `tener`, `fuego`, `tengo`,
  `tiene`, `yo tengo dos espadas`, `gender.agree`, the values the validator always used. A pack of another language gives its own words.
- **`IMorphology`** (`Vocaterra.Nlp.Language.Morphology`): `Pluralize`, `Singularize`, `InflectNoun`, `Article(definiteness, gender, number[, nounLemma])`,
  `AgreeAdjective`, `Conjugate`, `IsRegularizedIrregular`, `TryGetRegularization`. `MorphologyGenerator` implements it; `RegularizedForm` is public.
- **`ContentBundle`**: new `LanguageMorphology` (`IMorphology`, what the engine uses) and `AnalysisRules` (`IAnalysisRules`). `Morphology` is unchanged for a Spanish
  bundle (`MorphologyGenerator`) and throws `NotSupportedException` on access for a pack that creates another implementation.
- **`IAnalysisRules`** and **`IAgreementChecker`** (`Vocaterra.Nlp.Language.Analysis`), `AnalysisRules.For(pack)` (the pack's rules over an empty lexicon, for callers
  without a lexicon), and public `ReadingClasses` and `WordClass`. Breaking signatures: `ReadingClasses.IsDeterminerReading`, `IsClosedClassReading` and `IsSkippable`
  take the rules (they were internal in effect: only the analysis used them); `AccentRestoration.SpellingsFor(lexicon, analysis, rules)`;
  `SlotGrammarMatcher.Match(tokens, grammar, language[, rules])` (the 3-argument form still works and uses `AnalysisRules.For`).
- **Constructors that take the interface** (source compatible, `MorphologyGenerator` is an `IMorphology`): `LinguisticAnalyzer(lexicon, IMorphology, language[, rules])`,
  `TemplateEngine(lexicon, IMorphology, language[, ITemplateFunctions])`; `IntentResolver(catalog, language, embeddings = null, options = null, rules = null)`.
- **`ITemplateFunctions`** (`Vocaterra.Nlp.Language.Packs`): `ResolveVerbLemma`, `Conjugate`, `InflectNoun`, `InferNumber`, `GenderOf`, `Article`.
- **`LexiconProfile`** (`Vocaterra.Nlp.Language.Lexicon`): the closed vocabularies and the optional feature lists; `V1` is Spanish; `Validate(entry)`.
  `Lexicon.ReadFile(json | bytes, profile)`, `Lexicon.Load(json, folding, profile)`, `Lexicon.FromEntries(entries, folding, profile)`, `Lexicon.Folding`.
  `LexiconSchema.Validate(entry)` is the V1 check, as before.
- **`LexicalFeatures`** and **`EntryFeatures`**: trailing optional `Case`, `VerbForm`, `Declension`, `Particle`, `Register` (null; omitted from JSON while null).
  The record constructors take them as optional parameters, so the existing calls compile; a deconstruction of either record now has more elements.
- **`ErrorTagSet`** (`Vocaterra.Nlp.Core.Taxonomy`): `All`, `Count`, `Rank`, `IsKnown`, `IsPronunciation`, `V1`. `ErrorTags.All` is unchanged. `VerdictInputs`
  gained the optional trailing `ErrorTags`.
- **`NormalizedText.FinalMark`** (init, null by default) and `NormalizationOptions.CaptureFinalMark` (default false).
- **`RepairLines.Offer`** (optional, `OfferLineFormat`), `OfferLineFormat.Format(choices)`, `RepairStrategy.OfferFormat` (Spanish).
- **`IntentCatalog.Load(json, folding = null)`** and **`SlotGrammar.Parse(pattern, id = null, folding = null)`**: a method group of either no longer converts to
  `Func<string, T>`; use a lambda.

### Host-owned files

A bundle may hold files that the loader does not parse, such as the host's own data (`game/nouns.json`). Such a file is hashed in
`manifest.json` like every other file, has an item in `provenance.json` like every other file, and is ignored by the loader: validation
accepts an unknown file that is listed in both, and it does not check the file's contents. The host reads it back with
`TryReadHostFile`:

```csharp
if (bundle.TryReadHostFile("game/nouns.json", out ReadOnlyMemory<byte> nouns))
{
    string json = System.Text.Encoding.UTF8.GetString(nouns.Span);   // the bytes the load hashed against the manifest
}

IReadOnlyList<string> hostFiles = bundle.HostFilePaths;               // the host-owned paths, in ordinal order
```

The bytes are the ones the load read and checked: the bundle keeps them from the load, so a read never touches the disk and cannot return
bytes that changed after the load (a changed file fails the next load, not this read). A path is accepted only when it is relative, uses `/`
separators, has no empty, `.` or `..` segment, and names a host-owned file of this bundle. `TryReadHostFile` returns false for an unlisted
path, an absolute path, a path with `..`, a backslash or a drive letter, and for a file the loader owns (`provenance.json`,
`ATTRIBUTION.md`, `lexicon/`, `answer-sets/`, `agents/`, `cefr/`, `intents/`, `prompts/`, `dialogue/`, `feedback/`, `evaluation/`,
`effects/`). A bundle loaded with `VerifyHashes = false` has no host files.

A host file that a model rendered (pre-rendered speech, for example) is listed with the provenance kind `GeneratedByKinds.ModelOutput`,
`"model-output"` (M29g). Its item names the model in `model` (required; a missing or blank model is refused) and may name `voice`, `speaker` and
`source` (the text or line id it renders); the other kinds refuse those fields, and `model-output` is refused for any file the loader parses
(language data). It needs no approval in the bundle: the review of the output is the host's process.

```json
{ "path": "speech/shop_need.1.wav", "generatedBy": "model-output", "approvedBy": null, "approvedOn": null,
  "model": "kokoro-multi-lang-v1_0", "voice": "af_bella", "speaker": "3", "source": "shop_need.1" }
```

### Lint a bundle (M27a)

The loader checks references; `ContentLinter` checks behaviour: it runs the real analyzer, intent resolver and grader on the bundle's own
examples and finds examples nothing resolves, intents that take each other's sentences, hazardous grammar constructs, content nothing reaches, words
above a CEFR level and example expectations that no longer hold. A host or a build step can call it on a loaded bundle; the command line is
`vocaterra-content lint` (checks, waivers and exit codes: [TOOLS.md](TOOLS.md#content-lint)).

```csharp
using Vocaterra.Nlp.Content;

ContentBundle bundle = ContentLoader.Load("content/game-v1", new ContentLoadOptions { VerifyHashes = false });   // lint before hashing
LintReport report = ContentLinter.Lint(bundle, new LintOptions { Level = "a1", FailOnLevel = false });
Console.Write(report.Format());                  // one finding per line, then "lint: 0 errors, 2 warnings, 8 info, 0 waived"
string json = report.ToJson();                   // the same report as canonical JSON
bool ok = report.Passes(strict: false);          // false when there is an error (or, strict, a warning); info and waived never fail
foreach (LintFinding finding in report.Active(LintSeverity.Warning)) { /* Severity, CheckId, Path, Ids, Message, Suggestion */ }
```

`LintOptions`: `Level` (`a1` or `a2`; null skips the CEFR check and an unknown level throws `ArgumentException`), `FailOnLevel`, `RootPrompts`
(measure reachability from these prompts only; an unknown id throws `ArgumentException`) and `HostPools` (pools the host voices itself).
The report is deterministic: the same bundle and options give the same findings in the same order. `TextGrader` is the piece the example
expectations use and a host can use it too: `new TextGrader(bundle).Grade("yo tiene dos espada", prompt)` returns the `Verdict` the pipeline gives for
a typed answer (no recognizer, no pronunciation score), so a host can grade text input without a pipeline.

The intent catalog gained two additive members. An example is a string or `{ "text": ..., "errors": [...], "prompt": ... }`
(`IntentDefinition.ExampleSpecs` holds `IntentExample` records; `IntentDefinition.Examples` still lists the texts, so the resolver and the
embedding anchors are unchanged), and an intent may carry `"lint": { "allowOverlap": [...], "allowRuleOnly": [...], "allowHomograph": [...] }`
(`IntentDefinition.Lint`). `IntentCatalog.ToJson()` writes a string example as a string and omits `lint` when it is empty, so a catalog that uses neither
serializes as before. `ReservedPools` (in `Vocaterra.Nlp.Response.Templates`) names the dialogue pools the pipeline uses without an agent action
(`not_understood`, `answer_accepted`, `feedback.<kind>[.<tag>]`).

### Grammar check the strings (M30)

`vocaterra-content grammar <bundleDir>` checks the Spanish strings of a bundle with a local LanguageTool server (D10; procedure in
[CONTRIBUTING.md](CONTRIBUTING.md#check-the-grammar-of-your-spanish-before-you-hash-it)). A host with strings the bundle does not hold
(phrase sheets, its own English-side Spanish) passes them as JSON Lines with `--strings <file.jsonl>`. `--strings-only` checks only the file.
Each line is one object:

| Member | Required | Meaning |
| --- | --- | --- |
| `id` | yes | A non-empty string, unique in the file and not the id of a bundle string. It names the string in findings and in the waiver file. |
| `text` | yes | The string as the learner or the host shows it. Not normalised: the tool capitalises learner strings only. |
| `kind` | yes | `npc` (authored speech, checked as written), `learner` (what a learner says: the first letter is capitalised before the check) or `template` (a slot template, `{slot}` or `{slot:filter(args)}`, rendered with the core's template engine from four sampled fillings; each distinct rendering is checked). |
| `errors` | no | An array of expected error tags (`conj.person`, `gender.agree`, `noun.number`, `vocab.unknown`, ...). Its presence makes the line a learner example for the oracle; `[]` means "no error expected". |

```json
{"id":"le06-yo-quieres-agua","text":"yo quieres agua, por favor","kind":"learner","errors":["conj.person"]}
{"id":"greet.1","text":"¡Hola! Soy {name:cap}.","kind":"template"}
{"id":"prompt.buy","text":"¿Qué quieres comprar?","kind":"npc"}
```

Blank lines are skipped. A bad line (not JSON, an unknown kind, no text, a duplicate id) stops the command with exit 1 and the line number.
The file is read as UTF-8. Findings for these strings name the file; their waivers go in the same waiver file as the bundle's, by `id`.

## 2. Build the services (D0, with optional D1)

```csharp
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

// D0: a scripted recognizer returns a fixed transcript, so no model and no audio are needed.
IWordRecognizer words = new ScriptedWordRecognizer(
    new AsrResult("yo tiene dos espada", [new AsrHypothesis("yo tiene dos espada", 1.0)], [], 1.0, "scripted-transcript", RecognitionMode.Open));

IPhonemeRecognizer? phonemes = null;                        // optional: without it pronunciation is not scored
IEmbeddingModel? embeddings = new FakeEmbeddingModel();     // D0 stand-in for the embedding rung
ISpeechSynthesizer? synthesizer = new SilentSynthesizer();  // D0 stand-in: no audio is produced

var services = new PipelineServices(words, phonemes, embeddings, synthesizer, bundle);
```

With real models (D1), the same constructor takes the adapters. `ModelStore` reads the directory named by `VOCATERRA_MODELS_DIR` and checks
each file against `models/manifest.json`; an adapter that is not pinned is refused by its factory.

```csharp
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pronunciation;
using Vocaterra.Nlp.Response.Speech;

ModelStore store = ModelStore.FromEnvironment() ?? throw new InvalidOperationException("Set VOCATERRA_MODELS_DIR to a models directory.");
IWordRecognizer words = VoskWordRecognizer.FromStore(store, "vosk-model-small-es-0.42");           // D1, constrained prompts
IPhonemeRecognizer phonemes = new Wav2Vec2PhonemeRecognizer(store.ModelDirectory("wav2vec2-xlsr-53-espeak-cv-ft"));   // D1, optional
IEmbeddingModel embeddings = new OnnxEmbeddingModel(store.ModelDirectory("multilingual-e5-small"));  // D1, open prompts
ISpeechSynthesizer synthesizer = KokoroSynthesizer.FromStore(store, "kokoro-multi-lang-v1_0", speakerId: 28); // D1, optional (28 = ef_dora)
```

The recognizers are chosen by the host: Vosk for constrained prompts, sherpa-onnx (`SherpaOnnxOfflineRecognizer.FromStore`) for open prompts;
the CLI shows the choice. Every adapter exposes its `ModelHash`, and the pipeline puts those hashes into the verdict's `versions`.

## 3. Build a pipeline (D0)

```csharp
var pipeline = new UtterancePipeline(services);             // PipelineOptions is optional
```

`UtterancePipeline` runs the stages in a fixed order; the same input gives the same bytes of output. One pipeline serves many
utterances.

## 4. Build a conversation context (D0)

```csharp
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;

var agentState = bundle.Agents["villager"].InitialState();   // the host persists the state between utterances
ConversationContext context = ConversationContext.Create(
    worldSeed: 42,                                              // with the agent id and counter, seeds every random draw
    agentId: "villager",
    agentState: agentState,
    now: new StoredTimestamp(1_760_000_000),                    // the host's clock, passed in; nothing in the core reads one
    learner: LearnerModel.New(),                                // the host persists this and passes it back next time
    repeatCountsByTag: null);                                   // the counts before this utterance: none at the start, then the previous Next's (see below)
```

`ConversationContext.Create` computes `HostStateHash` over the values it was given. A host with its own state digest uses the constructor
instead and passes that digest.

### Host slots and host-named skills (CR-003, CR-005)

Two more inputs belong to one utterance. They are optional trailing parameters of `Create` and init properties of the context, so existing
`Create` calls and positional constructions compile unchanged.

```csharp
using Vocaterra.Nlp.Core.Verdicts;

ConversationContext context = ConversationContext.Create(
    worldSeed: 42, agentId: "villager", agentState: agentState, now: new StoredTimestamp(1_760_000_000), learner: learner,
    hostSlots: SlotMap.From([new("target", "árbol"), new("direction", "a la derecha"), new("distance", "12")]),
    extraSkills: ["word.árbol"]);
```

- **`HostSlots`** (`SlotMap?`, strings only like verdict slots): facts only the host knows. Reply and feedback templates read them with the same
  formatters verdict slots get, so `Mira {target:article(def)}, {direction}, a {distance:words(m)} metros.` renders as
  `Mira el árbol, a la derecha, a doce metros.` A noun lemma works with `article(def)` and `plural-if(...)` exactly as a verdict slot does; a
  number slot works with `words`. Keys are held in ordinal order, so insertion order never matters.
  - *Precedence.* The name space is shared and **the verdict wins**: where a host slot and a verdict slot have the same name the verdict's
    value is used, and the reserved feedback names `tag`, `expected`, `actual` and `recast` win over both. Reason: the verdict is what the
    player said and the pipeline derived, a host fact must not silently rewrite it; and a bundle that already works keeps rendering the same
    when the host adds a slot. Give host slots names the intents do not use (`target`, `direction`, `distance`).
  - *Missing slot.* A template that reads a host slot the host did not supply fails exactly like one that reads a missing verdict slot: a
    feedback template falls back to the generic pool of its kind (then to no feedback), while a reply template throws `TemplateException`
    from `Evaluate`. The loader's template check does not know the host's names; it proves a template renders with sample values (a slot read
    by `words`, `plural-if` or `conj` gets a number or a verb whatever its name, `TemplateEngine.SlotKinds`).
  - *Empty map.* An empty `SlotMap` is stored as `null`.
- **`ExtraSkills`** (`IReadOnlyList<string>?`): skill ids the utterance also exercised, such as `word.árbol`. The pipeline merges them with its
  own `intent.<id>` skill before its single `LearnerModel.Update` call, so each skill and each error family is stepped once: naming
  `intent.request_item` again, or an error family such as `conj`, changes nothing; a new id is added as one correct (or, for a family the
  verdict has errors in, incorrect) observation. The list is sorted ordinally and de-duplicated. An id must be non-empty and contain no
  whitespace (`LearnerModel.ValidateSkillId`); the context throws `ArgumentException` otherwise, at `Create` or when the property is set.
  A host that has no utterance (the learner met a word in the world) calls `learner.StepSkills(["word.árbol"], correct: true)`, which steps the
  named skills only and involves no error families.
- **Carry-over.** Both are per utterance: `Next` does **not** carry them over, the next context has none, and the host sets them again with
  `Create` when the next utterance needs them. (`HostStateHash` is recomputed from the carried values, as before.)
- **Hash.** `Create` folds the two inputs into `HostStateHash` only when at least one is present; a context without them hashes exactly as
  it did before this feature, so earlier logs and goldens still match. A host that built its own digest with the constructor must include
  them in it. Both are written into the replay envelope's `context` (omitted when absent), so `Replayer` reproduces the response and the
  learner model (see [REPLAY.md](REPLAY.md)).

## 5. Evaluate an utterance and read the verdict (D0 over the recognizer's output)

```csharp
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Verdicts;

PromptSpec prompt = bundle.Prompts["villager-open"];
string? agentLine = prompt.Text;                                // the line the agent says before the player answers (null when there is none)
PcmClip clip = PcmClip.FromSamples([]);                         // a real host passes the captured clip (16 kHz mono)
PipelineResult result = pipeline.Evaluate(clip, prompt, context);

Verdict verdict = result.Verdict;
Console.WriteLine($"{verdict.Intent.Value} communicated={verdict.Communicated} correctness={verdict.Correctness}");
foreach (ErrorTag error in verdict.Errors)
{
    Console.WriteLine($"{error.Tag}: expected '{error.Expected}', heard '{error.Actual}'");
}
```

`communicated` says whether the learner was understood; `correctness` says how well they spoke, from 0 to 1. The errors carry the span in the
normalized text, the expected form and what was heard. A host gates its own reactions on `communicated` and uses `correctness` for scoring.

### The words and the answer match (CR-002, CR-004)

`result.Words` has one entry per normalized token, in order. Each entry has the token (`Text`, the normalized text; `Span`, its offsets in
that text; `Original`, the surface as typed), a `Role` and the `Reading` the resolver chose. The role is `Word` (the lexicon knows it),
`Name` (the best slot grammar binds it to a `[WORD]` element; it has no reading) or `Unknown` (no reading, no grammar). A reading is the
`LexicalFeatures` record (`Lemma`, `Pos`, `Person`, `Number`, `Tense`, `Mood`, `Gender`; null where a feature does not apply). It is the
reading the agreement checks used, so its lemma agrees with the error spans; a host need not run `TextNormalizer` or `LinguisticAnalyzer`
again. Where the resolver left several readings of one word, the first in lexicon order is reported, except in three cases (M29j; the agreement
checks read the resolver's readings and are not affected):

- **Mood.** A form that is both the imperative and the indicative (`está`, `abre`) is reported as the 2nd person imperative only when it opens its
  clause (nothing before it but interjections: `¡Está quieto!`, `por favor abre la puerta`). Otherwise it is the indicative: `¿dónde está el río?`
  gives `estar`, 3rd person singular, `pres`, `ind`. A clause-initial `ser`, `estar` or `haber` is also the indicative before a noun phrase or a
  participle, and any clause-initial verb is before a subject pronoun. The text keeps no punctuation, so a question shows by its question word.
- **`gracias`** is the interjection (lemma `gracias`, `Pos` `interj`), alone and after a quantifier (`muchas gracias`). After an article or a numeral
  (`las gracias`, `dos gracias`) it is the noun `gracia`.
- **Fixed expressions.** Adjacent words that the lexicon lists as one form (`por favor`, `de nada`) are reported with one reading on every token: the
  lemma is the whole expression (`por favor`) and `Pos` is that entry's (`interj`). Each token keeps its own `Text`, `Span` and `Original`. A host
  detects a fixed expression by a `Lemma` that contains a space; the tokens that share it are adjacent. There is no new member or value.
  The agreement checks still see `por` and `favor` as a preposition and a noun. `Text` keeps accents and writes number
words as digits (`dos` is `2`); `Original` is the typed text. A word typed without its accent is reported with the reading the resolver
chose, so `esta` is `estar` after `donde`, and its `Original` stays `esta`.

`result.Answer` is the answer match of the prompt's answer set: `AnswerId` (null when nothing matched), `Type` (`Exact`, `Alias`, `Fuzzy` or
`None`), `Distance` (the edit distance, or the phonetic distance when that is smaller, so `ezpada` for `espada` is 0), `LikelyPronunciation`
(the spelling differs but the phonetic key matches) and `Confidence`.

```csharp
foreach (ResolvedWord word in result.Words!)
{
    Console.WriteLine($"{word.Token.Original} {word.Role} lemma={word.Reading?.Lemma} pos={word.Reading?.Pos} gender={word.Reading?.Gender}");
}

if (result.Answer is { AnswerId: not null } answer)
{
    Console.WriteLine($"{answer.AnswerId} {answer.Type} distance={answer.Distance} likelyPronunciation={answer.LikelyPronunciation}");
}
```

The pipeline always sets both members; they are nullable only so that a result built by hand (in a test) needs no analysis. Both are part of
the result's `CanonicalJson`, but not of the replay record (see [REPLAY.md](REPLAY.md)).

### Tense errors, `conj.tense` (M49, M51)

`conj.tense` is reported by the analysis for the cases below, each with `expected` and `actual` on the verb token (the last one on the verb and the infinitive together):

- **Past marker with a non-past verb** (`ayer`, `anoche`, `anteayer`, a time noun with `pasado`): a present or simple future form expects the preterite
  (`ayer canto` gives expected `canté`, actual `canto`; `ayer comeré` gives `comí`).
- **Future marker with a past verb:** `mañana`, `pasado mañana`, `próximo/a` before a time noun (`la próxima semana`, `el próximo año`) or a time noun
  followed by `que viene` (`la semana que viene`), with a preterite or imperfect form, expects the `ir a` + infinitive form for that person
  (`mañana comí` gives expected `voy a comer`, actual `comí`). When the verb is `ir` itself followed by `a`, the expected form is the present
  (`mañana fui a la tienda` gives `voy`). The present stays correct (`mañana como pan`), so does the future (`mañana comeré`), and `esta mañana` and
  `por la mañana` are not markers. `luego` and `más tarde` are not markers either (`luego comí` is ordinary narration). A sentence with a past marker as
  well is left to the first rule, and a verb before a `que` that precedes the marker (`dijo que mañana venía`) is not checked. An imperfect after such
  a `que` is not checked either.
- **A conjugated verb after `ir a`:** `voy a como` and `vamos a tenemos` expect the infinitive (`comer`, `tener`; the lemma). A noun phrase after `a`
  (`voy a la casa`, `vamos a la tienda`) and a word that can also be a noun are not checked.
- **`ir` followed by a bare infinitive (M51):** `voy comer pan` gives expected `voy a comer`, actual `voy comer`, with the span on both words (the
  error is the gap, so the span covers the pair and `expected` is the whole corrected phrase). `vamos comer fruta` gives `vamos a comer`, `van tener`
  gives `van a tener`. Present and imperfect forms of `ir` are checked. Clean: `voy a comer`, `quiero comer`, `vamos a la tienda`, `voy al río`, `ir a comer`
  (the infinitive `ir`), `voy contento`, `vamos` alone, and the preterite `fui comer`, which is left alone on purpose (`fui` is also `ser`, and the
  repair is not `ir a`). A next word that can also be a noun or an adjective is not checked.

The checks work on the whole utterance, not on clauses, and on the verb forms the lexicon lists.

### Carry the context to the next utterance

The host passes the repeat counts it carried from the previous `Next`; the first utterance of a conversation starts with none (`Create` with
`repeatCountsByTag: null`). `RepeatCountsByTag` holds how many times in a row each error tag, and `not_communicated`, was seen before this
utterance. The pipeline counts this utterance itself: after the verdict it applies `RepeatCounts.Next` and gives the result to the feedback
policy and the repair ladder, so the host never adds the current tags and never sends counts that already include them.

`ConversationContext.Next` builds the context of the next utterance from this one and the result: the agent the utterance spoke to in the
state the decision moved it to, the learner model after the update, the repeat counts before the next utterance (`RepeatCounts.Next` of
this context's counts and the verdict, which counts each error tag and `not_communicated` one more in a row and drops every other tag), and
the world seed.

```csharp
using Vocaterra.Nlp.Pipeline;

IReadOnlyDictionary<string, int> counts = RepeatCounts.Next(context.RepeatCountsByTag, result.Verdict);   // the counts before the next utterance, ordinal keys
context = context.Next(result, new StoredTimestamp(now));          // the context of the next utterance
```

The host still supplies the stored time, and it keeps the states of the other agents it talks to: `Next` carries only the agent of this
utterance. `Next` recomputes `HostStateHash` from the values it carries, as `Create` does, so a host that gave its own digest to the constructor
computes that digest again after `Next`.

### Two-phase evaluation: host facts that depend on the utterance (CR-017)

Host slots are taken before `Evaluate`, but some facts exist only after the core has read the line: the answer to "¿dónde está la arena?" needs
the nearest sand, and the host can look that up only once it knows the noun is `arena`. Split the evaluation instead of evaluating twice.
`Evaluate(clip, prompt, context)` is exactly `Complete(Analyze(clip, prompt), context)`: the same result, the same replay record and the same hashes.

```csharp
// Phase 1: recognition, normalization, analysis, intent and slots, answer match, pronunciation, verdict. Reads no conversation state.
UtteranceAnalysis analysis = pipeline.Analyze(clip, prompt);

// The host reads what phase 1 found (analysis.Verdict, analysis.Words, analysis.Answer, analysis.Asr) and computes its facts.
string noun = analysis.Words!.First(w => w.Reading?.Pos == "noun").Token.Text;          // "arena"
SlotMap near = SlotMap.From([new("target", noun), new("direction", Nearest(noun)), new("distance", "12")]);

// Phase 2: decision, response, effects, repair and the replay record, with the facts as host slots and extra skills (M26d semantics:
// per utterance, the verdict wins on a slot clash, the skills join the single learner update).
PipelineResult result = pipeline.Complete(analysis, context, near, extraSkills: [$"word.{noun}"]);
```

- **What phase 1 hands over.** `UtteranceAnalysis` exposes `Verdict`, `Words`, `Answer` (the values the final `PipelineResult` will carry),
  `Asr` and `Posteriors` (the recognizer outputs), `ClipHash` and `Prompt`. Everything else phase 2 needs is kept internally. The recognizers run
  once, in `Analyze`; completing the analysis never calls them again (a speech host recognizes once).
- **Three ways to give the host facts.** `Complete(analysis, context)` takes them from the context (`HostSlots`, `ExtraSkills`);
  `Complete(analysis, context, hostSlots, extraSkills)` replaces them with the arguments, through `context.WithHost(...)`, which recomputes
  `HostStateHash` as `Create` does, so the replay record covers the facts; `CompleteTrace` is `Complete` that returns the `PipelineTrace` a replay log needs
  (`ReplayEnvelope.From(trace, context)` with the context passed to phase 2, after `WithHost`).
- **Why two phases and not a callback.** A callback inside `Evaluate` would run host code in the middle of the pipeline: it can throw, block or
  read the clock there, it cannot be tested apart from the pipeline, and what it saw is not in any record. With two phases the intermediate is a plain
  value: the host's computation sits between two ordinary calls, nothing in the pipeline waits on it, the host can complete the same analysis
  in several contexts, and a replay needs nothing new, because the envelope already holds the recognizer output and the final context.
  `Evaluate` stays the single call for hosts that have no utterance-dependent facts.
- **Mismatch.** `Complete` throws `ArgumentException` when the analysis came from a pipeline over other content or models (the version stamps differ).
- **Threads.** An `UtteranceAnalysis` is immutable and safe to pass to another thread, and to complete more than once, from several threads at the same
  time (section 8). Its posterior matrix is a shared array: read-only for the host.

### Host-initiated agent turns (CR-015)

The guide opens a review with a question ("¿Qué es esto?") before the player says anything. The state machine used to move only on an utterance
verdict; an agent file can now also name a host event as a transition trigger, and the host raises it with `Advance`.

```json
{ "id": "idle", "transitions": [
  { "onHostEvent": "guide.ask", "to": "asking",
    "actions": [ { "dialogueIntent": "guide.review.ask", "hostEvent": "ui.show_question", "variantPoolId": "guide_ask" } ] },
  { "on": { "intent": "greet" }, "to": "trading", "actions": [ { "dialogueIntent": "greet_reply" } ] } ] }
```

```csharp
HostTurnResult? turn = pipeline.Advance("guide.ask", context);          // context.HostSlots feed the line, as for an utterance
if (turn is not null)
{
    foreach (ResponseLine line in turn.Response.Lines) Say(line.Text);
    foreach (AgentAction action in turn.Agent.Actions) Raise(action.HostEvent);   // the host events to act on
    context = context.Next(turn, new StoredTimestamp(now));              // the agent is in its new state, the counter advanced
    using var w = new StreamWriter("session.jsonl", append: true, new UTF8Encoding(false));
    new ReplayLog(w).Append(HostTurnEnvelope.From(turn, before));        // `before` is the context passed to Advance
}
```

- **Schema.** `AgentTransition` has one new optional member, `onHostEvent` (a non-empty event name). A transition with it is a host-event
  transition: only `Advance` takes it (the first one of the current state with that name, declaration order, ordinal comparison), and an utterance
  verdict never does, so an `"on": {}` that matched every verdict does not change meaning. `on` may then be omitted (it reads as the empty
  condition, ignored). Agent files without `onHostEvent` load and behave exactly as before.
- **The turn.** The matched transition's actions are voiced from their dialogue pools: an action with a variant pool uses the variant the step drew
  (`SeededRng.ForInteraction(worldSeed, agentId, counter)`, one draw per such action), any other action draws from the same interaction stream, as an
  utterance reply does. Lines render with `context.HostSlots` only; there is no verdict, so there are no verdict slots and no feedback. A line that names a host slot
  the context lacks throws `TemplateException`; an action naming a pool the bundle lacks is skipped, as for utterance actions.
- **The counter.** The step advances the interaction counter by one, exactly as an utterance step does, so a later utterance draws from the next
  stream and the host's record of how often the agent spoke stays aligned with the core's.
- **No step.** `Advance` returns `null` (and changes nothing, the counter is not advanced, there is nothing to record) when the bundle has no state
  machine for the context's agent or the agent's current state has no transition for the event. It is a null rather than an exception because a host
  raises events from game state it does not fully control; an agent state that belongs to another agent still throws `ArgumentException`.
- **Learner and repeat counts: untouched.** `context.Next(turn, now)` carries the learner model and the repeat counts over unchanged and moves only the
  agent. Reason: nothing the player did was observed. A skill update would be evidence out of nothing, and a repeat count is "the same error in a
  row", which a question in between should neither extend nor reset. A host that wants the question to count (for example "the player was shown
  this word") says so with `learner.StepSkills(...)`. Host slots are per step and are not carried over.
- **Replay.** A host turn is a second kind of replay log entry (`HostTurnEnvelope`, see [REPLAY.md](REPLAY.md)); `Replayer.Replay(HostTurnEnvelope, pipeline)`
  runs `Advance` again in the recorded context and compares the whole result. It needs no recognizer.

## 6. Plan the response (D0, with D1 for speech)

`Evaluate` also plans the response: the agent's lines, the effects the verdict triggers and an optional repair step.

```csharp
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Response.Effects;

foreach (var line in result.Response.Lines)
{
    Console.WriteLine($"agent: {line.Text}");
}

foreach (ResolvedEffect effect in result.Effects)              // the kind is an opaque string; the host decides what it means
{
    Console.WriteLine($"{effect.EffectId} {effect.Kind} {effect.Value}");
}

if (result.Repair is { } repair)
{
    Console.WriteLine($"repair: {repair.Kind} {repair.AgentLine}");
}
```

The repair ladder (`RepairStrategy.Next`, M29f) is deterministic by failed attempt: attempt 1 `AskAgain`, attempt 2 `SlowRepeat`, attempt 3
`OfferChoices`, later attempts `GiveUp`.

**Repair lines (M33).** The agent lines of attempts 1 and 2 come from the bundle's reserved dialogue pools `repair.ask_again` and
`repair.slow_repeat` when the bundle has them (`ReservedPools.RepairAskAgain`, `RepairSlowRepeat`): one variant is drawn with the interaction's
seed, and its text is spoken as written (no slots). Without the pools the lines are the language pack's `RepairFallback`, which for Spanish are
`¿Cómo?` and `Perdón, te lo repito más despacio.`. The give-up line is always the pack's. A bundle without the pools (`content/game-v1`) speaks
the same lines as before. The `OfferChoices` line (`¿Quieres decir «…»?`) is not a pool and is still Spanish text in the core. A constrained prompt's retry at attempts 1 and 2 recognizes every phrase of the answer set
(`AnswerSet.GrammarPhrases()`, aliases included). The `OfferChoices` step offers one phrase per answer, the **canonical** phrase
(`AnswerSet.CanonicalPhrases()`, normalized as the grammar is, no aliases), in the answer set's **declared order** (the order the answers
are written in the content, not an ordinal sort), and the first three distinct ones are offered. The retry at attempt 3 is a constrained
request limited to exactly those phrases, so a host can show them as buttons from `RetryRequest.GrammarPhrases`. Answers that normalize to
the same phrase, or to nothing, are left out. A set with fewer than three answers offers all of them.

Which answers are offered is therefore set by the content: a set declares the answers the prompt is for first, and a help or don't-know
answer last, so it is offered only when the set has fewer than three others. Given a yes/no set declared as confirm, deny, help, don't-know,
the choices are the confirm, the deny and help. There is no content marker for "service" answers, and the core does not recognize intent ids
in the repair ladder; a set that needs a different order is declared in that order.

Host slots (section 4) are available to the reply and feedback templates of this response; the planner is called with them as
`ResponsePlanner.Plan(..., hostSlots: context.HostSlots)`, and a host that renders a template of its own can use the same engine:
`bundle.TemplateEngine.Render(template, result.Verdict.Slots.WithFallback(hostSlots))` (the verdict wins, as in the pipeline).

The effect values come from the bundle's curves at the verdict's correctness; the core does not know what a value is worth. The host maps
`trade_price` or `spell_power` (or any other kind) to its own rules.

Speech is a separate call, so a host that shows text does not pay for audio. When the services have a synthesizer (D1 with Kokoro, D0 with
the silent stand-in), `Synthesize` renders every line of the plan in order:

```csharp
IReadOnlyList<AudioBuffer> speech = pipeline.Synthesize(result.Response, "agent-voice");
```

**Kokoro voices (CR-006).** `KokoroSynthesizer.FromStore(store, "kokoro-multi-lang-v1_0", speakerId, speed)` builds the adapter from the
manifest entry, with the manifest's model hash; the manifest must pin `model.onnx`, `voices.bin`, `tokens.txt` and the `espeak-ng-data/`
files, or the call throws. The adapter takes a numeric speaker id only (it does not look up names). The model has 54 speakers (ids 0 to 53),
the audio is 24000 Hz. The Spanish voices, read from the `speaker2id` metadata of the model's `model.onnx` (the release's `README.md` agrees
on `em_santa`):

| Speaker id | Voice | Use |
| --- | --- | --- |
| `28` | `ef_dora` | Spanish, female |
| `29` | `em_alex` | Spanish, male |
| `53` | `em_santa` | Spanish, male |

**Voice language (M33).** `FromStore(store, id, speakerId, speed = 1.0, lang = "es")` and the constructor's `lang` name the voice language the model
speaks. It is folded into `ModelHash` only when it is not `es`, so the Spanish hash is the one it always was; `KokoroSynthesizer.DefaultLanguage`
is `es`. The game's `SpeechEngine` calls `FromStore` without `lang` and keeps the Spanish hash.

Speed defaults to `1.0`. The engine is loaded for every `Synthesize` call and disposed at its end (see section 8), so wrap the
synthesizer in `CachedSynthesizer` when the same lines repeat.

## 7. Record and replay a log (D0, no models)

```csharp
using System.Text;
using Vocaterra.Nlp.Asr;

PipelineTrace trace = pipeline.Trace(clip, prompt, context);   // Trace is Evaluate plus the recognizer output
using (var writer = new StreamWriter("session.jsonl", append: true, new UTF8Encoding(false)))
{
    new ReplayLog(writer).Append(ReplayEnvelope.From(trace, context));
}

// Later: every entry is re-evaluated from its recorded recognizer output, so no audio is needed. The replay pipeline keeps the
// recording's services (the replayer swaps in only the recognizers), so the model hashes in `versions` match the recording.
var envelopes = ReplayReader.ReadEnvelopes(new StreamReader("session.jsonl", Encoding.UTF8));   // utterance entries only; ReadEntries also returns host turns
var recorded = new ReplayWordRecognizer(new Dictionary<string, AsrResult>(StringComparer.Ordinal));
var replayPipeline = new UtterancePipeline(new PipelineServices(recorded, phonemes, embeddings, synthesizer, bundle));
foreach (ReplayEnvelope envelope in envelopes)
{
    ReplayDiff? diff = Replayer.Replay(envelope, replayPipeline, static hash => new PcmClip([], PcmClip.DefaultSampleRate, hash));
    Console.WriteLine(diff is null ? "OK" : $"DIFF {diff}");
}
```

A log that also holds host-initiated turns (`HostTurnEnvelope`, section 4) is read with `ReplayReader.ReadEntries`, which returns each entry as an
utterance envelope or a host turn; `ReadEnvelopes` and `Read` skip the host turns.

A replay that prints `DIFF` means the stages, the content or the versions changed since the entry was recorded. A replay needs the same
embedder and synthesizer that produced the log, because those stages run again; with the fakes and the silent synthesizer that needs no model
file. [REPLAY.md](REPLAY.md) lists the envelope fields and what a diff points at.

## 8. Threads and load cost (CR-008)

What the code guarantees, as of this section. Where it gives no guarantee, the safe rule is given.

**Threads.** One `UtterancePipeline` may be called from several threads at once, including one thread in `Evaluate` or `Trace` while
another runs `Synthesize` on the same pipeline. The pipeline has no mutable state: its fields are read-only, the bundle and the lexicon are
immutable after the load, and the parts built on first use (the lexicon's derived indexes, the intent resolver's example vectors) are
published through `Lazy` or `ConcurrentDictionary`, so two first calls build them once. `Evaluate` also runs the word and phoneme
recognizers on thread-pool tasks when `RunRecognizersConcurrently` is set, so a recognizer sees calls on a pool thread, not on the caller's.
The guarantee stops at the injected adapters, which the pipeline does not make thread-safe:

| Adapter | Constructed | Concurrent calls on one instance | Shared across pipelines or threads |
| --- | --- | --- | --- |
| `ScriptedWordRecognizer` (test double) | in code | serialized by a lock | yes |
| `VoskWordRecognizer` | loads the model in the constructor (and a runtime copy on disk) | no lock: each `Recognize` makes its own native recognizer on a shared model, which the code does not guarantee; `Dispose` is not synchronized with `Recognize` | one instance per thread; do not dispose while another thread may recognize |
| `SherpaOnnxOfflineRecognizer` | loads the model in the constructor | `Recognize` and `Dispose` take one lock: calls queue, `Dispose` is safe | yes, with calls serialized |
| `Wav2Vec2PhonemeRecognizer`, `OnnxEmbeddingModel` | ONNX session in the constructor | no lock; the code gives no guarantee (ONNX Runtime documents concurrent `Run` as supported, which this code does not test) | one instance per thread, or serialize the calls |
| `KokoroSynthesizer` | checks the files in the constructor; each `Synthesize` loads its own engine and disposes it (about 1.5 s per short line on the development machine) | `Synthesize` takes one lock: calls queue | yes, with calls serialized |
| `CachedSynthesizer` over `DirectorySynthesisCache` | in code | documented as not safe for concurrent requests of the same key: a miss on both threads can make one `Put` throw (both write the same temporary file) | one thread per key |
| `SilentSynthesizer` | in code | its counter is interlocked | yes |

The rule for a host that runs evaluation and speech on different threads: one pipeline (or one set of services) per process is fine; give
each Vosk, ONNX recognizer and each cached synthesis key one thread of its own, or serialize their calls, and do not dispose a recognizer
while another thread can call it.

**Two-phase evaluation and host turns.** `Analyze`, `Complete`, `CompleteTrace` and `Advance` follow the same rule as `Evaluate`: they keep no state
in the pipeline. An `UtteranceAnalysis` is immutable (its members are get-only and the pipeline never mutates what it holds), so it may be handed
to another thread, completed there, and completed more than once or concurrently, in any contexts. The only shared mutable object reachable from
it is the phoneme recognizer's posterior array, which every consumer treats as read-only. `HostTurnResult` is an immutable record.

**Load cost.** `ContentLoader.Load` reads every file once into memory, checks every hash, parses the catalogs and validates the
cross-references; the imported Wiktionary lexicon (tens of thousands of entries) dominates. Load once per process and share the bundle;
`ContentLoadOptions.LexiconCache` avoids re-parsing the same lexicon files when a host loads several bundles. Building the pipeline is cheap:
the constructor creates the analyzer, the resolver and the response planner, and copies the dialogue pools. Models load where the adapter's
constructor says so: a recognizer or an ONNX model when it is constructed (`FromStore` uses the manifest hash, so it hashes nothing), the
Kokoro engine on every `Synthesize` (no engine is kept between calls: sherpa-onnx's Kokoro keeps state, so a reused engine changes its
output for the same text), and the intent resolver's example vectors on the first open-prompt call that embeds. `PipelineServices`
reads each adapter's `ModelHash` when it is constructed, so Kokoro hashes its files there. Load the models before the first utterance a
player waits for.

**Parakeet word timing (M26g).** `SherpaOnnxOfflineRecognizer` reads the transcript, the tokens and their start times from the native
result, and never the per-token durations: sherpa-onnx 1.13.8 returns a buffer for them on the Parakeet TDT greedy path that is not
float data (it changes on every decode), which made the last word's end time differ between identical decodes. Each word therefore ends
where the next one starts, and the last word ends at the clip end. Recognition results are deterministic for the same clip and model
(the whole `AsrResult` is compared by `ParakeetFromStoreTranscribesTheSpanishSampleClipDeterministically`). The thread count stays 1.

**Immutable after construction.** The `ContentBundle` and all it exposes (catalogs, lexicon, prompts, host file bytes), `UtterancePipeline`
and `PipelineOptions` (init-only), `PipelineServices` (read-only properties; the recognizers it holds are not immutable), `ConversationContext`,
`AgentState` and `LearnerModel` (updates return new values). The adapters are not immutable: they own native handles and counters, and
`Dispose` ends them.

## What is deterministic

| Step | Tier | Notes |
| --- | --- | --- |
| Load a bundle | D0 | Hashes and cross-references are checked; a changed file changes the bundle hash |
| Services with scripted or fake recognizers | D0 | No model file; the verdict is a pure function of the transcript and the context |
| Services with real recognizers, phonemes, embeddings, synthesis | D1 | Frozen models, one CPU thread, greedy decoding; their hashes are in `versions` |
| Pipeline, context | D0 | No clock: the stored timestamp, the seed and the counter are inputs |
| Evaluate, verdict, decision, response plan | D0 over the recognizer output | Same input, byte-identical `CanonicalJson` output |
| `Analyze` then `Complete`; `Advance` | D0 over the recognizer output | `Evaluate` equals `Complete(Analyze(...))` byte for byte; a host turn is a pure function of the context and the content |
| Record and replay | D0 | Replays need the recorded recognizer output and the recording's embedder and synthesizer (fakes need no model file); they compare the recorded verdict field by field |
