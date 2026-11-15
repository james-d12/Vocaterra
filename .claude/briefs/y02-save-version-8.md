You are implementing Y02, "Save version 8 and the sleep entry", for the Vocaterra repository (C#, .NET 10). The language core is the
engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core
(the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y02, which already exists on the branch `Y02-save-version-8` (cut from
`feature/settlement`). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y02 &&` and use absolute paths for file
tools; your shell may start in another checkout, which you must not edit. Commit after each item that builds and passes its tests, with
the prefix `Y02: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge `feature/settlement`
or `main`. When you finish, leave the worktree on your branch, clean.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly. Never start two heavy
commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Other agents build
on this machine too, so a heavy command will often wait for a free slot: that is normal, let it wait, and build and test in few,
well-chosen runs. While iterating run `scripts/test.sh fast` or `scripts/test.sh project <test project>`; run `scripts/test.sh full`
once before reporting. Exit 137 means the 6 GB cap killed it: lower the parallelism, do not work round the cap. The scripted runs
share a default work folder with other sessions: set `VOCATERRA_RUNS_WORK` to a folder of your own (in your scratchpad) for every
`scripts/scripted-runs.sh` call, the same folder for the capture before and the capture after.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
use a wrapper script outside the repository). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/process/code-guidelines.md; docs/game/saves.md (whole); in docs/game/settlement-plan.md section 8.1
(findings 1, 4 and 10) and, in 8.3, the paragraphs Y02 (yours), Y05 and Y12 (they will use what you save); in docs/game/decisions.md
the section "D-game-32" (point (d)) and "D-game-24" point 1 (grep for them, read only those). Then the code, by member name:
- src/Vocaterra.Game.Core/Save/SaveGame.cs: `CurrentVersion`, `OldestReadableVersion`, the load path and its version checks,
  `RestoreParts`, the capture side, `Sleep`.
- Save/SaveDocument.cs: `SystemsSave` and the entries beside it (`Needs`, `Chest`, `Bushes`, `Gifts`, `Planted`, `Procedures`): each
  is a model for an optional entry with a default when absent.
- Save/SaveMigration.cs: `Adapt`, only to see how a cross-bundle load treats the systems entries.
- Session/SleepPlaces.cs (whole; Y01 built it), Session/VoxelSession.Sleep.cs, the `LodgingGiven` flag on the session,
  Session/VoxelSession.Death.cs (`WakeAtSleepPlace`), Session/VoxelSession.Night.cs (`ApplyNight`).
- src/Vocaterra.Game/VoxelGame.cs: `SleepNow`, `DriveCollapse`, `PassNight` (how a sleep captures a save and restores it).
- The save fixtures tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json and save-v4.json and every test and script that names
  them (grep the whole repository, scripts/ and docs/ included).
Read the members named, never whole long files. Heavy output goes to log files; print at most 30 lines.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it
is. This brief also allows the scripts that name the save fixtures (scripts/content-loop.sh and any other your grep finds) and
content/README.md where it names them. A regression check is a C# test, never a script with text baselines; tests are named
Given_When_Then in folders that mirror the source; do not edit CLAUDE.md, either implementation-plan.md, either decisions.md or
docs/game/settlement-plan.md (the orchestrator does; name in your report every line of CLAUDE.md that your change makes untrue);
engine code only in src/Vocaterra.Game; no System.Random and no clock in the evaluation path; nothing under .github/workflows/. The
weather series is editing `Save/SaveGame.cs`, `Save/SaveDocument.cs`, `VoxelSession.Night.cs`, `Simulation/VoxelSimulation.cs` and
the two fixtures on another branch: change in those files what this brief asks and nothing beside it.

What this milestone is for. The owner decided that old saves break for this series (the game is unreleased): the save version
becomes 8, older saves are refused, and nothing is migrated. The series needs three things kept across a sleep and a load, because a
sleep is a save restored in the same run and anything not in the save is lost every night: whether the innkeeper has given the room
(`LodgingGiven`, which Y05 will set), the place the player last slept at (a death then wakes them there, D-game-24), and the sleep
places made while the game runs (Y12's camp).

Deliverables:

1. **Version 8.** `CurrentVersion` and `OldestReadableVersion` are both 8. A save of an older version is refused with a message
   that names its version and the oldest that loads. Take out the code that only served older versions (the branches on the
   version number, the old shapes), where it comes out cleanly; list what you removed. Write no migration.
2. **The `sleep` entry** in `SystemsSave`, optional as the others are (absent means: no lodging, no last place, no places made in
   play): the lodging flag; the last place slept, by its id, and with its cell when the place was made in play; the places made in
   play, each with what `SleepPlaces.Add` needs. Captured and restored with the other entries. In `RestoreParts` the places made in
   play go back before anything that asks `SleepPlaces` (the objective's sleep line, the arrow).
3. **The last place slept.** A sleep at a sleep place records it. It is written after `RestoreParts` inside `SaveGame.Sleep`, since
   the save the sleep restores was captured before the sleep (a value set before the capture would be the old one, and one set
   only on the session before the restore would be lost). A sleep that is at no place (the `--sleep` aid away from one) and a
   collapse leave the last place as it was.
4. **Waking after a death** goes to the last place slept when it is still in `SleepPlaces`, and otherwise to the place
   `death.wakeAt` names, as today.
5. **One fixture.** A version 8 fixture replaces save-v1.json and save-v4.json. Find how the fixtures were made and make the new one
   the same way, from a deterministic session; if they were written by hand, say so and produce the new one by a small recorded
   run. Every test that read the old ones reads the new one or, where its point was that an old version loads, becomes a test that
   an old version is refused (one small inline document is enough for that). Update the scripts that write the bundle hash into the
   fixtures.
6. **A cross-bundle load** (`SaveMigration.Adapt`, the game's default when the bundle changed) keeps the entry and lets go of a
   last place or a place whose id the new bundle no longer has, after the model of the other entries. Nothing is written for saves
   older than version 8.
7. **Tests.** The entry's round trip (with and without each part); a save without the entry; the last place after a sleep at the
   fire, after the aid away from a place, after a collapse; a death that wakes at the last place slept, with a second place added
   in the test; version 7 refused with its message; the fixture's tests. `SleepTests`, `NightTests`, `DeathTests`,
   `LoadKeepsDayTests` and `SleepPlacesTests` keep what they assert.
8. **The scripted runs.** Capture `r21-skip`, `r26-death` and `r03-npcs` before your first change and after your last and compare
   them (`scripts/scripted-runs.sh <out> <name>...`, `scripts/compare-runs.py`). The only differences allowed are in the save files
   (the version, the new entry) and what follows from the bundle hash; `r26-death` wakes at the same cell (152, 73, 106). Report the
   comparison. (`r20-sleep` and `r30-no-classes` are known to stop before their sleep today, so they prove nothing here.) Then one
   run of your own that does sleep: `--seed 42 --mute --needs off --dev --skip-to-session 2 --wait 1 --sleep --wait 2 --sleep
   --wait 2 --screenshot <png> --frames 30 --save <file>` goes from play session 2 to 4; check that its save holds the last place
   slept only if the player stood at the fire, and say what it holds.
9. **Documents.** docs/game/saves.md (version 8: what it adds, that nothing older is read, and the removal of the paragraphs about
   versions 1 to 6 that are no longer true), src/Vocaterra.Game.Core/README.md where it describes the save, content/README.md
   where it names the fixtures.

Not in this milestone: setting `LodgingGiven` anywhere but tests (Y05), any bed or new block, any scene edit, any change to the
objectives, the "blocks are gone, drop the place" rule for a camp (Y12).

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count; the feature branch has 10,714 passing and 3 skipped today); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; the scripted runs compare as deliverable 8 says.
Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfPceqswnMMBmTQzg9

Report, one page: branch, commit SHA, worktree path; what was built; what old-version code you removed; how the fixture is made;
test counts and the tests you changed, with the reason; the scripted runs' comparison; the lines of CLAUDE.md and of other
documents outside your area that are now untrue; bugs found and fixed; anything in Y05's or Y12's plan that should change;
known-not-fixed with reasoning; anything left undone and why. Leave out how you searched.
