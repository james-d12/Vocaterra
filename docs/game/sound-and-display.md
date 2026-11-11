# Sound and display

The audio system and the game's sounds, the window, and the UI scale and readable text.

## Sounds (S24)

The sound system (D-game-18): interaction, progress, movement and ambient sounds through a mixer with four categories, and no music. Every file is CC0 (Kenney's Impact Sounds, RPG Audio and
Interface Sounds, listed in [assets/audio/SOURCES.md](../../assets/audio/SOURCES.md)); the chimes, the push-to-talk beeps, the sparkle, the bottle and the fire's whoosh are
made in code at load from a fixed seed. The audio is under 1 MB (the budget is 15 MB for S24 and S25 together). Sounds react to events and never touch the simulation, a
turn, a verdict, a state hash or the replay.

**The mixer** (`src/Vocaterra.Game.Core/Sound/SoundMixer.cs`, engine free; the engine output is `src/Vocaterra.Game/Sound/SoundEffectOutput.cs`):
- Categories: voice (the NPCs' Spanish, S9), sfx, ui (panels, buttons, progress, the beeps) and ambient (S25's beds), each with a volume (defaults 1.0, 0.8, 0.8, 0.6).
- Ducking: sfx and ambient dip to 35 % under any NPC voice line (eased in over 0.08 s, back over 0.6 s), so the ear stays on the Spanish.
- The microphone rule: while push-to-talk or the mic check records, sfx, ambient and ui are all silent and what plays is stopped. The start beep plays just before the
  recording and the stop beep just after it; the recording drops the start beep's length from its start, so no game sound reaches Vosk.
- Variation: the take of a sound and a small pitch change come from a presentation generator seeded from the world seed (`PresentationRng`, its own SplitMix64 stream),
  never the same take twice in a row.
- Beds (for S25): `SoundMixer.SetBeds` takes position-weighted targets and cross-fades them over 1.5 s.
- Volumes are kept in the save's `settings.sound` and restored on `--load`.

**Flags.** `--mute` (the voices off, as before), `--sfx-volume <0..1>`, `--ambient-volume <0..1>`, `--no-sounds` (every sound but the voices off, the beeps too) and
`--sound-log <file>`. A scripted run (`--screenshot`) uses a null output: silent, no audio device needed. `--sound-log` still records its cues, one line each:
`tick category cue take gain pitch`, with `mark` lines for the voice and recording windows, `#` comment lines for what the microphone rule held back (`# 001640 held back (recording): sfx place_block`) and a `#` summary at the end. A scripted spoken step (`--wav`, `--speak`) and
the scripted mic check stand for a push-to-talk: the start beep, 24 silent ticks, the stop beep.

**The sounds.**

