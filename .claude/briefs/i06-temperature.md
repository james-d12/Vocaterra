# Brief: I06, temperature and cold

Sent 2026-11-15 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/i06`,
branch `I06-temperature`, cut from `feature/inventory` after I01's gate. The shared part is `.claude/briefs/i-series-standard.md`.

```
You are implementing I06: a temperature where the player stands, set against the player's warmth; cold slows the player and never
harms (the inventory plan's section 3.7; D-game-35 item 2). There are no clothes yet: warmth comes through a seam that gives zero until
I03a adds equipment stats.

Today: the atmosphere (D-game-31) is `src/Vocaterra.Game.Core/Atmosphere/` (DayClock and its daylight, WeatherSystem, WeatherChannels,
AtmosphereSnapshot); no file under src/ names a weather kind's id (two tests enforce it). A lit fire is found by `Needs/FireRings.NearLit`,
overhead cover by `Voxel/OverheadCover`. The walk speed is set once a tick in `VoxelSession.PrepareTick`
(`Session/VoxelSession.Eat.cs`, `Simulation.WalkPermille` from the needs' walk factor and the sprint). The needs warn the player with
their own lines (find how and reuse that path for «Tengo frío»). `--time <hour>` and `--weather <id>` hold the clock and the weather for
a run; follow them for `--temperature`.

What to build:
1. `src/Vocaterra.Game.Core/Temperature/`: `ITemperatureTerm` (one contribution for a place and a moment), `PlaceTemperature` (sums the
   registered terms into one value), and the first terms:
   - Base: the valley's base temperature, from data.
   - Day: a swing from the daylight (colder at night), amplitude from data.
   - Weather: cooling from the snapshot's channels (cloud cover, precipitation, wind), one coefficient per channel in data, never a kind's
     id.
   - Shelter: a term when `OverheadCover` says the player is under cover (data: how much, and whether it applies only in precipitation).
   - Fire: a bonus within a radius of a lit fire (`FireRings.NearLit`), radius and bonus from data.
   Units: tenths of a degree Celsius as an int (125 is 12.5 °C), deterministic integer arithmetic in the sum; a term may use the shared
   fixed-point helpers internally. Adding a term later (the biomes series' climate from `IPlaceInfo.TemperatureAt` with its lapse over
   altitude, a cave pull, a biome's base) must be one new class and its registration, with no edit to PlaceTemperature or the other
   terms. The biomes series (`feature/biomes`, docs/game/biomes-plan.md section 3.3 there, read with `git show`) describes the same type:
   keep its shape compatible and do not copy its code.
2. Warmth: `IWarmthSource` (the player's warmth in the same units), one implementation that returns zero, wired where I03a will put the
   equipment's `warmth` stat. Feeling = temperature + warmth.
3. `content/game-v1/game/temperature.json` and `content/schema/temperature.schema.json`: the terms' numbers and the bands (warm, cool,
   cold, very cold: a lower bound each, and each band's walk factor, 1.0 for warm and cool), validated at load (bands ordered, factors in
   (0, 1]). Pick first numbers that make a fair noon in the valley warm, a clear night cool, a rainy windy night cold and a fire warm again;
   the owner reviews how it feels after this milestone (stop 2), so say your numbers and why.
4. The effects: the band's walk factor multiplies `WalkPermille` in PrepareTick beside the needs' and the sprint's (one factor, not a
   rewrite); a cold icon on the HUD while the band is cold or very cold (Game; drawn as the needs' icons are, CC0 or code-drawn art); the
   player's line «Tengo frío» once when the feeling first enters a cold band, re-armed when it rises back to cool or warm. Do not change the
   save's shape: if the line's state would need a save field, keep it in memory (saying it again after a load is fine). Cold never damages.
5. `--temperature <n>` (degrees Celsius, may be negative or have one decimal) holds the temperature for a run, as `--time` does; the F3
   overlay shows the temperature, the warmth and the band if F3 has a line for the weather (follow it).
6. Determinism: the temperature reads only the stored time, the weather state and the world; it is derived each tick and not saved.

Tests (Game.Core.Tests/Temperature and the session's tests): each term alone; the sum; a test term registered from a test proves a new
term needs no edit; night colder than noon; rain and wind colder than fair; cover warmer in the rain; a fire warmer; bands and walk factor
(a cold feeling slows WalkPermille by the band's factor, warm leaves it unchanged); the line once per entry into cold; the validator's
refusals; `--temperature` parsing; the kind-id tests still pass.

Spanish: «Tengo frío» is the only new line. It must pass the strict A2 lint and grammar; if the needs' lines are spoken from prerendered
clips, add its clip the same way (scripts/content-loop.sh --grammar --speech). Content loop as content/README.md says; the new bundle
hash goes into the save fixture (save-v9.json since I01).

Documents: docs/game/running.md (the switch and what the bands do), docs/game/architecture.md (the Temperature folder and how to add a
term), a short paragraph in content/README.md on temperature.json.

Out of scope: clothes, warmth from items, the equipment (I03a), the stat sheet, the biomes' climate (no reference to feature/biomes code),
any harm from cold, pictures (no game window while the owner uses the machine).

The gate is the standard brief's. Report: the standard one page, plus your numbers (each term, each band) with the feeling they give at
noon, a clear night, a rainy windy night and by a fire at night, and every existing test changed and why.
```
