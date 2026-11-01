# Handover: the village series after phase B (2026-11-01)

> **Update (2026-11-01, after the merge): start at section 9.**
> - The owner merged PR #20 into `main` (`1dbb0e5`) and wants **all** the follow-ups done on `feature/village-followups`. That branch was
>   created from `origin/main` and pushed.
> - Section 9 lists every open failure and issue, with the recommended fix for each, the queue, and what to ask the owner first.
> - **Nothing in section 9 has been built:** the owner asked for the fixes to be noted here, not made.
> - Sections 1 to 8 are the background.

> **Update (2026-11-01, later):** section 3, the review, is done: `docs/qa-reports/2026-11-review-feature-village.md`. On the owner's word
> all ten findings were fixed on `feature/village`: the core's as M64 (`23af5cd`), the game's as V19 (`716d469`), with 7420 tests passing
> and bundle `0f8faea0…`.
> - Section 4's item 2 (the three fuzz seeds) is more pressing now: the `haber` family also fails at the default depth (seed
>   `4YuZ2yubxdC1`).
> - Section 5's free-talk follow-up and the core's grading gaps are still open, apart from what the review fixed: the greeting before a
>   name, and session 8's answer with a subject.

For a fresh agent, from the orchestrator session that ran phase B. Your job, in this order:

