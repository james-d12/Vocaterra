# I06b report (the world's climate in what the player feels)

The agent's report, saved by the orchestrator (2026-11-18). Sonnet 5.5 at medium effort; brief `i06b-climate-temperature.md`. Gated at
`a773a307`: 13,330 passed, 0 failed, 9 skipped; timing 23; format clean.

Branch `I06b-climate-temperature`, one commit 7dd8f919. `ClimateTemperatureTerm` ("climate", in `TemperatureTerms.Standard` between
weather and shelter) calls the generator's `IPlaceInfo.TemperatureAt(feet)` (the biome temperature field lowered by the lapse over height;
no formula copied); below `referenceThousandths` it cools by `coolingTenthsPerRange` per 1000 thousandths of fall, down to
`maxCoolingTenths`; never warms; 0 without a world. `TemperatureSituation.Places` (optional) set from `Scene.Places`. About 330 ns a tick,
no cache, no state, no save change. Data: `game/temperature.json` `climate` {-40, 170, 200}, schema, `ClimateCooling`, parse, validator.
F3 appends `climate -12.0` when not 0 (`TemperatureReading.ClimateTenths`).

Feel on seed 42, clear sky (noon / noon +10 / night / night +10, degrees): camp and village 18.2 / 28.2 / 6.2 / 16.2 (term 0, as before);
mid slope (-1784, 120, 2952) 13.3 / 23.3 / 1.3 cold / 11.3 (term -4.9); the highest peak (-632, 296, -1144) 6.2 cool / 16.2 / -5.8 very
cold / 4.2 cool (term -12.0); a point in the mid slope's rock at y 90 14.5 / 24.5 / 2.5 / 12.5 (term -3.7; not a found cave: the lapse uses
y, so underground reads warmer than the surface above it). Not done: caves are not treated specially; weather applies on peaks as in the
valley. Tests: 18 new (`ClimateTemperatureTermTests`, `StubPlaces`), `TemperatureDataTests` extended, one F3 assertion. No digest, golden
or fixture moved.
