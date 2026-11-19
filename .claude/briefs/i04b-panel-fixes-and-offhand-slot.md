# Brief: I04b, the panel's fixes and the off-hand slot by the hotbar

Sent 2026-11-16 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/i04b`,
branch `I04b-panel-fixes`, cut from `feature/inventory` after I04's record. The shared part is `.claude/briefs/i-series-standard.md`.

The owner looked at screenshots of I04's panel and I09's gear on 2026-11-16 and asked: "I want you to fix all of those issues identified.
Also, when you have something in your offhand, there should be a slot to the left of the hotbar with that item, kind of like Minecraft."

```
You are implementing I04b: seven fixes to the equipment panel the owner saw in screenshots, and a new off-hand slot beside the hotbar.
Read .claude/briefs/i04-report.md first (what I04 built and where).

Screenshots were taken by the orchestrator with a test-only content copy (items with `wear`, `look`, `stats`, a bag); you do not take
screenshots (open no game window). The orchestrator takes them again after your gate. Prove each fix with tests on layout or
mapping code (the layer only draws), and say in the report which shot shows it.

The fixes:
1. The held stack is not drawn when there is no mouse. `InventoryLayer.DrawHeld` (src/Vocaterra.Game/UI/Inventory/InventoryLayer.cs, about
   line 216) returns when `view.Mouse` is null, which it is in scripted runs (and could be with a pad or a pointer outside the window). Keep
   the pointer position when there is one; otherwise draw the held stack beside the cell it was picked up from (the held stack remembers
   its origin since I02), slightly offset so the empty cell shows. Put the placement in a testable function.
2. The tight layout drops a stat chip: at 640x480 the stats line shows armour, warmth and +10% walk, but the +20 stamina chip is missing.
   Every non-default stat must show at every layout: wrap the chips to more lines, or shrink them, with the doll and bag still fitting
   (the `UiScaleLayoutTests` cases). Test: the placer keeps every chip inside the panel at every layout case with five non-default stats.
3. The doll's three columns have no headings. Head each layered column with its Spanish noun and article, the English on hover as other
   nouns do: clothing «la ropa», armour «la armadura», cosmetic «la apariencia» (all three are in the lexicon: `ropa` and `apariencia`
   in `lexicon/wiktionary-es.json`, `armadura` already a catalogue noun since I04). The heading word is data: add a `heading` (a noun key)
   to the layered groups in `game/equipment.json` and its schema, so a fourth layer brings its own heading. Add `ropa` and `apariencia` to
   `game/nouns.json`, their English to `locale/en/nouns.json`, and to `cefr/a1.txt`'s world words if the strict A2 lint or the CEFR check
   needs it, each with its reason (D-game-35 (a)). In compact and tight layouts, if a heading word cannot fit, use a short mark and keep the
   word on hover; say what you chose.
4. The panel's title is the English "Inventory" (`ui.inventoryTitle`). D-game-35 (b) settled that «la bolsa» names the inventory: show the
   Spanish noun with its article, «la bolsa», as the title, with the English "Inventory" beside it (dimmer), the way the needs list shows
   its Spanish word and English. `bolsa` is in the lexicon (`seed.json`); add it to `game/nouns.json` if it is not a catalogue noun. Keep
   the other panels' titles as they are.
5. The refusal lines are English (I04's eleven `refuse*` keys in `locale/en/ui.json`). Make each a short Spanish line the player reads
   (A1 or A2 words, the strict A2 lint and the grammar check clean; for example «No puedes llevar eso.», «No hay sitio en la bolsa.»), with
   the English beside it or under it as the needs rows do. Find how the game already keeps short Spanish lines with their English (the
   needs' stage lines, «Tengo frío.» from I06) and follow that: Spanish in content/game-v1, English in the locale bundle. Every Spanish
   line gets a speech line if the pattern you follow has them (render them with the content loop's `--speech`). List every line, Spanish and
   English, in the report: the owner reviews the wording.
6. The key hints are stale: `ui.inventoryHints` and `ui.inventoryHintsShort` still say "left click: mine   right click: place". Rewrite
   both for what the clicks do since I02 (left: take, put, swap; right: half, one; shift: wear or move; the sort and trash buttons), in
   the English locale only, fitting the footer at every layout (the short one for compact and tight; test the fit if a measure exists).
7. Empty counted-slot labels are cut off: «esp…» for «espalda» in the backpack cell. Show the label in full: a smaller font in the cell,
   the label beside or under the cell, or the cell wide enough; choose what reads best at every layout and test that no label is
   truncated at regular size (compact and tight may keep the word on hover only: say so).

The new feature:
8. The off-hand slot by the hotbar (the owner: "kind of like Minecraft"). When the off-hand group has an item worn, the HUD draws one more
   cell to the left of the hotbar's first cell, set apart by a small gap, showing that item's icon (and its count badge if it ever has one).
   Nothing worn there: no cell, and the hotbar is exactly where it is today (verify: a layout test that the hotbar's rectangles are
   unchanged with nothing in the off-hand). The off-hand group is found as I09 found it (the slot's `wear` kind `offhand`, never the group
   id). The cell is not a hotbar slot: number keys and the wheel do not select it, and the bag's indexes do not move. The HUD's other parts
   left of the hotbar (the needs and breath bars in `HudLayout`, `NeedsHudLayout`, `BreathHudLayout`) must not overlap it at any UI scale and
   window size: move them only if they would, and test it. Optional, only if cheap and you say so: a key that swaps the selected hotbar item
   with the off-hand (Minecraft's F), sending the existing moves so the equip rules decide (most items are not off-hand items and are
   refused with the refusal line); if you add it, add it to the controls screen and its locale text.

Tests (Given_When_Then, mirrored folders): each fix as above; the off-hand cell appears only with an item worn, sits left of the hotbar
with the gap, and never overlaps the needs or breath bars at every UI scale case; the hotbar unchanged without it; the heading comes from
data (a test equipment data with a fourth layer and its heading).

Content: equipment.json and its schema, nouns, the Spanish refusal lines, the CEFR list if needed: run the content loop
(scripts/content-loop.sh, with `--grammar --speech` if lines are added) and put the new bundle hash in `Fixtures/save-v10.json`.

Documents: docs/game/architecture.md (the headings, the off-hand cell), docs/game/running.md or the controls doc if a key is added.

Out of scope: shipped gear and icons (I08a, I08b), the compact layout's item names cut short (leave as is), «accesorio» (not in the
lexicon: leave the accessory cells unlabelled), the light from the off-hand (I05b).

The gate is the standard brief's (the count after I04 is 12,102 passed, 3 skipped). Report: the standard one page, plus every Spanish line
and heading with its English, what each fix changed and which screenshot will show it, and every existing test changed and why.
```
