# Handover: the village series after phase B (2026-11-01)

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
