# I06's report (temperature and cold)

The agent's report, 2026-11-16 (Sonnet 5.5, high). Commits `b9a4dfda`, `d5023390` on `I06-temperature`, gated into `feature/inventory`
at `a9e5df38` (11,632 passed, 3 skipped).

- **Built:** `src/Vocaterra.Game.Core/Temperature/`: `ITemperatureTerm`, `PlaceTemperature` (sum, clamped to ±100 °C),
  `TemperatureSituation`, the terms `Base`, `Day`, `Weather`, `Shelter`, `Fire`, `TemperatureTerms.Standard` (the one registry),
  `IWarmthSource` and `NoWarmth`, `PlayerTemperature`, `TemperatureBand`, `TemperatureReading`, `TemperatureUnits`, `TemperatureData`,
  `TemperatureValidator`. A new term is one class and one line in `TemperatureTerms.Standard` (a test proves it). The warmth source is
  set on `VoxelSession.Temperature.Warmth`.
- **Content:** `game/temperature.json`, `content/schema/temperature.schema.json`, provenance; the line `temperature.cold` in
  `words.json` hudLines («Tengo frío.») and locale `ui.json` ("I'm cold."), text only like the needs' lines (no clip).
- **Session and HUD:** `VoxelSession.Temperature.cs`; `PrepareTick` multiplies `WalkPermille` by the band's factor; derived each tick, not
  saved. The cold line goes through the needs' announcement path; a code-drawn snowflake (`NeedIcons.ColdShape`) after the drive icons
  (`MaxIcons` 4). `--temperature <n>` (-100 to 100 °C, tenths) holds the place's value; F3 shows `temp 12.5C   warmth 0.0   feels 12.5C
  warm` (`held` under the flag).
- **Numbers:** base 14.0; day swing ±6.0 from the daylight curve; weather at channel 1.0: cloud 2.0, precipitation 6.0, wind 5.0; shelter
  +5.0 only in precipitation, by the covered share of the nine columns round the feet; fire +15.0 within 5 blocks of a lit ring. Bands:
  warm from 12.0 (walk 1.0), cool from 3.0 (1.0), cold from -5.0 (0.85, chill 1, the line), very cold below (0.65, chill 2, the line).
- **Feelings (no warmth):** sunny noon 18.2 warm; clear night 6.2 cool; rainy windy night -1.8 cold (walk 85%); rainy night by a fire
  13.2 warm; clear night by a fire 21.2; cloudy noon 16.2; rainy day 10.2; cloudy night 4.2; rainy night under a roof 3.2 (just cool).
  Very cold is not reachable in the valley today. Cloud cover cools at night too (no sunlight scaling): for the owner to judge.
- **Choices to confirm:** with `--needs off` the cold slows nothing and shows no icon or line (the language runs keep their
  baselines); shelter by the nine-column share, not one column; `--temperature` rounds to the tenth.
- **Existing changed:** `NoLiteralTextTests` (one allow-list entry, as the sibling validators); `save-v9.json` (hash);
  `StageAnnouncement.Meter` nullable; `VoxelDebugText.Lines` an optional temperature parameter, `VoxelDebugOverlay.LineCount` 3 to 5 (it
  was one short already); `GameData.Temperature`, `GameDataValidator` runs the new validator and adds the bands' line keys to
  `HudLineKeys`. Content-loop outputs: the grammar cache and strings, the locale and bundle manifests, provenance.
- **Not done:** the snowflake not seen (no window); clothes, harm, biome climate, pictures (out of scope).
