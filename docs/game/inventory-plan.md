# Inventory and equipment: the I series

A plan written on 2026-11-15 at the owner's request, from a read of `main` at `0db529a1` (after PR #65, the settlement series). Nothing
was built or run. Its goal is **a proper inventory**: a bag the player can rearrange, armour, clothing, accessory, off-hand, cosmetic
and backpack slots, gear that does something, and gear drawn on the player, built so that a later item, slot or effect is a row of data
and not a new system.

**Status (2026-11-15):** in progress. The owner accepted D-game-35 as written and answered its two questions (section 5), then said
to build ("Work on the plan", the same day); I00 registered the series in the game plan's section 4.15; the owner then said to start ("you can start the work"), one agent from this series at a time while he games; I01 is done. It is built on `feature/inventory`, cut from `main` at `0db529a1`, in the worktree `.claude/worktrees/inventory` (the
standing rule: new work in a new worktree from `main`). Milestone branches `I<nn>-<name>`, gated into `feature/inventory`; commit
prefix `I<nn>:`. As with every feature branch, nothing is pushed to `main`; the owner merges by pull request.

## 1. The request

> Planning out adding in a proper inventory system to the player. This will include armor slots, clothing slots, accessory slots and a
> backpack slot. Kind of like minecraft.

The owner pointed to the Core Systems Design Document
([`design/sources/Vocaterra_Core_Systems_Design_Document.docx`](design/sources/Vocaterra_Core_Systems_Design_Document.docx), 2026-10-30),
whose section "Inventory and equipment" already sketches the target: a 10-slot hotbar and a backpack grid beside an equipment panel
(four armour slots, an off-hand, three to five accessories, a cosmetic slot per armour and accessory slot), stacks of one for gear, extra
backpack rows from bags, click to pick up, right-click to split, shift-click to move, a sort button and a trash slot, and every item shown
by its Spanish name with its article. The owner's eight answers of 2026-11-15 (section 5) settle where this plan departs from that
document.

