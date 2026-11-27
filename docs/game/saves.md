# Saves

How saves survive content updates, and autosave with the crash report. Saving and loading in a run are in [running.md](running.md).

## Saves survive content updates (X01)

Every content change used to make every save unloadable (the S10 rule: a save refuses another bundle hash), so a player lost all progress when the game
updated. Now a `--load` of a save made from another bundle **migrates** it (`SaveMigration`, engine-free, in `Vocaterra.Game/Save`) unless the run is strict.

- **Kept where the id still exists in this bundle:** the learner's record (history, word rows), the settings and the session number, the word records
  (by lemma), the carded nouns, the phrase records (one per sheet row, by id), the objective's counts (by step key), each NPC's conversation state and its
  learner-model skills (`word.<noun>` and `phrase.<id>` skills of content that is gone are dropped), the tips, the arrow, the inventory (items are rows of
  the bundle's `game/items.json`, saved by name since version 9: a slot whose item this bundle lacks becomes an empty slot) and the lessons. A **lesson** is mapped by step id: a completed lesson stays completed (and `EverCompleted` stays) with the
  outcomes of the steps that still exist; a lesson in progress keeps the longest run of its steps, from the first, that are all among the finished ones,
  goes on at the next step, and does again a finished step that follows one the lesson has not finished (the tries on the step in progress reset when
  the step changed). A lesson that is gone is dropped, and so is an active mark or a replay that named it.
- **Dropped and logged:** records whose phrase row, noun, lesson, step, tip or step key is not in this bundle. **New** content (a phrase row, a lesson, a
  noun) starts empty, as in a fresh game.
- **World edits are applied by position.** The save holds whole chunks of the world it was made from, keyed by chunk coordinates (since version 9), so a grid that
  changed size still maps by position. A saved cell is applied only if it is a change the player can make to the cell the new scene has: a gathered block
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

## Format version 12 (the current one)

The current version is 12, and it is the only one read: a save of version 11 or older is refused with "The save is format version N; the oldest this game loads is 12 (it writes 12), and older saves are not migrated" (`SaveGame.Parse`; the
game is unreleased, so there is no migration code, and the code that read versions 1 to 10 is gone). The fixture the tests read is `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v12.json`: a scripted game's own output at Luna's lit fire (tick 4258), its items by name, its 30 slots, nothing worn and no wear, the biomes
series' world edits; the tests read it with the current bundle's hash, so a content edit leaves it alone, and the round trips of worn items, worn bags and wear are `WornSaveTests` and `WearSaveTests`. What version 12 holds, by area:

- **`states` on a `ChunkEdit` (K01, the only change of 12).** The per-cell state byte of the building series (`VoxelChunk.States`: a lazily allocated `byte[4096]`, absent while every state in the chunk is 0). A chunk that holds a state saves it as `"states"`: runs `{"start", "length", "state"}` of equal non-zero state bytes (1 to 255) in storage order, the same shape as `water`; a cell no run covers holds state 0, and the entry is left out of the file for a chunk with no state (every chunk of a world nobody has built a stateful block in, which is every world until the K02 blocks). The load refuses a run that is out of order, overlaps, leaves the chunk or lies on air (`VoxelGridDiff.Apply`). A state means nothing on its own; the block's id says how to read it. A chunk's digest takes the state bytes only while it holds one, so the chunk digests and the `stateDigest` of every world with no state are what they were at 11. A state-only change (the same block, a new state) makes the chunk a changed chunk. A save across bundles (`SaveMigration`) applies blocks by position and carries no state.
- **The items are saved by name.** `inventory[i].item` (and each stack of the belongings chest, `systems.chest.stacks[i].item`) is the item's stable `name` from `game/items.json` (`"wood"`, `"water-bottle"`, `"camp-kit"`), never an enum name or an index. **An empty slot is `{"item": "", "count": 0}`.** `SlotSave.Of`
  and `SlotSave.ToStack` translate with the bundle's `IItemCatalogue`; a slot whose name the catalogue lacks (a save of content that lost the item) loads as an empty slot, and a migration reports it among the dropped inventory slots. The state digest still reads the item's numeric id.
- **`inventory` holds the active slots only.** As many entries as the bag's active count: `bagSlots` of `game/equipment.json` (30) plus the `capacity` of the worn bag, so 30, 40 or 50. The simulation state (`VoxelSimulationState.Slots`) is the same list, and `SaveGame.Restore` refuses a save whose list does not match the count its worn items give.
- **`worn`, optional.** The worn items, one entry per worn slot in the data's order: `{"group": "clothing", "slot": "body", "item": "shirt"}`, the group's `id` and the slot's name from `equipment.json` (a counted slot's name is its wear kind and number: `accessory2`) and the item's `name`. Nothing is an
  index, so reordering the groups or the items does not scramble a save. The entry is left out while nothing is worn. A group, slot or item the data lacks loads as nothing (`WornSave.ToStacks`); across bundles `SaveMigration` reports them among the dropped `worn items`, resizes the bag to what the kept worn items give (a bag the bundle lost
  shrinks it to the base) and reports the stacks in slots that no longer exist among the dropped `inventory slots`.
