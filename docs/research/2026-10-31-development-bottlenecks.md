# Development bottlenecks and a plan (2026-10-31)

Written during phase B of the village series (`feature/village`, V13 in progress). Nothing was built or run. The sources:
- the gate logs (`.claude/gate-logs/` in the main checkout and each worktree: 76 runs, 10-21 to 10-31);
- the agents' TRX files (`.claude/test-timings/`);
- `git log --all` (762 commits);
- the findings logs (`docs/game/IMPLEMENTATION_PLAN.md` section 5, `docs/nlp/IMPLEMENTATION_PLAN.md` §5 and §5b, `docs/game/VILLAGE_PLAN.md`
  sections 4 and 5, `docs/process/test-performance-plan.md`);
- the Claude Code transcripts (125 files, 17 sessions and 108 agents, 764 MB, streamed; the running session was left out).

**Conventions.**
- A number with no mark is measured; "(est.)" marks an estimate.
- "API-eq. $" weights tokens by the published prices: Opus 5.5 $4 in, $20 out, $0.20 cache read; Sonnet 5.5 $2, $10, $0.20; Haiku 5.5
  $0.10, $0.50. Cache writes are taken at 1.25 times the input price and Haiku's cache read at $0.01. It is a relative weight, neither the
  owner's bill nor the usage meter.
- "Phase A" means V01 to V12c, V08 and the core's M60: 11 Opus xhigh agents, 10-30 19:46 to 10-31 12:59.

## 1. Summary

1. **Context times turns is the cost.**
   - Phase A's 11 agents used 954M tokens (97 % of them cache reads), about $272 API-eq.
   - One content milestone (V10, V11, V12) used 117M to 179M tokens over 269 to 347 turns, and its context grew to 680K to 800K.
   - 10-29 to 10-31 burned about $433. The three days that ended in the 94 % usage pause (10-21 to 10-23) burned about $499.
2. **A milestone's wall-clock is the agent's own time, mostly model time.**
   - Content milestones run 66 to 90 min.
   - Across phase A, 58 % of agent time is the model, 29 % builds, tests and content tools, and 13 % reads and polling.
   - The gate takes 175 s, and the orchestrator turns a milestone round in 4 to 6 min.
3. **Defects surface after the merge.**
   - 3 of the 11 phase A runs were follow-ups (V12b, V12c, M60): 22 % of agent time and 20 % of the cost.
   - V12c came from the owner's playtest.
   - M60 came from CI, and the owner had to relay it, because the token cannot read CI.
4. **The content loop is mostly mechanical churn.**
   - Every content edit moves seven artefacts.
   - The manifest and both save fixtures changed in 15 of phase A's 55 non-merge commits.
   - Each content milestone hit a full run with 8 to 11 failures before re-recording.
   - Grammar faults show only in the full tier.
5. **Core gaps and the owner's checks pile up.**
   - M56 to M59 cover at least 12 misgraded learner slips. All are filed, none is started, and the content works around them. M59(a) lies on
     V14's path.
   - 0 of 680 speech files have been heard by the owner.
   - CI results are unreadable (403).

## 2. Evidence

### 2.1 Tokens and cost (transcripts)

| Group | Runs | Tokens | API-eq. $ | Note |
|---|---|---|---|---|
| S series agents (Sonnet, Haiku) | 37 | ≈ 1.9B | 285 | S13, S17, S26 at $29 to $33 each |
| Phase A agents (Opus xhigh) | 11 | 0.95B | 272 | V10 $46, V12c $42, V12 $34, V11 $32 |
| Work done inside a session (R16 phase 2, C and T series) | 3 | ≈ 0.62B | 162 | the C-series session alone $111, 871 turns, context 962K |
| Orchestrator sessions | 14 | ≈ 0.35B | 115 | phase A's: $21, context 357K, two compactions |
| Core, P, X, research and review agents | 60 | ≈ 1.0B | 151 | |
| **All** | **125** | **4.8B** | **985** | |

- **Mechanism.**
  - Cache reads are 51 % to 90 % of each large agent's cost.
  - Every turn re-reads the whole context, so a milestone costs roughly its turns times its average context (V10: 347 turns at 516K).
