# Saves

How saves survive content updates, and autosave with the crash report. Saving and loading in a run are in [running.md](running.md).

## Saves survive content updates (X01)

Every content change used to make every save unloadable (the S10 rule: a save refuses another bundle hash), so a player lost all progress when the game
updated. Now a `--load` of a save made from another bundle **migrates** it (`SaveMigration`, engine-free, in `Vocaterra.Game/Save`) unless the run is strict.

- **Kept where the id still exists in this bundle:** the learner's record (history, word rows), the settings and the session number, the word records
  (by lemma), the carded nouns, the phrase records (one per sheet row, by id), the objective's counts (by step key), each NPC's conversation state and its
  learner-model skills (`word.<noun>` and `phrase.<id>` skills of content that is gone are dropped), the tips, the arrow, the inventory (items are an enum
  of the code, not of the bundle) and the lessons. A **lesson** is mapped by step id: a completed lesson stays completed (and `EverCompleted` stays) with the
  outcomes of the steps that still exist; a lesson in progress keeps the longest run of its steps, from the first, that are all among the finished ones,
  goes on at the next step, and does again a finished step that follows one the lesson has not finished (the tries on the step in progress reset when
  the step changed). A lesson that is gone is dropped, and so is an active mark or a replay that named it.
- **Dropped and logged:** records whose phrase row, noun, lesson, step, tip or step key is not in this bundle. **New** content (a phrase row, a lesson, a
  noun) starts empty, as in a fresh game.
- **World edits are applied by position.** The save holds whole chunks of the world it was made from (and, since X01, the grid's chunk counts, so a grid that
  changed size still maps by position). A saved cell is applied only if it is a change the player can make to the cell the new scene has: a gathered block
  (the cell now holds what a gather leaves) or a placed block (planks or a torch) in air or undergrowth that touches a block. Anything else is the old world
  generation, not the player's work: it is skipped and counted in the report. The world's digest is then **reported, not required** (it still matches, and
  the log says so, when the generation did not change). A player who would stand inside a block of the new world starts where a new game starts. A save of
  this very bundle is restored exactly as before: the exact diff and the digest check, which still refuses a damaged save.
- **Told and logged:** the toast "The game was updated. Your progress was kept; N old records no longer in the game were dropped.", the console lines
  `migrate: ...` (the areas, then each dropped id), and a `save_migration` line in the session log. `VoxelSession.Migration` holds the `SaveMigrationReport`.
- **Strict mode.** `--strict-save` refuses a save of another bundle with the old message plus the reason; **`--playtest` implies it** (a playtest's recall
  session needs the bundle frozen between the sessions, D-game-10; see `docs/game/playtest.md`). The library's `SaveGame.Load/Check/Restore` take
  `migrate` (default off: strict), and the front-end passes `!StrictSave`.

```bash
# a save on the current bundle, then the same save under a changed copy of the bundle (a lesson step removed, a tip reworded; the copy is re-stamped with
# scripts/content.sh): the strict run refuses, the default run keeps the progress and prints what was dropped; the toast is in the screenshot
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --save /tmp/x01.save --screenshot /tmp/x01-before.png --frames 30
dotnet run --project src/Vocaterra.Game -- --content /tmp/x01-bundle --mute --load /tmp/x01.save --strict-save --screenshot /tmp/x01-strict.png --frames 5
dotnet run --project src/Vocaterra.Game -- --content /tmp/x01-bundle --mute --load /tmp/x01.save --screenshot /tmp/x01-migrated.png --frames 30
```

## Format version 8: the `systems` map, the needs, the water and the sleep entry (Y02)

The current version is 8, and it is the only one read. The game is unreleased, so a save of an older version is **refused** (`SaveGame.Parse`: "The save is format version N;
the oldest this game loads is 8 (it writes 8), and older saves are not migrated.") and nothing migrates it: the code that read versions 1 to 7 is gone (the branches on the
version number, the version 2 word rows, the first-words split). One fixture, `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v8.json`, is the version 8 save the tests read. The weather's volume (E09) is `settings.sound.weather` (0 to 1, the default 0.6; a save from before E09 has no field and takes the default, with no version change).


**How the fixture is made:** a scripted game run, not by hand, so it is the game's own output. From the repository root, after a build (the camp route of `scripts/scripted-runs.sh`'s
`CAMP`, then a talk, then the player's own fire):

