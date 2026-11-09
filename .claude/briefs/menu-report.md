# Menu with settings (issue #34): report

Branch `feature/menu` (worktree `.claude/worktrees/menu`), commit prefix `Menu:`. Not pushed. Test counts are filled in at the end of this file.

## What the menu contains

Esc in the world opens it (pauses like the other panels). Main page: Resume, Settings, Controls, Exit (saves as F6 does, then closes). Up/Down, Enter/Space, mouse click, slider drag, wheel, Esc back. F1 opens the Controls page directly.

### Settings (all in the save's settings, `SaveSettings`; a default is not written; a launch flag wins for the run)
| Row | Range / values | Default | Stored as |
| --- | --- | --- | --- |
| Ambient volume | 0 to 100 step 5 | 60 | `sound.ambient` |
| Voice volume | 0 to 100 | 100 | `sound.voice` |
| Effects volume | 0 to 100 | 80 | `sound.sfx` |
| Interface sounds volume | 0 to 100 | 80 | `sound.ui` |
| Graphics quality | low, medium, high | high | `quality` (new) |
| Field of view | 60 to 110 degrees step 5 | 70 | `fieldOfView` (new) |
| Underwater blur | off, low, medium, high | low | `underwaterBlur` |
| Screen effects | on, off | on | `viewEffectsOff` |
| Font size | 100, 125, 150 percent (same as F9) | 150 (largest) | `textScale` |
| Mouse sensitivity | 25 to 300 percent step 5 | 100 | `mouseSensitivity` (new) |
| Invert mouse Y | on, off | off | `invertY` (new) |
| Drive rate | x0.5 to x4 step 0.25 | x1 | `needsRate` |
| Relaxed needs | on, off | off | `relaxedNeeds` |

A final "Needs a restart" note says that window size, speech and microphone (command line) are read at start-up.

### Testing section (never saved; hidden in `--playtest`)
Debug overlay and developer notes (F3, `--dev`); Needs running (`--needs`); Refill all needs (`--set-need`); All sounds but voices off (`--no-sounds`); Voices off (`--mute`); Camera over the shoulder (F5, `--third-person`); Save now (F6).

### Controls
Generated from `ControlTable` (56 controls in 7 groups, keys and mouse), the one table the key bindings, `InputKeys`, the dialogue panel, the lesson strip, the book, the station, the inventory and crafting panels read; descriptions are `ui.control.<id>` in `content/locale/en/ui.json`.

## Decisions
- Esc: opens the menu; in a panel it still closes the panel; with the closing card up the first Esc takes the card down; the playtest thank-you screen still quits. The menu is first in the `ScreenStack`. Esc no longer "releases the mouse" (the menu frees it). The click on Resume does not gather.
- F8 was the report key, so the old N09 settings panel was only reachable by `--settings`; it and the F1 help are retired in favour of the menu. `--settings`, `--menu`, `--controls`, `--menu-scroll` open pages for screenshots.
- Settings live in the existing save settings path; no save version bump (new fields are optional, written only when not default; older saves read defaults). Consequence: settings are kept when the game is saved (F6, autosave, Exit), and a new game without `--load` starts from defaults again.
- Testing section is always shown (in a playtest it is hidden) rather than behind a flag: the owner wants testing to be easy; every row is a thin wrapper over an existing flag or key, off by default, labelled, not saved.
- Quality changes at run time: `VoxelRenderer.SetPreset` drops all meshes and rebuilds the view nearest first; `--view-distance` still overrides the distance.
- Text size default 150 for interactive runs; scripted `--screenshot` runs keep 100 so recorded runs hold.
- Locale: all new text is `ui.*` strings; `scripts/content.sh refresh` run; game-v1 untouched (no re-hash).

## Not done / left for later
- `game/controls.json`, `GameData.Controls`, `TipBook.LoadControls`, `ControlHelp` and the locale `control.<id>.keys/.text` strings are now read by no screen. Left in place because removing the file re-hashes game-v1 (goldens and the save fixture). Retire in a content milestone.
- Time of day and pausing single needs are not exposed (no runtime switch exists; the issue forbids new systems).
- Sounds cannot be switched back on in a run started with `--no-sounds` or a scripted run (the null output was chosen at start).
- Run-time quality, FOV, sensitivity and invert were verified by build, unit tests and screenshots of the pages only; I could not drive the live window with keys here, so the live effect of changing quality in a running game is untested by hand.

## Screenshots (not committed), in `.claude/menu-shots/` of the worktree
menu-main.png, menu-settings.png, menu-settings-testing.png (scrolled to the Testing section), menu-controls.png, menu-controls-scrolled.png, menu-settings-150.png (text at 150 percent).
Made with `scripts/limited.sh src/Vocaterra.Game/bin/Debug/net10.0/Vocaterra.Game --seed 42 --quality low --beacon none --settings --menu-scroll 14 --screenshot <png> --frames 30`.

## Verification and commits
`dotnet build Vocaterra.sln -warnaserror`: 0 warnings, 0 errors. `dotnet test Vocaterra.sln`: 10397 passed, 0 failed, 50 skipped (Game.Core.Tests 2911 passed, Game.Tests 821, Nlp.Es.Tests 2292, Host.Tests 189, Fuzzing 118, Golden 17, and the rest). `dotnet format Vocaterra.sln --verify-no-changes`: clean.
Commits on `feature/menu` (not pushed): aabc5078 (the earlier agent's WIP checkpoint, kept as the base), 8ff8ab8a (retire F8 panel and F1 help, locale strings), 65e5c0d8 (menu screen, layer, run-time settings, Exit), 6a29ccce (tests, docs, launch flags, hints).
