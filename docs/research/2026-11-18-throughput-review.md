# A review of the throughput study (2026-11-18)

*A review by an Opus 5.5 session for the owner, who asked for the study [2026-11-17-ai-development-throughput.md](2026-11-17-ai-development-throughput.md) to be checked against the evidence and for further changes, ranked by the time they save for the work they cost. The owner approved every change below on 2026-11-18, with one exception: the chunk-speed milestones (0a to 0f of the handover [2026-11-18-0409-world-next](../handovers/2026-11-18-0409-world-next.md)) do not run on the throughput changes' branch, `feature/meta_improvements`. The owner added one decision: timing tests run only in the gate and CI, and the fuzz suite only in CI (item 7a).*

The review covers the same sources as the study (the gate logs in `.claude/gate-logs/`, the agent transcripts, `git log` of `feature/biomes`) and extends them to 2026-11-18 09:37 UTC, after B28, B30 and B31 had landed. It also reads `scripts/limited.sh`, `scripts/merge-gate.sh`, `scripts/test.sh` and the timing tests. All times are UTC. A number marked "(est.)" is an estimate; every other number was measured.

## 1. Summary

- **Most of the study holds.** Its measurements of the suite, the failed gates, memory and the fast tier match the logs.
- **Much of it has already landed.** B28 split the slow classes, raised Game.Core.Tests to 6 threads and made the fast tier fast: the gate's test phase is 25 to 29 minutes on a quiet machine (35 to 40 under load), down from 50. The brief template's rules cut polling sharply. B30 fixed the JobPool race.
- **Three points were wrong or underweighted**, and they change the order of the work:
  1. **Release tests** were estimated at 1.5 to 3 times faster. The chunk-generation review measured Release at 5 to 8 times Debug for generation, which is what the slowest Game.Core tests do. Release is now the largest lever left.
  2. **Wall-clock timing tests** were ranked ninth (1 to 2 hours a day). They are still the main cause of failed gates after B28.
  3. **The gate's waits for a slot** are real and continue, but their cause is that the gate asks `limited.sh` for a slot about 15 times per run. Holding one slot for the whole gate is far simpler than the five-token policy the study proposed.

## 2. The study's findings checked

| Study claim | Verdict | Evidence |
| --- | --- | --- |
| The suite grew from 7 to 50 minutes | Confirmed | 2026-11-15: the longest assembly (Nlp.Tests) took 3.5 to 4.9 minutes in two lanes. The 13:29, 14:23 and 15:30 gates on 2026-11-17 spent 50, 52 and 50 minutes on tests. |
| 6 of 13 gates on 2026-11-17 failed, five on timing tests | Confirmed | 14 gates on 2026-11-17 up to 16:45, 6 failed; five on timing, one (ResidencyInFlightTests) a real clash. The study's summary says 13 gates and its table 14. |
| Memory is not the constraint; Game.Core.Tests' 3 threads were | Confirmed | B28 raised it to 6 threads; the full run peaked at 5.5 GB. |
| The fast tier takes about 45 minutes | True then, fixed | After B28, `scripts/test.sh fast` takes 187 s for 11,830 tests. |
| Polling costs many agent turns and tokens | Confirmed, mostly fixed | Before the rule (58 agents): 3,522 of 10,160 Bash calls (35%) contained `sleep`, 122 used `nohup`. After it (9 agents from 17:10): 58 of 752 (8%), no `nohup`. Two agents still polled, one in 31 of its 60 Bash calls. |
| Release tests are 1.5 to 3 times faster | Underestimated | The chunk-generation review measured 5 to 8 times for generation ([2026-11-18-chunk-generation-speed.md](2026-11-18-chunk-generation-speed.md)). B28 found Game.Core.Tests bound by single generation tests over 5 minutes. |
| Timing tests cost 1 to 2 hours a day (item 9) | Underweighted | Since B28, 5 of 10 gates failed, at least 4 on a race or timing test that passed alone (JobPoolTests twice, fixed by B30; ProceduralReliefRegionTests twice). `ProceduralReliefRegionTests.cs:397` asserts on a raw `Stopwatch` outside the timing collection and failed 2 of the last 4 gates, B31's included. Item 9 does not name it. |
| The gate waits 2.8 hours for slots outside its test phase | Confirmed, continuing, cause understated | In the 13 gates since 16:45 on 2026-11-17, four waited 88 minutes in all outside the test phase (a build waited 46 minutes at 19:27). `merge-gate.sh` takes a slot separately for restore, build, test, format, five content steps, grammar and five licence scans; each can queue behind an agent's 26-minute test run. |
| Lead time and merges a day | Not re-measured | — |
| Considered and not recommended (affected assemblies only, parallel gates, a build cache, splitting Game.Tests, more agents) | Agreed | — |

