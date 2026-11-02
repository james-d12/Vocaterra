# Scripted runs: the front-end's regression set

Twenty-two scripted runs of the game that between them pass through every flow `VoxelGame` drives: the walk and the camp, talking and typing,
the first session's three NPCs and the village's (the baker's sale and the fisher's directions, VILLAGE_PLAN V09; the market of session 10 and the simple
future in free talk, V09 after phase B), the lessons, the book,
the inventory and crafting panels, the check battery, a whole playtest, the mic check, a saved report, the view over the shoulder, greyscale and
dusk, the interface text size, a spoken line, the sleep at the fire into the next session and a game skipped to a later session (V12c). Run them before a change to the front-end and
after it, and compare what each one wrote (stdout, screenshots, saves, sound logs, the session log). They are the check the tests cannot
give for rendering and input (R16).

## How to run them

Build once, then run each command from the repository root. Every run is a heavy command, so it goes through `scripts/limited.sh` (one at a
time, capped; CLAUDE.md "Machine resources"). A run ends by itself at `--screenshot` once `--frames` have passed and the turns are in.

```bash
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4
export DISPLAY=:0 VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
GAME=src/Vocaterra.Game/bin/Debug/net10.0/Vocaterra.Game
W=/tmp/vocaterra-run          # one work folder: clear it before each run, then copy it away (see "Comparing" below)
rm -rf "$W" && mkdir -p "$W"
```

Every command below also takes `--session-dir "$W/session"`, so that the session log, the autosave and the reports land in the work folder.
It is left out of the table for brevity.

`scripts/scripted-runs.sh OUT [name...]` runs them all (or the named ones) this way and keeps each run's folder in `OUT/<name>`;
`python3 -I scripts/compare-runs.py BASE NEW` compares two captures with the masks and the order-only rule below (exit 1 on a difference). Take
the baseline with the same script and the same work folder (`VOCATERRA_RUNS_WORK`) as the capture it is compared with: the work folder's path is
in the output.

## The runs

