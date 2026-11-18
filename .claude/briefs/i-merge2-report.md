# I-merge2 report (main's throughput series, PR #70, into feature/inventory)

The agent's report, saved by the orchestrator (2026-11-18). Sonnet 5.5 at medium effort; brief `i-merge2.md`. Gated at `c3bd3c2b`.

Branch `I-merge2`: the merge 7ef8d1d5 of `origin/main` at `ae30d65b` and 8ecc5033 (ItemsSaveTests read the fixture through
`SaveFixture.Read`).

## Resolutions (14 files)

CLAUDE.md: main's B29 text, naming save-v10.json. content/README.md: main's hash wording and rehash paragraph, then this side's "Items
(I01)" section. scripts/content-loop.sh: main's fixtures step (writes nothing). decisions.md, findings.md: a union in order (main's items 57
to 59 and B29 first, then D-game-35 and the I entries), no decision reworded. handovers/README.md: main's rows plus the inventory row
(this side's older world-next row dropped, main's replaces it). save-v9.json deleted again. The seven save tests (Needs, Atmosphere,
DialoguePages, LoadKeepsDay, SaveMigration, SleepEntry, ObjectiveBook) read `SaveFixture.Read("save-v10.json")`, version 10 kept, both
sides' tests kept.

## Changed

`save-v10.json`'s bundle hash is the placeholder (B29c); its state digest verifies. `SaveFixture.cs`'s doc example names save-v10.json.
`ItemsSaveTests` (two I01 tests) read the raw file and failed the strict load against the placeholder: now through `SaveFixture.Read`. No
golden, digest pin or content hash moved; `scripts/rehash-bundle.sh` left the hash at 029e51f8. No wall-clock assert on this side
(`WallClockRulesTests` passes). Outside the game's area: `docs/nlp/changing-the-core.md` names save-v10.json (its hash-step wording may be
stale after B29c).

## Gate

The first gate run used the pre-merge `merge-gate.sh` (Debug, the timing tests in the suite, a loaded machine): 13,388 passed, 10 failed,
all CPU-budget timing tests (B29b's budgets are set for Release); the fuzz suite passed. Re-run with main's gate script: Release build 0
warnings; `test` 13,234 passed, 0 failed, 9 skipped (no fuzz, no timing); `timing` 23 passed; flaky none; format clean; content gate, grammar
and licence scans pass. 854 s of tests (14.2 min); peak 6.4 GB.
