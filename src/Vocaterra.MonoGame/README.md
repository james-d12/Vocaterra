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
| `--walk <col>` | with `--screenshot`, a world step: walk until the player's middle is in that column (a ledge is jumped) |
| `--mine <col,row>` | with `--screenshot`, a world step: walk until the tile is in reach, then hold the mine command until it is gone (gives up after 1 200 ticks and says why) |
| `--place <item>@<col,row>` | with `--screenshot`, a world step: select the hotbar slot that holds the item (its Spanish noun, `madera`) and place it on the tile |
| `--select <slot>` | with `--screenshot`, a world step: select a hotbar slot, 1 to 10 as the number keys give them |
| `--inventory` | with `--screenshot`, a world step: open the inventory screen and keep it open for the screenshot (the next dialogue step closes it) |
| `--craft <item>` | with `--screenshot`, a world step (P6b): make one of the item (its Spanish noun, `mesa`, `puerta`, `horno`) from the inventory, with the stations in reach; prints `craft: mesa made (mesa 0 -> 1)`, or why it was refused (`MissingStation`, `MissingIngredients`, ...) |
| `--crafting` | with `--screenshot`, a world step: open the crafting panel and keep it open for the screenshot (the next step closes it) |
| `--speak "<text>"` | with `--screenshot`, an ordered step like `--say` (P5): Kokoro (speaker 28, `ef_dora`, through the core's `CachedSynthesizer`, cache `~/.cache/vocaterra/tts`) speaks the text, it is converted from 24 kHz to 16 kHz, Vosk recognizes it and it is evaluated as **spoken** input. The turn line prints `heard "..."`, the confidence (the mean of Vosk's word confidences) and the stage times. Needs the models (`VOCATERRA_MODELS_DIR`) |
| `--wav <file>` | with `--screenshot`, an ordered step (P5): feed a 16-bit PCM WAV (16 kHz mono, or any rate and several channels: it is converted and mixed down) as spoken input |
| `--listen <seconds>` | with `--screenshot`, an ordered step (P5): record the real microphone for 1 to 8 seconds as if the key were held, then send it (`microphone: ...` and `listened: ...` lines). With no microphone it says so |
| `--typed-credit <full\|half>` | the credit a correct typed use earns: `half` by default (six credited uses make a word known), `full` as before P5 (D-game-6); spoken input always earns full |
| `--no-speech` | do not load Vosk (typing only). A scripted run without `--speak`, `--wav` or `--listen` does not load it either |
| `--status <listening\|thinking>` | with `--screenshot`: show that status line of the panel (`Escuchando…`, `Pensando…`) once the script is done, for a screenshot of a state that is too short to catch |

The world steps run in the same order as `--say`, `--skip` and `--talk`, so `--select 1 --mine 517,182 --select 2 --walk 490 --mine 491,184 --place
madera@488,185 --say "¿dónde está la arena?" --inventory` works as one script. Each world step closes the dialogue first when it is open. A script
that starts with a world step does not walk to the guide; one that starts with `--say` or `--talk` does, as before.

