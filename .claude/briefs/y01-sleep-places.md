You are implementing Y01, "Sleep places", for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp,
the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the engine-free
logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y01, which already exists on the branch `Y01-sleep-places` (cut from
`feature/settlement`). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y01 &&` and use absolute paths for file
tools; your shell may start in another checkout, which you must not edit. Commit after each item that builds and passes its tests, with
the prefix `Y01: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge `feature/settlement`
or `main`. When you finish, leave the worktree on your branch, clean.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly. Never start two heavy
commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Another session's
agents build on this machine too, so a heavy command may wait for a free slot: that is normal, let it wait. While iterating run
`scripts/test.sh fast` or `scripts/test.sh project <test project>`; run `scripts/test.sh full` once before reporting. Exit 137 means
the 6 GB cap killed it: lower the parallelism, do not work round the cap.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
use a wrapper script outside the repository). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/process/code-guidelines.md; docs/game/settlement-plan.md section 8.1 (findings 7, 8, 10 and 11), 8.2 and,
in 8.3, the paragraphs Y01 (yours) and Y02 (what comes next, which you do not build); in docs/game/decisions.md the sections
"D-game-32" and "D-game-24" point 1 and "D-game-25" point 2 (grep for them, read only those); in content/README.md "The edit loop".
Then the code, by member name (line numbers move):
- src/Vocaterra.Game.Core/Session/VoxelSession.Sleep.cs (whole) and SleepOffer.cs (whole).
- Session/VoxelSession.Death.cs: `DieIfDead`, `WakeAtFire`, `NearestFreeGround`, `IsFreeGround`.
- Session/VoxelSession.cs: `FireLit`, `CampFireRing`, `PlayerFireRing`, `HasPlayerFireRing`, `AtPlayerFireRing`, `LightFire`,
  `ScanPlayerFireRing`, `PointAtOwnCamp`, `GiveSpareTorch`, the torch branch of the block-placed handler, `ScanFireRing`.
- Session/VoxelSession.Eat.cs (`Fires`), Session/VoxelSession.Needs.cs (rest by a lit fire), Needs/FireRings.cs (whole),
  Simulation/VoxelSimulation.cs (its own `Fires` and the crafting-station check).
- Needs/NeedsData.cs (`DeathSpec`, `RestSpec`, the loader), Needs/NeedsValidator.cs, content/game-v1/game/needs.json,
  content/schema/needs.schema.json.
- Input/KeyBindings.cs (`World`), and in src/Vocaterra.Game/VoxelGame.cs: `SleepFree`, `FireOffersSleep`, `FireNotTired`,
  `CardOffersSleep`, the view's `SleepPrompt` and `EndingSleep`, `SleepNow`, `SleepRefusal`, `DriveCollapse`, `PassNight`.
Read the members named, never whole long files (VoxelSession.cs and VoxelGame.cs are large). Heavy output goes to log files; print at
most 30 lines.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it
is; a regression check is a C# test, never a script with text baselines; tests are named Given_When_Then in folders that mirror the
source; do not edit either implementation-plan.md, either decisions.md or docs/game/settlement-plan.md (the orchestrator does); engine
code only in src/Vocaterra.Game; Vocaterra.Game.Core references no package (`ArchitectureRulesTests`); no System.Random and no clock
in the evaluation path; nothing under .github/workflows/. The weather series is editing `VoxelSession.Night.cs`, `Save/SaveGame.cs`,
`Save/SaveDocument.cs` and `Simulation/VoxelSimulation.cs` on another branch: keep your edits to those files as small as the work needs.

