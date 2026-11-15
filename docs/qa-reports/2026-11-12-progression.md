# QA report: progression (settlement series), 2026-11-12

Branch `feature/settlement`, commit `653ddbb7` (checked out detached in `/home/james/Dev/Vocaterra/.claude/worktrees/qa-settlement`). Area: progression, narrowed to the settlement series (the inn, buying land, the campsite) with the save and world checks that touch it.

Build: `dotnet build Vocaterra.sln -warnaserror -m:4` through `scripts/limited.sh`, succeeded, 0 warnings. The game binary is `src/Vocaterra.Game/bin/Debug/net10.0/Vocaterra.Game`.

Covered:
- Baseline scripted runs, all exit 0: r20-sleep, r30-no-classes, r10-playtest, r01-route (captures in `/tmp/vocaterra-qa/base/`). The r30 row's expectation for the baker is stale (see QA-2026-11-12-progression-5).
- About 60 short game runs in all, each in its own folder under `/tmp/vocaterra-qa/<run>` (the wrapper is `/tmp/vocaterra-qa/run.sh`). This is over the brief's budget of about 25; most runs were one to three lines long. Runs that wrote to a folder are cited by name below.
- Innkeeper Rosa: «no sé», two misses, «No, gracias.» then «sí», «¿Hay una habitación?», «quiero una habitacion por favor» (no accent), «yes please», a bare talk; the room taken before the camp; the room taken on day one then the camp finished; the skip greeting; the locked bed; the bed after a night; a load of a day-two save.
- Day one ending: Luna's «¡Muy bien! Ahora ve a la posada.», the beacon to the inn, the closing line «Buenas noches. Tu cama está allí.».
- Death: before any sleep (wakes at 174,43,188 by Luna's fire), after a night in the bed (wakes at 281,38,231 inside the guest room), after a night at the camp (wakes at 261,37,161 or 263,37,163), and after a load of a camp save.
- Mayor Tomás: the stake (`--talk villager --say "Sí, por favor."`), a purchase with 10 coins, with 9 coins, with no coins, with no plot marked, with no stake, a purchase again after a load, a pending plot across a save and load, the stake on owned land (marks nothing), the beacon to the mayor and to the plot.
- Camp: placed on owned land, refused off the plot, torch lights the ring (`fuego` step), sleep at the bedroll with no torch, pack back (kit returned, camp blocks and sleep places gone), a load after the pack (`campamento` and `fuego` read 0/1), a load with the camp standing (sleep and death wake at the camp).
- Planks: refused in the village (`place madera@292,38,215`), refused on the road (`place madera@234,38,205`), placed in the wild (`place madera@278,37,165`), refused to come back in the wild (`interact 278,37,165`).
- Saves: `--save` and `--load` of the inn room, the owned plot, the pending plot, the camp (placed, packed), the `Mi terreno` steps.
- CLI `eval`: not run (the area is progression; the innkeeper lines were run through the game).

## Bugs

### QA-2026-11-12-progression-1 (minor, game)
- Reproduce (with the QA wrapper's `--needs off --dev --mute --seed 42` added): `--teleport 280,227 --talk innkeeper --say "no sé" --wait 1`. The same happens with two misses: `--teleport 280,227 --talk innkeeper --say "¿Cuánto cuesta el pan?" --say "hola" --wait 1`.
- Expected: a fail-forward is credited to the phrase the learner was asked for at that NPC (`quiero-una-habitacion` at the inn, `quiero-un-terreno` at the mayor), or to nothing. The book's `pedir-agua` (quiero agua) is practised only at the stall.
- Actual: every NPC's fail-forward is recorded on `pedir-agua`. The console says `phrase pedir-agua fail_forward at innkeeper (dont_know)` and the summary says `pedir-agua: ... fail-forward 1`. The same happens for the greeting (`(greet)`) and for the baker's transfer (`phrase pedir-agua transfer at baker (pan)` in the r30 baseline, and `phrase pedir-agua transfer at villager (terreno)` in `l5-noplot`; whether a transfer should credit it is a question for the owner).
- Evidence: `/tmp/vocaterra-qa/i1-nose/stdout.txt` lines 21 to 30; `/tmp/vocaterra-qa/i2-misses/stdout.txt` lines 23 to 28; `/tmp/vocaterra-qa/base/r30-no-classes/stdout.txt` line 190 (the stall's second visit, which is correct).
- Likely file: `src/Vocaterra.Game.Core/Session/GuideSession.cs` line 1425 (`PhraseIds.RequestWater` is used for every `turn.FailForward`, whatever the NPC).

### QA-2026-11-12-progression-2 (minor, game)
- Reproduce (needs on): `--skip-to-session 2 --walk-to 280,227 --walk-to 279,231 --sleep --wait 2 --need health 0 --wait 3 --screenshot <png>`.
- Expected: the wake card's text is readable: «You fainted from thirst and woke where you sleep. Your things wait where you fell.» and the bed's prompt do not overlap.
- Actual: the prompt box `E: sleep until morning (session 4)` is drawn over the middle of the second text line ("You fainted from thirst … ngs wait where you fell"), so the line is partly hidden. In the camp variant the `Your land` label sits under the prompt as well.
- Evidence: `/tmp/vocaterra-qa/d4-death-bed/shot.png`; `/tmp/vocaterra-qa/c7-camp-wake/shot.png`.
- Likely file: the wake card and the sleep prompt's layout (`src/Vocaterra.Game/` HUD code; not checked).

### QA-2026-11-12-progression-3 (minor, game)
- Reproduce: `--teleport 280,227 --walk-to 172,187` (from the inn to the guide's camp, one leg).
- Expected: the scripted walk arrives, as it does in five legs.
- Actual: `walk-to 172,187: gave up at (210.30, 186.70), 37.8 blocks away, after 14400 ticks`. The player stands at y 42 against a trunk, with leaves at 210,43,187 ahead (the target line reads `leaves (210, 43, 187)`). The legs `--walk-to 240,222 --walk-to 226,222 --walk-to 215,205 --walk-to 200,195 --walk-to 172,187` all arrive (`/tmp/vocaterra-qa/w1-village-to-camp/stdout.txt`).
- Evidence: `/tmp/vocaterra-qa/w2-direct-walk/stdout.txt` line 18, `/tmp/vocaterra-qa/w2-direct-walk/shot.png`.
- Likely file: `src/Vocaterra.Game.Core/Scripted/VoxelScript.cs` (the walk-to route). I have not checked whether a player who walks by hand is blocked at that tree; only the scripted walker gave up.

### QA-2026-11-12-progression-4 (minor, game)
- Reproduce: `--skip-to-session 2 --give estaca --mark-plot 266,165` with ten `--give moneda`, then `--talk villager --say "¿Cuánto cuesta el terreno?" --say "Quiero un terreno, por favor." --say "sí"`.
- Expected: a «sí» that starts a class is a verdict that agrees with what happens, or it is not taken as a class start.
- Actual: the turn reads `"sí" -> unknown, not understood, 100 %` and in the same turn `lesson personas started` (step 2 of 22). The panel shows «You: sí / unknown, not understood, 100 %» under the Personas heading. The console does not print which offer line stood before the «sí», and the panel has 11 earlier lines hidden, so I could not confirm the offer was on screen.
- Evidence: `/tmp/vocaterra-qa/l2-buy/stdout.txt` (the turn after `plot_bought`); `/tmp/vocaterra-qa/l2-buy/session/session.jsonl` lines 31 to 33; `/tmp/vocaterra-qa/l2-buy/shot.png`.
- Likely file: `src/Vocaterra.Game.Core/Language/Lessons/LessonRunner.Classes.cs` (`OnClassTurn`) and the turn presenter's verdict line.

### QA-2026-11-12-progression-5 (minor, game; scripts and docs)
- Reproduce: `scripts/scripted-runs.sh <OUT> r30-no-classes`.
- Expected (per the row in `docs/game/scripted-runs.md`): the oven order is delivered at the first talk with Luz, and two loaves are bought (`paid.png`).
- Actual: the run exits 0, but the baker only offers the order (`order_offered at baker (oven-wood)`), the player never takes it, there is no `order_delivered`, and Luz answers `Necesitas más monedas.`. The order has been a task the player takes on since issue #54 (`game/objectives/oven-wood.json` says so). The run passes on its exit code and its row is stale.
- Evidence: `/tmp/vocaterra-qa/base/r30-no-classes/stdout.txt` (around `turn 14`); `/tmp/vocaterra-qa/base/r30-no-classes/session/session.jsonl` line 165 to 167.
- Likely file: `scripts/scripted-runs.sh` (the r30 line, which needs a «sí» at the baker's offer) and the `docs/game/scripted-runs.md` row.

## Observations

- **The day-one fire sleep before the inn.** `--sleep` (the scripts' aid) sleeps at Luna's lit fire before the room and before the water (`/tmp/vocaterra-qa/f1-fire-first/stdout.txt`, `sleep: --sleep: session 1 ends at the fire`, objectives `agua 0/1`). `VoxelGame.cs` line 1394 says the aids sleep regardless of the day-one gate, and `SessionComplete` needs `inn-room` too, so the player's own E does not offer this. Scripted runs can reach states the game does not allow; read their sleep lines with that in mind.
- **Death cause from `--need health 0`.** The death line says `fainted from thirst` (`player_died (sed)`) when thirst is 50, because with no drive at zero the cause falls to the lower of thirst and hunger (`VoxelSession.Death.cs`, `DieIfDead`). Only the aid does this; in play a drive at zero sets the cause. Do not read the death wording from an aid run.
- **`--teleport` lands on the top surface.** `--teleport 279,231` put the player on the inn's roof (y 43), and `--teleport 263,165` on the tent (y 40). My first sleep and death checks at those columns read `no fire is lit` and `wherever the player stood` for that reason. Use `--walk-to` or a free cell beside the object.
- **Stake and kit are not consumed.** After the purchase the save's bag keeps the `Stake` (`/tmp/vocaterra-qa/l2-buy/game.save`, inventory: Book, WaterBottle, Stake, CampKit). The stake marks nothing once land is owned (`land: the stake at 266,165 marked nothing: the plot is owned already`, `/tmp/vocaterra-qa/g2-stake-owned/stdout.txt`). Whether it should stay is a design call.
- **Stake given for a land request.** Asking the mayor for land with no stake gets `Primero necesitas una estaca. Toma.` and the stake (`/tmp/vocaterra-qa/l4-nostake/stdout.txt`). That is a helpful reply, and the stake is free.
- **Planks placed in the wild stay forever unless the player owns land.** `interact 278,37,165` gives `refused by the land rules (planks come back only on the player's own land)` (`/tmp/vocaterra-qa/p4-wild-takeback/stdout.txt`). This is the rule as written; a plank dam in the river is permanent.
- **`--mark-plot` skips the stake's cost.** With the dev aid, a plot is marked and bought with no stake in the bag (`/tmp/vocaterra-qa/q1-nostake-plot/stdout.txt`, `plot_bought`). In play the stake is needed to mark, so this is a test-aid path only. The mayor's `no_stake` miss is therefore not reached by any aid run.
- **Innkeeper and bread.** «¿Cuánto cuesta el pan?» gets `La habitación es gratis. ¿Quieres una habitación?` (`/tmp/vocaterra-qa/i2-misses/stdout.txt` line 22). It answers about the room, not the bread. The baker is the one who sells bread.
- **The stall's «quiero agua, por favor» at its offer.** On the r30 baseline's second visit the shopkeeper answers `Te entiendo. ¿Quieres comprar algo?` and the verdict is `unknown, not understood, 100 %` (`/tmp/vocaterra-qa/base/r30-no-classes/stdout.txt`, turn 13). The sentence is the book's own `pedir-agua`; it is not the offer's answer, but the two lines disagree.
- **English at the inn.** «yes please» gets `No te entendí. ¿Me lo dices otra vez?` and no room (`/tmp/vocaterra-qa/i5-english/stdout.txt`). That is fine.
- **Room first, camp after (verified working).** Taking the room on the first visit, then the camp and the water, ends the day with `session complete: innkeeper says: Buenas noches. Tu cama está allí.` and the beacon to the bed; Luna's «ve a la posada» is not said (`/tmp/vocaterra-qa/e5-room-first-water/stdout.txt`, `beacon_set (cama at 279,37,231)`). The water bottle is needed: `--give botella` works; `--give WaterBottle` says `no such item`.
- **Camp facts (verified working).** The camp's sleep places: `camp.bed` (radius 2) and `camp.fire` (radius 4, lit). The bedroll alone sleeps the player (`/tmp/vocaterra-qa/c11-bedroll-no-torch/stdout.txt`, `ends at your camp`), and death wakes at 263,37,163.

## Not covered

- **Right-click use of the stake and the camp kit.** Only the dev aids (`--mark-plot`, `--own-plot`, `--place-camp`) ran. The input path (`Input/WorldInput.cs`) is not covered.
- **A full bag (`no_room`).** Filling 40 slots needs too many `--give`s; not run.
- **`no_stake` in play.** Unreachable through the aids (see the observation above).
- **Runs r40-inn, r41-land and r42-camp.** The settlement plan's Y13 names them; they are not in `scripts/scripted-runs.sh` at this commit, so there was nothing to run. The Y13 close-out is not on this commit.
- **Capture comparison.** `compare-runs.py` was not run: I captured the baseline on this commit but had no earlier capture to compare with.
- **Beacon direction.** I checked the arrows by their distance labels (`terreno 60 blocks` at 291,220; `Alcalde 65 blocks` at 259,166) and by the place of the plot's posts on screen. I did not walk to the plot to confirm the arrow points at it.
- **The chest's cell after a death at the bed.** The console puts it at 279,38,231, which is the bed's own column. I did not check it visually.
- **Speech.** No `--speak` or `eval --wav` run for the inn or the mayor lines.
- **Full test suite.** Not run; the area is progression, not `all`.
- **The CLI `eval`.** Not run for the settlement lines.
