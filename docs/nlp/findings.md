# The core's findings log

Section 5b of the [core implementation plan](implementation-plan.md) (decisions made during implementation), in its own file since
2026-11-09 so that the plan stays short enough to read at the start of a session. A reference elsewhere to "§5b" or "section 5b" means
this file. What is still open is collected in [open-items](../open-items.md).

- **R01 (2026-10-23, repository-wide; tracked in `docs/game/implementation-plan.md` section 4.6):** the core's projects moved to the root
  `src/`, `tests/`, `tools/`; `content/v1`, `tests/golden`, `models/`, `docs/nlp/`; `nlp/Vocaterra.Nlp.sln` replaced by the filter
  `Vocaterra.Nlp.slnf` (licence scan, core-only build); one root `Directory.Build.props` keys the core's packing on project names
  `Vocaterra.Nlp.*`. Bundle hash `1c2240c7...` and all goldens unchanged. Next, R02 moves the core's tests onto `content/game-v1` and
  retires `content/v1` (supersedes D7 when done).
- **R02 (2026-10-23, done):** the core's tests, fuzz suite, demo and embedded seed lexicon run on `content/game-v1`; `content/v1` deleted; D7 superseded. Fuzz found and fixed a duplicate `conj.tense` in `AgreementChecker` (ir a + past marker). Details in `docs/game/implementation-plan.md` section 4.6.
- **`la agua` (2026-11-08, issue #42, the owner):** the agreement check took a feminine article before a stressed-a noun as agreeing (`la agua`, `una arma` were
  clean by a test), so the game's hand-over row for León's water could not name the commonest slip. `SpanishAgreementChecker.CheckDeterminer` now gives
  `gender.agree` `el`/`un` for a feminine singular article or the numeral one directly before such a noun; `la fría agua`, `esta agua` and `las aguas` stay clean
  (`docs/nlp/api.md`, "A feminine noun that takes `el` takes it right before it").

- **Vosk dither (M9):** stock Vosk models apply time-seeded dither, so identical clips decoded
  differently run to run. `VoskWordRecognizer` loads a temp copy of the model with
  `--dither=0.0` in `conf/mfcc.conf`; results are now identical across runs and processes.
- **Invariant globalization (M0/M1):** off; see §3.5.
- **Normalizer articles (M4):** `NormalizationOptions.ConvertIndefiniteArticles` defaults to true;
  the pipeline must pass `false` so article–noun agreement and `[ART]` grammar tokens work.
- **Answer-only prompts (M13b):** an Exact/Alias/Fuzzy answer match counts as `communicated`
  even when no intent resolves (`Intent = unknown`, `ResolvedBy` = match type). A prompt whose
  `AllowedIntentIds` is empty skips the intent stage entirely (it is an answer-only prompt); the
  earlier resolver rule "empty allows every intent" no longer applies at the pipeline level.
- **espeak-ng inside sherpa-onnx (tools.md audit):** the Apache-2.0 `org.k2fsa.sherpa.onnx` native
  library statically contains espeak-ng (GPL-3.0) symbols, and the Kokoro archive ships
  `espeak-ng-data/`. Any build that links sherpa-onnx (ASR *or* TTS) therefore carries GPL code,
  contrary to the HLD licensing table. Options for an ADR: build sherpa-onnx without TTS/espeak,
  run Parakeet through ONNX Runtime directly, or isolate TTS in a separate GPL-licensed process.
- **ARM64 natives (tools.md audit):** the Vosk NuGet package ships no linux-arm64/win-arm64 native
  library, so ARM64 targets need a separate Vosk build (CI ARM jobs pass only because model-gated
  tests are skipped there).
- **e5 ONNX export (tools.md audit):** `intfloat/multilingual-e5-small` already publishes
  `onnx/model.onnx` upstream, so `scripts/fetch-models.sh` can fetch it instead of requiring an export.
- **Fuzzing found (M16a, oracle O5):** `RecastBuilder` took the gender of un/una from the word after
  the numeral instead of the noun (`1 grande queso` → `una grande queso`). Fixed to look through
  adjectives to the noun; regression cases added to `RecastBuilderTests`.
- **Lexicon gap (M16a):** `cuál` is missing from `content/v1/lexicon/seed.json`, so the correct
  `ask_price` utterance `cuál es el precio de …` is tagged `vocab.unknown`. Fixing it means a content
  edit, bundle re-hash and golden re-record; assigned to M16b.
- **Analyzer is not part-of-speech aware (M16a):** verb/noun homographs (ayudas, lanza, vino, como, sé,
  ir/ser preterite) trip `conj.tense`, `conj.person` or `ser_estar` on correct sentences. The fuzz
  generators skip these words and count the skips. Design limit; candidate follow-up after M16.
- **Pronunciation scoring cost (M16a):** scoring grows roughly with the cube of transcript length
  (about 0.4 s at 400 characters, over 20 s at 1600). M16b bounds generator lengths and records the
  budget; a linear-time alignment is a follow-up.
- **Ill-formed UTF-16 (M16a):** `Accents.Strip` throws on a lone surrogate and `CanonicalJson`
  writes one as U+FFFD, so such strings do not round-trip. Input should be rejected or repaired at
  the normalizer boundary; M16b adds the rule or a tolerance.
- **Intermittent gate failure (M16a merge):** one unidentified test failed in the first full
  `dotnet test` run after the merge build; four later full runs and twelve fuzz-project runs were
  green. The merge gate now keeps its test log so a recurrence can be identified.
- **Fuzzing found (M16b):** three more source bugs, each fixed with a regression test in the owning
  project and its CsCheck seed in `tests/Slg.Fuzzing.Tests/README.md`: `Accents.Strip` and
  `SpanishPhoneticKey.Compute` (Slg.Nlp) and `G2pNormalizer` (Slg.Phonology) threw on a lone surrogate
  and now replace it with U+FFFD like `TextNormalizer`; the content loader threw
  `NullReferenceException` on a lexicon entry without `features` or a file without `entries`, now a
  `FormatException` reported as a validation problem. This closes the M16a UTF-16 finding.
- **Pronunciation scorer cost, root cause (M16b):** `PredictedErrorMatcher.Match` runs a full
  `PhoneAligner.Align` per predicted variant, cubic in phone count (`PredictedErrorsFor` likewise with a
  smaller constant). The fuzz oracles cap inputs at 20/40 phones and 150 characters. Follow-up: a
  single alignment shared across variants.
- **Content loader silent defaults (M16b):** missing number, flag or enum fields (prompt `mode` and
  `difficulty`, evaluation thresholds, feedback `type`, effect curve points) default silently instead
  of failing validation. Listed in `KnownTolerances.AcceptedContentField`. Follow-up: require them or
  make the defaults explicit in the schema.
- **Replayer coverage (M16b):** the replayer compares the core record (including `/verdict`) and the
  downstream decision and response, but not the envelope's duplicate `result/verdict` and `result/replay`
  copies, and it reads `promptId` and the phoneme model hash back as inputs rather than checking them.
  O14 replay sensitivity covers the compared paths only. Minor; a consistency check on the copies is a
  follow-up.
- **Resampler (M16b):** kernels are rebuilt on every call; a WAV header claiming 1 Hz makes the
  front-end throw `OverflowException` on a large clip and a header claiming 2 GHz costs about 2 s.
  Follow-up: validate the sample-rate range in `WavFile.Read`.
- **`cuál` added (M16b):** `content/v1/lexicon/seed.json` now has 1217 entries; bundle hash is
  `b076558f7d1280b20ae84844d6aa377467fae8d3e8a86fc9d924c89d535d4efb`; the end-to-end golden was
  re-recorded; `tests/golden/manifest.jsonl` is unchanged. O3 now realises every catalog grammar.
- **Fuzz CI (M16b, parked):** `fuzz` job on push and pull request, `fuzz-soak` weekly (Sunday 03:17
  UTC) and on demand at 50 000/50 000/10 000/3 000/5 000 iterations (176 s measured locally); the
  existing jobs are guarded to push and pull request. Whole fuzz project: 49 tests, about 17 s at
  default depth.
- **Fuzz flake (M16b):** O9's injected `pron.silent_h` coverage minimum failed in 2 of 25 runs; the
  generator now produces about 76 such cases per run. The M16a-merge intermittent failure never recurred
  in twelve further full runs.
- **CLI-examples review (docs, after M16):** writing verified README examples exposed evaluator quirks,
  none fixed yet (candidate M17c): (1) `offer_item` has no keyword rule for `tener` and its grammars allow
  a leading pronoun but not an adverb, so `ayer tengo dos espadas` is not understood while `yo tengo dos
  espadas` is; (2) a fuzzy near miss tagged `vocab.unknown` is recast with the player's own wrong word
  (`ezpada` → "Ah, ezpada. ¡Perfecto!"), and near misses are inconsistently communicated (`ezpada` yes,
  `espado` no; `espadas` not communicated with no error and confidence 0.5); (3) `buenas tarde` is
  corrected to `buena` (adjective) rather than `tardes` (noun), and the recast keeps the player's missing
  accents (`buenas dias` → `buenos dias`); (4) `vocab.wrong` puts the answer-set id in `expected`
  instead of a word; (5) the `ser_estar` recast replays the whole sentence; (6) effects depend on the
  verdict only, so a purchase in the NPC's `done` state still emits `buy_item_price` (by design; the
  game layer gates it). Also confirmed: the four `pron.*` tags need the phoneme model, which has no
  ONNX export yet.
- **M17a results:** `content/v1/lexicon/wiktionary-es.json` (4.7 MB) holds the 3 000 most frequent
  lemmas from the kaikki.org Spanish extract (enwiktionary dump 2026-09-02, pinned by SHA-256) ranked by
  the OpenSubtitles-based `es_50k` list: 40 263 entries (1 847 nouns, 680 verbs, 701 adjectives); the
  merged lexicon is 41 078 entries, 32 494 forms, 3 372 lemmas (was 1 268 entries, 404 lemmas). 5 000
  lemmas failed the bundle-load gate (333–373 ms against 300 ms) and left no margin on suite time, so
  3 000 ships. Measured: bundle load 246–267 ms debug, full suite +21 %, fuzz suite 20 s. Bundle hash
  `8090fe859f5ad0f7eeec56d1aa710110b00a8de76deca8728887d21826bafb78`. Sources live in git-ignored
  `lexicon-sources/` (fetched by `scripts/fetch-lexicon-sources.sh`); `docs/nlp/lexicon.md` has the
  regeneration command.
- **Hand-authored deference (M17a):** the importer's `--hand-authored` option drops imported readings
  that would override hand-authored ones (Wiktionary's masculine `espada`, the matador, would otherwise
  make `un espada` correct) and words spelled like hand-authored function words (`la` the note, `si`
  the noun). Curated readings win; the policy is in `docs/nlp/lexicon.md` and ADR 0008.
- **Analyzer bugs fixed by the fuzz oracles on the new words (M17a):** an imperative spelled like the
  third person excused `tú habla`; the copula correction picked an imperative (`ella está alta` →
  `eres`); adjective lemmas sorted before their masculine form broke the ser/estar class check
  (`ocupada`); nouns readable as both numbers (`corazones`) tripped `noun.number`. All in
  `AgreementChecker`, regression tests in `AmbiguousReadingTests`.
- **Homographs after M17a (input to the M18 decision):** forms with more than one reading rose from
  89 to 7 216; 339 of the 1 583 generator-usable nouns (21 %) are also verb forms; 347 nouns are left
  out of the generators for gender homographs; 1–7 sentences per fuzz run skip as verb-form homographs.
  A lemma can also inherit a frequent word's count through a shared form (`casar` ranks 21st via
  `casa`), which part-of-speech data would fix.
- **Fuzz narrowing accepted (M17a):** O15 no longer walks files over 1 MB for structural mutations (a
  byte flip still reaches them, and the small lexicon files exercise the same parser), loads through an
  opt-in `LexiconCache`, and samples on several threads because its iterations are independent. Each is
  documented in the fuzz README's "narrower than the plan" list.
- **Licensing to confirm before release (M17a):** the frequency list's counts are CC BY-SA 4.0, but
  the terms of the underlying OpenSubtitles corpus were not confirmed; only a ranking derived from the
  counts is used and no counts or text ship. kaikki.org marks the per-language file as deprecated; the
  SHA-256 pin and the committed output cover it, and the raw all-languages extract (24 GB) is the
  fallback, untested.
- **Second M17a false positive (CLI-examples re-verification):** `cuánto cuesta la espada` now yields
  `gender.agree` (`cuánto` → `cuánta`, correctness 0.85) on every `ask_price` input, because the
  imported lexicon carries gendered readings under the lemma `cuanto` and the agreement checker ties
  the interrogative to the following noun. Forwarded to M18 as a required case; the `ask_price`
  example was removed from `docs/nlp/cli-examples.md` until M18 lands and is to be restored then.
- **M18 results:** `ReadingResolver` (new) picks one reading per token before the checks run: local
  syntax first (hard constraints on words whose every reading is of the triggering kind), then the
  slot-grammar bindings (`SlotGrammarMatch.Bindings`, with tied parses keeping either reading), then
  local syntax again, then a fewest-errors fallback (exhaustive up to 256 combinations, coordinate
  descent beyond). Bindings before syntax hid real mistakes (`yo lanza` read as `[NOUN:spell]`), hence
  the order. 121 new tests, 96 of them real-lexicon homograph sentences; `ayer compré un libro` and
  `cuánto cuesta la espada` score 1 again and `cuánto cuesta el espada` still gives `gender.agree`.
  Fuzz generators now draw homographs (gender-homograph exclusions 347 → 0; verb-form skips 1–7 → ≤1
  per 200), suite time unchanged, O4/O5 green at 200 000 iterations. Unresolved: verbs sharing a form
  (`fue` is `ir` and `ser`) and sentences correct under one reading and wrong under another with
  nothing to prefer. The resolver is the seam a trained tagger (M20) would replace.
- **M17c results:** the near-miss inconsistency was a sentinel, not the edit budget: a fuzzy match
  exactly at the budget edge reported confidence 0.5, which the communicated margin (0.6 ± 0.02)
  rejected, so `espado` and `yelo` failed while `ezpada` (phonetic distance 0) passed. Confidence is
  now 1 − d/length and all three are communicated; a fuzzy match's unknown word carries the canonical
  word in `expected` and the recast uses it ("Ah, espada"). `vocab.wrong.expected` is the answer
  set's single canonical answer or empty. Recasts restore lexicon accents (`buenas dias` → "buenos
  días") without touching accentless spellings that are words themselves. `offer_item` gained a
  `tener` keyword rule, so `ayer tengo dos espadas` resolves by Rules and keeps `conj.tense`. Bundle
  hash `1adcf251fb31189f0cb42875135217e65f4ee3d8e4c81dff19b5e0073ee1d294`. Known limit: `espadas`
  for `espada` on an answer-only prompt is a Fuzzy match with no `noun.number` tag (no grammar to
  check number). The `docs/nlp/cli-examples.md` prose for the `fuego` example (expected now empty) is
  fixed in M19a's docs pass.
- **M19a results:** 516 files renamed or edited; solution `Vocaterra.Nlp.sln`, 27 projects under
  `Vocaterra.Nlp.*`; env vars `VOCATERRA_MODELS_DIR` and `VOCATERRA_FUZZ_ITERATIONS*` with the `SLG_`
  names read as a deprecated fallback; `ConversationContext` (AgentId, AgentState, HostStateHash),
  `AgentStateMachine` and friends in `Decision.ConversationAgents`, `ResolvedEffect`, effect kinds as
  opaque non-empty strings with unique effect ids; content `npcs/` → `agents/` and JSON keys `npcId` →
  `agentId`, `gameEvent` → `hostEvent`, `gameStateHash` → `hostStateHash`; replay logs recorded before
  M19a no longer replay (accepted: pre-release developer artefacts; the golden was re-recorded).
  Bundle hash `d45b8012dde06bdd54039c4857849709a88e96e11802b960f5c5b36795a26b3f`. Kept as data:
  `slg-vosk-runtime` cache folder, `vosk-runtime-1` hash input, bundle id `v1`, sample ids. New
  `docs/nlp/api.md` (snippets compiled in a scratch host) and ADR 0009. Follow-ups taken into M19b: the
  literal rename produced `Vocaterra.Nlp.Nlp` (to become `Vocaterra.Nlp.Language`); O15 lost its
  `kind` enum mutation and needs an empty-kind mutation instead; the parked workflow now reads the
  repository variable `VOCATERRA_RUN_MODEL_TESTS` (rename it on GitHub if `SLG_RUN_MODEL_TESTS` was set).
- **M19b results:** 11 packages `Vocaterra.Nlp.*` 0.1.0 (nupkg + snupkg, deterministic, no licence
  expression while D3 is open) via `scripts/pack.sh` into the local feed `artifacts/feed`;
  `samples/HostConsole` restores from that feed with package references only and passes its two
  checks (`scripts/sample-host.sh`, verified on main). `ContentBuilder pack`/`verify-pack` produce and
  check a deterministic bundle zip with its hash. Project `Vocaterra.Nlp.Nlp` renamed to
  `Vocaterra.Nlp.Language`. O15 gained an empty-required-string mutation (effect id, intent, kind;
  prompt id). Parked workflow has a `pack` job with the GitHub Packages publish step commented out
  (D5). The Asr and Response packages carry the sherpa-onnx dependency with GPL espeak-ng inside
  (D2): do not distribute them until that is settled. Suite: 2603 passed, 5 skipped.
- **M19c results:** `james-d12/Vocaterra` has its root commit: the game design (`docs/game/specification.md`),
  `docs/game/architecture.md`, bundle `content/game-v1` (a copy of the core sample with id `game-v1`, same
  hash), an engine-agnostic console host on the seven `Vocaterra.Nlp.*` packages with a text loop
  that carries agent state, learner model and repeat counts across turns, 4 smoke tests, a parked
  workflow, and `scripts/content.sh` that drives the core checkout's ContentBuilder. No API gap: every
  type the host needs is public. Core follow-ups noted by the host author: a helper for repeat counts
  (the host re-implements the rule), prompt text for what an agent says before evaluation (`PromptSpec`
  has none), and packaging the ContentBuilder as a dotnet tool. `specification.md` left the core.
- **Sandbox keyword refusals (process):** agent sandboxes refuse command lines containing the shell
  builtin words `eval` and `hash`, which are also the CLI and ContentBuilder subcommands; briefs must
  say to use a wrapper script outside the repo, and the orchestrator runs the ContentBuilder `hash`
  step itself when an agent is blocked.
- **M17b-core results:** `[WORD:slot]` matches one all-letter token (accents and ñ included; no
  digits, punctuation or hyphens), binds the normalized text, and the bound word carries no reading:
  never `vocab.unknown`, no agreement check, no recast. Intents `confirm`, `deny`, `farewell`, `thank`,
  `introduce_self` with grammars, examples and four answer sets; prompts `confirm-open`,
  `farewell-open`, `thank-open`, `introduce-open`; blacksmith `confirming` state (`trading` →
  `buy_item` → `confirming`; `confirm` → `done` with host event `trade.confirmed`; `deny` → `trading`).
  Bundle hash `3b1d1f02dff448c830d0cfdbaf1f5e330fc66922a7b7507abc4b56d78afbc657` (23 files),
  reproduced by the orchestrator on main; golden `e2e-confirm-open` added. Two core behaviour changes:
  among grammars with equal explicit-token counts the one binding more literal words now wins (before,
  `gracias`, also a noun, resolved to `cast_spell`), and the embedding rung's reported gap is clamped
  at 1. Fuzz found three more bugs (a name spelled like an irregular verb got `conj.irregular`; a
  recast conjugated a name that is a verb form; gap above 1), all fixed with seeds in the fuzz README.
  Known limits: recasts print names in lower case; hyphenated names do not match; `nos veo` carries
  no tag (no clitic-person check). `cefr-check` now lists `moneda` and `nada` (A2 words in A1
  templates) and `herrería`; reword or accept. Suite: 2663 passed, 5 skipped.
