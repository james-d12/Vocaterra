# Brief: E08, the close-out

Sent 2026-11-10 to a general-purpose agent, Haiku 5.5. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e08`, branch
`E08-close-out`, cut from `feature/weather` after E06's gate. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E08, "the close-out" of the E series (day, night and weather): the documents a later reader needs, three small
corrections, and a tour of the running game that shows the whole series working. Nothing new is designed. Work through the parts in
order and commit after each.

What the series built (read these first, they are short): docs/game/weather-plan.md (sections 3 and 7); the last eight entries of
docs/game/findings.md (E01, E04, E02, E07, E05, E02a, E03, E06: `grep -n "^- \*\*E0" docs/game/findings.md` gives their lines; read
only those); the Atmosphere section of src/Vocaterra.Game.Core/README.md; the day cycle, weather and rain sections of
src/Vocaterra.Game/README.md; docs/game/running.md's paragraphs on `--time`, `--weather`, the menu's rows and the scripted steps.

Part 1, documents (no code):
1. docs/game/architecture.md: add the series where the game layer's folders are described: Core's `Atmosphere/` (the clock, the
   weather's kinds as data, the snapshot), the game's `Graphics/Sky` and `Graphics/Weather`, and the one rule that holds them
   together: consumers read the snapshot's channels and never a weather kind's id, which two tests enforce (name them). Ten to
   twenty lines, in the document's own style; read its section "the game layer" first, not the whole file.
2. content/README.md: a section "Adding a weather kind", a numbered how-to that a content author can follow without reading code,
   checked against the files as they are: the row in game/weather.json and what each field does (the channels, the spell's seconds,
   `next` and its weights, `morningWeight`, `say`); the three things a kind's `say` keys need for every NPC that has `weatherAnswers`
   (the dialogue pool `<npc>_<key>`, the agent's transition on the host event `<id>.<key>`, the English line in the locale); the
   speech clips through the content loop; that a new precipitation kind also needs a row in the game's style table
   (src/Vocaterra.Game/Graphics/Weather/PrecipitationStyles.cs) and, if it should be heard, a bed; and what the validator and the
   tests will say when a step is missed. Use an invented kind (a fog) as the worked example in the text only: do not add it to the
   bundle.
3. docs/game/saves.md and docs/game/sound-and-display.md: read what E01, E04, E03 and E06 added and make each one coherent paragraph
   where the same thing is now said twice or in two places.
4. docs/game/running.md: one short section, "Day, night and weather", that gathers what is now spread over several paragraphs: the
   cycle's length and hours (from content/game-v1/game/day.json), the kinds and how they change (from weather.json), `--time`,
   `--weather`, `--dusk`, the menu's two rows, the F3 line, the scripted steps, and that a held hour or weather is part of the
   simulation's state and is saved. Remove the paragraphs it replaces.

Part 2, three small corrections (code):
5. src/Vocaterra.Game.Core/Menu/TestingHooks.cs: its comment says test aids are not kept in saves. A held clock or weather is
   simulation state and is saved. Correct the comment to say so for those two; change no behaviour.
6. The clouds at night under a heavy sky: in the screenshot of the village at night in the heaviest kind, the cloud slabs are light
   grey against a near-black sky. In src/Vocaterra.Game/Graphics/Sky (SkyLook.cs, SkyWeather.cs; read E05's tests in
   tests/Vocaterra.Game.Tests/Graphics/Sky/SkyWeatherLookTests.cs first) make the clouds' three colours follow the dome down at
   night under cover: at daylight 0 and full cover the cloud top's brightness is at most 1.5 times the dome's middle colour's; by
   day nothing changes; at the first kind's values nothing changes at any hour (E05's test that the first kind equals E02's look must
   pass untouched); and the change is continuous in the daylight and in the cover (E05's continuity test must pass untouched). Add
   one test for the new bound. Take a screenshot of that village view at `--time 0` in the heaviest kind before and after.
7. Run `pgrep -fa "until .*pgrep"` once at the start: if a shell loop of an earlier agent is listed whose working folder
   (`ls -l /proc/<pid>/cwd`) is under /home/james/Dev/Vocaterra/.claude/worktrees/weather or a worktree named e0*, report its pid and
   command in your report; do not stop it yourself and do not touch any other session's processes.

Part 3, the tour of the running game (scripted runs; nothing is committed from it). Read docs/game/running.md and
docs/game/scripted-runs.md for how a scripted run is made (`--screenshot`, `--frames`, `--shot`, `--teleport`, `--walk-to`, `--look`,
`--say`, `--quality low`); the weather's ids are in content/game-v1/game/weather.json. Write the pictures, flattened to opaque RGB,
and each run's console output to
/tmp/claude-1000/-home-james-Dev-Vocaterra/24d882de-ab0e-4e80-85f7-7ad42d8daea9/scratchpad/e08-tour/ (the folder exists).
8. The camp view in each of the three kinds at `--time` 5, 13, 21 and 0 (twelve pictures), and one with the F3 overlay on if a
   scripted run can show it (say so if it cannot).
9. The village in the rainy kind at 13 and at 0, and one picture from under a roof looking out.
10. The check E07 could not make: in the running game, walk to one NPC and say «¿Qué tiempo hace?» (a typed `--say`) in each of the
    three kinds at `--time 13`, and in the first kind at `--time 23`. From each run's console output or event log copy the NPC's
    reply into the report: it must be «Hoy hace sol.», «Hoy está nublado.», «Hoy llueve.» and, at night in the first kind, «Hace buen
    tiempo.». If a reply is anything else, report the exact output; do not change code to make it so.
11. One run that uses the scripted steps `--set-time` and `--set-weather` in the middle of a script, with a picture before and after
    the step.
12. One default run (no flags of the series) of 300 frames: its closing console line and its picture, to show a new game still
    starts on a sunny morning.
Look at every picture yourself and say in the report, picture by picture in one line each, what is wrong with any of them.

The gate is the standard brief's (the suite on `feature/weather` is given in your prompt). The report: the standard one page, plus the
four replies of point 10 verbatim, the list of pictures with your one-line reading of each, and anything in the documents you found
to be untrue of the code as it is (say what and where; correct the document, not the code).

Out of scope: any new feature, any change to the weather's data values, the content bundle, the NPCs' lines, the plans, the decisions,
the findings, the open items and the handovers.
```
