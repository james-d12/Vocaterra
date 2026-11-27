You are implementing B49, "Never retire a drawn LOD", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game (the
far view in src/Vocaterra.Game/Graphics/Far and src/Vocaterra.Game/Streaming, and its tests).

Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b49, already on your branch `B49-never-retire-lod`, cut from the
feature branch `feature/lod-lifecycle` (which equals main at 5791c955). Do not create another branch or worktree. Commit after each item that
builds and passes its tests, with the prefix `B49: `. Do not push your branch: it stays local and the gate merges it from there. Never push
or merge feature/lod-lifecycle or main. When you finish, leave the worktree on your branch, clean.

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
- docs/game/implementation-plan.md section 4.14: the rows B42, B45, B47 and your row B49 (grep `^| B4[2579]`).
- docs/research/2026-11-19-lod-transitions-review.md: findings 1, 2, 8 and 9 (the two reviews and the orchestrator's check of them).
- docs/handovers/2026-11-19-2035-lod-lifecycle.md: "Outcome so far" (why the gates missed these faults) and "How to judge them".
- .claude/briefs/b47-report.md (in the main checkout, /home/james/Dev/Vocaterra/.claude/briefs/): how stand-ins, granules, windows, the
  near-first gate and the look-ahead work now.
- docs/game/architecture.md: the far view's section only.
- Since the review, main gained two far-view changes you must keep: `git show dcc56e8a` ("Far view: no stand-in ground over the chunks",
  PR #74) and `git show c95c6866 --stat` ("no far pass when it cannot be seen", PR #76). Check the review's line references against the
  code as it is now, not as the review quoted it.
- The code: src/Vocaterra.Game/Graphics/Far/FarView.cs (Invalidate, re-selection and `_wanted`, Trim, the backdrop windows),
  FarTileSelector.cs, src/Vocaterra.Game/Streaming/LodRing.cs (only as far as FarView uses it), and the existing tests FarViewFlightTests,
  FarViewStandInClipTests and NearFirstGateTests.

Rules: a game milestone edits only the game projects, their tests and docs/game/ (no content/game-v1 edit is expected; none of the core).
A regression check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/; do not edit
either implementation-plan.md or either decisions.md, or README.md, docs/game/running.md and docs/game/architecture.md (give the text for
them in your report). Engine code only in src/Vocaterra.Game (D-game-1). No System.Random and no clock in logic that must be
deterministic; the far digests must not change.

The fault (verified by the orchestrator on 2026-11-19): `FarView.Invalidate` disposes any built tile that is not in `_wanted`, and a built
stand-in leaves `_wanted`, so a stand-in that is on screen is thrown away when the ring's mips change its sections: a tile-sized hole in
the far land (the owner's 64-block holes while flying). Findings 2, 8 and 9 are the same family (in-flight ancestors dropped on
re-selection, Trim evicting what still covers a waiting leaf, unfit backdrop windows).

Deliverables:
1. The test first: `FarViewLifecycleTests` with real workers (a JobPool with worker threads, not `JobPool(0)`), `RealRadius` 1,000 against a
   voxel radius of 400 (the game's ratio), and a flight out and back. Assert `Uncovered == 0` on every frame after the first full cover
   (not only after a fill completes). Include a section-change case (the ring's mips change the sections under a drawn stand-in) and show
   it fails on the code as it is before your fix: commit the test first and record the failure (frame counts) in your report. Keep it
   deterministic enough not to flake (bounded frames, waits on job completion rather than wall time); mark it `[Trait(TestSpeed.Trait,
   TestSpeed.Slow)]` if it takes more than a few seconds.
2. The invariant, "a drawn LOD is retired only once what replaces it is drawn": `Invalidate` marks tiles in the draw, fallback or below sets
   dirty instead of disposing them (they keep drawing until the rebuilt tile is ready); re-selection keeps in-flight ancestors of the current
   leaves in `_wanted`; `Trim` never evicts an ancestor of a waiting leaf; backdrop windows that do not fit are skipped, not marked unfit.
   Say how memory stays bounded (dirty tiles kept for drawing must still be freed once replaced).
3. The existing far tests (flight, stand-in clip, gate) still pass in both paths (GL and effects); measure on the handover's flight
   (seed 7900, view distance 576, far detail 200%, a fast flight; use a scripted or headless run with `--stream-stats`): frames with an
   uncovered leaf, far pass time, the 99th-percentile frame time, before and after. If a headless flight is not possible, say what you
   measured instead.
4. If you find a separate cause of holes or sky strips outside B49's scope (scheduling: B50; seams at the zone edge or interior cut edges:
   B51; split/merge thrash: B52), do not fix it: describe it in the report with evidence.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh
full (report the count; main's last gates were about 12,750 passed, 0 failed); scripts/test.sh timing; scripts/limited.sh dotnet format
Vocaterra.sln --verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built; the failing-first evidence; test counts; measurements before and
after; bugs found and fixed (where, symptom, fix, test); known-not-fixed with reasoning (including anything for B50 to B52); files touched
outside the far view; the text for docs/game/architecture.md's far-view section; anything left undone and why. Leave out a recap of this
brief.
