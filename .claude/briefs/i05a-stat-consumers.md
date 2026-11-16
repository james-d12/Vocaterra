# Brief: I05a, the stat consumers

Sent 2026-11-16 to a general-purpose agent, Sonnet 5.5 at medium effort (the plan's section 4). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i05a`, branch `I05a-stat-consumers`, cut from `feature/inventory` after I02's gate. The
shared part is `.claude/briefs/i-series-standard.md`.

```
You are implementing I05a: the stats that worn gear gives start to matter. Walk speed, a drain factor per need, a stamina bonus, and the
damage pipeline that armour will plug into (the inventory plan's sections 3.5 and 3.6; D-game-35 items 6 and 7). No shipped item gives
any stat yet (I08a and I08b add the gear): prove each consumer with test items.

Today (after I01, I06, I03a, I03b and I02): `Stats/` holds `StatSheet`, `StatBuilder` (sums and factors per key), `IStatContributor`,
`StatPipeline` and `StatKeys` (only `Warmth` so far; a new reader adds its own constant there). The equipment is the first contributor
(groups with `statsApply`). Warmth is already read (`Session/VoxelSession.Temperature.cs`). The other readers are:
- walk: `Session/VoxelSession.Eat.cs`, `PrepareTick` (about line 128): `WalkPermille = 1000 * walk * cold * sprint`;
- drains: `Needs/PlayerNeeds.cs`, `RateFactor` (one factor for every need; set by `VoxelSession.Needs.ApplyRateFactor` from the needs rate
  and relaxed mode);
- stamina: `PlayerNeeds` takes the stages' `StaminaPenalty` (about line 181);
- damage: `Water/IPlayerHealth.Damage(int amount, DamageCause cause)`, with `DamageCause` an enum (`Drowning`) in `Water/`, called by
  `DrowningDamage`; `NeedsHealth` and `NoPlayerHealth` implement it.

What to build:
1. Stat keys and their units, each a `StatKeys` constant with a doc comment that says its unit and how it combines, and a validator check on
   items' `stats` for each key's range: `walkSpeed` (a factor on walking, 1 leaves it alone; worn items multiply), `hungerDrain` and
   `thirstDrain` (factors on that need's fall), `maxStamina` (a bonus in the stamina meter's own units, added where the stage penalty is
   taken), `armour` (a whole-number rating summed; read by the damage pipeline only). Choose whether each is a sum or a factor in the
   builder, say why, and keep one rule per key. Absent means no change.
2. Walk: `PrepareTick` multiplies in the sheet's walk factor beside the needs', the cold's and the sprint's. Decide and say whether it
   applies with `--needs off` (the cold does not: I06 chose that so the language runs keep their baselines; follow the same reasoning).
   Clamp the product to a sane range in one place and test the clamp.
3. Drains: `PlayerNeeds` gains a factor per need (hunger, thirst; sleep stays 1 unless you find a reason), multiplied with the shared
   `RateFactor`, which keeps its meaning (the needs rate and relaxed mode). The sheet's `hungerDrain` and `thirstDrain` set them each tick
   or on change, deterministic either way.
4. Stamina: the sheet's `maxStamina` raises the meter's ceiling (or offsets the stage penalty: read how the penalty works and choose the one
   the plan's "a bonus where the stage penalty is taken" means; say which). The HUD's stamina bar shows the new ceiling if it draws one.
5. Damage: the cause becomes a data id. `content/game-v1/game/damage.json` and `content/schema/damage.schema.json` list the causes
   (`drowning` today) and, per cause, which modifiers apply (today armour applies to none: the owner said "Armor does not reduce drowning
   damage, lava or cold currently. However we may add that in the future"). `IDamageModifier` (an ordered list decorating
   `IPlayerHealth.Damage`), the first implementation an armour modifier reading the sheet's `armour` (with a formula you choose and
   document, e.g. a reduction capped below 100%), registered by id so that damage.json names it. `DamageCause` maps to the id (or is
   replaced by a string id: your choice, but drowning must behave exactly as now). Letting armour soften a cause later is a data row, not
   code: prove it with a test cause and a test damage file where armour applies.
6. State and digest: today's digests and every scripted run's baseline stay exactly as they are with nothing worn (verify). If a factor
   changes a stepped value only when something is worn, nothing in the digest moves for runs without gear.

Tests (Given_When_Then, mirrored folders, test items from `GearItems` with the stats): a worn walk item changes `WalkPermille` and the clamp;
each drain factor slows only its need; the stamina bonus; the damage pipeline: drowning unchanged with armour worn (damage.json applies
nothing), a test cause with armour applied is reduced by the formula, an unknown cause or modifier id refused when damage.json loads;
validator range refusals for each key; with nothing worn, today's digest and needs numbers.

Content: damage.json and its schema are content, so run the content loop (content/README.md, "The edit loop"; scripts/content-loop.sh does
the whole loop) and put the new bundle hash into `Fixtures/save-v10.json` if the loop does not. No new item rows, no new Spanish.

Documents: docs/game/architecture.md (each stat key's reader, the damage pipeline and how to make armour apply to a cause), content/README.md
(damage.json, the stat keys and their units).

Out of scope: the `light` stat (I05b), shipped gear (I08a, I08b), durability (I07), combat, the panel's stats line (I04).

The gate is the standard brief's (the count after I02 is in your prompt). Report: the standard one page, plus each key's unit and rule (sum
or factor), the armour formula, whether walk speed applies with `--needs off`, and every existing test changed and why.
```