- **`wear`, optional, on an `inventory` entry and on a `worn` entry.** `{"item": "axe", "count": 1, "wear": 40}` and `{"group": "clothing", "slot": "body", "item": "shirt", "wear": 12}`: how worn the item is, a whole number from 0 up to the item's `durability.max`. A new or never-worn item has no `wear` key. The load keeps the wear only for an item that has a
  durability and clamps it into 0 to its maximum (`Save/Wears`), so a save made against other content still loads. A worn-out item is saved with its wear equal to its maximum and loads worn out.
- **`systems.temperature`, optional: `{"felt"}`.** How far the temperature the player feels has got towards the place's (`PlayerTemperature`'s lag), in millionths of a tenth of a degree. It is state because the band the felt value falls in slows the walk and quickens the thirst, so a load that took the place's value at once would continue
  differently from the game it was saved from (`SaveGameTests.ALoadedGameContinuesExactlyAsTheOriginalWouldInTheNextSession`). Left out when nothing lags; a save without it starts at the place's value. The place's own temperature and the band's line are never saved: they come from the stored time, the weather and the world. `--temperature` is a launch flag and is never saved.
- **The biomes series' fields** (they share the version): `chunks`, one `ChunkEdit` per changed chunk by chunk coordinate (the next section), and `passOpen`, written as `true` only while the pass through the east ring is open and its lock is in force (`session.Pass.IsOpen && session.Pass.Locked`), left out otherwise, so a save from before the pass opened loads with it closed
  (`SaveGame.Restore` puts the pass back and the edge wall follows; the rockfall's blocks are in the world edits).
- **The state digest** hashes the active slots (30 with no bag) and says the active count outright once a bag has changed it; it then adds the worn slots, one entry (container, slot, item) each and nothing when none is worn; it appends one entry (container, slot, wear) for each bag slot and worn slot that has any wear and nothing when none has; and the held
  stack and the trash only when they hold something. So a run with no gear, no wear and nothing moved by hand has the digest of a run without those systems, apart from the 30-slot hash.
- **A save of this bundle that names what the bundle lacks (I10b, no version change).** A bag slot or a worn entry whose item (or worn slot) the bundle lacks loads empty, and no longer fails the digest: the digest was taken with the
  item, so a load that put anything right reports the digest instead of requiring it (as a load across bundles does) and logs one `save_repaired` event for each change in the session log (the detail names the slot and the reason).
  The same happens to a worn item whose slot does not take it (the fit check: the slot's wear kind and its group's tags, `EquipmentData.Fits`): it goes to the first empty bag slot, and the log line says so (and says when the bag has no
  room). A bag slot with an item and no count is refused, by a message that names the slot (`bag slot 4 holds apple but gives no count for it`); a save with nothing to put right that does not match its digest is refused as before.
  The refusals read as sentences: the .NET parameter name no longer shows (`The save does not restore. The inventory has 30 slots, not the 40 its worn items give.`).
- **Not saved: the held stack and the trash.** Moving items by hand keeps the stack at the cursor and the trash in the simulation state and its digest, but `SaveGame.Capture` first returns the held stack to its slot or the bag (`ReturnHeld`), so a save always has nothing held, and refuses (as a session not at rest does) only when the bag is full and the stack has nowhere to go.
  The trash's item is not written: a loaded game starts with an empty trash, and `Restore` clears both.

## The world edits by chunk coordinate (B05)

A save of the world holds only what the player changed, and the world itself is rebuilt, not stored. The parts of the world in a save (since the format change of the biomes series; version 12 now):

- **`chunks`: one `ChunkEdit` per changed chunk**, `{"x", "y", "z", "runs", "water", "states"}`, sorted by layer (`y`), then row (`z`), then column (`x`). The coordinates are chunk
  coordinates (block position divided by 16, rounded down), so `y` is negative below block 0. `runs` is the chunk's 4 096 blocks, run-length encoded in storage order
  (x fastest, then z, then y); `water` holds the cells whose level is not source, as runs `{"start", "length", "level"}`, and is left out when there are none; `states` holds the cells whose state byte is not 0 the same way (`{"start", "length", "state"}`, K01). The dense
  chunk index and the `world` entry (the chunk counts, X01) are gone, and so is the second copy of the world the first save used to build (0.6 to 0.7 s and about 38 MB).
- **A changed chunk** is one whose content differs from what it held just before its first write (`VoxelChunk.IsChanged`: each chunk records its own pristine digest lazily, and
  `VoxelGrid.SealPristine()` at the end of the scene build makes the built world the pristine one). A block gathered and put back is not a change.
- **The state digest** (`stateDigest`, `WorldStateDigest`) hashes, in order: the header (the world seed, the region template's digest, `ProceduralGenerator.Version`, and the
  SHA-256 of the world-generation files, `game/world.json`, `game/biomes.json` and `game/underground.json` where the bundle has it, so editing a phrase does not move it), the number
  of changed chunks, and for each changed chunk, in the same order, its three coordinates and its own digest; then the tick, the body, the hotbar and the inventory as before. Its cost
  grows with the number of changed chunks, not with the size of the world, and it does not depend on which chunks are resident. `hostStateHash`, the save's check and the start-up line use it.
- **Restoring** builds the world as a new game does (the scene builder for the bounded valley), then writes each saved chunk over it cell by cell (`VoxelGridDiff.Apply`); a grid that
  generates on read (`IChunkGenerator`) regenerates a chunk the first time a write touches it, so a streamed world needs no code of its own here (B06 supplies the chunk source).
  The restored world's state digest must equal the save's, or the save is refused. A save across bundles applies the edits by position as above.

**B14 changes no save format.** The state digest's world-generation hash covers `game/biomes.json` without the `ambient` field of its rows (`WorldStateHeader.WithoutPresentation`, which cuts those properties out of the bytes before hashing), so the sound's data is editable without refusing a save: a file with no `ambient` is hashed as it stands, and a change to any other part of a row still moves the digest. The cloud layer's `cloudAltitude` is in `game/weather.json`, which the digest never read. A save records neither: they are the bundle's, not the player's.

## Format version 8: the `systems` map, the needs, the water and the sleep entry (Y02)

Version 8 added the rule that only the current version is read. The game is unreleased, so a save of an older version is **refused** (`SaveGame.Parse`) and nothing migrates it: the code that read
versions 1 to 7 is gone (the branches on the version number, the version 2 word rows, the first-words split, the dense chunk index (B05)). The save the tests read is `save-v12.json` (above). The weather's volume (E09) is `settings.sound.weather` (0 to 1, the default 0.6; a save from before E09 has no field and takes the default, with no version change).


**What version 8's `systems` map holds (checked against `SystemsSave` in `SaveDocument.cs`, Y13).** Every entry is optional and left out when it is the new game's state, so the version stayed 8 as each was added:
`needs` (the five meters and two timers, N01), `chest` (the belongings a death left, N04), `bushes` (berry bushes picked since the last night, N06), `gifts` (today's gifts, N06), `planted` (the bushes planted at the
camp or on the owned plot that still stand, N08), `procedures` (what the learner knows of the stations, W06), `sleep` (the inn's lodging, the place last slept at, the places made in play, the camp and the kit owed, Y02 and Y12),
`atmosphere` (the day clock's offset and held hour, the weather's state, E01 and E04), `land` (the pending and the owned plot, Y09 to Y11) and `temperature` (the felt temperature, version 11). The sections below describe each; the settlement series' are
`sleep` (with `camp` and `kitOwed` inside it) and `land`, the weather series' is `atmosphere`.

**How the fixture is made:** a scripted game run, not by hand, so it is the game's own output. From the repository root, after a build (the camp route of `scripts/scripted-runs.sh`'s
`CAMP`, then a talk, then Luna's fire):

```bash
dotnet run --project src/Vocaterra.Game -- --session-dir <scratch> --needs off --seed 42 --mute --walk-to 176,186 <CAMP steps> --walk-to 177,185 --craft antorcha \
  --talk shopkeeper --say "quiero agua, por favor" --leave --teleport 176,188 --wait 1 --screenshot <png> --frames 30 --save <file>
```

The save has the player standing at Luna's lit fire (Y08: the mountain ring is gone; tick 4258), one conversation page, no lodging and no sleep recorded (so no `sleep` entry). No test pins the fixture's bundle hash
(B29c): the tests read it with the current bundle's hash, so a content edit leaves it alone.

**The `sleep` entry (Y02).** `systems.sleep` keeps what a sleep (a save restored in the same run) and a load must not lose, all optional, the entry absent when nothing is set:
`lodgingGiven` (the innkeeper has given the room; left out when false; set in play since Y05 by the host action `grant_lodging`, once and for good, and by the dev aid `--lodging`; a sleep, a load and a migration keep it), `last` (the place last slept at: `{"id"}` and, for a place made in play, its cell `x`, `y`, `z`) and
`places` (the places made in play, `SleepPlaces.Add`: `id`, `x`, `y`, `z`, `kind`, `radiusBlocks`, `heightBlocks`, `access` and `point` when it has one). The authored places are the bundle's
and are not saved. `RestoreParts` puts the places made in play back first, before the objective's sleep line and the arrow ask `SleepPlaces`.

- **The last place slept** is written by `SaveGame.Sleep` after `RestoreParts` (the save a sleep restores was taken before the sleep): a sleep at a usable place records it; a sleep at
  none (the `--sleep` aid away from a place) and a collapse leave it as it was. A death wakes the player at the last place while `SleepPlaces` still has it, else at the place
  `death.wakeAt` names.
- **The camp (Y12).** `systems.sleep.camp` is `{"x", "z", "ground", "facing"}`: the west-north corner of the camp's footprint on the owned plot, the height of the ground under it and the side its front faces
  (`north`, `east`, `south`, `west`); written only while a camp stands (the kit's use, `VoxelSession.UseCampKit`, sets it; packing the camp up clears it). The camp's cells and blocks are not saved: they are the scene layout's `camp` stamp
  turned and laid on that corner (`CampPlan`, the scene build's own turning), so a camp is never rediscovered from `Scene.Points` or the world. Its two sleep places are in `places` with the ids `camp.bed` (kind `bed`, the
  bedroll's head cell) and `camp.fire` (kind `fire`, the ring's middle; usable while a torch is on the ring, read from the world), each with the extra field `wake` (`{"x", "y", "z"}`, the cell a death wakes the player on).
  A load or a sleep puts the camp back only if every block of it still stands; otherwise its two places are dropped, the place last slept falls back (the inn's bed with lodging, else Luna's fire) and the camp kit comes back to the bag
  once. A bag with no room leaves the kit owed: `systems.sleep.kitOwed` is `true` (left out otherwise) and the kit is given the first tick the bag has room. The checklist `my-land`'s steps `camp` and `fire` are saved with the other counts and a load also reads them from the camp
  (`camp-placed`) and from the ring in the world (`camp-fire-lit`): a step done in either stays done, so packing the camp up after the checklist is done does not reopen it.
- **Across bundles** (`SaveMigration.Adapt`) the entry is kept: the lodging and the places made in play stay (a place whose cell is outside the new world is dropped), and a last place that
  is neither a place of the new bundle nor a place kept is let go (reported as `place last slept at`).

**The `temperature` entry** is described under the current version, above (`systems.temperature`: `{"felt"}`; optional, so a save without it starts at the place's value).

**The `land` entry (Y09).** `systems.land` keeps the player's plots, both optional, the entry absent when neither exists: `pending` (the plot marked with a stake and not yet bought, Y10) and `owned` (the plot bought from
the mayor, Y11), each `{"x", "z", "width", "depth"}`: the west-north corner and the size in blocks, along the world's axes (a plot is columns, not a volume). One plot of each kind at most. What is *not* saved is the rules: the
protected regions and the meadows come from `game/land.json` and the scene (`LandRules` builds its mask when the session starts), so an edit of the file changes where a column is protected for every save. A load and a sleep's restore put the
plots back as saved, with no check (a plot bought stays bought); a plot with no size or outside the world is refused (`SaveException`). The entry is optional, so the version stays 8 and no migration is needed; across bundles
(`SaveMigration.Adapt`) the entry is kept as it is. The dev aid `--own-plot x,z` (with `--dev`) makes a save carry an owned plot from the start. Y11 adds nothing to the entry: the purchase moves the pending plot to `owned` and puts the camp kit (`camp-kit`) in the slots and the coins out; the
checklist `my-land`'s `buy` step has the restore hook `plot-owned` and is worked out again from `owned`, its `stake` step counts the bag and one more once the plot is owned (the mayor keeps the stake at the purchase, Y15: the bag holds none then), and the mayor's agent state is in the NPC records as any NPC's.

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
  not held, the weather still in its first spell), so a save of a game that never slept or changed weather is byte for byte what it was (an older fixture loads as it is); it is written when either part is not a new game's. The state digest adds nothing for the default state, and from the first change of weather
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
  every save lists the changed chunks (B05: no second copy of the world is built, so the first save has no 0.7 s hitch).
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