| Event | Cue (category) | Source |
| --- | --- | --- |
| Gathering hits: wood, stone, leaves, soft ground | `gather_wood`, `gather_stone`, `gather_leaf`, `gather_soft` (sfx; every 15th tick of holding) | Kenney Impact Sounds, RPG Audio |
| A block breaks | `break_wood`, `break_stone`, `break_leaf`, `break_soft` (sfx) | Kenney Impact Sounds, RPG Audio |
| Placing a block, picking up an item, crafting done | `place_block`, `pick_up`, `craft_done` (sfx) | Kenney Impact Sounds, RPG Audio |
| Filling the bottle, lighting the fire | `bottle_fill`, `fire_light` (sfx) | synthesized |
| Inventory, crafting panel and book open and close; the book's page turn | `inventory_open`, `inventory_close`, `crafting_open`, `crafting_close`, `book_open`, `book_close`, `book_page` (ui) | Kenney RPG Audio |
| A craft refused (soft) | `craft_refused` (ui) | Kenney Interface Sounds |
| The dialogue opens and closes | `dialogue_open`, `dialogue_close` (ui) | Kenney Interface Sounds |
| Push-to-talk start and stop | `push_to_talk_start`, `push_to_talk_stop` (ui) | synthesized |
| A button, a tab, the F8 report saved | `ui_click`, `ui_tab`, `report_saved` (ui) | Kenney Interface Sounds |
| A lesson step, a unit, the lesson | `step_done` (soft chime), `unit_done` (brighter), `lesson_done` (flourish) (ui) | synthesized |
| An answer understood | `answer_understood` (ui) | synthesized |
| Not understood, "try again", "no sé", help | `answer_neutral` (ui): one soft steady tone, never punishing | synthesized |
| A new word (a word card, or a noun that became known) | `word_learnt` (ui, sparkle) | synthesized |
| An objective step | `objective_step` (ui) | Kenney Interface Sounds |
| The objective complete | `objective_done` (ui) | synthesized |
| A check item answered | `check_answered` (ui) | Kenney Interface Sounds |
| Session complete, at the fire | `session_done` (ui) | synthesized |
| Footsteps on grass, wood, stone (concrete), snow | `foot_grass`, `foot_wood`, `foot_stone`, `foot_snow` (sfx; a step every 2 blocks walked, so the pace follows the walk) | Kenney Impact Sounds |
| Footsteps on dirt, gravel, sand; a splash in water | `foot_dirt`, `foot_gravel`, `foot_sand`, `foot_water` (sfx) | synthesized |
| A jump, a landing | `jump`, `land` (sfx; a landing in water is a `foot_water` splash) | Kenney Impact Sounds |
| The birds over the forest by day | `bird_chirp` (ambient; scheduled in clusters by the presentation generator, louder the nearer the forest) | synthesized |
| The fire's crackle by the lit fire ring and the village lamps | `fire_crackle` (ambient; scheduled, louder the nearer the fire) | synthesized |
| The beds: wind on the cliff and the heights, the river and the lake, the waterfall (louder near it), the village murmur; the birds by day and the insects as the daylight falls (E03); the rain, lower under cover, the wind with the weather's wind channel, the birds quieter in rain (E06) | beds (ambient, looping; cross-fade over 1.5 s by position, panned to the source) | synthesized (loops, seamless) |

**Ambient beds and footsteps (S25).** The beds are `AmbientBeds` (`src/Vocaterra.Game.Core/Sound/AmbientBeds.cs`): the weight of each bed is a
function of the player's place (the scene's sites, the river's path, the lake, the waterfall, the fire rings and the lamps), so the same route gives the same
weights at the same tick. Wind is full on the cliff (feet above y 68) and silent in the valley; birds sing in the lowland forest by day and the insects
take over as the daylight falls (E03: the weights are the atmosphere's daylight, so `--time` or `--dusk` gives the mix of that hour, and halfway through dusk the two share the forest equally); the river follows its path, the waterfall is louder near its lip, and the village murmur is full by the houses and the stall. The
looping beds are made in code (`AmbientLoops.cs`): 10 seconds each, the tail faded into the head with an equal-power curve, so no click at the seam. The
scheduled birds and crackles (`AmbientSound.cs`) draw their times from the presentation generator, in ticks, and follow the bed's weight. Pan is the bearing
of the source from the way the player faces (simple stereo, not 3D). The footsteps (`Footsteps.cs`) read the block under the feet: every solid block kind
has a surface, and a step is every 2 blocks walked on the ground (about two a second at the walking speed); the take of a step never repeats twice in a
row. In water the steps splash. The beds and the scheduled sounds are silent while the microphone records and duck under a voice, like the other sounds.
The sound log writes a `mark` line when the place or its weights change (`bed at village: village=1.0`).

**Rain, wind and birds in the weather (E06).** The `rain` bed's weight is the precipitation channel's intensity (0 when no precipitation is named), reduced by up to three quarters under cover: nine columns round the feet, a
block apart, count as covered when their highest block is over the head, and the share covered sets the cut, so the rain on a roof is still heard, at a quarter, and eases as the player walks out from under an eave. The cover query is
`VoxelGrid.HighestBlock`, the one the drops stop at; Sound reads it and writes nothing to the simulation. The wind bed rises with the wind channel above 0.25 (by up to 0.5 at a channel of 1) on top of its rise with
height, so the first data's breeze (0.2) leaves the sunny kind's sound as it was and a strong wind is heard in the lowland too; the birds' weight is multiplied by one minus the intensity. Every other bed is unchanged. The loop is
synthesised (a 900 to 6500 hertz hiss with a patter of single drops, seamless, 441 kB in memory like the other loops). **The gain** is 0.2, the lowest of the beds (wind 0.5, river 0.45, waterfall 0.6, village 0.35,
insects 0.3): the loop is the loudest of them (RMS 0.22 of full scale against about 0.15) and its hiss lies where the consonants of speech are, so it must not mask an NPC. At the data's intensity of 0.8 and the default
ambient volume of 0.6 the rain's level is 0.8 x 0.22 x 0.2 x 0.6 = 0.021 of full scale, against 0.060 for the NPCs' clips (the median RMS of 60 of them): about 9 dB under the voice before the duck, and about 18 dB
under it while it ducks (0.35); at a gain of 0.3 it would have been 5.6 dB under. It is cross-faded by the mixer over 1.5 s, silent while the microphone records, dulled under water with the others.

```bash
# the S17 route with the sound log: add --sound-log to the route above; the cues played are listed with their ticks, and the recording window of the --wav step is marked
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --sound-log /tmp/route-sounds.log --lesson-run first-words ...
```

## The window

An interactive run starts in borderless full screen at the display's size (`HardwareModeSwitch` off, so the display's mode never changes and switching away is
instant). **F11** switches to a window and back: the window is the `--size` one, else 1280 x 720, and it can be resized. `--size <W>x<H>` starts in a window of that
size instead, and a scripted run (`--screenshot`) always starts in its window (1280 x 720 unless `--size` says otherwise), so its pictures keep a fixed size. The
console's `graphics:` line says which it started in (`full screen 2048x1152`, `window 1280x720`), and F11 prints `display: window 1280x720` and the like.

