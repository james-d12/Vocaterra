# Changing the core

The rules every change to the language core follows, and recipes for the four changes made most often: a new error tag, new
content, a new golden clip and a new model adapter. How to build and test is in [building-and-testing.md](building-and-testing.md);
what happens to one utterance is in [pipeline.md](pipeline.md), why the rules exist in [adr/](adr/), and the milestones in
[implementation-plan.md](implementation-plan.md).

- [Code conventions](#code-conventions)
- [Commits and shared files](#commits-and-shared-files)
- [The determinism checklist](#the-determinism-checklist)
- [Definition of done](#definition-of-done)
- [Recipe: add an error tag](#recipe-add-an-error-tag)
- [Recipe: add a prompt, answer set or intent to content/game-v1](#recipe-add-a-prompt-answer-set-or-intent-to-contentgame-v1)
- [Recipe: add a golden audio clip](#recipe-add-a-golden-audio-clip)
- [Recipe: add a D1 model adapter](#recipe-add-a-d1-model-adapter)
- [Gotchas](#gotchas)

## Code conventions

- File-scoped namespaces; nullable reference types on; warnings are errors; small immutable `record` types.
- Every public type has a one-line `/// <summary>`. Keep `src/Vocaterra.Nlp/AssemblyMarker.cs` in place (the one marker of the engine assembly; a test refers to it).
- Add tests next to the code: tests in `tests/Vocaterra.Nlp.Tests`, one folder per engine area, plain `Assert`; a test of Spanish behaviour (morphology, normalization, G2P, the seed
  lexicon, the Spanish pack) goes in `tests/Vocaterra.Nlp.Es.Tests`, next to the code in `src/Vocaterra.Nlp.Es`. Pure stages get a positive and a negative test and
  a "same input, byte-identical `CanonicalJson` output" test.
- Add no engine types, UI types or LLM packages to `src/`.
- The editor settings are in `.editorconfig` (UTF-8, LF, final newline, 4 spaces in C#, 2 elsewhere). Only the packages in `Directory.Packages.props` (at the repository root) may be used. If you need another package, open an issue or say so in the pull request instead of adding it: every package passes the license scan (`scripts/license-scan.sh [--offline]`), and the scan only means something if the list is curated.
- Documents: after you change a Markdown file anywhere in the repository, `dotnet test --filter MarkdownLinkTests` (in `tests/Vocaterra.Nlp.Tests`) must pass: it reports each broken relative link.

## Commits and shared files

- Start the commit subject with the milestone id and a colon, then a short summary in the imperative mood:
  `M13b: answer-match communication, spell grammars, idle transitions`. Maintenance of the plan itself uses
  `Plan:`. Work that belongs to no milestone uses a short area prefix (for example `Docs:` or `Content:`).
- Add a blank line and a body when the why is not obvious. Say what changed in behaviour, not which files you touched.
- One logical change per commit. Do not mix reformatting of untouched code with a feature.
- Milestone work touches its own `src/<Project>` and `tests/<Project>.Tests` folders, plus the extra paths the milestone names.
  Cross-cutting files (`Directory.Build.props`, `Directory.Packages.props`, the `.sln`, CI, `implementation-plan.md`) and
  `src/Vocaterra.Nlp/Core` change only with the maintainers' agreement, because everything else in the engine compiles against them.
- Do not commit models, recordings or replay logs from real people (they are gitignored or kept local; see the golden-clip recipe).

## The determinism checklist

Copy this into the description of every pull request and tick what applies. Each item comes from the determinism rules in
`implementation-plan.md` section 3 and [ADR 0002](adr/0002-determinism-rules.md); a "not applicable" is fine, an unchecked
item that does apply is not.

```markdown
### Determinism checklist
- [ ] No `System.Random`, `Random.Shared` or other unseeded randomness; random choices use `SeededRng.ForInteraction(worldSeed, agentId, counter)`.
- [ ] No `DateTime.Now`, `DateTime.UtcNow`, `Stopwatch`, `Environment.TickCount` or any clock in `src/`; time arrives as a `StoredTimestamp`. (Latency measurement is allowed only in `tools/Vocaterra.Nlp.Bench`.)
- [ ] Hashing uses `DeterministicHash` (SHA-256). No `string.GetHashCode()` or `HashCode` for anything persisted, logged or compared across runs.
- [ ] No output depends on `Dictionary` or `HashSet` enumeration order; collections are sorted with `StringComparer.Ordinal`; maps serialize with sorted keys (`CanonicalJson`, `SlotMap`).
- [ ] Culture-free: `CultureInfo.InvariantCulture` for formatting and parsing, `StringComparison.Ordinal` for comparison, Unicode NFC text; no culture-sensitive `ToLower()`, `ToUpper()`, `Compare` or `IndexOf(string)`. `InvariantGlobalization` stays off.
- [ ] Every threshold uses `Margin(threshold, epsilon)` and every caller handles Accept, Reject and Uncertain. Summation order is fixed; no parallel reduction in a grading path.
- [ ] Any model sits behind a `Vocaterra.Nlp.Core` interface; tests use a deterministic fake; real-model tests are skipped unless `VOCATERRA_MODELS_DIR` is set and the files verify; one thread, CPU provider, greedy decoding.
- [ ] No non-deterministic (N-class) code, and no LLM package, in `src/`. Build-time LLM work stays in `tools/Vocaterra.Nlp.ContentBuilder` (Freeze pattern) and every generated content file is marked and approved in `provenance.json`.
- [ ] Every model file has a SHA-256 in `models/manifest.json`; version stamps carry the content hash and the sorted model hashes; I recorded the license of every native binary I added, not only of its package.
- [ ] Build hygiene: nullable on, warnings as errors, `dotnet format --verify-no-changes` clean, a one-line `///` summary on every new public type.
- [ ] If I changed `content/game-v1`: I re-ran `hash`, ran the whole suite (the core's tests run on this bundle), re-recorded the end-to-end goldens if my change moved them, and updated the bundle hash in `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v8.json` (see the recipe below).
- [ ] If I changed any stage: the same inputs still give byte-identical output, and replay logs recorded before my change still replay (or I explain why they cannot).
```

## Definition of done

Before you ask for review, in this order, from a clean tree:

1. `dotnet build -warnaserror` is green for the whole solution.
2. `dotnet format` then `dotnet format --verify-no-changes` is clean.
3. `dotnet test` is green for the whole solution (model-gated tests may be skipped).
4. New public types have summaries; no rule in the checklist is broken; no package outside `Directory.Packages.props` is used.
5. Every command you wrote in a document was run and works. If you changed behaviour that [docs/nlp/pipeline.md](pipeline.md),
   [docs/nlp/replay.md](replay.md) or the README describe, update them with fresh output.

## Recipe: add an error tag

An error tag is a stable id (`conj.person`, `pron.r`, ...) that flows through the analyzer, the verdict weights, the feedback policy,
the dialogue and the learner model. Add it in this order:

1. **Taxonomy** (`src/Vocaterra.Nlp/Core/Taxonomy/ErrorTags.cs`). Add a constant, document it with a summary, and append it to the `AllTags`
   array. Append at the end: errors in a verdict are sorted by position and then by the tag's index in `ErrorTags.All`, so inserting
   in the middle changes the order of existing verdicts and breaks golden files. Tags are `family.name`; the family (the text before
   the first dot) is also the learner-model skill id, so `conj.person` and `conj.tense` train the same skill `conj`.
   Update `tests/Vocaterra.Nlp.Tests/Core/ErrorTagsTests.cs` (it pins the count and the order). The taxonomy table of the original design note is archived
   (`docs/archive/nlp/history/high-level-docs.md`) and is no longer updated.
   This is a change to the engine's `Core` folder: agree it with a maintainer first.
2. **Analyzer.** Produce the tag where the error is detected, with the span in the normalized text and the expected form:
   - grammar and vocabulary tags: `src/Vocaterra.Nlp/Language/Analysis/AgreementChecker.cs` (agreement rules) or `LinguisticAnalyzer.cs`; the
     expected form comes from the pack's morphology (Spanish: `MorphologyGenerator` in `src/Vocaterra.Nlp.Es/Morphology`), never a hard-coded string; the Spanish agreement
     and analysis rules are in `src/Vocaterra.Nlp.Es/Analysis`;
   - pronunciation tags: a predicted variant in `src/Vocaterra.Nlp/Phonology/PredictedErrorVariants.cs` and its matching in
     `src/Vocaterra.Nlp/Pronunciation/PredictedErrorMatcher.cs`.

   Add one positive and one negative test (`tests/Vocaterra.Nlp.Es.Tests/Analysis`, `tests/Vocaterra.Nlp.Es.Tests/Phonology`, `tests/Vocaterra.Nlp.Tests/Pronunciation`).
3. **Evaluation weights.** Add the tag to `errorWeights` for `beginner`, `intermediate` and `advanced` in
   `content/game-v1/evaluation/evaluation-config.json` (pronunciation tags are 0 today, so they do not reduce `correctness`), and to the
   built-in defaults in `src/Vocaterra.Nlp/Evaluation/EvaluationConfig.cs`. A grammar tag missing from the map costs `unknownTagWeight` (0.3);
   a missing `pron.*` tag costs 0. Extend `tests/Vocaterra.Nlp.Tests/Evaluation`.
4. **Feedback rule.** The default rules in `content/game-v1/feedback/feedback-policy.json` match every tag with `*`: a recast on the first
   offence, a hint on the second and third, an explicit correction from the fourth (`pron.*` gets a model pronunciation from the
   second). Add a rule with the exact tag (`"tagPattern": "ser_estar"`) only if the new tag should be treated differently. The most
   specific pattern wins (exact tag, then family wildcard, then `*`).
5. **Dialogue.** Add pools named `feedback.<kind>.<tag>` to `content/game-v1/dialogue/dialogue.json`, for the kinds `recast`, `hint`,
   `explicit_correction`, `model_pronunciation` and `slow_repeat` (for example `feedback.hint.gender.agree`). If a specific pool is
   missing the pipeline falls back to `feedback.<kind>`. Templates can use the slots of the verdict plus `{tag}`, `{expected}`,
   `{actual}` and `{recast}`, and the template functions of the template engine (`{item:plural-if(qty)}`,
   `{verb:conj(2,sg,pres)}`); a template that needs a slot the verdict may not have falls back to the generic pool, so prefer slots the
   matching grammars always provide. Run `cefr-check` for the words you added.
6. **Content hash.** Re-hash (next recipe), then add or extend a pipeline test that shows the tag
   all the way from a transcript to the agent's reply.

## Recipe: add a prompt, answer set or intent to content/game-v1

There is one bundle in the repository (R02): `content/game-v1`, the game's. The core's demo CLI, its tests and its fuzz suite run on it, so a core change that
needs content edits this bundle, and a game edit that breaks something the core relies on fails a core test; that is the feedback this arrangement is for. The
game's own rules for editing it (the phrases of `content/PHRASES.md` are the source of truth for the Spanish the learner is taught, `scripts/content.sh` is the
tool, and the gate runs `lint --strict --level a1 --host-pools 'guide.*'`) are in [docs/game/content.md](../game/content.md); this recipe is the core's side of the
same workflow.

The game's content loop, `scripts/content-loop.sh` (`content/README.md`, "The edit loop"), runs the lint, hash, validate, grammar and
fixture steps below in one command; the steps are listed here so a core change knows what each one checks.

The bundle is a folder of JSON files under `content/game-v1`, listed with SHA-256 hashes in `content/game-v1/manifest.json`. The loader
refuses a bundle whose files differ from the manifest, and the bundle hash goes into every verdict, so every edit ends with a re-hash. The game's
host-owned files (`game/*.json`, `speech/*.wav`) are hashed and listed in `provenance.json` like the rest and are ignored by the loader.

Where things go:

| You add | File | Notes |
| --- | --- | --- |
| An answer set | `answer-sets/<answerSetId>.json` | `{ "answerSetId", "answers": [{ "answerId", "canonical", "aliases": [], "dialects": [] }] }`. The file name is the id. Aliases carry synonyms and regional variants (`coche`, `carro`, `auto`); they also become the phrase list of constrained recognition. |
| An intent | `intents/intents.json` | `intentId`, `requiredSlots`, `slotGrammars` (the DSL: `[ART]`, `[NUM:qty]`, `[NOUN:item]`, `[VERB:lemma:slot]`, `?` for optional, `(a\|b)`, and since M74 `(a\|b c):slot`, which fills the slot with the words of the alternative that matched), `keywordRules` (all lemmas must be present), `examples` for the embedding rung (each a string, or an object `{ "text": "yo tienes una fruta", "errors": ["conj.person"] }` that the lint grades; see [Lint your content before you hash it](#lint-your-content-before-you-hash-it)) and optionally `lint` (waivers). An intent needs at least one of `slotGrammars`, `keywordRules` or `examples` (M82: the loader refuses a bare intent, since nothing can produce it). |
| A prompt | `prompts/prompts.json` | `promptId`, `mode` (`Constrained` or `Open`), `difficulty`, `answerSetId`, `allowedIntentIds` (empty means an answer-only prompt such as a quiz), `agentId` or `null`, `slotGrammarIds` (each must be a grammar defined in `intents.json`), `text` (optional: the line the agent says before the player answers; not empty, no leading or trailing whitespace). |
| agent behaviour | `agents/<agentId>.json` and `dialogue/dialogue.json` | Transitions keyed on intent and error tags; `variantPools` must have matching dialogue pools. |
| Effects | `effects/effects.json` | Verdict to effect with a correctness curve (spell power, trade price). |
| Lexicon | `lexicon/*.json` | Every `*.json` file in the folder is merged (identical readings stored once, different readings of a form all kept; see [docs/nlp/lexicon.md](lexicon.md)). `lexicon/*.source.json` are sidecars of generated files, not lexicons. A Wiktionary import is regenerated with `import-lexicon`, never edited by hand, and stays marked `wiktionary-import` with its sidecar. A word the content uses but the lexicon lacks goes into a hand-authored file; `LexiconCoverageTests` names the word and where it is used. |

Steps:

1. Edit or add the files. Keep text in NFC and write grammar only with the lemmas of the lexicon (a word the analyzer does not know
   becomes `vocab.unknown`).
2. Add an item for every **new** file to `content/game-v1/provenance.json` (`generatedBy` is `hand-authored`, `llm-draft`,
   `wiktionary-import` or `model-output`; a language-model draft is refused until `approvedBy` and `approvedOn` are set). `model-output`
   (M29g) is for host-owned files a model rendered, such as pre-rendered speech: it needs a `model`, may carry `voice`, `speaker` and
   `source`, and is refused for any file the loader parses. If you import Wiktionary data, `ATTRIBUTION.md` must keep its CC BY-SA notice.
3. Lint, then re-hash. `lint` does not need current hashes, so run it first and fix what it reports (next section). Re-hashing rewrites
   `content/game-v1/manifest.json` from the files on disk and prints the new bundle hash:

   ```bash
   scripts/content.sh lint content/game-v1 --strict --level a1 --host-pools 'guide.*'
   scripts/content.sh hash content/game-v1 --bundle-id game-v1
   scripts/content.sh validate
   scripts/content.sh cefr-check content/game-v1 --level a1
   ```

   (`scripts/content.sh` runs `tools/Vocaterra.Nlp.ContentBuilder` from source; `dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- <command>` is the same.)

   `hash` keeps the manifest's `bundleId`, `contentVersion`, `language` and `supportLanguage` when it rewrites the file. The game's bundle has no
   `language` (it means Spanish) and must keep none: a `language` field is folded into the bundle hash (ADR 0011).

   Before `hash`, `validate` reports exactly what is out of date (`hash mismatch`, `file is not listed in manifest.json`,
   `no provenance item for ...`). `validate` also checks references: a prompt that names an unknown answer set, intent or grammar fails.
   `cefr-check` is advisory; it lists the lemmas of dialogue templates and of prompt text (`text`) that are not in `cefr/a1.txt` or
   `cefr/a2.txt`. Prompt findings are printed in their own section and name the prompt id.
4. Try it: `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "..." --prompt <your-prompt> --content content/game-v1`.
   To keep lines graded, add a row to the topic class of `tests/Vocaterra.Nlp.Tests/Pipeline/Lines/` (`Line*Tests`: prompt, line, intent, communicated, error tags with
   their words), or, for a greeting or the listener's name, to `tests/Vocaterra.Host.Tests/LineHostPathTests.cs` (the game's path); `dotnet test` runs them.
5. Run the whole suite. No test of the core pins the bundle hash (each compares the manifest with the hash computed from the files), so a plain
   edit followed by `hash` needs no literal changed; but the core's tests do read the content, and they tell you what the edit broke:
   - `GameBundleContractTests` (`tests/Vocaterra.Nlp.Tests/Content`) states what the core relies on: every prompt resolves its answer set, agent and intents, every
     example resolves to its intent, every agent state machine is closed over the bundle's intents, the dialogue has the pools the planner and the agents name,
     the CEFR lists and the lexicon load. A failure there is a defect in your edit, or a rule the core should change (then it is a core milestone).
   - The fuzz suite (`tests/Vocaterra.Nlp.Fuzzing.Tests`) draws its agents, prompts, grammars and mutation targets from the bundle, so a new kind of grammar or
     dialogue reaches it without a test edit; a failure there is a finding to fix at its source (see its README).
   - The two end-to-end goldens (`tests/Vocaterra.Nlp.Golden.Tests/Data/e2e-*.golden.json`) hold the core's whole result for one villager turn and one
     shopkeeper turn. If your edit moves one (a dialogue line, a grammar, an agent transition), re-record both in the same change and check that the diff shows
     only what you meant to change (the bundle hash is a placeholder in the files, so it never shows):

     ```bash
     VOCATERRA_RECORD_GOLDEN=1 dotnet test tests/Vocaterra.Nlp.Golden.Tests
     ```

     A golden of another language (a bundle whose manifest has `"language"`) is read and recorded under `Data/<lang>/` (`GoldenFiles.cs`, M35); the Spanish
     goldens keep their paths.
   - `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v8.json` is a saved game and carries the bundle hash: after `hash`, put the new `bundleHash` of
     `content/game-v1/manifest.json` into it.
   - The grammar cache (`content/grammar/game-v1-*`, next section) when you added or changed Spanish text.

   Replay logs recorded earlier report `/versions/contentHash` as different after a content change; that is expected.

### Lint your content before you hash it

`vocaterra-content lint <bundleDir>` runs the real analyzer, intent resolver and grader over the bundle's own examples and reports mistakes
that `validate` cannot see (`validate` checks references only). Run it after every content edit and before `hash`; it does not verify hashes.
Each finding is one line, `severity check path [ids] message`, and the last line is a summary. Errors are defects (exit 1), warnings are
hazards (exit 1 only with `--strict`), info lines are facts. `validate` runs the lint too and prints its errors and warnings, but fails on them
only with `--strict`. Options: `--level a1|a2` (CEFR check of dialogue, prompt text and answer-set canonicals), `--fail-on-level` (unlisted
lemmas become errors), `--json`, `--prompts a,b` (measure reachability from those prompts only) and `--host-pools p*,q` (pools the host voices
itself). The checks, the waivers and the exit codes are described in [tools.md](tools.md#content-lint).

What to do with a finding:

- `coverage.uncovered` (error): the example resolves to nothing. Add a grammar (the finding prints a skeleton as a hint) or fix the example.
- `coverage.rule-only` (warning): only a keyword rule covers the example. Add a grammar, or waive it with `"lint": { "allowRuleOnly": ["..."] }`.
- `confusion.prompt` (warning or error) and `confusion.catalog` (info): an example of one intent that another intent's grammar also matches.
  Narrow the grammar that is too broad. If the overlap is intended and the resolver's tie-break decides it, waive it on either intent with
  `"lint": { "allowOverlap": ["other_intent"] }`; the finding stays in the report, marked `waived`.
- `hazard.*`: an optional `[WORD]` beside an optional element (the name can swallow a word meant for something else); a grammar word or
  `[VERB:lemma]` the lexicon lacks (a correct sentence would get `vocab.unknown`; add the word to a hand-authored lexicon file); a verb whose
  forms are also nouns (info).
- `reach.*`: a pool, intent, answer set, agent or agent state that nothing reaches. Delete it or wire it in.
- `cefr.*`: a lemma above the level you asked for.
- `expect.*`: an example with expected error tags whose verdict differs. Write one learner-error example per new intent as
  `{ "text": "yo tienes una fruta", "errors": ["conj.person"] }`; the lint grades it against the first open-mode prompt that allows the intent (or the
  prompt named in `"prompt"`), and the tags must match exactly, so the example stays a regression test.

Editing examples or adding `lint` waivers changes `intents/intents.json`, so re-hash afterwards like any content edit.

### Check the grammar of your Spanish before you hash it

The content procedure is: lint, `cefr-check`, **grammar**, then the Opus pass (D-game-12), then `hash`. The grammar step sends every Spanish
string of the bundle to a local LanguageTool server (plain `es`, the server is never the public API, D10) and reports what it flags. It
removes the mechanical errors (agreement, spelling) before a reviewer reads the text.

The scripts and the tool run from the repository root:

```bash
scripts/fetch-tools.sh                     # once: the pinned Java runtime and LanguageTool, in ~/.local/share/vocaterra/tools
scripts/languagetool.sh start              # the server, capped (2 GB, 200 % CPU); scripts/languagetool.sh stop when done
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only   # online: adds to the cache
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --offline --strict   # offline: the committed cache (the gate)
```

- The strings are the dialogue lines (slot templates rendered from four sampled fillings, with the core's own template engine), prompts,
  answer-set canonicals and aliases, and intent examples. `--strings` adds a host's JSONL strings (format in [docs/nlp/api.md](api.md#grammar-check-the-strings-m30)).
  Learner-side strings (answers, examples) are capitalised before the check, so the sentence-start rule does not fire on every answer.
- Results are cached in `content/grammar/<name>-cache.json`, keyed by the SHA-256 of the LanguageTool version, the settings and the text.
  Commit the cache with the content it checks. An online run that finds a string not in the cache asks the server; an offline run
  (`--offline`, what the gate uses) never does and fails on a missing entry, so after a content edit run the online command once.
  An online run keeps the cache's other entries, so a `--strings` file's entries survive a run without it; `--prune` drops the entries
  that run did not use (run it with the same `--strings` as the offline checks).
- Waivers are in `content/grammar/<name>-waivers.json`: each names the rule and the string (`id`, or the exact `text`) and must give a
  `reason`. A waiver that matches nothing is a warning (`grammar.waiver-unused`).
- Findings print like lint's. `grammar.<RULE>` is a warning for agreement, spelling and grammar, and info for style and typography.
  Learner examples are an oracle: `oracle.flagged-and-tagged` and `oracle.tagged-but-silent` are info (a silent server is a blind spot, not
  a failure), and `oracle.untagged-but-flagged` is a warning: either the example is an error you did not tag (add `"errors"`, or fix it), or the
  server is wrong (waive it with a reason).
- Exit codes: 0 pass, 1 errors (warnings too with `--strict`), 2 usage, 3 no server answered in online mode (skipped, not passed).

The grammar check is required by the merge gate (offline, `--strict`, from the committed cache), and `GrammarTests.TheCommittedCacheReplaysTheBundleOfflineWithItsWaivers`
runs the same command in the test suite. A clean run is not approval: a native speaker still reviews the text (`PHRASES.md`'s `reviewer` column).

## Recipe: add a golden audio clip

The golden set exists to answer one question with real data: does the recognizer keep the mistakes a learner makes? The full
protocol, consent text and manifest schema are in [tests/golden/README.md](../../tests/golden/README.md); the short version:

- **Real recordings stay on your machine.** `tests/golden/audio/` is gitignored and nothing uploads recordings. Participation is opt-in
  and requires the consent sentence in `tests/golden/README.md`; recordings without consent are deleted. Speakers are opaque labels (`s01`),
  never names. The committed `tests/golden/manifest.jsonl` holds synthetic entries only: a test (`ManifestGoldenTests`) requires every committed
  entry to have the speaker `synth`.
- **To benchmark real clips**, record 16 kHz mono 16-bit WAV, one utterance per file, push-to-talk, unedited, named
  `<speaker>-<prompt>-<n>.wav` (for example `s01-villager-open-2.wav`). Keep them and a manifest outside the repository or in the
  gitignored folder, with one annotation line per clip. `expectedTranscript` is what was actually said, mistakes included:

  ```json
  {"clip":"audio/s01-villager-open-2.wav","promptId":"villager-open","speaker":"s01","l1":"en","expectedTranscript":"yo quiere dos espada","expectedErrors":[{"tag":"conj.person","actual":"quiere"},{"tag":"noun.number","actual":"espada"}],"expectedIntent":"request_item"}
  ```

  Then run the benchmark with your own manifest (clips are looked up relative to the manifest, or in `--audio-dir`):

  ```bash
  dotnet run --project tools/Vocaterra.Nlp.Bench -- --manifest ~/vocaterra-golden/manifest.jsonl --content content/game-v1 --models models
  ```

  It prints WER, learner-error preservation (the share of annotated wrong words that survive in the transcript) and p50/p95 latency per model and mode.
- **To add a synthetic entry** to the committed manifest (for the tests, not for measuring a recognizer), add a line with
  `"speaker":"synth"` and the clip name `audio/synth-<promptId>-<n>.wav`. The golden tests generate a one-second tone for it under
  their own output folder, and `ThePipelineAgreesWithEveryAnnotation` fails unless the pipeline produces the annotated intent and
  errors for `expectedTranscript`. Use a `promptId` that exists in `content/game-v1/prompts/prompts.json`.
- **G2P golden lines** are separate: add `{"text": "...", "dialect": "la" | "es", "ipa": "..."}` to
  `tests/Vocaterra.Nlp.Es.Tests/Data/g2p-golden.jsonl` (Latin American neutral or Castilian; it moved there in M53 with the Spanish G2P). The IPA is written by hand from a pronunciation
  source, not copied from the converter's own output; espeak-ng may be used as a development-time cross-check only.
- When a model changes, run the benchmark on the same clips before and after and put the numbers in the pull request.

## Recipe: add a D1 model adapter

A D1 model is a neural model with frozen weights. It enters the system only as an adapter behind a `Vocaterra.Nlp.Core` interface, pinned by
hash and gated in tests ([ADR 0004](adr/0004-model-pinning-and-gating.md)).

1. **Interface.** Implement the interface for the role: `IWordRecognizer` (ASR), `IPhonemeRecognizer`, `IEmbeddingModel` or
   `ISpeechSynthesizer`. Configure it for determinism: one intra-op thread, CPU execution provider, greedy decoding or a fixed
   voice and speed, no sampling, no clock. Expose `ModelHash`. If the runtime has its own sources of variation, remove them as
   `VoskWordRecognizer` does for dithering, and write the finding down (`implementation-plan.md` section 5b). Put the adapter in the
   folder that owns the role (`Asr`, `Pronunciation`, `Language`, `Response`).
2. **Fake.** Add or extend a deterministic fake for the same interface (`ScriptedWordRecognizer`, `FakePhonemeRecognizer`,
   `FakeEmbeddingModel`, `SilentSynthesizer` are the patterns). Everything else in the test suite uses the fake.
3. **Manifest entry** in `models/manifest.json`: `id` (also the directory name under `VOCATERRA_MODELS_DIR`), `role`, `name`, `version`,
   `license`, `source`, an `archive` (`format` `zip` or `tar.bz2`, `sha256`, `bytes`) when it can be downloaded, the `files` the adapter loads
   (`path`, `sha256`, `bytes`) and `notes`. Fetch the model once, hash the files you load with `sha256sum`, and pin them; a `null` hash
   means "not pinned", and `ModelStore.ModelHash` refuses to produce a hash for it. Add a `FromStore(store, id)` factory that uses
   `ModelStore.ModelHash(id)`. Update `content/game-v1/ATTRIBUTION.md` (license table) and `models/README.md`.
4. **Licenses.** Record the license of the model, of the NuGet package and of every native binary and data file it brings. Check that
   it is in `scripts/license-allowlist.txt` terms and that no GPL component (for example espeak-ng) rides along unseen
   ([ADR 0007](adr/0007-kokoro-phonemizer-licensing.md)); the license scan reads package metadata, not binaries. A new NuGet
   package needs the maintainers' approval.
5. **Pipeline wiring.** Add the model to `PipelineServices` (or reuse its slot) so its hash appears in `VersionStamp.ModelHashes`
   (`asr`, `phoneme`, `embedding`, `tts`), and to the CLI's `ModelSet` if it should be selectable with `--models`.
6. **Gating.** Real-model tests must be skipped, not failed, when the model is absent. Follow `ModelGate` and `VoskModelFactAttribute`
   in `tests/Vocaterra.Nlp.Tests/Pipeline/VoskPipelineTests.cs` (and `tests/Vocaterra.Nlp.Tests/Asr/TestSupport.cs`): skip unless `VOCATERRA_MODELS_DIR` is set and
   `ModelStore.Verify(id)` reports the files as ok.
7. **Golden test.** With the model present, a gated test must show: the same clip decoded twice (in one process, and ideally in two)
   gives identical output; the result equals the recorded expectation for a small committed clip or a synthetic signal; a record
   made with the real model replays without the model (`ReplayEnvelope`; see [docs/nlp/replay.md](replay.md)); thresholds that use
   the model's scores have a `Margin`. Once real learner audio exists, add the model to the benchmark runs.
8. **CI.** Add a `models` job to `../../.github/workflows/ci.yml` that fetches the model and runs the model-gated tests (only when the
   repository variable `VOCATERRA_RUN_MODEL_TESTS` is `true`).

## Gotchas

- The normalizer's `ConvertIndefiniteArticles` defaults to `true` (`una espada` becomes `1 espada`). The pipeline passes `false`; when you
  normalize text for agreement checks, `[ART]` grammar tokens or answer matching, pass `new NormalizationOptions(ConvertIndefiniteArticles: false)`.
- `System.IO.MatchType` collides with `Vocaterra.Nlp.Core.Stages.MatchType` under implicit usings: add
  `using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;`.
- Inside `Vocaterra.Nlp.Language.*` the namespaces `Vocaterra.Nlp.Language.Lexicon` and `Vocaterra.Nlp.Language.SlotGrammar` share names with the types `Lexicon` and `SlotGrammar`;
  put `using` lines after the file-scoped namespace line or use aliases.
- The G2P drops digits. Spell numbers out before phonemizing (`bundle.Language.Numbers.DigitsToWords`; the Spanish static is
  `SpanishNumbers.DigitsToWords` in `Vocaterra.Nlp.Es.Normalization`).
- Do not name a Spanish type in the engine (`src/Vocaterra.Nlp`): the Spanish pack is the project `src/Vocaterra.Nlp.Es` (M53, ADR 0012), the engine cannot reference it, and
  `SpanishCallSiteTests` (empty allowlist) and `LayeringTests` (no engine namespace depends on `Vocaterra.Nlp.Es`) enforce it. A host or a tool that loads a Spanish bundle
  references the Es project and uses `SpanishContent.Load` or `LanguagePacks.Default.WithSpanish()`. Text-side Spanish goes through `bundle.Language` (ADR 0011, amendment M33), and the
  analysis side (morphology, word classes, agreement checks, template functions, the lexicon fold and profile, error tag ranks) through
  `bundle.LanguageMorphology`, `bundle.AnalysisRules`, `bundle.TemplateEngine` and the pack (amendment M34). A new use of `SpanishNumbers`,
  `SpanishPhoneticKey`, `SpanishG2p`, `SpanishTextNormalizer`, `PunctuationFixer`, `WordClasses`, `MorphologyGenerator` or the
  other names in `tests/Vocaterra.Nlp.Tests/Architecture/SpanishCallSiteTests.cs` in the engine source fails the guard. The allowlist is empty; an entry needs a reason a reviewer accepts. A new lexicon feature value goes into the pack's `LexiconProfile`, not into
  `LexiconSchema`.
- `CanonicalJson` rejects `null` for non-nullable members when reading (`RespectNullableAnnotations`); design JSON models with that in mind.
- A prompt whose `allowedIntentIds` is empty is answer-only: the pipeline skips intent resolution, and an exact, alias or fuzzy answer match
  counts as `communicated`.
- Tests that need the Vosk model look for it through `VOCATERRA_MODELS_DIR` and the committed manifest; if the files do not verify they are
  skipped, which looks like a pass. Check the skipped count when you expect them to run.