```bash
dotnet run --project src/Vocaterra.Game -- --session-dir <scratch> --needs off --seed 42 --mute --walk-to 176,186 <CAMP steps> --walk-to 177,185 --craft antorcha \
  --talk shopkeeper --say "quiero agua, por favor" --leave --teleport 153,109 --place antorcha@153,74,105 --wait 1 --screenshot <png> --frames 30 --save <file>
```

The save has the player standing at the lit mountain fire, one conversation page, no lodging and no sleep recorded (so no `sleep` entry). `scripts/content-loop.sh` writes a new
bundle hash into it as into any file under `tests/`.

**The `sleep` entry (Y02).** `systems.sleep` keeps what a sleep (a save restored in the same run) and a load must not lose, all optional, the entry absent when nothing is set:
`lodgingGiven` (the innkeeper has given the room; left out when false; set in play since Y05 by the host action `grant_lodging`, once and for good, and by the dev aid `--lodging`; a sleep, a load and a migration keep it), `last` (the place last slept at: `{"id"}` and, for a place made in play, its cell `x`, `y`, `z`) and
`places` (the places made in play, `SleepPlaces.Add`: `id`, `x`, `y`, `z`, `kind`, `radiusBlocks`, `heightBlocks`, `access` and `point` when it has one). The authored places are the bundle's
and are not saved. `RestoreParts` puts the places made in play back first, before the objective's sleep line and the arrow ask `SleepPlaces`.

- **The last place slept** is written by `SaveGame.Sleep` after `RestoreParts` (the save a sleep restores was taken before the sleep): a sleep at a usable place records it; a sleep at
  none (the `--sleep` aid away from a place) and a collapse leave it as it was. A death wakes the player at the last place while `SleepPlaces` still has it, else at the place
  `death.wakeAt` names.
- **Across bundles** (`SaveMigration.Adapt`) the entry is kept: the lodging and the places made in play stay (a place whose cell is outside the new world is dropped), and a last place that
  is neither a place of the new bundle nor a place kept is let go (reported as `place last slept at`).

