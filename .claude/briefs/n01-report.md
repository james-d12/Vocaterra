# N01 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `N01-needs-model`, final commit `770d69a2`, on feature/adaptive_design at 558ed095.

- Data: `content/game-v1/game/needs.json`, `content/schema/needs.schema.json`; `GameData.Needs` (`NeedsData`), `NeedsValidator` (refuses a food that is
  no item, a non-positive rate, stages out of order). Regrowth, gifts and the roast apple are optional parts for later.
- Model: `src/Vocaterra.Game.Core/Needs/` (`PlayerNeeds`: `Step(ticks, activity)`, `Eat`, `Drink`, `Sleep`, `Collapse`, `Die`, `Wake`, `StageOf`,
  `HealthStage`, `Effects()`, `Capture`/`Restore`; `NeedsData`, `NeedsValidator`, `NeedKinds`). Stages and effects computed, never stored.
- Wiring: `Session/VoxelSession.Needs.cs` steps once per world tick while `PlayPauses == None` (set each frame by `VoxelGame.PlayPausesNow`);
  raises `need_stage` (hambre, sed, sueño, salud). Seven new world events in `WorldEvents.All`; only `need_stage` raised.
- Save v6: optional typed `Systems` record on `SaveDocument`, its `Needs` entry the five meters and two timers (`effortFreeSeconds` added for an
  exact round-trip); not in the digest; out-of-range values clamped, a non-number refused. `docs/game/saves.md` updated.
- Flags `--needs on|off`, `--set-need <need> <value>` (repeatable); `docs/game/running.md`. `scripts/scripted-runs.sh` adds `--needs off` to every run.
- Values chosen: gathering 0.0278 stamina per gather tick (a minute by hand about 10 s of sprint; the axe a quarter); a night or collapse never
  lowers a drive below 10 and leaves one already below 10; at full hunger (or thirst) the food or drink is kept, cooked food too; meters change
  continuously; a forbidden sprint counts as walking; a thirst of 0 caps stamina's maximum.
- Stage lines are keys only (`need.hunger.1` ...); N05 needs their Spanish: Tengo un poco de hambre / Tengo hambre / Tengo mucha hambre / Me muero
  de hambre, the same for sed and sueño, Estoy débil / Estoy muy débil.
- Deviations: no v5 fixture exists (v1, v4 and a synthetic v5 tested); the night still passes through `SaveGame.Sleep` unchanged (`SleepTests`:
  "a sleep equals a load"), so N03 applies `PlayerNeeds.Sleep` on both paths; rest counts only at the player's own lit fire (the forge left to
  N02); sprint is not derived in the session (N02).
- Checks: `--set-need thirst 25.2 --walk-to 176,186 --wait 8` raises `need_stage (sed)` with needs on and nothing with needs off. Scripted runs:
  r01, r09, r20, r21 differ only in save version 5 to 6 and +131 bytes.
- Tests: 9019 -> 9157 passed, 3 skipped (with models); CI's case 9116 passed, 45 skipped. One ContentSchemaFuzzTests failure (seed
  `41s2qgWEC-M9`) the orchestrator reproduced on the base too: planned as M81.
