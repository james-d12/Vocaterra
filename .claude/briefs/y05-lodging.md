You are implementing Y05, "Lodging", for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp, the
Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the engine-free logic),
Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y05, which already exists on the branch `Y05-lodging` (cut from
`feature/settlement`). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y05 &&` and use absolute paths for file
tools; your shell may start in another checkout, which you must not edit. Commit after each item that builds and passes its tests, with
the prefix `Y05: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge `feature/settlement`
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

Read first: CLAUDE.md; docs/process/code-guidelines.md; in docs/game/settlement-plan.md section 8.2 (the first bullet), in 8.3 the
paragraphs Y05 (yours), Y06 and Y07 (they build on yours), and section 9.6; in docs/game/decisions.md the section "D-game-32" (point
(b): the bed is free, lodging is for good) and "D-game-24" point 1 (grep for them, read only those); docs/game/saves.md, the part on
`systems.sleep`. Then the code, by member name:
- src/Vocaterra.Game.Core/Npc/HostEventTable.cs: `Kinds` and the parse switch; Npc/NpcEffect*.cs: `NpcEffectKind` and how a kind with
  no item is built (`give_missing`, `procedure_repeat`); content/schema/host-events.schema.json (how earlier kinds were added: V03, V15
  and W08 added kinds without a schema bump); Content/GameDataValidator.cs where it checks host-event actions.
- Session/VoxelSession.cs: the effect dispatch (the `switch` on `NpcEffectKind` near `GiveItem`, `PointAtPlace`) and
  Session/VoxelSession.Teaching.cs `ApplyTeacherEffect` as a model of a kind handled in its own file.
- Event/WorldEvent.cs: `WorldEvents` and `All`; Event/Trigger.cs `TipEvents`; Save/Autosave.cs `ReasonOf` (the autosave reasons).
- Session/VoxelSession.Sleep.cs (whole; `LodgingGiven`, the capture and restore of the sleep entry), Session/SleepPlaces.cs (whole;
  the `lodging` access), Session/SleepOffer.cs (`AtFire`, `OnCard`, `PromptText`, `Text`), Session/VoxelSession.Death.cs
  (`WakeAtSleepPlace`), Save/SaveGame.cs `Sleep`.
- src/Vocaterra.Game/VoxelGame.cs: where `view.SleepPrompt` is set, `SleepNow`, `PassNight`; the locale strings `sleepPrompt`,
  `sleepCard` (grep content/locale).
- content/game-v1/game/needs.json: the sleep place `inn.bed` (point `bed.inn`, kind `bed`, access `lodging`), from Y04. The stand cell
  is (278, 231), beside the west bed at (277, 231); the inn's door is (280, 226).
- tests/Vocaterra.Game.Core.Tests: SleepPlacesTests, SleepEntrySaveTests, SleepTests, DeathTests, and how existing tests build a
  test bundle or a host-event table in code (grep for `HostEventTable.Parse` or the test bundle helpers).
Read the members named, never whole long files. Heavy output goes to log files; print at most 30 lines.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/locale, content/schema, assets/ and
docs/game/ and takes the core as it is. A regression check is a C# test, never a script with text baselines; tests are named
Given_When_Then in folders that mirror the source; do not edit CLAUDE.md, either implementation-plan.md, either decisions.md or
docs/game/settlement-plan.md (the orchestrator does; name in your report every line of CLAUDE.md that your change makes untrue);
engine code only in src/Vocaterra.Game; no System.Random and no clock in the evaluation path; nothing under .github/workflows/. The
weather series edits the save code and `VoxelSession.Night.cs` on another branch: change in those files only what this brief asks.

What this milestone is for. The inn's bed (Y04) exists with the access `lodging`, and nothing can grant it yet. Y05 adds the action
that grants it, so that Y06's innkeeper only needs a host-event row. The owner decided the bed is free and lodging is for good: no fee,
no expiry, no bunk. A night in the bed is the same night as one at the fire; a death after a night in the bed wakes at the inn
(D-game-24: the last place slept). No NPC in game-v1 grants lodging in this milestone; Y06 adds the innkeeper and her row.

