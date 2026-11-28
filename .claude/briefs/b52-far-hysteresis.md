You are implementing B52, "Hysteresis for far LOD", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game
(the far view's tile selection: src/Vocaterra.Game/Graphics/Far/FarTileSelector.cs, and the near-first gate's reopen in src/Vocaterra.Game/Streaming/NearFirstGate.cs).

Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b52, already on your branch `B52-far-hysteresis`, cut from the
feature branch `feature/lod-lifecycle` after B49, B50 and B51 merged. Do not create another branch or worktree. Commit after each item that
builds and passes its tests, with the prefix `B52: `. Do not push your branch: it stays local and the gate merges it from there. Never push
or merge feature/lod-lifecycle or main. When you finish, leave the worktree on your branch, clean. Another agent works beside you on B53 (test and allocation clean-up) in another worktree; you should not need to touch the same files. If you must edit `FarView.cs`, keep it small and say what.
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
- docs/game/implementation-plan.md section 4.14: the rows B45, B47, B49, B50 and your row B52 (grep `^| B4[579]`, `^| B5[02]`).
- docs/research/2026-11-19-lod-transitions-review.md: Review 2's "Hysteresis" bullet and "Where they agree".
- .claude/briefs/b49-report.md, b50-report.md and b51-report.md in your worktree (the far view's lifecycle, the gate's new rule, the walls
  under cut edges). B49's `FarViewLifecycleTests` is the flight harness to extend.
- The code: `FarTileSelector` (how a footprint splits by `SplitDistance` and the far-detail bands), how `FarView` calls it each frame
  (re-selection, `Reselect`, the look-ahead), `NearFirstGate` (its reopen: it opens when the near view is done, after 20 s, or when the eye
  is 128 blocks from where it closed; B50 added `IsNearViewMeshed`), and the selector's tests.

Rules: a game milestone edits only the game projects, their tests and docs/game/ (no content/game-v1 edit is expected; none of the core).
A regression check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/; do not edit
either implementation-plan.md or either decisions.md, or README.md, docs/game/running.md and docs/game/architecture.md (give the text for
them in your report). Engine code only in src/Vocaterra.Game (D-game-1). No System.Random and no clock in logic that must be
deterministic; the far digests must not change.

Deliverables:
1. Hysteresis in `FarTileSelector`: a tile splits at `SplitDistance` and merges only beyond about 1.15 times it (one constant, documented),
   so an eye that moves back and forth inside the band causes zero splits or merges. The selector keeps state from the previous selection
   for this (the previous leaves); a jump (teleport) or a settings change resets it. The leaves must still partition the ground exactly
   (no overlap, no hole): a test over many eyes and both directions.
2. A test that fails on the old code: an eye moving back and forth across a split distance (and across several levels' bands) counts the
   leaf changes; old code switches on every crossing, new code zero inside the band. Also a flight out and back in `FarViewLifecycleTests`
   still has 0 uncovered frames, and report the number of tile builds before and after (fewer is the point).
3. The near-first gate's reopen with the same idea: once it has opened because the eye moved 128 blocks, it does not close again on a
   small movement back (state the rule you choose and test it); B47's and B50's gate tests still pass.
4. Selection must stay deterministic for a given sequence of eyes (no clock, no random): the scripted runs and the far digests do not
   change; if a scripted-run test's output moves because selection now depends on history, explain why and how you handled it.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh
full (report the count; B50's gate: @@COUNT@@ passed, 0 failed); scripts/test.sh timing; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/. If you change
anything after the full run, rerun at least the classes you touched and say so.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built; failing-first evidence; test counts; measurements; bugs found and
fixed (where, symptom, fix, test); known-not-fixed with reasoning; files touched outside your area; the text for docs/game/architecture.md's
far-view section if it needs it; anything left undone and why. Leave out a recap of this brief.
