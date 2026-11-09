# Menu, second pass: the launch-flag settings that can change at run time (issue #34, PR #46)

Branch `feature/menu` (worktree `.claude/worktrees/menu`), three commits on top of `6a29ccce`, not pushed:
- `cca31b19` Menu: draw distance setting and the launch flags that were missing
- `8c5f3fcf` Menu: the Testing section grows (needs sliders, teleport, beacon, dusk, greyscale, all-nouns-known view, tips, typed credit, autosave), grouped under subheadings
- `d7a26158` Menu: shorter testing labels, so no row is cut at 800x600 and 150 percent; the layout test measures at the real font's width

## Rows added

### Settings (saved in the save's settings, a default is not written, launch flag wins for the run)
| Row | Range | Stored as | Flag |
| --- | --- | --- | --- |
| Draw distance (blocks), in Graphics under Graphics quality | Auto, then 48 to 576 in steps of 16 | `viewDistance` (new; absent or 0 = Auto) | `--view-distance` (existing; now feeds this setting) |

Quality and draw distance are two settings. Quality sets the preset (distance 224/304/400, undergrowth, shading, clouds); the slider overrides only the distance; Auto (the lowest slider position, shown as the word "Auto") gives it back to the quality. A chosen distance holds when quality changes. `VoxelRenderer.SetPreset` now keeps built meshes when only the distance changes (Remesh drops the chunks past it, new ones come in nearest first), and drops them all only when undergrowth or corner shading change, so dragging the slider is cheap. The old `LaunchOptions.PresetFor` is gone (`SceneDrawer.ApplyQuality(level, viewDistance?)`). `--view-distance` values are snapped to steps of 16 and capped at 576 (the world is 384 wide; 576 passes its far corner).

### New launch flags (so every menu setting can be set at launch; each wins over the save for the run)
`--voice-volume <0..1>`, `--ui-volume <0..1>`, `--fov <degrees>` (60 to 110, steps of 5; off-step or out-of-range refused, like `--needs-rate`), `--mouse-sensitivity <percent>` (25 to 300, steps of 5), `--invert-y on|off`. All in `LaunchOptions.Usage`, `SoundOptions`/`SoundSetup.VolumesFor`, `LaunchSettings`, `PlayerSettings.Resolve`.

