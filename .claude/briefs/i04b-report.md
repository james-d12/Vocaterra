# I04b report (the panel's fixes and the off-hand slot by the hotbar)

The agent's report, saved by the orchestrator (2026-11-18). Sonnet 5.5 at high effort; brief `i04b-panel-fixes-and-offhand-slot.md`. The
first agent wrote the eight items (WIP 90e614e7, 2026-11-16) and was stopped by the owner; a second finished them after the merges with
`main`. Gated at `3334359e`.

Branch `I04b-panel-fixes`: 90e614e7 (WIP), d1ba25c3 (merge of feature/inventory: both manifests rehashed with rehash-bundle.sh,
save-v10.json kept as the placeholder), 4fe9a350 (locale manifest after the content loop), 624f280d (the off-hand fallback, counts, guard).

## Built

| Item | Change | Shot |
|---|---|---|
| 1. Held stack | Drawn beside its origin cell when there is no pointer (`UI/Inventory/HeldPlacement.cs`, `HeldPlacementTests`). | held (`--move left,bag,12`) |
| 2. Stat chips | `StatLinePlacer` and the layout keep every chip inside the panel at every layout with five non-default stats. | 640x480 tight |
| 3. Headings | `heading` (a noun key) on the layered groups in `equipment.json` and its schema; `HeadingText` picks «la ropa», then «ropa», then a short mark («arm», «apa») as room shrinks, the full word on hover. `ropa`, `apariencia`, `bolsa` in `nouns.json` and the English locale; 6 card WAVs. | 1280x720, 800x600, 640x480 |
| 4. Title | «la bolsa» with the English noun ("bag") beside it, dimmer; `InventoryPanel.TitleLemma = "bolsa"`. | any panel shot |
| 5. Refusals | Spanish in `game/words.json` `hudLines` (`refuse.*`), English in the locale bundle under the same ids (the needs' stage-line pattern); speech rendered. | refusal |
| 6. Key hints | Both rewritten (English locale). | any panel shot |
| 7. Labels | In full at regular size; compact and tight keep the word on hover. | 1280x720 |
| 8. Off-hand cell | `HotbarLayer.OffHandBoxFor`: left of the hotbar, 10 px gap, the item's icon; the hotbar unchanged with nothing worn (tested); the needs block clear of it. Where there is no room left of the hotbar (533x400, 640x480: `hotbar.X - 58 < 12`) the cell stands one slot above the hotbar's left end and the needs block lifts 58 px. No swap key (optional, not cheap: a controls binding and locale text). | HUD with an off-hand item |

## Spanish lines (owner review, stop 3)

«Eso es otra cosa.» That is a different item. «La basura tiene otra cosa.» The bin holds something else. «Ya está lleno.» That stack is
full. «No hay sitio en la bolsa.» There is no room in the bag. «Son demasiados.» That is more than one slot holds. «No puedes llevar eso.»
You cannot wear that. «Eso no va ahí.» That is not worn there. «Eso no se lleva aquí.» That kind of thing is not worn here. «Ya llevas
uno.» You already wear one of those. «Solo uno a la vez.» Only one at a time. «La bolsa no está vacía.» The bag is not empty. Headings: «la
ropa» clothes, «la armadura» armour, «la apariencia» appearance. All pass the grammar check and the strict A2 lint as written.

Key hints: long "{key:inventory} or Esc: close   left click: take, put, swap   right click: half, one   shift+click: wear or move   bars:
sort   bin: trash"; short "{key:inventory} or Esc: close   left: take, put   right: half, one   shift: wear".

## Tests changed

Noun and card counts 40 to 43 (`NounCatalogTests`, `SpeechLinesTests`, `GameDataTests`); `SpanishLiteralGuardTests` allows the literal
"bolsa" in `InventoryPanel.cs` (the lemma key naming the panel; could move to data); `OffHandCellLayoutTests` (new in the WIP) covers the
fallback and compares the needs boxes field by field; the WIP's edits to `MoveRefusalTextTests`, `MoveNoticeTests` and the layout tests. No
golden, oracle or fixture changed.

## Gate

The agent: Release build 0 warnings; `test.sh full` 13,301 passed, 0 failed, 9 skipped (+67); timing 23; format, validate, A2 lint clean;
content loop 0 new findings, 0 of 1,686 speech files to render. The merge gate at `3334359e`: test 13,301 passed, 0 failed, 9 skipped; flaky
none; format, content, grammar and licence clean; **timing 22 passed, 1 failed**: `StreamedWorldTimeTests` (a column's band 5.21 ms of CPU
against a budget of 2.6), failing again alone. Accepted by the orchestrator as load, not I04b: I04b changes no world code or world data, the
same test passed at `c3bd3c2b` an hour earlier at load 5, and the gate ran while another session's three world-next agents held the load at
14 to 20. To re-run when the machine is quiet.

## Known not fixed

The compact layout's names cut short and labels on hover only (as allowed); 50 slots plus the needs list at 533x400 overflows (as before);
`accesorio` unlabelled.
