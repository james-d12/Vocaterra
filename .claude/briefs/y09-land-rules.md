You are implementing Y09, "Land rules and protection", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* projects; the game is Vocaterra.Game.Core (the engine-free logic),
Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone is game logic in Vocaterra.Game.Core with one
new content file; the core is taken as it is.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y09, which already exists on the branch `Y09-land-rules` (cut from
`feature/settlement` after Y06m, the merge of `main` with the weather series). Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/y09 &&` and use absolute paths for file tools; your shell may start in another checkout,
which you must not edit. Commit after each item that builds and passes its tests, with the prefix `Y09: `. Do not push your branch. Never
push or merge `feature/settlement` or `main`. When you finish, leave the worktree on your branch, clean, with no process of yours running.

Another agent builds Y07 (the opening's logic) at the same time in its own worktree. It edits `VoxelSession.cs`, the objectives,
`session.json`, the ending and the skip; keep your edits of `VoxelSession.cs` small and in their own region (a partial class file
`VoxelSession.Land.cs` is preferred), and do not touch the objectives, `session.json` or the ending. Whichever of you is merged second
merges the first and hashes the bundle again; the orchestrator handles that.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly (content-loop refuses to run
under limited.sh). Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait
loop on `pgrep -f`. The other agent's heavy commands share the slots, so a command may wait: let it wait. Exit 137 means the 6 GB cap
killed it: lower the parallelism. Set `VOCATERRA_RUNS_WORK` to a folder of your own (in your scratchpad) for every
`scripts/scripted-runs.sh` call, the same folder before and after.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`: for `scripts/content.sh hash`, write a
wrapper script in your scratchpad and run that). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first (lean: name the sections, never whole long files): CLAUDE.md; docs/process/code-guidelines.md; docs/game/architecture.md (Core
and Game, the content load); content/README.md (how a game data file is added: schema, `GameData`, validator, manifest, README row);
docs/game/settlement-plan.md section 5 "Ownership and camp placement", section 8.2, the Y09, Y10, Y11 and Y12 paragraphs of 8.3 (the
later ones say what is not yours and what they will need from you), sections 9.2 (the meadows and the plot), 9.3 (the price) and 9.6;
docs/game/decisions.md "D-game-32" point (c) (grep for it). The code: `Content/GameData.cs`, `Content/GameDataValidator.cs`, an existing
small data file and its schema as the model (`game/needs.json` and its `NeedsData`/`NeedsValidator`, or `game/day.json` from main),
`Simulation/VoxelSimulation.cs` (`TryPlace`, the gather path, `WalkPermille` as the model for a rules hook), `Scene/SceneBuilder.cs`
and the scene's sites, paths and bridges (`game/scene.json`, read only), `Save/SaveDocument.cs` and `Save/SaveGame.cs` (`SystemsSave`,
`systems.sleep` as the model), the hint line the player sees on a refused action.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/locale, content/schema, assets/ and docs/game/
and takes the core as it is. A regression check is a C# test, never a script with text baselines; tests are named Given_When_Then in
folders that mirror the source; do not edit CLAUDE.md, either implementation-plan.md, either decisions.md or docs/game/settlement-plan.md
(the orchestrator does); engine code only in src/Vocaterra.Game (this milestone should need none beyond the hint's text if that lives in
the game); no System.Random and no clock in the evaluation path; nothing under .github/workflows/. **No `scene.json` edit** (Y08 and Y04
are the scene's milestones). The content gate at the A2 ceiling must stay clean (`scripts/content.sh lint content/game-v1 --strict
--level a2 --fail-on-level --host-pools 'guide.*'`). No save backward compatibility (the game is unreleased): the save stays version 8;
a new optional `systems.land` entry needs no version bump and no migration code.

What this milestone is for (D-game-32 (c)): placing stays allowed in the wild, so a plank dam in the river still works; it is refused
inside protected regions (the village, the roads, Luna's camp). Only owned land takes the camp kit (Y12), and only there can the player
take back planks they placed. The player will mark a plot with a stake (Y10) and buy it from the mayor (Y11); this milestone gives them
the rules and the state they need, with no item, no outline and no purchase.

Since Y06m (the merge of `main`'s weather series; read `y06m-report.md`, "For Y07 to Y13", in the same folder as this brief):
`SystemsSave` is `(Procedures, Sleep, Atmosphere)`; add `Land` after them, optional, no version bump. `VoxelSimulation` now carries
main's atmosphere; `AtmosphereSimulationTests` pins two world digests along a route, which move only if your change alters that
route's world (it should not: say so if it does). Any new opaque mesh takes `ChunkMesher`'s `sunTint` (you should need none). The test
count to compare with is 11,221 passed, 3 skipped.

Deliverables:

1. **`game/land.json`** with its schema `content/schema/land.schema.json`, its load in `Content/GameData.cs` (one `LandData` record), a
   `CheckLand` in the validator with clear messages, its manifest and provenance rows, and a row in content/README.md. Its fields
   (9.6): the protected regions by the scene's site names with a margin (the village's disc: its radius plus blend plus 2 = 34 as 9.2
   measured, preferably expressed as the village site with margin so it follows the scene; Luna's camp 12 + 2 = 14), the path and
   bridge blocks with a margin of 2, the eligible regions (the sites `meadow.north` and `meadow.south`, radius 22), the plot's width and
   depth (10, 10), its price (10 coins; Y11 reads it), the slope it allows (1). The validator refuses a site name the scene lacks, a
   non-positive size, a price below 0.
2. **`LandRules`** (pure, in Core, `Land/` folder): a mask per column built once when the session starts from the scene and
   `land.json` (protected, eligible); `PlotCheck` for an origin and the plot's size answers yes or gives the reason: protected, outside
   the meadows, too steep, water, a tree (trunk or leaves above any column, as 9.2's probe), not ground (grass or dirt on top). Reasons
   are an enum the hint line and Y10 can name. Re-check 9.2's numbers with it: on seeds 42, 7, 1234 and 2025, at least three
   non-overlapping valid 10 × 10 plots in each meadow (a test on seed 42 at least; report the counts on all four).
3. **The rules hook on `VoxelSimulation`** (as `WalkPermille` is): `TryPlace` asks it, so placing planks (and any block other than the
   torch, see below) on a protected column is refused with a hint (`ui.landProtected` or near: "You cannot build in the village." /
   "…on the road." as the reason fits; English UI text in `locale/en/ui.json`, Spanish only if the hint line shows Spanish today). A
   torch is always allowed, so the exercise at Luna's ring and the camp task are untouched. List every block a player can place today
   (grep the place path and the items that place a block) and say for each whether it is now refused on protected columns; check the
   water series' uses (a plank dam, a bridge, buckets of water: grep the water plan's H milestones on `main`) still work in the wild.
   Direct simulation commands (the scripted-runs and dev aids that place blocks) go through the same check unless they are the scene's
   own build.
4. **Taking planks back:** gathering a plank block the player placed is allowed only inside the owned plot; elsewhere it is refused
   with a hint. Find how gathering decides today what a player may break (scene blocks versus placed ones) and keep everything else
   as it is: trees, berries, stone and the rest of ordinary gathering are untouched everywhere. Until Y11 nobody owns a plot, so a
   placed plank stays where it is: say in the report how that reads in play and whether the hint says why.
5. **`systems.land`** on `SystemsSave`: the pending plot (origin, size; Y10 marks it) and the owned one (origin, size; Y11 sets it),
   written only when one exists, read back on load, a test that saves and loads both. `VoxelSession` (in `VoxelSession.Land.cs`) holds
   them with methods Y10 and Y11 will call: `SetPendingPlot(origin) -> PlotCheck result`, `OwnPlot()` (from the pending one; no coins,
   no item: Y11 adds the exchange), and `IsOwned(column)`. A dev aid `--own-plot x,z` (with `--dev`) that checks and owns a plot at
   start, for the scripted runs and the later milestones' tests; help text and docs/game/running.md.
6. **Tests:** the mask (a village column protected, a road column protected within 2, a meadow column eligible, a column far in the wild
   neither); `PlotCheck` for each reason on seed 42 with real coordinates; placing planks in the village refused with the hint, in the
   wild allowed, a torch at Luna's ring allowed; taking back a placed plank refused outside the plot and allowed inside an owned one;
   ordinary gathering (a tree trunk, a berry) still allowed in the village where it is today; the save round-trip of `systems.land`;
   the validator's refusals; the scene's existing build (the scene places blocks in protected regions) unaffected.
7. **Runs in the game, from a new world** (through limited.sh, `--seed 42 --mute --needs off --dev`): place a plank in the village
   (refused, report the hint), in the wild (placed), take it back (refused, report the hint); with `--own-plot` in the north meadow,
   place and take back a plank inside the plot (both allowed); `--save` and report `systems.land`. Screenshots of the hints into
   `artifacts/y09/` (git-ignored).
8. **Unchanged behaviour:** capture `r03-npcs`, `r21-skip`, `r26-death` and any scripted run that places blocks (grep the runs for a
   place step) before your first change and after your last and compare them; report any difference and why.
9. **Documents:** content/README.md (the row for `land.json`), docs/game/architecture.md (LandRules and the rules hook, a few lines),
   docs/game/saves.md (`systems.land`), docs/game/running.md (`--own-plot`), the Core README if it lists the folders.

Not in this milestone: the stake item, the use-item hook, the outline and the HUD line (Y10); the mayor, `sell_plot`, coins, the camp kit,
the checklist `my-land` (Y11); the campsite and its placing (Y12); any `scene.json` edit; any Spanish line spoken by an NPC.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count; 11,221 passed and 3 skipped after Y06m);
scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes (after
`hash` through your wrapper, and the new bundle hash into the save fixture as CLAUDE.md says; `scripts/content-loop.sh` does both); the
A2 content gate clean; the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder) passes. Do not commit bin/, obj/ or
artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built (the data, the rules, the hook, the save entry, the aid); the
placeable blocks and what is refused where; the plot counts on four seeds; test counts and the tests added or changed; the runs in the
game; the scripted runs' comparison; what Y10, Y11 and Y12 should call and know; known-not-fixed with reasoning; anything left undone and
why. Leave out how you searched.
