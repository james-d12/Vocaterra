# Brief: I-merge-fix, finishing the merge of main into feature/inventory

Sent 2026-11-18 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i-merge-fix`, branch `I-merge-fix`, cut from `feature/inventory` at `646fc48e`. The first
agent (brief `i-merge-main.md`) resolved every conflict and was stopped by the owner before any check; the orchestrator committed its
state as `e32b508d` and merged it into `feature/inventory` (`55d88fbd`). The owner then said to go ahead.

```
You are finishing I-merge: `origin/main` (the biomes series, PR #69, at bb89521b) is already merged into feature/inventory, with every
conflict marker resolved, but nothing was checked. Read .claude/briefs/i-series-standard.md (the shared rules), then
.claude/briefs/i-merge-main.md (the merge's rules: what each side did and how they meet; they still hold), then the "Stopped: I-merge"
entry of docs/handovers/2026-11-15-1436-inventory.md.

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i-merge-fix, branch `I-merge-fix`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i-merge-fix &&`; absolute paths for file tools; never edit another checkout. Commits
prefixed `I-merge: `; no history rewriting; do not push.

Steps:
1. Review the resolution: the merge commit is e32b508d (parent 1 the inventory side 9c2ca67b, parent 2 origin/main bb89521b). For each
   of the 37 conflicted files (its commit body and `git show --cc e32b508d` list them), check both sides' changes survive (`git diff
   e32b508d^1 e32b508d -- <file>` shows what main brought; `git diff e32b508d^2 e32b508d -- <file>` what this side brought). The first
   agent stopped while on the documents: review docs/game/saves.md and every document and README after it with most care, each a
   union of both sides, no decision reworded. Fix what is wrong.
2. Build `-warnaserror`; fix what the merge broke (both sides' features kept, as the merge brief says).
3. The bundle: rehash both manifests (game-v1 through a wrapper script; check how content/locale/manifest.json is made), validate, the
   A2 content gate, `scripts/content-loop.sh --grammar --speech` (render only what is missing). The bundle hash into
   `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v10.json`; its state digest, the end-to-end and procedural goldens and any pinned digest
   re-recorded only where the merged state moved them, each named with the side that moved it.
4. The gate below, then the report.

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i-merge,
at most 30 lines printed; never background them, never poll with sleep or pgrep. Other sessions build on this machine: a wait for a
limited.sh slot is normal. No game window. The VOCATERRA_HEAVY_SLOTS=1 prefix is not needed.

Gate (from the worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(passed, failed, skipped, compared with feature/inventory after I04, 12,102 passed and 3 skipped, and main's count from the biomes
series' last report or handover, else say you did not find it); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes
clean; scripts/limited.sh scripts/content.sh validate passes; the A2 content gate clean.

Report, one page: commits; each resolution you corrected and why; number collisions and how settled; every test, golden, digest and
fixture changed and why; the counts; files outside the game's area touched; anything left undone. Leave the worktree clean on your branch
with no process of yours running. Commit messages: subject, blank line, body, blank line, then the trailers
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
