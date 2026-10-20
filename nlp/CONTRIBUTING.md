# Contributing

Thank you for helping with the voice recognition, decision and feedback system. This page is the working agreement: how to set
up, how to name and commit things, the determinism checklist every change must pass, and recipes for the four changes people
make most often (a new error tag, new content, a new golden clip, a new model adapter).

Read these first: [README.md](README.md) for what the project is and how to run it, [docs/PIPELINE.md](docs/PIPELINE.md) for what
happens to one utterance, [docs/adr/](docs/adr/) for why the rules exist, and `IMPLEMENTATION_PLAN.md` for the milestones.

- [Development setup](#development-setup)
- [Branches and commits](#branches-and-commits)
- [Code conventions](#code-conventions)
- [The determinism checklist](#the-determinism-checklist)
- [Definition of done](#definition-of-done)
- [Fuzz tests](#fuzz-tests)
- [Recipe: add an error tag](#recipe-add-an-error-tag)
- [Recipe: add a prompt, answer set or intent to content/v1](#recipe-add-a-prompt-answer-set-or-intent-to-contentv1)
- [Recipe: add a golden audio clip](#recipe-add-a-golden-audio-clip)
- [Recipe: add a D1 model adapter](#recipe-add-a-d1-model-adapter)
- [Gotchas](#gotchas)

## Development setup

1. Install the .NET SDK pinned in `global.json` (10.0.402; later patch releases are accepted). A user-local install is fine
   (see the README, "Prerequisites"). In every shell that builds the project, `DOTNET_ROOT` and `PATH` must point at it:

   ```bash
   export DOTNET_ROOT="$HOME/.dotnet"
   export PATH="$HOME/.dotnet:$PATH"
   ```

2. Build and test from the repository root:

   ```bash
   dotnet restore
   dotnet build -warnaserror
   dotnet test
   dotnet format --verify-no-changes
   ```

   `dotnet test tests/Vocaterra.Nlp.Nlp.Tests` runs one project; add `--filter "FullyQualifiedName~LinguisticAnalyzer"` for one class.
   `dotnet format` (without `--verify-no-changes`) fixes formatting.

3. Models are optional. Without them the real-model tests are skipped, not failed. To run them:

   ```bash
   scripts/fetch-models.sh vosk-model-small-es-0.42
   export VOCATERRA_MODELS_DIR="$PWD/models"
   dotnet test
   ```

4. Disk space: a full build writes about 3 GB per checkout (native speech packages). If you work in several worktrees, remove build
   output you no longer need with `git clean -fdX -- src tests tools` (it deletes only ignored files in those folders, such as `bin/` and `obj/`).

5. The editor settings are in `.editorconfig` (UTF-8, LF, final newline, 4 spaces in C#, 2 elsewhere). Only the packages in
   `Directory.Packages.props` may be used. If you need another package, open an issue or say so in the pull request instead of
   adding it: every package passes the license scan (`scripts/license-scan.sh [--offline]`), and the scan only means something if
   the list is curated.

## Branches and commits

- Work on a branch, never directly on `main`. A branch carries one milestone or one self-contained change. Maintainers merge
  to `main` once the whole solution builds and tests green.
- Start the commit subject with the milestone id and a colon, then a short summary in the imperative mood:
  `M13b: answer-match communication, spell grammars, blacksmith idle transitions`. Maintenance of the plan itself uses
  `Plan:`. Work that belongs to no milestone uses a short area prefix (for example `Docs:` or `Content:`).
- Add a blank line and a body when the why is not obvious. Say what changed in behaviour, not which files you touched.
- Commits written with an AI assistant end with the assistant's `Co-Authored-By:` line and a `Claude-Session:` line.
- One logical change per commit. Do not mix reformatting of untouched code with a feature.
- Milestone work touches its own `src/<Project>` and `tests/<Project>.Tests` folders, plus the extra paths the milestone names.
  Cross-cutting files (`Directory.Build.props`, `Directory.Packages.props`, the `.sln`, CI, `IMPLEMENTATION_PLAN.md`) and
  `src/Vocaterra.Nlp.Core` change only with the maintainers' agreement, because everything else compiles against them.
- Do not push to `main`. Do not commit models, recordings or replay logs from real people (all gitignored or local, see below).

## Code conventions

- File-scoped namespaces; nullable reference types on; warnings are errors; small immutable `record` types.
- Every public type has a one-line `/// <summary>`. Keep existing `AssemblyMarker.cs` files in place (a test refers to some of them).
- Add tests next to the code: one xUnit project per source project, plain `Assert`. Pure stages get a positive and a negative test and
  a "same input, byte-identical `CanonicalJson` output" test.
- Add no engine types, UI types or LLM packages to `src/`.

## The determinism checklist

Copy this into the description of every pull request and tick what applies. Each item comes from the determinism rules in
`IMPLEMENTATION_PLAN.md` section 3 and [ADR 0002](docs/adr/0002-determinism-rules.md); a "not applicable" is fine, an unchecked
item that does apply is not.

```markdown
### Determinism checklist
- [ ] No `System.Random`, `Random.Shared` or other unseeded randomness; random choices use `SeededRng.ForInteraction(worldSeed, npcId, counter)`.
- [ ] No `DateTime.Now`, `DateTime.UtcNow`, `Stopwatch`, `Environment.TickCount` or any clock in `src/`; time arrives as a `StoredTimestamp`. (Latency measurement is allowed only in `tools/Vocaterra.Nlp.Bench`.)
- [ ] Hashing uses `DeterministicHash` (SHA-256). No `string.GetHashCode()` or `HashCode` for anything persisted, logged or compared across runs.
- [ ] No output depends on `Dictionary` or `HashSet` enumeration order; collections are sorted with `StringComparer.Ordinal`; maps serialize with sorted keys (`CanonicalJson`, `SlotMap`).
- [ ] Culture-free: `CultureInfo.InvariantCulture` for formatting and parsing, `StringComparison.Ordinal` for comparison, Unicode NFC text; no culture-sensitive `ToLower()`, `ToUpper()`, `Compare` or `IndexOf(string)`. `InvariantGlobalization` stays off.
- [ ] Every threshold uses `Margin(threshold, epsilon)` and every caller handles Accept, Reject and Uncertain. Summation order is fixed; no parallel reduction in a grading path.
- [ ] Any model sits behind a `Vocaterra.Nlp.Core` interface; tests use a deterministic fake; real-model tests are skipped unless `VOCATERRA_MODELS_DIR` is set and the files verify; one thread, CPU provider, greedy decoding.
- [ ] No non-deterministic (N-class) code, and no LLM package, in `src/`. Build-time LLM work stays in `tools/Vocaterra.Nlp.ContentBuilder` (Freeze pattern) and every generated content file is marked and approved in `provenance.json`.
- [ ] Every model file has a SHA-256 in `models/manifest.json`; version stamps carry the content hash and the sorted model hashes; I recorded the license of every native binary I added, not only of its package.
- [ ] Build hygiene: nullable on, warnings as errors, `dotnet format --verify-no-changes` clean, a one-line `///` summary on every new public type.
- [ ] If I changed `content/v1`: I re-ran `hash`, updated the pinned hash in the tests and re-recorded the end-to-end golden (see the recipe below).
- [ ] If I changed any stage: the same inputs still give byte-identical output, and replay logs recorded before my change still replay (or I explain why they cannot).
```

## Definition of done

Before you ask for review, in this order, from a clean tree:

1. `dotnet build -warnaserror` is green for the whole solution.
2. `dotnet format` then `dotnet format --verify-no-changes` is clean.
3. `dotnet test` is green for the whole solution (model-gated tests may be skipped).
4. New public types have summaries; no rule in the checklist is broken; no package outside `Directory.Packages.props` is used.
5. Every command you wrote in a document was run and works. If you changed behaviour that [docs/PIPELINE.md](docs/PIPELINE.md),
   [docs/REPLAY.md](docs/REPLAY.md) or the README describe, update them with fresh output.

## Fuzz tests

`tests/Vocaterra.Nlp.Fuzzing.Tests` generates inputs from the project's own catalogs and checks sixteen oracles against the real stages. The design is
in [docs/FUZZING_PLAN.md](docs/FUZZING_PLAN.md); the oracles, the bugs found so far and the known findings are in
[tests/Vocaterra.Nlp.Fuzzing.Tests/README.md](tests/Vocaterra.Nlp.Fuzzing.Tests/README.md).

- Before you open a pull request that touches `Vocaterra.Nlp.Nlp`, `Vocaterra.Nlp.Evaluation`, `Vocaterra.Nlp.Response`, `Vocaterra.Nlp.Audio`, `Vocaterra.Nlp.Phonology`,
  `Vocaterra.Nlp.Pronunciation`, `Vocaterra.Nlp.Decision`, `Vocaterra.Nlp.Content` or `Vocaterra.Nlp.Pipeline`, run `dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests`.
- A bug the fuzzer finds is fixed in the owning module, with a regression test in that module's test project. List the bug with its
  CsCheck seed in the "Bugs found and fixed" section of the fuzz README.
- A documented exception may be waved through only in `tests/Vocaterra.Nlp.Fuzzing.Tests/Oracles/KnownTolerances.cs`, and it needs a reason.
- Never weaken an oracle to make a failure go away.

## Recipe: add an error tag

An error tag is a stable id (`conj.person`, `pron.r`, ...) that flows through the analyzer, the verdict weights, the feedback policy,
the dialogue and the learner model. Add it in this order:

1. **Taxonomy** (`src/Vocaterra.Nlp.Core/Taxonomy/ErrorTags.cs`). Add a constant, document it with a summary, and append it to the `AllTags`
   array. Append at the end: errors in a verdict are sorted by position and then by the tag's index in `ErrorTags.All`, so inserting
   in the middle changes the order of existing verdicts and breaks golden files. Tags are `family.name`; the family (the text before
   the first dot) is also the learner-model skill id, so `conj.person` and `conj.tense` train the same skill `conj`.
   Update `tests/Vocaterra.Nlp.Core.Tests/ErrorTagsTests.cs` (it pins the count and the order) and the taxonomy table in `HIGH_LEVEL_DOCS.md`.
   This is a change to `Vocaterra.Nlp.Core`: agree it with a maintainer first.
2. **Analyzer.** Produce the tag where the error is detected, with the span in the normalized text and the expected form:
   - grammar and vocabulary tags: `src/Vocaterra.Nlp.Nlp/Analysis/AgreementChecker.cs` (agreement rules) or `LinguisticAnalyzer.cs`; the
     expected form comes from `MorphologyGenerator`, never a hard-coded string;
   - pronunciation tags: a predicted variant in `src/Vocaterra.Nlp.Phonology/PredictedErrorVariants.cs` and its matching in
     `src/Vocaterra.Nlp.Pronunciation/PredictedErrorMatcher.cs`.

   Add one positive and one negative test (`tests/Vocaterra.Nlp.Nlp.Tests/Analysis`, `tests/Vocaterra.Nlp.Phonology.Tests`, `tests/Vocaterra.Nlp.Pronunciation.Tests`).
3. **Evaluation weights.** Add the tag to `errorWeights` for `beginner`, `intermediate` and `advanced` in
   `content/v1/evaluation/evaluation-config.json` (pronunciation tags are 0 today, so they do not reduce `correctness`), and to the
   built-in defaults in `src/Vocaterra.Nlp.Evaluation/EvaluationConfig.cs`. A grammar tag missing from the map costs `unknownTagWeight` (0.3);
   a missing `pron.*` tag costs 0. Extend `tests/Vocaterra.Nlp.Evaluation.Tests`.
4. **Feedback rule.** The default rules in `content/v1/feedback/feedback-policy.json` match every tag with `*`: a recast on the first
   offence, a hint on the second and third, an explicit correction from the fourth (`pron.*` gets a model pronunciation from the
   second). Add a rule with the exact tag (`"tagPattern": "ser_estar"`) only if the new tag should be treated differently. The most
   specific pattern wins (exact tag, then family wildcard, then `*`).
5. **Dialogue.** Add pools named `feedback.<kind>.<tag>` to `content/v1/dialogue/dialogue.json`, for the kinds `recast`, `hint`,
   `explicit_correction`, `model_pronunciation` and `slow_repeat` (for example `feedback.hint.gender.agree`). If a specific pool is
   missing the pipeline falls back to `feedback.<kind>`. Templates can use the slots of the verdict plus `{tag}`, `{expected}`,
   `{actual}` and `{recast}`, and the template functions of the template engine (`{item:plural-if(qty)}`,
   `{verb:conj(2,sg,pres)}`); a template that needs a slot the verdict may not have falls back to the generic pool, so prefer slots the
   matching grammars always provide. Run `cefr-check` for the words you added.
6. **Content hash.** Re-hash and update the pinned values (next recipe), then add or extend a pipeline test that shows the tag
   all the way from a transcript to the NPC's reply.

## Recipe: add a prompt, answer set or intent to content/v1

The bundle is a folder of JSON files under `content/v1`, listed with SHA-256 hashes in `content/v1/manifest.json`. The loader
refuses a bundle whose files differ from the manifest, and the bundle hash goes into every verdict, so every edit ends with a re-hash.

Where things go:

| You add | File | Notes |
| --- | --- | --- |
| An answer set | `answer-sets/<answerSetId>.json` | `{ "answerSetId", "answers": [{ "answerId", "canonical", "aliases": [], "dialects": [] }] }`. The file name is the id. Aliases carry synonyms and regional variants (`coche`, `carro`, `auto`); they also become the phrase list of constrained recognition. |
| An intent | `intents/intents.json` | `intentId`, `requiredSlots`, `slotGrammars` (the DSL: `[ART]`, `[NUM:qty]`, `[NOUN:item]`, `[VERB:lemma:slot]`, `?` for optional, `(a\|b)`), `keywordRules` (all lemmas must be present) and `examples` for the embedding rung. |
| A prompt | `prompts/prompts.json` | `promptId`, `mode` (`Constrained` or `Open`), `difficulty`, `answerSetId`, `allowedIntentIds` (empty means an answer-only prompt such as a quiz), `npcId` or `null`, `slotGrammarIds` (each must be a grammar defined in `intents.json`). |
| NPC behaviour | `npcs/<npcId>.json` and `dialogue/dialogue.json` | Transitions keyed on intent and error tags; `variantPools` must have matching dialogue pools. |
| Effects | `effects/effects.json` | Verdict to effect with a correctness curve (spell power, trade price). |
| Lexicon | `lexicon/*.json` | Every `*.json` file in the folder is merged (identical readings stored once, different readings of a form all kept; see [docs/LEXICON.md](docs/LEXICON.md)). `lexicon/*.source.json` are sidecars of generated files, not lexicons. A Wiktionary import is regenerated with `import-lexicon`, never edited by hand, and stays marked `wiktionary-import` with its sidecar. A word the content uses but the lexicon lacks goes into a hand-authored file; `LexiconCoverageTests` names the word and where it is used. |

Steps:

1. Edit or add the files. Keep text in NFC and write grammar only with the lemmas of the lexicon (a word the analyzer does not know
   becomes `vocab.unknown`).
2. Add an item for every **new** file to `content/v1/provenance.json` (`generatedBy` is `hand-authored`, `llm-draft` or
   `wiktionary-import`; a language-model draft is refused until `approvedBy` and `approvedOn` are set). If you import Wiktionary data,
   `ATTRIBUTION.md` must keep its CC BY-SA notice.
3. Re-hash. This rewrites `content/v1/manifest.json` from the files on disk and prints the new bundle hash:

   ```bash
   dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- hash content/v1
   dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- validate content/v1
   dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- cefr-check content/v1 --level a1
   ```

   Before `hash`, `validate` reports exactly what is out of date (`hash mismatch`, `file is not listed in manifest.json`,
   `no provenance item for ...`). `validate` also checks references: a prompt that names an unknown answer set, intent or grammar fails.
   `cefr-check` is advisory; it lists template lemmas that are not in `cefr/a1.txt` or `cefr/a2.txt`.
4. Try it: `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "..." --prompt <your-prompt> --content content/v1`.
5. Update what pins the hash. After any content edit these tests fail until you do:
   - in `tests/Vocaterra.Nlp.Content.Tests/ManifestTests.cs`, change `GoldenBundleHash` to the new bundle hash;
   - re-record the end-to-end golden file and check that only the three `contentHash` lines changed (unless your change was meant to
     alter the example):

     ```bash
     VOCATERRA_RECORD_GOLDEN=1 dotnet test tests/Vocaterra.Nlp.Golden.Tests
     ```
6. Run the whole suite. Replay logs recorded earlier report `/versions/contentHash` as different after a content change; that is expected.

## Recipe: add a golden audio clip

The golden set exists to answer one question with real data: does the recognizer keep the mistakes a learner makes? The full
protocol, consent text and manifest schema are in [golden/README.md](golden/README.md); the short version:

- **Real recordings stay on your machine.** `golden/audio/` is gitignored and nothing uploads recordings. Participation is opt-in
  and requires the consent sentence in `golden/README.md`; recordings without consent are deleted. Speakers are opaque labels (`s01`),
  never names. The committed `golden/manifest.jsonl` holds synthetic entries only: a test (`ManifestGoldenTests`) requires every committed
  entry to have the speaker `synth`.
- **To benchmark real clips**, record 16 kHz mono 16-bit WAV, one utterance per file, push-to-talk, unedited, named
  `<speaker>-<prompt>-<n>.wav` (for example `s01-offer-items-open-2.wav`). Keep them and a manifest outside the repository or in the
  gitignored folder, with one annotation line per clip. `expectedTranscript` is what was actually said, mistakes included:

  ```json
  {"clip":"audio/s01-offer-items-open-2.wav","promptId":"offer-items-open","speaker":"s01","l1":"en","expectedTranscript":"yo tiene dos espada","expectedErrors":[{"tag":"conj.person","actual":"tiene"},{"tag":"noun.number","actual":"espada"}],"expectedIntent":"offer_item"}
  ```

  Then run the benchmark with your own manifest (clips are looked up relative to the manifest, or in `--audio-dir`):

  ```bash
  dotnet run --project tools/Vocaterra.Nlp.Bench -- --manifest ~/vocaterra-golden/manifest.jsonl --content content/v1 --models models
  ```

  It prints WER, learner-error preservation (the share of annotated wrong words that survive in the transcript) and p50/p95 latency per model and mode.
- **To add a synthetic entry** to the committed manifest (for the tests, not for measuring a recognizer), add a line with
  `"speaker":"synth"` and the clip name `audio/synth-<promptId>-<n>.wav`. The golden tests generate a one-second tone for it under
  their own output folder, and `ThePipelineAgreesWithEveryAnnotation` fails unless the pipeline produces the annotated intent and
  errors for `expectedTranscript`. Use a `promptId` that exists in `content/v1/prompts/prompts.json`.
- **G2P golden lines** are separate: add `{"text": "...", "dialect": "la" | "es", "ipa": "..."}` to
  `tests/Vocaterra.Nlp.Golden.Tests/Data/g2p-golden.jsonl` (Latin American neutral or Castilian). The IPA is written by hand from a pronunciation
  source, not copied from the converter's own output; espeak-ng may be used as a development-time cross-check only.
- When a model changes, run the benchmark on the same clips before and after and put the numbers in the pull request.

## Recipe: add a D1 model adapter

A D1 model is a neural model with frozen weights. It enters the system only as an adapter behind a `Vocaterra.Nlp.Core` interface, pinned by
hash and gated in tests ([ADR 0004](docs/adr/0004-model-pinning-and-gating.md)).

1. **Interface.** Implement the interface for the role: `IWordRecognizer` (ASR), `IPhonemeRecognizer`, `IEmbeddingModel` or
   `ISpeechSynthesizer`. Configure it for determinism: one intra-op thread, CPU execution provider, greedy decoding or a fixed
   voice and speed, no sampling, no clock. Expose `ModelHash`. If the runtime has its own sources of variation, remove them as
   `VoskWordRecognizer` does for dithering, and write the finding down (`IMPLEMENTATION_PLAN.md` section 5b). Put the adapter in the
   project that owns the role (`Vocaterra.Nlp.Asr`, `Vocaterra.Nlp.Pronunciation`, `Vocaterra.Nlp.Nlp`, `Vocaterra.Nlp.Response`).
2. **Fake.** Add or extend a deterministic fake for the same interface (`ScriptedWordRecognizer`, `FakePhonemeRecognizer`,
   `FakeEmbeddingModel`, `SilentSynthesizer` are the patterns). Everything else in the test suite uses the fake.
3. **Manifest entry** in `models/manifest.json`: `id` (also the directory name under `VOCATERRA_MODELS_DIR`), `role`, `name`, `version`,
   `license`, `source`, an `archive` (`format` `zip` or `tar.bz2`, `sha256`, `bytes`) when it can be downloaded, the `files` the adapter loads
   (`path`, `sha256`, `bytes`) and `notes`. Fetch the model once, hash the files you load with `sha256sum`, and pin them; a `null` hash
   means "not pinned", and `ModelStore.ModelHash` refuses to produce a hash for it. Add a `FromStore(store, id)` factory that uses
   `ModelStore.ModelHash(id)`. Update `content/v1/ATTRIBUTION.md` (license table) and `models/README.md`.
4. **Licenses.** Record the license of the model, of the NuGet package and of every native binary and data file it brings. Check that
   it is in `scripts/license-allowlist.txt` terms and that no GPL component (for example espeak-ng) rides along unseen
   ([ADR 0007](docs/adr/0007-kokoro-phonemizer-licensing.md)); the license scan reads package metadata, not binaries. A new NuGet
   package needs the maintainers' approval.
5. **Pipeline wiring.** Add the model to `PipelineServices` (or reuse its slot) so its hash appears in `VersionStamp.ModelHashes`
   (`asr`, `phoneme`, `embedding`, `tts`), and to the CLI's `ModelSet` if it should be selectable with `--models`.
6. **Gating.** Real-model tests must be skipped, not failed, when the model is absent. Follow `ModelGate` and `VoskModelFactAttribute`
   in `tests/Vocaterra.Nlp.Pipeline.Tests/VoskPipelineTests.cs` (and `tests/Vocaterra.Nlp.Asr.Tests/TestSupport.cs`): skip unless `VOCATERRA_MODELS_DIR` is set and
   `ModelStore.Verify(id)` reports the files as ok.
7. **Golden test.** With the model present, a gated test must show: the same clip decoded twice (in one process, and ideally in two)
   gives identical output; the result equals the recorded expectation for a small committed clip or a synthetic signal; a record
   made with the real model replays without the model (`ReplayEnvelope`; see [docs/REPLAY.md](docs/REPLAY.md)); thresholds that use
   the model's scores have a `Margin`. Once real learner audio exists, add the model to the benchmark runs.
8. **CI.** Extend the `models` job in `.github/workflows-pending/ci.yml` so that it fetches the model (it runs only when the
   repository variable `VOCATERRA_RUN_MODEL_TESTS` is `true`).

## Gotchas

- The normalizer's `ConvertIndefiniteArticles` defaults to `true` (`una espada` becomes `1 espada`). The pipeline passes `false`; when you
  normalize text for agreement checks, `[ART]` grammar tokens or answer matching, pass `new NormalizationOptions(ConvertIndefiniteArticles: false)`.
- `System.IO.MatchType` collides with `Vocaterra.Nlp.Core.Stages.MatchType` under implicit usings: add
  `using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;`.
- Inside `Vocaterra.Nlp.Nlp.*` the namespaces `Vocaterra.Nlp.Nlp.Lexicon` and `Vocaterra.Nlp.Nlp.SlotGrammar` share names with the types `Lexicon` and `SlotGrammar`;
  put `using` lines after the file-scoped namespace line or use aliases.
- The G2P drops digits. Spell numbers out before phonemizing (`SpanishNumbers.DigitsToWords`).
- `CanonicalJson` rejects `null` for non-nullable members when reading (`RespectNullableAnnotations`); design JSON models with that in mind.
- A prompt whose `allowedIntentIds` is empty is answer-only: the pipeline skips intent resolution, and an exact, alias or fuzzy answer match
  counts as `communicated`.
- Tests that need the Vosk model look for it through `VOCATERRA_MODELS_DIR` and the committed manifest; if the files do not verify they are
  skipped, which looks like a pass. Check the skipped count when you expect them to run.
