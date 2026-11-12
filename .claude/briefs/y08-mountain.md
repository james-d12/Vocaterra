You are implementing Y08, "The mountain, the strings and the rebaseline", for the Vocaterra repository (C#, .NET 10). The language core is
the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone edits the scene and the
game's strings, removes the mountain camp's code paths and captures the scripted runs again; the core is taken as it is.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y08, which already exists on the branch `Y08-mountain` (cut from
`feature/settlement` after Y07, the opening's logic, and Y09, land rules, were recorded). Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/y08 &&` and use absolute paths for file tools; your shell may start in another checkout,
which you must not edit. Commit after each item that builds and passes its tests, with the prefix `Y08: `. Do not push your branch. Never
push or merge `feature/settlement` or `main`. When you finish, leave the worktree on your branch, clean, with no process of yours running.

Another agent builds Y10 (the stake and the plot's outline) at the same time in `.claude/worktrees/y10`: it adds the item `stake`, a
use-item hook in `Input/WorldInput.cs`, `OverlayMeshes.PlotOutline`, a HUD line and a launch flag, and edits `VoxelSession.Land.cs`.
Stay out of those. Whichever is merged second merges the first and hashes the bundle again; the orchestrator handles that.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
the prerender and every screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build
Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`). scripts/test.sh, scripts/content-loop.sh and scripts/scripted-runs.sh call it
themselves where they need it: run them directly (content-loop refuses to run under limited.sh). Never start two heavy commands at once
(no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. The other agent's heavy commands share
the slots, so a command may wait: let it wait. Exit 137 means the 6 GB cap killed it: lower the parallelism. Set `VOCATERRA_RUNS_WORK`
to a folder of your own (in your scratchpad) for every `scripts/scripted-runs.sh` call, the same folder before and after.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`: for `scripts/content.sh hash`, write a
wrapper script in your scratchpad and run that). Model-gated tests and the speech need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first (lean: name the sections, never whole long files): CLAUDE.md; docs/process/code-guidelines.md; content/README.md, "The edit
loop" and the scene's section; docs/game/settlement-plan.md section 8.2, the Y04, Y07, Y08 and Y12 paragraphs of 8.3 (Y12 reuses the
`tent` stamp and the torch hook), section 9.1; docs/game/decisions.md "D-game-32" (grep for it); docs/game/running.md on the scripted
runs and `scripts/scripted-runs.sh` with its compare tool. The reports in /home/james/Dev/Vocaterra/.claude/worktrees/settlement/.claude/briefs/:
`y04-report.md` (the inn's counter), `y06m-report.md` ("For Y07 to Y13": the sleep through `ApplyNight`, the `sunTint`, the digest pins
in `AtmosphereSimulationTests`), `y07-report.md` (what the opening now does, what it left for you), `y09-report.md` (the land rules).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/locale, content/schema, assets/ and docs/game/
and takes the core as it is. A regression check is a C# test, never a script with text baselines (the scripted runs are captures you
compare and report, not tests); tests are named Given_When_Then in folders that mirror the source; do not edit CLAUDE.md, either
implementation-plan.md, either decisions.md or docs/game/settlement-plan.md (the orchestrator does); engine code only in
src/Vocaterra.Game; no System.Random and no clock in the evaluation path; nothing under .github/workflows/. The content gate at the A2
ceiling must stay clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`). No save
backward compatibility (the game is unreleased): old saves may break; write no migration code.

What this milestone is for (D-game-32, 8.2): the mountain ledge stays as the place the player arrives (`spawn` at `camp.player`), but its
tent, chest, crate and fire ring go: the player's first night is at the inn (Y07) and their own camp comes later on bought land (Y12).
After the opening's logic stopped using the mountain camp (Y07), this milestone removes it from the world, the code and the strings, and
captures the scripted runs again once.

Deliverables:

1. **The scene** (`game/scene.json`): the `tent` stamp's placement at `camp.ledge`, `chest.player`, the crate beside it and the fire ring
   `firering.player` go. The `tent` stamp's definition stays (Y12 places it). `camp.player` keeps its site, its blend and the spawn, and
   loses the label «mi campamento». Grep everything that names `chest.player` (the death's chest, a tip, an objective): if anything needs a
   player's chest, say what and propose where it goes (the inn's guest room is the natural place; do it only if the death's chest or
   another system breaks without it, and say so). The inn's counter: it is a row of `crate` a player can step onto (Y04); stop that if it
   can be done without a crafting panel opening (a block that is solid and reads as a counter but is not `table`; or a slab-height
   block; your choice, say why). The scene's digests and the world digests in tests are pinned again once, with the reason.
2. **The sleep places and the death** (`game/needs.json`): the mountain's sleep place `camp.fire` goes; Luna's fire (the scene's
   `firering` at `camp.guide`) becomes a sleep place (kind `fire`, access `always`, usable when lit, as every fire), so a player without
   lodging can still sleep there (8.2: "Luna's fire stays usable"); `death.wakeAt` names it. Check that the camp task's torch at Luna's
   ring lights it, and that a death before any sleep wakes beside Luna's fire.
3. **The code** that named the mountain camp: `firering.player` in `Event/Trigger.cs`, `Event/WorldEvent.cs`, `Needs/NeedsData.cs`,
   `Session/SleepPlaces.cs`, `Session/VoxelSession.cs` and `Sound/AmbientBeds.cs` (the fire's ambient bed now plays at any lit fire ring
   the scene or play has, or at Luna's: choose and say), and the tests that name it (`OpeningAndEndingTests`, `TriggerTests`,
   `GameWordsTests`, `NeedsDataTests`, `ObjectiveTests`, `SceneTests`, `VillageTests`, `OpeningFrontendTests`, `BeaconArrowTests`,
   `TestingRowsTests` and `AmbientBedsTests`: grep again, the list may have moved after Y07). Keep the torch hook in `VoxelSession.cs`
   general enough for Y12 to point it at a placed ring (Y12 widens it; do not remove what it will need: say what you kept).
4. **The strings:** the console's sleep line says "at the fire" wherever the player slept: it names the place (the sleep place's kind or
   a name per place: «at the inn», «at the fire»; English console text). The locale's `tips.json`, `ui.json` and `controls.json` lines
   that speak of the player's own camp, tent or fire up the trail; `words.json`'s `hud.camp`; any HUD text such as "Session complete:
   your fire is lit" if Y07 left it (read y07-report.md). Spanish strings only if a Spanish line names the mountain camp (grep
   `dialogue.json` for `campamento` and check each use; Luna's own camp stays «el campamento»).
5. **The content loop:** `scripts/content-loop.sh --grammar --speech` after each content edit; `--record` only if the end-to-end goldens
   move (say which and why); `--tier full` once before the report. The new bundle hash goes into the save fixture as CLAUDE.md says (the
   loop does it).
6. **The scripted runs, captured again:** first capture all of them on your branch's base, then make the changes, then capture again and
   compare. `r20-sleep` and `r30-no-classes` place a torch on the mountain ring and sleep there: rewrite their steps to the new day one
   (the innkeeper's room, then the sleep in the bed; or Luna's fire for one of them, so both kinds of sleep stay covered) so that both
   reach their sleep again (today they stop before it). Read the differences of r01, r10, r20 to r24, r26, r27 and r30 (and any other run
   that differs) and say for each whether it is intended; then re-baseline the captures the repository keeps (find how scripted-runs.sh
   records a baseline, and follow it). Pin no hour or weather unless a run's picture needs it (say so).
7. **Tests:** a death before any sleep wakes beside Luna's fire; a sleep at Luna's lit fire works with no lodging; no sleep place or
   point named `firering.player` or `camp.fire` remains and the validator refuses a `wakeAt` that names no sleep place; the console's
   sleep line names the inn for a night in the bed; the counter cannot be stood on (if you changed it). Change the existing tests above.
8. **Runs in the game, from a new world** (through limited.sh, `--seed 42 --mute --needs off`): a screenshot of the mountain ledge at
   spawn (no tent, chest or ring), of Luna's lit fire, of the inn's counter; a death before any sleep; a sleep at Luna's fire without
   lodging. Into `artifacts/y08/` (git-ignored).
9. **Documents:** docs/game/lessons.md, running.md and playtest.md (the mountain camp gone, the runs changed), content/README.md where it
   names the mountain's points, docs/game/architecture.md if it names `firering.player`.

Not in this milestone: the stake, the plot, the mayor, the camp kit, the campsite (Y10 to Y12); the opening's objectives and the ending
(Y07 did them); a new NPC or lesson.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count, against `feature/settlement`'s after Y07 and Y09, in their reports); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; the A2 content gate clean; the full suite once without
models (`VOCATERRA_MODELS_DIR` an empty folder) passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was removed and changed (the scene, the sleep places, the code, the strings,
the counter, `chest.player`); any new Spanish with its gloss; the digests pinned again; test counts and the tests added or changed; the
runs in the game; each scripted run's difference and verdict, and what was re-baselined; anything Y12 or Y13 should know; known-not-fixed
with reasoning; anything left undone and why. Leave out how you searched.
