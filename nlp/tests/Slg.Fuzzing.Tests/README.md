# Slg.Fuzzing.Tests

Combinatorial, property-based fuzz testing for the voice pipeline, modelled on `CodeGuard.RuleFuzzing.Tests`. The design is
`docs/FUZZING_PLAN.md` (generators, oracles O1 to O16, tolerances, iteration budgets). In short: every generator walks **our own
catalogs** (the lexicon, the `IntentCatalog` grammars, `ErrorTags.All`, the `DialogueCatalog` templates, the `AnswerSet`s of
`content/v1`), mixes benign and adversarial inputs 3 to 1, and runs them through the real stages looking for crashes, drift between
components that must agree, and broken determinism. Because the generators read the catalogs, new content widens the coverage with no
test edits. Everything runs in process with fakes for the model-backed stages (`ScriptedWordRecognizer`, `FakePhonemeRecognizer`,
`FakeEmbeddingModel`, `SilentSynthesizer`); no model file is needed.

This is **stage 1 (M16a)**: the foundation and the language oracles. Stage 2 (M16b) adds the audio, phone and content-mutation generators and
the signal, decision, content and pipeline oracles (O8, O9, O11, O13, O14, O15).

## Running

```bash
dotnet test tests/Slg.Fuzzing.Tests
```

No flags are needed: every fuzz `[Fact]` runs its full default depth on a plain run (there is no "fast" tier to opt out of). Measured on a
development machine, with the project already built: **about 5 s wall time** for the whole project at default depth.

| Oracle | What it checks | File | Default iterations | Env var |
| --- | --- | --- | --- | --- |
| O1 | Normalizer never crashes, is idempotent, spans and originals are well-formed | `NormalizerFuzzTests` | 3 000 | `SLGFUZZ_ITERATIONS` |
| O2 | Analyzer never crashes, is deterministic (also on a fresh instance), output is well-formed | `AnalyzerFuzzTests` | 3 000 | `SLGFUZZ_ITERATIONS` |
| O3 | Correct sentences analyze clean, match their grammar, yield their slot values | `CorrectSentenceFuzzTests` | 2 000 | `SLGFUZZ_ITERATIONS_GRAMMAR` |
| O4 | One mutation yields exactly the named tag, with the original form as correction | `MutationTagFuzzTests` | 2 000 | `SLGFUZZ_ITERATIONS_GRAMMAR` |
| O5 | The recast closes the loop: it restores the corrected sentence and re-analyzes clean | `RecastLoopFuzzTests` | 2 000 | `SLGFUZZ_ITERATIONS_GRAMMAR` |
| O6 | Answer matching agrees with an independent model of its documented rules, plus metamorphic relations | `AnswerMatchFuzzTests` | 3 000 | `SLGFUZZ_ITERATIONS` |
| O7 | Intent resolver: allowed intents respected, ladder order respected, unresolved is margin 0 and confidence 0 | `IntentResolverFuzzTests` | 2 000 | `SLGFUZZ_ITERATIONS_GRAMMAR` |
| O10 | Verdict assembly follows its documented rules; order of unordered inputs does not matter; JSON round trip | `VerdictAssemblyFuzzTests` | 3 000 | `SLGFUZZ_ITERATIONS` |
| O12 | Every template renders clean text; only the missing-slot `TemplateException`; stable seeded selection; planner | `TemplateFuzzTests` | 2 000 (three facts) | `SLGFUZZ_ITERATIONS_GRAMMAR` |
| O16 | Everything that flows through `CanonicalJson` round-trips byte for byte | `SerializationRoundTripFuzzTests` | 3 000 stage records, 1 000 catalogs, 300 replay envelopes | `SLGFUZZ_ITERATIONS`, `SLGFUZZ_ITERATIONS_PIPELINE` |

The mutations of O4 and O5 are `conj.person`, `noun.number` (singular after a plural determiner, and plural after a singular one),
`gender.agree` (article and adjective), `ser_estar`, `word_order`, `conj.irregular`, `conj.tense` and `vocab.unknown`. O5 closes the loop for all
of them except the non-word, for which the recast must leave the words alone.

`PipelineHarnessTests` and `OracleInfrastructureTests` are small guards for the harness builders stage 2 relies on and for the suite's own
helpers (failure messages, first-difference path, tolerances, pattern round trip).

## Pushing even deeper for a one-off soak

