You are implementing B50, "Far scheduling fixes", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game
(the far view's job scheduling: src/Vocaterra.Game/Streaming/LodRing.cs, the job pool, the drainage warm-up, the near-first gate).

Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b50, already on your branch `B50-far-scheduling`, cut from the
feature branch `feature/lod-lifecycle` after B49 merged. Do not create another branch or worktree. Commit after each item that builds and
passes its tests, with the prefix `B50: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge
feature/lod-lifecycle or main. When you finish, leave the worktree on your branch, clean. Another agent works beside you on a different
milestone in another worktree; both of you touch `FarView.cs` at most lightly, so keep your changes there small and say which you made.
Tests must not read the wall clock or `Stopwatch` (`WallClockRulesTests` fails the suite; B49 hit it).

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, and every screenshot or scripted
run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`).
scripts/test.sh calls it itself: run it directly. Never start two heavy commands at once (no `&`, no background builds, no parallel test
runs). Waiting: run a long command with the Bash tool's `run_in_background: true`, end your turn and wait for its notification; never poll
with `sleep`, `until grep ...; do sleep` or `pgrep -f` loops, and never start a run with `nohup ... &`. While iterating run
`scripts/test.sh fast` plus the test classes you touch (`scripts/test.sh project tests/Vocaterra.Game.Tests --filter ...`); merge
feature/lod-lifecycle into your branch before your one full run (`git merge feature/lod-lifecycle`; it is a local branch in the same
repository), then run `scripts/test.sh full` once and report only after it has finished; run `scripts/test.sh timing` as well, since this
milestone touches per-frame work. Exit 137 means the cap killed it: lower the parallelism, do not work around the cap.
Game runs (pictures, scripted runs, measurements): a Release build; `scripts/limited.sh timeout 900 <command>`; scripted runs need
`--workers 8 --no-settle --wait <s>` for live behaviour (the default 2 workers settle the whole 16 km view first, which takes minutes); a
fresh seed and `VOCATERRA_LOD_DIR=<a temp folder>` for any measurement (the far store under ~/.local/share/vocaterra/lod/ is shared);
one batch script for all runs rather than many small ones.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`).

Read first (sections, never whole long files):
- CLAUDE.md.
- docs/game/implementation-plan.md section 4.14: the rows B45, B47, B49 and your row (grep `^| B4[579]` and `^| B50`).
- docs/research/2026-11-19-lod-transitions-review.md: findings 3, 4, 6, 10 and 11 (Review 1), and "Why the gates passed".
- .claude/briefs/b49-report.md and .claude/briefs/b47-report.md in your worktree: how the far view's lifecycle, stand-ins, granules,
  windows, the near-first gate and the look-ahead work now. B49 added `FarViewLifecycleTests` (real workers, the game's ring ratio, a flight
  out and back, `Uncovered` asserted every frame): use and extend it rather than writing another harness.
- docs/game/architecture.md: the far view's section only.
- The code: `FarView.WarmDrainage` and its comment; `JobPool` (`TakeLocked`, lanes, `FineBias`) and `JobPoolTests`; `LodRing` (`Awaits`,
  `_eye`, `_plannedFor`, footprint failure handling); the near-first gate (`NearFirstGate`, `IsMeshedAround` in the voxel renderer or
  streamer); `DrainageNetwork` (B40's per-start-node lock, `_reading`). Check each finding's line references against the code as it is now.

Rules: a game milestone edits only the game projects, their tests and docs/game/ (no content/game-v1 edit is expected; none of the core).
A regression check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/; do not edit
either implementation-plan.md or either decisions.md, or README.md, docs/game/running.md and docs/game/architecture.md (give the text for
them in your report). Engine code only in src/Vocaterra.Game (D-game-1). No System.Random and no clock in logic that must be
deterministic; the far digests must not change.

Deliverables (each with a test that fails on the code before your change, recorded in the report):
1. Finding 3: the drainage warm-up runs behind every coarse tile, stand-in, ring and chunk job, as its comment says (`FineBias` or its own
   lowest lane); a `JobPoolTests` case pins the order.
2. Finding 4: `LodRing` re-plans a failed footprint (bounded retries or backoff, no busy loop) instead of waiting for a 32-block move, so
   fine tiles over it do not wait 240 s.
3. Finding 11: `LodRing.Awaits` measures from `_plannedFor`, the same origin as the plan.
4. Finding 10: `IsMeshedAround` (or the gate) counts the near view as done when the column under the eye has no chunk in the view set
   (spawning or flying high), so the gate does not hold far work for 20 s.
5. Finding 6: no duplicate continent reads (an in-flight landmass mark under the publish lock, or warm rows in sequence) and `_reading`
   bounded; a test counting reads with several workers.
6. Far digests unchanged (`FarSectionDigestTests` and any other digest test pass unedited); finding 7 (shared sea nodes and publish order)
   is out of scope: do not change it, but note if your change makes it more or less likely.
7. Measure with B49's flight test (uncovered frames, frames until first cover) before and after; a headless `--stream-stats` start on a
   fresh seed with `VOCATERRA_LOD_DIR` set to a temp folder, `--workers 8 --no-settle`, if it runs within 15 minutes: the `pipeline:`
   counters and time to far covered, before and after.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh
full (report the count; B49's gate: 14,010 passed, 0 failed); scripts/test.sh timing; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/. If you change
anything after the full run, rerun at least the classes you touched and say so.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built; failing-first evidence; test counts; measurements before and after;
bugs found and fixed (where, symptom, fix, test); known-not-fixed with reasoning; files touched outside your area; the text for
docs/game/architecture.md's far-view or streaming section if it needs it; anything left undone and why. Leave out a recap of this brief.