- **What fills the context.**
  - Phase A's ten V agents took in 7.1M characters of tool output.
  - 66 % of it came from 1,449 shell reads (`grep`, `sed -n`, `cat`, `tail`), 13 % from `Read` and 7 % from `git`.
  - Test and build output is about 3 %, because `scripts/test.sh` already summarises.
  - 49 % of the V agents' output tokens are thinking (1.08M of 2.19M).
- **What the brief asks the agent to read.**
  - The drafted V13 brief lists `VILLAGE_PLAN.md` "all of it" (71 KB), `content/PHRASES.md` (78 KB) and `docs/game/SPEECH_CHECKLIST.md`
    (71 KB, a generated table).
  - Read in full, that list holds ≈ 70K tokens in context for the rest of the run (est.: about 10 % of a content milestone's cost).
  - `VILLAGE_PLAN.md` was read whole 15 times across all the sessions.
- **The model is not the lever.**
  - At API prices the same tokens would cost $231 on Sonnet (−15 %, since the cache-read price is the same) and $12 on Haiku.
  - Context size and turn count are what cost.
- **The weekly limit.** Phase B at phase A's rate is about 1.0B to 1.3B tokens and $250 to $320 (est.), on top of a burn rate like the one
  before the 10-23 pause.

### 2.2 Wall-clock (gate logs, TRX, transcript timestamps)

| Step (this machine, through `limited.sh`) | Now | Before |
|---|---|---|
| Gate build (restore and build) | 21 to 24 s | 40 to 48 s (10-22, 10-23) |
| Fast tier | 35 s, 5,757 tests | 26 s, 5,142 tests (10-29) |
| Full suite | 95 s, 6,079 tests | 35 s (10-21); 172 to 180 s (10-27); 84 s (V01); 150 s (V12, V12b, before M60) |
| Format, content gate and validator | ≈ 46 s | 30 to 55 s |
| **Whole gate** | **175 s** | 80 s (10-21); 240 to 254 s (10-23, 10-27); 156 s (V01); 223 s (V12) |

- **The slowest tests.**
  - The fuzz suite takes 53 s: O15 `ContentSchemaFuzzTests` 45 s, `DecisionFuzzTests` 40 s.
  - Game.Core.Tests takes 52 s: `SceneTests` 40 s of class time.
  - Nlp.Tests takes 30 s. It was 77 s before M60: the lint grew with the content.
  - The suite grows with the bundle: the fast tier grew 35 % in two days.
- **Gate outcomes.**
  - Of the 76 logged runs, 5 failed:
    - two fuzz coverage flakes, which became M29e and M29h;
    - a fuzz regression, which M49 fixed;
    - one run with three `SpeechUxTests` failures;
    - the first required grammar run.
  - All 12 phase A runs passed first time. One of them duplicated another: V08's agent ran the gate itself.
- **Phase A's 607 active agent minutes:**
  - ≈ 354 min of model time (58 %);
  - 177 min of heavy commands (29 %): tests, builds, `content.sh`, prerender, scripted runs, LanguageTool;
  - 76 min of reads, git and polling (13 %).

  V10, V11 and V12 ran 87, 66 and 90 min, with 26, 18 and 32 min of heavy commands. From V01 to V12b (12 plan milestones in 8 h 34 min),
  93 % of the elapsed time was agent time.
- **Lock contention.** When two sessions ran at once (10-29), their gates waited on the lock 10 times and took 636 s and 1,001 s, against
  about 250 s alone.

### 2.3 Rework

| Phase A follow-up | Found by | Agent time | API-eq. $ |
|---|---|---|---|
| V12b: the village NPCs had no `focus`, so free talk credited the wrong phrase | V09's scripted runs, three milestones later | 26 min | 7 |
| V12c: the lessons could not be reached from a new world | the owner's playtest | 76 min, sent back once at review | 42 |
| M60: the lint's timing test took about 6 s on the GitHub runner | CI on PR #20, relayed by the owner | 30 min | 6 |

- **Root causes.**
  - The tests and the runs used shortcuts (`--lesson-run`, `--session n`), never the learner's route.
  - The scripted runs are not in `dotnet test` and have no committed baselines (V02's finding).
  - Timing budgets were set on a machine about four times faster than CI.
- **Earlier work.**
  - 32 suffixed follow-up ids (M26a to M26g, M29c to M29j, S17b, S19b, S20b, V12b, V12c).
  - The owner's playtest of S12 produced S16 and S17b, and S22 "known issues" took 72 min.
  - Four fuzz flakes became core milestones (M16b, M29e, M29h, O4).
  - The two crashes on 10-22 cost four resume agents (≈ 65 min).
  - The usage pause stopped X06 mid-work for a week.
- **Churn.**
  - Phase A's 67 agent commits: 31 work, 13 plans and findings, 12 merges, 7 re-recordings only (the identity baseline and the fuzz stream
    digests) and 4 docs.
  - The game bundle's `manifest.json` was touched by 68 commits in the project, `save-v1.json` by 40.

### 2.4 The content loop

Every content edit moves seven things:

1. the manifest and provenance;
2. the grammar strings and the cache (a live LanguageTool server, then the cache-from-nothing check);
3. the identity baseline;
4. the two `FuzzLanguageStreamTests` digests (re-recorded at V10, V11 and V12 with the generators unchanged; since the core's M57 they run on
   frozen inputs, so a content edit no longer moves them, and the content loop's step 7 checks that they did not move);
5. `save-v1.json` and `save-v4.json`;
6. the WAVs at two speeds;
7. the scripted runs, captured before and after.

The first full run after the edit failed with 10 tests at V10, 11 at V11 and 8 at V12: goldens, digests, and fuzz oracles reacting to the
new grammars. The fuzz suite is not in the fast tier, so those failures show only in a full run (about 2.5 min with the build). V10 made 33
test runs and 15 `content.sh` calls, V12 38 and 13. `scripts/content.sh refresh` covers steps 1 and 2 and the gate's content checks, but
none of steps 3 to 7.

### 2.5 Core gaps, listening, blind spots

- **Core gaps.** M56 to M59 are filed and not started. Each has a workaround in the content:

  | Gap | Workaround |
  |---|---|
  | M56: `ser` with places | `gender.agree` examples |
  | M57: future markers | later markers kept out |
  | M58: `cuánto costa` | `cuánto costan` |
  | M59: the person check after a name | a pronoun before the verb |

  Where they meet phase B:
  - V14 retells the day in the third person and the plural with named villagers, which is exactly M59(a): `Domingo trabajo …` gives nothing.
  - V17 waits on M57.
  - V15's prices sit beside M58.
- **Listening.**
  - 0 of 680 rows in `docs/game/SPEECH_CHECKLIST.md` are marked: 340 lines at two speeds, 62 MB.
  - Phase B adds about 240 files.
  - A voice rejected late re-renders its NPC's lines and moves the hash again.
- **What the gate and CI miss.**
  - The learner's route.
  - The scripted runs.
  - CI's no-models path: the gate always runs with models.
  - CI runs no lint, no cefr-check and no `--validate-content`.
  - Thin timing margins against the ≈ 4× slower runner. `LoadingTheBundleIsFastEnough` has a median of 1.0 to 1.2 s against a 3 s budget
    (2.5× to 3×), and phase B adds about 35 % more WAV bytes for the load to hash (est.).

### 2.6 Recurring friction

| Obstacle | Times | Cost; state now |
|---|---|---|
| Worktree-isolation refusals (complex commands, `export`, `eval`/`hash`) | 505, 79 % of 637 Bash errors, 10-21 to 10-28 | A wasted turn each. Gone since agents share the worktree: since 10-30, 23 errors in 2,501 Bash calls. |
| `eval`/`hash` words | 17 refusals logged, 707 wrapper calls, one manual approval by the owner | Handled by wrappers and `refresh`; small now. |
| Exit 137 | 3 (10-22) | A rerun each; the caps work. |
| Desktop crash | 2 (10-22) | About 65 min of resume agents and a restart; none since `limited.sh`. |
| Usage limit | 1 pause (10-23) | X06 stopped; a similar burn rate now. |
| CI unreadable (403) | every result | An owner round trip per failure (M60: about 1 h). |
| Context compaction | 6 in 5 sessions | Lost detail; the owner asked for a clean handover. |
| `pkill`/`pgrep` self-matches | 14 (exit 144) | Minor. |

## 3. The plan, ranked

**P1. Lean context in every brief.**
- **Size and where:** S, in the orchestrator's briefs.
- **When:** now, from V14's brief.
- **What to change:**
  - Name sections, never "all of it".
  - Never read `SPEECH_CHECKLIST.md`, `manifest.json` or the whole of `PHRASES.md`: grep the rows instead.
  - Send every heavy command to a log and print at most 30 lines.
  - Keep `sed -n` ranges under about 150 lines.
  - Wait with Monitor or `until`, not repeated `tail`.
  - Keep the report to one page.
- **Gain:** 20 % to 30 % fewer cache-read tokens, about 25M to 45M tokens ($7 to $12), per content milestone (est.).
- **Check:** rerun section 2.1's aggregates on V14, against V10 to V12 (average context 434K to 516K; 117M to 179M tokens).
- **Risk:** an agent misses a rule. The section pointers keep each rule findable.

**P2. One command for the content loop.**
- **Size and where:** S to M, in `scripts/`, as a small tooling milestone.
- **When:** between V13 and V14.
- **What to change:** extend `scripts/content.sh refresh` so that one command:
  1. runs today's refresh;
  2. prerenders the changed lines and prints the `--check` summary;
  3. writes the bundle hash into the save fixtures;
  4. on `--record` only, re-records the identity baseline and the stream digests, printing every moved entry;
  5. runs `test.sh fast` and the grammar-sensitive fuzz classes at full depth (`CorrectSentence`, `MutationTag`, `RecastLoop`,
     `FuzzLanguageStream`), which respects the owner's T5 decision;
  6. ends with one summary.

  No agent command line then needs the words `eval` or `hash`.
- **Gain:** 30 to 60 fewer turns per content milestone, 10 to 15 min and 15M to 30M tokens (est.). Grammar faults show in about a minute,
  and fewer steps get forgotten.
- **Check:** `content.sh`, wrapper and test calls per milestone, against V10's 15, 12 and 33.
- **Risk:** a moved golden could pass unseen. Against that: recording only on request, the report explaining each move, the orchestrator
  reading the identity diff, and the gate unchanged.

**P3. Fix the core gaps before the content that needs them.**
- **Size and where:** S to M each, as core milestones.
- **When:** now, between content milestones, one agent at a time.
- **What to change:** run them in this order.
  1. M57, with part (c) first: the stream digests pinned to a fixed fixture, so content stops moving them.
  2. M59 (a) to (c), before V14.
  3. M58 and M56, before V15.

  This is in the orchestrator's remit: a core change the game needs is a core milestone that names the game milestone waiting on it.
  Report it to the owner.
- **Gain:** V14 teaches the real slips, a re-recording step goes from every content milestone, and a likely V14b is avoided (est.).
- **Check:** the "meanwhile" notes in `content/PHRASES.md`, and the digests not moving at V14.
- **Risk:** a core change re-records the identity baseline. Run it alone and never narrow an oracle.

**P4. Test the learner's route in every content milestone.**
- **Size and where:** S, in the brief, as one deterministic scripted run plus the orchestrator's spot check.
- **When:** now.
- **What to change:**
  1. From a new world, or from `--skip-to-session n−1`, sleep at the fire into session n.
  2. Check the Lessons page's `next:` line, the objective and the beacon.
  3. Play the lesson with `--lesson-run`.
  4. Talk freely once with each NPC (V12b's class of bug).
  5. Later, make it a slow test.
- **Gain:** a V12c- or V12b-type defect is caught inside the milestone. That saves a 26 to 76 min follow-up and an owner round trip.
- **Check:** no follow-up from V09 or the owner's review.
- **Risk:** a few minutes per milestone.

**P5. Check CI parity locally.**
- **Size and where:** S, in `scripts/test.sh` (a `--no-models` switch) and the briefs.
- **When:** now.
- **What to change:**
  - Run the full suite without models once per content milestone, which is CI's case.
  - Audit the wall-clock budget tests from the TRX files against the ≈ 4× runner factor.
  - Where the margin is under 4×, make the code faster, as M60 did. The bundle load comes first.
- **Gain:** the next CI-only failure, likely as the bundle grows (est.), is caught before the owner relays it.
- **Check:** CI on PR #20 stays green.
- **Risk:** none to the gate.

**P6. Rotate the orchestrator.**
- **Size and where:** S, in the process.
- **When:** between milestones.
- **What to change:** start a clean session from the handover every three or four milestones, or at about 300K of context. The owner
  proposed this on 10-31.
- **Gain:** fewer compactions. Phase A's orchestrator reached 357K and compacted twice.
- **Risk:** a lost instruction, if the handover misses it.

**P7. Speed up the full suite.**
- **Size and where:** S to M, test and core work.
- **When:** after phase B.
- **What to change:**
  - Order the test assemblies into balanced lanes (T6).
  - Split `SceneTests`, `VillageTests` and `SaveMigrationTests`.
  - Give `ContentLoader` an in-memory file source for O15 (a core milestone).
  - Hash the WAVs as a stream.
- **Gain:** full suite from 95 s to about 70 s, fuzz suite from 53 s to about 20 s (est.).
- **Risk:** the test layout churns.

**P8. Gate the scripted runs.**
- **Size and where:** M, a game milestone.
- **When:** after phase B, or with V09.
- **What to change:** commit the runs' baselines and compare them in a slow test.
- **Gain:** a V12b-type difference fails the gate.
- **Risk:** a baseline to re-record when a run is meant to change.

## 4. Owner decisions

Each of these changes a rule the owner set on purpose.

1. **Two agents at once,** with every heavy command still behind the one lock (for example, M57 or V16 beside a content milestone)?
   - For: about 30 % to 50 % more throughput (est.), because the heavy lane is idle about 70 % of a milestone. Peak memory is unchanged,
     because the lock serialises the heavy commands; the 10-22 crashes came from two unlimited builds.
   - Against: a second worktree (several GB), merge conflicts on `content/game-v1` and the digests, and gates 2.5 to 4 times longer under
     contention (10-29).
2. **Opus at xhigh for every V milestone?**
   - Options: keep Opus, but at effort high for the mechanical loop, or allow a Haiku helper for prerendering, re-recording and capturing
     the runs. The design and the Opus pass over the Spanish stay on Opus.
   - Thinking is 49 % of Opus output. At API prices Sonnet saves only about 15 % here, and Haiku about 95 % at a quality risk.
3. **One agent per milestone?** A content milestone could be split across two agents: first the Spanish, the design and the data, then the
   WAVs, the grammar, the runs and the docs. That halves the average context, for about 35 % to 45 % fewer tokens (est.), at the cost of a
   second brief and a hand-off.
4. **CI.**
   - Give the token read access to checks and actions, so the orchestrator reads CI itself.
   - Run CI on feature-branch pushes (open since 10-23).
   - Decide whether CI runs the content gate.
5. **Listening.**
   - A sample before V15: one line per voice at each speed, and phase B's lines at normal speed, instead of 900 or more files at the end.
   - A voice rejected early re-renders one NPC once.

The resource caps, never pushing `main`, the full gate and the full-depth fuzz suite stay as they are; nothing above needs them changed.

## 5. Not worth doing

- **Speeding up the gate.** At 175 s it is about 3 % of a milestone, and phase A's runs all passed first time.
- **Raising the memory cap, the cores or `-m`.** Exit 137 happened three times in all, and the work is model-bound.
- **A shallower fuzz pass, or dropping the identity guard.** The owner declined T5, and the fuzz suite found V12's `qué tú hace` and every
  grammar fault so far.
- **Going back to `isolation: worktree`.** It produced 505 refusals.
- **Moving to Sonnet to save money.** About 15 % at API prices.
- **Re-recording goldens inside the gate.** It would hide behaviour changes.
- **Rewriting or splitting the plans.** The cost is exploration output, and section pointers are enough.
- **xUnit v3 or Microsoft.Testing.Platform.** Start-up is about 1 s per assembly (T6).
- **Running the scripted runs in parallel.** That breaks the one-heavy-command rule, for 3 to 8 min saved.
