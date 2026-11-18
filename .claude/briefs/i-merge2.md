# Brief: I-merge2, main's throughput work (PR #70) into feature/inventory

Sent 2026-11-18 to a general-purpose agent, Sonnet 5.5 at medium effort. Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i-merge2`, branch `I-merge2`, cut from `feature/inventory` at `6135a540` (which already holds
main up to `bb89521b`, the biomes series, through I-merge and I-merge-fix). The owner (2026-11-18): "more changes have been merged to main,
these are mostly to make the test suite run faster and improve development speed", then "pls start the work again now, starting with the
merge from main fixes".

```
You are implementing I-merge2: merge `origin/main` (now ae30d65b: 33 commits since bb89521b, PR #70, the B29 throughput series) into
the inventory series. Read .claude/briefs/i-series-standard.md (shared rules), then .claude/briefs/i-merge-main.md (how the two sides
meet; it still holds), then .claude/briefs/i-merge-fix-report.md (what the first merge settled).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i-merge2, branch `I-merge2`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i-merge2 &&`; absolute paths for file tools; never edit another checkout. `git fetch
origin`, `git merge origin/main` (no rebase, no squash, no history rewriting). Commit the merge once it builds (`I-merge2: Merge
origin/main (the throughput series, PR #70) into the inventory series`, the body listing each conflicted file and its resolution); later
fixes separate, prefixed `I-merge2: `. Do not push.

What main changed (read main's CLAUDE.md, scripts/README.md and the B29 reports in .claude/briefs/b29*-report.md after the merge): Release
builds for tests and CI; `scripts/test.sh full` runs without the fuzz suite and the timing tests (`timing` and `ci` modes added);
wall-clock asserts became CPU budgets in timing collections, `HangGuard`, `WallClockRulesTests`; the save fixture pins no bundle hash (a
placeholder, read through `SaveFixture.Read`); `scripts/rehash-bundle.sh` (hash plus validate; a manifest.json conflict is resolved by
taking either side and running it); the content loop's fixture step writes nothing; merge-gate.sh in one limited.sh slot with flaky
re-runs; a PreToolUse polling hook (scripts/hooks/no-polling.py, .claude/settings.json).

The trial merge conflicts in 14 files: CLAUDE.md, content/README.md, docs/game/decisions.md, docs/game/findings.md,
docs/handovers/README.md, scripts/content-loop.sh, tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json, and the tests NeedsSaveTests,
AtmosphereSaveTests, DialoguePagesSaveTests, LoadKeepsDayTests, SaveMigrationTests, SleepEntrySaveTests (Save/) and ObjectiveBookTests.

Rules:
- Save version 10 and `save-v10.json` stay (this side). Bring main's B29c change into save-v10.json (the bundle hash a placeholder, read
  through `SaveFixture.Read`) and delete save-v9.json again. In the save tests, take main's way of reading the fixture and this side's
  version 10, its file name and its worn items and active slots; keep both sides' tests. Any test or doc main pointed at save-v9.json
  points at save-v10.json; check `SaveFixture` itself names the right file.
- scripts/content-loop.sh, CLAUDE.md, content/README.md: main's B29 behaviour wins; keep this side's lines where they add something
  (save-v10.json, version 10, the inventory series' content). docs/game/decisions.md, findings.md, docs/handovers/README.md: a union in
  order, no decision reworded. Leave docs/game/inventory-plan.md, the implementation plan's I rows and .claude/briefs/ to the orchestrator.
- Any test this side added since bb89521b's base (the I series: I01 to I04, tests/Vocaterra.Game*.Tests) that asserts wall-clock time
  must follow main's new rule (WallClockRulesTests tells you): make it a CPU budget in a timing collection or drop the assert, and list it.
- The content manifests: take either side and run scripts/rehash-bundle.sh (through a wrapper only if the sandbox refuses it).
- No game window.

Gate (from the worktree's root; main's tools now): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0
warnings); scripts/test.sh full (passed, failed, skipped; compare with I-merge-fix's 13,388 passed + 3 failed, 9 skipped before its
fixes, whose expected count was 13,391; main's mode now leaves out the fuzz and timing tests, so say what the mode ran); scripts/test.sh
timing; scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean; scripts/limited.sh scripts/content.sh validate; the A2
content gate (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`).

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i-merge2,
at most 30 lines printed; never background them; never poll with sleep or pgrep (the new hook refuses it anyway). A wait for a
limited.sh slot is normal.

Report, one page: commits; each conflicted file and its resolution in a line; every test, golden, digest and fixture changed and why;
wall-clock asserts on this side changed; the counts and what each mode ran; files outside the game's area touched; anything left
undone. Leave the worktree clean on your branch with no process of yours running. Commit messages: subject, blank line, body, blank line,
then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