Version 6 added the optional `systems` map to the save: a system's name to its entry, so a system joins the save without another format change (W06 added `procedures`).
Its `needs` entry holds the player's five meters and two timers: `hunger`, `thirst`, `sleep` (0 to 100), `stamina` (0 to its current maximum), `health` (0 to 100),
`exhaustionSeconds` (seconds of active play at sleep's last stage) and `effortFreeSeconds` (seconds since the last effort). Later milestones added the regrowth, gift and
chest state as optional fields of the entry. Stages and their effects are never saved: they are worked out from the meters and the data. Water's fields are optional like the others:
`air` (the player's breath, in ticks) in the simulation state, and `flow` (the water that is not a source in the scene diff and the cells waiting to flow, H06). The underwater blur setting
(H11) is the optional `underwaterBlur` of the save's `settings` (the level as its number: 0 off, 2 medium, 3 high; no field is the default, low). Drowning leaves no state of its own.

- **A save without `systems`** loads with full needs.
- **The needs are not in the world's digest.** A load and a night compare the digest; the needs are checked by range instead: a value out of range
  (a drive or health outside 0 to 100, stamina over its maximum, a timer below 0) is **clamped** into it, and a value that is not a number **refuses** the save.
- **A night is a load in the same run** with the night's changes on the needs (N03): `SaveGame.Sleep` restores the needs as a load does, then `VoxelSession.ApplyNight` applies
  `PlayerNeeds.Sleep` (sleep to 100, health +20, hunger -15 and thirst -20 never below 10; with `--needs off` nothing). A **collapse** is the same night with `collapse: true`
  (`PlayerNeeds.Collapse`: sleep to 50, no health), taken where the player stands, so the world, the position and the tick are the save's. Both advance the day and the sitting
  and run the day's resets (`RunDayResets`, `DayResetCount`; a load runs none).
- **The belongings chest (N04)** is the `chest` entry of `systems`: `{"x","y","z","stacks":[{"item","count"}]}`, the cell of the Chest block and one total per item kind, absent when
  no chest stands. The Chest block itself is in the world's block edits (`chunks`), as any placed block; a load restores both and never removes a chest, a night does
  (`RunDayResets`), with its contents.
- **The NPCs met (L01)**: `metNpcs` is an optional list of the NPC ids the player has talked to (a class opens on
  having met its teacher); a save without it takes every NPC as met from day 2 on and only the guide before. A lesson is never saved engaged: a load
  restores the active class paused.
- **The orders taken on (issue #54, D-game-30)**: `givenOrders` is an optional list of the order ids the player said «sí» to (or whose class reached
  its `lessonStep`), left out while empty; a save without it has taken on none, and the NPCs offer their orders again. An NPC saved while it waited on its offer
  (`offering_order`) is put back in its usual state at the next talk, which offers again. The orders the player said no to today are not saved: a load offers them again.
- **Picked bushes and the day's gifts (N06)** are the `bushes` and `gifts` entries of `systems`, both absent when empty. `bushes` is `[{"x","y","z"}]`, the cells of the berry bushes
  picked by hand since the last night (the rule is `regrowth` in `game/needs.json`); each is a plain bush in the world's block edits until a night (`RunDayResets`, `RenewAtNight`) sets it
  back to a berry bush. A regrown bush is the generated block again, so the world's digest is the unpicked world's: the digest follows the blocks, as for any placed or removed block, and
  a cell the player built on meanwhile is let go. `gifts` is the keys (`shopkeeper:bread`, `villager:bread`, `guide:water-bottle`; `gifts` in `game/needs.json`) given since the last night.
  A gift a lesson step requires (a `do` step on `item_given` at that NPC) is never closed and never counted. With `--needs off` nothing is recorded, nothing grows back and every gift is
  given. The NPC whose gift is used answers its `<npc>.gift_used` with a line of its existing pools (the stall `shop_price`, the villager `villager_bye`, the guide `guide_offer_no`).
- **Planted bushes (N08)** are the `planted` entry of `systems`, absent when none stands: `[{"x","y","z"}]`, the cells of the bushes the player planted at the camp (`planting` in `game/needs.json`) that still stand. Each is also in `bushes` until a night grows it back, so the picked list needs no change; the cap of planted bushes counts this list. A save with none loads with none.
- **The day clock and the weather (E01, E04)** are the `atmosphere` entry of `systems`, with two parts. The entry is absent in a new game's default state (the clock
  not held, the weather still in its first spell), so a save of a game that never slept or changed weather is byte for byte what it was (the version 8 fixture loads as it is); it is written when either part is not a new game's. The state digest adds nothing for the default state, and from the first change of weather
  on the entry is part of the digest and the save. A load and a sleep's restore put the entry back before the digest check. The save is version 8 with it (main's weather series left the save at 7 and the settlement series' `systems.sleep` made 8; the entry is optional, so it changed no version): `systems` carries `sleep` and `atmosphere` side by side, and a load raises neither a `day_phase` nor a `weather_changed` event.
  - **The clock** is `{"offset", "frozenHour"}`. `offset` is the number of ticks added to the clock's position in the cycle (0 up to one cycle; a sleep or a collapse sets it so
    the hour is `wakeHour` in `game/day.json`), `frozenHour` the hour a test or a screenshot holds the clock at (absent when it runs). A save without the entry starts the clock at `startHour`.
  - **The weather** is the `weather` field: `{"current", "previous", "spellStart", "spellEnd", "spells", "mornings", "held"}`. `current` and `previous` are kind ids of
    `game/weather.json`, the ticks are the spell's, `spells` and `mornings` are the counters the seeded draws are keyed by (with the world seed, so a restored game continues to the same
    next kinds), `held` a kind a test holds. The field is absent while the weather is in its first spell (the start kind, never rolled or held). A save without it restores the first spell;
    an unknown kind id is a refused save.
- **A load raises no `need_stage` event** for what was saved: the stages are taken as they are.

## The conversation pages (U08)