Each oracle reads its iteration count from the environment variable above when it is set to a positive integer:

```bash
SLGFUZZ_ITERATIONS=50000 SLGFUZZ_ITERATIONS_GRAMMAR=50000 SLGFUZZ_ITERATIONS_PIPELINE=3000 dotnet test tests/Slg.Fuzzing.Tests
```

Fifty thousand iterations per oracle take well under a minute. (`SLGFUZZ_ITERATIONS_SIGNAL` and `SLGFUZZ_ITERATIONS_CONTENT` are reserved for stage 2.)

## Reproducing a failure

CsCheck picks a fresh random seed on every run and prints it on failure:

```
Set seed: "6VTk-wxfywU1" or -e CsCheck_Seed=6VTk-wxfywU1 to reproduce (3 shrinks, 1,519 skipped, 2,000 total).
```

Re-running with `CsCheck_Seed=<seed>` in the environment replays the failing case. The assertion message always carries the generated input
so the case can be pasted into the owning module's test project as a permanent regression `[Fact]` once the bug is fixed:

```
[O3] A correct sentence produced grammar errors: gender.agree@0+6 expected 'buenas' actual 'buenos'.
Input: "pattern=[(buenos|buenas) (días|tardes|noches)] text=\"buenos noches\" normalized=\"buenos noches\" ..."
```

A sentence input prints its grammar pattern, the written text and the normalized text; a verdict input prints the canonical JSON of the whole
`VerdictInputs`; a text input prints the string as a JSON literal. Determinism failures also name the first differing JSON path.

All `Sample` calls run with `threads: 1`, so a determinism oracle compares two runs on the same footing. xUnit still runs the test classes in
parallel; the shared fixtures (`PipelineHarness.Shared`) hold only immutable data and thread-safe lazy indexes.

## Layout

- `FuzzOptions.cs` - iteration defaults and env-var overrides; the 3-in-4 benign, 1-in-4 adversarial weights.
- `Generation/`
  - `AdversarialCorpus` - fixed edge strings (empty, 10 000 characters, emoji, combining marks, lone surrogates, number words, `{slot}`, `null`, `al`, `del`).
  - `LexiconCatalog` - the lexicon through the generators' eyes: nouns with both numbers, verbs with full paradigms, descriptive adjectives, the closed classes.
  - `SpanishTextGen` / `TextNoise` - lexicon-driven phrases (word salad and loosely structured phrases) with casing, accent, NFD, `¿¡?!,.`, spacing, number-word and adversarial-token noise.
  - `GrammarPattern`, `PatternGen` - slot grammar patterns as a tree: every catalog grammar, structural skeletons, and random DSL strings built from the token kinds.
  - `SentenceGen`, `SentenceRealizer`, `SentenceModel` - correct sentences from a pattern, agreement right by construction (see below).
  - `MutationGen` - exactly one mutation of a correct sentence, with the errors the analyzer must report and the sentence the recast must restore.
  - `SlotMapGen` - slot maps derived from the placeholders and filters of the real templates.
  - `VerdictGen` - random stage results within the contract ranges, including exact-threshold confidences.
- `Oracles/` - `PipelineHarness` (bundle plus fakes wired once, with builders for services, pipeline, `GameContext`, prompts), `KnownTolerances`, `Determinism`.
- `*FuzzTests.cs` - one file per oracle.

### How the sentence generator stays honest

`SentenceRealizer` mirrors the analyzer's own scans (noun phrase = determiners, adjuncts, a noun head, adjuncts; a subject pronoun pairs with
the next verb after at most two adverbs or clitics) and builds each sentence so that articles, numerals and adjectives agree with their noun,
verbs agree with their subject pronoun, adjectives sit on the right side of the noun, copulas meet an adjective of their class, and no past
marker sits next to a present verb. It is a model of the analyzer, so the oracles are an agreement check between two implementations. A
pattern that cannot be realised correctly is **skipped and counted**, never silently weakened; each oracle prints its generator
diagnostics and asserts a minimum of realised sentences, and `MutationTagFuzzTests` and `RecastLoopFuzzTests` assert that every mutation
kind stays reachable. If a mirrored table in `LexiconCatalog` (the ser/estar adjective lists, the prenominal adjectives, the past-time
adverbs, ...) drifts from `AnalysisTables` in `Slg.Nlp`, the mutation oracles fail and name the kind.