### Testing section (never saved, hidden in `--playtest`), now in groups under subheadings
- **Needs:** needs running (existing); slider each for hunger, thirst, sleep, health, stamina, 0 to 100 step 5 (`session.SetNeed`, live like `--set-need`; stamina stops at its maximum; health 0 is a death); refill all (kept).
- **World and view:** debug overlay (existing), camera over the shoulder (existing), **dusk lighting** (`--dusk`), **greyscale view** (`--greyscale`), **all nouns known (view)**, **first-time tips** (`--tips`), **beacon** (off or one of the scene's named points, as `--beacon`).
- **Teleport:** a **place picker** over the same named points (sorted) and a **"Teleport to the place"** button; the feet go to the ground of the point's column via the new `GroundFinder.StandingAt`, the one rule `--teleport` now also uses.
- **Sound:** all sounds but voices off, voices off (existing).
- **Scoring:** **typed answers earn half / full credit** (`--typed-credit`).
- **Files:** **autosave on/off** (`--no-autosave`), save now (existing).

Page restart notes grew to three lines: window size/speech/microphone; seed, content, scene, language, save to load; sound log, session and report folders, scripted steps.

## Decisions
- **All nouns known is view-only.** `--assume-known` writes full credit into `WordProgress` (and so into a save), so the menu does not use it. The toggle (`GreyscaleWorld.AssumeAllKnown`) only empties the grey set and the book's greyscale icons; the learner model is untouched (tested). The `--assume-known <nouns>` flag stays as it was.
- **Greyscale at run time:** `GreyscaleWorld.Enabled` is settable; turning it on takes the words known then as not new (no toast burst); turning it off hands the renderer an empty grey set once.
- **Dusk at run time:** `VoxelRenderer.SetDusk` (clear colour, fog, mesh options, all meshes released: the lantern glow and evening light are in the vertices), `SkyRenderer.SetDusk` (dome rebuilt), `ArmPresenter.Dusk`, `AmbientSound.Dusk` (birds to insects). One flag in `VoxelGame`, applied to all four.
- **Typed credit:** `GuideSession.SetCredit` (and `ProcedureRunner.SetCredit`); only later uses are affected; the rule is not in a save. The session-start log profile now reads the session's credit.
- **Autosave:** `AutosaveController.Enabled` / `SetEnabled`; the controller always exists now (`--no-autosave` starts it off); turning it on restarts its policy from now so it does not write at once; the crash report says "off" while it is.
- **Beacon "off"** clears the beacon, which dismisses the guide's own arrow as arriving would (the arrow hangs on the beacon). Picking a point replaces it.
- **Teleport** keeps the menu open (the world is drawn behind it); a point with no ground prints a console line and does nothing.
- Needs sliders read live (the menu pauses the game, so the values do not move while it is open).
- New row kinds in Core: `Subheading` (not selectable), factories `MenuRow.Slider`, `MenuRow.Picker` (over names, with an optional "none" word) and `Switch` with its own on/off words; `TestingHooks` is now an initializer record whose optional hooks leave their rows out (keeps tests and playtest simple).
- Labels shortened so nothing is cut at 800x600 / 150 percent (found in a screenshot: "Greyscale: unknown nouns grey" was cut); the layout test now measures at 7.4 px a character (the real font at that size is about 7.3).

## Candidates left out, and why
- `--sound-log`: the log file is opened with the mixer at start-up and takes a path; needs a text field and a restart of the sound side.
- `--session-dir`, `--report-dir`: paths; the session log is open from the start.
- `--report-kind`: the report panel has its own Bug and Feature buttons.
- Seed, content, scene, locale, load/save/strict-save, session and skip-to-session, check, playtest, window size, speech models, mic check and every scripted step: start-up only (named in the page's restart notes).
- Not done: not exercised by hand in a live window (I cannot drive keys here). The run-time paths (dusk re-mesh, greyscale switch, teleport, beacon, quality/distance, autosave toggle) are covered by unit tests of their logic and by screenshots of the pages with the launch flags set; the renderer-side `SetDusk` and `SetPreset` have no GPU-free test.

## Tests
Full: `dotnet build Vocaterra.sln -warnaserror -m:4` 0 warnings, 0 errors; `dotnet format Vocaterra.sln --verify-no-changes` clean; `dotnet test Vocaterra.sln -m:2`: **10431 passed**, 0 failed, 50 skipped (was 10397): Game.Core.Tests 2935 (+31 skipped), Game.Tests 831, Nlp.Es.Tests 2292, Nlp.Tests 4049, Host.Tests 189, Fuzzing 118, Golden 17. One fuzz test (`PipelineFuzzTests.OpenFallback_LeavesATrustedFirstReadingAlone...`, a generator-coverage check) failed once under heavy machine load and passed on its own and in a full rerun of the project (118 of 118); nothing under `src/Vocaterra.Nlp*` or `content/game-v1` changed.
New tests: Core `TestingRowsTests` (10), `PlayerSettingsTests` (draw distance, flags over the save), `AutosaveControllerTests`, `TypedCreditSettingTests`, `GroundFinderTests`; Game `GreyscaleTests` (runtime switch, all-known view leaves the model alone), `VoxelLaunchOptionsTests`, `SoundLaunchOptionsTests`, `UiScaleLayoutTests.TheWholeSettingsPage...` (every label, note and value of the full page at every window size and scale).

## Docs
`docs/game/running.md` (the menu paragraph: draw distance, all flags, the grouped Testing section, restart notes), `docs/game/implementation-plan.md` (Menu-2 row in 4.10, findings entry "Second pass"), `docs/game/sound-and-display.md` (the long settings page), `src/Vocaterra.Game/README.md` (flag table). Locale: new `ui.*` strings, `scripts/content.sh refresh` run (locale manifest rewritten; game-v1 untouched, no re-hash).

## Screenshots (not committed), `.claude/menu-shots/` in the worktree
`menu-settings-2-graphics.png` (scroll 0: Sound and Graphics with Draw distance "Auto"), `menu-settings-2-more.png` (scroll 12), `menu-testing-1-needs.png` (19), `menu-testing-2-view.png` (31), `menu-testing-3-end.png` (100, the end with the three restart notes), `menu-settings-800x600-150.png` (smallest window at 150 percent), `menu-testing-800x600-150-flags.png` (same size with `--dusk --greyscale --tips --view-distance 160`, rows show "on"). Earlier shots from the first pass are still there.
Command shape: `scripts/limited.sh src/Vocaterra.Game/bin/Debug/net10.0/Vocaterra.Game --seed 42 --quality low --beacon none --settings --menu-scroll <n> --screenshot <png> --frames 30`.
