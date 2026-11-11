# Y06 report: the innkeeper Rosa

(The report of the second Y06 agent, 2026-11-11, as received; it resumed from the first agent's WIP `0d8a745c`.)

Branch `Y06-innkeeper`, worktree `/home/james/Dev/Vocaterra/.claude/worktrees/y06`, HEAD `f2a83ef8`. Commits on `0d8a745c`: `9cc4ced2`
(phrases, intents examples, figure, speech, tests, docs), `935eec6a` and `f2a83ef8` (identity baseline only), `0aa121a1` (weather and
understood replies, roster tests, save fixture, speech).

## Built
- NPC `innkeeper` (label Posadera, name Rosa), id last in `npcs.json`. Agent states `idle` (prompt `inn-offer`) and `given` (prompt
  `inn-given`). The opening is the offer at the first talk only; later talks open with the prompt text: «¿Quieres una habitación?»
  until given, «Buenas noches.» after.
- «sí», «sí, por favor», the request and «¿Hay…?» raise `innkeeper.room` (grant_lodging, one row in `host-events.json`) and move to
  `given`. `patience`: misses 2, afterHelp 1, event `innkeeper.give`, once; the give path also raises `innkeeper.room`, through its own
  pool `innkeeper_give`. «No, gracias» answers and leaves the offer open.
- Beyond the brief, because the roster tests require it of every NPC: weather and wellbeing replies (`slotReplies`,
  `innkeeper.weather`), `innkeeper.understood` and `acknowledgementPools`; both prompts allow `state_weather` and `ask_weather`.
- Scene: at `npc.innkeeper` (282, 227), facing west.
- Figure `innkeeper`: plum dress, mustard shawl with fringe, silver hair in a bun, a ring of keys; a new face cell `FaceInnkeeper`, last
  in the atlas enum. Screenshots in `artifacts/y06/` of the y06 worktree.
- Phrase rows `quiero-una-habitacion` and `hay-una-habitacion` (home `inn-offer`) in `PHRASES.md` and `phrases.json`, their locale
  cards; `focus` those two plus `si-por-favor` and `no-gracias`. 11 examples added to `request_item` and `ask_availability` in
  `intents.json` (`PhraseSheetTests` requires every variant to be an intent example).
- Docs: `content/README.md`, `src/Vocaterra.Game/README.md`, `docs/game/running.md`, `docs/game/lessons.md`.

## New Spanish (every word passes the A2 gate; habitación, cama, gratis, posada were in the lists)
- `innkeeper_offer` «¡Hola! ¿Quieres una habitación?» (Hello! Would you like a room?)
- `innkeeper_room` «¡Claro! / ¡Muy bien! La habitación es gratis. Tu cama está allí.» (Of course / Very good! The room is free. Your bed is over there.)
- `innkeeper_give` «Está bien. La habitación es gratis. Tu cama está allí.» (after «no sé» or two misses)
- `innkeeper_night` «Buenas noches. Tu cama está allí.» / «¡Buenas noches!»
- `innkeeper_no` «Está bien. / Bueno. La habitación es gratis.»
- `innkeeper_free` «La habitación es gratis. ¿Quieres una habitación?» (to «¿Cuánto cuesta?»)
- `innkeeper_help` «Puedes decir: «Quiero una habitación, por favor».»; `innkeeper_help_given` «Puedes decir: «Buenas noches» o «Gracias».»
- `innkeeper_understood` «Te entiendo. La habitación es gratis.»
- `innkeeper_well` «Bien, gracias.»; `innkeeper_nice` «¡Qué bien!»; `innkeeper_yes` «¡Sí!»; `innkeeper_sun` «Hoy hace sol.»;
  `innkeeper_weather` «Sí, hace {weather}.» (the baker's eleven values)
- `innkeeper_thank` «De nada.» / «¡Con gusto!»; `innkeeper_bye` «¡Hasta luego!» / «¡Nos vemos!»
- Prompts: `inn-offer` «¿Quieres una habitación?», `inn-given` «Buenas noches.»; role `npc.innkeeper.role` "The innkeeper".

## The core's readings at `inn-offer` (all communicated, correctness 1)
sí / sí, por favor → `confirm`; «Quiero una habitación, por favor.» / «Una habitación, por favor.» → `request_item` {habitación};
«quiero una cama» → `request_item` {cama}; «¿Hay una habitación?» → `ask_availability`; «No, gracias.» / «no» → `deny`; «no sé» →
`dont_know`; «hola» → `greet`; «¿Cuánto cuesta?» → `ask_price`; «Gracias» → `thank`; «azul» → `unknown`; «quiero un habitación» and
«hay un habitación» → their intents with `gender.agree`. No wrong reading.

## Voice
`pf_dora`, speaker 42, speed 1.0, no `gain`: mean RMS 1824 against Luna's `ef_dora` 1814 and the others 1932 to 2163 (within 10%).
All her files rendered and indexed; the checklist rows wait for the owner's listening.

## Tests
New `Npc/InnkeeperTests.cs` (14 cases: the offer; six lines each grant once and move to `given`; «no sé»; two misses; «No, gracias»
then yes at the next talk; a second visit grants nothing; save and load keep the grant; the bundle validates). `LodgingTests`' three
reflection tests now go through her talk turn. Roster-pinned tests updated (NpcRoster, NpcAgent, OffPrompt, NpcPatience, FourthNpc,
NpcService, NpcFrontend, PhraseSheet and lookup, PhraseProgress, PhraseSupportFrontend, GameData, SpeechLines, Scene,
GrammarStringsExtractor, the face lists in VillageArt and VillageFrontend). `save-v8.json` patched with her NPC record, two phrase
records and the new bundle hash. Build 0 warnings; format clean; validate passes; A2 lint clean. Full suite with models 10,797 passed,
3 skipped (before 10,763); without models 10,750 passed, 50 skipped. End-to-end goldens unmoved; the identity baseline gained 21 lines,
moved none.

## Runs in the game (`--dev --skip-to-session 2`, walk to 280,227, `--talk innkeeper`)
- «sí»: confirm, state given, `innkeeper.room`, `world lodging_granted`; after the sleep `systems.sleep` = {lodgingGiven: true, last: inn.bed}.
- «Quiero una habitación, por favor.»: `request_item`, the same.
- «perro», «azul»: «No te entendí…», then FAIL-FORWARD with `innkeeper.room` and the give line; the same `systems.sleep`.

## Scripted runs (`r03-npcs`, `r21-skip`, `r26-death`, before from the base in a temporary worktree)
Screenshots identical. Expected differences: one more NPC record and two phrase records and the new bundle hash in the saves; speech
738 lines / 1476 files → 785 / 1570; figure quads 882 → 1008; autosave sizes.

## Core gaps and findings
- A fuzz flake: `ContentSchemaFuzzTests.MutatedBundles_...` failed once in three full runs, seed `2zwDo8nRB_f7` (O15: removing
  `supportLanguage` from `manifest.json` is called undocumented though the field is optional and unhashed). Unrelated to Y06; passed
  on rerun.
- `patience` counts every line at the prompt (deny, greeting, weather included), so «No, gracias» then any other line gives the room;
  the owner may want misses 3. `afterHelp` 1: «no sé» or «ayuda» gives it in the same turn after the help line.
- Four info-level grammar findings, all deliberate wrong examples (`un habitación`).

## For later milestones
Y07: `innkeeper.room` raises `lodging_granted` once (the trigger for `inn-room`). Y08: the counter is still a crate one can step on.
Y11: `terreno`, `alcalde`, `estaca` not needed; `posada` not in her lines.

## Known not fixed
«Quiero dormir aquí» and «¿Puedo dormir aquí?» unread (Y00's core gaps). The owner should look at `innkeeper-behind-counter.png`.
