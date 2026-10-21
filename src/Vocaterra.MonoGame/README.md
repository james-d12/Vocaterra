# Vocaterra.MonoGame

The game's window: the only engine code in the repository (plan section 3 rule 3, D-game-1). MonoGame 3.8.6 DesktopGL on `net10.0`, no
content pipeline (no MGCB: textures are made in code, text is a TTF read at run time through FontStashSharp). It references
`Vocaterra.Simulation` by project and no `Vocaterra.Nlp.*` package yet (P3 adds `Vocaterra.Game`, which brings them).

## Run

```bash
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet run --project src/Vocaterra.MonoGame -- --seed 42
```

| Option | Meaning |
| --- | --- |
| `--seed <ulong>` | the world seed (default 42) |
| `--screenshot <path.png>` | scripted run: fixed 60 Hz clock, scripted input, save the back buffer as a PNG, exit 0 (1 if the PNG cannot be written) |
| `--frames <n>` | with `--screenshot`: run until `n` ticks have been simulated, one per frame (default 120) |
| `--walk-right <ticks>` | with `--screenshot`: hold 'right' for the first `n` ticks |

Example, as the orchestrator verifies rendering: `... -- --seed 42 --screenshot /tmp/p2.png --frames 120 --walk-right 60`. It still opens a
window (it needs a display, `DISPLAY` or `xvfb-run`), but nobody has to look at it.

Controls: **A / D** or **Left / Right** walk, **Space / W / Up** jump (hold for a higher jump). The overlay (top left) shows FPS, tick,
seed, the tile under the player's feet, the tile under the mouse, and a Spanish line that proves the font draws `¿ ¡ á é í ó ú ü ñ`.

## Architecture

```
Frontend/    plain classes, no engine type (a test fails if Microsoft.Xna or FontStashSharp appears here), tested without a GPU
  TickAccumulator   real frame time -> whole ticks; integer micro-ticks, cap of 5 steps per frame, surplus dropped; Alpha for interpolation
  IFrameClock       RealFrameClock (Stopwatch) and FixedFrameClock (one tick per frame, for --screenshot); the only timers in the project
  InputActions      the held actions (Left, Right, Jump); IInputSource.Poll(tick); CommandMapper -> PlayerCommand list; ScriptedInput
  Camera            follows a point in simulation units, clamps to the world, whole-pixel origin; VisibleTiles/VisibleChunks;
                    TileToScreen, UnitsToScreen and ScreenToTile (the mouse-to-tile conversion)
  TileArt           placeholder tile pixels (RGBA bytes, deterministic speckle, no System.Random)
  DebugText, FrameStats, LaunchOptions
(root)       the MonoGame wiring
  Program           parses the command line, runs VocaterraGame, returns its ExitCode
  VocaterraGame     Game subclass: clock -> accumulator -> poll input -> Step the simulation; follow; draw world then layers
  FrameView         what a layer may read for one frame (simulation, camera, interpolated player box, fonts, a 1 px texture, mouse)
  IFrameLayer       "draw after the world": DebugOverlay now; the dialogue panel, hotbar and so on later
  WorldRenderer     visible tiles from the atlas, the player rectangle
  TileAtlas         one texture holding every tile id's art in a row (id n at x = 16 n)
  FontProvider      FontStashSharp FontSystem over assets/fonts/PixelOperator.ttf
  KeyboardInput     the keyboard as an IInputSource (the only place that knows the key bindings)
  ScreenshotWriter  back buffer -> PNG
```

The frame is: `Update` reads the clock, the accumulator returns 0 to 5 ticks, and for each tick the input source is polled once, mapped to
`PlayerCommand`s and given to `GameSimulation.Step`. `Draw` interpolates the player box between the last two ticks (`Alpha`), moves the camera,
clears to sky, draws the visible tiles and the player, then every layer. The clock never reaches the simulation: it sees only `Step`.
MonoGame's own fixed-step timer is off (`IsFixedTimeStep = false`) and MonoGame's initial extra `Update` is skipped, so a frame is one
`Update` plus one `Draw`. The world is drawn at 2x (32 px tiles from 16 px art) with point sampling.

## How P3 to P6 plug in

- **P3, the guide's dialogue panel.** Add a class implementing `IFrameLayer` (draw the panel with `view.Fonts` and `view.Pixel`) and add it to
  `VocaterraGame._layers`. Text entry needs `Window.TextInput` (MonoGame's character event) handled by the panel. While the panel has focus,
  wrap the input source so it returns `InputActions.None` (an `IInputSource` decorator that checks a flag), so typing `a` or `d` does not walk.
  The guide's arrow is another layer using `view.Camera.UnitsToScreen` or `TileToScreen`. `Vocaterra.Game` (`GameSession`) is created next to the
  `GameSimulation` in the game's constructor and stepped in the tick loop after `Step`; it owns the stored time (from `Tick`), never the clock.
- **P4, greyscale rendering.** `TileAtlas` is the one place that makes tile textures. Build a second atlas (the same ids, desaturated pixels
  made on the CPU, no shader toolchain: research section 3.3) and let `WorldRenderer` choose per tile from word progress (saturation per
  lemma). A real PNG atlas loads with `Texture2D.FromStream`; record each file in `assets/ATTRIBUTION.md` (CC0 only, D-game-5).
- **P5, the microphone.** Add an audio-capture class (MonoGame `Microphone`, or PortAudio if its audio is bad) behind an interface in a new
  folder; poll it from `Update`, and feed samples to `Vocaterra.Game`. Push-to-talk is another `IInputSource`-style flag read in `Update`;
  it does not go through `PlayerCommand`. Playback uses `DynamicSoundEffectInstance`.
- **P6, mining and building.** The mouse becomes a `PlayerCommand` with the tile as its two arguments: `view.Camera.ScreenToTile(mouse.X,
  mouse.Y)` is already there (the overlay shows it). Extend `InputActions`/`CommandMapper` or add a second mapper for the targeted
  commands (mouse buttons, hotbar digits), send them in the same per-tick loop, and check `World.Tiles.InBounds`. Tile changes come back
  through the simulation (dirty chunks, events); the renderer reads `World.GetTile` every frame, so nothing needs invalidating, and
  `Camera.VisibleChunks` is the range for any per-chunk cache P7's lighting wants.

## Tests

`tests/Vocaterra.MonoGame.Tests` (no GPU, no window): the accumulator (exact ticks, uneven frames, the cap and dropped ticks, bad input),
the camera (centring, clamping at every edge, small worlds, visible tiles and chunks, screen/tile round trips), the command mapping and
scripted input driving a real simulation (and a determinism check over uneven frames), the command line, the overlay text, placeholder
art, FPS, the font (an own TrueType `cmap` reader checks `á é í ó ú ü ñ Á É Í Ó Ú Ü Ñ ¿ ¡` and the overlay's line; the file is copied to the
output; the CC0 record exists) and architecture rules (no engine type in `Frontend/`, no `System.Random`, clock or `Stopwatch` outside
`FrameClock.cs`, project references only the simulation, no engine code in the simulation or the host).

## Notes

- The sandbox or CI may have no display: `--screenshot` then needs `xvfb-run -a dotnet run ...`. The Linux runtime needs `libSDL2`
  and `libopenal` (the DesktopGL package ships its own copies under `runtimes/`).
- The back buffer is read after the frame is drawn and before the swap; on DesktopGL it comes back upright (checked), not flipped.
