You are implementing B07f, "The far store's edit layer", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core
(the engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours. This milestone's
work is in the far view's store and its feeding: src/Vocaterra.Game.Core/World/Lod (`LodStore`, `LodWorld`, `FileLodPersistence`, the ring
and edits), src/Vocaterra.Game/Streaming (`StreamedWorldHost`, `LodEdits`, `LodRing`), Graphics/SceneDrawer.cs (the LOD folder's key),
Graphics/Far/FarView.cs (two small items), and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b07f, already on the local branch `B07f-far-store-edits` (cut from
`feature/biomes` at 30f104cb). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B07f: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every game run and screenshot through scripts/limited.sh (builds `-m:4`, tests
`-m:2`; the caps are the script's defaults: 10 GB a command, 16 GB in all); one heavy command at a time, no `&`, no wait loops on
`pgrep -f`. Three other agents build at the same time: expect waits. Iterate with a project or `scripts/test.sh fast`; `scripts/test.sh
full` once at the end. Keep scratch on disk under /home/james/Dev/Vocaterra/artifacts/tmp/b07f/ (not /tmp), removed at the end.
Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Game windows are allowed unless the orchestrator says the owner is playing.

Why: the owner asked for an Opus review of the codebase and then said "go ahead with all the fixes recommended from review" (D-game-34
item 37). Read the review: docs/research/2026-11-16-codebase-review.md, section 1 (C1 to C3 major; C6, C7, C10 minor) and section 3 (T1).
Its findings, with file and line, in short:
- **C1.** `LodWorld.IngestChunk` relights from a stale snapshot and writes the rebuilt sections back as Real with mask 0xFF
  (`LodStore.cs:406, 432-452`); `LodRing.Ingest` and `LodEdits` both run on the Ring lane, so with two workers an edit's relight can write
  model blocks over the ring's real sections (persisted), and the ring then treats the stack as finished for good.
- **C2.** Edits loaded from a save never reach the far view (`SaveGame.Restore` in `Program.cs:97` runs before the host exists,
  `SceneDrawer.cs:140`; `LodEdits` only hears later installs and changes; changed chunks are never evicted); and the LOD folder is keyed by
  seed, generator version and data hash (`SceneDrawer.cs:111`), not by save, so another save or a new game on the same seed shows the other
  timeline's buildings for good (`KeepEdits`, `EditedMask`, `LodStore.cs:355-381`).
- **C3.** The model-byte count races with `Trim` (`LodStore.cs:178-189, 207-229`: a slot trimmed between `Entry()` and `Keep()` is
  subtracted twice) and a corrupt slot dropped in `Get()` (:115-118) is not subtracted.
- **C6.** `FarView.DropUnwantedPending` discards a rebuilt mesh of a tile that keeps its old buffer without putting it back in `_dirty`
  (`FarView.cs:527`). **C7.** A job running with old settings can be accepted after `FarView.Reconfigure` clears the lane (`FarView.cs:247`).
- **C10.** `StreamedWorldHost.Dispose` waits on `FileLodPersistence.Flush` on the game thread and then joins for 30 s while closing files
  (`FileLodPersistence.cs:173-179`, `StreamedWorldHost.cs:278-285`): exit can hang.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; the review above; .claude/briefs/b07c-report.md
and b07c-review.md (the store's design and its earlier fixes); docs/game/saves.md if it exists. Another agent (B07e) is changing
Graphics/Far now (FarView, FarTileMesher, FarTileSelector, FarTerrainRenderer) and VoxelRenderer: keep your FarView changes to C6 and C7
only, small, and say so; the next merge will join them.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Engine code only in src/Vocaterra.Game; Core stays pure where it is pure (World/Lod is outside the pure folders
because it is mutable; keep generation untouched: the 18 procedural goldens, `SceneTests.GoldenDigest` and the fixture's state digest must
not move). No save backward compatibility is needed (the game is unreleased), but say if a save's format changes.

Deliverables:
1. **C1:** no relight can overwrite a real section with older blocks: serialise all work on one footprint (a per-stack lock or one owner per
   footprint for both kinds of job), or make the light pass re-read each section under its stripe lock and change only the light bytes. A
   test with the ring's ingest and an edit's ingest of the same stack on two threads, many rounds: the real blocks and the edit both survive,
   in memory and on disk.
2. **C2:** at host creation every chunk the loaded save changed (`grid.ChangedChunks()`) is queued into the edit ingest, so a save's
   buildings appear in the far view at start; the edit overlays are tied to the save (generator sections may stay per world; edit
   overlays per save slot or a folder per save), and on a load the store drops edited sections the save does not hold and ingests the
   generator's stacks again. Tests: a save with an edit loaded into a fresh host shows the edit in the far store; a second save on the same
   seed without the edit does not show it; quitting without saving leaves no edit in the far view of the next load.
3. **C3:** the model-byte count stays exact under concurrent `Put`/`Update`/`Trim` and after a corrupt slot is dropped (a test).
4. **C6, C7:** a dropped pending mesh keeps its tile dirty; meshes carry a settings version and stale ones are refused (tests).
5. **C10:** shutdown flushes within a bound off the game thread, the writer stops cleanly, files close after it; exit never hangs (a test
   with jobs and writes in flight).
6. In the game (Release): build a house far out, save, quit, reload: the far view shows it from 1 to 2 km at once; load a different save on
   the same seed: it does not. Pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b07f/ with an index.md; look at each.
7. Documents: the Game.Core and Game READMEs (the store and saves), docs/game/running.md if the LOD folder's place changes.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,370 passed before, plus your new tests); format clean; content
validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; each finding fixed and how; the tests; the game check and pictures; anything left. No
file dumps or long logs.