## 3. What landed after the study

| Study item | Status |
| --- | --- |
| 1, a faster suite | Done by B28 (`87952df5`): nine to two classes per split, Game.Core.Tests at 6 threads, NamePlates' two game runs side by side. B27 and B27b cut the drainage cost. |
| 3, one full run per milestone | Done: about 35 classes marked Slow (B28); the rule is in the brief template. |
| 7a, two milestones per gate | In use (M100 with B28; B11c with B27b). |
| 8, agent rules | In the brief template (2026-11-17 17:07); measured in section 2. |
| 9, timing tests | Partly done (M100, B24, B24a, B30); the rest is item 4 below. |
| 2, 4, 6, 7b, 10 | Planned as B29, one M milestone with eight deliverables, queued behind the chunk-speed series and B32 to B34. |

## 4. Changes ranked by time saved for the work

The owner approved all of these on 2026-11-18. Savings are estimates against the machine and suite after B28.

| # | Change | Saves (est.) | Effort | Source |
| --- | --- | --- | --- | --- |
| 1 | **Release tests**, measured once first: `scripts/test.sh`, the gate and CI's `build-and-test` build and test with `-c Release`, the goldens checked unchanged, each CPU timing budget re-set to twice its Release cost. | The suite from about 26 minutes to 6 to 10; about 8 to 12 agent-hours and 4 gate-hours a day | S | Study item 2 (B29 part A) |
| 2 | **The gate holds one slot for its whole run.** `merge-gate.sh` runs itself once under `limited.sh`; its inner calls then run directly, because `limited.sh` is already re-entrant (`VOCATERRA_LIMITED=1`). The whole gate stays inside one scope's caps (10 GB, 10 cores); its largest step, the suite, peaks at 5.5 GB. | About 1.5 hours a day of gate waiting, which sits on the path to every merge | XS (a few lines) | New |
| 3 | **The gate re-runs its failed tests alone** (`--filter` on each failed test's full name) and labels each FLAKY (passed alone) or FAILED. Flaky tests are listed with the result and recorded as findings, as the orchestrator now does by hand; a test that flakes twice goes to item 4. | The manual re-run and judgement at about half of the gates, and some re-gates of 30 minutes each | XS to S (about 20 lines) | New |
| 4 | **No more wall-clock asserts.** Move the remaining asserts on elapsed time to CPU-time budgets in the serial timing collection, and add an architecture test that rejects an assert on a `Stopwatch` outside it. Candidates from a search (each to be checked): `ProceduralReliefRegionTests`, `DrainageNetworkTests`, `FarTerrainSamplerTests`, `GuideTurnTests`, `WorldFactsTests`, `SpeechUxFrontendTests` (11 asserts) and `LintTests`. | Removes the main cause of failed gates | S | Study item 9, widened |
| 5 | **A test-time report in the gate**: each assembly's time and its largest class's time against the last gate's; a warning past 10% growth or a class over 5 minutes. | Stops the next unnoticed +15 minutes (B07f and B02c each added that) | XS to S | Study item 10 (B29 part D) |
| 6 | **The hot spots**: the save fixture checks the bundle hash against `manifest.json` instead of pinning it; a re-hash script; the orchestrator writes the README, `running.md` and `architecture.md` notes when it records a milestone. | Fewer conflicts (16 of 48 agents met one, each costing a merge and a full run) | S | Study item 6 (B29) |
| 7 | **A hook that enforces the waiting rule**: a PreToolUse hook on Bash rejects a `sleep` over about 30 seconds and `until … sleep` loops. | The rule cut polling from 35% to 8% of Bash calls; the hook removes the rest and the orphaned loops | XS (settings) | New |
| 7a | **Timing tests run only in the gate and CI; the fuzz suite only in CI** (the owner, 2026-11-18). Every timing test (the 10 classes in the two `TimingCollection`s, plus the tests item 4 moves there) gets a trait, and `scripts/test.sh` (fast, full and project) leaves them out, so an agent's run never fails on timing under another agent's load. An agent whose milestone changes performance or a timing test runs them on purpose (a `timing` mode of `test.sh`). The gate runs them as their own step after the suite, so they do not compete with the gate's own test hosts; a failure there gets item 3's re-run. `Vocaterra.Nlp.Fuzzing.Tests` leaves the gate and `test.sh` and runs in CI's `build-and-test` only; its oracles are never weakened. | Agents stop chasing timing failures that are not theirs; the gate stops failing on older core bugs found by a new seed (2 of 10 gates since B28, 4 of 51 in the study); the fuzz suite's 2 to 5 minutes leave every full run and the gate (it sits in the gate's shorter lane, so the gate's wall time falls by 0 to 3 minutes) | S, in B29a | Owner, 2026-11-18 |
| 8 | **Deferred: the five-token slot policy and the stamp skip** (B29 parts B and C), re-measured after items 1 to 3. With runs about three times shorter, slots free three times sooner, and the stamp skip would save only about 8 minutes a gate while trusting stamps written by agents. | Avoids an M of work that may no longer pay | — | Study items 4 and 7b |
| 9 | **Then the longest single tests**: any Game.Core test still over 5 minutes in Release (B28 named the chunk-order tests and `LodStoreRingEditRaceTests`) gets fewer samples or split theory data, with no assertion weakened. | The tail Release leaves | S | New |

## 5. Order

The throughput changes run on their own branch, `feature/meta_improvements`, cut from `main` after pull request #69 was merged. The chunk-speed series (0a to 0f) does not run on it (the owner, 2026-11-18).

1. **B29a** (S, Sonnet 5.5 medium): items 1, 2, 3, 5 and 7a, all script and trait changes.
2. **B29b** (S, Sonnet 5.5 medium or Haiku 5.5): items 4 and 6.
3. **Item 7** as a settings change.
4. **Re-measure** the suite, the gate and the slot queue, then decide item 8. Item 9 follows if a class is still over 5 minutes.

**What item 7a needs besides the scripts:**

- **CI must run where the work is.** CI runs on pushes to `main`, on pull requests and by hand (`.github/workflows/ci.yml`), so a branch whose work needs CI keeps a pull request open (a draft until it is ready) and every push runs it (the owner, 2026-11-18, D-game-34 item 59). No new trigger is needed.
- **The orchestrator reads CI.** The token's scopes now allow `gh run list --branch <branch>` and `gh run view`; the orchestrator checks CI after each push (item 59). Each fuzz failure becomes a core milestone, as now.
- **The documents.** When B29a lands, CLAUDE.md's "The fuzz suite is a gate; never weaken an oracle to make it pass" becomes "The fuzz suite runs in CI; never weaken an oracle to make it pass", `scripts/test.sh full` stops saying it runs the suite "as the gate and CI run it", and `.claude/agents/orchestrator.md` says which tests the gate runs.

| | After B28 (2026-11-18) | After B29a and B29b (est.) |
| --- | --- | --- |
| Gate's test phase, quiet machine | 25 to 29 min | 6 to 10 min |
| Whole gate | about 27 min, up to 94 with slot waits | about 10 min |
| Gates failing on a test that passes alone | about 4 in 10 | about 1 in 10 |
| Gates failing on an older core bug found by a fuzz seed | about 2 in 10 | none (found in CI) |
| `scripts/test.sh fast` | 187 s | under 60 s |

## Method and caveats

- **Gate phases** come from each log's creation and last-write times (btrfs), as in the study; assembly times and failed tests from the `dotnet test` output in each `test-*.log`.
- **Polling** was counted from the Bash tool calls in the subagent transcripts of the orchestrator session `bf9afb01`, split at the first timestamp of each agent (17:10 on 2026-11-17, when the rule entered the brief template). Its script is `artifacts/tmp/meta/scripts/review-polls.py`, outside git. Its denominator is Bash calls, not turns, so it is not directly comparable with the study's 14% of turns.
- **The wall-clock candidates** come from a search for asserts on `Elapsed` or a watch in test files; some may be false positives.
- **The Release estimate** scales the measured 5 to 8 times on generation down for tests that do not generate; B29a measures it.
- **One failure not classified:** the 03:40 gate on 2026-11-18 failed two `BiomeTourSeedsTests` cases; their cause was not checked.
