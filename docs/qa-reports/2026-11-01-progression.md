# QA report: progression (village sessions 4 to 10), 2026-11-01

Branch `feature/village-followups`, commit `9dfa08b` (worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`; read-only git, nothing committed). Build: `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, exit 0.

Covered: 20 game runs (folders `/tmp/vocaterra-qa/progression-*`, each with `stdout.txt`, `stderr.txt`, `session/session.jsonl`, screenshots and saves). No CLI `eval` runs (the language area belongs to the other tester). The runs:
- **The route per session**, from `--skip-to-session n`: the objective and beacon at the start, the lesson by `--lesson-run`, free talk once with each NPC of the lesson, `--sleep` into the next session, then the next session's objective and inventory. Sessions 4 (`el-pueblo`), 5 (`numeros-y-pan`), 6 (`gente-del-pueblo`), 7 (`como-es`), 8 (`ayer-en-el-pueblo`), 9 (`manana-en-el-pueblo`) and 10 (`el-mercado`) were all completed in these runs, sessions 6 to 8 and 9 to 10 in chained runs.
- **The coin path:** `--skip-to-session 7`, the farmer's refused order at 0 coins, the water order (three apples), the sleep, then session 8's inventory.
- **Orders:** `oven-wood` with the wrong NPC (smith: no delivery), with the right NPC after the lesson (five wood out, four coins in), and a second talk to the baker (no second delivery). The session 10 market: `vendo madera` to the smith (four coins in), `Vendo manzanas.` to the farmer, `Vendo madera.` to the baker with no wood left.
- **Saves:** a mid-lesson save and load (session 5 to 6), a save with five wood before the oven order and its load, a save made right after a sleep and its load.
- **`--lesson-skip`** on all 28 steps of `gente-del-pueblo`, and **`--check pre`** at `--session 7` (first screen only: `check: pre, 63 items`, which matches the battery table in `docs/HANDOVER_VILLAGE_PHASE_B.md`).
- The known items of handover section 9.3 were only re-checked where the same behaviour showed up again.

## Bugs

### QA-progression-1 (major, game)
- Reproduce: `--seed 42 --mute --skip-to-session 5 --teleport 309,216 --interact 311,38,214 --interact 311,39,214 --interact 311,40,214 --interact 311,38,218 --interact 311,39,218 --wait 1 --screenshot <dir>/shot.png --frames 20 --save <dir>/wood.save`, then `--seed 42 --mute --load <dir>/wood.save --talk baker --say "Quiero dos panes, por favor." --screenshot <dir>/shot2.png --frames 20`.
- Expected: the session 5 order `oven-wood` (five wood for four coins) is still open after the load, and the baker takes the wood and pays. A save and load should not change the session's objective or the order.
- Actual: the load says `now session 6`, but the HUD line and the objective still read `Session 5: go to the bakery ... to start the lesson "Números y pan"`, with the beacon `Luz 29 blocks`. The baker does not deliver: `Necesitas más monedas.` (turn 1), no `order_delivered`. The learner keeps five wood and two coins for good, so the bread purchase and the coin path cannot happen. A mid-lesson save (Observations) keeps its lesson; this one loses its order.
- Evidence: `/tmp/vocaterra-qa/progression-wood-load/after-load.png` (HUD "Session 6", objective "Session 5"); `/tmp/vocaterra-qa/progression-wood-load/stdout.txt` line 21 (`wood 5`, `bread 0`); `/tmp/vocaterra-qa/progression-wood-load2/stdout.txt` lines 29 to 30 (`Necesitas más monedas.`); `/tmp/vocaterra-qa/progression-wood-save/stdout.txt` line 54 (`saved in session 5`). Compare `/tmp/vocaterra-qa/progression-order-5/stdout.txt` (no save: the order delivers after the lesson, line 108 onwards).
- Likely file: the load path, `src/Vocaterra.Game/Flow/SessionStart.cs` (a load begins the next session), and the order's session gate in `src/Vocaterra.Game.Core` (the objective and `deliverTo` by session).
- Owner: game.

### QA-progression-2 (major, game)
- Reproduce: `--seed 42 --mute --skip-to-session 8 --sleep --wait 2 --inventory --screenshot <dir>/pre.png --frames 20 --save <dir>/pre.save`, then `--seed 42 --mute --load <dir>/pre.save --wait 1 --screenshot <dir>/post.png --frames 20`.
- Expected: a save made at the start of session 9 (after the sleep) loads as session 9, with session 9's objective and its lesson `manana-en-el-pueblo` still to do.
- Actual: the save says `saved in session 9, now session 10`. Session 9 is skipped: the HUD says `Session 10`, but the objective still reads `Session 8: go to the square and talk to the villager to start the lesson "Ayer en el pueblo"`, and the lessons `ayer-en-el-pueblo`, `manana-en-el-pueblo` and `el-mercado` all read `not started`. The load rule that moved session 5 to 6 in QA-progression-1 moves a save taken at the start of a session by two.
- Caveat: the sleep here is forced with `--sleep` before lesson 8 was started (the test aid does not check the session's work). The double advance does not depend on that: it happens for any save made at the start of a session. QA-progression-1 and -2 share the load rule; read them together.
- Evidence: `/tmp/vocaterra-qa/progression-sleep-save/after-sleep.png` (HUD "Session 9", objective "Session 8"); `/tmp/vocaterra-qa/progression-sleep-load/after-load.png` (HUD "Session 10", objective "Session 8", beacon "Aldeano 174 blocks"); `/tmp/vocaterra-qa/progression-sleep-load/stdout.txt` line 2 (`saved in session 9, now session 10`) and lines 36 to 38 (the lesson states); `/tmp/vocaterra-qa/progression-sleep-load/session/session.jsonl` line 1 (`session_start` session 10, `skipped_to` 8).
- Likely file: `src/Vocaterra.Game/Flow/SessionStart.cs` (the load's next-session rule), and `src/Vocaterra.Game.Core/Session/VoxelSession.Sleep.cs` (the objective after a sleep).
- Owner: game.

### QA-progression-3 (minor, game)
- Reproduce: `--seed 42 --mute --skip-to-session 10 --talk farmer --say "Vendo manzanas." --screenshot <dir>/shot.png --frames 20`.
- Expected: a clear answer. The farmer sells apples and buys only berries (`farmer.buy_fruit` is the `berry` item in `content/game-v1/game/host-events.json`), so the refusal is right, but the reply should not praise the sentence and then refuse it.
- Actual: `Sí, «vendo manzanas». ¡Muy bien!` (the recast line `feedback.recast.1` in `content/game-v1/dialogue/dialogue.json`, quoting the learner's sentence with praise), then `No, gracias. Solo compro fruta.` (`farmer_wont_buy.1`). The same sentence is marked `understood, 40 %` (the M67 item of the handover queue, known).
- Evidence: `/tmp/vocaterra-qa/progression-market-10/stdout.txt` lines 65 to 67.
- Likely file: `content/game-v1/dialogue/dialogue.json` (`feedback.recast.1` and `farmer_wont_buy`, the offer's answer). The recast's choice is the core's.
- Owner: game (the lines and their choice); the 40 % is core (M67).

### QA-progression-4 (minor, game)
- Reproduce: `--seed 42 --mute --skip-to-session 5 --teleport 309,216 --interact 311,38,214 --interact 311,39,214 --interact 311,40,214 --interact 311,38,218 --interact 311,39,218 --talk baker --say "Quiero dos panes, por favor." --screenshot <dir>/shot.png --frames 20`, and at the lesson's wood step leave the wood steps to the player.
- Expected: a learner who already holds the five wood is not sent to gather them again, and the baker's refusal gives the real reason.
- Actual: the lesson's do step `n4-wood` waits for `item_picked_up` (`step 10 do (n4-wood) step_started (item_picked_up)`), so a learner who already has the wood must pick up more. The order is then held until the lesson's `n4-back` step (by design, V14), and meanwhile the baker says `Necesitas más monedas.` (you need more coins): the learner has two coins, the bread costs four, and the order that would bring the four coins is the one held back. The reason given is the wrong one.
- Evidence: `/tmp/vocaterra-qa/progression-order-5/stdout.txt` lines 51, 75 to 76 and 96.
- Likely file: the do step `n4-wood` of `numeros-y-pan` in `content/game-v1` (the lesson's steps), and the baker's `cant_pay` line choice.
- Owner: game.

## Observations

- **Known, unchanged (handover 9.3 D(a), free talk):** the NPCs do not understand lines the learner has just used in a lesson, or a greeting. Shopkeeper `Hola` (`/tmp/vocaterra-qa/progression-r7-route/stdout.txt` lines 173 to 175), shopkeeper `Hola, ¿qué tienes?` (`progression-r5-route/stdout.txt` line 173), fisher `Fuimos al río.` (`progression-chain-6-9/stdout.txt` around line 462; the villager understood the same line in its lesson), smith `Tengo que ir al río.` (around line 466), fisher `Iré al río.` (`progression-chain-9-10/stdout.txt` line 150), shopkeeper `¿Puedo pagar con manzanas?` (line 324).
- **Known, unchanged (9.3 D, the 40 %):** `Mañana tendré pan.` is understood (`state_have`) at 40 % (`progression-chain-9-10/stdout.txt` line 139); `Vendo manzanas.` at 40 % (QA-progression-3).
- **Known, unchanged (9.3 E, the Lessons page):** the book opens at its top (`progression-s4-start/shot.png`), so the `next:` line of the next lesson is never on screen. Not checked further, see Not covered.
- **Known, unchanged (9.3 F, name plates):** `Herrero` and `Aldeano` plates over figures (`progression-chain-9-10/s9-start.png`, `progression-r5c-coins/s5-paid.png`).
- **Known, unchanged (9.3 G):** the test-aid skip reaches session 7 and 8 with the coins and goods the earlier orders leave (no coin, three apples, two loaves, the axe). Its session log marks the skip (`skipped_to 8`) even in a run loaded later (`progression-sleep-load/session/session.jsonl` line 1).
- **Coin path (checked):** the coins add up as the plan says: `fruit-for-mar` +2, `oven-wood` +4, two loaves −4, the axe −2 (its wood and coins were not delivered in an automated route, see Not covered). So a learner reaches session 7 with 0 coins, and `/tmp/vocaterra-qa/progression-r7-route/inv8.png` at session 8 shows the book, three apples, the axe and two loaves, and no coin. A learner who skipped the axe carries two coins into session 9 (`progression-chain-6-9/s9-start.png`), which is consistent.
- **Market (checked):** `/tmp/vocaterra-qa/progression-market-10/shot.png`: four wood sold to the smith for four coins (`smith.buy_wood`, `¡Gracias! Aquí tienes tus monedas.`), no wood left, and the baker answers `No tienes madera.` to a sale with none. Fruit: see QA-progression-3.
- **A lesson step credits a refused sale:** `Quiero vender madera` with no wood (step `k2-sell`) is marked `done`, although the baker answers `No tienes madera.` (`progression-chain-9-10/stdout.txt` lines 199 to 204). The Spanish is right, so the step is not wrong, but nothing was sold and the learner is not told to bring wood first.
- **A lesson step accepts a claim the bag contradicts:** `Tengo cuatro monedas.` at step `n5-coins` is accepted (`state_have`) with two coins in the bag (`progression-r5-route/stdout.txt` line 101).
- **A lesson done by skipping:** `--lesson-skip` on all 28 steps of `gente-del-pueblo` completes it as `done 0, skipped 28`, and the Lessons page then shows it as completed (`progression-lesson-skip/stdout.txt` lines 124 to 126). The record does not show that nothing was done.
- **The lesson started by a talk does not change the objective:** after `--talk baker` starts `numeros-y-pan`, the HUD still reads `Next: talk to the baker for the lesson "Números y pan"` (`progression-order-5/first-talk.png`). The lesson does go on at the baker, but the learner is not told it has begun.
- **A mid-lesson save and load keeps the lesson:** saved at step 2 of `numeros-y-pan` in session 5, the load (session 6) resumes at step 2 on the baker's talk (`progression-mid-load/stdout.txt` lines 16 to 22). The session counter still moves on, as in QA-progression-1.
- **The sleep after the last lesson:** `--sleep` in session 10 goes on into session 11, and the stdout gives no line that the course is over (`progression-chain-9-10/stdout.txt` line 328). I did not look at the screen for a closing card. Possibly intended.
- **The `--sleep` test aid** starts the next session without the session's objective done (QA-progression-2 caveat). In play, E at the fire is gated by `RestDue` (`VoxelSession.Sleep.cs`), so this is the aid only.
- **A fixed answer to a total:** the farmer answers `¿Cuánto es todo?` with `Quince manzanas, quince monedas.` whatever the learner holds (three apples in the session 10 bag, `progression-chain-9-10/stdout.txt` line 314). The same fixed line is in `docs/game/SCRIPTED_RUNS.md` r22.
- **A log label:** the farewell `Adiós` is logged as `turn 8 (fisher, villager-open)` (`progression-r4-route/stdout.txt` line 153), so the prompt id in the log is the villager's. Log only.
- **Cosmetic:** the first `--shot` before any tick (`progression-r4-route/start.png`) is a partly black frame (a black band above and to the right). Shots after `--wait 1` are whole.

## Not covered

- **The Lessons page's `next:` line:** no launch flag scrolls the book's left list (`--book-detail` scrolls the right page only), so the `next:` line of sessions 7 to 10 was not seen.
- **The axe order in session 6 with real wood:** the trunks of the session 6 orchard are unknown (`--interact 311,41,214` and `311,41,218` did not gather). Without wood the axe (two wood and two coins) is not delivered in a played route, so the coin path reaches session 7 at 0 coins only through `--skip-to-session 7`, not through a played session 6.
- **The full check battery:** only the first screen of `--check pre` at session 7 (63 items, item 1 "A baker asks what you bought...", `progression-check-7/check.png`). No answers were given, so no score was checked. `--check post` was not run.
- **The oven wood by a lesson's own do step:** the automated lesson-run skips every do step that needs a walk to a tree (`the step 'n4-wood' has nothing to walk to`), so the wood was only given by `--interact` at the bakery's orchard.
- **Loading the autosave file:** I loaded the saves the runs wrote at their end and the mid-lesson save; the autosave in a session folder was not loaded.
- **Saves and loads in session 10 and beyond:** the save after the last lesson was not loaded.
- **Speech and spoken lines:** not in this area (the speech run belongs to another tester).
- **Process cleanup:** I started one run with the wrong arguments (a wrapper called without a name). It was waiting on the heavy-command lock; I killed it before it ran a game and deleted its folder. No process of mine is left running (checked with `pgrep` before this report).