Together with the owner's standing rule: the code follows `docs/process/code-guidelines.md` (single responsibility, components,
composition, logic in `Vocaterra.Game.Core` and drawing in `Vocaterra.Game`, Given_When_Then tests in folders that mirror the source), and
the armour and temperature code in particular is open to later causes and sources without edits (the owner: "the code for it should be
adaptable and follow solid principles").

## 2. What exists today (on `main`)

- **The bag** (`Items/Inventory.cs`): a fixed array of `SlotCount = 40` `ItemStack(ItemId, Count)` slots, the first `HotbarSize = 10`
  the hotbar. `Add` fills existing stacks, then empty slots; `Remove` takes from the lowest slot first; `CanExchange` checks a trade.
  There is no move, swap, split, merge or sort. The selected slot lives in `Simulation/VoxelSimulation.cs`; the simulation's state,
  restore and digest assume 40 slots.
- **Items are code** (`Items/ItemDefinition.cs`): `enum ItemId : ushort` with 14 members (Wood, Torch, Berry, Bottle, WaterBottle, Bread,
  Book, Coin, Apple, Axe, RoastApple, Flour, Stake, CampKit) and a hardcoded table of `ItemDefinition(Id, Name, MaxStack, ToolSpec?)`:
  99 to a stack, 1 for the book, the axe, the stake and the camp kit. `content/schema/nouns.schema.json` lists the item names as an enum;
  `Content/GameDataValidator.cs` and `Npc/HostEventTable.cs` (`ItemNamed`) read the static `ItemDefinition.All`. The activities plan's
  W12 (items as data) and W13 (durability) are planned and not started.
- **The inventory screen is a view** (`Items/InventoryPanel.cs`, `InventoryScreen.cs`; drawn by `UI/Inventory/InventoryLayer.cs` and
  `InventoryLayout.cs`, four rows of ten; the hotbar by `UI/Hud/HotbarLayer.cs`). I or E opens it; Esc, I or E closes it; it handles no
  click. `VoxelGame.cs` routes clicks only to the crafting panel, the station panel and the dialogue; the crafting panel's click handler
  is the model to copy. Opening the screen marks the carried nouns as read.
- **Icons are drawn in code** (`Graphics/Blocks/BlockArt.cs`, `BlockAtlasLayout.IconFor(ItemId)`); held items are extruded from them
  (`Graphics/Figures/HeldArm.cs`, `ItemModels.cs`). First person draws the arm and the held item (`ArmPresenter`); the third-person
  (shoulder) view draws `FigureDirector.Avatar()`, a `PlayerAvatar` record of position, yaw, pitch and walk phase, with no appearance.
- **Needs and damage** (D-game-24): five meters. The walk speed is set once a tick in `VoxelSession.PrepareTick`
  (`Session/VoxelSession.Eat.cs`) as `Simulation.WalkPermille` from the needs' walk factor and the sprint; the drains share one
  `PlayerNeeds.RateFactor`; the stamina maximum comes from `needs.json` less the stage penalties. The only damage is drowning, through
  `Water/IPlayerHealth.Damage(int, DamageCause)` with `enum DamageCause { Drowning }`.
- **Death** (`Session/VoxelSession.Death.cs`): every stack with `MaxStack > 1` goes to a belongings chest; stacks of one stay. The
  `needs.json` rule `death.keepInBag: "stacksOfOne"` is parsed into `DeathSpec.KeepInBag` and never read.
- **Light** is baked: lanterns' glow is its own additive mesh (E02a), and a held torch lights only the first-person arm (`ArmLight`).
  Nothing lights the world round the player.
- **The atmosphere** (D-game-31): `AtmosphereSnapshot` channels (cloud cover, sunlight, fog, wind, precipitation) and the daylight; no
  temperature. No file under `src/` names a weather kind's id (two tests). A lit fire is found by `Needs/FireRings.NearLit`, overhead
  cover by `Voxel/OverheadCover`.
- **Shops are data** (`game/host-events.json`): `{ "kind": "sell", "slot": "item", "prices": { "harina": 1 } }` sells what the learner
  names; `buy` buys one item. An NPC understands a noun that is in the lexicon, in `game/nouns.json` and allowed by its answer sets.
  Recipes (`game/recipes.json`) name their items by noun, at two stations: `mesa` and `fuego`.
- **Saves** are at version 8 (D-game-32). The bag is 40 `SlotSave(Item, Count)` entries; the belongings chest sits under `systems`.
- **Words:** of the gear words, `camisa`, `zapato`, `sombrero`, `ropa`, `bolsa`, `cabeza` and `pie` are on the A1 list and `anillo`,
  `pulsera`, `casco`, `cuerpo`, `pierna`, `vestido` and `calor` on the A2 list; `chaqueta`, `abrigo`, `pantalones`, `bota`, `gorro`,
  `mochila`, `escudo`, `tela`, `cuero`, `hierro`, `cinturón`, `collar`, `amuleto` and `farol` are on neither. "Tu bolsa está llena" already
  uses `bolsa` for the whole inventory.
- **Earlier plans and decisions:** D-game-23 (c), proposed, says "no combat; gear is for places" (W26: "Clothes with equipment slots;
  cold on the mountain slows walking, never harms; a lantern; the tailor after a voice trial"). The biomes plan (D-game-34, decided
  2026-11-15) shows in its section 3.3 how a temperature system plugs into its climate (`IPlaceInfo.TemperatureAt`), and its ores have
  no items in that series.

## 3. Design

### 3.1 Principles

- **A kind is a row of data** (D-game-21, as the weather's kinds are): items, slot groups, stat keys' sources, damage causes' rules and
  temperature terms' numbers live in `content/game-v1/game/`. Code names behaviours (the bottle fills, the book opens, a bag adds
  slots, armour mitigates), never a particular item's id outside the behavioural enum members.
- **Seams before features.** Each system is an interface with one first implementation, so later combat, biome climate and food buffs
  add an implementation, not an edit (section 3.5, 3.6, 3.7).
- **Inventory edits are simulation commands.** Moving, splitting, equipping, sorting and trashing are `VoxelCommand`s applied in the
  60 Hz tick, so scripted runs replay them and the digest covers them. Panels only queue commands.
- **Logic in Core, drawing in Game.** `Vocaterra.Game.Core` holds the bag, the equipment, the stats, the temperature and the rules;
  `Vocaterra.Game` draws the panel, the avatar's layers, the sleeve and the light.

### 3.2 Items as data (I01, W12 taken over)

`game/items.json` and `content/schema/items.schema.json`: per item a stable `name` (the key saves and data use), a stable numeric `id`
(the 14 enum values kept; data-only items from 100, never reused), `stack`, `icon` (an atlas key, with a fallback tile the validator
reports), `held` (how the hand shows it), `tags` and `tool` (today's `ToolSpec`). Later milestones add `wear`, `stats`, `look`,
`capacity` and `durability` (the sections below).

`ItemDefinition`'s static table becomes an `IItemCatalogue` (look up by id, name and noun) loaded once by `GameData` and passed to what
needs it: `Inventory`, `VoxelSimulation`, `GameDataValidator`, `HostEventTable`, `NounCatalog`, the icons. The enum keeps only the members
code refers to by behaviour; the loader checks each against its data row. `nouns.schema.json` stops listing item names, and the validator
checks every item a noun, a recipe, a shop row, a gift or a planting names. **Done when** nothing in play changes but the save's shape.

### 3.3 The bag and the backpack slot (I03b)

The bag becomes **the hotbar's 10 slots plus 20** (base 30). The backpack slot takes a bag item whose `capacity` adds slots: the small bag
(`el saco`) 10, the large bag (`la mochila`) 20, so at most 50. The array is sized to the maximum and the active count follows the worn bag. A bag
cannot come off while any of its slots holds an item (the panel says why); swapping one bag for another is allowed when the slots the
swap would drop are empty. The simulation's state and digest carry the active count.

### 3.4 Equipment: slot groups and layers (I03a, I03b)

`game/equipment.json` lists the slot groups; each has an id, a layer, a count, the item tags it accepts and a Spanish label noun.

| Group | Slots | Layer | Accepts | Label |
| --- | --- | --- | --- | --- |
| Clothing | head, body, legs, feet | 1 (under) | `clothing` | la cabeza, el cuerpo, las piernas, los pies |
| Armour | head, body, legs, feet | 2 (over) | `armour` | the same |
| Cosmetic | head, body, legs, feet | 3 (looks only) | `clothing` or `armour` | the same |
| Accessories | 3 | none | `accessory`, no duplicates | (accessories) |
| Off-hand | 1 | none | `offhand`: shield, torch, lantern | (the other hand) |
| Backpack | 1 | none | `bag` | (the back) |

An item's `wear` field names the slot it fits (`head`, `body`, `legs`, `feet`, `accessory`, `offhand`, `back`). Cosmetic slots cover the
four body slots only, because accessories are not drawn; a cosmetic item gives no stats. The main hand stays the selected hotbar slot, as
in Minecraft.

### 3.5 Stats (I03a, I05a)

A `StatSheet` is built each tick from `IStatContributor`s through a `StatBuilder` (sums and factors per key). Stat keys are open strings
in data; the first set and their consumers:

| Key | From | Read by |
| --- | --- | --- |
| `armour` | armour pieces, the shield | the damage pipeline (section 3.6) |
| `warmth` | clothing (and armour, a little, if data says) | the temperature system (section 3.7) |
| `walkSpeed` | an accessory | `PrepareTick`: a factor on `WalkPermille` beside the needs' and the sprint's |
| `hungerDrain`, `thirstDrain` | accessories | `PlayerNeeds`: `RateFactor` becomes a factor per need |
| `maxStamina` | an accessory | `PlayerNeeds`: a bonus where the stage penalty is taken |
| `light` | an accessory, the lantern or torch in the off-hand | the player light (section 3.8) |

Equipment is the first contributor. W15's meals and buffs plug in later as timed contributors, with no change to the sheet or its
readers. A worn-out item contributes nothing (section 3.12).

### 3.6 Armour and damage (I05a)

Armour gives a rating for combat, which a later series adds; today it reduces nothing (the owner: "Armor does not reduce drowning
damage, lava or cold currently. However we may add that in the future"). `IPlayerHealth.Damage` is decorated by an ordered list of
`IDamageModifier`s. The cause becomes a data id (`drowning` maps from today's enum), and `game/damage.json` says which modifiers apply to
which cause: today armour applies to none. Combat later adds a cause and a row; letting armour soften falls, lava or cold later is a row,
not code. The rating shows in the panel's stats line (`armadura`).

### 3.7 Temperature and warmth (I06)

A `Core/Temperature/PlaceTemperature` sums `ITemperatureTerm`s into one fixed-point value for where the player stands, as the biomes plan's
section 3.3 foresees:

- **Day:** a swing from the daylight (`day.json`'s curve; colder at night).
- **Weather:** cooling from the `AtmosphereSnapshot`'s channels (cloud cover, precipitation, wind), with coefficients in
  `game/temperature.json`, never from a kind's id.
- **Shelter:** a term from `OverheadCover` (under a roof is warmer in the rain).
- **Fire:** a bonus near a lit fire (`FireRings.NearLit`), with its own radius.
- **Later, with no edit here:** the biomes series' climate (`IPlaceInfo.TemperatureAt`, with its lapse over altitude) and its
  underground pull register as further terms.

The player's **warmth** is the `warmth` stat. Feeling is `temperature + warmth` against `temperature.json`'s bands (warm, cool, cold,
very cold); a cold band sets a walk factor in `PrepareTick` and shows a cold icon on the HUD, and the player's own line «Tengo frío»
appears once when the band first turns cold, as the needs warn. Cold never harms (W26's rule, the owner's answer 2). Until I03a lands
the warmth comes through a zero source. `--temperature <n>` holds the value for a run, as `--time` and `--weather` do.

### 3.8 Light round the player (I05b)

There is no dynamic world light today, so this is a spike with its own review: a light at the player (strength and radius from the `light`
stat) passed to the world's effects at night, without rebuilding chunks every frame. The first try is a uniform-style term on the effects
the world is drawn with, falling back to a small additive mesh like the lanterns' glow (E02a) if that is the cheaper path. The frame time
on the low preset is measured.

### 3.9 Interaction (I02)

`ISlotContainer` gives one address scheme to the bag, the equipment and the trash; `InventoryOps` does the moves over it, so a later chest
(W24) reuses it. The panel holds one **held stack** (the cursor):

- **Left click:** pick up a stack, put it down, swap with what is there, or merge into a stack of the same item.
- **Right click:** pick up half; with a held stack, put one down.
- **Shift-click:** quick move: a wearable to its slot, a worn item back to the bag, the hotbar to the pack and back.
- **Sort:** catalogue order, then count; the hotbar is left alone.
- **Trash:** one slot; it keeps its last item until another replaces it.
- **Closing the panel** returns the held stack to the slot it came from, or the first free slot.

Each is a `VoxelCommand`. Equip rules (the slot accepts the item, no duplicate accessories, the bag rule) are checked in Core, so a refused
move leaves everything as it was and the panel shows why.

### 3.10 The panel (I04)

The inventory screen becomes the equipment panel: the paper doll on the left (three columns, clothing, armour and cosmetic, by the four
body rows labelled in Spanish; the accessories, the off-hand and the backpack below), the bag grid on the right (three to five rows as the
bag allows; the hotbar row apart), a stats line (`armadura`, `calor`, and the accessories' effects), the sort button and the trash slot.
Items keep today's language layer: the Spanish name with its article from `NounCatalog`, English on hover until the learner model knows
the word, the words marked read when the panel opens. A compact layout serves small windows.

### 3.11 Gear on the player (I09)

`PlayerAvatar` gains an appearance: for each body part, the colours of what shows, chosen cosmetic first, then armour, then clothing, from
the items' `look`. The figure mesher draws them as slightly inflated boxes over the body (I09 checks what `FigureMesher` supports first).
The worn bag shows on the back and the off-hand item in the left hand, in both views. In first person the arm takes the body slot's sleeve
colour.

### 3.12 Durability and repair (I07, W13 taken over)

`ItemStack` gains `Wear`, meaningful for items with `durability` (which stack to 1). An `IWearSource` per item says what wears it: **use**
for tools (W13's rule: a gather adds wear), **time worn** for clothes, **damage absorbed** for armour (none until combat exists). At its
durability an item is worn out: a tool works at bare hands' speed and gear gives no stats; nothing is ever lost or destroyed. A repair
recipe at the workbench restores it with the item's material (`tela`, `cuero`, `hierro`). The hotbar and the panel draw a wear bar.
`tool_worn` fires once. W14 (the axe's handle with Domingo) stays in the W series and builds on this.

### 3.13 Getting gear: recipes and shops (I08a, I08b)

Every gear item is **both craftable and buyable** (the owner's answer 5). Clothes, bags, accessories and armour are crafted at the
workbench (`mesa`; there is no forge station); Pablo (the shopkeeper) sells clothes, bags, accessories, `tela` and `cuero`, and Domingo (the
smith) sells armour, the shield, the lantern and `hierro`, each by naming it («Quiero una camisa»), through `sell` rows with `prices`. The
player starts the game wearing basic clothes.

A draft list (the owner settles names, numbers and prices at stop 3, section 4):

| Group | Items (draft) |
| --- | --- |
| Materials | `la tela` (cloth), `el cuero` (leather), `el hierro` (iron); bought, since nothing in the world gives them yet |
| Clothing | head: `el sombrero`, `el gorro`; body: `la camisa`, `la chaqueta`, `el abrigo`; legs: `los pantalones`; feet: `los zapatos`, `las botas` |
| Armour | two tiers, leather and iron: `el casco`, `la armadura`, a leg piece (`las grebas` or `las rodilleras`, to choose) and armoured boots (named so they do not clash with the clothes' `botas`) |
| Accessories | `el anillo` (max stamina), `la pulsera` (walk speed), `el cinturón` (slower hunger), `el collar` (slower thirst), `el amuleto` (light at night) |
| Off-hand | `el escudo` (armour), `la antorcha` (today's torch, light), `el farol` (lantern, more light) |
| Bags | `el saco` (10 slots; answer 11) and `la mochila` (20) |

The words on neither list join the marked block of world words at the end of `cefr/a1.txt`, each with a one-line reason, the rule of
`content/README.md` for a noun the player handles (as `harina`, `estaca` and `terreno` did); lessons and phrases stay at the A2 ceiling.

### 3.14 Death, saves and determinism

- **Death:** worn items, the worn bag and stacks of one stay with the player; the bag's stackables go to the belongings chest, as today.
  `VoxelSession.Death.cs` reads `DeathSpec.KeepInBag` in place of its hardcoded `MaxStack > 1`.
- **Saves:** every milestone that changes the save's shape (I01, I03b, I07) takes the next free version when it merges, refuses older saves
  and records the fixtures again; no migration (the owner's rule of 2026-11-04). `main` is at version 8, and the biomes series takes 9 at
  B05, so the numbers are fixed at merge time.
- **Determinism:** the equipment, the active bag size and the wear are simulation state and part of the digest; temperature reads only the
  stored time, the weather state and the world; the light and the avatar are drawing only.

## 4. Milestones

Models follow the owner's rule (2026-11-06): Haiku 5.5 for small tasks, Sonnet 5.5 at medium effort for medium ones, at high for large or
design-heavy ones; content milestones follow the same rule (D-game-22). At most two building agents at a time **on the machine**, so the
biomes session's agents count. Every milestone: `dotnet build -warnaserror`, the tests, `dotnet format --verify-no-changes` (all through
`scripts/limited.sh`), tests named Given_When_Then in folders that mirror the source, `ArchitectureRulesTests` green, then the merge gate.

| Id | Milestone | Size | Model | Depends on |
|---|---|---|---|---|
| I00 | **Register the series** (documents only): the game plan's section 4 rows; the activities plan's notes (W12 to I01, W13 to I07, W26's clothes and cold to I06 and I08; the tailor stays in W26); D-game-23 (c) marked as amended by D-game-35; open items. | S | Haiku | acceptance |
| I01 | **Items as data:** `items.json` and its schema, `IItemCatalogue` passed in place of the static table, icons by key with a fallback, the validator's item checks, `nouns.schema.json` without the enum, the save's next version. Done when nothing in play changes but the save's shape. | L | Sonnet, high | I00 |
| I06 | **Temperature and cold:** `PlaceTemperature` and its terms (day, weather channels, shelter, fire), `temperature.json` and its schema, the cold bands and walk factor in `PrepareTick`, the HUD's cold icon, «Tengo frío», `--temperature`, warmth through a seam (zero until I03a). | M | Sonnet, high | I00 |
| I03a | **The equipment core:** `equipment.json` and its schema, `Equipment`, the items' `wear`, `stats` and `look`, the equip rules, `StatSheet`, `StatBuilder` and `IStatContributor`, the equip and unequip commands. | L | Sonnet, high | I01 |
| I03b | **The bag and the rest of the slots:** base 30 plus the worn bag, the bag rule, the off-hand and cosmetic slots, the death rule, the state and digest, the save's next version; the tests that assumed 40 slots recorded again. | M | Sonnet, high | I03a |
| I02 | **Interaction:** `ISlotContainer`, `InventoryOps`, the held stack, swap, merge, split, shift-click, sort and trash as commands; click routing in `VoxelGame.cs`. | M | Sonnet, medium | I03b |
| I09 | **Gear on the player:** the avatar's appearance and layers, the bag on the back, the off-hand item in both views, the sleeve's colour; screenshots. | M | Sonnet, medium | I03b |
| I05a | **Stat consumers:** walk speed, a drain factor per need, the stamina bonus, the `IDamageModifier` pipeline and `damage.json` (armour applies to no cause yet). | M | Sonnet, medium | I03b, I06 |
| I05b | **Light round the player** (a spike): the `light` stat at night from an accessory or the off-hand; the frame time measured; screenshots. | M | Sonnet, high | I03b |
| I04 | **The equipment panel:** the paper doll, the bag grid, Spanish slot labels, the stats line, sort and trash, the compact layout; screenshots. | M | Sonnet, medium | I02 |
| I08a | **Gear content, first half:** the materials and the clothes: rows, icons, nouns, lexicon and locale, world words, workbench recipes, Pablo's `sell` rows and answer sets, the starting clothes, warmth numbers; the content loop. | L | Sonnet, high | I03b, I06, stop 3 |
| I08b | **Gear content, second half:** armour, accessories with their four effects, the two bags, the shield and the lantern, Domingo's `sell` rows, recipes and prices; the content loop. | L | Sonnet, high | I05a, I08a |
| I07 | **Durability:** `Wear` on the stack, `IWearSource`s (use, time worn, damage), tools and gear, repair recipes, the wear bar, `tool_worn`, `--wear`, the save's next version. | M | Sonnet, medium | I04, I05a, I08b |
| I10 | **Close-out:** `architecture.md`, `saves.md`, `content/README.md` ("Adding an item", "Adding a slot group"), the scripted runs, a QA pass (ui, save, progression), the full gate, the screenshot tour, the pull request. | S | Haiku | all |

**The build order** (two at a time where the files allow):

1. I00.
2. I01 with I06.
3. I03a.
4. I03b.
5. I02 with I09.
6. I05a with I05b.
7. I04 with I08a (I08a once the owner has settled the list at stop 3).
8. I08b.
9. I07.
10. I10.

**Never together**, because they edit the same files: I02 and I03b (`Inventory.cs`), I02 and I04 (the inventory layers), I07 and I08b
(the item rows).

I01, I06, I08a, I08b and I07 edit `content/game-v1`, so each runs the content loop (`content/README.md`, "The edit loop"): the new bundle
hash (through a wrapper script, since the sandbox refuses the word), the validation, the strict A2 lint, the host's `--validate-content`,
the whole suite, the goldens recorded again if they moved, and the hash in the save fixture.

**Owner-review stops** (the work waits for the owner at each):

1. **Acceptance:** D-game-35 (section 5; accepted 2026-11-15, building held until the owner says).
2. **After I06:** how the temperature feels at night, in the rain and by a fire, before any warmth numbers are set.
3. **Before I08a:** the gear list: the Spanish names, the world words, the numbers and the prices.
4. **After I04 and I09:** pictures of the panel and of the player in gear.
5. **After I05b:** how the light looks and what it costs a frame.
6. **After I10:** the owner plays the series before the pull request.

Verification at the end of the series: the full suite and the merge gate; two runs of one seed and one command list give the same state
digest with gear moved, worn and worn out; scripted runs that buy a `camisa` by voice, craft one, put it on, stand out at night with and
without it, fill the bag and try to take the bag off, die in gear, and save and load with gear worn; screenshots of the panel and of the
avatar in each layer; then the owner's check in the game.

## 5. The owner's decisions (2026-11-15)

Recorded as D-game-35 (decided 2026-11-15; building started the same day).

1. **Armour gives an armour rating for combat**, which will come in a later series; it reduces nothing now (not drowning, not lava, not
   cold), and the damage code is open so other causes can be added later. Amends D-game-23 (c) ("no combat") to "combat later".
2. **Clothing is a warmth layer under armour** (head, body, legs, feet); temperature from the weather and the night now, biome climate
   later; cold slows the player and never harms.
3. **The backpack:** with no bag, the hotbar's 10 slots and 20 more (base 30); a small bag adds 10 and a large one 20 (at most 50); a bag
   cannot come off while its slots hold items.
4. **Items as data first**, taking over W12: `game/items.json` and a schema; the save breaks, older saves are refused, no migration.
5. **Every gear item is craftable and buyable:** two routes for each, recipes and the NPCs.
6. **Worn gear is drawn** on the third-person avatar, and the first-person arm takes the sleeve's colour.
7. **Accessories:** three slots; the first effects are light at night, walk speed, slower needs (hunger, thirst) and more stamina, each a
   row of data.
8. **The extras, all in:** an off-hand slot (shield, torch, lantern), cosmetic slots, durability for gear (taking over W13 for tools), a
   sort button and a trash slot.
9. **The work is done in a new git worktree cut from `main`** (the standing rule).

**Answered with the acceptance** (2026-11-15):

10. **(a) The gear words:** "World words". The gear nouns on neither CEFR list (`chaqueta`, `abrigo`, `pantalones`, `botas`, `gorro`,
    `mochila`, `escudo`, `tela`, `cuero`, `hierro`, `cinturón`, `collar`, `amuleto`, `farol`, the armour's leg piece and `saco`) join the
    world-words block of `cefr/a1.txt` as nouns the player handles (as D-game-32 (e) did for four words), each in the milestone that first
    uses it; the final list is settled at stop 3.
11. **(b) The small bag:** "el saco". «La bolsa» keeps naming the whole inventory.
12. **When to build:** "Accept, but don't build yet"; later the same day, "Work on the plan": I00 done; then "stop and wait for me to tell you to start" before I01 and I06.

## 6. What this plan does not do

- No combat, enemies or weapons; armour's rating waits for them.
- No tailor NPC (W26, after a voice trial); Pablo sells the clothes.
- No placeable chests, quick stack or deposit-all (W24).
- No forge or anvil station; every recipe is at the workbench.
- No NPC repair service (W14 builds on I07), no reforging, no gear rating («nivel de equipo»).
- No more than three accessory slots, and no cosmetic slots for accessories.
- No lesson about clothes; the words are met in the panel, the shops and the recipes.
- No core (`Vocaterra.Nlp`) change. If a shop's noun needs one, that is a core milestone.

## 7. Risks

- **I01 touches nearly every file that names an item.** It lands alone in its step and early. A series that merges after it rebases onto
  the catalogue (the biomes series adds no items, by D-game-34).
- **The bag from 40 to 30.** The simulation's state, restore and digest assume 40 (`VoxelSimulation.cs`); tests fill the bag
  (`PlotPurchaseTests`, `StationPanelTests`, `ProcedureRunnerTests`, `DeathTests`, `InventoryTests`, `BagTests`); a scripted run may now
  fill it. I03b re-greps after each rebase.
- **The light is new rendering.** Nothing lights the world dynamically today; I05b is a spike with its own stop and may come back with a
  smaller answer (a glow mesh, the player's view only).
- **Temperature against the biomes series.** Both plans describe the same `PlaceTemperature`; whichever series builds second adds its
  terms to the other's. The handover and `docs/open-items.md` say so.
- **The weather kinds' ids.** Temperature reads channels; no kind's id goes into `src/`.
- **Saves and goldens.** Three save versions in one series; each records the fixtures again. The icon atlas keeps its order through the
  `icon` key so screenshot goldens do not move.
- **Words.** About sixteen nouns above A2 join the world words (answer 10); armour's leg piece has no common A2-friendly name.

## 8. Progress

| Id | Status | Gate | Notes |
|---|---|---|---|
| (proposal) | done, 2026-11-15 | documents only | This plan, D-game-35 (proposed), the index rows and the handover. |
| (acceptance) | done, 2026-11-15 | documents only | D-game-35 decided as written; the gear words as world words; the small bag `el saco`; building held. |
| I00 | done, 2026-11-15 | documents only (doc-link test) | Registered by the orchestrator: the game plan's section 4.15, the activities plan's notes (W12, W13, W26, (c)), D-game-23 (c) amended, open items. |
| I01 | done, 2026-11-15 | `e865a818`, 11,543 passed, 3 skipped | Items as data: `items.json` (14 rows), `IItemCatalogue` passed to every consumer, icons by key with a fallback, the validator's item checks, save version 9 (items by name; an empty slot is `""`). The enum keeps nine behavioural members; Bread, Apple, Axe, RoastApple and Flour are rows only. Report: `.claude/briefs/i01-report.md`. |
| I06 | done, 2026-11-16 | `a9e5df38`, 11,632 passed, 3 skipped | Temperature: five terms (base 14, day ±6, weather by channel, shelter in rain, fire +15 within 5) in tenths of a degree, four bands (cold from -5 walks at 85%, very cold at 65%), the snowflake and «Tengo frío.», `--temperature`, F3; warmth through `IWarmthSource` (zero). A fair noon feels 18, a clear night 6, a rainy windy night -2. Stop 2 (the owner's feel in the game) waits until he can open a window. Report: `.claude/briefs/i06-report.md`. |
| I03a | done, 2026-11-16 | `1627482f`, 11,751 passed, 3 skipped | The equipment core: `equipment.json` (six groups as rows; the off-hand and cosmetic slots work through the same rules), the items' `wear`, `stats`, `look`, `Items/Gear/` with its rules as `IEquipRule`s, `Stats/` with `StatPipeline` and contributors, the `Equip` and `Unequip` commands, warmth from the sheet, `--equip`. Worn items are not saved until I03b. Report: `.claude/briefs/i03a-report.md`. |
| I03b | done, 2026-11-16 | `805b2c73`, 11,819 passed, 3 skipped | The bag: base 30 from `equipment.json`'s `bagSlots`, the items' `capacity`, the active count following the worn bag, `BagCapacityRule` (`EquipResult.BagNotEmpty = 12`), the inventory screen's 3 to 5 rows; death reads `death.keepInBag` (worn gear and stacks of one stay); save version 10 with the active slots and the worn items by group, slot and item name (`save-v10.json`, nothing worn). Two pinned digests moved only by the shorter slot hash (proven). Report: `.claude/briefs/i03b-report.md`. |
| I02 | done, 2026-11-16 | `95aedf89`, 11,905 passed, 3 skipped | Interaction: `Items/Containers/` (`ISlotContainer` over the bag, the equipment and the trash; `InventoryOps` with all-or-nothing moves through a transaction; the held stack as a container), `VoxelCommandKind.SlotMove = 10` with a sub-code, `MoveRefused = 21`, `MoveReason` 0 to 9; clicks on today's screen, a sort button and a trash cell (icons only), `--move`. Closing on a full bag keeps the stack held; a save returns it first. The held stack and the trash are not saved; no save version change; no existing test changed. Report: `.claude/briefs/i02-report.md`. |
| I05a | done, 2026-11-16 | `b9aac8c9`, 11,977 passed, 3 skipped | The stat consumers (taken before I09: no game window needed): `StatRules` (one sum-or-factor rule and range per key), `walkSpeed` in `PrepareTick` (clamped 0.1 to 2.5; applies with `--needs off`), `hungerDrain` and `thirstDrain` per need, `maxStamina` raising the ceiling, `Damage/` with `IDamageModifier`, `ArmourDamageModifier` (min(80%, 4% per point)) and `game/damage.json` (drowning, no modifiers). No digest or baseline moved. Report: `.claude/briefs/i05a-report.md`. |
| I09 | done, 2026-11-16 (screenshots wait) | `50ae2330`, 11,996 passed, 3 skipped | Gear on the player: `Appearance` and `AppearanceBuilder` (layers from the highest `layer` number down; the back and the off-hand found by the slot's `wear` kind, so equipment.json did not change), `Equipment.Revision`, `PlayerGear` (cached on change), `GearFigureParts` (inflated boxes per part, a pack box), the off-hand item in both views, the sleeve's colour in first person. At most 9 extra boxes; nothing worn draws exactly as before. Built without windows: the screenshots need a test-only content folder with `look` rows and wait for the owner. Report: `.claude/briefs/i09-report.md`. |
| I04 | done, 2026-11-16 (screenshots wait) | `0cf31798`, 12,102 passed, 3 skipped | The equipment panel (taken before I05b, which needs a window): `EquipmentShape` (the doll's columns are the layered groups in layer order, a seventh group gets cells from data), clicks on equipment cells, refusal lines (English, `locale/en/ui.json`), hover, the stats line (`armadura`, `calor`, icons for the rest), regular, compact and tight layouts. Seven label and stat nouns added to `game/nouns.json`, `armadura` to `cefr/a1.txt` (D-game-35 (a)). I02's three behaviours kept; the quit path no longer throws with a held stack and a full bag. Report: `.claude/briefs/i04-report.md`. |
| I05b, I08a to I10 | todo | | Build order in section 4. |
