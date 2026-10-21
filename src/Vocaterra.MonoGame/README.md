# Vocaterra.MonoGame

The game's window: the only engine code in the repository (plan section 3 rule 3, D-game-1). MonoGame 3.8.6 DesktopGL on `net10.0`, no
content pipeline (no MGCB: textures are made in code, text is a TTF read at run time through FontStashSharp). It references
`Vocaterra.Game` (the guide, the session and the world facts; it brings the language core's packages) and `Vocaterra.Simulation` by project and
names no `Vocaterra.Nlp.*` package itself.

## Run

```bash
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet run --project src/Vocaterra.MonoGame -- --seed 42
```

| Option | Meaning |
| --- | --- |
| `--seed <ulong>` | the world seed (default 42) |
| `--content <dir>` | the content bundle folder (default: `content/game-v1` found by walking up from the executable, then from the current directory; `VOCATERRA_CONTENT_DIR` also works) |
| `--screenshot <path.png>` | scripted run: fixed 60 Hz clock, scripted input, save the back buffer as a PNG, exit 0 (1 if the PNG cannot be written) |
| `--frames <n>` | with `--screenshot`: run until `n` ticks have been simulated, one per frame (default 120, or 240 with `--say`) |
| `--walk-right <ticks>` | with `--screenshot`: hold 'right' for the first `n` ticks |
| `--say "<line>"` | with `--screenshot`, repeatable: walk the player to the guide, open the dialogue panel, type the line and send it, so nobody has to type; each turn is printed to the console with its time, and the words it counted (`word:`, `known:`) |
| `--point <col>,<row>` | with `--screenshot`: the mouse cursor sits on that tile for the `--say` lines (the guide's name check reads it) |
| `--point-nearest <noun>` | with `--screenshot`: the cursor sits on the tile of that noun nearest the player when each line is sent (`arbol`, `árbol`, `piedra`, `hierba`, `tierra`; an accent is optional). Not with `--point` |
| `--skip <minutes>` | with `--screenshot`, between `--say` lines: move the **stored time** on by that many minutes without stepping the world (nothing else changes). Order matters: `--say A --skip 11 --say B` sends B eleven minutes after A. Printed as `skip: ...` |
| `--talk` | with `--screenshot`, between `--say` lines: close the dialogue and talk to the guide again, which is when she may ask a review question (`review: "..." pointing at col,row`, or `talk: no review`). The first opening (before the first `--say`) asks one too when a word is due, but nothing is due yet in a new game |
| `--journal` | with `--screenshot`: open the journal once the `--say` lines are sent (at the start without `--say`) |

Examples, as the orchestrator verifies rendering: `... -- --seed 42 --screenshot /tmp/p2.png --frames 120 --walk-right 60`, and the guide:
`... -- --seed 42 --say "¿dónde está el árbol?" --screenshot /tmp/p3.png --frames 240` (the PNG shows the dialogue panel with the guide's answer and
the arrow; add a second `--say` for a second turn, and raise `--frames` so the script has time to finish: about 40 ticks to walk and settle, then
about 12 per line). They still open a window (they need a display, `DISPLAY` or `xvfb-run`), but nobody has to look at it.

The P4b demos (spaced colour, seed 42, cursor on the nearest tree; add `--screenshot /tmp/x.png --frames 100`, the script ends at about tick 100):
(a) `--point-nearest árbol --say "es un árbol" --say "es un árbol" --say "es un árbol"`: one credit and two `early:` outcomes, the trees a third coloured,
the hint `Bien. Repasa más tarde.` in the panel and as a toast; (b) `--say "es un árbol" --skip 11 --say "es un árbol" --skip 61 --say "es un árbol"`:
`known: el árbol`, the trees in full colour; (c) `--say "es un árbol" --skip 11 --talk` (the guide asks `¿Qué es esto?` with the arrow and the `¿?` marker on
a tree; add `--say "es un árbol"` for the answer, which earns the second third, and `--journal` with a `--skip 2` instead to see `otra vez en 8 min`).

The P4 demo (words), as it was before spacing (three uses in a row no longer make a word known; see the P4b demo (b)):
`... -- --seed 42 --point-nearest árbol --say "es un árbol" --say "es un árbol" --say "es un árbol" --journal --screenshot /tmp/p4-journal.png --frames 600`
(now a third of the colour, and the journal shows `otra vez en 10 min`). With one `--say` and `--frames 400` the trees are a third
coloured and the rest of the world grey.

Controls: **A / D** or **Left / Right** walk, **Space / W / Up** jump (hold for a higher jump), **J** opens the journal (the words, see below; **J** or
**Esc** closes it, and the player does not walk while it is open). Next to the guide (within 5 columns), the hint
"Pulsa T para hablar" shows; **T** opens the dialogue panel, which takes the keyboard: the movement keys then type letters (input focus), **Enter**
sends the line, **Esc** closes the panel, **Left / Right / Home / End / Backspace / Delete** edit it. With the panel closed, **Esc** takes the
guide's arrow away. For Spanish on a keyboard that cannot type it: **Alt + a vowel** gives `á é í ó ú` (with Shift, the capital), **Alt+N** `ñ`,
**Alt+/** `¿`, **Alt+1** `¡`; the window's own text events (a Spanish layout, a compose key) work as well. The overlay (top left) shows FPS, tick,
seed, the tile under the player's feet, the tile under the mouse, and a Spanish line that proves the font draws `¿ ¡ á é í ó ú ü ñ`.

**Words (P4).** The world is grey until the learner has used its word. Say `es un árbol` to the guide with the mouse on a tree (or `es una
piedra` on a stone): three correct uses make the noun known, and its objects go from grey to full colour in thirds (one third per credited
use). A correct use earns the same credit typed or spoken (see `CreditRules` in `Vocaterra.Game`). When a noun becomes known a toast
shows `¡Nueva palabra: el árbol!`. The journal (J) lists every catalogue noun: `el árbol` once seen (`¿?` before), its icon in its current
colour, the counts (heard, read, said, said correctly), three pips, and when a started word can earn colour again (`otra vez en 8 min`, or
`lista para repasar`).

**Spaced colour and the guide's review (P4b).** The uses must be spaced (D-game-6 as amended): the first correct use earns a third at once, the
second only 10 minutes of play time later, the third an hour after the second (a new play session lifts the gaps; the gaps are in `CreditRules`).
A correct use that comes too early is still said and answered, but the colour does not move: the game says `Bien. Repasa más tarde.` (a toast
and a line in the panel, in yellow). When a word is due and the player opens the dialogue with **T**, the guide points at the nearest example
(the arrow and a marker that shows `¿?`, not the noun) and asks `¿Qué es esto?`; the answer is graded against that tile, whatever the mouse
touches, and a correct one earns the next third. Closing the panel (Esc) drops the question and its arrow. Press **Esc** with the panel closed to
take any other arrow away.

## Architecture

```
Frontend/    plain classes, no engine type (a test fails if Microsoft.Xna or FontStashSharp appears here), tested without a GPU
  TickAccumulator   real frame time -> whole ticks; integer micro-ticks, cap of 5 steps per frame, surplus dropped; Alpha for interpolation
  IFrameClock       RealFrameClock (Stopwatch) and FixedFrameClock (one tick per frame, for --screenshot); the only timers in the project
  InputActions      the held actions (Left, Right, Jump); IInputSource.Poll(tick); CommandMapper -> PlayerCommand list; ScriptedInput
  Camera            follows a point in simulation units, clamps to the world, whole-pixel origin; VisibleTiles/VisibleChunks;
                    TileToScreen, UnitsToScreen and ScreenToTile (the mouse-to-tile conversion)
  TileArt           placeholder tile pixels (RGBA bytes, deterministic speckle, no System.Random)
  DebugText, FrameStats, LaunchOptions   (--say, --content, --point, --point-nearest and --journal too)
  Greyscale         the grey copy of an RGBA atlas: integer luma (299, 587, 114 over 1000), alpha kept; the tile atlas calls it once at load
  Journal           JournalRows (every catalogue noun: label, counts, pips, saturation, when the next credited use is possible; known first) and JournalModel (open or closed; opening counts a read)
  Toasts            ToastQueue: lines shown one after another for 240 ticks each, by the simulation's tick (no clock)
  ScriptedCursor    the --point and --point-nearest cursor for the scripted run (accent-insensitive noun lookup)
  TextEntry         the dialogue panel's text box: insert (Spanish letters and marks included), backspace, delete, caret, limits; AccentShortcuts
  InputFocus        who has the keyboard; FocusGatedInput returns no actions while the panel is open; KeyRepeater counts auto-repeat in frames
  DialogueModel     the panel as state: open or closed, the text box, the lines, a request to send; Submit shows the turn's lines; TextWrap
  ScriptedDialogue  the --say script: walk to the guide, settle, open, then each step in order: type and send a line, --skip (stored time), --talk (reopen)
  TurnTimer         times one guide turn for the panel's note (display only; the only other timer than the real clock)
(root)       the MonoGame wiring
  Program           parses the command line, runs VocaterraGame, returns its ExitCode
  VocaterraGame     Game subclass: clock -> accumulator -> poll input -> Step the simulation; follow; draw world then layers
  FrameView         what a layer may read for one frame (simulation, session, dialogue, camera, interpolated player box, fonts, a 1 px texture, mouse)
  IFrameLayer       "draw after the world": GuideLayer, PointerLayer, DebugOverlay, DialoguePanel, ToastLayer and JournalLayer, in that order; the hotbar later
  GuideLayer        the guide as a placeholder figure, her name and the "Pulsa T para hablar" hint
  PointerLayer      the arrow: a frame and a marker on the target tile, an arrow beside the player, an edge arrow when the target is off screen
  DialoguePanel     draws the DialogueModel: the lines (colour per speaker), the text box with its caret, the key hints
  DrawHelpers       rectangles, borders, rotated bars and shadowed text over the 1 px texture and the font
  WorldRenderer     visible tiles, each at its noun's saturation (`GameSession.Words`), the player rectangle
  JournalLayer      the journal screen: the list, icons in colour, the counts, the pips, the key hints
  ToastLayer        the "new word" line across the top, fading over its last half second
  TileAtlas         two textures in one layout (id n at x = 16 n): the colour atlas and its grey copy, made by `Greyscale` at load;
                    `DrawTile` draws the grey copy at (1 - s) and the colour copy with alpha s (premultiplied: `Color.White * s`), no shader
  FontProvider      FontStashSharp FontSystem over assets/fonts/PixelOperator.ttf
  KeyboardInput     the keyboard as an IInputSource (the only place that knows the key bindings)
  ScreenshotWriter  back buffer -> PNG
```

The frame is: `Update` reads the clock, reads the keyboard once (the panel's keys), the accumulator returns 0 to 5 ticks, and for each tick the
input source is polled once, mapped to `PlayerCommand`s and given to `GameSession.Step` (the simulation's step plus the arrow's reach check). A
line the player sent (Enter, or the script) is evaluated at the end of `Update`, after the tick loop and never inside a step; evaluation is
synchronous in P3 (about 4 ms warm, 18 ms for the first line after the warm-up) and P5 moves it to a worker. `Draw` interpolates the player box between the last two ticks (`Alpha`), moves the camera,
clears to sky, draws the visible tiles and the player, then every layer. The clock never reaches the simulation: it sees only `Step`.
MonoGame's own fixed-step timer is off (`IsFixedTimeStep = false`) and MonoGame's initial extra `Update` is skipped, so a frame is one
`Update` plus one `Draw`. The world is drawn at 2x (32 px tiles from 16 px art) with point sampling.

## The guide's dialogue (P3) and how P4 to P6 plug in

- **P3, the guide, typed (done).** `GameSession` (from `Vocaterra.Game`) is created in `Program.cs` before the window opens (bundle load, world
  generation and a warm-up evaluation, so a missing bundle is a message and an exit code, not a crash) and handed to `VocaterraGame`. The
  keyboard source is wrapped in `FocusGatedInput`, so the panel's open flag decides who gets the keys. Text comes from `Window.TextInput` (printable
  characters only; control keys, Alt accents, Enter and Esc are read from the keyboard state so nothing is typed twice, and the `t` that opens the
  panel is swallowed). `VocaterraGame` is declared `Microsoft.Xna.Framework.Game` in full, because the namespace `Vocaterra.Game` would hide the
  type from a lookup inside `Vocaterra.*`. The guide and the arrow are layers using `view.Camera.TileToScreen` and `UnitsToScreen`.
  Verified with real key events on a nested X server (Xephyr), not only by tests: walking with A, T, Alt+vowel accents, Backspace, Enter.
- **P4, greyscale rendering (done).** `TileAtlas` makes the colour atlas and the grey copy (`Greyscale`, on the CPU, no shader: research
  section 7.3); `WorldRenderer` draws each tile at its noun's saturation; the journal and the toast read the same counts. The art is still the
  code-drawn placeholders (D-game-5 allows them); a real PNG atlas loads with `Texture2D.FromStream` and is recorded in `assets/ATTRIBUTION.md`
  (CC0 only).
- **P5, the microphone.** Add an audio-capture class (MonoGame `Microphone`, or PortAudio if its audio is bad) behind an interface in a new
  folder; poll it from `Update`, and feed samples to `Vocaterra.Game`. Push-to-talk is another `IInputSource`-style flag read in `Update`;
  it does not go through `PlayerCommand`. Playback uses `DynamicSoundEffectInstance`.
- **P6, mining and building.** The mouse becomes a `PlayerCommand` with the tile as its two arguments: `view.Camera.ScreenToTile(mouse.X,
  mouse.Y)` is already there (the overlay shows it). Extend `InputActions`/`CommandMapper` or add a second mapper for the targeted
  commands (mouse buttons, hotbar digits), send them in the same per-tick loop, and check `World.Tiles.InBounds`. Tile changes come back
  through the simulation (dirty chunks, events); the renderer reads `World.GetTile` every frame, so nothing needs invalidating, and
  `Camera.VisibleChunks` is the range for any per-chunk cache P7's lighting wants.

## Tests

`tests/Vocaterra.MonoGame.Tests` (no GPU, no window): the text box and the accent shortcuts, input focus (the movement keys do not walk the
player while the panel is open, on a real simulation), the dialogue model's turn flow, key repeat, text wrapping, the `--say` script driven
headless against a real session and bundle (walk, open, type, send, the arrow, determinism), the accumulator (exact ticks, uneven frames, the cap and dropped ticks, bad input),
the greyscale copy (luma on a small buffer, alpha kept, a partial pixel refused), the journal rows and its read counts, the toast queue (order,
duration by tick), the scripted cursor (accent-insensitive nouns, nearest tree), the P4 command-line rules, the P4b `--skip` and `--talk` steps, the journal's due text and the scripted review (a skip, a talk, the question, the answer; determinism), the camera (centring, clamping at every edge, small worlds, visible tiles and chunks, screen/tile round trips), the command mapping and
scripted input driving a real simulation (and a determinism check over uneven frames), the command line, the overlay text, placeholder
art, FPS, the font (an own TrueType `cmap` reader checks `á é í ó ú ü ñ Á É Í Ó Ú Ü Ñ ¿ ¡` and the overlay's line; the file is copied to the
output; every character of the panel's texts and of the bundle's dialogue pools and prompts is in the font; the CC0 record exists) and
architecture rules (no engine type in `Frontend/`, no `System.Random`, clock or `Stopwatch` outside `FrameClock.cs`, project references only the
game layer and the simulation, no engine code in the simulation, the game layer or the host).

## Notes

- The sandbox or CI may have no display: `--screenshot` then needs `xvfb-run -a dotnet run ...`. The Linux runtime needs `libSDL2`
  and `libopenal` (the DesktopGL package ships its own copies under `runtimes/`).
- The back buffer is read after the frame is drawn and before the swap; on DesktopGL it comes back upright (checked), not flipped.