What this milestone is for. Today sleeping, the E prompt, the closing card, the arrow to bed and waking after a death are all tied to
one scene point, `firering.player`, and to "my own fire is lit". The series will add a bed at an inn and later a camp the player
places, so those five things must ask a list of sleep places instead. This milestone builds that list and moves the five things onto
it, with the mountain fire as the only place in it, so the game plays exactly as before. (The name is "sleep place" because
`needs.json`'s `rest` is already the rule for resting by a lit fire. Do not rename that.)

Deliverables:

1. **The data.** In content/game-v1/game/needs.json a `sleepPlaces` list with one entry for the mountain fire: an id (`camp.fire`), the
   scene point (`firering.player`), the kind (`fire` or `bed`), the reach across and in height (today's `FireBlocks` 5 and 6) and the
   access (`always` or `lodging`). `death.wakeAt`, which nothing reads today, becomes the id of the sleep place the player wakes at
   (`camp.fire`). `NeedsData`, `NeedsValidator` (unique ids, a known kind and access, `wakeAt` names a listed place), the schema, and
   the needs row of content/README.md. If the bundle's validation can check that each place's point is in the scene, add it there;
   if it cannot from where it sits, say so in the report.

2. **`SleepPlaces`** in Game.Core, owned by the session: the authored places resolved against `Scene.Points` (a place whose point
   the scene lacks is left out, as today's code does nothing without the point), and places added and removed while the game runs
   by a cell (Y12 will use that; here only tests do). It answers: whether a place is usable (a fire when a lit ring stands at it, read
   from the world at the time of asking; a bed when its access is met: `always` is met, `lodging` is met by a flag on the session that
   is false for now and that Y05 will set); the usable place the player stands at, if any; whether any place is usable; the place to
   wake at.

3. **The five things moved onto it.**
   - `CanSleep` asks whether any place is usable where it asked `FireLit` (so the `--sleep` aid, which checks no position, still
     works). `AtOwnFire` becomes `AtSleepPlace`.
   - `RestDue`, `RestLine`, `NoteRestDue` and `PointAtFire`: the arrow goes to the nearest usable place.
   - `SleepOffer`: the prompt functions ask `AtSleepPlace`. `OnCard` also needs the player at a usable place (today the card's Enter
     sleeps wherever the player stands, which will be wrong at an inn); away from one the card offers no sleep, and the objective's
     line and the arrow already say where to go. This is the one intended change of behaviour: test it.
   - `WakeAtFire` becomes `WakeAtSleepPlace`: the place `death.wakeAt` names, with `NearestFreeGround` and the wake distances kept.
     Correct the comment that says "two to three blocks" (the constants give two to five).
   - The front end follows: the prompt, the card and `SleepRefusal` in VoxelGame.cs, the bindings in KeyBindings.cs. Text keys stay as
     they are (Y08 rewrites the strings).
   - Sleep, the prompt, the card, the arrow and the waking must no longer read `_fireLit`, `_playerFireRing` or the constant
     `PlayerFireRing`. The `light-fire` objective, its hooks (`spare-torch`, `point-own-camp`, `scan-fire-ring`) and the `fire_lit`
     event keep what they use today: they leave in Y07, not here.

4. **One fire-ring finder.** `FireRings` reads the rings once, and the session and the simulation each keep their own. Make it one
   that both use, with a `Rescan()`. Test that a ring whose blocks are set while the game runs is seen after `Rescan()` by the rest
   rule, by the crafting station's check and by `NearestFreeGround`.

5. **Tests.** New tests for `SleepPlaces`, the data and the validator. `SleepTests`, `NightTests`, `DeathTests`, `DayOneSafetyTests`,
   `LoadKeepsDayTests`, `OpeningAndEndingTests`, `OpeningFrontendTests` and `KeyBindingsTests` pass with no change of what they assert;
   where a test had to change (the data's shape, a renamed member), list it in the report with the reason.

6. **The scripted runs stay the same.** Capture `r20-sleep`, `r26-death` and `r30-no-classes` before your first change and after your
   last (`scripts/scripted-runs.sh <out> <name>...`, docs/game/scripted-runs.md) and compare them with `scripts/compare-runs.py`. The
   only differences allowed are the bundle hash and what follows from it; report the comparison's output.

7. **The bundle and the documents.** `needs.json` is in the bundle: after the edit run the content loop as content/README.md says
   (`scripts/content-loop.sh`, with `--tier full` once before the report); no Spanish changes, so nothing is to be recorded. Update
   src/Vocaterra.Game.Core/README.md and docs/game/architecture.md where they describe sleeping at the fire and waking after a death
   (grep for them), and docs/game/running.md only if a sentence in it is no longer true.

Not in this milestone: any change to the save (Y02 adds the entry and the last place slept; the `lodging` flag is not saved yet), any
new block, any scene edit, any change to the objectives or their hooks, any rewritten string, `Sound/AmbientBeds.cs`.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean; scripts/limited.sh scripts/content.sh
validate passes; the three scripted runs compare as deliverable 6 says. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfPceqswnMMBmTQzg9

Report, one page: branch, commit SHA, worktree path; what was built, type by type; test counts; the scripted runs' comparison; the
tests you changed and why; bugs found and fixed (where, symptom, fix, test); anything in Y02's or Y05's plan that should change
because of what you found; known-not-fixed with reasoning; files touched outside the milestone's area; anything left undone and why.
Leave out how you searched.
