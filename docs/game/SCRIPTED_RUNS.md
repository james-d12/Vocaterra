# Scripted runs: the front-end's regression set

Sixteen scripted runs of the game that between them pass through every flow `VoxelGame` drives: the walk and the camp, talking and typing,
the three NPCs, the lessons, the book, the inventory and crafting panels, the check battery, a whole playtest, the mic check, a saved report,
the view over the shoulder, greyscale and dusk, the interface text size and a spoken line. Run them before a change to the front-end and
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
| r10-playtest | A whole playtest: the mic check, the pre-check, the lesson, the camp, the three NPCs, the transfer, the fire at the player's camp, the post-check that starts by itself, the playtest save and the thank-you screen | `--playtest --seed 42` then the pre-check answers, `--lesson-run first-words`, the camp steps of r01 (from `--walk-to 172,187` to the `--place`), `--talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" --talk villager --say "¿dónde está el río?" --walk-to 234,222 --interact 233,34,222 --look 180,-14 --talk guide --say "sí, por favor" --leave --walk-to 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1`, the post-check answers (below), `--screenshot "$W/shot.png" --frames 60` |
| r11-mic | The mic check alone: the worker hears a synthesized "hola" | `--seed 42 --mic-check --screenshot "$W/shot.png"` |
| r12-report | A scripted report saved at once: the picture, the Markdown, the toast | `--seed 42 --mute --report "the bridge has a gap" --screenshot "$W/shot.png" --frames 30` |
| r14-third | The view over the shoulder: the player's figure, the camera kept in front of walls | `--seed 42 --mute --third-person --look 120,-5 --screenshot "$W/shot.png" --frames 60` |
| r15-grey-dusk | Greyscale (blocks whose noun is not known are grey) and dusk (the lamps, the light on the arm) | `--seed 42 --mute --greyscale --dusk --teleport 222,222 --look 80,-16 --screenshot "$W/shot.png" --frames 60` |
| r16-text-scale | A small window at 150 % text: the canvas, the book's lessons page laid out on it | `--seed 42 --mute --size 800x600 --text-scale 150 --lesson-run first-words --book-page lessons --screenshot "$W/shot.png" --frames 30` |
| r17-speak | A spoken line (synthesized, then heard by the recognizer) at the stall | `--seed 42 --talk shopkeeper --speak "quiero agua" --screenshot "$W/shot.png"` |

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

## Comparing two captures

Mask before comparing: every number followed by `ms` (the draw and frame timings, the turns' latency), the wall-clock times and dates, and
the session log's `wall_seconds`, `*_ms` and latency fields. Measured on 2026-10-26 by capturing the unchanged code twice:

- **Exact (byte for byte after the masks, screenshots and saves included):** r01, r07, r08, r09, r11, r12, r14, r15. r09 writes a save, so
  it also checks that a save comes out the same.
- **Order only:** r02, r03, r04, r05, r06, r10, r16, r17, every run with a turn for the worker. When a turn is applied depends on how fast the
  worker thread evaluates it (a busy machine, the models in the disk cache), so these runs land their turns a few ticks apart from one capture
  to the next, and every tick count, stored time, state digest, save and picture after that moves with them. r03 and r04 matched in two
  captures on a busy machine and moved by a tick on an idle one. Compare only `stdout.txt` and `session.jsonl`, with every number and digest
  masked, and the session log's lines as a set (a turn's line can land before or after an event line of the same frame). That they move at all
  is a finding: a scripted run does not freeze the world exactly while a turn is out.

## By hand (a live run)

What no script reaches: walk and look with the mouse, **E**, **T** and **V** on each NPC (hold V to talk), the panel's buttons with keys and
the mouse, **Esc** at each level (a recording, a turn thinking, the text box, the panel), **F8** then type then **Esc** (nothing kept) and
**F8** then **Ctrl+Enter** (saved), **F6**, **J** (Tab, the arrows, the wheel, a click on a tab, Enter and Shift+Enter on a row, P), **I**, **C**
(a click on a row), **E** at the workbench, **F1** (scroll it at 150 %), **F5**, **F9**, **F3**, a right click with the book selected, the
wheel and 1 to 0 on the hotbar, and in `--playtest`: **F10** (the post-check at once) and **Esc** on the thank-you screen.
