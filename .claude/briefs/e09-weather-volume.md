# Brief: E09, a Weather volume slider

Sent 2026-11-11 to a general-purpose agent, Haiku 5.5. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e09`, branch
`E09-weather-volume`, cut from `feature/weather` after E08's gate. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E09, a fifth volume slider, "Weather", for the weather's own sounds. The owner (2026-11-11): rain is under the
Ambient slider, which "adjusts the same as say birds tweeting, or wind effects"; rain should get its own slider.

The behaviour:
1. A new setting `SettingId.WeatherVolume`: a slider 0 to 100 in steps of 5, default 60 (the same as Ambient, so a new game sounds as
   before), its label key `settingsWeather` ("Weather" in content/locale/en/ui.json), shown in the menu's sound rows right after Ambient
   (src/Vocaterra.Game.Core/Menu/PlayerSettings.cs, MenuRows.cs, SettingId.cs).
2. The rain bed (`AmbientBeds.Rain`) follows the Weather volume and NOT the Ambient volume: rain's level is its weight x its bed gain x
   the Weather volume, so Ambient at 0 leaves the rain, and Weather at 0 silences only the rain. Every other bed, the wind included
   (it comes mostly from height), stays on Ambient. Any later precipitation bed joins Weather: make the rule "the beds of the weather"
   one list or one predicate in AmbientBeds (e.g. `AmbientBeds.IsWeather(name)`), not an `if name == "rain"` in the mixer.
3. Weather ducks under an NPC's voice and is silent while the microphone records, exactly as Ambient is, and is muffled under water as
   the beds are (src/Vocaterra.Game.Core/Sound/SoundMixer.cs: `Gain`, `PushBeds`). Whether you add a `SoundCategory.Weather` or apply
   a second gain in `PushBeds` for the weather beds is your choice: pick the smaller change that keeps the duck and the mic rule shared,
   and say which in the report.
4. `SoundVolumes` (src/Vocaterra.Game.Core/Save/SaveDocument.cs) gains `Weather = 0.6`; the save's `settings.sound` keeps it and a
   `--load` restores it (no migration: old saves need not load; the save fixtures must keep loading, since a missing field takes the
   default). The volume changes live from the menu like the others (src/Vocaterra.Game/VoxelGame.cs, the `SettingId` switch near the
   other volumes).
5. A flag `--weather-volume <0..1>` like `--ambient-volume` (src/Vocaterra.Game/Startup/LaunchOptions.cs, its help text, `SoundOptions`,
   `LaunchSettings`, `SoundSetup`).
6. Tests (Given_When_Then, folders mirroring the source): the setting's spec and default; the menu row's place; the rain bed's level
   follows Weather and not Ambient, and another bed follows Ambient and not Weather; the duck and the mic silence apply to the rain;
   the save round trip; the flag parses and is clamped like the others.
7. Documents: docs/game/sound-and-display.md (the categories' line, the flags, the rain paragraph's level arithmetic now says Weather),
   docs/game/running.md (the flag), docs/game/saves.md (`settings.sound.weather`), and the Sound section of
   src/Vocaterra.Game.Core/README.md if it lists the volumes.

Out of scope: any change to a bed's gain or sound, the wind's split, a master volume, the content bundle (`content/game-v1`; the
locale bundle `content/locale` is in scope for the label, and its manifest is rewritten with `scripts/content.sh hash content/locale
--bundle-id locale` through a wrapper script outside the repository, see the standard brief).

The gate is the standard brief's. Report: the standard one page, the choice you made in point 3 and why, and the level of the rain bed
at the defaults before and after (it must be the same).
```