Slots are compared strictly unless the pattern is ambiguous (two optional tokens that can match the same word, `[DET]` against `[ART]` or
`[ADJ]`, `un` and `una` as article or numeral, a literal word that is also a noun): then the matcher may legitimately bind the words in more
than one way and only the extracted values are checked against the words of the sentence.

## Bugs found and fixed while building this suite

- **`RecastBuilder` picked the gender of `un`/`una` from the word after the numeral, not from the noun** (`Slg.Response`). `1 grande queso`
  was recast as `una grande queso`, because the invariant adjective `grande` has a feminine reading that came first. The builder now looks
  through the adjectives to the noun the numeral quantifies and falls back to the next word only when there is no noun. Found by O5
  (CsCheck seed `2PhTP0PVr4W1`, input `ellos compran taberna 1 grande queso`). Regression: `RecastBuilderTests.DigitsAreSpelledOutBeforeSpeaking`
  (`tengo 1 grande queso`, `tengo 1 buena espada`) in `tests/Slg.Response.Tests`.

## Known, deliberately not fixed

Findings that are content gaps or design limits rather than defects of one stage. None changes behaviour here; the generators step around
them explicitly (each is a skip or a restriction in the generator, counted in the diagnostics) and the oracles stay strict.

- **Content: `cuál` is not in the lexicon.** The `ask_price` grammar `cuál es el precio de [ART]? [NOUN:item]` therefore gives
  `vocab.unknown` (0.4 off correctness at Beginner) for a perfectly correct utterance. O3 reports the grammar as `skip:unknown-literal:cuál`.
  Fix is a lexicon entry in `content/v1/lexicon` plus re-pinning the bundle hash, which is content work outside this stage.
- **The analyzer's checks read every word as every part of speech it can be.** A noun that is also a verb form (`ayudas`, `lanza`, `vino`)
  is reported as `conj.tense` in a sentence with `ayer`, and as `conj.person` right after a subject pronoun; `como` (conjunction) after
  `nosotros` is read as the verb `comer`; `sé` (saber) is also the imperative of `ser`, so `sé preocupado` is a `ser_estar` mistake;
  `fueron` is both `ir` and `ser`, so a recast `fueron vacío` trips `ser_estar`. A fix needs part-of-speech disambiguation (design). The
  generators avoid these homographs (`LexiconCatalog` drops them from the closed classes; the mutation generator skips the sentences).
- **`Accents.Strip`, `SpanishPhoneticKey.Compute` and `string.Normalize` throw `ArgumentException` on ill-formed UTF-16** (a lone
  surrogate). The pipeline only passes them normalized text, which never contains one, so nothing reaches them; the noise generator guards
  its own calls.
- **`CanonicalJson` writes a lone surrogate as U+FFFD**, so a transcript with one does not survive serialize, deserialize, serialize
  byte for byte. Recognizers return well-formed text, so O16 makes its transcripts well-formed first.
- **Pronunciation scoring grows roughly with the cube of the transcript length**: 60 ms at 200 characters, 0.4 s at 400, 2.7 s at 800, more
  than 20 s at 1 600 (`hola hola ...` with the fake phoneme recognizer; no phoneme recognizer, no cost). A spoken utterance is far
  below that. Stage 2 (O9, O14) should decide whether to cap the phone count; until then O16 pairs only transcripts of up to 150
  characters with the phoneme recognizer.
- **A numeral next to `un`/`una`/`uno` is one number when spoken** (`cuatrocientos una` is 401, `una mil` is 1000), and two numerals in
  a row recast as `un una`. These are properties of the language, so the sentence generator does not produce them (`skip:numeral-next-to-article`,
  `skip:adjacent-numerals`).
- `NpcDefinition` is a plain record over lists, so it has no value equality; O16 compares its JSON only.

## Extension points for stage 2

`PipelineHarness` already exposes what the remaining oracles need: `Bundle`, `NewServices` (scripted word recognizer, optional fake
phoneme recognizer, fake embedder, silent synthesizer), `NewPipeline`, `NewContext`, `Prompt`, `Clip`, `Words`, `Grammars`. Stage 2 adds
`AudioGen`, `PhoneGen` and `ContentMutationGen` to `Generation/`, new entries to `KnownTolerances` (the WAV reader and content loader
exceptions of plan section 6), the corresponding `*FuzzTests.cs` files, and the CI job.