The P6 demo (seed 42): the axe is slot 1 and the pickaxe slot 2 at the start, so the trunk at `517,182` (the tree next to the spawn) is cut with
the axe, the stone at `491,188` is reached through a two-tile hole at `491,184` and `492,184` (dug with the pickaxe, which makes the player fall
to the stone's level) and a plank is placed at `488,185`:

```
dotnet run --project src/Vocaterra.MonoGame --no-build -- --seed 42 --select 1 --mine 517,182 --mine 517,181 --select 2 --walk 490 \
  --mine 491,184 --mine 492,184 --mine 491,188 --place madera@488,185 --walk 524 --inventory \
  --screenshot p6.png --frames 1800
```

The P6b demo (seed 42): fell a tree, ask how a table is made while short of wood, gather more, craft and place the table, craft a door, ask
where sand is and where the axe is, and open the panel:

```
dotnet run --project src/Vocaterra.MonoGame --no-build -- --seed 42 --frames 6000 --mine 517,182 --say "¿cómo hago una mesa?" \
  --mine 497,182 --mine 494,182 --craft mesa --place mesa@496,182 --say "¿cómo hago una puerta?" --craft puerta \
  --say "¿dónde hay arena?" --say "¿dónde está el hacha?" --crafting --screenshot p6b.png
```

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

**Speaking (P5).** Hold **Tab** while the panel is open, or hold **V** next to the guide (it opens the panel and starts listening), and let go to
send. The panel shows `Escuchando…` while the key is held and `Pensando…` while the turn is on the worker; the line you said appears as
`Tú (voz): ...` with the confidence ("oído con 96 %"). A clip is trimmed of silence at both ends, converted from the microphone's 44.1 kHz to 16 kHz
and cut at 8 seconds (recording stops by itself there). Spoken turns earn full credit and typed turns half (see `CreditRules`); the turn line printed
to the console says which. With no microphone, no models or no manifest the panel gives a short Spanish message and typing is unaffected. While the
models load a loading screen (`Cargando la voz…`) covers the world and nothing in it is interactive.

Controls: **A / D** or **Left / Right** walk, **Space / W / Up** jump (hold for a higher jump), **J** opens the journal (the words, see below; **J** or
**Esc** closes it, and the player does not walk while it is open). Next to the guide (within 5 columns), the hint
"Pulsa T para hablar" shows; **T** opens the dialogue panel, which takes the keyboard: the movement keys then type letters (input focus), **Enter**
sends the line, **Esc** closes the panel, **Left / Right / Home / End / Backspace / Delete** edit it. With the panel closed, **Esc** takes the
guide's arrow away. For Spanish on a keyboard that cannot type it: **Alt + a vowel** gives `á é í ó ú` (with Shift, the capital), **Alt+N** `ñ`,
**Alt+/** `¿`, **Alt+1** `¡`; the window's own text events (a Spanish layout, a compose key) work as well. The overlay (top left) shows FPS, tick,
seed, the tile under the player's feet, the tile under the mouse, and a Spanish line that proves the font draws `¿ ¡ á é í ó ú ü ñ`.

**Words (P4).** The world is grey until the learner has used its word. Say `es un árbol` to the guide with the mouse on a tree (or `es una
piedra` on a stone): three correct uses make the noun known, and its objects go from grey to full colour in thirds (one third per credited
use). A correct spoken use earns a full use and a correct typed use half a use (D-game-6 from P5; `--typed-credit full` restores full credit for typed input),
so a word takes three spoken uses or six typed ones. A word's pips light for any credit (half a use lights one) and all three only when it is known. When a noun becomes known a toast
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

## Mining, placing and the inventory (P6)

- **Mouse**: the left button mines the tile under the pointer while it is held (the tile must be in reach, five tiles, and the selected
  item must be the right tool); the right button places the selected item on the tile under the pointer, once per press. Both do nothing while
  the dialogue, the journal or the inventory has the screen.
- **Hotbar**: the number keys 1 to 9 and 0 select slots 1 to 10, the mouse wheel moves the selection one slot. The strip at the bottom shows
  the first ten slots with their icons, counts and keys; the selected slot is outlined in gold. The player starts with the axe (slot 1) and the
  pickaxe (slot 2).
- **Inventory**: **I** or **E** opens the 40 slots (the top row is the hotbar); Esc, I or E close it. Each item shows its icon and its Spanish word
  (`madera`, `piedra`) coloured by the word's saturation, and its count. Opening the screen counts each shown word as read once (never as a
  spoken credit). The player does not walk while it is open.
- Stack counts are drawn white on an opaque dark plate (`CountBadge`, `DrawHelpers.Badge`) in the hotbar and the inventory, so they read on every
  icon (the plank icon is pale; P6 drew the count straight on it). A test checks the colour contrast. Inventory labels show the lemma
  (`madera`), not the article: the slot is 64 pixels wide, and the guide, the journal and the panel's rows give the article.
- Placeable blocks and tools have their icons in the atlas (`TileArt`, drawn in code); the colour and grey rule is the one the tiles use. No
  art file was added, so `assets/ATTRIBUTION.md` is unchanged.

## Crafting (P6b)

- **Panel**: **C** opens it (Esc or C closes it; it does not open over the journal or the inventory). It lists every recipe of the bundle
  (`Session.Recipes`): the output's icon and Spanish word, each ingredient as `have/need word` (green when the inventory has enough, red when it
  does not), the station (`Usa mesa`, or `A mano`) and a status at the right: `Hacer` when it can be made here, else `Sin material`, `Sin mesa` / `Sin horno`
  (the station is not within 6 tiles) or `Inventario lleno`. A click on a row sends one `PlayerCommand.Craft`; nothing mines or places while the panel is
  open and the player does not walk. There is no tab in the inventory: the panel is its own key.
- **Colour rule**: every noun (output, ingredient, station) is drawn grey until its word is used, then in colour, like the inventory's labels.
  Opening the panel counts each shown noun as read once (`CraftingScreen.Open`, once per opening, never per frame); a read never credits a spoken use.
- **Model**: `Frontend/CraftingScreen` (open state, `Rows`, `LemmasShown`), `CraftingLayout` (the geometry, shared by drawing and the mouse hit test) and
  `CountBadge` are plain values, so tests click a row and check the counts without a window. `CraftingLayer` only draws.
- **Art**: the furnace, glass, brick and the sword are drawn in code like the other placeholders; no file was added, so `assets/ATTRIBUTION.md` is unchanged.

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
  DialogueModel     the panel as state: open or closed, the text box, the lines, a request to send; Submit shows the turn's lines; TextWrap.
                    P5: Pending (Pensando…), Listening (Escuchando…), SpeechNotice, a scripted spoken request, TakeLine and Complete for the worker's turns
  PushToTalk        the push-to-talk state over an ISpeechCapture (the device): Begin, Pump (collects, caps at 8 s), End (a trimmed 16 kHz clip or null)
  TurnPump          Submit (the typed line and the scripted speech to the worker) and Apply (the finished turns, in order, to the session and the panel)
  BusyFrameStats    the frame lengths while loading, listening or thinking: the printed proof that the frame does not block
  ScriptedDialogue  the --say script: walk to the guide, settle, open, then each step in order: type and send a line, --skip (stored time), --talk (reopen)
  TurnTimer         times one guide turn for the panel's note (display only; the only other timer than the real clock)
(root)       the MonoGame wiring
  Program           parses the command line, runs VocaterraGame, returns its ExitCode
  VocaterraGame     Game subclass: clock -> accumulator -> poll input -> Step the simulation; follow; draw world then layers
  FrameView         what a layer may read for one frame (simulation, session, dialogue, camera, interpolated player box, fonts, a 1 px texture, mouse)
  IFrameLayer       "draw after the world": GuideLayer, PointerLayer, DebugOverlay, DialoguePanel, ToastLayer and JournalLayer, in that order; the hotbar later
  GuideLayer        the guide as a placeholder figure, her name and the "Pulsa T para hablar" hint
  PointerLayer      the arrow: a frame and a marker on the target tile, an arrow beside the player, an edge arrow when the target is off screen
  DialoguePanel     draws the DialogueModel: the lines (colour per speaker), the text box with its caret, the status (Escuchando…, Pensando…), the key hints
  LoadingLayer      the loading screen while the turn worker loads the speech models
  MicrophoneCapture MonoGame's Microphone as an ISpeechCapture (the only capture code; DesktopGL/OpenAL, 44.1 kHz mono, converted by AudioClips)
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
line the player sent (Enter, a released push-to-talk key, or the script) is handed to the turn worker at the end of `Update`, after the tick loop
and never inside a step; the frame goes on, and the start of a later `Update` applies the finished turns in the order they were submitted
(P5: evaluation runs on one dedicated thread that owns Vosk; before it was synchronous). While the models load the loading screen shows and the world
does not step. `Draw` interpolates the player box between the last two ticks (`Alpha`), moves the camera,
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
- **P5, spoken input (done).** `MicrophoneCapture` (MonoGame `Microphone`, which works here; PortAudio was not needed) sits behind
  `ISpeechCapture`; `PushToTalk` and `TurnPump` are plain classes in `Frontend/`. Push-to-talk is a key read in `Update` (Tab in the panel, V next to
  the guide), not a `PlayerCommand`. MonoGame 3.8.6 returns nothing from `Microphone.GetData` unless something subscribes to `BufferReady`, so the
  capture subscribes an empty handler. Playback (P8) will use `DynamicSoundEffectInstance`.
- **P6, mining and building.** The mouse becomes a `PlayerCommand` with the tile as its two arguments: `view.Camera.ScreenToTile(mouse.X,
  mouse.Y)` is already there (the overlay shows it). Extend `InputActions`/`CommandMapper` or add a second mapper for the targeted
  commands (mouse buttons, hotbar digits), send them in the same per-tick loop, and check `World.Tiles.InBounds`. Tile changes come back
  through the simulation (dirty chunks, events); the renderer reads `World.GetTile` every frame, so nothing needs invalidating, and
  `Camera.VisibleChunks` is the range for any per-chunk cache P7's lighting wants.

## Tests

`tests/Vocaterra.MonoGame.Tests` (no GPU, no window, no microphone; P5: the push-to-talk state over a fake device, the panel's pending and
listening states, the turn pump against a real worker, the new launch options, the scripted `--speak` step with the real models when
`VOCATERRA_MODELS_DIR` has them and skipped with a reason otherwise): the text box and the accent shortcuts, input focus (the movement keys do not walk the
player while the panel is open, on a real simulation), the dialogue model's turn flow, key repeat, text wrapping, the `--say` script driven
headless against a real session and bundle (walk, open, type, send, the arrow, determinism), the accumulator (exact ticks, uneven frames, the cap and dropped ticks, bad input),
the greyscale copy (luma on a small buffer, alpha kept, a partial pixel refused), the journal rows and its read counts, the toast queue (order,
duration by tick), the scripted cursor (accent-insensitive nouns, nearest tree), the P4 command-line rules, the P4b `--skip` and `--talk` steps, P6b's crafting panel (rows, layout hits, one click one craft, reads once per opening), the `--craft` and `--crafting` steps, the count badge's contrast and the new art, the journal's due text and the scripted review (a skip, a talk, the question, the answer; determinism), the camera (centring, clamping at every edge, small worlds, visible tiles and chunks, screen/tile round trips), the command mapping and
scripted input driving a real simulation (and a determinism check over uneven frames), the command line, the overlay text, placeholder
art, FPS, the font (an own TrueType `cmap` reader checks `á é í ó ú ü ñ Á É Í Ó Ú Ü Ñ ¿ ¡` and the overlay's line; the file is copied to the
output; every character of the panel's texts and of the bundle's dialogue pools and prompts is in the font; the CC0 record exists) and
architecture rules (no engine type in `Frontend/`, no `System.Random`, clock or `Stopwatch` outside `FrameClock.cs`, project references only the
game layer and the simulation, no engine code in the simulation, the game layer or the host).

## Notes

- The sandbox or CI may have no display: `--screenshot` then needs `xvfb-run -a dotnet run ...`. The Linux runtime needs `libSDL2`
  and `libopenal` (the DesktopGL package ships its own copies; on a Linux x64 build they are copied next to the executable, see
  `docs/ARCHITECTURE.md` for the native-asset trim).
- The back buffer is read after the frame is drawn and before the swap; on DesktopGL it comes back upright (checked), not flipped.
