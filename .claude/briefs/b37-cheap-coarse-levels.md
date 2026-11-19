You are implementing B37, "Chunk speed 3: cheap coarse levels (L3 to L7)", for the Vocaterra repository (C#, .NET 10; the game is
Vocaterra.Game.Core, Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b37 (absolute; cd there first), on the local branch
`B37-cheap-coarse-levels` (already created there, cut from `feature/world-next` at a696c1ee). Commit after each step that builds and passes, prefix `B37: `. Do not push;
never push or merge feature/world-next or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b37/, removed at the end.
Resources and waiting: every dotnet restore, build, test and format, every game or WorldPreview run through scripts/limited.sh (builds
`-m:4`, tests `-m:2`); one heavy command at a time, no `&`; long commands with the Bash tool's `run_in_background: true`, then end your
turn and wait for the notification; never `sleep`/`until` polls, `pgrep -f` loops or `nohup ... &`. Two other agents build beside you
(the slots in limited.sh queue you; that is expected). Game runs: a Release build, `scripts/limited.sh timeout 900 <command>`, one batch
script rather than many small runs, few and short windows (the owner may be using the machine). Iterate with `scripts/test.sh fast` plus
the classes you touch (`scripts/test.sh project <test project> --filter ...`); **merge `feature/world-next` into your branch before your
one `scripts/test.sh full`**, and run `scripts/test.sh timing` too (this milestone changes performance). Exit 137 is the memory cap:
lower the parallelism, never work around the cap. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. The
sandbox refuses the words `eval` and `hash` in a command line.
Rules: a game milestone edits only the game projects (src/Vocaterra.Game.Core, src/Vocaterra.Game, src/Vocaterra.Host, tools/Vocaterra.WorldPreview),
their tests and docs/game/, and takes the core (Vocaterra.Nlp*) as it is. A regression check is a C# test. Never edit .github/workflows/,
either implementation-plan.md, either decisions.md, docs/game/biomes-plan.md, README.md, docs/game/running.md or docs/game/architecture.md
(give their text in your report). `World/` stays pure (ArchitectureRulesTests: no clock, no System.Random, no `[ThreadStatic]` or static
caches without an allow-list line, IntMath); determinism unchanged; **never loosen a test's limit or re-record a pin unless this brief
says so**. Do not commit bin/, obj/ or artifacts/.
Gate: build -warnaserror (0 warnings); scripts/test.sh full (the count on feature/world-next at your base, plus your tests); timing tests
pass; format clean; scripts/content.sh validate passes. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDc2yqvjCy1ntkujnVS

Why: the owner's top priority is chunk generation's speed (D-game-34 items 52 and 61). B36 made the far sampler share its work
(`World/Far/FarScope.cs`); now levels 3 to 7 are about 80% of the far view's model work and level 3 alone 47%. Read
.claude/briefs/b36-report.md (all of it, short), docs/research/2026-11-18-chunk-generation-speed.md sections 1 and 3 (#3), the B36 entry
in docs/game/findings.md, and your row: docs/game/implementation-plan.md section 4.14, B37. At levels 4 to 7 samples are 32 to 128 blocks
apart, so a lattice cache gives little. Files: src/Vocaterra.Game.Core/World/Far/LodModelSource.cs (`TreeLevels`, `MergeLevels`),
FarTerrainSampler.cs, FarScope.cs, and `SurfaceRock` (src/Vocaterra.Game.Core/World/Generation/Procedural/SurfaceRock.cs).

**This milestone changes the far picture slightly, so the owner must approve before/after pictures before it merges.** It must never
change the world itself: chunks, the goldens, the save fixture's digest, the atmosphere and tour pins all pass unchanged. The B36 digest
test (`FarSectionDigestTests`) may be re-recorded **for levels 3 to 7 only**, in its own commit, saying why; its levels 0 to 2 cases and the
`Fill`/`FillCells` cases at steps 1 and 2 stay unchanged.

Deliverables:
1. Baseline with the bench (`scripts/limited.sh dotnet run -c Release --project tools/Vocaterra.WorldPreview -- bench`, full mode,
   seeds 42 and 2026): the far stack rows L3 to L7, land and sea. Profile L3 land first (277 ms after B36): B36's lead is the tree raster
   and placement at L3 (`Trees()` below `TreeLevels` 4; `PlantTrees` uses an unshared `FarGround`). Report where the time goes.
2. Cheaper coarse levels, choosing per level what keeps the picture closest for the time saved (say what you chose and why): climate
   from the nearest lattice point instead of four corners; the cliff test from the stack's own child grid instead of ±1-block neighbour
   reads; river pooling from the grid; trees at L3 through the scope or a cheaper raster; `MergeLevels` 3 if it pays. Target 4 to 10 times
   on L3 to L7.
3. If the far store persists model-built sections on disk, make sure old ones are not reused after this change (bump the store's
   version or key, as B-series milestones did with `ProceduralGenerator.Version` for the world; do not bump the world's version if the
   world does not change). Say what you did.
4. Tests: the far picture's invariants still hold at L3 to L7 (no holes, water where the ring's real data has water at a sampled set of
   places, heights within a stated tolerance of the exact sampler, cliffs still marked where the exact sampler marks them for most of a
   sample: state the share). Never loosen an existing test's limit.
5. Pictures for the owner, in /home/james/Dev/Vocaterra/artifacts/b-series/b37/ with an index.md: before and after side by side, Release,
   High, 16 km, midday, a pre-filled far store so the view is complete: seed 42 from a hill toward the mountains, seed 42 over the sea
   toward a coast, seed 2026 toward the peaks and its lake; plus one per seed at dusk. Same camera before and after (scripted runs). Also
   a difference measure per pair (the share of pixels that changed visibly). Look at every picture and say plainly what changed.
6. The bench after, same rows; and if the machine allows, one Release game run with a fresh far store (16 km, seed 42, `--stream-stats`):
   the fill time before and after.
Report, half a page: branch, head, worktree; where L3's time went; what you changed per level; the bench before and after; the tests;
the pictures and what changed in them; anything left. No file dumps. Do not merge or ask to merge: the orchestrator shows the owner the
pictures first.
