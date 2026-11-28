You are implementing B53, "Far view test and allocation clean-up", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game
(far-view tests and small fixes: the far coarse-level test, src/Vocaterra.Game/Graphics/Far/FarScope (or wherever `FarScope` lives), `ColumnGroup.CopyWindowTo`, the far digest places).

Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b53, already on your branch `B53-far-cleanup`, cut from the
feature branch `feature/lod-lifecycle` after B49, B50 and B51 merged. Do not create another branch or worktree. Commit after each item that
builds and passes its tests, with the prefix `B53: `. Do not push your branch: it stays local and the gate merges it from there. Never push
or merge feature/lod-lifecycle or main. When you finish, leave the worktree on your branch, clean. Another agent works beside you on B52 (hysteresis in `FarTileSelector` and the near-first gate) in another worktree; do not touch those two files.
Tests must not read the wall clock or `Stopwatch` (`WallClockRulesTests` fails the suite).
The machine is shared (the owner may be playing a game, and another session builds): if a heavy command is killed for memory, wait and run
it again once `free -g` shows 8 GB or more available; do not report with the full run missing unless it fails twice.

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
- docs/game/implementation-plan.md section 4.14: your row B53 (grep `^| B53`).
- docs/research/2026-11-19-lod-transitions-review.md: Review 1's findings 5, 12 and 13 (lines 31 to 48).
- The code: `FarCoarseLevelsTests` (the "Level 3 ... the same" test), the scope-less sampler it should compare with, `FarScope` and its
  corner table, `ColumnGroup.CopyWindowTo`, and where the far view's digest places are defined (grep `coast` and `sea` near the far
  digest tests, for example `FarSectionDigestTests`).

Rules: a game milestone edits only the game projects, their tests and docs/game/ (no content/game-v1 edit is expected; none of the core).
A regression check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/; do not edit
either implementation-plan.md or either decisions.md, or README.md, docs/game/running.md and docs/game/architecture.md (give the text for
them in your report). Engine code only in src/Vocaterra.Game (D-game-1). No System.Random and no clock in logic that must be
deterministic; the far digests must not change.

Deliverables (small, mechanical; one commit each):
1. Finding 5: `FarCoarseLevelsTests`' "Level 3 ... the same" test compares the code with itself (coarseFrom 4 and 99 take the same path at
   level 3). Make it compare level 3 against the scope-less sampler (the independent path) so it would catch a real difference; show it
   still passes, and that it fails if you deliberately break the level-3 path locally (do not commit the break; say how you checked).
2. Finding 12 (only the corner table): the `FarScope` corner table is about 16 times oversized (about 6 MB on the large-object heap at L2
   and L3). Size it on its own need; the far digests must not change (`FarSectionDigestTests` and the other digest tests pass unedited);
   report the bytes allocated per scope before and after (a test with `GC.GetAllocatedBytesForCurrentThread` is fine; no clock).
3. Finding 13, bounds: add a bounds check (a `Debug.Assert` or an `ArgumentOutOfRangeException` guard, whichever fits the hot path; say
   which and why) in `ColumnGroup.CopyWindowTo`, with a test for the out-of-range case if it throws.
4. Finding 13, digest places: two far digest places are identical (coast far = sea far). Give the coast its own distinct place (a real
   coast: land and sea both in view) so the digest guards something new. This adds or changes one recorded digest: re-record only that
   one, say its old and new value, and show the other digests did not move.
Do not change anything else in the far view; anything else you notice goes in the report.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh
full (report the count; B50's gate: @@COUNT@@ passed, 0 failed); scripts/test.sh timing; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/. If you change
anything after the full run, rerun at least the classes you touched and say so.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built; failing-first evidence; test counts; measurements; bugs found and
fixed (where, symptom, fix, test); known-not-fixed with reasoning; files touched outside your area; the text for docs/game/architecture.md's
far-view section if it needs it; anything left undone and why. Leave out a recap of this brief.
