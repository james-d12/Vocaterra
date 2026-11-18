# I06c report (the temperature as a climate model)

The agent's report, saved by the orchestrator (2026-11-18). Sonnet 5.5 at high effort; brief `i06c-climate-model.md`. Gated at `3e4f2fb9`:
13,517 passed, 0 failed, 9 skipped (+66 over I08a); timing 23; format clean; A2 gate clean; 0 new grammar findings.

Branch `I06c-climate-model`: 5ad4f7b6 (the model), 87a7c9ed (merge of feature/inventory), 344b0d43 (heat tests, F3, docs), 4ab505a1
(the felt temperature saved, guard entries, F3).

**Model.** `ITemperatureTerm.TenthsAt(ITemperatureContext)`; the context holds the atmosphere, feet, covered share, fires and places, and
derives on first ask `Exposure` (0 to 1000), `Biome` (its adjustment), `ClimateTemperature`, `Snows`, `GroundTenths`; a new input is one
member. `IPlaceInfo.SnowBelowTemperature` exposes the generator's snow line; `PlaceColumns` caches the last column. `temperature.json`
schemaVersion 2 with `terms` rows (id plus parameters); `TemperatureTermRegistry.Standard` maps ids to factories; `TermParameters` validates
(missing, out of range, unknown, duplicate); a new system is one class with `From`, one registry line, one data row (and a schema
alternative). Terms: `climate` (the baseline), `day`, `weather`, `shelter`, `fire`; `base` removed. F3: `temp 10.0C -> 8.0C ... [climate
14.0 day 3.1 ...]`.

**Baseline.** The generator's temperature at the column and height through a `ClimateCurve` (points (-1216, -6.0), (-40, 14.0), (60,
14.0), (600, 19.4)): the cold side I06b's slope, a flat stretch over the valley's values, the warm side +0.1 a thousandth; plus the biome
offset. **Biome rows** in temperature.json by id (all temperature numbers in one file; validated against biomes.json; no id in src/):
beach +1.0, day 1.2; plains day 1.3; dense-woodland day 0.8, weather 0.85; lake-basin day 0.7; ocean day 0.6; river-channel day 0.8;
jagged-peaks and stony-peaks weather 1.2; snowy-slopes weather 1.15; woodland none. **Snow:** precipitation below the snow line cools 4.0
more. **Underground:** climate read at max(feet, natural surface); exposure full to 4 blocks below the surface, fading over 20; the
baseline eases to a ground 10.0; day, weather and shelter scale by exposure.

**Heat bands** (for stop 2): very-hot from 35 (walk 0.75, thirst x2.0), hot from 28 (walk 0.9, thirst x1.5), warm 12, cool 3, cold -5
(0.85), very cold (0.65); «Tengo calor.» (I'm hot.) in `hudLines`; thirst through `PlayerNeeds.ThirstFactor`; no hot icon. With +10 warmth
a clear valley noon is 28.2, hot.

**Lag:** `lag.secondsToSettle` 15; the first reading, `--temperature` and a held value are immediate; warmth after the lag. **It reaches
the save** (optional `systems.temperature {"felt"}`, no version bump; saves.md): without it a loaded game diverged (the band moves walk and
thirst).

**Feel, seed 42** (noon clear / night clear / noon rain / night rain, at +0): camp and village 18.2 / 6.2 / 10.2 / -1.8 (as before); mid
slope 13.1 / 1.1 / 5.1 / -6.9; highest peak 5.7 / -6.3 / -7.8 / -19.8 (night clear +10: 3.7 cool); deep cave (-47,23,190) 10.0 always;
cave mid-fade (275,75,-486) 13.8 / 6.0 / 8.6 / 0.8; beach 25.8 / 11.4 / 17.8 / 3.4; jagged peaks y239 5.0 / -7.0 / -8.5 / -20.5.

**Tests:** ClimateCurve, TemperatureTermRegistry (a fourth term from data), BiomeAdjustment, UndergroundTemperature, SnowTemperature,
HeatBand, TemperatureLag; adapted ClimateTemperatureTerm, TemperatureTerm, TemperatureData, SessionTemperature, PlayerTemperature;
VoxelDebugTextTests (F3), PlaceAmbienceTests (fake), two NoLiteralText and one SpanishLiteralGuard allow-list entries. No golden or fixture.
Not done: the per-tick cost of the lookups unmeasured (one-column cache); the felt value only on F3; not played.
