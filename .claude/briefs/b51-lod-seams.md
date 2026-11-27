You are implementing B51, "LOD seams", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game
(the far tiles' mesh and where far land meets the voxel zone: src/Vocaterra.Game/Graphics/Far/FarTileMesher.cs, the zone edge in FarView and the renderers).

Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b51, already on your branch `B51-lod-seams`, cut from the
feature branch `feature/lod-lifecycle` after B49 merged. Do not create another branch or worktree. Commit after each item that builds and
passes its tests, with the prefix `B51: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge
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
- docs/game/implementation-plan.md section 4.14: the rows B45, B47, B49 and your row (grep `^| B4[579]` and `^| B51`).
- docs/research/2026-11-19-lod-transitions-review.md: Review 2's "Stitch LOD seams" bullet and "Where they agree"; Review 1's "Against the owner's LOD summary".
- .claude/briefs/b49-report.md and .claude/briefs/b47-report.md in your worktree: how the far view's lifecycle, stand-ins, granules,
  windows, the near-first gate and the look-ahead work now. B49 added `FarViewLifecycleTests` (real workers, the game's ring ratio, a flight
  out and back, `Uncovered` asserted every frame): use and extend it rather than writing another harness.
- docs/game/architecture.md: the far view's section only.
- The previous handover's owner symptoms: docs/handovers/done/2026-11-19-2035-lod-lifecycle.md, "The owner's open checks" (a sky line
  where the near land meets the far land; the videos' index is artifacts/tmp/owner-videos-2026-11-19/index.md in the main checkout,
  /home/james/Dev/Vocaterra, if it exists).
- The code: `FarTileMesher` (the interior cut edges of a coarse tile cut for finer terrain; review 2 found a five-block opening near its
  line 730 at the time), granules (`FarGranules`), how the far view hides leaves under the voxel zone (main's `dcc56e8a`, `git show
  dcc56e8a`), and how the GL and effects paths draw the far pass relative to chunks (depth, order).

Rules: a game milestone edits only the game projects, their tests and docs/game/ (no content/game-v1 edit is expected; none of the core).
A regression check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/; do not edit
either implementation-plan.md or either decisions.md, or README.md, docs/game/running.md and docs/game/architecture.md (give the text for
them in your report). Engine code only in src/Vocaterra.Game (D-game-1). No System.Random and no clock in logic that must be
deterministic; the far digests must not change.

Deliverables:
1. The zone-edge seam: no sky visible where the voxel zone meets the far land. The plan's approach: the far leaves overlap a chunk (or a
   margin) under the zone and are drawn behind it, so a missing or lower chunk edge shows far ground, not sky; keep B42's and main's rule
   that no far ground draws over a region the chunks show (no z-fighting or flicker: B42's and `FarViewStandInClipTests` still pass in both
   paths). If overlap is wrong for a reason you find, propose and build the better option and say why.
2. Interior cut edges: no opening along a coarse tile's edges where it is cut for finer neighbours (skirts or joining faces down to the
   neighbour's height, or another closed join); a mesh test that finds the five-block opening on the old code (fails first) and none after,
   over several seeds and levels.
3. A test that no visible sky shows through any seam on a flight (extend B49's `FarViewLifecycleTests` or the mesh tests: for example
   every boundary edge of the far mesh is closed or covered by chunks), both paths where it applies.
4. Pictures before and after (Release, `--far-view` on, a fresh seed, `VOCATERRA_LOD_DIR` a temp folder, `--workers 8 --no-settle --wait
   <s>`, shaders on and off) at a spot with the zone edge and coarse cuts in view; save under artifacts/b-series/b51/ (not committed); the
   added triangles or quads per tile and the far pass time.
5. Far digests unchanged unless the mesh digest must move: if a mesh digest moves, say why and re-record only that.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh
full (report the count; B49's gate: 14,010 passed, 0 failed); scripts/test.sh timing; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/. If you change
anything after the full run, rerun at least the classes you touched and say so.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built; failing-first evidence; test counts; measurements before and after;
bugs found and fixed (where, symptom, fix, test); known-not-fixed with reasoning; files touched outside your area; the text for
docs/game/architecture.md's far-view or streaming section if it needs it; anything left undone and why. Leave out a recap of this brief.