## UI scale and readable text (X08, R09)

The interface text has three sizes: 100, 125 and 150 percent (`SaveSettings.TextScaleLevels`). `--text-scale <n>` sets it for a run, **F9** steps through them in play
(F9 had no other binding, and no text box reads it; the menu's Font size is the same setting), and the choice is kept in the save's settings (`textScale`; a save from before has 100, a new game 150 since the menu). 100 is the size the layouts
were made at; 150 is the largest because an 800 x 600 window at 150 percent is a 533 x 400 canvas, the smallest the layouts are proven on (a smaller window uses a lower
scale, `UiScale.Effective`).

The 2D layers are drawn on a canvas of the window divided by the scale and magnified to the window (`UiScale`, `UiTransform`), so every engine-free layout model simply
gets a smaller canvas; the mouse and the projected labels use the same canvas. Atlas pictures are drawn in window pixels so a 1.5 magnification leaves no light line under
an icon. What each layout does when the canvas is short:

- **Dialogue panel:** under 540 px tall it is about half the canvas; it gives up the key hints line, then the buttons' English, then the log (the bubble and the lesson strip carry the line).
- **Lesson strip:** smaller padding and buttons; the English goes over pages (the `[M] More (1/2)` button), then the hint shrinks, then a second row of Spanish goes; buttons wrap to rows.
- **Cards, bubble, beacon label:** the bubble stands in the band between the strip and the panel (or is left out); the label is left out when it has no free place; the cards collapse or hide as before.
- **Check card:** compact rows; a heading of more than two rows takes the small font; a typed answer scrolls inside its box.
- **Book:** the ribbons narrow, a row's note gives up its last column, a conjugation table stacks its tenses in blocks, the key hints shorten.
- **Checklist, toast, workbench panel:** the checklist wraps by step (up to three rows) and gives way to the strip; a toast wraps; the workbench stands under the checklist.
- **Report, mic check, inventory, crafting, the menu (Esc: the main page, the settings, the controls), closing card:** the report shows fewer text rows and wraps its hints; the inventory uses 44 x 56 slots; the crafting station column is left out when there is no room; the menu's settings page (its Graphics, Testing and the other groups) shows as many rows as fit and scrolls, and a label or note that is wider than its row is cut, so `UiScaleLayoutTests` measures every row of the full page at each size, and its controls page wraps and scrolls (Up, Down, wheel); the closing card cuts the summary with an ellipsis.

Contrast: the dim lines and the paper book's inks are in `UiPalette`, and `UiScaleLayoutTests` measures every pair (WCAG 2.x, 4.5 to 1 for body text, on the panel and where a bright sky shows through a translucent panel).
