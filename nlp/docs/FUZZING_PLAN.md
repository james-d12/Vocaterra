# Combinatorial Fuzz Testing Plan

Status: planned (milestone M16 in `IMPLEMENTATION_PLAN.md`). Modelled on
[CodeGuard.RuleFuzzing.Tests](https://github.com/james-d12/CodeGuard/tree/main/tests/CodeGuard.RuleFuzzing.Tests):
catalog-driven generators, explicit oracles, full soak by default, seed replay for reproduction.

## 1. Why this project needs it

The voice pipeline is a chain of twelve deterministic stages whose inputs are open-ended (speech,
learner text, content data) and whose central promise is *same input, same verdict, on every
machine*. Hand-written unit tests pin the worked examples; they cannot explore the combinatorial
space of lexicon × grammar × mutation × dialect × difficulty × NPC state. Fuzzing closes that gap:

- **Crash oracles** find the unhandled `IndexOutOfRange`, `KeyNotFound`, `ArgumentException`,
  infinite loop, or `NaN` that an unusual-but-valid input triggers.
- **Determinism oracles** prove the §3 rules hold across thousands of random inputs rather than a
  handful of goldens.
- **Agreement oracles** check that independent components agree with each other, so a change to one
  cannot silently drift from another: the morphology generator against the analyzer, the recast
  builder against the analyzer, the replayer against the pipeline, the content hasher against the
  loader.

## 2. Approach (what is kept from CodeGuard)

| CodeGuard practice | Here |
| --- | --- |
| `CsCheck` generators (`Gen<T>`), `Sample(..., iter:, threads: 1)` | Same library (4.9.1), same call shape, `threads: 1` so determinism oracles compare like with like |
| Generation walks the engine's own capability catalog | Generation walks **our catalogs**: `Lexicon.Seed`, `ErrorTags.All`, the `SlotGrammar` DSL, `IntentCatalog`, `DialogueCatalog`, `PhoneInventory`, `DialectConfig.AllShipped`, `NpcDefinition`, `AnswerSet`s from `content/v1` — new content widens coverage with no test edits |
| Benign + adversarial corpus mixed by weight | `AdversarialCorpus` of strings, audio, phones, JSON mutations; 1-in-4 adversarial |
| `KnownParameterConstraints` allow-list of expected rejections | `KnownTolerances`: an auditable list of *expected* exceptions (e.g. `TemplateException` for a missing slot, `InvalidDataException` for a malformed WAV) |
| One `*FuzzTests.cs` per oracle | Same |
| Full soak by default, env var to go deeper, `CsCheck_Seed` replay | `SLGFUZZ_ITERATIONS*` env vars; failures print the seed and the full generated input as canonical JSON so it can be pasted into a regression `[Fact]` |
| README lists bugs found and known-not-fixed findings | Same, maintained with the suite |

Everything runs in process with fakes for the D1 models (`ScriptedWordRecognizer`,
`FakePhonemeRecognizer`, `FakeEmbeddingModel`, `SilentSynthesizer`); no model files are needed.
Target: the whole suite adds under 60 s to `dotnet test` on a laptop.

## 3. Project layout

```
tests/Slg.Fuzzing.Tests/
  Slg.Fuzzing.Tests.csproj          references every src project + Slg.Content; CsCheck via central versions
  README.md                         how to run, deepen, reproduce; bugs found; known-not-fixed
  FuzzOptions.cs                    iteration defaults + env-var overrides; corpus weights
  Generation/
    AdversarialCorpus.cs            fixed edge values (strings, audio shapes, phones, JSON mutations)
    SpanishTextGen.cs               lexicon-driven word/phrase generators, casing/accents/punctuation noise
    SentenceGen.cs                  grammar-driven CORRECT sentences from a SlotGrammar + lexicon
    MutationGen.cs                  one-tag mutations of a correct sentence (see oracle O4)
    AudioGen.cs                     WAV/PCM shapes: tones, noise, silence, clipping, odd rates/lengths
    PhoneGen.cs                     phone sequences from PhoneInventory; posterior matrices for the fake recognizer
    VerdictGen.cs                   random Verdict / stage results within contract ranges
    SlotMapGen.cs                   slot maps from lexicon nouns, verbs, numbers
    ContentMutationGen.cs           structural mutations of content/v1 JSON (drop field, wrong type, dangling id)
  Oracles/
    PipelineHarness.cs              content/v1 + fakes wired once; builders for GameContext and PromptSpec
    KnownTolerances.cs              expected exception allow-list, one documented entry per case
    Determinism.cs                  helper: run twice, compare CanonicalJson, report first differing path
  *FuzzTests.cs                     one per oracle below
```

Naming follows the existing projects; the project joins the solution and CI like any other test
project. `CsCheck` (MIT) is added to `Directory.Packages.props` and must pass `scripts/license-scan.sh`.

## 4. Generators

- **SpanishTextGen.** Words drawn from `Lexicon.Seed` forms (all POS), joined into 1–8 token phrases;
  noise transforms applied with low probability: upper/mixed case, accent stripping, accent *adding*,
  NFD decomposition, `¿¡?!,.` insertion, doubled spaces, number words ↔ digits, leading/trailing
  whitespace, a token from the adversarial string corpus (empty, 10 000 chars, emoji, combining marks,
  `null`, digits only, `al`/`del`).
- **SentenceGen.** Picks a grammar from the content `IntentCatalog` (or a random DSL string built from
  the token kinds `[ART] [DET] [PRON] [NUM:x] [NOUN:x] [ADJ:x] [VERB] [VERB:lemma] [VERB:lemma:slot]`,
  `?`, `(a|b)`), then realises it with **correct** forms using `MorphologyGenerator`: subject pronoun ↔
  conjugated verb, article ↔ noun gender/number, numeral ↔ plural noun, adjective agreement, post-nominal
  adjective order. Returns the sentence, the grammar, and the expected slot values.
- **MutationGen.** Applies exactly one mutation to a correct sentence and names the tag it must produce:
  `conj.person` (swap person), `noun.number` (singular after a numeral > 1), `gender.agree` (flip the
  article), `ser_estar` (swap copula for a classed adjective), `word_order` (move a post-nominal
  adjective before its noun), `conj.irregular` (regularise an irregular form via the paradigm),
  `conj.tense` (add `ayer` to a present-tense sentence), `vocab.unknown` (replace a noun with a
  non-word that is not a near-miss of any lexicon form).
- **AudioGen.** `WavData` at rates {8000, 11025, 16000, 22050, 44100, 48000, 96000}, 1–2 channels,
  PCM16 or float32, lengths 0 ms–5 s, content from {silence, sine, two-tone, white noise, clipped
  square, DC offset, single impulse}; plus raw byte mutations of a valid WAV (truncate, corrupt header
  field, wrong chunk size) for the reader oracle.
- **PhoneGen.** Sequences from `PhoneInventory` 1–20 long; posterior matrices via
  `FakePhonemeRecognizer.FromPhones` with optional one-site confusion drawn from the predicted-error table
  (`ɾ→ɹ`, `+h`, `a→ə`, …) and optional "mumbled" frames.
- **VerdictGen / stage-result generators.** Values within the §4 contracts: confidences in [0,1] (and a
  few adversarial exact-threshold values `t`, `t±ε`), spans inside the text, tags from `ErrorTags.All`
  plus a few unknown tags, slot maps with duplicate keys in different insertion orders.
- **ContentMutationGen.** Loads `content/v1` JSON files as `JsonNode`, applies one structural mutation:
  remove a required property, change a value's JSON kind, replace an id reference with a fresh one,
  append an unknown enum value, duplicate an id, flip one byte of a file without updating the manifest.

## 5. Oracles

Each oracle is one `[Fact]` (or a small `[Theory]`) in its own file. "Deterministic" always means:
run the operation twice on fresh instances, `CanonicalJson.Serialize` both outputs, assert
byte-identical, and on failure report the first differing JSON path plus the generated input.

| ID | Oracle | Generator | Property |
| --- | --- | --- | --- |
| O1 | Normalizer never crashes and is idempotent | SpanishTextGen (+adversarial) | no exception; `Normalize(Normalize(x).Text) == Normalize(x)`; spans are in-range, non-overlapping, ordered; `Original` round-trips to the input substring |
| O2 | Analyzer never crashes, is deterministic, output is well-formed | SpanishTextGen | no exception; errors sorted by span then tag rank, no duplicate `(tag, span)`; every tag ∈ `ErrorTags.All`; spans within text; `Confidence` ∈ [0,1] |
| O3 | Correct sentences analyze clean and match their grammar | SentenceGen | zero grammar errors (pron.* excluded); `MatchedGrammarId` = source grammar; slot values = expected lemmas |
| O4 | **Mutation ↔ tag agreement** (the key oracle) | SentenceGen + MutationGen | the mutated sentence yields exactly the named tag (plus possibly `vocab.unknown` for the non-word case) with `Expected` equal to the original form at that span; the grammar still matches (agreement errors do not block matching) |
| O5 | **Recast closes the loop** | SentenceGen + MutationGen | `RecastBuilder.Build(mutated, analysis)` normalizes to the original correct sentence; re-analyzing the recast yields no grammar errors |
| O6 | Answer matching metamorphic | AnswerSets × SpanishTextGen | canonical/aliases → Exact/Alias with distance 0; phonetic-preserving perturbations (b↔v, ll↔y, s↔z, drop h, strip accents, up to 2 edits) → non-None with `LikelyPronunciation` where phonetic key is equal; unrelated lexicon words → None; result deterministic; `Confidence` ∈ [0,1] |
| O7 | Intent resolver never crashes; rung order is respected | SentenceGen × PromptSpec gen | no exception; `Rung == Grammar` whenever O3 matched a grammar of an allowed intent; Unknown ⇒ margin 0 and confidence 0; allowed-intent filtering never returns a disallowed intent |
| O8 | G2P invariants | SpanishTextGen (letters only) × dialects | phones ⊆ inventory; syllable spans partition the normalized text in order; exactly one stressed syllable per word; deterministic; `Tokenize(ToIpaString())` round-trips; dialect variants differ only in `{θ,s,ʎ,ʝ,h}` sites; each predicted-error variant differs from the base at exactly one site and names a `pron.*` tag |
| O9 | Pronunciation scoring | PhoneGen (+confusions) | perfect posteriors ⇒ all scores 1, no tags (both modes); one predicted confusion ⇒ exactly that tag at that grapheme span; all scores and confidence ∈ [0,1], never NaN; deterministic; Viterbi segments partition frames in label order |
| O10 | Verdict assembly | VerdictGen | correctness ∈ [0,1] and equals `1 − Σ weights` to 1e-9; confidence = min of inputs; errors sorted/deduped; `Communicated` rule holds exactly as documented; `ResolvedBy` consistent with match type/rung; permuting input error order gives an identical verdict; `CanonicalJson` round trip byte-identical |
| O11 | Decision layer | VerdictGen × NPC × seeds × timestamps | NPC step never throws and lands in a declared state, variant index < pool count, same seed ⇒ same path; BKT P(known) ∈ [0,1], non-decreasing on correct, non-increasing on incorrect; FSRS stability > 0, due ≥ now, Hard < Good < Easy intervals, deterministic; feedback plan ≤ 2 items, every tag from the verdict |
| O12 | Templates and dialogue | DialogueCatalog × SlotMapGen | every content template renders for slot maps containing its placeholders; the only tolerated exception is `TemplateException` for a missing slot; output has no `{`/`}`, balanced `¿?`/`¡!`; `plural-if(qty)` agrees with the numeral; seeded variant selection is stable |
| O13 | Audio front-end | AudioGen | `WavFile.Read` throws only `InvalidDataException`/`NotSupportedException` on corrupted bytes, never anything else; valid input ⇒ 16 kHz mono clip, length = `floor(n·up/down)`, samples finite; VAD output is a contiguous sub-range of the input; gain never exceeds the target peak; hash stable across two runs; `WavFile` write→read round-trips |
| O14 | Pipeline end to end | SentenceGen/MutationGen × prompts × GameContext | never throws with `ScriptedWordRecognizer`; `PipelineResult` deterministic; verdict is the same as calling the stages by hand (agreement); replay envelope round-trips (`Replayer` reports no diff); mutating any envelope field ⇒ a diff naming that path (replay sensitivity) |
| O15 | Content schema violations | ContentMutationGen | every mutation makes `ContentLoader.Load` throw `ContentValidationException` (never another type) naming the mutated path; byte flips change the bundle hash (hash sensitivity); the unmutated bundle always loads |
| O16 | Serialization round trips | all generators | every public record that flows through `CanonicalJson` (`Verdict`, `AnalysisResult`, `IntentResolution`, `PronunciationResult`, `ReplayEnvelope`, catalogs) survives serialize→deserialize→serialize byte-identically |

## 6. Tolerances (expected, documented rejections)

`KnownTolerances` is the only place an exception may be waved through, and every entry says why:

- `TemplateException` when a generated slot map lacks a placeholder the template uses (O12).
- `InvalidDataException` / `NotSupportedException` from `WavFile.Read` on corrupted or unsupported
  input (O13).
- `ContentValidationException` from the loader on a mutated bundle (O15). Any *other* exception type
  from the loader is a bug.
- Analyzer on a sentence containing adversarial-corpus tokens may report `vocab.unknown`; O3/O4 only
  use lexicon-derived sentences so they do not tolerate it.

## 7. Iteration budgets and reproduction

| Oracle | Default iterations | Env var |
| --- | --- | --- |
| O1, O2, O6, O8, O10, O11, O16 | 3 000 | `SLGFUZZ_ITERATIONS` |
| O3, O4, O5, O7, O12 | 2 000 | `SLGFUZZ_ITERATIONS_GRAMMAR` |
| O9, O13 | 1 000 | `SLGFUZZ_ITERATIONS_SIGNAL` |
| O14 | 300 | `SLGFUZZ_ITERATIONS_PIPELINE` |
| O15 | 500 | `SLGFUZZ_ITERATIONS_CONTENT` |

Budgets are tuned so the whole project stays under about 60 s; `README.md` records the measured time.
A failure prints CsCheck's seed; rerunning with `CsCheck_Seed=<seed>` replays the shrunk case. The
assertion message always includes the generated input as canonical JSON, so the case can be pasted
into the owning module's test project as a permanent regression `[Fact]` once fixed.

## 8. Delivery

- **M16a — foundation and language oracles:** project, CsCheck, `FuzzOptions`, corpus, text/sentence/
  mutation/slot-map generators, harness, tolerances, O1–O7, O10, O12, O16; README with timings.
- **M16b — signal, decision, content and pipeline oracles:** audio/phone/content-mutation generators,
  O8, O9, O11, O13, O14, O15; README "bugs found" section; CI job in the parked workflow.
- Bugs found are fixed in the owning module with a regression test there, in the same milestone, and
  listed in the README. Findings that are design changes are listed under "known, deliberately not
  fixed" with the reasoning, as CodeGuard does.

## 9. Success criteria

1. `dotnet test tests/Slg.Fuzzing.Tests` runs every oracle at full default depth, green, under ~60 s.
2. Each oracle in §5 exists, is catalog-driven where the table says so, and reports the generated
   input on failure.
3. At least the O4/O5 closed loop (generate → mutate → analyze → recast → re-analyze) is in place; it
   is the strongest guard the evaluator has.
4. Any bug found during the build is fixed at its source with a regression test, and documented.