Deliverables:

1. **The action kind `grant_lodging`** in `HostEventTable` (no fields beyond `kind`), its `NpcEffectKind`, its row in the schema, and
   the validator's acceptance of it. Applied by the session: it sets `LodgingGiven = true`; giving it a second time changes nothing and
   raises no second event.
2. **The world event `lodging_granted`** in `WorldEvents` (and `All`, so a tip or an objective's trigger may name it), raised once,
   when lodging is first given. It is an autosave reason in `Autosave.ReasonOf`, with a console reason in the words the others use.
   Y07 will hang the objective `inn-room` on it; do not add that objective.
3. **Kept across a sleep and a load.** Y02 saves `LodgingGiven` in `systems.sleep`; prove it holds when lodging is given in play:
   given, then a sleep (a save restored in the same run), then a load from a file. If anything drops it, fix it there.
4. **The bed's prompt.** At a bed whose access is met, the sleep prompt and the card work as they do at the fire. At a bed whose
   access is not met (no lodging yet), the player sees a short line that says the bed is the inn's and a room must be asked for, and
   E does nothing; the text is an English locale string beside `sleepPrompt`, never hard-coded. Away from any sleep place nothing
   changes. Keep the fire's prompt and card as they are.
5. **A dev aid for the scripted runs and the spot check:** a launch option `--lodging` that gives lodging at start, allowed only with
   `--dev`, beside the other dev aids in `LaunchOptions.cs`, listed in its help and in docs/game/running.md.
6. **Tests**, on a test bundle or a host-event table built in the test (game-v1 has no `grant_lodging` row): the kind parsed and
   validated; the effect sets the flag and raises `lodging_granted` once (twice given, one event); saved and loaded; kept across a
   sleep; the inn's bed not usable before and usable after; a night in the bed passes the night as one at the fire does (the same
   session step and needs); the last place slept is `inn.bed` after it; a death after it wakes at the bed's stand cell (or the nearest
   free ground beside it); the prompt text at a locked bed and at an open bed. The existing sleep, death and save tests keep what they
   assert.
7. **A run in the game:** `--seed 42 --mute --needs off --dev --lodging --skip-to-session 2 --walk-to 278,231 --wait 1 --sleep --wait 2
   --screenshot <png> --frames 30 --save <file>` (adjust the walk if `--walk-to` stops short: Y04 found it stops about half a block
   short, and `--teleport` lands on the roof). Report the save's `systems.sleep` and the play session before and after. Then the same
   without `--lodging`: the bed refuses and the player sleeps nowhere (report the console line). Put the screenshots in
   `artifacts/y05/` (git-ignored).
8. **Unchanged behaviour:** capture `r21-skip`, `r26-death` and `r03-npcs` before your first change and after your last, and compare
   them (`scripts/scripted-runs.sh <out> <name>...`, `scripts/compare-runs.py`). No difference is expected; report any and why.
9. **Documents:** docs/game/saves.md (the flag is now set in play), content/README.md where it lists host-event kinds or world events,
   src/Vocaterra.Game.Core/README.md if it lists them, docs/game/running.md for the dev aid.

Not in this milestone: the innkeeper, her lines, voice or host-event row (Y06); the objective `inn-room`, any change to `session.json`
or the opening (Y07); any scene edit or change to `needs.json`'s places; any fee or expiry; the mountain's camp (Y08).

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count; the feature branch has 10,749 passing and 3 skipped today); scripts/limited.sh
dotnet format Vocaterra.sln --verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. If you edit
content/game-v1 (you should not need to), run `scripts/content-loop.sh --grammar --speech` and put the new hash in the save fixture.
Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built; test counts and the tests you added or changed, with the reason;
the run in the game (both cases) and the scripted runs' comparison; the lines of CLAUDE.md and of other documents outside your area
that are now untrue; bugs found and fixed; anything in Y06's or Y07's plan that should change; known-not-fixed with reasoning;
anything left undone and why. Leave out how you searched.