- **M19d results:** `RepeatCounts.Next(before, verdict)` in `Vocaterra.Nlp.Pipeline` (ordinal-sorted
  immutable result; verdict tags and `not_communicated` count `before+1`, everything else is dropped,
  counts below 1 are treated as absent, saturates at `int.MaxValue`) and
  `ConversationContext.Next(PipelineResult, StoredTimestamp)` (keeps world seed and agent id, takes
  the decision's next agent state and learner model, the new counts and the given time; a host with
  its own state digest must recompute it). `PromptSpec.Text` (optional, JSON `text`, omitted when
  null; validator refuses empty, whitespace-only or untrimmed text; O15 mutates it) and a line for all
  18 sample prompts. ContentBuilder packs as the dotnet tool `vocaterra-content`
  (`Vocaterra.Nlp.ContentBuilder` 0.1.0, 12 packages from `scripts/pack.sh`); the tool nupkg is
  218 MB because `PackAsTool` bundles every native runtime (onnxruntime for all platforms, libvosk,
  sherpa-onnx with espeak-ng, D2) through `Content` → `Asr`/`Response`/`Pronunciation`; it is for
  local installation from the feed only. Bundle hash
  `cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1` (23 files), reproduced by the
  orchestrator on main; goldens changed only in their `contentHash` lines (`PromptSpec` is not in the
  replay record). Bug found by the rename: `VoskRuntimeModel.IsComplete` accepted a cache directory
  whose symlinks dangled into the old `SpanishLearningGame` path (`File.Exists` is true for a dangling
  link); fixed to resolve links, with a regression test. `cefr-check` scans dialogue templates only,
  not prompt text (follow-up if prompt text should be level-checked). Suite: 2682 passed, 5 skipped.
  Follow-ups: README still says 14 prompts (there are 18); the tool has no package readme; trimming
  the tool's natives means splitting content loading from the model adapters. For the game repo:
  switch `scripts/content.sh` to the installed tool, update the `../SpanishLearningGame` defaults in
  `nuget.config`, `content.sh` and docs to `../Vocaterra.NLP`, replace `NextRepeatCounts` with
  `RepeatCounts.Next` (the host copy can overflow), show `PromptSpec.Text`, re-sync `game-v1`.
- **M19e results:** `LICENSE` (Apache-2.0, byte-identical to the Debian copy of the apache.org text,
  sha256 `cfc7749b…523d30`) and `NOTICE` (copyright 2026 james-d12; the Wiktionary lexicon stays CC BY-SA
  4.0); `PackageLicenseExpression` Apache-2.0 and both files shipped in all 12 packages;
  `docs/nlp/PACKAGING.md` documents the decided consumption route (the game includes this repository as a
  git submodule and uses the local feed from `scripts/pack.sh`; hosted feeds are not planned).
  Isolation audit: README, `high-level-docs.md`, `tools.md`, `docs/nlp/PACKAGING.md`, `docs/nlp/package-readme.md`
  and `docs/nlp/lexicon.md` now describe the core only; ADRs 0001–0009 and the research report keep their
  bodies with a note that game matters are decided in the Vocaterra repository; text not already in
  the game's docs went to Vocaterra `docs/nlp/NOTES_FROM_CORE.md`. No code or content changed (bundle hash
  unchanged). Found by the agent's second test run: fuzz oracle O5 fails with seed `1diyRPH9lxJ1`
  (`ayer tengo sufrir`: `conj.tense` expected `tuve`, recast repeats `tengo`); reproduced by the
  orchestrator on main before the merge, so it predates M19e; scheduled as M19f. Follow-ups:
  `content/v1/ATTRIBUTION.md` still says "the game loads at run time" and "licence not chosen" (a
  content edit, needs the bundle procedure; fold into the next content milestone); the game's
  `docs/game/architecture.md` and `nuget.config` still name the old feed path (game side).
- **M19f results:** the O5 failure was not a recast bug. The pattern `[WORD:item]? [VERB] [ADJ]?
  [VERB:sufrir]` bound `ayer` as the optional name; a name carries no reading, so the past-time marker
  vanished, no `conj.tense` was raised and the recast had nothing to apply. Fix in the matcher:
  `[WORD]` binds only a letter word with no reading or with at least one open-class reading
  (`ReadingClasses.IsClosedClassReading`: article, numeral, determiner, pronoun, preposition,
  conjunction, adverb, interjection). Contract change: a spelled numeral (`dos`) no longer binds
  `[WORD]`, so `mi nombre es dos` is now unresolved rather than a name. Regression fact in
  `Vocaterra.Nlp.Language.Tests`; seed `1diyRPH9lxJ1` passes on main; O5 passed on four more seeds;
  `me llamo Ana`, `soy Ana` and the README demo unchanged. Suite: 2683 passed, 5 skipped.
- **Open requests from the game (read 2026-10-21, end of day):** the game orchestrator ran G1 and G2
  (its main `59b8630`) and filed CR-002 to CR-013 in `~/Dev/Vocaterra/docs/CORE_REQUESTS.md` (CR-009 is
  not listed there). All are `open`; none is accepted yet. Two are bugs in the core and go first:
  CR-012, the repeat counts mean "including this utterance" to `FeedbackPolicy.Plan` and `RepairFor`
  but `RepeatCounts.Next`/`ConversationContext.Next` (M19d) produce the counts before the next
  utterance, so the host cannot supply the right value (decide one meaning, fix the pipeline side,
  document it in `docs/nlp/api.md`); CR-013, accent-insensitive lookup is only a fallback, so `esta`
  never reads as `está` and `dónde esta el arbol` gets a false `gender.agree` and a wrong recast.
  CR-011 and CR-010 are the ATTRIBUTION.md and cefr-check follow-ups already listed under M19d/M19e.
  CR-006 unblocks the game's speech milestone P8 and must respect D2 (private builds only). The next
  core session triages them into M26 (and M27 if the result-surface work is large), marks each row
  `accepted (M26)` or answers why not, and keeps one agent per batch of disjoint modules.
- **M26 triage (2026-10-21, second session):** CR-002…CR-013 accepted and split by module so batches
  that touch disjoint directories can run in parallel: M26a (Pipeline, CR-012) and M26b (Language,
  CR-013) first, in parallel; M26c (Pipeline result surface and Content, CR-002/004/007/008) after M26a;
  M26d (CR-003/005, cross-module: context, templates, replay record, learner model; Sonnet) after M26c;
  M26e (models manifest and Response/Speech, CR-006) when disk allows (two model downloads, ~1 GB);
  M26f (CR-010/011, a content edit that re-pins the bundle hash) last so the re-pin happens once.
  CR-012 decision: `ConversationContext.RepeatCountsByTag` means the counts before this utterance (what
  `ConversationContext.Next` and `RepeatCounts.Next` already produce), and the pipeline computes the
  counts including this utterance itself; the alternative (documenting that the host passes the
  "including" counts) is impossible for a host, which cannot know the tags before it evaluates. The
  rows in the game repository's requests file were edited in its working tree and left for the game
  orchestrator to commit (its checkout had uncommitted plan work).
- **M26a results (CR-012):** the pipeline computes `RepeatCounts.Next(context.RepeatCountsByTag, verdict)`
  once after the verdict and passes it to `FeedbackPolicy.Plan` and `RepairFor` (the `Math.Max(1, seen)`
  guard is gone: a not-communicated verdict always carries its count). `yo tiene dos espada` three times
  now gives recast, hint, hint (was recast, recast, hint); the repair ladder for three failures is
  AskAgain, SlowRepeat, OfferChoices (was AskAgain, AskAgain, SlowRepeat). The CLI's `--repeat` now
  means counts before the utterance (README, `docs/nlp/pipeline.md`, `docs/nlp/cli-examples.md` examples shifted
  by one, outputs unchanged). Fuzz oracle `PipelineFuzzTests.CheckDecision` updated to the new contract
  (still exact equality). `samples/HostConsole` threads `ConversationContext.Next` over three repetitions
  and checks the ladder. Caveat: a replay log recorded before M26a with non-empty counts replays with
  different feedback (none is committed). Suite: 2685 passed, 5 skipped (one duplicate theory row
  removed, three facts added). The agent cleared the shared `~/.nuget/packages/vocaterra.nlp.*` 0.1.0
  cache while testing the sample; checkouts re-extract from their own feeds.
- **M26b results (CR-013):** `Lexicon.LookupWithAccentVariants` (exact readings plus the readings of
  accented forms that fold to an unaccented lexicon form; `Lookup` unchanged for its other callers) and a
  resolver step 0, `AccentVariantChoice` (rule tables in `AnalysisTables`): the typed reading is the
  default and an accented reading wins only when no typed reading fits its neighbours (an article,
  demonstrative or possessive with no noun phrase after it, a pre-verbal pronoun with no verb, a
  conjunction or preposition with nothing after it; a determiner after `dónde/donde/cómo` becomes the
  verb; `como` + pronoun + verb or + a finite verb becomes `cómo`); grammar-bound words and names keep
  their reading; up to 3 passes. Accent policy: nothing is reported to the learner (no accent tag, as
  for `arbol`); a recast made for another reason restores the accent (`AccentRestoration.SpellingsFor`
  in `RecastBuilder`). Corpus of 1102 sentences: 4 false positives removed (`donde esta el arbol`,
  `como esta el herrero`, `aqui esta el herrero`…), 10 real mistakes now found that the old reading hid
  (`el esta simpatico` → `ser_estar`, `tu tengo` → `conj.person`…). The first hand-back added a false
  `conj.person` on `soy el herrero y tu como te llamas`; sent back and fixed (`ed5cde8`) before the gate.
  Fuzz: new O3 fact drops accents where the neighbours leave no doubt; realiser re-rolls
  determiner/preposition/pronoun patterns that a reader can take either way
  (`skip:accent-variant-homograph`, within the homograph budget); five seeds that already failed on
  main were narrowed with reasons in the fuzz README (`2to272FZCXD3`, `belmI3pADeia`, `fApfampI1fQ3`,
  `8OeIOEx_Hfm3`, `1RTZdjQ2tFb2`). Suite: 2749 passed, 5 skipped; fuzz 57 facts. Known, not fixed:
  `tu hablas`/`tu ayudas` still read as possessive + noun (`noun.number`); `esta hecho` gives
  `gender.agree`; the slot-grammar matcher still sees typed readings, so `[VERB:estar]` does not bind
  `esta` (the game keeps its supplement entry); **follow-up:** in a grammar with an alternation and a
  `[WORD]` name, a mis-conjugated verb can bind as the name and its error is lost (seed `1RTZdjQ2tFb2`,
  fuzz narrowed, source not fixed).
- **M26c results (CR-002/004/007/008):** `ResolvedWord(Token, WordRole {Word, Name, Unknown}, LexicalFeatures?
  Reading)` in Core.Stages, carried as an optional trailing `Words` on `AnalysisResult` and on
  `PipelineResult` together with `Answer` (`MatchResult?`); additive, existing constructions compile.
  The core `ReplayRecord` is unchanged (both are derived from the recorded inputs); they travel in the
  envelope's `result` and `Replayer` compares `/words` and `/answer`, so a resolver change that keeps the
  verdict still shows on replay (logs recorded earlier report them as differing; none committed). The two
  e2e goldens gained the new fields (additions only). Host files: any manifest-listed file the loader does
  not own (`ContentPaths.IsLoaderOwned`), bytes kept from the load's hash check (never re-read from disk),
  `ContentBundle.TryReadHostFile(path, out ReadOnlyMemory<byte>)` and `HostFilePaths`; strict relative
  path rule; none exposed when loaded with `VerifyHashes=false`. `docs/nlp/api.md` §8 states the threading and
  load-cost contract. Thread-safety findings, documented not fixed (follow-up candidates):
  `VoskWordRecognizer.Recognize` checks `_disposed` without a lock (dispose racing a call can use a freed
  model; rule: one instance per thread, no dispose in use); `DirectorySynthesisCache.Put` uses a fixed
  `<key>.wav.tmp` (two concurrent misses on one key can throw); `VoskRuntimeModel.Prepare` is not
  synchronized per model directory (construct recognizers of one model on one thread); Wav2Vec2 and ONNX
  embedding adapters give no guarantee. Suite: 2776 passed, 5 skipped.