| Run | What it covers | Command (after `scripts/limited.sh "$GAME" --session-dir "$W/session"`) |
| --- | --- | --- |
| r01-route | The walk down the trail (the planned walk, the start beacon reached), gathering trunks and berries, crafting a torch, placing it, the objective's steps and the autosave at a step, the footsteps | `--seed 42 --sound-log "$W/sounds.log" --walk-to 176,186 --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 --look 180,-15 --screenshot "$W/shot.png"` |
| r02-river | A typed turn with the guide, her answer pointing the beacon at the river, the arrow | `--seed 42 --look 120,-5 --say "¿dónde está el río?" --screenshot "$W/shot.png" --frames 300` |
| r03-npcs | The shopkeeper (the bottle), the villager (the river), leaving a dialogue, the save at the end | `--seed 42 --mute --sound-log "$W/sounds.log" --talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" --talk villager --say "¿dónde está el río?" --leave --screenshot "$W/shot.png" --frames 60 --save "$W/game.save"` |
| r04-journal | The guide's whole first lesson, its recap, the journal on the phrases page, the save | `--seed 42 --mute --lesson-run first-words --journal --journal-phrases --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"` |
| r05-book-play | The book's speaker buttons: a noun's recording, a phrase's slow recording, two `--shot` steps, the sound log | `--seed 42 --sound-log "$W/sounds.log" --lesson-run first-words --book-play fruta --shot "$W/noun.png" --book-play-slow phrase:pedir-agua --shot "$W/phrase.png" --screenshot "$W/shot.png" --frames 30` |
| r06-practise | "Practise again" in the book: the book closes, the toast and the beacon say where to go | `--seed 42 --mute --lesson-run first-words --book-practise first-words --screenshot "$W/shot.png" --frames 30` |
| r07-inventory | The inventory panel open at the end | `--seed 42 --mute --inventory --screenshot "$W/shot.png" --frames 30` |
| r08-crafting | The crafting panel open at the end | `--seed 42 --mute --crafting --screenshot "$W/shot.png" --frames 30` |
| r09-check-pre | The pre-check battery: comprehension picks and production answers, its `--shot`, the hand-over to the world, the save | `--seed 42 --check pre --wait 2 --shot "$W/check.png"` then the pre-check answers (below) then `--screenshot "$W/shot.png" --frames 60 --save "$W/game.save"` |
| r10-playtest | A whole playtest: the mic check, the pre-check, the lesson, the camp, the first session's three NPCs, the transfer, the fire at the player's camp, the post-check that starts by itself, the playtest save and the thank-you screen | `--playtest --seed 42` then the pre-check answers, `--lesson-run first-words`, the camp steps of r01 (from `--walk-to 172,187` to the `--place`), `--talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" --talk villager --say "¿dónde está el río?" --walk-to 234,222 --interact 233,34,222 --look 180,-14 --talk guide --say "sí, por favor" --leave --walk-to 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1`, the post-check answers (below), `--screenshot "$W/shot.png" --frames 60` |
| r11-mic | The mic check alone: the worker hears a synthesized "hola" | `--seed 42 --mic-check --screenshot "$W/shot.png"` |
| r12-report | A scripted report saved at once: the picture, the Markdown, the toast | `--seed 42 --mute --report "the bridge has a gap" --screenshot "$W/shot.png" --frames 30` |
| r14-third | The view over the shoulder: the player's figure, the camera kept in front of walls | `--seed 42 --mute --third-person --look 120,-5 --screenshot "$W/shot.png" --frames 60` |
| r15-grey-dusk | Greyscale (blocks whose noun is not known are grey) and dusk (the lamps, the light on the arm) | `--seed 42 --mute --greyscale --dusk --teleport 222,222 --look 80,-16 --screenshot "$W/shot.png" --frames 60` |
| r16-text-scale | A small window at 150 % text: the canvas, the book's lessons page laid out on it | `--seed 42 --mute --size 800x600 --text-scale 150 --lesson-run first-words --book-page lessons --screenshot "$W/shot.png" --frames 30` |
| r17-speak | A spoken line (synthesized, then heard by the recognizer) at the stall | `--seed 42 --talk shopkeeper --speak "quiero agua" --screenshot "$W/shot.png"` |
| r18-bakery | The bakery purchase in session 5 (VILLAGE_PLAN V03, V11): five trunks cut from the farmer's orchard, the order `oven-wood` delivered as the panel opens on the baker (five wood out, four coins in, `order_delivered`; `paid.png`), then `quiero dos panes, por favor` at her open prompt (`baker.sell_bread`: four coins out, two loaves in; `sale.png`); the screenshot is the inventory with the book and two loaves, no coin and no wood, and the save holds them | `--seed 42 --mute --session 5 --teleport 309,216 --interact 311,38,214 --interact 311,39,214 --interact 311,40,214 --interact 311,38,218 --interact 311,39,218 --talk baker --shot "$W/paid.png" --say "quiero dos panes, por favor" --shot "$W/sale.png" --leave --inventory --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"` |
| r19-directions | Asking the way (VILLAGE_PLAN V02, V10): the fisher on the bridge's east landing asked `¿dónde está la panadería?` (`place_asked` bakery, the beacon on the bakery: `arrow.png` shows his answer and the column with `panadería 44 blocks`), the walk up the road to the bakery's door, the arrival (`beacon_reached` bakery, the arrow gone); the screenshot is the bakery from its path, with the baker and her name | `--seed 42 --mute --teleport 236,206 --talk fisher --say "¿dónde está la panadería?" --shot "$W/arrow.png" --walk-to 282,206 --walk-to 282,212 --look 0,4 --screenshot "$W/shot.png" --frames 60` |
| r20-sleep | The next session by a sleep at the fire (V12c): session 1's ending the short way (the camp, the shopkeeper's bottle on his fail-forward second visit, no typed turn, the river and the player's camp by `--teleport`), the closing card with `Enter: sleep until morning and start session 2` (`ending.png`), `--sleep` (the autosave `written at sleep`, the log's `session_end` and second `session_start`, the `slept` event), session 2's objective naming Personas with the beacon on the villager and the morning toast, and no fire prompt, since Personas is left (`session2.png`); the screenshot is the book's Lessons page with `next: talk to Aldeano` in gold for Personas and `open: talk to Luna` for the skipped first lesson; the save is session 2's | `--seed 42 --mute` then the camp steps of r01 (from `--walk-to 172,187` to the `--place`), `--teleport 283,211 --talk shopkeeper --wait 3 --leave --talk shopkeeper --leave --teleport 234,222 --interact 233,34,222 --teleport 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 --shot "$W/ending.png" --sleep --look 129,-20 --wait 2 --shot "$W/session2.png" --book-page lessons --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"` |
| r21-skip | `--skip-to-session 4` (V12c, a test aid): the console's and the log's `skipped to play session 4 (sessions 1 to 3 counted as done; test aid)`, El pueblo's objective with the beacon on Mar from the player's camp (`beacon.png`), the book's Lessons page with the three earlier lessons completed in sessions 1, 2 and 3; the save carries `skippedTo` | `--seed 42 --mute --skip-to-session 4 --wait 1 --shot "$W/beacon.png" --book-page lessons --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"` |
| r22-market | The market of session 10 (VILLAGE_PLAN V15): `--skip-to-session 10` (no coin, three apples, two loaves, the axe), four trunks cut in the farmer's orchard (`before.png`: four wood in the hotbar, no coin), `vendo madera` to Domingo (`offer_item`, `smith.buy_wood`: the four wood out, four coins in; `sold.png`), `quiero dos panes, por favor` to Luz, whose lesson El mercado starts as her panel opens and waits at its first explanation (`baker.sell_bread`: four coins out, two loaves in; `bought.png`), and `¿cuánto es todo?` to León (`ask_total`, "Quince manzanas, quince monedas."; `total.png`); the screenshot is the inventory (the book, three apples, the axe, four loaves, no coin, no wood) and the save holds it | `--seed 42 --mute --skip-to-session 10 --teleport 309,216 --interact 311,38,214 --interact 311,39,214 --interact 311,40,214 --interact 311,38,218 --shot "$W/before.png" --talk smith --say "vendo madera" --leave --shot "$W/sold.png" --talk baker --say "quiero dos panes, por favor" --leave --shot "$W/bought.png" --talk farmer --say "¿cuánto es todo?" --shot "$W/total.png" --leave --inventory --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"` |
| r23-future | The simple future (VILLAGE_PLAN V16, V17): `--skip-to-session 9`, Luz's lesson Mañana en el pueblo with typed answers (`Mañana compraré pan`, `Buscaré madera para Luz` and `Iré al río` to Domingo, `Luz hará pan` to Mar: 12 steps done, the two reviews skipped with nothing due), then free talk with Luz at her open prompt, `mañana compraré pan` (`state_action`, understood, "¡Eso es!"; `reply.png`; no phrase use is logged: her prompt's rows have no `state_action`, see `docs/game/README.md`, the village's open points); the screenshot is the book's Verbs page on `comprar`, its columns past, future (ir a) and future, `compraré` said twice, both correct | `--seed 42 --mute --skip-to-session 9 --lesson-run manana-en-el-pueblo --talk baker --say "mañana compraré pan" --shot "$W/reply.png" --leave --book-verb comprar --screenshot "$W/shot.png" --frames 30` |
| r24-followups | The follow-ups (VILLAGE_PLAN V20, V21, V21b): from `--skip-to-session 10`, `mañana compraré pan` to Luz in free talk (`understood`, «¡Qué bien!», `phrase manana-comprare-pan unaided`; `future.png`), `quiero madera` to the stall (`request_item`, «No tengo eso. Tengo agua y pan.», no bottle; `stall.png`), and `ayer compre pan` to Luz (understood, «¡Qué bien!»: no accent note, as free talk has none; the note shows at a lesson's answer step, `compré tres manzanas` in el-mercado, `docs/game/README.md`, the open points; `accent.png`); the screenshot is the hotbar and the save holds them | `--seed 42 --mute --skip-to-session 10 --talk baker --say "mañana compraré pan" --shot "$W/future.png" --leave --talk shopkeeper --say "quiero madera" --shot "$W/stall.png" --leave --talk baker --say "ayer compre pan" --shot "$W/accent.png" --leave --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"` |

The pre-check answers (r09 and r10; the battery's order for seed 42):

```
--pick 1 --say "¿Qué es esto?" --say "Sí." --pick 3 --say "No, gracias." --pick 2 --say "Necesitamos agua." --say "¿Dónde está el río?" --say "No." --say "Más despacio, por favor." --say "Otra vez, por favor." --say "Ella quiere fruta." --say "Tengo una fruta." --pick 2 --pick 3 --pick 3 --say "Ayer comí fruta." --pick 3 --pick 2 --pick 3 --pick 3 --pick 2 --pick 1 --say "Sí, por favor." --pick 3 --pick 3 --pick 2 --pick 2 --say "Voy a comer pan." --say "Quiero madera." --say "Quiero agua, por favor."
```

The post-check answers (r10):

```
--say "Necesitamos agua." --say "Más despacio, por favor." --pick 1 --say "Sí, por favor." --say "Ella quiere fruta." --pick 2 --pick 1 --pick 1 --pick 3 --pick 1 --say "Voy a comer pan." --pick 1 --say "Ayer comí fruta." --say "¿Dónde está el río?" --pick 2 --say "Quiero madera." --pick 3 --say "No." --pick 1 --say "No, gracias." --pick 2 --pick 3 --pick 1 --pick 2 --say "Otra vez, por favor." --pick 2 --say "Sí." --say "Tengo una fruta." --say "¿Qué es esto?" --pick 2 --say "Quiero agua, por favor."
```

There is no r13: `--report-open` leaves the report panel open, and a paused world takes no more ticks, so the run never ends by design
(S21, `IMPLEMENTATION_PLAN.md`). Check the open panel by hand (below).

**The follow-ups against `1dbb0e5`** (V24, 2026-11-02; the merge of PR #20, where the follow-ups start, against `768a3e5`, the follow-ups' tip; 22 runs, captured once each in the one worktree). Every difference traces to a follow-up milestone:

- **Every run:** the `session_start` line gains a `day` field (V25), and every save and autosave is 34 to 46 bytes larger (the sitting, V25).
- **The speech line:** `457 lines, 914 files` to `531 lines, 1062 files` (V20 +20 files, V21 +6, V21b +122).
- **The exact runs** (r01, r07, r08, r09, r12, r14, r15, r20, r21): the bundle hash in the session log and the replay record (content, V20 to V21b); r15 shows no name plate on the fisher, who is neither faced nor the beacon's NPC (V22); r20 and r21 show the Lessons page at `next:` (V21b, the picture).
- **r18 and r19** (order only): Luz's and Mar's focus cards are no longer shown in these sessions, whose lessons are not yet unlocked (V20 (c)), so the
  phrases `quiero-dos-panes`, `donde-esta-la-panaderia` and `donde-esta-la-herreria` are `unaided`, not `supported` (no card to lean on).
- **r22:** León's total in free talk is «Una manzana, una moneda.» (V21b); a session with nothing due says «Nothing to review yet» once (V23a, the toast).
- **r23:** the free-talk line `mañana compraré pan` is a phrase use (V20 (b)), answered «¡Qué bien!» (V20), with the card-and-audio support of its turn; Luz's two
  `vendo-madera` cards are no longer shown (V20 (c)).
- **Nothing else moved:** the ticks, the world digests, the scene (`1958 trees`, `9b952982f5e1bc44`), every other picture and every other line of the logs.
The runs that moved only in `day` and the speech count are order-only or exact as before.

## Comparing two captures

Mask before comparing: every number followed by `ms` (the draw and frame timings, the turns' latency), the wall-clock times and dates, and
the session log's `wall_seconds`, `*_ms` and latency fields. Measured on 2026-10-26 by capturing the unchanged code twice:

- **Exact (byte for byte after the masks, screenshots and saves included):** r01, r07, r08, r09, r12, r14, r15, r20, r21. r09, r20 and r21 write
  a save, so they also check that a save comes out the same. r20 and r21 (V12c) take no turn on the worker (r20 gets the bottle from the shopkeeper's
  fail-forward host turn, which runs on the game's thread); captured twice on 2026-10-31 they matched byte for byte.
- **Order only:** r02, r03, r04, r05, r06, r10, r11, r16, r17, r18, r19, r22, r23, every run with a turn for the worker (r11's is the mic check's "hola": the
  same binary heard it at tick 1, 6 and 1 in three runs). When a turn is applied depends on how fast the
  worker thread evaluates it (a busy machine, the models in the disk cache), so these runs land their turns a few ticks apart from one capture
  to the next, and every tick count, stored time, state digest, save and picture after that moves with them. r03 and r04 matched in two
  captures on a busy machine and moved by a tick on an idle one. Compare only `stdout.txt` and `session.jsonl`, with every number and digest
  masked, and the session log's lines as a set (a turn's line can land before or after an event line of the same frame). That they move at all
  is a finding: a scripted run does not freeze the world exactly while a turn is out. r18 and r19 (VILLAGE_PLAN V09) have a turn each, so they
  are order only too; captured twice on 2026-10-31 they matched byte for byte but for the turn's latency, which r19's `arrow.png` draws in the
  panel (`18.4 ms`, `18.9 ms`). r22 and r23 (V09 after phase B) have three and five turns; captured twice on 2026-11-01, r22 matched byte for byte, pictures and save
  included, and r23 but for the free-talk turn's latency in `reply.png` (`2.7 ms`, `3.3 ms`). r24 (V24, three turns) captured twice on 2026-11-02: the session log, the stdout and the save match after the masks, and the three pictures
differ only in the panel's latency label (a box of about 20 by 10 pixels, measured with a pixel difference). `compare-runs.py` lists r24 as
different, since r24 is not in its order-only list; compare its pictures by that box.

**Across a content change** every run differs, and each difference must trace to the change. Phase A of the village plan (`f3a242f` against
`feature/village` at `1f201e4`, V09; the captures of r01 to r17) moved: in every run the closing summary's lessons (six; `personas` 22 steps and
`ayer-y-manana` 21 with their review steps); in the exact runs the speech count (`340 lines, 680 files`), the scene line (`1958 trees`, another
digest: the village's radius 24 also widens the calm ground around it, so the hillside behind it is drawn anew), the mesh and figure counts (seven
figures), the world digests and the saves' sizes; in the session logs `beacon_reached` and `place_asked`; on the pictures the bakery, the smithy,
the well and the orchard at the village and in its far view, the fisher with his lamp on the bridge's east landing (r15) and the farmer's name
behind the stall (r17); the book's line counts (93 phrases, 22 nouns, six lessons); and in r03 and r17 the `npc` tip, which now shows on the road as the
crosshair passes the fisher, so the `speak` tip shows when the stall's panel opens. The first session's route (r10) took the same ticks and
asked the same 31 check items before and after.

Phase B of the village plan (`feature/village` at `ccf8187`, the end of phase A with V12b and V12c, against `fa22116`, after V15b; V09 again, every run
captured in the one worktree; `main`'s merge `0cf674b` is older than `ccf8187`, so none of it is in the difference) moved, and nothing else:

- **Every run:** the closing summary lists ten lessons, the four new ones `not started` (`como-es` 20 units, `ayer-en-el-pueblo` 18, `manana-en-el-pueblo`
  14, `el-mercado` 22; V13, V14, V17, V15). In the order-only runs that is the only difference but for r18's (below).
- **The exact runs** (r01, r07, r08, r09, r12, r14, r15, r20, r21): the speech line, `451 lines, 902 files` (680 before; V13 to V15), and every save 18.4 KB
  larger (r01's first autosave 38 396 to 56 751 bytes: the records of the 155 phrase rows, not 93, of ten lessons and of the order `water-for-plants`; V13 to
  V15). The ticks, the world digests, the scene line (`1958 trees`, `9b952982f5e1bc44`) and every picture are the same, but for the Lessons pages of r20 and
  r21 (`lines 1 to 29 of 192`, 121 before) and r21's hotbar and state digest (`a43e2434…` to `1dc2f615…`): the skip carries session 1's bottle of water
  since V13 (`water 1` on the console).
- **r18:** Luz shows a fourth card, `Vendo madera.` (her focus since V15), and the session's end counts her new rows (`vendo-madera`, `once-a-quince`,
  `dieciseis-a-veinte`). The order `oven-wood` names its lesson since V14, but `numeros-y-pan` has not begun at `--session 5`, so the delivery is not held
  back: five wood out, four coins in, two loaves bought, as before.
- **r10:** the same 31 items and answers; the villager's turn landed a tick earlier, so the rest of the session log is a tick early (the order-only rule) and
  187 pixels of the dimmed world behind the thank-you panel differ. The panel is the same.
- **V09's own fix** (the skip counts session 1's bottle as the shopkeeper's gift and his key words as taught; `docs/game/README.md`, "The learner's route
  through phase B"): r21's save holds `"keyWordsTaught":true` and the shopkeeper's given bottle (2 bytes fewer), its world digest unchanged; r20, r22 and r23
  captured again are the same.

## By hand (a live run)

What no script reaches: walk and look with the mouse, **E**, **T** and **V** on each NPC (hold V to talk), **Enter** on the closing card and **E** at the lit
fire (sleep, V12c; not while a lesson step waits), the panel's buttons with keys and
the mouse, **Esc** at each level (a recording, a turn thinking, the text box, the panel), **F8** then type then **Esc** (nothing kept) and
**F8** then **Ctrl+Enter** (saved), **F6**, **J** (Tab, the arrows, the wheel, a click on a tab, Enter and Shift+Enter on a row, P), **I**, **C**
(a click on a row), **E** at the workbench, **F1** (scroll it at 150 %), **F5**, **F9**, **F3**, a right click with the book selected, the
wheel and 1 to 0 on the hotbar, and in `--playtest`: **F10** (the post-check at once) and **Esc** on the thank-you screen.
