You are implementing Y12, "The campsite", for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp, the
Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* projects; the game is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game
(MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone is game logic: placing the camp kit on owned land as one
piece, the camp's sleep places and fire, planting on the plot, and packing the camp back; the core is taken as it is.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y12, which already exists on the branch `Y12-campsite` (cut from
`feature/settlement` after Y08 and Y11 were recorded). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y12 &&`
and use absolute paths for file tools; your shell may start in another checkout, which you must not edit. Commit after each item that
builds and passes its tests, with the prefix `Y12: `. Do not push your branch. Never push or merge `feature/settlement` or `main`. When
you finish, leave the worktree on its branch, clean, with no process of yours running. No other agent builds while you work, unless the
orchestrator tells you otherwise.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly (content-loop refuses to run
under limited.sh). Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait
loop on `pgrep -f`. Exit 137 means the 6 GB cap killed it: lower the parallelism. Set `VOCATERRA_RUNS_WORK` to a folder whose name is
unique to Y12 (in your scratchpad) for every `scripts/scripted-runs.sh` call.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`: for `scripts/content.sh hash`, write a
wrapper script in your scratchpad and run that). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first (lean: name the sections, never whole long files): CLAUDE.md; docs/process/code-guidelines.md; docs/game/architecture.md;
docs/game/saves.md (`systems.sleep`'s places made in play, `systems.land`); docs/game/settlement-plan.md section 5 "Ownership and camp
placement" (validate the whole structure, support, entrances, plot containment and the wake area before consuming equipment or
changing blocks; persist fixture identities and positions, never rediscover a camp from `Scene.Points`; update the dynamic destinations
for the dialogue worker, navigation, sleep, fire cooking and planting), 8.2, the Y01, Y02, Y05a, Y08 to Y12 paragraphs of 8.3 with their
as-built notes; docs/game/decisions.md "D-game-32" (grep for it). The reports in
/home/james/Dev/Vocaterra/.claude/worktrees/settlement/.claude/briefs/: `y01-*` and `y02-*` (sleep places, places made in play),
`y05a-report.md` (a sleep place is "at" only on foot from its cell; the wake cell), `y06m-report.md` ("For Y07 to Y13": every sleep goes
through `SaveGame.Sleep`/`ApplyNight`; new opaque meshes take the `sunTint`), `y08-report.md` (the two-cell bed and bedroll, Luna's fire
as a sleep place, what it kept of the torch hook for you), `y09-report.md` (`IsOwned`, `TryPlace`'s rule: camp blocks must not go
through it; only planks come back today), `y10-report.md`, `y11-report.md` (the camp kit item, the checklist `my-land`, the beacon at
the plot). The code: `World/Generation/SceneBuilder.cs` (`BuildStamp` and its rotation; the `tent` stamp in `game/scene.json`; the
`fire_ring` prop), `Needs/FireRings.cs` (`Rescan`), `Session/SleepPlaces.cs`, `Session/VoxelSession.cs` (the torch hook at a ring, the
fire lit, `FireLit`), `Session/VoxelSession.Planting.cs` (planting within the radius of Luna's ring, `CampFireRing`), `Input/WorldInput.cs`
(the stake's and the berry's use hooks), `Sound/AmbientBeds.cs`.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/locale, content/schema, assets/ and docs/game/
and takes the core as it is. A regression check is a C# test, never a script with text baselines; tests are named Given_When_Then in
folders that mirror the source; do not edit CLAUDE.md, either implementation-plan.md, either decisions.md or docs/game/settlement-plan.md
(the orchestrator does); engine code only in src/Vocaterra.Game; no System.Random and no clock in the evaluation path; nothing under
.github/workflows/. The content gate at the A2 ceiling must stay clean (`scripts/content.sh lint content/game-v1 --strict --level a2
--fail-on-level --host-pools 'guide.*'`). No save backward compatibility (old saves may break; no migration code).

What this milestone is for (D-game-32, 8.2): the camp kit the mayor sold with the plot (Y11) places a tent, a bedroll and a fire ring as
one piece inside the owned plot, with a torch to light the ring. The bedroll and the lit ring become the player's own sleep places; a
death after a night there wakes at the camp. The plot takes planted berries. Gathering a camp block packs the whole camp back into the kit.

Deliverables:

1. **One camp shape** in content (a stamp or a small `camp` definition in `game/scene.json`'s stamps or a new small data file: choose,
   say why): the existing `tent` stamp, a two-cell bedroll inside or beside it (Y08's block), a fire ring a few blocks in front of the
   tent's opening, and the cell the player wakes on. The camp's footprint must fit a 10 × 10 plot with room to walk round it.
2. **The stamp's rotation as a helper:** move the rotation out of `SceneBuilder.BuildStamp` into a helper both the scene's build and the
   placing in play use, so they put blocks down the same way (a test that the scene's existing stamps build the same digest as before).
3. **Placing:** with the kit in hand, the use key on a ground cell (as the stake's hook) places the camp facing the player's direction
   (snapped to the four axes). It checks every cell first: inside the owned plot (`IsOwned` for every column), free (air or undergrowth
   to clear), on level ground (each cell's support), clear of the player, a free cell to wake on reachable on foot (Y05a's walk). On any
   failure it changes nothing and puts the reason on the hint line (English, `ui.camp*` keys: "Put your camp on your own land.", "The
   ground is not level enough here.", "Something is in the way.", "Stand clear of the camp.", and so on). On success it sets the blocks
   directly (not through `TryPlace`), takes the kit, rescans the fire rings, makes the bedroll and the ring sleep places made in play
   (saved in `systems.sleep` with their positions and the camp's identity, per section 5), and raises a world event `camp_placed`. Camp
   blocks are not gatherable one by one (see 7).
4. **The fire:** the torch hook in `VoxelSession.cs` (the torch on a ring's rim lights it) works at the placed ring; the ring's sleep place
   is usable when lit, as every fire. Fire cooking and the fire's ambient bed (`AmbientBeds`) follow the placed ring as they do Luna's
   (say what you did for each; a missing sound is a known gap Y13 records if you cannot add it cheaply).
5. **Planting:** `VoxelSession.Planting.cs` allows planting inside the owned plot (with the same `maxBushes` rule or a separate count:
   choose, say why), as well as within the radius of Luna's ring.
6. **`my-land`'s steps** `camp` (`camp_placed`, restored from the save's camp) and `fire` (the placed ring lit, restored from the world),
   with their HUD texts; the checklist completes when all four steps are done and never holds sleep back.
7. **Packing back** (the first thing to cut if the milestone runs large; say so if you cut it): gathering any camp block on owned land
   removes the whole camp (every block it placed, the bushes left alone), puts the kit back in the bag (refuse with a hint if the bag
   is full, changing nothing), and drops the camp's sleep places; the last place slept falls back to the inn's bed if it was the camp's
   and lodging is given, otherwise to Luna's fire. The kit can be placed again.
8. **A load** drops a camp sleep place whose blocks are gone (a test that edits the world before the load) and gives the kit back once;
   a camp whose blocks stand is restored with its places.
9. **Tests:** placing on owned land sets every block, takes the kit, adds the two sleep places and `camp_placed`; each refusal reason
   changes nothing; a torch on the placed ring lights it and the ring sleeps the player to morning; the bedroll sleeps the player; a death
   after a night at the camp wakes at the camp; planting inside the plot works and outside both radii does not; packing back returns the
   kit and removes every block and both places; the save round-trip of a placed camp; a load with a missing camp gives the kit back once;
   the scene's stamps build the same digest through the new helper.
10. **Runs in the game, from a new world** (through limited.sh, `--seed 42 --mute --needs off --dev --skip-to-session 2`): with
    `--own-plot` and the kit given (`--give` with its noun, or the aid's form), place the camp (a use step, or a dev flag
    `--place-camp x,z,facing` if the scripted steps cannot aim the use: add it with help text and docs), place a torch on the ring,
    `--sleep` at the bedroll, `--save`; report the console, the events, `systems.sleep` and `my-land`. A refusal outside the plot. A
    pack back. Screenshots of the camp from a few blocks away, by day and by night with the fire lit, into `artifacts/y12/`
    (git-ignored).
11. **Unchanged behaviour:** capture `r03-npcs`, `r21-skip`, `r26-death` and any run that plants or lights a fire before your first
    change and after your last; report each difference and why.
12. **Documents:** docs/game/saves.md (the camp in `systems.sleep`), docs/game/architecture.md (the camp, a few lines), docs/game/running.md
    (any new flag), content/README.md (the camp shape).

Not in this milestone: more than one camp or plot; building with planks beyond what Y09 allows; meals, comfort or warmth at the camp;
selling land back.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count, against `feature/settlement`'s after Y08 and Y11, in their reports); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes (after `hash` through your wrapper, and the new bundle
hash into the save fixture as CLAUDE.md says; `scripts/content-loop.sh` does both); the A2 content gate clean; the full suite once
without models (`VOCATERRA_MODELS_DIR` an empty folder) passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built (the shape, the helper, the placing and its checks, the fire,
planting, the steps, packing back, the load); the refusal lines; test counts and the tests added or changed; the runs in the game; the
scripted runs' comparison; what Y13 should record as known gaps (the fire's sound at a placed camp if missing, «mi campamento» not
answered when asked for directions, one plot only, no meals at the inn); known-not-fixed with reasoning; anything left undone or cut and
why. Leave out how you searched.