1. **Review all the code on `feature/village`** (PR #20, into `main`) before the owner merges it (section 3).
2. **Help the owner decide** the open decisions, the proposed follow-ups and what V09 found (sections 4 to 6).
3. **Only if the owner says so:** set up the follow-up branch and run it as the orchestrator (section 7).

The owner's own words for this handover: "The agent will then review all of the current code on the branch, and I can also decide whether
to continue with the follow-ups and the decisions and also what the V09 found."

A prompt the owner can give you: *"Read `docs/HANDOVER_VILLAGE_PHASE_B.md` on `feature/village` and do its section 3, the review. Report to
me before changing anything."*

## 1. Ground rules (read before running anything)

- **Where:** the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`, on `feature/village`. Start the session there. Do not
  edit the main checkout `/home/user/Dev/Vocaterra` or the other worktrees.
  - The other worktrees hold paused work that is not part of this branch: `X06-review`, `game_refactor-work`, `feature/content` and
    `feature/test-performance`. Leave them alone unless the owner asks.
- **Never push to `main` or merge into it.** The owner merges feature branches by pull request.
- **The review changes nothing.** Make no commits to `feature/village` during the review. A fix the owner wants is planned as a milestone
  (section 7).
- **The machine has crashed twice from overload** (16 cores, 11 GiB of RAM).
  - Every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run goes through `scripts/limited.sh`. It
    serialises heavy commands machine-wide and caps each one at 6 GB.
  - Use `-m:4` for builds and `-m:2` for tests. Never run two heavy commands at once.
  - At most two read-only subagents at a time. Only one agent may build or test, and then no other agent runs.
  - Before anything heavy, check `free -h` (at least 4 GB available), `uptime` (load under 16) and `df -h /home` (at least 10 GB free).
    Disk had 11 GB free on 2026-11-01.
- **Environment:**
  - Prefix commands with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
  - The sandbox refuses a command line containing the words `eval` or `hash`. Put such a command in a wrapper script under
    `/tmp/claude-1000/`.
  - Model-gated tests need `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`.
- **CI:** the session's token cannot read CI results (`gh pr checks` and `gh run list` give 403). Ask the owner what PR #20's checks
  show.
- **Project rules:** `CLAUDE.md` (the repository's instructions), `.claude/agents/orchestrator.md` (the orchestrator's role; its "Handover
  (2026-11-01)" is the short form of this document) and `docs/code_guidelines.md`.

## 2. What the branch holds

`feature/village` is at `529c055` or later, about 170 commits ahead of `main`. `origin/main` is `54ed22e`, which is also the merge base:
the branch is up to date with `main` (its merge at `0cf674b` brought the CI workflow and the QA agent).

**The village series** (`docs/game/VILLAGE_PLAN.md`; decision D-game-22 in `docs/game/DECISIONS.md`): a bigger village with four more
NPCs, items and orders, and seven lessons, sessions 4 to 10.

- **The NPCs:** guide Luna, the shopkeeper at the stall, the villager (shown as Aldeano), farmer León, fisher Mar, baker Luz and smith
  Domingo.
- **Phase A** (sessions 4 to 6, `a1`, done 2026-10-31):
  - V01 to V07: NPC behaviour from data, host actions and the beacon, items and orders, the scene, figures, the voice trial, the check
    battery by lesson;
  - V10 to V12: sessions 4 "El pueblo", 5 "Números y pan" and 6 "Gente del pueblo";
  - V09: the documents and runs for phase A;
  - V12b: the NPCs' focus rows;
  - V12c: the next session by sleeping at the fire, and `--skip-to-session n`.
- **Phase B** (sessions 7 to 10, `a2`, done 2026-11-01):

| Milestone | What | Commit |
| --- | --- | --- |
| V08 | The A1/A2 gate: `level` on lessons, the lint at the A2 ceiling | `808e0f0` |
| V13 | Session 7 "¿Cómo es?": adjectives, `gustar`, the water order | `f5933c8` |
| V18 | `scripts/content-loop.sh`, the content loop in one command | `d986adf` |
| V14 | Session 8 "Ayer en el pueblo": the preterite in every person, `tener que`; the listener's name stripped (`Vocative.cs`) | `78a37fa` |
| V16 | The simple future at a lesson step; the book's future column | `71573ba` |
| V17 | Session 9 "Mañana en el pueblo": the simple future against `ir a` | `cc155d4` |
| V15, V15b | Session 10 "El mercado": 11 to 20, totals, `poder`, selling to NPCs (`offer_item`, the host's `buy`); a greeting before a request (`Greeting.cs`) | `d082f05`, `557db47` |
| V09 again | Phase B's documents, the scripted runs, the learner's route (section 6) | `da8dba6` |
| M56 (core) | `ser` where a place takes `estar`; a subject after its verb | `d87e823` |
| M57 (core) | The later time markers in one table (`SpanishTimeMarkers`) | `888f914` |
| M58 (core) | `cuánto costa` and `hay pans` graded; the `word_order` fuzz mutation's guard (the owner's "option 1") | `78ce805` |
| M59 (core) | The person checks; a lone name is never checked | `12ea9d7` |
| M60, M61 (core) | The lint's speed for CI; the core's lint tests at the A2 ceiling | `95bd7bf`, `808e0f0` |
| M62, M62b (core) | Predicate adjectives agree; a colour before its noun is `word_order`; the fuzz realiser re-rolls a name read as a noun | `65b04e6`, `5f3450b` |

**Size against the merge base** (`git diff --shortstat 54ed22e feature/village -- <area>`): 868 files, +47 441 and −3 781 lines.

| Area | Files | Lines added / removed |
| --- | --- | --- |
| The core: `src/Vocaterra.Nlp`, `src/Vocaterra.Nlp.Es` | 19 | +2 048 / −88 |
| The core's tests: `tests/Vocaterra.Nlp.*` | 37 | +4 831 / −426 |
| `docs/nlp` | 7 | +386 / −18 |
| The game's logic: `src/Vocaterra.Game.Core` | 63 | +3 351 / −364 |
| The game's engine project: `src/Vocaterra.Game` | 19 | +452 / −52 |
| The host: `src/Vocaterra.Host` | 3 | +25 / −5 |
| The speech renderer: `tools/Vocaterra.SpeechRender` | 1 | +245 / −49 |
| The game's tests: `tests/Vocaterra.Game.*`, `tests/Vocaterra.Host.*` | 91 | +8 328 / −156 |
| `content/` | 580 | +23 630 / −2 489 |
| `docs/game` | 23 | +1 690 / −100 |
| `scripts/` | 8 | +745 / −29 |
| `.claude/` (the orchestrator file and the milestone briefs) | 14 | +1 378 / −1 |

Most of `content/` is generated, and is regenerated by `scripts/content-loop.sh` rather than edited by hand:
- 472 speech WAVs;
- 42 answer sets;
- the grammar cache;
- the manifest.

The hand-written content is:
- the ten lessons: `content/game-v1/game/lessons/*.json`;
- the seven NPC files and the four orders: `game/npcs/`, `game/objectives/`;
- `content/PHRASES.md` (the source of truth for phrases);
- the intents, the lexicon and the dialogue.

**Where each milestone is recorded:**
- the game's findings log, `docs/game/IMPLEMENTATION_PLAN.md` section 5 (the V entries at its end);
- the core's milestone table and findings, `docs/nlp/IMPLEMENTATION_PLAN.md` §5 and §5b;
- the design, `docs/game/VILLAGE_PLAN.md` sections 3.5 to 3.9, 4 and 5;
- each milestone's brief, `.claude/briefs/`.

**Last measured** (V09's gate, 2026-11-01):
- **with the speech models:** 7259 passed, 3 skipped;
- **without them (CI's case):** 7218 passed, 44 skipped, 27 of them the game's;
- **otherwise:** 0 warnings; format clean; the content gate at A2, the host's `--validate-content`, the grammar check and the licence
  scan clean;
- **the content bundle:** `36b2b8ad…`.

## 3. The review

Review by area against the merge base: `git diff 54ed22e feature/village -- <path>`. Read each milestone's findings entry alongside its
code: it says what was meant and what was left. Write the findings to one file, `docs/qa-reports/` is the usual place (for example
`docs/qa-reports/2026-11-review-feature-village.md`), and give the owner a summary: bugs first, then risks, then cleanups. Rank what
must be fixed before the merge above what can wait for the follow-up branch.

Suggested order and what to look for:

1. **The core** (`src/Vocaterra.Nlp*`). The main new code is:
   - the agreement checker's new parts (`SpanishAgreementChecker.Preverbal.cs`, `.Postverbal.cs`, `.Predicate.cs`);
   - `ReadingResolver`, `LinguisticAnalyzer` and `TextReadings`;
   - the morphology slips (`MorphologyGenerator`: `costa`, `pans`);
   - `SpanishTimeMarkers`;
   - `ContentLinter`.

   Check:
   - **False alarms on correct learner lines.** The worst bug a learner can meet. Try names, greetings, `usted`/`ustedes`, and a name
     said to the listener (transcripts carry no commas: `Luz quiero pan` must stay clean).
   - **The determinism rules:** no `System.Random`, no clock in the evaluation path, ordinal comparisons.
   - **The fuzz suite** (`tests/Vocaterra.Nlp.Fuzzing.Tests`, its README lists the narrowed oracles). No oracle may be weakened without
     the owner. M58 (c) narrowed O4 and O5 for the `word_order` case only, by the owner's decision. M62b is a re-roll in the sentence
     generator, not a narrowing.
2. **The boundary.** `docs/nlp/API.md` grew by 166 lines on the branch. It records new members on `IAnalysisRules` and `IMorphology`,
   "each breaking for a host that implements" them. `docs/nlp/adr/0009-core-and-game-boundary.md` says "a change to a public type listed
   there is a change to the boundary and needs this record updated", and ADR 0009 was not changed on the branch. Check whether it should
   be: the likely one fix before the merge.
3. **The game's logic** (`src/Vocaterra.Game.Core`).
   - Selling and buying: `NpcService`, `NpcHostEvents`, `HostEventTable`. The NPC pays for what the player sells (`buy`) and sells for
     coins (`sell`); `wont_sell`, `wont_buy` and `cant_sell` handle the misses.
   - Evaluating a line, then its rest after a greeting (`Greeting.cs`) or after the listener's name (`Vocative.cs`), so the core still
     reads one intent per line.
   - The lessons (`LessonRunner`, `LessonBook`: `level`, `after`, `tense: fut`).
   - The next session and the skip (`VoxelSession.Sleep.cs`, `VoxelSession.Skip.cs`, `SessionSkip`, `SleepOffer`).
   - `GameDataValidator`, `CheckBank`, `PhraseSheet`, `ScenePlaces`, `WorldFacts`.
   - The save (`SaveGame`; saves must load across content updates, X01).

   The architecture rules are gated by `ArchitectureRulesTests`:
   - no engine package in `Game.Core`;
   - no `System.Random` or `Guid`;
   - the wall clock only in `WallClock.cs`.
4. **The engine project** (`src/Vocaterra.Game`): engine code lives only here (D-game-1). The changes are the launch options, `VoxelGame`,
   the figures, the block art and the layers.
5. **The content.**
   - The Spanish of the ten lessons and the NPC files: natural and correct. Each phase-B line had an Opus pass and the orchestrator's
     read; a native speaker's read would be stronger.
   - Each lesson keeps to its declared level (`a1` or `a2`).
   - The generated files match their sources: the content loop and the gate check it.
6. **The tests:** they pin behaviour rather than implementation, and the timed tests (section 4, item 3) have the margins CI needs.
7. **The scripts:**
   - `scripts/content-loop.sh` (630 lines, new);
   - `scripts/merge-gate.sh`, `content.sh`, `prerender-speech.sh`, `limited.sh`;
   - `scripted-runs.sh` and `compare-runs.py` (the scripted runs).
8. **The documents** read true against the code: `docs/game/README.md` (the village section), `PLAYTEST.md`, `SCRIPTED_RUNS.md`.

To check behaviour cheaply (each through `scripts/limited.sh`, one at a time):
- **Build:** `dotnet build Vocaterra.sln -warnaserror -m:4`.
- **Tests:** `scripts/test.sh fast` while looking; `scripts/test.sh full` once if needed. The full suite takes long and uses the whole
  allowance.
- **The core demo:** `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open
  --content content/game-v1` must give exactly `conj.person` and `noun.number`. It needs a wrapper script for the word `eval`.
- **The host:** `dotnet run --project src/Vocaterra.Host -- --transcript "<line>" --prompt <id>` evaluates one line at a prompt. Examples:
  `hola, quiero dos panes` at `baker-open`, `vendo madera` at `smith-open`, `¿cuánto es todo?` at `farmer-open`. The host has no bag, so a
  sale prints its success line whatever is held; the game decides.
- **The game:** `DISPLAY=:0 dotnet run --no-build --project src/Vocaterra.Game -- --seed 42 --mute --skip-to-session 10 --lesson-run
  el-mercado --screenshot <png> --frames 30`. `--help` lists the options.
- **Do not run `scripts/merge-gate.sh` for the review.** It merges a branch into its target.

## 4. Decisions for the owner

1. **The voices.**
   - None of the 902 speech files has been listened to (`docs/game/SPEECH_CHECKLIST.md`: no file marked).
   - Phase B added 222 files (111 lines, two speeds each). By NPC, in files: the farmer 70, the guide 54, the baker 30, the smith and the
     villager 22 each, the fisher 14, the shopkeeper 10.
   - The voices are provisional (D-game-22). Rejecting a voice re-renders only that NPC's lines.
2. **Three fuzz failures in the core**, older than phase B and rare. Each fix narrows the fuzz oracles O4 or O5, so each waits for the
   owner. Reproduce one with `CsCheck_Seed=<seed>` on the fuzz project. Until a decision, a gate that fails on one of these seeds alone is
   rerun once after checking the seed.
   - `egVgLELrxyL4` (O4): the `conj.tense` mutation lacks M26b's guard for a name in an alternative. It fails about once in six runs of
     the fuzz project at its default depth, so it can fail a gate.
   - `2ldq2rIKLrG2` (O4): the same mutation where a grammar's literal decided a reading. It fails only at five times the depth.
   - `b3HSKSLCsL_1` (O5): the `conj.person` mutation after a noun subject and `haber`, which M59 leaves out by design. It fails only at five
     times the depth. The alternative to narrowing is core work: checking the plural forms of `haber` against a singular noun subject (a
     widening).
3. **The timed tests on CI.** GitHub's runner is about four times slower than this machine. Measured by V09 under the full suite's load:

   | Test | Here | Budget | At four times |
   | --- | --- | --- | --- |
   | `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` (core) | 1438 ms | 3000 ms | about 5.8 s: likely fails |
   | `WorldFactsTests.ASnapshotOfTheLargeWorldIsQuickAndAnswersEveryNounOfTheRoute` (game) | 0.50 s, the whole test | 1.5 s | likely fails |
   | `SceneBuildTimeTests.Seed42SceneMatchesTheGoldenAndLoadsInUnderThreeSeconds` (game) | 708 ms | 3000 ms | about 2.8 s: at the edge |
   | `LintTests.TheBundleLintTakesUnderFiveSeconds` (core) | 0.78 s | 5 s | under |
   | `ChunkIndexTests.ADigestOfALargeWorldIsCachedUntilTheNextEdit` | 0.13 s, the whole test | 100 ms for the digest | likely under |
   | `GuideTurnTests.ATurnTakesPartOfAFrameAndNeverTheSimulationTick` | 8.2 ms a turn | 250 ms | under |

   If PR #20's CI is red on one of these, fix it on `feature/village` before the merge, as M60 did for the lint: a wider budget or a
   measure that excludes the load. Otherwise `main`'s CI goes red.
4. **Name plates show through figures** (Luz's plate over the shopkeeper's face at session 8's morning; the farmer's in phase A). This is
   a design question.
5. **Older open questions,** not from this series:
   - the core's D8 to D11 and D13 (`docs/nlp/DECISIONS.md`; D1 and D4 are deferred, do not raise them);
   - the open questions in `.claude/agents/orchestrator.md`'s 2026-10-23 handover.

## 5. The follow-ups (proposed, not started)

**The orchestrator's recommendation:**
1. **Finish the review**, and fix only what blocks the merge on `feature/village`: a real bug in phase B's code, the ADR 0009 note if
   needed, and a red CI result.
2. **Let the owner merge PR #20.**
3. **Do the follow-ups on one new branch from `main`** (for example `feature/village-followups`), run as phase B was: one milestone at a
   time, each gated, then one PR.

PR #20 is already about 170 commits. Phase B is a finished, gated unit. A new PR gets its own CI run.

**Worth doing, in this order:**

1. **A QA pass on the village** (`subagent_type: qa`, `.claude/agents/qa.md`, areas `language` or `progression`). It goes first, so its
   findings are planned with the rest.
2. **The owner's fuzz decision** (section 4, item 2). The first seed can fail a gate about once in six fuzz runs.
3. **The core's grading gaps**, as core milestones (M-numbers in `docs/nlp/IMPLEMENTATION_PLAN.md`):
   - **From V15:**
     - `¿puede usted vender manzanas?` gives a wrong `conj.person` (expects `vende`): a false alarm on a correct line;
     - `puedo pago con fruta` and `quiero vendo madera` give no error;
     - `¿cuánto cuestan todo?` gives no `conj.person`;
     - `ti` is not in the lexicon (`vocab.unknown`);
     - `¿cuánto son todo?` is understood with intent `unknown`.
   - **The wrong-word flag:** a word outside a constrained prompt's answer set gets `vocab.wrong`, even in a lesson's own sentence (V17:
     `usted comprará pan` at a lesson step; in free talk, section 6).
   - **M63, filed:** a subject pronoun before a question word is lost to M56's check (`ellas adónde están …`; fuzz seeds
     `8zKtIhOY3951`, `c5SlpISRmTZ3`).
   - **The core milestones' own left points** (`docs/nlp/IMPLEMENTATION_PLAN.md` §5b):
     - M56: `son cerca`, `ser en`, the first and second persons after the verb;
     - M58: `podo` and `esto` only under a grammar, `azuls`, `los panes costa`;
     - M62: `él es alta` clean, `son` after a lone name, a `de …` subject's number only.
4. **The free-talk follow-up** (a game V milestone; after the core work, since part of it is the core's `vocab.wrong`).
   - **Sentences their own NPC does not understand.** In free talk, 9 of phase B's 27 practised sentences get "No te entendí" from the NPC
     who taught them, because the open prompts of the villager, the fisher and the stall take no statements, and Domingo's no
     `tener que`:
     - `Es grande.` and `Compré tres manzanas.` at the stall;
     - the villager's three;
     - `Tengo que ir al río.` at Domingo;
     - `Luz compró pescado.` and `Luz hará pan.` at Mar;
     - `Tienes veinte panes.` at Luz.
   - **Practice not counted.** Free talk credits only the rows in an NPC's `focus`, so a phase-B sentence said again in free talk counts
     no phrase use, though the book's word records count it (`Mañana compraré pan.` to Luz, `Vendo madera.` to Domingo, `Me gustan las
     manzanas.` to León).
   - **Example cards before their lesson.** An NPC shows its focus cards from the first talk: Luz shows `Vendo madera.` (session 10) in
     session 5, and León his A2 lines from his first talk. That is A2 to an A1 learner (VILLAGE_PLAN section 5, "Not overwhelming").
   - **A lesson's sentence counted as a slip.** At an open prompt whose answer set lacks it, it is flagged: `buscaré madera para Luz` to
     Domingo gets "a small mistake" and a recast that writes `luz` lower case.
   - **The Lessons page opens at its top,** so in sessions 8 to 10 the `next:` lesson is far down (192 lines).
   - Smaller ones that fit here:
     - the stall answers any request with the bottle (older than the village, S6);
     - `tener que` has no book entry;
     - the NPCs' `¡Muy bien!` plays in Luna's voice.

**Worth dropping or deferring** (a learner never meets them):
- the session skip buys one apple, not three, for the market (no session follows 10);
- `KeyWordsTaught` is one save flag (it matters only when a second NPC gets key words);
- the host's `--transcript` mode prints a sale's success line whatever the bag holds (a test aid);
- five old world words (`madera`, `antorcha`, `botella`, `fuego`, `despacio`) sit in the body of `a1.txt`, not its marked block;
- the closing review is skipped when nothing is due;
- the book counts a use correct only when it was understood at its prompt.

**How each follow-up is built:**
- **The village series' rule** (D-game-22): every V milestone by Opus 5.5 at xhigh effort, one agent at a time, on a milestone branch
  gated into the feature branch.
- **The models:** the used briefs in `.claude/briefs/`, V15 for content, V09 for documents, M62 for the core.
- **The model policy for work outside the series** is in `.claude/agents/orchestrator.md`: Haiku by default, Sonnet for design-heavy work,
  Opus on request. Ask the owner which applies to the follow-up branch.

## 6. What V09 found (phase B's documents and runs)

Recorded in `docs/game/IMPLEMENTATION_PLAN.md` section 5 ("V09 again"). The full open-points list is `docs/game/README.md`, "Open points
after phase B".

- **The scripted runs against the end of phase A** (`ccf8187` against `fa22116`; all 20 runs, `docs/game/SCRIPTED_RUNS.md`). Every
  difference traces to phase B:
  - ten lessons in each closing summary;
  - speech at 451 lines in 902 files (680 files before);
  - saves 18.4 KB larger (155 phrase records, ten lessons, the order `water-for-plants`);
  - the Lessons page at 192 lines (121 before);
  - the skip carries session 1's bottle of water (V13);
  - Luz's fourth card `Vendo madera.` (V15).

  Ticks, world digests, the scene and every other picture are unchanged. Nothing is unexplained.
- **Two new runs, both reproducible:**
  - `r22-market`: session 10, four trunks cut, `vendo madera` to Domingo (four wood out, four coins in), two loaves bought from Luz,
    `¿cuánto es todo?` to León ("Quince manzanas, quince monedas."). The bag after: the book, three apples, the axe, four loaves, no coin.
  - `r23-future`: session 9's lesson, then `mañana compraré pan` to Luz in free talk, then the book's Verbs page on `comprar` with
    `compraré` said twice, both correct.

  The orchestrator ran both again after the gate and checked their pictures.
- **The learner's route through sessions 7 to 10,** played from `--skip-to-session 7` with a sleep at the fire between sessions:
  - each session opens on its lesson: the objective names it and its NPC, the beacon points at the NPC, and the morning toast shows;
  - all four lessons run to their ends;
  - after session 10 the game says the series is done.
- **One bug fixed:** a skipped game left the shopkeeper as if never met. In session 7 he showed his key words again and handed over a
  second bottle, logging a phrase use the learner never made. The skip now counts his bottle and key words (`SessionSkip`,
  `SessionSkipTests`).
- **Found, not fixed:** the free-talk points in section 5, item 4.
- **The counts, measured** (`content/game-v1`; after phase A in brackets):
  - 10 lessons (6) and 229 steps (155);
  - 155 phrase rows (93): 74 said, 80 heard, 1 both;
  - 22 nouns (22) and 20 verbs (15);
  - 29 intents (23) and 70 prompts (47);
  - 451 speech lines in 902 files (340 in 680);
  - 87 check items (55): 43 heard, 44 said.

  The check battery by session is 31, 31, 31, 39, 47, 55, 63, 71, 79, 87. The pilot playtest stays at its 31 items; `docs/game/PLAYTEST.md`
  section 14 says what a playtest of sessions 7 to 10 would need.
- **The coin path:** a learner reaches session 7 with 0 coins and leaves session 10 with 0 coins. Selling wood or fruit to NPCs (V15) is
  the one repeatable source of coins.

## 7. If the owner says continue

- Act as the orchestrator: `.claude/agents/orchestrator.md`, starting at its "Handover (2026-11-01)" and its "Start of a session" checks.
  The orchestrator plans, briefs, gates and records; it never writes code under `src/`, `tests/` or `tools/` itself.
- Merge order: the owner merges PR #20 first. Then create the follow-up branch from `main` and push it.
- **The gate:** `GATE_TARGET=<feature branch> scripts/merge-gate.sh "<label>:<milestone branch>"`, run from that branch's checkout, in the
  background. It merges, builds with warnings as errors, runs the full suite with models, format, the content gate, grammar and the
  licence scan.
- After each milestone:
  - record the status and the findings in the plans;
  - commit and push the feature branch;
  - delete the milestone branch, locally and on the remote;
  - report briefly to the owner.
- **Gotchas from this series** (`.claude/agents/orchestrator.md`, "Gotchas learnt this series"):
  - CI's live grammar job rebuilds the grammar cache from nothing and fails on any difference;
  - `scripts/content-loop.sh` runs directly, never under `limited.sh`;
  - spot-check the learner's route, not only `--lesson-run`;
  - never end a session with an agent running, and kill an agent's leftover wait loops;
  - try a core change on correct learner lines, not only the brief's.

## 8. Housekeeping

- `/tmp/claude-1000/m59` (1.2 GB, left over from M59) was deleted on the owner's word on 2026-11-01.
- `/tmp/claude-1000/v09` (about 1 MB) holds V09's logs and can go too.

## 9. After the merge: every open item and its recommended fix (2026-11-01)

The owner merged PR #20 into `main` (`1dbb0e5`) and wants all the follow-ups done, on `feature/village-followups`. That branch was created
from `origin/main`, pushed, and checked out in the worktree `.claude/worktrees/village`.

The owner asked for the fixes to be noted here, not made, so **nothing below is built**: it is the plan for the next orchestrator.

- This section replaces sections 4 to 6 as the plan; they keep the background.
- Each item gives the problem, where it is, the recommended fix and how to prove it.
- File the milestones' rows in the plans (the core's §5, the game's VILLAGE_PLAN table) before launching them.

### 9.1 Ask the owner once, at the start

1. **The model.**
   - Recommended: Opus 5.5 at xhigh for M65 to M67, M69 and V20, where Spanish is judged and the design is subtle, as in the series.
     Sonnet 5.5 for V21 to V23 and the timing milestone; Haiku 5.5 for V24, the documents.
   - The series rule (D-game-22) says Opus for every V milestone; the orchestrator's default is Haiku.
2. **The fuzz narrowings of M65 (9.3 A).** Each needs a yes or a no. Recommended: yes to (a), (b) and (c); leave (d).
3. **The name plates' design (V22).** Recommended: show only the plate of the NPC the player faces within talking range, drawn
   depth-tested.
4. **The voices** (9.3 K): the owner's listening, not blocking.
5. **What `main`'s CI showed after the merge** (9.3 H). This session's token cannot read CI.

### 9.2 The queue (one agent at a time, each gated into `feature/village-followups`)

| Order | Id | What | Depends on | Size |
| --- | --- | --- | --- | --- |
| 1 | QA | A QA pass on the village, `language` then `progression` (9.3 I) | | |
| 2 | M65 | Fuzz stability: the four open failures, a skip budget, `scripts/try-lines.sh` (9.3 A) | the owner's yes (9.1) | S |
| 3 | M66 | The core's grading gaps, with M63 (9.3 B) | M65 | M |
| 4 | M67 | `vocab.wrong` only on a line that was not understood (9.3 C) | M65 | S |
| 5 | V20 | Free talk: the NPCs understand what they teach, practice is credited, cards come after their lesson, names in recasts (9.3 D) | M66, M67 | M/L |
| 6 | V21 | Smaller game fixes: the stall by item, feedback in each NPC's voice, `tener que` in the book, the Lessons page at `next:` (9.3 E) | V20 | M |
| 7 | V22 | Name plates (9.3 F) | the owner's design | S |
| 8 | V23 | The deferred small items (9.3 G) | V20 | S/M |
| 9 | M69 | The core milestones' left points: missed errors, never false alarms (9.3 B, last part) | M66 | M |
| 10 | M68 | The timed tests' budgets on CI, only if `main`'s CI shows one red or within reach (9.3 H) | | S |
| 11 | V24 | Documents and runs after the follow-ups, as "V09 again" did (9.3 J) | all | M |

The QA reports' confirmed bugs join the queue where they fit; a bug that blocks a lesson goes first. One PR at the end:
`feature/village-followups` into `main`.

### 9.3 Each item, with the recommended fix

**A. Fuzz stability (M65, core).** It goes first because each of these failures can turn a gate or CI red at random. They are listed in
`tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` under "left for a decision". A narrowing needs the owner's yes (9.1), and each guard gets a
test showing that what it leaves out is correct Spanish, as `AmountMutationGuardTests` does.

- **(a) `egVgLELrxyL4`, O4, the most frequent:** it fails about one fuzz run in six at the default depth. The `conj.tense` mutation lacks
  M26b's guard for a name inside a grammar alternative.
  - Fix: give the mutation M26b's guard, so a sentence whose name sits in an alternative is skipped. This is a narrowing.
- **(b) `b3HSKSLCsL_1` and `4YuZ2yubxdC1`, O5:** M59's `conj.person` mutation after a noun subject and `haber`. `unas televisiones han`
  becomes `ha` and `el subtítulo ha` becomes `han`.
  - M59 leaves `haber` out by design (`hay dos panes`). The generator also builds `haber` with no participle, which is not Spanish.
  - Since M64's gate it fails at the default depth too.
  - Fix now: the mutation takes M59's exclusion of `haber`, so it models what the analyzer claims to check. This is a narrowing.
  - Later, when a lesson teaches the perfect (`he comido`), widen instead: the analyzer checks `ha` and `han` against a noun subject
    when a participle follows (those two forms are never existential), and the realiser gives `haber` its participle.
- **(c) `2ldq2rIKLrG2`, O4, at five times the depth:** the `conj.tense` mutation where a grammar's literal decided a reading. With `ayer`
  added the grammar no longer matches, and `porque` reads as a noun.
  - Fix: keep a mutated case only when it matches the same grammar as the original. This is a narrowing.
- **(d) `2tpLGUOSqj41`, O5, at five times the depth, older than M64:** a `word_order` recast where `[WORD:item] [ADJ]` can bind the moved
  adjective as a name.
  - Recommended: leave it recorded, since it is rare and appears only at depth. The alternative is to skip `word_order` beside a
    `[WORD]` slot, which is a narrowing.
- **A skip budget (not a narrowing).** The realiser re-rolls whole sentence classes:
  - `skip:coordinated-subject`, `literal-verb-subject`, `colour-before-noun`, `noun-reads-as-adjective`, `ser-of-place`,
    `predicate-subject` and `time-marker-of-generated-words`;
  - one of them hid the review's R1 (`tú y yo fuimos` graded wrong).

  Add a test that counts each skip kind over a fixed-seed run and fails when one exceeds a share of the draws (for example 5 %), so a
  re-roll cannot silently eat a class of sentence.
- **Tooling: `scripts/try-lines.sh`** (the brief must allow `scripts/`).
  - Every milestone of the review used two throwaway wrappers, `/tmp/claude-1000/review/batch.sh` (the CLI) and `host1.sh` (the host,
    the game's path). Each takes a file of `prompt|line` and prints one result line per line.
  - Turn them into one script in the repository, with the review's 99 correct lines (`/tmp/claude-1000/review/lines1.txt`, `lines2.txt`)
    and M64's lines (`lines3.txt`) as line files beside it. Every later brief then says "the correct lines stay clean" with one command.
- **Proof:** three runs of the fuzz project at the default depth and one at five times it pass.

**B. The core's grading gaps (M66, core, with the filed M63; the left points as M69).** Try every line with the CLI on the bundle first.
Each fix gets a regression test in `tests/Vocaterra.Nlp.Es.Tests` with its clean neighbours, and the fuzz suite widens where the realiser
can model it.

- **False alarms, first:**
  - **`¿puede usted vender manzanas?` gives `conj.person` (expects `vende`).**
    - Cause: `CheckSubjectVerb`'s bare-infinitive rule (`yo comprar` gives `compro`) fires on a subject pronoun after its verb.
    - Fix: a subject pronoun right after a finite verb is that verb's subject (an inverted question), and the infinitive after it belongs
      to the verb. Clean: `¿puede usted vender…?`, `¿quiere usted comprar…?`, `¿va usted a comprar…?`. Still flagged: `usted vender
      manzanas`.
    - This matters most: `poder` is session 10's verb and `usted` its register.
  - **`ni tú ni yo fuimos` and `tú o yo vamos` are flagged** (M64 left them). Extend M64's coordination:
    - `ni … ni` is plural;
    - `o` accepts the plural and the form of the nearest member.
- **Missed errors a learner of these lessons makes:**
  - **A finite verb after a modal:** `puedo pago con fruta`, `quiero vendo madera`. After `poder`, `querer`, `deber`, `necesitar` and
    `tener que`, a finite form of a lexicon verb is reported with its infinitive as `expected` (`pago` gives `pagar`). Use the tag that
    `ir a` with a finite verb already gives (`voy a como` gives `comer`; `docs/nlp/API.md`, "Tense errors"), so the learner meets one
    kind of message.
  - **`¿cuánto cuestan todo?` gives no `conj.person`.** M56's price check agrees `costar` with the noun phrase after it. Extend it to
    `todo`, `eso` and `esto`, which are singular: `cuestan todo` gives `cuesta`, and `¿cuánto son todo?` gives `es`. The game's grammar
    for that question is V20 (e).
  - **`los panes costa`** (M58 left it): the slip reading of `costa` after a phrase that opens the line holds only for the singular.
    Extend it to the plural, giving `cuestan`. It belongs to session 10.
  - **`azuls`** (M58 left it): the plural-slip rule covers nouns only. Extend it to adjectives that end in a consonant (`azuls` gives
    `azules`). It belongs to session 7's colours.
  - **`ti` is not in the lexicon** (`para ti` gives `vocab.unknown`): add it as a prepositional pronoun, following `docs/nlp/LEXICON.md`.
  - **M63, as filed:** a subject pronoun before a question word (`ellos dónde están …`) is lost to M56's check.
- **M69, the left points of M56, M58 and M62** (all missed errors, never false alarms, so they come last):
  - M56: `son cerca`, `ser en` with a place, and the first and second persons after the verb;
  - M58: `podo` and `esto` read as the slip only under a grammar;
  - M62: `él es alta` with no grammar and no `muy`, `son` after a lone name, and the gender of a subject with `de …`.
- **Proof:** the line files of A give what they gave before, except the lines fixed; the core demo is exact.

**C. `vocab.wrong` only on a line that was not understood (M67, core).**

- **Problem:** correct, understood lines are scored as small mistakes (correctness 0.4), in free talk and at lesson steps:
  - `hola luz quiero dos panes`;
  - `usted comprará pan` at a lesson step;
  - `buscaré madera para luz` to Domingo, which gets "a small mistake" and a recast.
- **Cause:** `VerdictAssembler.DeriveVocabWrong` (`src/Vocaterra.Nlp/Evaluation/VerdictAssembler.cs`, about line 219). It adds
  `vocab.wrong` on the first content word whenever a constrained prompt's answer set has no match, even when a slot grammar understood
  the line and resolved an intent.
- **Fix:** derive it only when the line resolved no intent, or when it is a near miss of one answer (a fuzzy match below the threshold).
  Never derive it on a line a grammar understood. The derived tag is meant to explain a miss, and an understood line has none.
- **Proof:**
  - in the line files, the `vocab.wrong` on understood lines is gone, and lines that were not understood keep it;
  - re-record the goldens and the identity baseline, which will move, and list each change;
  - a game test that pinned the 0.4 is changed with the orchestrator's leave, as at M56 and M62.

**D. Free talk (V20, game, after M66 and M67).**

- **(a) Sentences their own NPC does not understand.** In free talk, 9 of phase B's 27 practised sentences get "No te entendí" from
  the NPC who taught them (section 5, item 4, lists them). The villager's, the fisher's and the stall's open prompts take no statements,
  and Domingo's takes no `tener que`.
  - Fix: each NPC's open prompt accepts the intents of the lessons it teaches (`state_action`, `describe`, `state_have`, `state_like`,
    and `tener que`'s). The NPC answers with a short line from its pools that keeps the talk going (a recast, «¡Qué bien!»).
  - Add a game test that runs every said row of a lesson through its NPC's open prompt, so this cannot regress.
- **(b) Practice not counted.** Free talk credits only the rows in an NPC's `focus`.
  - Fix: credit any phrase row of a lesson the learner has started or finished, at any NPC. The book's word records already count them.
- **(c) Example cards before their lesson.** An NPC shows its focus cards from the first talk: Luz shows `Vendo madera.` (session 10)
  in session 5, and León shows his A2 lines.
  - Fix: a card shows only once its row's lesson is the current one or done.
- **(d) Names in recasts.** Domingo's recast writes `luz` in lower case (`buscaré madera para luz`).
  - Fix, in the game when the line is shown: capitalise an NPC's name in a recast unless a determiner comes before it (`la luz` stays).
- **(e) `¿cuánto son todo?`** is understood with intent `unknown`.
  - Fix: `ask_total`'s grammar (`content/game-v1/intents/intents.json`) takes `son`. With M66 the core then gives `es`.
- **(f) `quiero cero panes` credits the phrase `quiero-dos-panes`,** though nothing changes hands (V19 left it).
  - Recommended: keep the credit, since the Spanish was right and practised the pattern. Say so in `docs/game/README.md`.
- **Proof:**
  - V09's measure again: all 27 practised sentences understood by the NPC who taught them;
  - the learner's route through sessions 7 to 10, with free talk once with each NPC;
  - the scripted runs, every difference explained.

**E. Smaller game fixes (V21).**

- **The stall answers any request with the bottle** (older, S6).
  - Fix: route the stall through V19's `ExchangeMiss`. Its items go in `host-events.json`, and another item gets `wont_sell`.
  - Make `shop_hay` («Hay agua y pan.») agree with what it sells.
- **The NPCs' `¡Muy bien!` and the other shared feedback lines play in Luna's voice.**
  - Fix: render the shared feedback pools once per NPC voice (`tools/Vocaterra.SpeechRender`, with the index keyed by speaker). The game
    plays the speaking NPC's file and falls back to the guide's.
  - Count the WAVs first (feedback lines × 6 NPCs × 2 speeds) and report the disk cost before rendering.
- **`tener que` has no book entry.** Add one where the book lists `ir a`, with `tengo que ir al río`.
- **The Lessons page opens at its top.** It runs to 192 lines, and in sessions 8 to 10 the `next:` lesson is far down.
  - Fix: open it scrolled to the `next:` lesson.
- **Proof:** unit tests; a screenshot of the Lessons page at session 9; the stall's lines through the host.

**F. Name plates (V22, after the owner's design answer).** Plates show through figures: Luz's plate over the shopkeeper's face on
session 8's morning, and the farmer's in phase A.
- Recommended fix: draw plates with the depth test against the scene and the figures, and show only the plate of the NPC the player
  faces within talking range (and the beacon's target).
- Proof: screenshots before and after at the two known places.

**G. The deferred small items (V23).** The owner wants all the follow-ups, so these are queued too, after V20.

- **`KeyWordsTaught` is one save flag.** Make it a set of NPC ids, with a save migration: an old save's flag means the shopkeeper's.
- **The session skip buys one apple, not three, for the market.** Buy what session 10's lesson assumes: three.
- **The host's `--transcript` mode has no bag.** Add an optional `--bag <item:count,…>`, so a sale's line follows the bag. Without it the
  host behaves as today.
- **Five old world words sit in the body of `content/game-v1/cefr/a1.txt`, not its marked block:** `madera`, `antorcha`, `botella`,
  `fuego` and `despacio`. Move them into the block, and check that the lint and the CEFR counts do not move.
- **The closing review is skipped when nothing is due.** Show one short line instead, so the learner knows it ran.
- **The book counts a use as correct only when it was understood at its prompt.** Count a correct use wherever it was understood, which
  follows D (b).
- **`WorldFacts.NearestOfNounJoinedTo` floods a water body with no bound.** Cap the visited blocks (for example at 4096) and fall back to
  the nearest block seen.

**H. The timed tests on CI (M68, only if needed).** At four times this machine's speed (the GitHub runner), V09's measures give:

| Test | Measured here | At four times | Budget |
| --- | --- | --- | --- |
| `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` | 1438 ms | about 5.8 s | 3 s |
| `WorldFactsTests`' snapshot | 0.50 s | about 2.0 s | 1.5 s |
| `SceneBuildTimeTests` | 708 ms | about 2.8 s | 3 s |

V19 measured the bundle load at 1248 ms. Ask the owner what `main`'s CI showed.
- Fix, as M60's: measure after a warm-up run, and scale every budget by a factor that CI sets in an environment variable
  (`VOCATERRA_TIMING_SCALE=4` in `.github/workflows/ci.yml`).
- The brief must name the workflow file, and it allows this one milestone to edit both halves' tests.

**I. The QA pass (first in the queue).** Run `subagent_type: qa` (`.claude/agents/qa.md`) with the area `language`, then `progression`,
one at a time.
- It builds and runs the game, so it counts as the one agent running.
- Its reports land in `docs/qa-reports/`, and its confirmed bugs join the queue.

**J. Documents and runs (V24, last), as "V09 again" did:**
- `docs/game/README.md`: the counts, and the open points rewritten;
- `PLAYTEST.md`;
- `SCRIPTED_RUNS.md`, against `1dbb0e5`;
- the speech checklist's counts;
- the learner's route through sessions 4 to 10;
- CI's case, the suite run without models.

**K. The owner's own tasks (not milestones).**
- **Listen to the voices.** None of the 914 speech files is marked.
  - Recommended order: the voice trial first (`docs/game/voice-trial/`, one sample per NPC), then spot-check each NPC's newest lines in
    `docs/game/SPEECH_CHECKLIST.md`.
  - A rejected voice re-renders only that NPC's lines.
- **The answers of 9.1.**
- **Not needed for this queue:** the core's older decisions D8 to D11 and D13.

**L. Housekeeping.**
- The merged branches `M64-review-core` and `V19-review-game` are still on the remote. The session's permission check refused to delete
  them; the owner can run `git push origin --delete M64-review-core V19-review-game`.
- `/tmp/claude-1000/review` holds the review's wrappers and line files: keep it until M65 has copied them into the repository.
  `/tmp/claude-1000/v09` can go.
- Disk has about 12 GB free. Reuse the worktree `.claude/worktrees/village`, now on `feature/village-followups`, rather than making a new
  one.

### 9.4 How to run it

- **Section 7's rules hold,** with `GATE_TARGET=feature/village-followups`. The closest models for briefs are
  `.claude/briefs/M64-review-core.md` (core) and `.claude/briefs/V19-review-game.md` (game).
- **Every brief keeps:**
  - correct learner lines staying clean (the line files of A);
  - the content loop for any content edit;
  - the learner's route;
  - CI's case;
  - one page of report.
- **Until M65 lands,** a gate that fails only on one of the open fuzz families of A is rerun once after checking the seed. Record the
  seed.
- **After each milestone:** record it, push `feature/village-followups`, and delete the milestone branch locally. The remote deletion
  needs the owner's leave.
