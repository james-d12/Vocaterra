# Y13 report: close-out (the runs and the documents)

(The agent's report, 2026-11-12, condensed by the orchestrator.)

Branch `Y13-close-out`, HEAD `2c44fcb5` (six commits on `653ddbb7`); worktree `/home/james/Dev/Vocaterra/.claude/worktrees/y13`. Gate
green: build 0 warnings, format, validate, A2 lint clean; `scripts/test.sh full` 11,457 passed, 3 skipped (scripts and docs only).

## The scripted runs (seed 42, `--dev --mute`, each ends with a shot and a save)
- `r40-inn`: the first lesson, CAMP, the shopkeeper's bottle, the river; `agua 1/1` completes the checklist; `guide says: ¡Muy bien! Ahora
  ve a la posada.`, beacon `posada 46 blocks`; Rosa's room (`innkeeper.room`), the closing card, the walk to 279,231, `--sleep`:
  `session 1 ends at the inn, session 2 begins`. Shots: beacon, talk, ending, bed, the morning (with «Mi terreno» 0/1 ×4).
- `r41-land` (`--skip-to-session 2 --mark-plot 266,165`): «Quiero una estaca, por favor.» → `villager.give_stake`; the pending outline
  and HUD line; «Quiero un terreno, por favor.» with no coins → «Necesitas diez monedas. Vende madera a Luz.»; after ten coins the same
  line buys it (`plot_bought (261,160)`, the kit), «Your land». Shots: stake, pending, cantpay, bought, owned.
- `r42-camp` (`--needs on`): `--own-plot 261,160 --give equipo --place-camp 263,161,north`; a torch at 262,38,161 → `fire_lit`;
  `--set-time 23`, `--sleep` → `ends at your camp`; a berry planted at 268,36,167; a death from outside → `woke at 263,37,163`; a held
  interact packs the camp in 45 ticks. Shots: day, night, wake (dark at the held 23:00).
- `r43-stake-refused` (added: `--mark-plot` acts once at launch, so the village refusal needs its own run): `land: the stake at
  285,206 was refused: Protected, Village` (the hint has faded by the shot; the console is the record).
Each run captured twice and compared: all four `same`.

## Documents
`scripted-runs.md` (rows r40 to r43, 32 runs, the exact list, a Y13 bullet); `architecture.md` (a section "The settlement series (Y00
to Y13): where to look", a table of data and code); `running.md` (the S17 route now ends at the inn's bed; a "Day one, day two and the
camp, scripted" block; sleeping at three kinds of place and the console's wording); `lessons.md` (the stale sleep wording and r20, the
note on the ledge, a section "Day two offers land"); `playtest.md` (ends at the room; no sleep at any place); `saves.md` (an overview of
version 8's `systems`, checked against `SystemsSave`); `docs/game/README.md` (no tent; the inn, the camp and the land);
`src/Vocaterra.Game.Core/README.md` (three `firering.player` statements). No change needed in `content/README.md` or
`docs/process/documentation.md`. Plan and history texts left as they are. CLAUDE.md's fixture line is right (`save-v8.json`, the only
fixture). For the orchestrator: `docs/README.md`'s settlement-plan row still says "first slice, on `feature/settlement`".

## Known gaps (for the open items)
«mi campamento» unanswered in directions (no point or label for the placed camp); one plot and one camp; no meals, comfort or warmth at
the camp, no meals at the inn; the camp is no destination for the dialogue worker or navigation; planting needs the needs on; the
take-back hint on scene planks (Y09); no plural key for the price (Y10); the outline's size limit and light (Y10); no reach limit for
the stake beyond the aim (Y10); meadow origins stricter than "centre within 20" (Y09); `patience` at the inn counts every line (Y06);
core gaps «Quiero dormir aquí», «¿Puedo dormir aquí?», «Quiero comprar un terreno», «Quiero este terreno»; the mayor's «Toma la estaca»
with nothing given while one is held, no `no_room` at the stake offer, a miss ends the offer (Y11); the tent's steps can be climbed, the
repeated "bag full" hint (Y12); Rosa's and the mayor's speech wait for the owner's listening; the two fuzz flakes (the coverage floor,
`dTc3W4dw5Cg2`); no scripted step uses the stake; `--sleep` sleeps anywhere while a fire is lit (Y05).

## Bugs
None. An observation: a death on the camp's wake cell itself puts the chest there and wakes the player on the next free cell (noted in
running.md as intended).