Each NPC's page of conversation lines (U06) rides in the save, so the panel scrolls back over earlier sessions. `dialoguePages` is an **optional** top-level field (an optional field, no format version of its own: a save without it loads with empty pages): a list of `{"npc", "lines": [{"speaker", "text"}]}`, one entry per NPC that has lines, in NPC id
order, each page at most 200 lines (`DialogueModel.MaxLines`, the newest kept). The field is left out of the file while no NPC has a line. `speaker` is a stable name: `player`,
`player-spoken`, `note`, `correction`, `accent`, `guide` (the NPC's own reply), `world`, `hint`, `english`; the coaching line is made by the panel and never stored, and a name a build
does not know is skipped on load. Every write of a save (F6, the end of a run, the autosave, the crash's last-chance save, a night) captures the pages through `SaveService`;
the game puts them back with `DialogueModel.RestorePages` after a load. What was opened is not saved, so the first talk with an NPC after a load appends its opening to the restored
page as the new session's greeting. The learner's own lines are also in `learner.history` (the book's "What I said" reads that, not these pages). A page costs about 100 bytes a line.

## Autosave and the crash report (X05, R04)

The game saves itself at its safe points, and a crash leaves a report instead of losing the session.

**Autosave.** `AutosavePolicy` (`src/Vocaterra.Game/Autosave.cs`, engine-free) decides the frames; the game writes them in `VoxelGame.AutosaveDue`.

- **When.** A lesson step ends (done, skipped or moved on), a lesson completes, a camp objective step is done, the session completes, a dialogue closes, the
  player sleeps at their fire (V12c: the session as it ends, with no toast; the policy then starts again as a load's does), and
  every **five minutes of play** (18000 simulation ticks at 60 a second; the wall clock is not read). Five minutes: a crash loses at most that much of play, and a save is
  a few tens of kilobytes, so writing one every five minutes costs nothing a tester notices. An event that falls inside an open dialogue waits for the
  dialogue to close (a lesson runs inside its guide's panel, so its saves come when the panel shuts).
- **Only at rest.** Never while a turn is on the worker, key words are on show, a dialogue is open, the microphone is recording, a check battery or the mic check
  is on, or the report panel is open. The save is the same document F6 writes, so `--load` reads it like any save, in the next play session.
- **Where.** In the session folder: `autosave.save` (a playtest writes `playtest.save`, the file F6 writes, so its end-of-run save and its autosave are one file).
  Its path is printed at the start (`autosave: <path> (...)`), and each write is printed (`autosave: <path> written at <reason> (session, tick, stored time, digest, bytes)`).
- **How.** The document goes to `<file>.tmp`, is flushed to the disk, and is renamed over the save (`SaveFile.Write`). A crash or a failed write before the rename
  leaves the last autosave intact; a stale `.tmp` is overwritten by the next write. The save is taken on the game loop (the state must not move during it):
  the first one builds the fresh scene it is diffed against (about 0.7 s on the valley, once per run, then kept); later saves take about 20 ms.
- **The feedback.** A small "Saved" toast, four seconds, as the other toasts show. Nothing else on the screen changes, and no save changes the world, the digest or the replay:
  a save only reads the session.
- **Off.** `--no-autosave` writes no autosave (a playtest still writes `playtest.save` at its end). Scripted and test runs write their autosave into their session folder
  (`--session-dir` to choose it); use `--no-autosave` when a run must write nothing.

**Crash report.** `Program.cs` runs the game inside a handler. A crash on the game loop, an unhandled exception on any thread, or an unobserved task exception writes
`reports/crash-s<session>-t<tick>.md` in the session folder (`crash-<local time>.md` when the state cannot be read), beside the F8 reports, with:

- the exception, its stack and its inner exceptions, and where it came out;
- the game state the handler could read (the session, the tick, the bundle, the seed, where the player stands, the objective, the lesson, whether a dialogue is open),
  each row on its own, so a row that fails says so and the others are shown;
- the last-chance save: `crash.save` in the session folder, written only when the game is at rest and no dialogue is open. It never replaces the autosave or a player's save;
- the newest 200 lines of `session.jsonl`, and the build commit.

Then the handler prints both paths (`crash: report written to ...`, `crash: last-chance save written to ...`) and the process exits with code 1. An unobserved task
exception writes a report and no save, and the game goes on. The handler never throws: a folder that cannot be made, or a read that fails, is said on the console.

`scripts/playtest-report.sh` lists the crash reports with the F8 reports (kind `Crash`). For the facilitator, see `docs/game/playtest.md` section 10.

`--debug-crash-after <tick>` is a **test-only** hook (not in `--help`): the game throws on the game loop once the simulation reaches that tick, so the crash handler
can be seen working. Nothing in a playtest uses it.

```bash
# a lesson with autosave on (the saves are printed); the session folder is the scratch folder, so the user's own sessions are not touched
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session-dir /tmp/x05/lesson --lesson-run first-words --leave --wait 2 --screenshot /tmp/x05/lesson.png --frames 30
# the autosave loads: the objective and the lesson are where the save left them
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session-dir /tmp/x05/load --load /tmp/x05/lesson/autosave.save --screenshot /tmp/x05/load.png --frames 30
# a forced crash: the report and crash.save are written and the exit code is 1
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session-dir /tmp/x05/crash --debug-crash-after 600 --screenshot /tmp/x05/crash.png --frames 900; echo "exit $?"
```