- **M26d results (CR-003/005):** `ConversationContext.HostSlots` (`SlotMap?`) and `ExtraSkills`
  (sorted, distinct; ids non-empty without whitespace), set through `Create`; both per utterance (`Next`
  does not carry them), omitted from JSON and from the old snapshot hash when empty (existing hashes,
  envelopes and goldens unchanged), included in `HostStateHash` and the envelope's context when present.
  Host slots share the verdict's namespace and lose on a clash (the reserved feedback names win over
  both), so adding a host fact never changes a line that rendered before; formatters (`article(def)`,
  `words(m)`) work on them; a missing one behaves as a missing verdict slot. Extra skills join the
  pipeline's `intent.<id>` in the single `LearnerModel.Update`; `LearnerModel.StepSkills` steps named
  skills alone (shares one private `Apply`). CLI `--host-slot`, `--host-skill`. Bug fixed: the loader's
  render check gave every unknown slot the sample `espada`, so `{distance:words(m)}` failed to load;
  `TemplateEngine.SlotKinds` now infers number/verb slots from their filters (the "a `words` slot named
  like a noun" check is gone as a consequence). Fuzz O14 and the replay/serialization oracles cover host
  inputs. Caveat: a host that builds its own digest with the constructor and then sets `HostSlots` with
  `with` gets a stale hash (documented: use `Create`).
- **M26e results (CR-006):** both release archive digests matched; all extracted files pinned (Kokoro 377
  files, 401 MB; Parakeet 8 files, 671 MB; downloaded into the git-ignored `models/`).
  `KokoroSynthesizer.FromStore(store, id, speakerId, speed)`. Spanish voices from the model's
  `speaker2id` metadata: `ef_dora` 28 (female), `em_alex` 29 and `em_santa` 53 (male). Parakeet
  transcribes the bundled `es.wav` correctly (load 1.6 s, decode 0.67 s). Two bugs found: (1) native
  collision: `Microsoft.ML.OnnxRuntime` 1.31 (via Language) and sherpa-onnx ship different
  `libonnxruntime.so` into one output folder and sherpa fails (`VERS_1.28.2 not found`); fixed in
  Response.Tests with `ExcludeAssets="native"`, but **a host that loads both the embedding model and
  Kokoro will hit it** (needs one ORT version both packages accept; follow-up, package decision);
  (2) a reused sherpa-onnx Kokoro engine does not repeat its output for the same text; the synthesizer
  now creates and disposes the engine per call, deterministic but 1.5–3.7 s per line (use
  `CachedSynthesizer`); supersedes M26c's "loads on first Synthesize" for Kokoro. Response now references
  Asr (for `ModelStore`), so the Response package pulls the ASR package; moving `ModelStore` to Core is a
  follow-up. Suite after M26d+M26e: 2823 passed, 3 skipped (wav2vec2 ×2, e5 ×1, both need an ONNX export).
- **Content-authoring research (Opus, 2026-10-21):** the owner named content authoring (intents and the
  content folder) as the main cost. Report `docs/research/2026-10-21-content-authoring.md`; outcome in §10.
  It corrected the orchestrator's first proposal: the game's copies are unchanged rather than edited, and
  pattern suggestion generalises poorly, so a lint (coverage, confusion, reachability, CEFR, examples
  with expected errors) goes first, then layering with `std-v1`, then format de-duplication. Owner chose
  lint first. Side finding: the hand grammars hold 4 real cross-intent matches, one the `gracias` →
  `cast_spell` class hidden only by the tie-break (M27a will report them).
- **M26f results (CR-010/011):** `CefrChecker` also checks prompt text, reported in its own section with
  prompt ids (`CefrPromptReport`); sample `ATTRIBUTION.md` rewritten for the sample only (hand-authored
  content Apache-2.0 per D3; third-party notices kept; model licences as in the manifest; D2 note kept;
  GitHub URLs instead of relative paths). Owner added `algo` and `acuerdo` to `cefr/a1.txt` (A1 and A2
  lists are disjoint by design and A2 inherits A1, so A2 is unchanged; the orchestrator's first
  instruction to add them to A2 broke `SampleContentTests` and was reverted). Bundle hash
  `cedb97c4…3afad1` → `1c2240c7…c9d38f2`; goldens differ only in `contentHash`. cefr-check on the sample:
  A1 prompt text 46 lemmas, 9 unlisted (game words: espada, hechizo, herrero, lanza, lanzar, moneda,
  objeto, oro, saludo). Process finding: the sandbox's worktree guard refuses `ContentBuilder ... hash`
  in an isolated agent (it reads the subcommand as the shell builtin `hash`), with no approval prompt;
  the orchestrator ran `hash` in the agent's worktree three times on the owner's explicit instruction.
  Every content milestone will hit this (M27b): either the owner runs or authorises the orchestrator to
  run `hash`, or the tool gains an alias subcommand (owner's call).
- **M26g results:** the intermittent Parakeet determinism failure was not threading (`NumThreads` is 1):
  sherpa-onnx 1.13.8's C# `Result.Durations` returns a buffer that is not float data on the Parakeet TDT
  path (pointer-like bit patterns, different on every decode), so the last word ended at ~2e37 s. The
  adapter no longer reads it: a word ends where the next starts and the last at the clip end (consumer
  change: `AsrWord.EndSeconds` includes trailing silence). Range assertions added; 30/30 isolated runs
  pass; Vosk and Kokoro checked, unaffected. Durations can return after an upstream upgrade.
- **M26 complete (2026-10-21):** all 11 game requests done; suite 2831 passed, 3 skipped.
- **Game requests, batch 2 (read 2026-10-21, late):** the game filed CR-014…CR-018 during the session
  (its G6/P4/P4b/P6b work). Accepted as M28a–c, split by module: M28a (Pipeline and Decision API,
  CR-017 "should" and CR-015), M28b (Response templates, CR-016 and CR-018), M28c (grading, CR-014). All
  three are disjoint from M27a (Content, ContentBuilder, the intent file schema), so M28a can run beside
  M27a once the owner agrees; M28a first because CR-017 costs the game a second evaluation per turn today.
- **M27a results:** `vocaterra-content lint <bundle> [--strict] [--level] [--fail-on-level] [--json]
  [--prompts a,b] [--host-pools p*,q]` over `ContentLinter.Lint(bundle, LintOptions)` in Content; `validate`
  prints lint errors/warnings and fails on them only with `--strict`. Checks: coverage (with rung;
  rule-only and uncovered examples, a grammar skeleton as a hint), confusion per prompt (`confusion.prompt`
  warning/error) and across the catalog (`confusion.catalog` info), grammar hazards (homographs as info:
  as warnings they were 7 true but unactionable findings on the sample), reachability (from all or
  chosen root prompts), CEFR (dialogue, prompt text, answer-set canonicals), example expectations.
  Additive schema: an example is a string or `{"text","errors","prompt"}`; per-intent `"lint"` waivers
  (`allowOverlap`, `allowRuleOnly`, `allowHomograph`; errors cannot be waived; waived findings still
  print). `TextGrader` grades text in Content (Content cannot reference Pipeline), with a parity test
  against `UtterancePipeline` on 99 example/prompt pairs; `ReservedPools` names the pipeline's reserved
  pools. Sample: 0 errors, 2 warnings (`thank` rule-only ×2), 8 info (the `gracias`/`cast_spell` overlap
  hidden by the tie-break, 7 homographs); 0.56 s. Game bundle (read-only): the research's 4 confusions
  reproduce; the host-voiced `guide.*` pools need `--host-pools`; from the guide's prompts the blacksmith
  material (12 pools, the agent, 7 answer sets, 8 intents) is unreachable. Reported, not fixed: `nos veo`
  (meant as a learner error) grades clean (clitic `nos` gives no subject; analyzer gap); pool-level waivers
  do not exist; the lint has no embedding rung. Content edits it proposes are M27a2 (they need `hash`).
  Suite: 2948 passed, 3 skipped.
- **M28a results (CR-017/015):** `UtterancePipeline.Analyze(clip, prompt)` returns an immutable
  `UtteranceAnalysis` (recognizer output, verdict, words, answer; no conversation state) and
  `Complete(analysis, context[, hostSlots, extraSkills])` runs decision, response, effects, repair and the
  replay record; `Evaluate`/`Trace` are now `Complete(Analyze(...))`, equal by construction and by a test
  over every example on every allowing prompt; the recognizer runs once; `Complete` refuses an analysis
  from a pipeline with another version stamp; two-phase chosen over a host callback (no host code inside
  the pipeline, testable, nothing unrecorded). Host turns: agent transitions gain optional
  `onHostEvent`, taken only by `AgentStateMachine.StepHostEvent` (utterances never take them);
  `UtterancePipeline.Advance(hostEvent, context)` returns `HostTurnResult?` (null when no transition or no
  machine: nothing changes); lines from the actions' pools with host slots only; interaction counter +1;
  learner and repeat counts untouched (nothing was observed; a host may call `StepSkills`);
  `ConversationContext.Next(HostTurnResult, now)`, `WithHost(...)`. Replay: a third line kind
  `"kind":"hostTurn"` (`HostTurnEnvelope`, `ReplayReader.ReadEntries`, `Replayer.Replay(HostTurnEnvelope,…)`,
  CLI `replay` reads both); old logs, envelopes and goldens unchanged; older core builds reject host-turn
  lines (documented). Fuzz O14: half the cases two-phase across two pipeline instances, a third take a
  host turn first; one bug found in the new code by the oracle (host turn recorded the full version stamp,
  replay differed at the phoneme model hash; seed `7CBlRsqnn4a1`; now records `ContentHash`). Side effect:
  serialized `AgentDefinition` JSON emits `onHostEvent: null` (no hash covers it). Not done: no
  two-phase example in `samples/HostConsole` (docs carry it). Suite: 2982 passed, 3 skipped.
- **Session close (2026-10-21, evening):** M26 (all 11 requests), M26g, M27a and M28a merged; the
  research report is in `docs/research/`. Next, each waiting for the owner's go: M27b layering and
  `std-v1` (with M27a2's sample fixes folded in; content edits need `hash`, which the sandbox refuses inside
  an isolated agent, so the owner runs it or tells the orchestrator to), M28b (CR-016/018 templates),
  M28c (CR-014 expected answers). Follow-ups logged: ORT native collision between
  Microsoft.ML.OnnxRuntime and sherpa-onnx in one host; Kokoro per-call engine cost (D9); `ModelStore`
  to Core; Vosk dispose race and the synthesis-cache temp file; `nos veo` analyzer gap; pool-level lint
  waivers; the `[WORD]`-absorbs-a-mis-conjugated-verb limit (seed `1RTZdjQ2tFb2`). The game's
  `CORE_REQUESTS.md` edits (all rows' statuses) are uncommitted in the game checkout for its orchestrator.
- **Two orchestrators (owner, 2026-10-21):** this repository is run by the core orchestrator only; the
  Vocaterra repository has its own orchestrator and plan. Protocol: the game writes requests for core
  changes to `docs/nlp/CORE_REQUESTS.md` in its repository; the core orchestrator reads that file at the
  start of every session, implements accepted requests as core milestones, and marks the row done with
  the core commit; the game orchestrator then bumps its submodule. The core never plans game content;
  the game never reaches into core source (packages only, API gaps go in the requests file).
- **Build output size:** each full build writes ~3 GB of native-package output per checkout;
  worktrees are cleaned after merge. Trimming runtime packs is a follow-up.
- **One repository (owner, 2026-10-21; game decision D-game-7):** the core now lives under `nlp/` of
  `james-d12/Vocaterra`, the game under `game/`; this history was rewritten under `nlp/` and merged.
  One orchestrator (`.claude/agents/orchestrator.md` at the root), one gate (`scripts/merge-gate.sh`,
  which runs this folder's licence scan too), one solution (`Vocaterra.sln` at the root; this folder's
  `Vocaterra.Nlp.sln` stays for core-only work, `scripts/pack.sh` and `scripts/license-scan.sh`). The
  game references the core's projects directly; the feed exists only for `samples/HostConsole` and a
  consumer outside the repository (D5 superseded in that sense). The game's request file is closed:
  its open rows are M28b, M28c and M28d; CR-024 (the pack picked up the game's `nuget.config`) is
  closed by the root `nuget.config` (nuget.org only). A game milestone never edits `nlp/`; a core
  change the game needs is a milestone here that names the game milestone waiting on it. Paths in this
  plan, the docs and the tests stay relative to `nlp/`.
- **M29c results (2026-10-22, Sonnet 5.5; found by the game's S5).** Root cause: `AgreementChecker.CheckDeterminer` blamed the noun for
  every number mismatch with a determiner and corrected the determiner's gender with its own number ("no, mucho gracias" gave expected
  'gracia' and 'mucha', recast «no mucha gracia»); `CheckAdjective` made "buenos" agree with "día" («Bueno día»). Fix: a table of fixed
  plural expressions (`AnalysisTables.IsFixedPluralExpression`: bueno + día/tarde/noche, mucho + gracia); when no other determiner is
  present and either side is plural, the phrase is plural and the modifier takes the noun's gender. The generic rule stays (fuzz oracle O4
  requires `noun.number` on the noun next to a singular determiner). Now: «no muchas gracias», "buenos día" → 'días'. `RecastBuilder`
  unchanged. 53 new tests; goldens unchanged; core demo unchanged. A longer fuzz run found M29d (unrelated). Recasts still lose ¿ ? (built
  from normalised text; a design question, not done).
- **M29e results (2026-10-22, Haiku 5.5; found by the game's S2 gate).** The pipeline fuzz generator now draws a host turn's agent after
  the host-turn flag (1/2 the blacksmith, 1/2 an unknown agent), so `hostturn:none` averages 54 per 300 iterations (minimum 43 in 20 runs;
  before 19, minimum 8) against the unchanged minimum of 10; every other asserted key was already more than 4 standard deviations clear.
  Coverage flake, not a pipeline bug; logged in the fuzzing README.
- **M29g results (2026-10-22, Haiku 5.5; requested by the game's S9).** `GeneratedByKinds.ModelOutput` (`"model-output"`): a provenance
  item of this kind needs a non-blank `model` and may carry `voice`, `speaker` (string) and `source`; no approval; refused for
  loader-owned language data (lexicon, intents, prompts, dialogue, ...) and the model fields refused on other kinds; the fields serialize
  only when set, so existing bundles are byte-identical. Fuzz O15 lists the four new optional fields like `approvedBy`. 23 new tests.
- **M29f results (2026-10-22, Haiku 5.5; found by the game's S7).** `AnswerSet.CanonicalPhrases()` (one canonical per answer, the set's
  declared order, no aliases); `RepairStrategy` offers the first three distinct canonicals; attempts 1 and 2 keep the full grammar, the
  attempt-3 retry grammar is the offered canonicals. The stall now offers «quiero agua por favor», «quiero pan por favor», «quiero fruta por
  favor» (was «agua por favor», «ayuda», «ayuda por favor») and the offer «sí por favor», «no gracias», «ayuda» (was «ayuda», «ayuda por
  favor», «claro»). Help answers come last by content order (no service marker in the core). The agent built only the core solution, so
  three game tests that pinned the old choices failed in the gate; a follow-up commit (the orchestrator's narrow exception) updated them.
  Lesson: a core milestone that changes behaviour visible to the game builds the whole solution.
- **M29h results (2026-10-22, Haiku 5.5; found by the S8 gate).** The replay test's effects region depends on a communicated verdict of an
  effect intent (only 37 of 300 cases had effects, and the mutated leaf was drawn uniformly over all leaves). Now the mutation picks a region
  uniformly among those the envelope has, then a leaf in it, and one case in four is aimed at an effect intent (drawn last, so earlier draws
  keep their values). Effects per run: before min 2, mean 5.4; after min 30, mean 47.4 (15 runs). Minimums unchanged. A rare O4 word-order
  failure seen once during the work is filed as M29i.
- **M29j, readings a learner's word record exposes (2026-10-22, Sonnet 5.5; found by the game's S19; merged `7ebf9b5`).**
  - **Root cause.** `result.Words` reported the first reading the resolver left, in lexicon order. The resolver rightly works in word classes:
    the imperative and indicative of one verb are both finite, and a noun beats an interjection because interjections are a last resort. So
    the reported reading was an accident of lexicon order, and the lexicon's multi-word entry `por favor` could never match a single token.
  - **Fix.** A reporting step, `Analysis/ReportedReadings`, runs after resolution and changes no error and no check (`ReadingResolver` and
    `AgreementChecker` are untouched, so M29c's «mucho gracias» still gives `gender.agree` 'muchas'):
    - an ambiguous imperative/indicative form is reported as the imperative only when it opens its clause; clause-initial `ser`, `estar` or
      `haber` before a determiner, noun or participle is the indicative;
    - `gracias` is the interjection unless an article or numeral precedes it, through a one-word table (`AnalysisTables.InterjectionFirstWords`),
      because the imported lexicon marks `sus`, `sé` and others as interjections too;
    - two or three adjacent tokens whose joined text is an exact lexicon form (`por favor`, `de nada`) report that entry's reading on each
      token. The lemma contains a space; each token keeps its `Text` and `Span`.
  - **Boundary.** No public member added; api.md documents the three cases. Goldens unchanged; 22 new tests; fuzz 57/57 at the defaults and
    at 10,000 and 8,000 iterations. Gate: 4,259 passed, 5 skipped; core demo unchanged.
  - **Known limits:** normalization drops `¿?`, so a bare «¿Está quieto?» reads as the imperative; `Está lloviendo` too (no gerunds in the
    lexicon); only exact lexicon multi-word forms are found («por qué» and «buenos días» are not in the lexicon).
- **M30, the LanguageTool content grammar check (2026-10-22, Haiku 5.5; merged `0d04a33`).**
  - **The command.** `vocaterra-content grammar <bundle> [--strings <jsonl> [--strings-only]] [--server URL] [--offline] [--cache]
    [--waivers] [--level] [--format text|json] [--strict] [--prune]`. Exit codes: 0 pass; 1 errors, or a cache miss offline; 2 usage;
    3 no server.
  - **Strings and oracle.** The strings JSONL (`id`, `text`, `kind` npc|learner|template, optional `errors`) is in api.md. Templates
    render from four sampled fillings with the core's `TemplateEngine`. Every intent example goes through the oracle.
  - **Files.** Cache `content/grammar/v1-cache.json` (sorted, keyed by sha256 of LanguageTool version, settings and text); waivers
    `v1-waivers.json` (a reason is required); learner-error fixture `v1-learner-errors.jsonl`; the tool manifest `tools/manifest.json`
    (Temurin and LanguageTool 6.6 by sha256); `scripts/fetch-tools.sh` and `scripts/languagetool.sh` (capped systemd scope, not
    `limited.sh`). Tests use an `HttpMessageHandler` fake, so no Java runs.
  - **content/v1.** No grammar error. One style waiver (`...` against `…`). Three untagged learner errors (`confirm.ex5`,
    `introduce_self.ex4`, `thank.ex4`), already M27a2's.
  - **Oracle over the 15 learner errors.** 11 flagged and tagged; 4 tagged but silent (`sí, por favo`, `yo nos vemos`, `dónde está la
    río`, `cuál es esto`). LanguageTool's suggestions are sometimes wrong (`Mucha gracia`, `cabo`).
  - **Checked by the orchestrator:** the gate (4,412 passed, 5 skipped; licence scan unchanged), and `grammar --offline` over content/v1
    with no server: exit 0, 3 warnings, 1 waived.
  - **Wired into the gate:** the orchestrator added an advisory step to `scripts/merge-gate.sh` (offline, never fails).
- **M50, core folder cleanup (2026-10-23, Haiku 5.5; merged to `main` `3bc525b`).**
  - **Removed:** `scripts/pack.sh`, `docs/nlp/PACKAGING.md`, `scripts/sample-host.sh` and `samples/HostConsole/`. The sample was in no
    solution and no test used it; its only caller was `pack.sh`, and with no feed (D5) nothing outside the repository will consume
    packages. The CLI and the game cover the host path.
  - **Moved:** `high-level-docs.md` to `docs/archive/nlp/history/`.
  - **Refreshed:** `tools.md`, with the snapshot at `a642066`, 28 scanned packages, and the M30 JRE and LanguageTool.
  - **Rewritten:** README ("Using the core from a host"), CONTRIBUTING, api.md and scripts/README.
  - **New:** `scripts/check-links.sh`. It found 1 broken link before (to `.github/workflows-pending/README.md`) and 0 after;
    CONTRIBUTING requires it to pass.
  - **Unchanged:** `content/v1`, the goldens, fuzz, tests (4,477 passed, 5 skipped), the licence scan and the core demo.
  - **Kept as build configuration:** the packaging properties in `Directory.Build.props` and `docs/nlp/package-readme.md`.
  - **Left for the next game milestone:** stale references outside `nlp/` in `scripts/content.sh`, `scripts/README.md`, the root
    `nuget.config` comment and `docs/game/architecture.md`.
- **M49, near-future tense checks (2026-10-23, Sonnet 5.5; for the game's S27; merged to `main` `dc53cbb`).**
  - **The new check.** `AgreementChecker.CheckNearFuture` gives `conj.tense` in two cases:
    - a future-time marker with a preterite-only or imperfect-only finite verb. The markers are `mañana` (not after a determiner or
      `de`), `pasado mañana`, `próximo/a` + time noun, and time noun + `que viene`. Expected is the `ir a` + infinitive form for the person
      (the form S27 teaches), or the present of `ir` for `ir a`;
    - a finite `ir` + `a` + a word that is only a finite verb. Expected is the infinitive: `voy a como` gives `comer`.
  - **Past markers.** `CheckTense` also flags a simple future after `ayer`.
  - **Left clean:** the present or future after a future marker, `luego` and `más tarde` (ordinary narration; deliberately not markers),
    `esta mañana`, `por la mañana`, reported clauses, and noun phrases after `a`.
  - **Source fixes.**
    - `ReadingResolver` keeps the verb after the `a` of `ir a`. `voy a como` was read as a conjunction before.
    - `próximo/a` before a time noun is exempt from `word_order` (`AnalysisTables.IsUpcomingTimeNoun`). This removes the old false
      positive on `la próxima semana`.
  - **Caught by the gate.** The first version made `próximo` prenominal everywhere. The fuzz oracle O5 (`RecastLoopFuzzTests`, seed
    `2tirebhSKN_5`) caught `próximos salones` losing its `word_order` error, and the fix was narrowed to time nouns. The generator now
    leaves `mañana` out of its adverb pool, as it already left out `ayer`. A generated "correct" sentence with `mañana` and a preterite
    is genuinely wrong now; no oracle changed.
  - **Results.** 41 new tests; fuzz 57 of 57 at the failing seed, at the defaults and at 10,000 and 8,000 iterations (the orchestrator
    reran the seed and a fresh seed on merged `main`). Goldens, content, the core demo and the host demo are unchanged. Gate: 4,636
    passed, 5 skipped.
  - **Limits:** the checks work on the whole utterance (no clause split, and normalization drops punctuation); `mañana` has no noun
    reading.
- **M51, `ir` + bare infinitive (2026-10-23, Sonnet 5.5; found by the game's S27; experiment X02, merged to `feature/experiment` `e70441c`).**
  - **The check.** `AgreementChecker.CheckIrWithoutA` gives `conj.tense` for a present or imperfect finite `ir` directly before a bare
    infinitive: `voy comer pan` gives expected `voy a comer`, actual `voy comer`. The span covers both words.
  - **Left clean:** `voy a comer`, `quiero comer`, `ir a comer`, `vamos a la tienda`, `voy contento`, and `fui comer` on purpose (fui is also
    ser).
  - **Source fix.** `ReadingResolver` keeps `van` (also a noun) as a verb before an infinitive.
  - **Generator mirrors.** The generator skips `ir` + infinitive (`ir-bare-infinitive`), and the WordOrder mutation skips `próximo` + time
    noun. The second fixes an O4 flake left by M49 (seed `7oEL4lRaLMu2`, "los treinta próximos domingos" is correct Spanish), which is also
    present on `main`. No oracle changed.
  - **Results.** 18 new tests. Fuzz passed at the seed, twice at the defaults and twice at 10,000 and 8,000 iterations; the orchestrator
    reran the seed on the merged branch. Gate: 4,800 passed, 5 skipped. Goldens and both demos are unchanged.
  - **Limits:** adjacency only; the preterite is not checked; no gerunds in the lexicon.
  - **Note for the owner:** `main` still carries the rare O4 flake until this branch, or the one-file generator fix, reaches it.
- **M31, Spanish identity guard (2026-10-26, Haiku 5.5; merged to `feature/language-packs` `f4694ff`).**
  - **Built.** `tests/Vocaterra.Nlp.Golden.Tests/SpanishIdentityTests.cs` with a frozen corpus (`Data/identity/corpus.jsonl`, 6,349
    inputs: 103 homograph sentences, 108 regression sentences of M26b, M29c and M29j, 224 intent examples, 20 CLI examples, 894 G2P words,
    3,000 correct and 2,000 mutated fuzz sentences drawn once at seed `6VTk-wxfywU1`) and a baseline (`baseline.jsonl`, 1.5 MB, 6,524
    lines with the 175 rendered template variants). Each stage hash is the first 16 hex characters of a SHA-256 over canonical JSON. The
    test runs in about 9 s and needs no models. It re-records only with `VOCATERRA_RECORD_IDENTITY=1`, never with the golden variable.
    `scripts/identity-check.sh <base-ref>` checks the gate rules: the bundle hash is unchanged, and nothing under `tests/*/Data/`,
    `tests/golden/` or `content/` changes except pure renames.
  - **It bites.** One changed repair line (`¿Cómo?` to `¿Cómo??`) made 5,504 of 44,618 stage checks differ. A changed dialogue template
    never reaches the guard, because the loader rejects the bundle on its hash first.
  - **Base ref for Phase A.** M52 and every later Phase A branch run `scripts/identity-check.sh f4694ff`, not `main`. M31 itself adds the
    baseline, so it cannot pass against `main`.
  - **Limits.** M26b's 1,102-sentence corpus was never committed, so it is not included. The fuzz draw was a one-off and is not in the
    repository; the committed corpus is its record. `PromptGrammars` and the sample-slot rule are internal to Content, so the test keeps
    frozen copies of both.
  - **Gate.** 5,254 passed, 3 skipped. Two failures were already on `main`: the licence scan (`Vocaterra.Nlp.slnf` is missing; M52
    recreates it) and the game test `SpokenModelTests.AMistakeInASpokenLineIsHeardAsSaidAndTheCoreCorrectsIt` (with the real Vosk model the
    recognizer hears "a dónde está la árbol", one extra leading word).
- **M52, one engine assembly (2026-10-26, Haiku 5.5; merged to `feature/language-packs` `1976508`).**
  - **Built.** `src/Vocaterra.Nlp` holds the 11 former libraries as folders, moved with `git mv`; namespaces and resource names are unchanged.
    `tests/Vocaterra.Nlp.Tests` holds the 11 unit test projects as folders, with their `Data/` moved as pure renames. The 11 smoke tests are
    now one. Fuzzing and Golden stay separate and reference only `Vocaterra.Nlp` (Golden also references Bench). `Vocaterra.Nlp.slnf` is
    recreated, so the licence scan runs again. ADR 0012 is added; ADR 0003 and 0009 carry dated notes.
  - **Layering.** `Architecture/LayeringTests` (NetArchTest.Rules 1.3.2, MIT) encodes the old project graph as namespace rules. It also
    checks that every stage has types, and that a negative fixture is reported. A temporary Core to Pipeline reference made two cases fail.
  - **`VocaterraCore`** is two assignments in `Directory.Build.props`: `StartsWith('Vocaterra.Nlp.')`, then `true` for `Vocaterra.Nlp`
    itself. An MSBuild property value has no `Or`.
  - **Two ONNX Runtimes in one test process (not foreseen).** sherpa-onnx and Microsoft.ML.OnnxRuntime each ship a `libonnxruntime.so` with
    a different API version. In one output folder one overwrote the other. `NativeOnnxRuntimes.cs` (a module initializer) loads sherpa's copy
    by full path and maps the managed package's `onnxruntime` to `ort-managed/`. The csproj copies that file from
    `$(PkgMicrosoft_ML_OnnxRuntime)`, and the Linux x64 build fails if it is missing. The native and model tests share
    `NativeRuntimeCollection` (not parallel). The merged project passed three runs in a row.
  - **Licence scan.** Mono.Cecil, which NetArchTest brings in, names the MIT page by URL; `license-scan.sh` now knows that URL. NetArchTest
    names no licence and has a reviewed MIT override; the orchestrator checked its LICENSE file.
  - **Gate.** 5,258 passed (14 layering cases added, 10 smoke facts removed), 3 skipped. The licence scan passed. The only failure is the
    pre-existing game speech test. `identity-check.sh f4694ff` passed, and the CLI demo output is byte-identical. One game test
    (`ArchitectureRulesTests`) had its path regex widened to accept `Vocaterra.Nlp`.
- **M32, language id and pack registry (2026-10-27, Sonnet 5.5; merged to `feature/language-packs` `ba85fe3`).**
  - **Built.** `src/Vocaterra.Nlp/Language/Packs/` holds `ILanguagePack` (text side only: `LanguageId`, `Normalizer`, `Folding`, `Numbers`,
    `PhoneticKey`, `Punctuation`, `G2p?`, `GrammarDefaults`, `Lint`, `RepairFallback`), `SpanishLanguagePack.Instance` and the
    `LanguagePacks` registry (`Default` holds `es`; `Of`, `With`, `TryResolve` by the primary subtag, ASCII case-insensitive). The analysis
    members (morphology, analysis rules, template functions, `LexiconProfile`, `ErrorTagSet`, recast policy) are left to M34 (ADR 0011).
    The statics did not move and no call site is routed yet (M33).
  - **Manifest.** `language` and `supportLanguage` (optional, omitted when null). With `language` the bundle hash is
    `Combine("language", tag, path₁, sha₁, …)`, the tag as written; without it, the hash is today's. `supportLanguage` is not hashed: it
    changes no output, and hashing it would change every stamp and replay record. The loader resolves the pack right after reading the
    manifest (also with `VerifyHashes` off); an unknown or malformed tag is one `ContentValidationException` problem. `ContentLoadOptions.LanguagePacks`
    lets a host pass its own registry; the ContentBuilder `hash` command takes `--language` and `--support-language`.
  - **Layering.** `Language.Packs` has its own rule (Core, Language, Phonology, Response); the `Language` rule excludes it.
  - **For M33.** The pack uses `Response` for `PunctuationFixer`, and `Response` may use `Language`, so a `Response` call site that takes an
    `ILanguagePack` makes a namespace cycle the rules allow: ADR 0011 point 9 says to move the punctuation behind the interface or keep the
    cycle on purpose. The pack repeats Spanish literals (`por`, `favor`, `un`, `una`) also found in `LintCoverage`, `SlotGrammarMatcher` and
    `AnalysisSlotFinder`; M33 removes the duplicates. Two visibility-only edits: `WordClasses.SubjectPronounLemmas` (an ordered list) and
    `LintCoverage.SubjectPronouns` (internal).
  - **Not done.** The replay envelope's optional `language` field (research §4.6) is not added; it is optional and not in M32's scope.
    `TextFolding.Normalize` throws on a lone surrogate (pre-existing, unreachable from the pipeline, which replaces lone surrogates first).
  - **Gate.** 5,366 passed (109 new: pack, registry, manifest, ContentBuilder and four O15/O16 fuzz cases), 5 skipped (models not on this
    machine), 0 failed; licence scan passed. Fuzz at 10,000 grammar iterations green. `identity-check.sh f4694ff` passed, and the CLI demo
    gives `conj.person` and `noun.number` with content hash `aab525f9…`.
- **M33, text-side seams (2026-10-27, Haiku 5.5; merged to `feature/language-packs` `d677412`).**
  - **Routed through the bundle's pack.** The pipeline (normalizer, answer matching, analysis, intent resolution, pronunciation numbers and
    G2P, speech requests, recasts, the recognizer's grammar phrases), the response side (`TemplateEngine`, `SpeechRequests`,
    `RecastBuilder`), the grammar defaults (`SlotGrammarMatcher`'s optional subject and trailing polite words, `AnalysisSlotFinder`), the
    content tools (`TextGrader`, `ContentLinter`, `LintCoverage`, `CefrChecker`, `LintConfusion`, `ContentValidator`) and Bench and CLI.
    Engine signatures such as `AnswerMatcher.Match`, `LinguisticAnalyzer`, `IntentResolver` and `SlotGrammarMatcher.Match` now take an
    `ILanguagePack` (recorded in `api.md`); the game uses none of them and compiles unchanged.
  - **Repair lines.** Reserved pools `repair.ask_again` and `repair.slow_repeat` override `RepairFallback` when a bundle has them (one
    variant drawn with the interaction seed); `content/game-v1` has none, so the text is unchanged. The give-up line is always the pack's.
  - **New pack members.** `ILanguagePack.Recast` (`IRecastPolicy.RestoredSpellings`, `SpanishRecastPolicy`: the accent rules moved out of
    `RecastBuilder`) and `INumberWords.Agree`. ADR 0011 records both.
  - **Kokoro.** `FromStore` takes `lang = "es"`, folded into `ModelHash` only when it is not `es`. The CLI and Bench name each model's
    language and refuse a model whose language differs from the bundle's.
  - **Cycle removed.** `PunctuationFixer` and `NumberAgreement` moved to `Language/Packs`, so the packs use only Core, Language and
    Phonology; `LayeringTests.ThePacksAndTheResponseStageHaveNoCycle`. The duplicated `por`, `favor`, `un`, `una` literals now come from
    the pack.
  - **Guard.** `Architecture/SpanishCallSiteTests` scans `src/Vocaterra.Nlp` and `tools/Vocaterra.Nlp.*` for the Spanish statics and
    fails outside an allowlist: the packs, the statics' own files, the compatibility entry points (`RecastBuilder.Build`,
    `SpeechRequests.Create`, `TemplateEngine.CreateDefault`), and per-file M34 entries (`Lexicon`, `IntentCatalog`, `SlotGrammar`,
    `SlotGrammarMatcher`, `MorphologyGenerator`, `ReadingResolver`, `AccentVariantChoice`, `ReportedReadings`, `AnalysisTables`,
    `AgreementChecker`, `ReadingClasses`) that need `IMorphology`, `IAnalysisRules` or `LexiconProfile`. It has a negative test.
  - **Left for M34 and later.** The lexicon builds its indexes with the Spanish fold at load. `RepairStrategy.OfferLine` («¿Quieres decir
    «…»?») is still Spanish text in the engine, not a pack member. `PronunciationScorer`'s predicted-error tags are Spanish-tuned
    (research item 14).
  - **Bug caught in review.** The Spanish pack's instance initializers read word lists declared below `Instance`, before they were set; the
    lists now come first.
  - **Gate.** 5,385 passed (17 new; the 2 Parakeet tests run again now that the model is in the shared folder), 3 skipped, 0 failed;
    licence scan passed; fuzz at 10,000 grammar iterations green; no existing expected value changed. `identity-check.sh f4694ff` passed,
    and the CLI demo is unchanged.
- **M34, analysis-side seams (2026-10-27, Sonnet 5.5; merged to `feature/language-packs` `2161c6e`).**
  - **New pack members.** `LexiconProfile`, `ErrorTags` (`ErrorTagSet`: ranks, `IsKnown`, `IsPronunciation`; `V1` is today's Spanish
    set), `CreateMorphology` (`IMorphology`; `MorphologyGenerator` implements it), `CreateAnalysisRules` (`IAnalysisRules`: word-class
    predicates and `CreateAgreementChecker`) and `CreateTemplateFunctions` (`ITemplateFunctions`). `RepairLines.Offer` (`OfferLineFormat`)
    carries «¿Quieres decir «…»?», with the same text; it has no pool override, because a pool line has no placeholder for the choices.
  - **Moved into `Language/Packs`.** `SpanishAnalysisTables` (was `AnalysisTables`), `SpanishAgreementChecker` (was `AgreementChecker`),
    `SpanishAnalysisRules`, `SpanishTemplateFunctions`. `ReadingResolver`, `AccentVariantChoice`, `ReportedReadings`,
    `AccentRestoration`, `ReadingClasses` and `LinguisticAnalyzer` stay shared and take the rules and the pack's folding. `ReadingClasses`,
    `WordClass` and `RegularizedForm` are public so a pack in another assembly can implement the seams (M53).
  - **Lexicon.** Indexes are built with the pack's `ILexiconFolding`; lexicon files are checked against the pack's profile (a Spanish `n`
    gender is still refused with the same message); the lexicon cache key includes the language id. `IntentCatalog.Load` and
    `SlotGrammar.Parse` take an optional folding; without one they use `TextKey.Of` (NFC plus invariant lower case: the code of
    `TextFolding.Normalize`, moved, so the key is the same).
  - **`ContentBundle.Morphology`.** The engine uses the new `bundle.LanguageMorphology` (`IMorphology`) and `bundle.AnalysisRules`.
    `bundle.Morphology` keeps its type for the game and returns the same object for Spanish; for another pack it throws
    `NotSupportedException` on access, not at load.
  - **Schema.** `LexicalFeatures` and `EntryFeatures` gain optional `Case`, `VerbForm`, `Declension`, `Particle`, `Register` (null, left out
    of JSON). `NormalizedText.FinalMark` exists behind `NormalizationOptions.CaptureFinalMark` (default off; the pipeline does not set it).
    The analyzer and `VerdictAssembler` order errors by the pack's ranks through an optional `VerdictInputs.ErrorTags`.
  - **Guard.** The M34 allowlist went from 16 entries to 4: `MorphologyGenerator.cs` (3 patterns; the Spanish `IMorphology`, moves in
    M53) and `Lexicon.cs` (`Lexicon.Seed`, `Lexicon.Empty` and the overloads without a folding default to Spanish; the embedded seed is
    Spanish and 32 test files and the ContentBuilder use them; a bundle's lexicon never does).
  - **Breaking API, none used by the game:** the five new `ILanguagePack` members; `ReadingClasses` predicates and
    `AccentRestoration.SpellingsFor` take the rules; `IntentCatalog.Load` and `SlotGrammar.Parse` no longer convert to `Func<string, T>`;
    deconstructing `LexicalFeatures` or `EntryFeatures` yields more elements. `api.md` lists them.
  - **Left out.** `ReadingResolver`'s local-syntax rules are shared, not pluggable: verb-second order or separable prefixes need the rule
    set extended (M54 onward; ADR 0011). `PredictedErrorMatcher` still reads `ErrorTags.All` (pronunciation stays Spanish-tuned,
    research item 14).
  - **Gate.** 5,419 passed (34 new, in `AnalysisSideSeamTests`: a pack that changes one member and the engine following it), 3 skipped,
    0 failed; no assertion removed, Fuzzing and Golden untouched; fuzz at 10,000 grammar iterations green; `HomographSentencesTests` green;
    `identity-check.sh f4694ff` passed, and the CLI demo is unchanged.
- **M35, per-language test infrastructure and a toy pack (2026-10-27, Haiku 5.5; merged to `feature/language-packs` `dd8eaa7`).**
  - **Harness.** `Oracles/FuzzLanguage` (pack, bundle, free-text generator, sentence generator, mutation kinds) and
    `Oracles/LanguageOracles` (the bodies of O1, O2, O7 and a neutral O14). The Spanish classes keep their names, seeds and iteration counts
    and call those bodies; the Spanish O14 (`PipelineFuzzTests`) is unchanged. The orchestrator compared the old and new failure checks: all
    are kept, and the error-tag check now reads the pack's set (Spanish: `ErrorTagSet.V1`, built from `ErrorTags.All`).
    `FuzzLanguageStreamTests` pins the first case at 400 fixed seeds (SHA-256 over the stream), from the unchanged generators: CsCheck seeds
    only the first iteration of a sample, so later iterations are not reproducible across runs.
  - **Toy pack.** `Languages/Xx/` in the Fuzzing project, public API only: `XxLanguagePack` (every member, non-Spanish), a three-word bundle
    written to a temp directory at test time with `"language": "xx"`, loaded through a registry without Spanish (the default registry
    refuses it). O1, O2, O7 and the neutral O14 pass on it, also at 20,000 general, 10,000 grammar and 3,000 pipeline iterations. No result
    carries a non-toy tag or a Spanish mark, and `bundle.Morphology` throws `NotSupportedException`.
  - **Hidden Spanish dependencies found and fixed**, each with a regression test:
    1. `TextKey.Of` (the fold behind `TextFolding` and the no-folding parsers) threw on a lone surrogate; it now reads it as U+FFFD, as
       `Accents.Strip` does. Valid text is unchanged. `SpanishLanguagePackTests` had pinned the throw; it now pins the new contract.
    2. `SampleSlots` filled templates for the validator with Spanish words (`tener`, `espada`, `yo tengo dos espadas`, …). New
       `ILanguagePack.Samples` (`SampleWords`); the Spanish values are the same. Breaking for any other `ILanguagePack` implementer.
    3. `RecastBuilder.AddNumberEdits` spelled any digit run up to six digits; it now skips values above the pack's `Numbers.MaxValue`, the
       rule `TemplateEngine` already used. Spanish is unaffected (its bound is 999,999).
  - **Goldens.** `GoldenFiles` reads and records another language's goldens under `Data/<lang>/`; Spanish keeps `Data/<name>`. Code only;
    no file under a `Data/` folder was added.
  - **Not done.** O3, O4 and O5 do not run on xx (no grammar realiser or mutation set for it). The xx run cannot prove directly that
    `SpanishLanguagePack` is never initialised; the registry without Spanish, the result checks and `SpanishCallSiteTests` cover it
    (ADR 0011).
  - **Gate.** 5,438 passed (19 new), 3 skipped, 0 failed; fuzz at 10,000 grammar iterations green; `identity-check.sh f4694ff` passed, and
    the CLI demo is unchanged.
- **M53, the Spanish pack project (2026-10-27, Sonnet 5.5; merged to `feature/language-packs` `65de544`). Phase A is complete.**
  - **Moved** (61 renames) into `src/Vocaterra.Nlp.Es` (namespace root `Vocaterra.Nlp.Es`): `SpanishLanguagePack`; `Es.Text`
    (`SpanishRecastPolicy`, `SpanishTemplateFunctions`, `PunctuationFixer`, `NumberAgreement`, the new `SpanishRepairLines`); `Es.Analysis`
    (`SpanishAnalysisRules`, `SpanishAgreementChecker`, `SpanishAnalysisTables`, `WordClasses`); `Es.Normalization` (`SpanishNumbers`,
    `SpanishPhoneticKey`, `Accents`, `TextFolding`, and `TextNormalizer` renamed `SpanishTextNormalizer`); `Es.Morphology`
    (`MorphologyGenerator`, `SpanishOrthography`); `Es.Phonology` (`SpanishG2p` and its stages, `exceptions.json`); the seed-lexicon
    resource. The LogicalNames are unchanged.
  - **Stayed neutral:** `DialectConfig`, `G2pResult`, `PhoneInventory`, `PredictedErrorVariants`, `ErrorTags`/`ErrorTagSet.V1`,
    `LexiconProfile.V1` (the frozen v1 contract every pack may pick), and the espeak phone map (it maps the multilingual xlsr-53 vocabulary
    onto the shared inventory; ADR 0012). The engine keeps a neutral `TextTokenizer`; `SpanishTextNormalizer` adds the number words and
    `al`/`del` on top, with identical output. No `InternalsVisibleTo` from the engine to the pack: `Utf16`, `TextKey`, `Lexicon.Derive`,
    `TextTokenizer` and `InvariantLexiconFolding` became public.
  - **Host-facing (api.md, ADR 0012).** `LanguagePacks.Default` is empty; a manifest with no `language` still means `es`, resolved from
    the host's registry, so a bare `ContentLoader.Load` of a Spanish bundle now throws. `SpanishContent.Load`, `.Options`,
    `LanguagePacks.WithSpanish()` and `CreateTemplateEngine()` register it. `ContentBundle.Morphology` is removed: use
    `bundle.SpanishMorphology()` or `bundle.LanguageMorphology`. The Spanish seed is `SpanishLexicon.Seed` (and `Load`, `FromEntries`,
    `Empty`); `Lexicon.Empty` stays with the neutral folding. The pack-less Spanish overloads (`RecastBuilder.Build`,
    `SpeechRequests.Create`, `TemplateEngine.CreateDefault`, `ResponsePlanner()`, `RepairStrategy.Next`'s short forms) are gone;
    `FakePhonemeRecognizer.ForSpanish` is `ForPhoneInventory`.
  - **Game edits (allowed by the brief, no behaviour change):** a project reference to `Vocaterra.Nlp.Es` in `Game.Core` and `Host`;
    `SpanishContent.Load` at the two load sites (`GameBundle.cs`, `Host/Program.cs`); `.SpanishMorphology()` for `.Morphology` (four
    files); moved names (`SpanishTextNormalizer`, namespaces) in five files and their tests.
  - **Guards.** `LayeringTests`: no engine namespace depends on `Vocaterra.Nlp.Es`, and the engine assembly does not reference it.
    `SpanishCallSiteTests` scans `src/Vocaterra.Nlp` with an empty allowlist (asserted). `XxIndependenceTests` (source and reflection
    scans) keep the toy pack free of Es types, since the Fuzzing project now references Es. `git grep Spanish src/Vocaterra.Nlp` finds only
    comments, doc examples and the sidecar field `LexiconSourceInfo.SpanishEntries` (a JSON field name; renaming it changes the format).
  - **Test projects referencing Es:** Es.Tests; Nlp.Tests, Fuzzing and Golden (they run `content/game-v1`); the three tools. Three
    resolver tests stay in Nlp.Tests because they use engine internals, and link the Es tests' `TestTokens.cs`.
  - **Bug found and fixed.** `UtterancePipeline.RepairLinesFor` dropped the pack's `RepairLines.Offer`, so every pack got the engine's
    default offer wording (hidden while that default was Spanish). Regression test
    `PipelineTests.ThePacksOwnOfferWordingReachesTheOfferedChoices`.
  - **Not done.** `DialectConfig.LatinAmericanNeutral` is still `SpeechRequests.Create`'s default dialect (a per-pack default is a later
    decision). A pack with phones outside `PhoneInventory` needs its own espeak map (ADR 0012).
  - **Gate.** 5,445 passed (Nlp 2,452, Es 1,129, Fuzzing 76, Golden 17, Game.Core 863, Game 872, Host 36; 7 new, none lost), 3 skipped,
    0 failed; licence scan passed (30 packages); fuzz at 10,000 grammar iterations green; `identity-check.sh f4694ff` passed (the G2P golden
    and `exceptions.json` are pure renames); the CLI demo and the host demo are unchanged.
- **Test performance, the core's half (2026-10-29, Opus 5.5, `feature/test-performance`; plan `docs/process/test-performance-plan.md`, T2, T3, T4).**
  - **One bundle load per test process.** `GameBundleLocator.Bundle` (tests/Shared) loads `content/game-v1` once with every hash verified;
    Nlp.Tests' two loads and Golden.Tests' three use it. The CLI tests still load through `--content` (the CLI takes no `LexiconCache`).
  - **The model gate verifies each model once.** `ModelGate.IsAvailable` keeps its verdict per id; the pipeline tests' copy of the gate is
    gone. Measured: the gates hash Vosk (58 MB) and Parakeet (641 MB) at about 1 GB/s, so the saving is seconds.
  - **The serial collection.** Its 33 plain facts were not moved out: they take 0.2 s in all. Its cost is the model tests themselves (23.7 s with
    models, at the end of Nlp.Tests), which is why Nlp.Tests takes 59 s with models and 36 s without. T3b (owner, 2026-10-29) lets the
    collection run beside the assembly's other tests (its own tests still one at a time): Nlp.Tests alone with models went from 53.5 s
    to 34.4 s in three green runs. It reverses M52's choice that these tests never run beside another test.
  - **Fast tier:** `scripts/test.sh fast` leaves out the fuzz suite (an assembly trait), the 17 model-gated facts, `CliTests`, `ProgramTests`,
    `LintTests`, `LintCommandTests`, `SpanishIdentityTests` and three long single tests; Nlp.Tests runs 2,313 of 2,455 tests in 11 s. The
    gate, CI and `scripts/test.sh full` run the whole suite at its full depth; no oracle changed.
  - **O15 is the fuzz suite's longest test** (52 to 55 s of 63 s). It already shares the lexicon and hard-links the speech; a cheaper
    equivalent with the same oracle needs `ContentLoader` to read an in-memory file source, a core API change (T6). The owner declined a shallower local
    fuzz pass (T5): the suite runs at its one full depth.

- **M54, the identity guard masks the bundle hash (2026-10-29, Opus 5.5; on `feature/content`, for the game's C series).**
  - **Found:** C01 added a `$schema` line to each of the game's files in `content/game-v1/game/`. The identity guard then failed on 12,698 of
    44,618 stage checks, every `grade` and every `pipeline`. Their verdicts carry the bundle's content hash in their version stamp, and
    `content/game-v1` had not changed since the guard was recorded at M31, so the coupling had never shown.
  - **Proof that no behaviour changed:** with the new hash replaced by the old one in each stage's JSON before hashing, the unchanged baseline
    passed (2 of 2 tests). The guard now masks `Bundle.ContentHash` (`<bundle-hash>`), and the baseline was recorded again in its own commit.
    The masked JSON of the old and the new bundle is the same text, so the masked baseline holds for both.
  - **The rule after M54:** an edit of `content/game-v1` that changes no Spanish behaviour moves no identity hash. A hash that moves is a real
    change, and the old Phase A rule stands (re-record only for a deliberate Spanish change, in its own commit). `scripts/identity-check.sh` still
    checks that the bundle hash is unchanged, which only Phase A asked for; it is not part of the gate.

- **M55, the manifest tests stop assuming game-v1 has no support language (2026-10-29, Opus 5.5; on `feature/content`, for the game's C03).**
  - `ManifestLanguageTests` (three tests) and `ManifestTests.TheManifestSerializesOneFilePerLineAndRoundTrips` read `TestSupport.Bundle.Manifest`
    as a manifest without `language` and `supportLanguage`. The game's C03 records `supportLanguage: en` in it (the core's M32 field, recorded
    and never read by the core).
  - The tests now take the example `with { SupportLanguage = null }`, and `WithoutALanguageTheHashIsExactlyTheOldCombineOfPathsAndHashes` checks
    that the hash is still the plain combination of paths and hashes with the support language present. No behaviour of the core changed.

- **M60, the lint analyses each example once per winning grammar (2026-10-31, Opus 5.5; merged to `feature/village` `95bd7bf`, for the game's PR #20).**
  - The cause: the confusion check asks the resolver about every example in a synthetic prompt of two intents, for each rival intent, and the
    lint's cache was keyed by the prompt, so each text was read, matched and completed about 33 times. Only the grammar match depends on the
    prompt; the readings depend on the text, and the completion (names, errors, the reading resolver, the checks) on the text and the winning match.
  - The analyzer's halves (`Read`, `Match`, `BestMatch`, `Complete`, the type `TextReadings`) are internal, and the public `Analyze` is exactly the
    three steps, so the pipeline is unchanged by construction. `LintEvaluationTests` (three tests) compares the lint's analyses with fresh ones:
    one text whose winning grammar changes between prompts, a word bound as a name in one prompt only, and every example in every prompt that
    allows its intent; a cache keyed by the text alone, one handing over another grammar's match, and one without a copy of the tokens each fail them.
  - Timings (the content tool, Debug, `lint --level a2`): 680 to 240 ms on the bundle before phase A, 1610 to 398 ms now; the process 2.2 s to 1.0 s.
    What remains is grammar matching, examples times grammars, about 185 ms in all; cutting it means changing `Language/SlotGrammar` and is the
    next lever if phase B grows the bundle a lot.
  - The timing test's clock started before it read `TestSupport.Bundle` (a lazy load of the 69 MB bundle); only the type initialiser of `LintTests`
    kept the load out of it. It reads the bundle first now; its budget stays 5 s. Tests: 6033 passed, 3 skipped (6030 before).

- **M61, the core's tests follow the game bundle's A2 ceiling (2026-10-31, Opus 5.5; merged to `feature/village` `808e0f0`, with the game's V08).**
  - R02 put the core's tests on `content/game-v1`, and two of them asserted a clean A1 for it with fail-on-level. The game's gate moved to an A2
    ceiling with a per-lesson level (D-game-22 (b), V08), so phase B's A2 lessons would have failed the core suite for content that is
    correct by the owner's rule. Only the level changed: A2 with fail-on-level must be clean, and A1 may report only `cefr.*` warnings. Tests on
    the core's own fixtures are unchanged.

- **M56, the agreement checks that were silent where a learner errs (2026-10-31, Opus 5.5 xhigh; merged to `feature/village` `d87e823`,
  before the game's V14).**
  - (a) `ser` where a place takes `estar` (`ser_estar`): `ser` with `lejos` or `cerca`, past up to two adverbs (`es lejos`, `es muy cerca`,
    `eran lejos` → `estaban`); `dónde` with a third-person `ser` and a definite noun phrase, agreed with the phrase (`¿dónde es la herrería?`
    → `está`). **An event's place keeps `ser`:** the core tells an event only by its noun, so the Spanish pack holds a closed list of event
    nouns (`fiesta`, `reunión`, `clase`, `boda`, `cena`…): `la fiesta es en la plaza`, `¿dónde es la fiesta?` and `¿dónde es?` alone stay
    clean. `de dónde`, `es lejano`, `cerca de las 3` and `fue lejos` stay clean too.
  - (b) A subject after its verb (`conj.person`): a third-person verb agrees with the noun phrase after it after `estar` (`¿dónde están la
    herrería?` → `está`), after `gustar`-type verbs behind a clitic (`me gusta las manzanas` → `gustan`, `me gustan el pan` → `gusta`),
    `quedar` after `dónde`, and `costar` or `valer` after `cuánto` (`¿cuánto cuestan el pan?` → `cuesta`). `cuestan dos monedas`, `hay dos
    panes` and the others stay clean. The new rules are in `SpanishAgreementChecker.Postverbal.cs`.
  - A false positive fixed on the way: a pronoun after a preposition is no subject (`a ella le gustan las manzanas` gave `conj.person`).
  - A reading change in the engine, through a rule the pack supplies (`IsVerbSeekingQuestionWord`, no Spanish in the engine): the word after
    `dónde` or `cómo` is the verb when it can be one, so `¿dónde son las casas?` gives `están`.
  - Tests: 82 in `PostverbalSubjectTests`; on the old code the 33 error cases gave nothing and the 4 preposition cases gave a false
    `conj.person`. The fuzz generator follows the new rules (it no longer counts `dónde están la herrería` as correct) and the `conj.person`
    mutation now covers a subject after the verb: widened, nothing narrowed, no new bug. `OldSentenceDigest` moved with the generator.
  - Baselines: the end-to-end goldens did not move; the Spanish identity baseline moved in 15 entries (7 wrong-Spanish fuzz sentences now
    `conj.person`, 8 a word's reading: `queda` after `dónde` and `hace` after `cómo` are now the verbs).
  - One game test pinned the old gap (`FarmLessonTests`, "a core gap, M56"): `me gusta las manzanas` at `c2-apples` is now the core's
    person slip (the step done with the slip, the core's recast only, no double feedback), and `me gusta la manzana` keeps the lesson's own
    person retry. Changed by a follow-up on the branch, the one game file, by the orchestrator's leave.
  - Tests: 6194 passed and 3 skipped before; 6276 passed and 3 skipped after. The orchestrator's CLI spot-check matched the report; one
    note: `cuestan dos monedas` at `baker-open` gives `vocab.wrong` on `cuestan` (no `conj.person`, as specified); whether that tag is
    older than M56 was not checked.
  - The game may now restore these `content/PHRASES.md` error examples (V14, deliverable 4): `esta-lejos` (`es lejos`; its intent stays
    `unknown` unless `ask_far` also takes `ser`), `donde-esta-la-herreria`, `me-gusta-el-pan`, `me-gustan-las-manzanas`, and `cuánto
    cuestan el pan` for `cuanto-cuesta` (`cuánto costa` waits for M58).
  - Left: `son` with no pronoun or `dónde` before it is still read as the noun (`son cerca`); `ser en` with a place is not checked; the first
    and second persons and the subjunctive after the verb are not checked.

- **M59, the person and tense checks the third person needs (2026-10-31, Opus 5.5 xhigh; merged to `feature/village` `12ea9d7`,
  before the game's V14).**
  - (a) A noun phrase that opens its clause is checked against the verb after it (`SpanishAgreementChecker.Preverbal.cs`): **only a
    third-person verb of the other number, after a phrase with a determiner or names joined by `y`** (`los niños come pan`, `las manzanas
    es roja` → `son`, `la manzana son roja` → `es`, `Luz y Domingo comió pan` → `comieron`).
  - **A lone name is never checked, in any person.** The first build flagged it, as the brief asked (`Mar pescaste en el río`). The
    orchestrator's CLI check found that this flags correct Spanish learners say all the time: `Luz quiero pan` at the baker gave
    `conj.person` "quiere", and `Domingo tengo madera` and `León quiero tres manzanas` the same. A name said to the listener is a vocative,
    and speech transcripts carry no commas. The brief was wrong; the agent changed the rule on the same branch. For the same reason no
    first- or second-person verb is checked after any noun phrase (`Luz y Domingo, queremos pan`; the object put first, `el pan quiero`).
    A retelling slip with a name (`Luz hice pan`) is the lesson's own person miss.
  - Known limit: an object put first with no clitic (`las manzanas vende Luz`) has the shape of `los niños come pan` and is flagged. It is
    rare and marked Spanish; left as is.
  - (b) `cómo` + a clitic + `llamar`/`sentir`: the verb agrees with the clitic (`cómo te llama` → `llamas`, `cómo se llamas` → `llama`).
  - (c) After `ir a`, a word that can also be a preposition is read as the verb, so `va a hace un hacha` gives `conj.tense`.
  - (d) `hace` is the preposition only before a phrase of time, the verb elsewhere, a grammar's literal `hace` included. The engine reaches
    the Spanish words through two new `IAnalysisRules` members (`IsElapsedTimePreposition`, `IsElapsedTimeNoun`).
  - Fuzz: one real bug found and fixed at the source (the accent-variant choice turned `te estas` into `té estás`; regression test, README).
    The generator follows the new rules; the noun-subject person mutation covers only a third-person verb after an article or a numeral
    and its noun. No oracle as it stood before M59 was narrowed. The sentence digest moved with the generator.
  - Game tests: `PeopleLessonTests` (that file only, by the brief's leave): `¿Cómo te llama?` is the core's slip; `Domingo voy…` and
    `Domingo trabajo…` stay the lesson's own misses.
  - Baselines: the end-to-end goldens did not move; the identity baseline moved in 86 entries, then 36 of them back to their hashes from before M59 with the lone-name rule.
  - Tests: 6276 passed and 3 skipped before; 6431 passed and 3 skipped after, by the gate (the agent reported 6418).
  - The game may restore `como-te-llamas` (`¿cómo te llama?`) and V13's `la manzana son roja` / `las manzanas es roja` at `lesson-apple`;
    `trabaja-en-la-herreria` and `va-a-hacer-un-hacha` keep the pronoun by design.
  - Found on the way (game, for V14): at the open prompts a line that starts with the listener's name resolves no intent and gets
    `vocab.wrong` (`Luz quiero pan` at `baker-open` → `unknown`). It is older than M59.

- **M57, the later future markers in the fuzz generator and `pasado mañana` after a plural noun (2026-10-31, Opus 5.5 xhigh; merged to
  `feature/village` `888f914`, before the game's V17).**
  - (c), first: the two `FuzzLanguageStreamTests` digests run on a frozen copy of their inputs
    (`tests/Vocaterra.Nlp.Fuzzing.Tests/Fixtures/StreamDigest`, 156 KB: the bundle's hand-written lexicon files, 1299 entries, and its 92
    grammars as at `0041eff`; the Wiktionary lexicon left out). A content edit no longer moves them; a generator change still does. A
    second test checks that `FuzzLanguage.Spanish` gives the same streams as the old generator code on the real bundle.
  - (b): `pasado mañana` is one time phrase wherever it stands (`compro panes pasado mañana` no longer gives `noun.number`). Found on the
    way: `la semana pasada` was never a past marker on the bundle's lexicon (`pasada` is also a noun, and the resolver chose it); a form of
    `pasado` after a time noun is now matched as written.
  - (a): the generator finds the multi-word markers in a grammar's literal words, past and future, and redraws a sentence whose chosen
    words made or broke a marker (never in 20 000 iterations). **The lists live in one public table of the Spanish pack,
    `SpanishTimeMarkers`, read by the checker and the generator** (public, because ADR 0012 shows the fuzz suite only the public API).
  - Tests: 15 cases in `NearFutureTenseTests`; before the fix the plural-noun cases gave `noun.number`, `voy a ir el domingo pasado
    mañana` a false `conj.tense`, and `la semana pasada` with a present nothing. The old generator fails O3 to O5 on the new fixture
    grammars, and O3 finds (b) on the old checker. No oracle narrowed. Digests set twice (frozen inputs; then the sentence digest for (a),
    in its own commit); no golden moved.
  - Tests: 6642 passed and 3 skipped before; 6659 passed and 3 skipped after.
  - For V17: `state_action` may take `pasado mañana`, `la semana que viene` (`mes`, `año`), `la próxima semana`, `el próximo día` (`mes`,
    `año`), `la semana pasada` and `el año pasado` as alternatives. The game's `luego` and `después` are not core markers.
  - **Open, for the owner:** an older O5 failure (seed `ff2aNwHuJBk8`, about one full fuzz run in eight, already on `0041eff`): the
    `word_order` mutation's guard reads the pattern's roles, not the words' readings, so it moves an adjective after a word that can
    itself be an adjective (`suelta plantar satisfecha`) and expects `word_order` the core rightly does not give. Fixing the guard narrows
    O4 and O5, which needs the owner's word. Until then a gate that fails there is rerun once, after checking the seed in the log.
  - Left to the game (V17's brief): `scripts/content-loop.sh` step 7, `scripts/README.md`, `content/README.md` and
    `docs/research/2026-10-31-development-bottlenecks.md` still say the digests move with content.
  - The orchestrator's CLI spot-check matched (`compro panes pasado mañana` clean; `pasado mañana compré panes`, `la semana que viene compré pan`
    and `la semana pasada compro pan` `conj.tense`; `compré pan la semana pasada` clean).

- **M58, two learner slips the core misread, and the `word_order` mutation's guard (2026-11-01, Opus 5.5 xhigh; merged to
  `feature/village` `78ce805`, before the game's V15).**
  - (a): the Spanish morphology says when a known word is also an irregular verb's form with the stem change missed. A word that is only a
    noun (`costa`, `costas`, `jugo`, `mente`, `mentes`, `cabo`) is read as the verb, with `conj.irregular`, where a verb is expected:
    after a subject pronoun or a clitic (`yo jugo`, `me costa`), after `cuánto` when the noun would not agree with it (`cuánta costa`
    stays the noun), after the noun of a phrase that opens the sentence (`el pan costa dos monedas`), or where a grammar binds it as the
    verb; elsewhere it stays the noun (`la costa`, `en la costa`). A word of another class (`solo`, `esto`; `podo`, `pode`, forms of
    `podar`) is read as the slip only where a grammar that matches nothing without it names the verb (`me pode ayudar` gives `puede`;
    `yo podo el árbol` stays clean). `quero`, `tenes`, `dormo` are not words and already gave `conj.irregular`.
  - (b): a word the lexicon lacks that is a noun in `l`, `r`, `n`, `d`, `z` or `j` plus `-s` (or a `z` noun plus `-es`) is that noun's
    plural with `noun.number` (`pans`, `flors`, `relojs`, `luzes`, `cancions`); loans (`robots`) and real unknown words are unchanged.
  - Found on the way and fixed: `hola Luz buenos días` gave `gender.agree` "buena" (older); a fixed expression after a noun is its own
    phrase.
  - (c), the owner's option 1: the `word_order` mutation's guard reads the words' readings (`MutationGen.WordOrder`,
    `CanOnlyExtendAPhrase`): it passes over a word that can be a descriptive adjective and cannot be a noun or a determiner, and makes no
    move when a word that can be a noun stands behind. Seed `ff2aNwHuJBk8` now skips that sentence and mutates one the analyzer flags
    (`WordOrderGuardTests`). The fuzz README lists it under the narrowed oracles with the decision and its date; two runs of the project at
    five times its depth passed (81 of 81 each).
  - The generators leave four noun lemmas out of their pools (`cabo`, `costa`, `jugo`, `mente`: a form of each is also a verb's slip), as
    `ratos` was before. No other oracle changed.
  - Tests: `LearnerSlipTests` 67 (the 29 slip cases failed before; the 38 clean neighbours passed before and after), `SlipDetectionTests`
    41, `FixedPluralExpressionTests` 9 (7 failed before), `WordOrderGuardTests` 3. 6771 passed and 3 skipped before; 6891 passed and 3
    skipped after, with models. No golden and no identity entry moved. `api.md`, `pipeline.md` and `lexicon.md` name the new cases.
  - For V15: the game may restore `¿cuánto costa el pan?` (row `cuanto-cuesta`) and `hay pans` (row `hay-pan`) in `content/PHRASES.md`.
  - Left: `podo`, `esto` and the like give the slip only under a grammar; an adjective's plural with the wrong ending (`azuls`); `los panes
    costa` is not graded.
  - **Open, for the owner:** an older O4 failure, found by M58's runs (once in six runs of the fuzz project; it fails the same way before
    M58): the `conj.tense` mutation lacks the guard M26b gave the `conj.person` mutation for a name in an alternative (seed
    `egVgLELrxyL4`: `ayer presiono abusar`, where `[ADV] [WORD:item]` binds `presiono` as a name, which is never checked). Giving it
    M26b's guard narrows O4 and O5, which needs the owner's word; the fuzz README lists it under "Found at M58 and left for a decision".
    Until then a gate that fails on that seed alone is rerun once, after checking the seed in the log.
  - The orchestrator's CLI spot-check matched: at `baker-open`, `¿cuánto costa el pan?` is `ask_price` with `conj.irregular`, `hay pans`
    `ask_availability` with `noun.number`, `¿cuánto cuesta el pan?` and `hay dos panes` clean; the core demo gives exactly `conj.person`
    and `noun.number`.

- **M62, the predicate adjective and the colour before its noun (2026-11-01, Opus 5.5 xhigh; merged to `feature/village` `65b04e6`,
  before the game's V15).**
  - (a), `SpanishAgreementChecker.Predicate.cs`: an adjective after `ser` or `estar`, past `muy`, `más`, `menos` or `tan`, in a comparison
    and joined by `y`, agrees with the subject: a subject pronoun before the copula (with its gender where it has one: `ella es alto` gives
    `alta`; `yo`, `tú`, `usted`, `ustedes` give number only), else M59's noun-phrase subject, else a determined phrase after the adjective
    (`es caro la manzana` gives `cara`), else the copula's number alone. A lone name is never given a gender (`Luz es alto` is clean, as
    M59 decided). `las manzanas son caro` gives `gender.agree` and `noun.number` with `caras`, the pair the attributive check gives. New
    reading rules: `son` and `era` between a determined noun and an adjective are the copula; the word after `muy` or `tan` is not a noun;
    an adjective-and-noun word after a determined noun is its adjective when it agrees with the noun and its determiners and has no finite
    verb reading.
  - (b): a colour that is also a noun (`roja`, `rojas`, `blanca`, `gris`) right before a noun it and its determiners agree with is the
    adjective, so `rojas manzanas` gives `word_order`; a list of nouns without its commas is not (`una rosa, pan y agua`). New pack rules
    `IAnalysisRules.IsPostnominalAdjective` and `IsDegreeAdverb`. `quiero tres rojas manzanas` at `farmer-open` gives `word_order` with
    intent `unknown`, as `azul espada` did; with `[ADJ]?` before `[NOUN:item]` in `request_item` it would fill the quantity and the item
    and still give `word_order` (a game choice, for V15).
  - Fuzz: no oracle narrowed. The `gender.agree` mutation was widened to a predicate adjective after a subject pronoun of known gender
    (`PredicateAdjectiveMutationTests`); the realiser learned the new rules (four skips, none narrowing), and the sentence stream digest was
    recorded again. Four bugs in the new rules were found by the identity baseline and O3 to O5 at five times the depth, and fixed with
    regression tests (the fuzz README's "Found and fixed in M62").
  - Tests: `PredicateAdjectiveTests` and `PrenominalAdjectiveTests`, 139 cases (51 mistakes gave nothing or less before; 75 correct
    neighbours passed before and after); three older tests take the new extra error (`ella está alto` twice, `las manzanas es roja`). One
    game test pinned the old gap (`FarmLessonTests`): `la manzana es rojo` at `c1-how` is now the core's `gender.agree` with the recast
    `roja`, the step done with the slip and no double feedback. Changed by a follow-up on the branch, the one game file, by the
    orchestrator's leave, as at M56. 6891 passed and 3 skipped before; 7031 passed and 3 skipped after, with models. No end-to-end golden
    moved; one identity entry did (`ella está alto`, now also `gender.agree` `alta`). `api.md`, `pipeline.md` and `cli-examples.md` name
    the new cases.
  - For V15: the game may restore `la-manzana-es-roja` (`la manzana es rojo`) and `es-grande` (`el pan es grandes`, `noun.number`) in
    `content/PHRASES.md`, and `quiero-tres-manzanas-rojas` (`word_order`) if `request_item` takes the adjective; `la manzana es muy caro`,
    `el pan es muy cara` and `la manzana es más barato que el pan` are graded.
  - Left: an adjective that is also a noun after the copula, with no grammar and no `muy` or `tan`, stays the noun (`él es alta` is
    clean); `son` after a lone name is a noun unless a grammar binds it; a subject with `de …` gets the number check only.
  - **Found at five times the depth, older than M62** (each analysed the same way at `76f52f8`; every run at the default depth passed; the
    fuzz README's "Found at M62, older than M62, and left for a decision"):
    - **For the owner**, with M58's `egVgLELrxyL4` (the same family): O4, the `conj.tense` mutation where the grammar's literal decided a
      reading (seed `2ldq2rIKLrG2`, `las los despego unas porque`: with `ayer` before it the grammar no longer matches and `porque` reads
      as the noun). A guard narrows O4 and O5.
    - **For the owner:** O5, the `conj.person` mutation after a noun subject and `haber` (seed `b3HSKSLCsL_1`, `el subtítulo ha
      peligroso` made `han`): M59 leaves `haber` out by design (`hay dos panes`). Either the mutation takes M59's exclusion (a narrowing)
      or the core checks the plural forms of `haber` against a singular noun subject (`el niño han comido`; a widening, core work).
    - **A core gap, filed as M63 (no owner decision):** O5 and O3, a subject pronoun before a question word (seeds `8zKtIhOY3951`,
      `c5SlpISRmTZ3`, `ellas adónde están un detenido lento`): M56's check of `estar` against the phrase after it starts its clause after
      `adónde` and gives `conj.person` `está`. The check should keep the pronoun before the question word (or the realiser model the
      clause opener, a widening).
  - The orchestrator's CLI spot-check matched: at `farmer-open`, `la manzana es rojo` is `describe` with `gender.agree`, `las manzanas son
    caro` `gender.agree` and `noun.number`, `el pan es grandes` `noun.number`, `quiero tres rojas manzanas` `word_order` with no intent;
    `la manzana es roja`, `el pan es grande`, `quiero tres manzanas rojas`, `Luz es alta`, `León es alto`, `usted es muy amable`,
    `ustedes son muy amables`, `una gran casa`, `la casa de Luz es grande` give no agreement or order error (only the older `vocab.wrong`
    on a word outside a prompt's answer set); the core demo gives exactly `conj.person` and `noun.number`.
  - **M62b, found by the gate of the game's V15 (merged `5f3450b`, same agent):** O3 failed once (CsCheck seed `aeG5w1c3Hvh2`, the
    random pattern `[ART]? [ADJ:item] ([WORD:qty]|[NOUN]|[ADJ] [VERB])?` gave `un japón Dejo`, and the analyzer gave `word_order` on
    `japón dejo`); it failed the same way at `e3b1848`, before V15. Older than M62 and no part of its colour rule: a tied parse keeps every
    reading, so the drawn name `Dejo`, also the noun `dejo`, is the head of the phrase after an adjective that follows its noun. The
    analyzer is right; the realiser re-rolls a drawn name that is a lexicon noun right after a determiner or a stand-alone adjective
    (`skip:name-read-as-noun`, the kind M62 used; no oracle narrowed). Regression `NameReadAsNounTests`; three default runs and one at five
    times the depth passed; nothing moved. 7034 passed and 3 skipped after.

- **M64, the core findings of the review of `feature/village` (2026-11-01, Opus 5.5 xhigh; merged to `feature/village` `23af5cd`, before
  the game's V19).**
  - (a), `SpanishAgreementChecker.Coordination.cs`: a subject pronoun joined by `y` or `e` to members before it that open their clause
    (M59's test) ends one plural subject. A first person gives 1pl; else a second person gives 2pl or 3pl (`ustedes`; both accepted, a
    correction proposes 3pl); else 3pl. A predicate adjective after it is plural, with a gender only when every member has one.
    - `CheckSubjectVerb` and `PredicateSubjectOf` read it.
    - A `y` after a verb or a preposition joins clauses, and the pronoun is checked alone: `luz hizo pan y yo fui al río` is clean,
      `luz hizo pan y yo fuimos al río` gives `fui`.
    - A list without its commas (`luz mar y yo`) is not checked, and a phrase with `de` is one member.
    - A verb that agrees with no accepted person is flagged with the coordinated form (`tú y yo fui` gives `fuimos`), as M59 flags
      `Luz y Domingo comió`.
  - (b) A phrase of plural cardinals before a singular `ser`, `fue` or `era` with a singular amount predicate (`mucho`, `poco`,
    `suficiente`, `caro`…, or a singular phrase with a determiner) is one sum: `diez monedas es mucho dinero` and `dos panes es suficiente`
    are clean, `dos niños es alto` still gives `son`.
  - (c) `IsApproximateTime` takes `del`: `es cerca del mediodía` is clean, `la tienda es cerca del río` still gives `está`.
  - (d) ADR 0009's M64 note lists M56 to M62's public surface (M64 adds none). `api.md` dates `IsVerbSeekingQuestionWord` to M34 and
    documents (a) to (c), and so does `pipeline.md`.
  - (e) `Array.AsReadOnly`. The compiler's type for those collection expressions was already read-only, so the review's risk was theoretical.
  - Fuzz:
    - The realiser models a coordinated pronoun subject where it can tell what the analyzer reads (`CoordinatedSubjectFuzzTests`): O3,
      O4 and O5 widened.
    - **One narrowing, at the owner's request to fix the review's findings:** M59's `conj.person` mutation after a noun subject is not
      made where the sentence opens with a numeral, the verb is `ser` going singular and an amount predicate follows
      (`AmountMutationGuardTests`). It is documented in the fuzz README, and is a little wider than the analyzer's rule.
    - Found and fixed at five times the depth, older than M64: `el ala es muy sincera` gave a false `gender.agree` (seed `clQQ50b0dLj2`;
      a feminine noun that takes `el`).
    - Found and left, older (it fails the same way at `951e7c8`): O5, a `word_order` recast where the grammar can bind the moved
      adjective as a name (seed `2tpLGUOSqj41`, at five times the depth).
  - Tests: 52 new cases failed before the fix (`CoordinatedSubjectTests`, `PreverbalSubjectTests`, `PostverbalSubjectTests`,
    `PredicateAdjectiveTests`, `SpanishTimeMarkersTests`), with the clean and still-flagged neighbours as tests too. 7259 passed and 3
    skipped before; 7364 and 3 after, with models. No golden or stream digest moved.
  - **The gate failed once, on O5,** with CsCheck seed `4YuZ2yubxdC1`: `unas televisiones han` mutated to `ha`.
    - It is the family of `b3HSKSLCsL_1`: M59's person mutation after a noun subject and `haber`, which M59's check leaves out by design.
      M64 does not touch it.
    - The seed reproduces. Five fresh runs of the test and one of the whole fuzz project passed (87 of 87), so the gate was taken as
      passed under the rule for the open seeds.
    - **The family now fails at the default depth,** not only at five times it, so a gate or a CI run can hit it. The owner's decision on
      it (the handover's section 4, item 2) is more pressing.
  - The orchestrator's spot-check:
    - the review's 99 lines give what they gave before, except the ten fixed ones;
    - at `lesson-we-went` through the host, `mar y yo fuimos al río` and the three others give no `conj.person`, and the recast keeps
      `fuimos`. They still get `vocab.wrong` (correctness 0.4) because the answer set has no subject; that is V19's;
    - the clause coordinations are clean, and the core demo is exact.
  - Left: `ni tú ni yo fuimos` and `tú o yo vamos` are still flagged (older; `ni` and `o` were outside the brief).
- **M65, fuzz stability (2026-11-01, Opus 5.5 xhigh; gated on `feature/village-followups` at `479eae4`; the owner's yes to (a) to
  (c), D-game-22).** The first follow-up after PR #20's merge, run in its own worktree with a lean build, beside two QA passes and V22.
  - **The three narrowings** (all in `Generation/MutationGen.cs`, each pinned by a fact in `NarrowedMutationGuardTests`: the failing
    pattern is not mutated, the analyzer shows why, and a close neighbour is still mutated and flagged):
    - (a) the `conj.tense` mutation shares M26b's guard (`NameBesideAnAlternation`), no wider than M26b; seed `egVgLELrxyL4`;
    - (b) M59's preverbal `conj.person` mutation leaves out `haber`, the analyzer's own exclusion; seeds `b3HSKSLCsL_1` and
      `4YuZ2yubxdC1`. The realiser still builds `haber` with no participle: dropping it now would lose O4's check of `ellos ha`, which the
      perfect will need. The README records the widening for when a lesson teaches the perfect;
    - (c) narrower than the handover's wording: "keep only a case that still matches its grammar" would have kept 46 of 6 002 tense
      cases, because `ayer` in front rarely fits. The match decides only a literal's accent reading (`porque`/`porqué`), so the guard asks
      for it only when a literal has an accent variant (237 cases skipped, 4 %); seed `2ldq2rIKLrG2`.
    - (d) `2tpLGUOSqj41` recorded, not narrowed (the owner).
  - **The skip budget:** the realiser counts every re-rolled attempt (`rejected:<reason>`); `SkipBudgetTests` (about 2 s, 2 000 fixed
    seeds each for O3 and O4/O5) fails when one reason passes 5 % of the draws. None does: the largest is `accent-variant-homograph`
    (4.21 % in O3, 3.70 % in O4/O5). `coordinated-subject`, `ser-of-place`, `predicate-subject` and `time-marker-of-generated-words`
    took no draws. The table is in the fuzz README.
  - **`scripts/try-lines.sh [--host] <file>...` and `--check`**, with `scripts/try-lines/review1.txt`, `review2.txt` (the review's 100
    lines) and `m64.txt`, and their CLI and host baselines. Its output equals the review's two wrappers on the three files.
  - **Runs:** the fuzz project (91 tests, 87 before) passed three times at the default depth (about 1 min 6 s each) and once at five
    times it (3 min 59 s). The gate: 7428 passed and 3 skipped before, 7432 and 3 after, with models, at the first run.
  - **New finding, older than M65:** seed `8yc522XLB_W2` (O4, `word_order`) fails the same way on `9dfa08b`. The mutation breaks the
    grammar match, so `rica`, which the grammar bound as a noun, reads as an adjective and the analyzer adds `conj.person` `desfiló`. The
    same family as (c) and (d); recorded in the fuzz README. Until it is decided, a gate failing on it alone is rerun once.
  - The orchestrator's spot-check: `scripts/try-lines.sh --check` on the merged branch gives the six baselines, and the core demo is
    exact.
- **M71, the O3 coverage flake (2026-11-01, Opus 5.5 xhigh; merged at `35ece1c`, gated with V23a and M66 at `53f63e6`).** Found at
  V23a's gate: O3's "the catalog grammar … was never realised" failed twice in a row with no core change.
  - **The cause was the draw, not the realiser** (the brief's guess was wrong): O3 picks a catalog grammar in 40 % of its 2 000 recipes,
    about 7 per grammar for 110 grammars, so some grammar is never drawn in about 7.5 % of runs (151 of 2 000 simulated runs, over 83
    grammars; 1 of 10 real runs). V15's and V17's family realises from every recipe it gets; its re-rolls are all made good.
  - **The fix:** after the random sample, each grammar it missed is realised from up to 50 fixed recipes (`M71T00000000` on) through the
    same checks; a grammar none of them realises still fails, with its rejections listed (`TheCoveragePass_ReportsOnlyAGrammarTheRealiserCannotRealise`).
    The coverage is now asserted at every depth (before, from 1 000 recipes). The realiser and the skip shares are unchanged. O3 alone
    passed 10 runs of 10; the project passed three times at the default depth and once at five times.
  - **New finding, not fixed:** the realiser never draws `buenas tardes`, which the analyzer accepts (`tardes` is a form of `tardar`, so
    `PreverbalPhrase` takes `buenas` as a subject and re-rolls it as `literal-verb-subject`). Filed with M69.
- **M66, the grading gaps of sessions 7 to 10, with M63 (2026-11-01, Opus 5.5 xhigh; merged at `53f63e6`, gated with V23a and M71).**
  Run beside V25 and V23a in M65's worktree, from M65's last commit. 63 new cases in `tests/Vocaterra.Nlp.Es.Tests`, each failing
  before the fix.
  - **False alarms gone:** `¿puede usted vender manzanas?` (a pronoun right after a finite verb that agrees with it is its subject; an
    infinitive after it is not checked against it; `usted vender manzanas` and `yo comprar pan` still flagged); `ni tú ni yo fuimos` (`ni`
    plural) and `tú o yo vamos` (`o`: the plural or the nearest member's form; the gender after a two-reading `o` is not checked; a mix
    of `y` and `o` is not checked); `cada manzana` no longer gives `word_order`.
  - **Errors now caught:** a finite verb after a modal gives `conj.tense` with the infinitive (`puedo pago` `pagar`, `quiero vendo`
    `vender`, `tengo que voy` `ir`, `debo como más fruta` `comer`). Homographs: after `poder` and `tener que` a noun/verb word is the
    verb; after `querer`, `deber` and `necesitar` only before a word that can only be a noun, so `quiero vino`, `necesito pago` stay
    clean; `como` is the verb only before `más` or a determiner; a new clause after a modal (`no puedo estoy cansado`) is not checked.
    `¿cuánto cuestan todo?` gives `cuesta`, `¿cuánto son todo?` `es`; `las manzanas cuesta una moneda` gives `cuestan` (QA-language-3:
    `cuesta` was read as the noun); `los panes costa` gives `cuestan` and `cuesta`; `azuls` gives `noun.number` `azules`; `ella qué
    quieres` gives `quiere` (M63; seeds `8zKtIhOY3951` and `c5SlpISRmTZ3` pass). `ti` added to `lexicon/supplement.json`; bundle
    `356a257e…` (`save-v1.json` and `save-v4.json` took the hash).
  - **Public surface:** `IAnalysisRules.IsModalVerb` and `ModalTakesNounObject`, with defaults; ADR 0009 has a note, `api.md`,
    `pipeline.md` and `lexicon.md` are updated.
  - **Fuzz, widened only:** the realiser builds `ni`/`o` subjects and a pronoun before `qué`; the person mutation works through a
    question word; a new O4/O5 case makes the infinitive after a modal finite; the verb after `tener que` is an infinitive; two re-rolls
    broader than the analyzer (`modal-complement-homograph`, `noun-reads-as-verb`); `ModalAndQuestionFuzzTests`; the stream digest
    re-recorded. One fuzz finding fixed (`c5E0ikVTT_s3`: `podamos` is `podar`'s, not a modal). 93 tests, four runs passing.
  - **Goldens:** none moved. Six identity entries moved, each checked by the orchestrator: `mi madre cocina`, `la voz programa`, `el
    aniversario toma` (verbs now), `necesitamos ruedas` (the noun), and two "correct" corpus sentences that are not Spanish and are now
    flagged (`14 alienígenas entrega` `entregan`, `aquellas vacaciones amenaza emocionado` `amenazan`).
  - **The orchestrator's checks** on the merged tree, through the host: `las manzanas cuesta una moneda` `cuestan`, `puedo pago con fruta`
    `pagar`, `¿cuánto cuestan todo?` `cuesta`, `ni tú ni yo fuimos al río` clean, `tengo pan para ti` with no unknown word;
    `scripts/try-lines.sh --check` gives all eight baselines.
  - **Known limit:** `necesito ayuda domingo` (a name said to the listener) would be flagged on the CLI; the host takes the name out.
- **M67, what a constrained prompt's answer set tells the verdict (2026-11-01, Opus 5.5 xhigh; gated on `feature/village-followups`
  at `6b922d7`).** Run beside V20, in M66's worktree from M66's last commit; gated from that worktree on the feature branch.
  - **(a)** `VerdictAssembler.DeriveVocabWrong` adds the tag only when the line resolved no intent, or when a keyword rule (never a slot
    grammar) understood it and it is a near miss (the distance to the nearest answer within the fuzzy budget for the line's own length;
    `cuál es esto` keeps it). Understood lines lose their correctness 0.4: 27 rows of the line files changed, each only losing its
    `vocab.wrong` (`hola luz quiero dos panes`, `los panes cuestan dos monedas`, `buscaré madera para luz`, `me llamo domingo`, …); no
    line that was not understood changed. Two content examples of `intents.json` (`ayer voy a comer fruta`, `mañana comimos fruta`)
    dropped the tag (bundle `ebbf17f3…`, the save fixtures took it). The identity baseline moved in 306 entries, the grade and pipeline
    stages only; no golden and no game test moved. O10's model follows the new rule.
  - **(b) parked as `M67b-answer-verb-forms`** (pushed, not merged): `AnswerVerbForms` turned a lesson answer that differs from the
    nearest answer in one verb's form into `conj.tense`/`conj.person`. It failed 32 game tests, which pin V14's design: at a lesson step
    the game itself makes `Luz compra pescado` a miss with a tense retry and the hint «compró»
    (`YesterdayLessonTests.ThePresentAtAPastStepIsAMissWithATenseRetry`). QA-language-5 and 10 were seen on the host's `--transcript`
    path, which runs no lesson; in the game they are already handled. M67b would change the design, so it waits for the owner's word.
  - **(c)** `scripts/limited.sh` is re-entrant: under `VOCATERRA_LIMITED=1` it runs its command directly.
  - **Runs:** the fuzz project four times on M67 (70 s, 60 s, 60 s, 208 s at five times). On M67b's widened O4, two analyzer findings
    (filed with M69 if they reproduce without it): O4 `3isZGy3IeUc8` (`vaya sentir`), O5 `deXmFwx4GzL4` (a recast keeps a gender
    error). The gate: 7603 passed and 3 skipped before, 7612 and 3 after, with models, at the first run.
  - The orchestrator's checks on the merged tree, through the host: the four understood lines above are clean, `tengo pan para ti` (not
    understood) keeps `vocab.wrong`, the core demo is exact, and `scripts/try-lines.sh --check` gives every baseline.
- **M70, the core findings of the QA pass on the village (2026-11-02, Opus 5.5 xhigh; gated on `feature/village-followups` at
  `f1a02d3`).** Run beside V20 and V21 in M66's worktree; V20 merged in before its final checks.
  - **Typed `n` for `ñ`** (the owner's answer): `Lexicon.Lookup`'s accent-insensitive fallback also tries the unmarked spelling through a
    new `ILexiconFolding.UnmarkedSpellings` (none by default). A spelling that is itself a word keeps its reading (`una`, `pena`, `mono`,
    `suena`); the Spanish pack keeps a closed list of real words the game's lexicon lacks (`ano`, `cana`, `cono`, …) that are never
    folded. `manana compraré pan` is clean (it was `vocab.unknown`, 0.6).
  - **A tense-marking accent typed without it** (the owner's answer): the public record `AccentNote(Span, Written, Accented, Marks,
    Reading)` on `PipelineResult.Notes`, `UtteranceAnalysis.Notes` and `AnalysisResult.Notes` (null when none, left out of the JSON, so
    verdicts and replay are unchanged). From the analysis (a tense error alone that the accented form of the same person removes: `ayer
    compre pan` is clean with a note) and from the answer match (`compre dos panes`, `luz compro pescado`). A person error stays an error
    (`ella compro pan` gives `compra`). V21b reads `result.Notes ?? []` and shows `SpanishAccentNotes.Describe(note)` («compré», con
    tilde: es pasado). ADR 0009 and `api.md` updated.
  - **`gustar` and `encantar`** agree with the thing liked: `me gusto el pan` gives `conj.person` `gusta`; `yo gusto el pan` gives
    `conj.person` over both words with `me gusta` (the recast says «me gusta el pan»); `me gustas tú` and the other neighbours stay clean.
  - **An unknown word takes the answer's word at its own place** (`AnswerCorrections` aligns the line with the nearest answer phrase;
    the recast is the core's `RecastBuilder`): `compré three manzanas` gives `tres` and «compré tres manzanas». One test moved: `buen
    dia` takes `día`, not `días`.
  - **The preterite's spelling slips** `buscé`, `pescé`, `llegé`, `empezé`, `pagé`, `jugé`, `tocé` give `conj.irregular` with the right
    form (M58's machinery). Limit: the unaccented `busce` is not taken as the slip.
  - **A learner's own name:** the cause is content (no `ask_name` grammar binds a name); V21 adds `me [VERB:llamar] [WORD:name]`.
  - **An error on a number** reports the learner's word (`dos`), not its digits.
  - **Fuzz, widened only:** an O3 check for `ñ` typed as `n`; the person mutation makes first- and second-person `gustar`; the
    irregular mutation makes `buscé`; the realiser re-rolls `nosotras gustamos los…` (it drew it as correct); the stream digest
    re-recorded. Four runs passing before the merge, one after. M67b's O5 seed `deXmFwx4GzL4` reproduces (filed with M69); its O4 seed
    `3isZGy3IeUc8` does not.
  - **Baselines:** the line files' baselines re-recorded after V20's merge (`d67d991`): 47 rows per path moved, all V20's (lines at
    newly opened prompts now resolve an intent); M70's own changes are in `m70.txt`. The identity baseline moved in 318 entries (317 the
    number word, 1 `encantar`); no golden moved. Tests: 7682 passed and 3 skipped before, 7867 and 3 after, with models; the gate passed
    at the first run.
  - **History:** the commit `3bbfde0` does not build on its own (it names a type the next commit adds); a force-push to fix it was
    refused, and the tip builds.
  - **The orchestrator's checks** on the merged tree through the host: the lines above as stated, and `try-lines --check` gives every
    baseline. Found: `compré three manzanas` at `lesson-market` answers with the recast's «¡Muy bien!» and then «¡Qué bien!» in one turn
    (to V21's one reply per turn).
- **M69, the left points of the core's village milestones (2026-11-02, Opus 5.5 xhigh; gated on `feature/village-followups` at
  `76d3658`, 8119 passed and 3 skipped, with the models).** Every item a missed error or a fuzz finding; no correct line flagged.
  - **M56's left points:** `son` and `era` before `lejos` or `cerca` read as the copula (`IAnalysisRules.IsLocationAdverb`): `las casas son
    cerca` gives `ser_estar` `están`. `ser` before `en` and a place gives `ser_estar` (`la tienda es en el pueblo` → `está`, `yo soy en
    casa` → `estoy`), clean after an event (the list gained `visita`, `entrega`, …), a lone name, no subject, a time, a manner or an
    idiom (`en serio`, `en monedas`). "The first and second persons after the verb" read as a subject pronoun after a question's verb
    (`¿dónde estás yo?` → `estoy`, `¿cómo estás usted?` → `está`) and `estar` or a price verb in the 1st or 2nd singular before an
    article phrase (`¿dónde estás la herrería?` → `está`); ordinals left alone (`¿dónde estuviste la tercera?` is right; the identity
    baseline caught it).
  - **M58's left point:** `IAnalysisRules.CallsForSlip`: `yo podo ir` → `puedo` (`podar` takes no infinitive), `yo esto cansado` and
    `esto cansado` → `estoy`; clean: `esto es pan`, `yo esto no lo sé`, `yo podo el árbol`.
  - **M62's left points:** `IsPredicateAdjective` (a closed list of common adjectives): `él es alta` → `alto`, `la casa es bonito` →
    `bonita`; clean: `ella es médico`, `el pan es comida`. `luz son alta` → `conj.person` `es`. A `de …` subject gives the predicate its
    head's gender (`la casa de los niños es pequeño` → `pequeña`).
  - **Question words:** `cuanto son todo` → `es`, the recast restoring the accent; `cuántos cuestan todo` → `noun.number` `cuánto` and
    `cuesta`; `¿cuántos son?` lost a false `noun.number` (`sones`). Left: `cuántas cuesta todo` is still read as a noun phrase (as before).
  - **Fuzz:** the realiser draws `buenas tardes` (O3 widened, a test pins it). O5 `deXmFwx4GzL4` fixed at the source (`RecastBuilder`:
    the numeral 1 ignored its noun behind a possessive). O3 `2cqJbc9y05h3` (found by V21's merge fix, M70's `ñ` fold gave `danó` the
    readings of `daño` and `dañó`): an accent the learner typed now picks the form that has it. Two older bugs found by its runs and
    fixed: `gustamos` after `ayer` corrected to `gustan`, not `gustaron`; the realiser drew `21 podo pasar` as correct (re-rolled now).
    Widened: two skeletons, `es en` for the `ser_estar` mutation, the `gender.agree` mutation against a `de` subject; nothing narrowed;
    the sentence digest re-recorded. Three runs at the default depth and one at five times: the five-times run met `fSMdah3kOSd7`, a new
    seed of the recorded `2tpLGUOSqj41` family (`word_order`, a `[WORD]` slot binds the moved adjective), recorded in the fuzz README;
    its rerun and a last default run passed.
  - **Proof:** `try-lines --check` gives every earlier file unchanged on the CLI and the host; `m69.txt` moved in 40 rows a path, all
    its listed mistakes. The identity baseline moved in 6 entries (all `dónde estoy el mar`-type non-Spanish sentences); no golden moved;
    the core demo unchanged. Tests: Nlp.Es 1997 → 2225, Nlp 3146 → 3149, Fuzzing 95 → 98, Golden 17; the game's tests unchanged.
  - **The orchestrator's checks** on the merged tree (CLI and host), 30 correct lines of its own: no grammar error on any (`el agua es
    fría`, `el hacha es pesada`, `el hijo de la panadera es alto`, `la fiesta es en la plaza`, `la clase es en la escuela`, `esto es
    pan`, `buenas tardes`, `¿cuántos son?`, …); the nine mistakes above graded as stated. Seen again, older (V17's point): at a
    constrained open prompt a correct line with a word outside its answer set gets `vocab.wrong` and is not understood (`la tienda está en
    el pueblo` or `domingo es alto` at `farmer-open`).
- **M72, a correct line that the prompt does not take (2026-11-02, Opus 5.5 xhigh; gated on `feature/village-followups` at
  `4af6bd7`, 8199 passed, 3 skipped, with the models).** The owner's option 2 (D-game-22, 2026-11-02); V27 builds the game's half.
  - **(a) The derived `vocab.wrong`** (`VerdictAssembler.ExplainsAMiss`): a line with no intent gets it only at a prompt that names the
    word (an answer set of one answer, or a quiz) or when it is a near miss of an answer. `la tienda está en el pueblo` at `farmer-open`,
    `tengo dos manzanas` at `fisher-open` and `me gusta el río` at `smith-open` have no error and stay not communicated (each was 0.4 with
    an empty expected word). `cuál es esto` keeps its tag; a line a slot grammar understood never gets one.
  - **(b) `IntentResolution.OffPromptIntent` and `Verdict.OffPromptIntent`** (`IntentId?`, the last parameter, left out of the JSON while
    null, in the hand-written `Equals` and `GetHashCode`): when the allowed intents resolve nothing, `IntentResolver` runs the grammar and
    keyword rungs over the whole catalog (no embedding). The verdict carries it only when the intent is unknown and no answer matched, so
    never on a communicated line. The CLI prints an `off-prompt:` line; `api.md` (with a table) and ADR 0009 are updated.
  - **(c) `ask_activity`'s keyword rule** is `qué` with `hacer` (it was `hacer` alone): `hace frío`, `hace calor` and `hoy hace sol` are
    not `ask_activity` at any of the 70 prompts; `¿qué haces?` still is. Side effect: `hace dos días compré pan` at `smith-open` is now
    `state_action`. Bundle `a2075569…`; both save fixtures follow.
  - **Moved:** try-lines 37 CLI and 33 host rows (35 and 31 only lost the derived tag, 2 each are (c)); every near miss kept its tag. The
    identity baseline moved 4007 of 6688 entries, in the grade and pipeline stages only, each checked against M67's rule re-assembled
    (2399 lost the tag, 1427 lost it and gained an off-prompt intent, 181 only gained one). No golden moved. Two game tests changed their
    expected value only (`LearnerRecordTests.AnErrorSpanMarksAWordUsedButNotCorrect`, `SpeechUxTests.ATypedLineTheCoreDidNotUnderstand…`;
    their names no longer say what they assert, V27 renames them).
  - **Fuzz:** O10's model and generator follow the rule; O7 checks the new member against the resolver run with every intent allowed; O3
    gains a verdict leg (a correct sentence's verdict holds no error the analyzer does not find in its words with no prompt's grammar).
    The leg admits a near-miss `vocab.wrong` only where its own copy of M67's rule holds, and counts it (about 1 in 2,000): a correct
    sentence can be a near miss of an answer (`rápido por favor`, `pasado mañana iré al río con Mar`). Default depth: three clean runs
    and the full suite. Five times the depth: old findings only, O5 `3-VvCuMLFEA2` (a new seed of the recorded word-order family) and O3
    slots `92moRWz9NQ54` (the grammar does not yield `item=suyo`); M72's own oracles passed alone at five times.
  - **Found, none in code M72 changed (for M73 or the owner's list):** O2 `7HIoK0InC7l7` (`conj.person` twice on one word); M67's
    near-miss rule tags correct Spanish (above); `dos panes es caro` has no error (a numeral subject is not checked); `pedro` gets
    `vocab.unknown` (no name slot); `ustedes trabajan mucho` is `ask_activity` (the `trabajar` rule); `hoy hace buen tiempo` is off the
    prompt as `ask_craft`; `cómo está usted` is not understood.
  - **The orchestrator's checks** through the host: `me gusta el río` at `smith-open`, `tengo dos manzanas` at `fisher-open`, `el río es grande` at `baker-open` and `la tienda está en el pueblo` at `farmer-open` are unknown, not communicated, correctness 1, no error (the game still says «No te entendí» until V27); `hace frío` at `smith-open` is no longer `ask_activity`, `qué haces` still is; `quiero dos panes` at `baker-open` unchanged; the core demo gives exactly `conj.person` and `noun.number`. Seen, older (M67): `cuál es esto` at `lesson-what` keeps `vocab.wrong` with an empty expected word and the step praises it as «Cuál es esto».
- **M73, the small core points after the follow-ups (2026-11-02, Opus 5.5 xhigh; gated on `feature/village-followups` at `59d4044`,
  8265 passed, 3 skipped, with the models).** Bundle `fa2c8825…`.
  - **(a) `cuántas cuesta todo`:** `ReadingResolver` reads `cuesta` as `costar` after a form of `cuánto` before a neuter pronoun
    (`IAnalysisRules.IsNeuterPronoun`, with a default); the agreement check grades the question word: `gender.agree` and `noun.number`,
    both `cuánto` (it was `noun.number` `cuestas`). `cuánto cuesta todo`, `cuántas cuestas hay` and `la cuesta es grande` stay clean;
    `cuántos es todo` stays clean as M69 left it.
  - **O2 `7HIoK0InC7l7`:** a verb the person check matched to a subject pronoun before it gets no second subject after it (it gave
    `conj.person` twice).
  - **`dos panes es caro`:** the cause was M64's one-sum rule counting `caro` as an amount; a price is one sum only when the amount is money
    (`diez monedas es caro` stays clean). It gives `son` and `caros`; the fuzz guard follows (a widening).
  - **The intent catalog:** `ask_activity` takes `trabajar` with `qué`, `dónde` or `cuál` (`ustedes trabajan mucho` and `¿trabajas
    mucho?` lose it; `¿qué haces?` and `¿en qué trabajas?` keep it); `ask_craft` takes `hacer` with `cómo` or `querer`, and a grammar
    `[VERB:poder] [VERB:hacer] [ART] [NOUN:item]` (the weather is out; the lessons' craft questions keep it; bare `hacer una antorcha` and
    `hago pan` lose it); `greet` has a grammar for `cómo estás/está/están/estáis` (the NPCs answer it as `hola`: «¡Hola! ¿Qué buscas?»).
    The identity baseline moved 9 entries; no golden moved.
  - **Fuzz:** `26dVDqElv1l2` (the noun `son`), `8yc522XLB_W2` (`rica` drawn as a noun after `joya unida`) and `92moRWz9NQ54` (`suyo`
    drawn for an alternative a pronoun before it takes) are fixed in the realiser: a reader takes each as the analyzer does, so the draw is
    re-rolled (`HomographRerollTests`; documented as generator fixes). **Still recorded:** `2tpLGUOSqj41` with `fSMdah3kOSd7` and
    `3-VvCuMLFEA2`: the `[WORD]` slot rightly binds the moved adjective as a name (`Clara`), and the only fix would narrow the mutation.
    Three default runs and one at five times passed.
  - **Found, older (for the owner):** `qué tiempo hace` is `ask_activity` (M72's `qué` with `hacer`); `hace calor` and `hoy hace sol` are
    `state_action` through a lesson's `[VERB:hacer] [NOUN:item]` grammar. A weather intent, or a way to rule a lemma out, is the owner's call.
  - **The orchestrator's checks** through the host: the lines above as stated; `ustedes trabajan mucho` and `hoy hace buen tiempo` at
    `smith-open` are now «No te entendí» (no intent of the catalog takes them); `cómo hago un hacha` is off the prompt as `ask_craft`; the
    core demo gives exactly `conj.person` and `noun.number`. **Older, M67's near-miss rule:** `quiero hacer un hacha` at `smith-open` is
    `state_want` by a keyword rule and a near miss of an answer, so it gets `vocab.wrong` with an empty expected word (0.4) while Domingo
    praises it («Claro, «quiero hacer un hacha». Muy bien.»): the clearest case of the rule tagging correct Spanish (M72's finding), for the
    owner with M67b.

- **M76, checks as C# tests (2026-11-02, Sonnet 5.5; gated on `feature/village-followups` at `523f818`, 8797 passed, 3 skipped,
  with the models).** The owner's word (D-game-22, 2026-11-02): "I would prefer things like this to be a csharp unit test, not some
  throwaway script or junk text file." In PR #28, before the merge.
  - **(a) `scripts/try-lines/` as tests:**
    - The 540 rows of its 9 line files are 502 unique (prompt, line) pairs. 496 give the same result through the CLI and the host.
    - 20 rows were dropped because `OffPromptLineTests` already asserts them in full.
    - The core keeps 482 cases, in `tests/Vocaterra.Nlp.Tests/Pipeline/Lines/`: one `LineGrading` helper (the CLI's eval path) and one
      class per topic. Tenses has 93, correct statements 78, gender and number 77, person agreement 55, questions 43, prices and amounts
      39, lines not taken 39, greetings and names 35, vocabulary 12, and `ser` and `estar` 11.
    - The host keeps 28 cases in `tests/Vocaterra.Host.Tests/LineHostPathTests.cs`: the 6 rows whose host result differs, and 22 with a
      greeting or the listener's name.
    - Each case is a `TheoryData` row whose failure names the prompt and the line. All pass, and no row disagreed with an existing test.
      The agent proved the tests bite with one changed word and one changed intent, both reverted. They take about 2 s in all (not
      `Slow`).
    - `scripts/try-lines.sh` and its 27 files are deleted. `contributing.md`, `scripts/README.md` and the READMEs follow. The history in
      the plans, the briefs and the handovers stays.
    - Some topic placements were made by a script and are debatable; Tenses and correct statements are broad.
  - **(b) The audit of `scripts/`:**
    - `check-links.sh` became `MarkdownLinkTests` (`tests/Vocaterra.Nlp.Tests/Documents/`, skipping the build output, the models, the
      worktrees and the gate logs). It found 2 broken links in `docs/nlp/tools.md` (to `workflows-pending/ci.yml`), now fixed; the
      "parked workflow" text around them is still stale.
    - `identity-check.sh` (phase A's rules against `f4694ff`) and `core-move-scan.py` (R16 phase 2) were obsolete, and are deleted.
    - Kept as tooling: `test-timings.py`, `playtest-report.sh`, `scripts/playtest/`.
    - **Proposed, not built:**
      - `audit-usings.py` becomes IDE0005 as a build warning (about half a day, the build's configuration);
      - the scripted runs (`scripted-runs.sh` with `compare-runs.py`, used by the QA agent and `scripted-runs.md`) become game tests
        (3 to 5 days).
- **M77, unused usings as a build warning (2026-11-02, Sonnet 5.5; gated on `feature/village-followups` at `7bcaefd`, 8797 passed,
  3 skipped, with the models).** The owner: "do the IDE0005 warning too", after M76's audit.
  - **The settings:**
    - `.editorconfig` sets `dotnet_diagnostic.IDE0005.severity = warning`.
    - `Directory.Build.props` turns on `EnforceCodeStyleInBuild`, and `GenerateDocumentationFile` for every project: the compiler reports
      IDE0005 only with the documentation file.
    - Outside the core's `src/` libraries, the XML-doc warnings that the file brings are muted: CS1591, CS1570, CS1573, CS1574 and CS0419.
      The core's libraries keep theirs.
    - No `.csproj` changed.
  - **The first build** reported IDE0005 410 times. No other IDE rule fired: the file-scoped namespaces were already clean.
  - **240 unused usings removed in 179 files**, and only `using` and blank lines (checked by the orchestrator on the diff).
  - **The proof:** an unused using added to a core file and then to a game file each failed the `-warnaserror` build (reverted).
  - **The cost:** a clean build went from 34.6 s to 40.4 s (about 17 % slower). CI inherits the rule with no workflow change.
  - **`scripts/audit-usings.py` is deleted;** the R16 documents carry a dated note.
- **M74, the weather intents and `ask_wellbeing` (2026-11-04, Opus 5.5 xhigh; gated on `feature/village-decisions` at `7ce037c4`,
  8957 passed, 3 skipped, with the models).** The owner's items 1, 3 and 5 of 2026-11-02 (D-game-22); V29 builds the replies.
  - **The intents:** `state_weather` (slot `weather`, the words after `hace`: `frío`, `buen tiempo`; also `llueve`, `nieva`, `está
    nublado`, `¡qué frío!`, by tense), `ask_weather` (`¿qué tiempo hace / hará / hacía?`, `¿cómo está el tiempo?`) and `ask_wellbeing`
    (the four `cómo` forms and `¿qué tal?`, also after `hola`), taken out of `greet` and allowed at the 8 prompts that allow `greet`.
    `hola, ¿cómo estás?` is `ask_wellbeing`; `cómo estás usted` keeps `conj.person`.
  - **The resolver:** a grammar of an intent the prompt does not allow takes the line when it beats the best allowed grammar in the rung
    order and names more of the line's words; the line is then unknown with that intent off the prompt. So `hace calor` is never read
    through `[VERB:hacer] [NOUN:item]`. The DSL gained `(a|b c):slot`; the matcher skips grammars that cannot match (a bundle-wide test
    proves it never skips a match); `reach.intent` is info for an intent read only off the prompt.
  - **Left as they are, with reasons:** `¿qué tiempo hace?` at `smith-open` and `farmer-open` stays `ask_activity` (a keyword rule)
    until V29 allows `ask_weather` there; letting outside grammars beat allowed keyword rules moved 75 identity entries and broke
    `sí, gracias`, `dónde encuentro madera` and `quiero hacer un hacha`, so it was reverted. `hace dos días compré pan` is `state_action`
    with `item=pan` (the `comprar` rule; "ago" is neither weather nor `hacer`), against the brief's "neither": correct Spanish keeps
    its reading (accepted by the owner, 2026-11-05; `docs/nlp/decisions.md`). `¿hace frío?` is `state_weather` (a transcript has no question mark).
  - **Bugs:** `hace dos días compré pan` took `item=día` (fixed in `AnalysisSlotFinder`); with `ask_wellbeing` out of `greet` the NPCs
    said «No te entendí» to `¿cómo estás?` (no game test caught it; `ask_wellbeing` twins of the `greet` transitions in
    `content/game-v1/agents/*.json` until V29). The fuzz suite found two of the milestone's own (a weather grammar giving `mañana hacía
    frío mañana`; the generator's group slots). **The first gate failed O3** at seed `8vYtnrmXN4e2`, `cuánto sois cómoda`: an older
    generator bug (the same recipe gives the same sentence on the base commit; M74's grammars only moved the draw). The generator now
    re-draws a noun after a finite `ser` or `estar` that is also a predicate adjective (`noun-reads-as-predicate-adjective`); the
    analyzer and the oracle are unchanged.
  - **Moved:** 10 grading rows `greet` to `ask_wellbeing`, 4 `state_action` to off-prompt `state_weather`, 80 new rows
    (`LineWeatherAndWellbeingTests`); 5 of 6,698 identity entries; no golden. Bundle hash `9ebec412…`. The lint takes about 2.4 s
    (was 1.7 s). The orchestrator's spot-check: the demo line unchanged; `hace frío` off the prompt, `¿cómo estás?` `ask_wellbeing` at
    `villager-open`.
- **M75, the near-miss rule (2026-11-04, Opus 5.5 xhigh; gated on `feature/village-decisions` at `4d7b2c7e`, 9019 passed, 3
  skipped, with the models).** The owner's item 4 of 2026-11-02: a near miss must name its word.
  - **The rule:** `AnswerNearMisses.Find` (`Language/Matching/`) takes the answer phrases nearest the line, measured as `AnswerMatcher`
    measures, in its order; the first with the same number of words, exactly one word different (without accents) and a slip pair
    wins. A slip pair is a spelling slip within the answer word's own budget (the smaller of the spelling and phonetic distances), two
    question words (`IAnalysisRules.IsQuestionWord`, new; Spanish lists `qué`, `cuál`, `quién`, `dónde`, `adónde`, `cuándo`, `cómo`,
    `cuánto` and their forms), or two words of one closed class (`art`, `prep`, `pron` in the lexicon). `VerdictAssembler.DeriveVocabWrong`
    tags that word with the answer's word as `Expected`, under M67's other conditions; M67's distance rule (`IsNearMiss`) is gone. New
    public types `AnswerNearMiss` and `SlipPair`; `VerdictInputs` gains an optional `NearMiss`.
  - **The PR #28 review's item 8:** a set with no answers gives no near miss (tested).
  - **Moved:** `quiero hacer un hacha` (smith-open), `pasado mañana iré al río con Mar`, `rápido por favor`, `buenas tardes luz` and
    `¿cuántos son todo?` lose the tag; `cuál es esto` keeps it, now naming `qué` on `cuál`. 3 of 6,698 identity entries (all lost the
    tag); no golden, no game test. O10's model and generator follow the rule, O6 gained a model of the finder, O3's verdict leg is
    strict again (9,596 graded sentences at 5× depth, none tagged). `docs/nlp/cli-examples.md` had two stale examples since M67/M72.
  - **Known, not fixed:** the core's recast ignores the derived tag, so `cuál es esto` is still praised («¡Eso es!») at 0.4 (the game's
    own error lines read `Expected`): taken into M78. A quiz with several answers still names no word (M17c's design). The spelling
    branch never fires through the pipeline (a one-word slip within its budget is a fuzzy match first). Unaccented `que`, `como`,
    `cuando`, `donde` count as question words (accepted).
- **M78, the small core points (2026-11-05, Sonnet 5.5, a Haiku follow-up for three game tests; on V29's branch; gated with it).**
  `la tienda está en el pueblo` and `usted hace hachas muy buenas` are `describe`, `tú pescas mucho` is `state_action` (three new grammars, no new
  intent; `yo trabajo mucho` and its persons are `state_action` too). A word with no reading is a name after `soy`, `me llamo`, `mi nombre es`, a
  greeting, `adiós` or `chao`, or as a lone capitalised word (`IAnalysisRules.IsNamePosition`, `NameFallback`); a bare lower-case `pedro` stays
  `vocab.unknown` (it cannot be told from a wrong word). `necesito ayuda domingo` is accepted: `domingo` is the day noun and the host takes the
  name out. The recast reads the verdict's errors, so `cuál es esto` at `lesson-what` is recast as «Qué es esto». tools.md describes the live CI.
  The suite: 9127 passed, 3 skipped, and the one known failure below. **The grammar cache:** `GrammarTests.TheCommittedCacheReplaysTheBundleOfflineWithItsWaivers`
  failed on V29's and M78's 43 new strings while the sandbox kept the dotnet process from the local LanguageTool server; on the owner's
  retry (2026-11-05) the loop's `--grammar` step ran: 20 new cache entries, 0 new findings, the rebuild from nothing equal. Gated with V29 at
  `3b4a2214`, 9131 passed, 3 skipped. The orchestrator's CLI check: `soy pedro` has no `vocab.unknown` but takes no intent (`unknown`, not
  communicated) at `villager-open`, so the NPC says «No te entendí»: a small core point left (an intent for `soy <name>`). Report:
  `.claude/briefs/m78-report.md`.
- **M81, an O15 tolerance (2026-11-05, Haiku 5.5; gated on `feature/adaptive_design`, 9160 passed, 3 skipped).** Seed `41s2qgWEC-M9` removed
  `describe`'s `slotGrammars` and the loader took the bundle. The loader is right: an intent with examples is reachable with no grammar through
  the embedding rung, and only the lint's coverage check refuses it (as it does for the other rule-less intents). `KnownTolerances` now names
  the case (`ExampleOnlyIntent`, beside `RuleOnlyIntent`); no loader code changed; two regression tests and a fuzz README finding. **For the
  owner:** an intent with no grammar, rule or example still loads (`OptionalMembersMayBeOmitted` pins it); the owner (2026-11-05): no bundle has
  one, so enforce it (M82). Report `.claude/briefs/m81-report.md`.
- **M82, an intent nothing can produce is refused (2026-11-05, Haiku 5.5; gated on `feature/adaptive_design` at `80492cf9`, 9363 passed, 3
  skipped).** The owner's answer to M81: `IntentCatalog.Build` refuses an intent with no slot grammar, keyword rule or example (empty lists
  count as absent; the bundle loader names `intents/intents.json`). `OptionalMembersMayBeOmitted` pins a grammar-only intent; new tests for a
  rules-only and an examples-only intent and a theory for the refused ones (bare, `requiredSlots` only, all empty); one fixture of
  `IntentExampleTests` took an example. The fuzz tolerance is unchanged and O15 passes. `docs/nlp/changing-the-core.md` states the rule.
- **An O7 failure found by W02 (2026-11-07, not planned yet).** On `feature/bread-pilot` at `54e6625c` (before W02, and the same on W02),
  CsCheck seed `8eu5g1sTe_A8` fails `IntentResolverFuzzTests`: «ayuda por favor por favor» at a prompt allowing `ask_activity`,
  `request_item` and `farewell` (not `ask_help`); the analyzer matched `request_item`'s `[ART]? [NUM:qty]? [NOUN:item] [ADJ]? por favor`, which
  `ask_help`'s `ayuda (por favor)?` outranks, yet the resolution is Grammar `request_item`. Either the resolver should not take a grammar an
  outranking one beat, or `ayuda` should not read as an item noun there; the oracle stands. The seed is random per run, so a gate can hit it.
  Proposed as M83 for the owner. Log saved at `/tmp/claude-1000/orch/fuzz-8eu5g1sTe_A8.log` (not committed); report
  `.claude/briefs/w02-report.md`.
- **M80, the open-recognition fallback (2026-11-07, Sonnet 5.5 high; gated at `d939ceb7` on `feature/bread-pilot`, 9893 passed, 3
  skipped).** `PipelineOptions.OpenRecognitionFallback` (default off) runs a second, open decode on a Constrained prompt's first
  recognition when the constrained reading is not taken; the taken second reading wins and the other is `UtteranceAnalysis.OtherPass`.
  Posteriors are computed once; the stages after recognition are one private method. **The floor** (the owner, 2026-11-07, D-game-22):
  `PipelineOptions.OpenPassWordFloor`, default 0.5, the first validated option (out of [0, 1] or NaN throws): the free reading is taken
  only when its weakest word's confidence is at least the floor; no word confidences means not taken. Bench `--open-fallback` and
  `--open-vosk`. O14 checks that no taken free reading has a word under the floor. With the fallback off no golden, identity entry, replay
  or digest moved. **Measured** (Kokoro voices only, 8 variants, 345 clips, Vosk small): 0 false takes (2 without the floor: «ese libro es
  muy interesante» heard «el libro…», `el` 0.45); 4 wrong takes («usted hace hachas muy buenas» heard with a leading «si» and read as
  `confirm`, as typed «si usted…» is; the floor cannot stop them); off-prompt reach 87/120, 63 of them understood; 1 of 108 understood
  lines lost to the floor; free decode median 237 ms (p95 342). The accent risk on real speech is unmeasured (no owner recordings).
  The first gate run was killed at the shared 8 GB cap while W03's suite ran beside it (exit 143); rerun alone, it passed. Reports
  `.claude/briefs/m80-report.md` and `m80-report-2.md`. V31 uses it.
- **M83, the O7 seed (2026-11-07, Sonnet 5.5 medium; merged at `45499631`, gated with the water merge at `bc881c58` on
  `feature/bread-pilot`, 10328 passed, 3 skipped).** The cause was none of the brief's guesses: the matcher binds the trailing polite
  phrase's words to literal nodes, so they count as literal and named tokens, but `SlotGrammar.MaxLiteralTokens` and `MaxNamedTokens`
  counted only the pattern's own elements; `IntentResolver.OutsideGrammar` and `MatchScore.CanBeBeatenBy` used those bounds to skip an
  outside grammar and judged `ask_help` (literal 5 with the phrase) unable to beat `request_item`'s 4. The fix, in `IntentResolver.cs`
  only: both bounds add `GrammarDefaults.TrailingPoliteWords.Count`. No public type changed; the oracle is unchanged and passes the seed
  and five more runs. Three tests in `IntentResolverCatalogReadingTests`; a "Bugs found and fixed" entry in the fuzz README. Its first
  gate was cut off by the machine's reboot (2026-11-07); `LintTests.TheBundleLintTakesUnderFiveSeconds` took 5.55 s beside T9's build in
  the agent's own run (3 s alone). Report `.claude/briefs/m83-report.md`.
- **M84, free talk's forced rows (2026-11-08, Sonnet 5.5 high; gated with T10 at `386815a6` on `feature/bread-pilot`, 10363 passed,
  3 skipped).** Measured on 2541 evaluations (843 clips, 8 voices): of 1501 taken constrained readings, 852 correct rows, 241 other lines
  read as typed, 408 forced. The answer match separates them best (correct rows: Exact 804, Alias 11, Fuzzy 37, None 0; forced: Exact 0,
  Alias 45, Fuzzy 65, None 298); the words' coverage of the voiced audio next (correct 0.76 to 1.00, forced median 0.54); `[unk]` and
  speech per letter do not. Coverage alone misses V31's shopkeeper case («me cuesta el río», coverage 1.00). **The trigger**, used only
  with `OpenRecognitionFallback`: `PipelineOptions.OpenPassOnLooseMatch` (default true: a taken reading matched `None` or `Fuzzy`) and
  `OpenPassMinCoverage` (default 0.6, [0, 1], 0 off) also run the open decode; the free reading wins only when taken, over
  `OpenPassWordFloor`, with a different intent (`Pipeline/ForcedReading.cs`, `EnergyVad.SpeechFrames`). **Bars:** 0 of 852 correct rows
  and 0 of 241 other correct lines replaced, 0 false takes; 297 of 408 forced lines rescued (every V31 line); 1 wrong take (M80's
  «usted hace hachas…» as `confirm`). Extra open decodes on 45 % of taken readings in this off-prompt-heavy corpus, 4 % on correct rows;
  median 199 ms, p95 365 ms. O14 checks the trigger and the different-intent rule (mutation-checked). V32 is not needed (`OtherPass` and
  `Asr.Mode` unchanged; a replaced row shows as `heard_open`); the game's `architecture.md` and the D-game-9 note still say the second
  decode runs only for a missed line. Its first gate failed on the water merge's Game.Core heap (T10). Report `.claude/briefs/m84-report.md`.
- **M85, the lint's speed again (2026-11-08, Sonnet 5.5 medium; gated at `a1066151` on `feature/bread-pilot`, 10394 passed, 3
  skipped).** CI on PR #40 failed `LintTests.TheBundleLintTakesUnderFiveSeconds` (5.54 s on the runner). Here the lint took 2.4 to 2.6 s
  (Debug, steady; 3.0 to 3.2 s cold), against M60's 398 ms: the confusion check resolves 20,448 (example, rival) pairs (33 intents, about
  640 examples), and since M74, M75 and M83 every `Resolve` scans the whole catalog (136 grammars) in `OutsideGrammar`, about 2.2 million
  `Better` calls a run with about 2,500 matches; each grammar's match was recomputed for every pair. Also `CheckPair` matched with the
  pack's default `AnalysisRules` while the resolver uses the bundle's, so `TokenFacts`' one-entry cache alternated. Fixed at the source:
  `TokenFacts.Matched` keeps one match per grammar per analysis; `IntentResolver.PlausibleGrammars` lists the grammars that pass `CanMatch`
  once per analysis (catalog order kept, so ties break as before) and the grammar rung visits only those (this is the production
  resolver, not only the lint; the pipeline was not timed); `LintConfusion` builds facts once per rules set. Now about 0.7 s steady and
  1.2 s cold; output byte-identical (`Format()` and `ToJson()` at a1, a2, the gate's options and no level); the 5 s budget unchanged.
  **Rule:** a catalog-wide rung must reuse the per-analysis `TokenFacts`, never rebuild them per prompt. **Watch point:** `CheckPair`
  matches with `AnalysisRules.For(language)`, not the bundle's rules, left to keep the output identical. Report
  `.claude/briefs/m85-report.md`.
- **M91, O4's `podar` before an infinitive (2026-11-08, Sonnet 5.5 medium; gated at `6275653f` on `feature/ui-issues`, 10431 passed, 3
  skipped).** Found by U08's merge gate, CsCheck seed `3m67O5Rpuya2`, and reproduced on the code before U08 (`main` has it too). **The
  generator was wrong:** the realiser drew a finite `podar` before an infinitive («vosotras podáis rastrear opero»); M69's re-roll covered
  only `podo`, `pode`, `podes`, so the `conj.person` mutation to «podo» made a sentence the analyzer reads, by M58/M69's `CallsForSlip`
  ("`podar` takes no infinitive"), as `poder`'s slip. `SentenceRealizer` now re-rolls every finite `podar` right before an infinitive
  (`slip-in-context`, the seed in the comment). **The analyzer is unchanged and right:** «vosotras podo rastrear» gives `conj.person` →
  `podéis` and `conj.irregular` → `puedo`, two faults on one word, as `ellos quero pan` gives `quieren` and `quiero`; accepted behaviour.
  O4's assertion unchanged; the stream digest, goldens and replays did not move. 5 test cases in `HomographRerollTests`.
