# I05a report (the stat consumers)

The agent's report, saved by the orchestrator (2026-11-16). Sonnet 5.5 at medium effort; brief `i05a-stat-consumers.md`.

Branch `I05a-stat-consumers`, commit 0543d8b6 (one commit), worktree `.claude/worktrees/i05a`, clean, not pushed.

## Gate

- Build with -warnaserror: 0 warnings. Format clean (after fixing import order in `GameData.cs` and `GameDataValidator.cs`). Content
  validate passes.
- `scripts/test.sh full`: 11,977 passed, 0 failed, 3 skipped (+72 against 11,905): Game.Core 4,012, Game 1,156, Host 189, Nlp 4,168,
  Nlp.Es 2,309, Fuzzing 126, Golden 17.

## Built

- **Stats:** `Stats/StatRules` holds one `StatRule` per key (sum or factor, range per item, whole or not); contributors go through
  `StatRules.Contribute`, readers through `StatRules.Read`. `EquipmentStats` uses `Contribute`. `WalkSpeed.Permille` is the one clamp
  (100 to 2,500 permille). `StatKeys` gains `WalkSpeed`, `HungerDrain`, `ThirstDrain`, `MaxStamina`, `Armour`.
- **Validator:** `EquipmentValidator` checks each item's stat against its key's range; open keys such as `light` have none.
- **Needs:** `PlayerNeeds.Stats` is a `Func<StatSheet>` pointed at the simulation's sheet, read live (no save field, no digest change).
  `Step` multiplies `RateFactor` by the hunger or thirst factor (sleep stays 1). `Effects` adds `maxStamina` to the ceiling. `NeedsHud`
  scales the stamina bar when the ceiling is above 100.
- **Walk:** `PrepareTick` multiplies in the sheet's `walkSpeed` beside the needs', the cold's and the sprint's.
- **Damage:** `DamageCause` is a `readonly record struct` holding a data id (`DamageCause.Drowning` still compiles). New `Damage/`:
  `IDamageModifier`, `ArmourDamageModifier`, `DamageModifiers` (by id), `DamageData`, `DamageCauseRule`, `DamageValidator`, `ModifiedHealth`
  (decorates `IPlayerHealth`). `GameData.Damage` and `WithDamage`; `GameDataValidator` runs `DamageValidator`. The session wraps its health
  in `ModifiedHealth`.
- **Content:** `game/damage.json` (drowning, `modifiers: []`), `schema/damage.schema.json`, a provenance row; the content loop rehashed the
  bundle and `save-v10.json` has the new hash (2c59dfb6 to 9814f2d4).
- **Docs:** architecture.md (each key's reader, the damage pipeline, making armour apply to a cause), content/README.md ("Stat keys and
  damage", the `damage.json` row).

## Keys

| Key | Unit | Items | Range per item |
| --- | --- | --- | --- |
| `walkSpeed` | factor, 1 = no change | multiply | 0.5 to 1.5 |
| `hungerDrain`, `thirstDrain` | factor on that need's fall | multiply | 0.25 to 2 |
| `maxStamina` | stamina points (meter 0 to 100) | sum | 0 to 100 |
| `armour` | whole-number rating | sum | 0 to 100 |
| `warmth` | tenths of a degree | sum | -2,000 to 2,000 (range added here) |

Factors multiply so that two items of 1.1 give 1.21, never a sum past 100%.

- **Armour:** reduction = min(0.8, 0.04 × armour), applied as round(amount × (1 − reduction)), never below 1 for a positive hit (10 armour
  takes 40%; 20 or more is capped at 80%).
- **Stamina:** ceiling = max(MinimumMaximum, 100 − stage penalties + maxStamina), so it can pass 100.
- **Walk speed with `--needs off`:** it applies (gear, not a need; nothing worn gives 1, so no baseline moves). The cold stays off with
  needs off, as I06 chose; the drains and stamina follow the needs, so they are off too.

## Existing tests and files changed

- `GearItems`: `Ring`'s `walkSpeed` 0.05 (written as a sum) is now 1.1 (a factor); new `Anklet` (1.2), `Flask` (hungerDrain 0.5), `Gourd`
  (thirstDrain 0.5).
- `NoLiteralTextTests`: allow-list entries for `DamageValidator.cs` and `StatRule.cs` (content-author problem text, as other validators).
- `save-v10.json`: the bundle hash only. `manifest.json`, `provenance.json`: the damage entry and the rehash.

## Not fixed

- An unknown cause passed to `ModifiedHealth.Damage` at run time passes through unmodified; it is refused at load only.
