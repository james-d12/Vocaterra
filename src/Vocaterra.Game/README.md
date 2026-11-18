# Vocaterra.Game

The game's window: the only engine code in the repository (plan section 3 rule 3, D-game-1). MonoGame 3.8.6 DesktopGL on `net10.0`, no
content pipeline (no MGCB: textures are made in code, text is a TTF read at run time through FontStashSharp). It references
`Vocaterra.Game.Core` (the game's engine-free systems: the world and its tick, the session, the guide, the word progress, the screens' models;
it brings the language core's projects) by project and names no `Vocaterra.Nlp.*` package itself. Its folders are in
`docs/game/architecture.md`.

The prototype's world is the first-person voxel scene (S2 to S4). The 2D slice (P1 to P7) was retired at S4; its last commit is tagged
`2d-slice-p7`.

## Run

```bash
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet run --project src/Vocaterra.Game -- --seed 42
```

| Option | Meaning |
| --- | --- |
| `--seed <ulong>` | the world seed (default 42) |
| `--content <dir>` | the content bundle folder (default: `content/game-v1` found by walking up from the executable, then from the current directory; `VOCATERRA_CONTENT_DIR` also works) |
| `--scene <file>` | the layout file (default: the bundle's `game/scene.json`) |
| `--quality <low\|medium\|high>` | S13 and S14: the view distance in blocks (low 224, medium 304, high 400, the default; a real GPU), and what is drawn: **plant distance and share** (the undergrowth is drawn within 64, 112 or 192 blocks of the eye, 80, 100 or 100 percent of it built), **ambient occlusion** (on at all three; the preset says, so a slower machine can turn it off) and **clouds**. Only the chunks within the view distance are meshed and drawn, the fog is complete just inside it and the camera's far plane stops past it. Medians on this VM (software GL), 1280 x 720: low 42 ms from the camp and 32 ms in the forest, medium 52 ms, high 49 ms (see the S14 line of the plan's findings) |
| `--world <streamed\|valley>` | B07: the streamed tall world round the valley (the default) or the bounded valley alone; see `docs/game/running.md` |
| `--workers <auto\|1-8>` | B07d: the worker threads for the run (the settings page's Worker threads, saved): auto is the processor count less four, at least 1, at most 8, the default for play; a scripted run keeps 2 unless this is given |
| `--prefetch <0\|1\|2>` | B07: the old worker option, 0 to 2, winning over `--workers` (0 keeps everything on the game loop's thread) |
| `--far-distance <auto\|km>`, `--far-detail <50-200>`, `--far-trees <auto\|off\|256..4096>`, `--far-textures <0-3>`, `--far-exact <auto\|off\|500..2000>` | B07d: the far view's settings of the Graphics group (distance in km 1 to 16, detail in percent in steps of 25, single trees, textured levels, real-chunk radius), each saved and set for the run by its option; auto and the defaults are the quality's own |
| `--pregen <blocks>`, `--pregen-at <x,z>` | B07d: makes the far view's real sections round a point (default where the run starts) out to the radius on all the workers, prints progress and exits with no window; a later run finds them on disk |
| `--far-view <off\|blocks>` | B07: the far view's range in streamed worlds (256 to 65536; default the quality's: 4096, 8192, 16384) |
| `--no-settle` | B07: a scripted run streams the world as a live one does and does not wait for everything to be in place before a picture |
| `--gl-shaders [on\|off]` | B17b, B17c: every chunk pass (the opaque faces, the lit pieces, the cut-outs, the grass, the water; the lanterns' glow is the block light in the faces' own vertices since B17c, with the light as data the program combines with the hour, the weather and the glow, a fog by height and the water shader; the far view too) is drawn through the game's own GLSL program (`Graphics/Gl`, `chunk.vert.glsl`, `chunk.frag.glsl`) on MonoGame's OpenGL context; on by default (the settings page's Shaders row, kept in a save's settings), `off` draws with MonoGame's effects as before, and so does a context below OpenGL 3.3 or a program that does not compile (one `gl:` log line says which). A bare `--gl-shaders` is on; wins over the saved setting for the run; see `docs/game/b17-spike.md` |
| `--sun-follows-hour [on\|off]` | B17c: the faces are shaded by the sky's sun through the day with the deeper dusk (B22's sun per band); on by default (the settings page's Graphics group); off keeps the afternoon sun at every hour and the older, lighter dusk; wins over the saved setting for the run |
| `--climate-colours [on\|off]` | B17c: the grass and the oak leaves take the colour of their climate (B22's land tint, the near chunks and the far view's tiles); on by default; off is the colour map's middle everywhere (the far tiles are built again) |
| `--water-shader [on\|off]` | B17c: the water's depth tint (B22), the gentle movement of its surface and a brighter edge at grazing angles (the movement and the edge need the shaders); on by default; off is plain water |
| `--height-fog [on\|off]` | B17c: the haze lies thicker in the low land, in the colours of the far view's (needs the shaders); on by default |
| `--far-light [on\|off]` | B17c: the far view is drawn through the same program as the chunks, in the same sun, fog and haze (needs the shaders); on by default; off draws it as before, in the day's baked light |
| `--stream-stats` | B07: a scripted run prints the residency, mesh and far-view counts and timings on a `stream:` line |
| `--view-distance <blocks>` | draw this far instead of the quality's distance (48 to 576 in steps of 16; the menu's Draw distance, whose Auto is the quality's own) |
| `--grass <0-100>` | B18: the decorative grass layer's density in percent, 0 to 100 in steps of 10 (the settings page's Grass slider; default 60; 0 is the look of before, no tuft drawn or built); wins over the saved setting for the run |
| `--fov <degrees>`, `--mouse-sensitivity <percent>`, `--invert-y on\|off` | the menu's Field of view (60 to 110 in steps of 5), Mouse sensitivity (25 to 300 in steps of 5) and Invert mouse Y, for a run; the save's value otherwise |
| `--voice-volume <0..1>`, `--ui-volume <0..1>` | the menu's Voice volume and Interface sounds volume, for a run (with `--sfx-volume`, `--ambient-volume` and `--weather-volume`) |
| `--screenshot <path.png>` | scripted run: fixed 60 Hz clock, the steps below in order, save the back buffer as a PNG, exit 0 (1 if the PNG cannot be written) |
| `--frames <n>` | with `--screenshot`: run at least `n` ticks before the screenshot (default 120) |
| `--look <yaw>,<pitch>` | a step: face this direction in degrees (yaw 0 north, 90 east, clockwise from above; pitch positive up, within 89) |
| `--walk-to <x>,<z>` | a step: walk with `Move` commands to the middle of that block column, within half a block, or give up after 14 400 ticks (S13: the way is planned over the standable cells (`WalkPlanner`, A*: up one block, down three, no wading, no one-block gaps between trees, a dearer cell beside a trunk), so a walk follows the trail and the road, and the hops run along the eight directions); sidesteps and jumps when stuck. Prints `walk-to 60,52: arrived at (60.02, 52.41) after 800 ticks` |
| `--teleport <x>,<z>` | a step (S13, a debug step for screenshots): stand the player on the ground of that column (the highest block that is not a tree, with room above it). Prints `teleport 230,222: standing at 230,35,222` or why not |
| `--interact <x>,<y>,<z>` | a step: aim at the block and hold `Interact` until it is gathered or 600 ticks pass (the player must be within 4 blocks). Prints `interact 9,11,17: gathered trunk in 60 ticks (now air, +1 wood)` |
| `--place <item>@<x>,<y>,<z>` | a step: place one item (`madera`, `antorcha`, or the item key `wood`, `torch`) in that empty cell next to a block, within reach |
| `--select <n>` | a step: hotbar slot 1 to 10 |
| `--craft <item>` | a step: craft the recipe that makes the item (`antorcha`), with the workbench in reach; prints `craft: antorcha made (antorcha 0 -> 1)` or why it was refused |
| `--talk <npc>` | a step (S6): walk to the NPC (an id of `game/npcs.json`: `guide`, `shopkeeper`, `villager`, `fisher`, `baker`, `smith`, `farmer`, `innkeeper`), stop at the stand-off (`NpcStandOff`, about two blocks, a clear line to its chest) facing it, open the dialogue panel on it; the NPC's opening is printed (`talk: shopkeeper (host turn, stall-need): ¿Qué necesitas?`) |
| `--say "<line>"` | repeatable, a step: type the line into the panel and send it to the NPC it is open on, and wait for the reply; with no panel open it first talks to the guide (as in S3); each turn is printed (`turn 1 (guide, guide-open): ... -> ask_directions, entendido`, the NPC's lines, the arrow, then the game events it caused) |
| `--leave` | a step: close the dialogue (a `--walk-to` or another `--talk` closes it as well) |
| `--shot <png>` | a step: save the screen at this point of the script and go on (the final `--screenshot` is saved when the script is done) |
| `--beacon <point>` | stand the beacon column on a named point of the scene (`camp.guide`, `camp.player`, `firering`, `workbench`, `stall`, `signpost`, `village`, `house1` to `house3`, `inn`, `bed.inn`, `npc.innkeeper`, `bridge`, `river`, `lake`, `waterfall`, `trail`, `meadow.north`, `meadow.south`); it clears when the player comes within 3 blocks. A new game starts with it on `camp.guide` (the walk down the trail from the player's own camp); `--beacon none` puts none up. The beacon is not fogged |
| `--book`, `--book-page <page>`, `--journal`, `--journal-phrases`, `--inventory`, `--crafting` | open those screens once the steps are done (the panel stays open for the screenshot). S20: `--book` (the same as `--journal`) opens the book on its nouns page, `--book-page nouns\|verbs\|phrases\|lessons\|said` on the named page, and `--journal-phrases` on the phrases page (S8). Tab, Left and Right turn the page; the wheel, Page Up and Page Down scroll |
| `--wait <seconds>`, `--skip <seconds>` | script steps (S8): run the game on that long; move the stored time on without waiting (so a spacing gap passes and the guide's phrase review is due) |
| `--typed-credit <full\|half>` | the credit a correct typed use earns: `half` by default, `full` as before P5; spoken input always earns full |
| `--no-speech` | do not load Vosk (an interactive run loads it unless this is given; a scripted run never does) |
| `--third-person` | start in the behind-the-shoulder view (S15); **F5** switches between it and first person in an interactive run (first person is the default). The player's own figure is drawn with its backpack, and the arm is not |
| `--dusk` | an evening (S15, E02): holds the day clock at 21, the middle of a dusk: an amber and rose sky and haze, a lower, warmer sun on the blocks, the figures and the arm, and the lanterns and the pergola's torch glowing at full strength (by day they glow at half) |
| `--time <hour>` | holds the day clock at an hour from 0 to 24, fractions allowed (E02): the sky, the fog, the sun and the moon, the stars and the light of the world show that hour; `--time` wins over `--dusk` for the clock |
| `--greyscale` | the playtest A/B switch (D-game-6 as amended): blocks whose noun is not known yet are grey, known ones in colour (`GreyscaleWorld`, `WordColours`; blocks no noun names, the figures, the sky and the UI stay in colour); a noun that becomes known during play regains its colour and toasts. Default: full colour |
| `--assume-known <nouns>` | a test aid: the comma-separated catalogue nouns count as known from the start (`--assume-known árbol` colours the trunks for an A/B shot); not a play path |
| `--voxel` | accepted and ignored (the 3D world is the only one since S4) |

The steps run in the order given, one at a time, each at the simulation's fixed 60 Hz (one tick per frame on the fixed clock). The screenshot is
saved after `--frames` ticks and once the last step has finished and 30 ticks have settled. `LookControl`, `VoxelScript` and `LiveVoxelInput` hold
the logic; `VoxelGame` only wires it to MonoGame.

**Controls.** WASD or the arrows walk (`MoveRelative`), Space jumps, the mouse looks. The mouse is captured in SDL's relative mode (MonoGame 3.8 has no
switch for it, so `MouseLook` calls `SDL_SetRelativeMouseMode` and `SDL_GetRelativeMouseState` in the SDL library MonoGame loaded, and warps the
pointer to the middle of the window if that library cannot be reached); **Esc** frees the mouse and a click takes it back. Hold the **left
button** on a gatherable block (a trunk, a berry bush) to gather it, the **right button** places the selected item in the cell in front of the
face the crosshair is on, `1`..`0` and the wheel pick the hotbar slot, **I** or **E** the inventory, **C** (or **E** with the crosshair on the
workbench) the crafting panel, **E** at the player's own lit fire once the session's work is done (or **Enter** on the closing card) a sleep until the next play session (V12c: `SleepOffer`,
`SaveGame.Sleep`; `docs/game/lessons.md`, "The next session: sleep at the fire"), **J** the book (S20; the selected book item, used with the right button, opens the same book), **F3** the debug overlay. The camera eases over a step-up (`CameraEase`: the eye lifts a block in
one tick; the camera holds the old height and lets it go over 8 ticks).

## What is drawn

`ChunkMesher` (Frontend, no engine type) turns each 16 x 16 x 16 chunk into quads: a cube face is emitted unless the block next to it is an opaque
cube (or the same kind of transparent block: leaves against leaves, of any tree; water against water), so a block shut in on all sides has no face. **The light (S14)
is in the vertex colours**: each face is shaded by a sun that stands high in the north-west by day (`ChunkMesher.SunDirection`, the Day band's sun; B22: **each light band bakes its own sun**, `LightBands.SunDirectionOf`: the sky's own
sun at 5.8 o'clock low in the east for the Dawn band and at 20.2 low in the west for the Dusk band (`SkyArc`, so the faces are lit from where the sky's sun stands), the moon at midnight for the Night band; a face's share of the sun is its
normal's dot with that direction over the *Day* sun's height, so a low sun lights its own side fully and the tops only a fraction: the ground darkens as the sun goes down, `ChunkMesher.SunShare(face, band)`; the far view and the figures keep the Day band's directions),
so the top is full and warm (a tint of 1.0, 0.98, 0.92), the west and north faces about two thirds of the way to it, and the east, south and bottom faces dark and
blue (the shade fills with sky); and every cube vertex is darkened by its **ambient occlusion** (0 to 3 of the three blocks that touch the corner: a vertex keeps 1.0, 0.82,
0.66 or 0.5 of its light), the quad's diagonal turned to follow the darker corners. Stone is also tinted by height (`RockTint`: warm and earthy at the foot of the
mountains, grey in the middle, cool on the peaks). The mesher reads the chunk and a border of one block once into an array, so the occlusion costs no more than a block lookup.
**The grass layer (B18, D-game-34 item 34).** On the top face of a grass block (the tops named in `Graphics/World/GrassBiomes.json`) with air over it stands a tuft: two crossed cut-out quads of the tall grass's picture, in one of three heights and three tints, jittered inside its own cell and narrower than it, so it never reaches over a path, a doorstep or a crop beside it. A tuft's presence, height and tint come from a hash of the cell alone (`GrassMesher.CellHash`), so tufts stand still as the eye moves and are the same in every run; nothing under, on or over a plant, a path, farmland, snow, a door or any block carries one. How many columns carry one is the biome's weight (the same file: lush in plains and meadows, 0.85 to 0.9 on the forest floors, 0.4 in the taiga and 0.5 in the grove, 0.3 elsewhere; the bounded valley, which has no place query, counts 1) times the slider times the quality's cap (`QualityPreset.GrassCapPercent`: 40, 70 and 100 at low, medium and high); a tuft shows when its hash rank is under that product, so raising the density only adds tufts. It is render only: no block, no collision, not edited, not saved, and the weights are not in the world's files, so no setting can move a digest. The layer is its own pass, not part of the chunk's meshes: `VoxelRenderer` builds a chunk's layer (`GrassMesher`) lazily when the chunk comes within the quality's `GrassDistance` of the eye (48, 72 and 96 blocks), nearest first inside the frame's mesh budget, and lets it go well beyond; a chunk meshed again loses its layer and gets a new one. The quads of a layer are ordered by quadrant (8 x 8 columns) and by density rank, so drawing is a prefix of each quadrant: the slider picks how many rank tiers, and the distance thins them over the outer half of the reach (a tuft at a time, a quadrant at a time), so there is no edge and no pop; moving the slider between two values above 0 builds nothing again. The plants do not sway in this game, so neither does the grass.

Each chunk gives three meshes: the **opaque** one (cubes, leaves and bushes: their pictures have cut-out pixels), the **plants** (the undergrowth as two crossed quads, a stable
pick by position when the preset builds only a share) and the **water**, which goes into a second mesh drawn after the opaque faces, chunk by chunk from the farthest
to the nearest, reading but not writing depth, with its surface a sixteenth lower. The water's top face takes its picture by depth (a lighter, clearer one over the shallow bed, a
deeper blue over deep water) and its transparency per corner (clearer where a solid block touches it), so the sand shows through at the shore; a column of water that falls
(water above it, air beside it: the waterfall) shows the streaky foam picture, unshaded. The bush, the berry bush, the torch (a stick with a flame; the flame is the prototype's light),
the open door and the fence are small shapes of their own. **The atlas** is `BlockArt` plus `BlockArtNature` (S14): 16 x 16 pixel art per cell (8 columns, 7 rows of colour cells, and below
them a grey copy of each cell, the luma from `Greyscale`), drawn in code from a fixed integer hash and fixed palettes (CC0 by construction, no picture file): grass top and
side (the green bleeding over the dirt), dirt, stone, mossy stone, cobble, snow top and side, sand, the gravel path, the bark and the rings of the oak, the spruce and the birch,
planks, three kinds of leaves with cut-out holes, the bush, tall grass, ferns, reeds with cattails, a yellow and a white flower, shallow and deep water and the waterfall. The village's
cells (the stall, the houses, the signpost, the workbench) and the item icons were redone in S15 (`BlockArtVillage`: timber, lantern, crates, chest, the red and white awning, canvas, farmland, wheat, the bread, apple and bottle piles, the five boards of the signpost in a 3 x 5 pixel font, the faces, cloth, skin and hair of the figures, the backpack and the torch icon; the atlas grew to 12 rows of colour cells). The texture is made with its mip chain (`BlockArt.MipLevel`, each
level averaged cell by cell so no level ever mixes one cell with the next, and with the colours weighted by alpha so the leaves do not darken) and sampled with `PointMipLinear`: crisp
pixels close up, smooth and in the right colours far away, no shimmer. `VoxelRenderer` uploads one vertex buffer per chunk and mesh and draws with MonoGame's own effects (no custom shader:
`mgfxc` needs Wine on Linux): `AlphaTestEffect` for the opaque faces and the plants (texture times vertex colour, the cut-out pixels dropped at alpha 110, fog in the haze colour) and `BasicEffect`
for the water and the small overlays. The order is the sky (`SkyRenderer`, drawn from a `SkyLook`: a dome with a gradient from deep blue overhead to the pale haze at the horizon, which is the fog's colour, so the far mountains
fade into haze instead of ending in a wall; square stars at hashed places that fade in with the night; a square sun with a stepped glow on its arc over the day; a square moon at night; and a layer of flat blocky clouds from `CloudField`, a hash of the grid cell, drifting east with the tick, melting into the haze
with distance), then the opaque chunks, the plants of the chunks near the eye with no back-face culling, the figures and the highlight, the water and the beacon. At the scale of the 384 x 384 x 128 world (S13) the renderer meshes and draws only
the chunks within the quality preset's view distance of the eye (`ChunkViewSet`, nearest first, rebuilt when the eye changes chunk, the view changes or a chunk gains its first block or loses its last: it hears block changes from the grid, and an ordinary edit costs it nothing): a chunk of air is never in the set,
a chunk full of opaque blocks all round is skipped without meshing (`ChunkMesher.IsBuried`), and a mesh is let go when its chunk is more than two chunks beyond the view. The chunks drawn are culled against the camera's frustum.
**The remesh path (B21).** `Remesh` builds no mesh itself: `ChunkMeshPipeline` keeps the lists of what to build (a chunk with no mesh, a chunk a block change or a light or the sky's reach touched, a chunk in another light band) and the game thread, within the frame's budget,
takes a `ChunkSnapshot` of the blocks a chunk reads (slab copies of the resident chunks' arrays: the chunk, a layer round it, two layers under it, the blocks its lights stand on, the river's currents), hands it with the lights, the sky's tile and the grey set to a worker of the job pool (`JobLane.Mesh`), and puts the vertices that come back on the card
(`VoxelRenderer` implements `IChunkMeshSink`: `Install`, `InstallGrass`, `Release`). The decorative grass layers go the same way (a chunk's 16 biome ids are read on the game thread first). Every chunk has a stamp that a change of anything its mesh reads bumps, and a result whose stamp is not the chunk's is refused and the chunk built again, so the card never holds a mesh of blocks older than the grid's;
the old mesh is drawn until the new one replaces it. A block change lists the chunk and every neighbour that reads the cell (face, edge or corner: the corner shading reads the diagonal neighbour, so a corner vertex no longer keeps a stale shade) and the chunk above for the water's depth; a chunk edited within 40 blocks of the eye is meshed on the game thread in the frame of the edit (at most four a frame), so a block broken or placed shows at once. A frame with nothing to do looks at nothing: the lists are walked a window (512) at a time, in the frustum first and nearest first, instead of the old pass over every wanted chunk's key. With no worker (`--prefetch 0`, tests) the same jobs run on the game thread inside the budget; a scripted run's `Remesh` with no budget waits for the workers, so a picture is of a finished view.
`--stream-stats` prints the game thread's allocation in the streaming and meshing phases and the pipeline's counters. Triangles run counter-clockwise seen from outside and the renderer culls clockwise ones.Triangles run counter-clockwise seen from outside and the renderer culls clockwise ones. The NPCs are figures of textured boxes
(`NpcFigures`, `FigureMesher`: the atlas's cloth, skin and hair cells tinted by the part, a face on the head, shaded with the blocks' sun table) at the scene's NPC placements. Each frame the mesher
rebuilds a figure into one reused buffer (no allocation) from its pose (`FigureAnimation`: idle, talking, walking), and `FigureFacing` turns a figure in a conversation to the player. The plates of the NPC the player faces within talking range and of the beacon's NPC hang over their heads
(`NamePlates`, `NpcLabelLayer`: drawn in the world's pass, depth-tested against the scene and the figures, not for the speaker, who has the bubble). `FirstPersonCamera` builds MonoGame's view and projection from `CameraBasis` (the same
vectors as the simulation's look vector, and MonoGame is right-handed with a camera looking down -z, so x east, y up, z south need no flip);
`ScreenProjection` is the same camera as plain maths for the labels and the beacon's arrow. **`--greyscale` (D-game-6)** keeps its meaning: a block whose noun is not known is drawn
from the grey copy of its cell, a block no noun names stays in colour. The new trees are named by `árbol` and the new rock by `piedra` in `nouns.json`, so they go grey and regain colour with their words;
the undergrowth, the snow and the bushes are named by no noun and stay in colour, as the bush always did.

**The camp objective and the word cards (S4).** The checklist (`ObjectiveLayer`, top right) reads `Campamento: madera 5/5 · frutas 3/3 · antorcha
1/1 · agua 0/1`: a done step in green, an open one in white. Each step that completes shows a toast (`Done: frutas 3/3`), and the whole checklist
its own (`Campamento completo`). The first time an item enters the inventory (a pickup, a craft) its word card goes up (`WordCardLayer`, above the
hotbar): `New word`, the noun with its article (`la antorcha`), and an empty slot where the audio comes with S9; it stays for five seconds
(`WordCardQueue`, one at a time, by ticks). Pickups do not toast any more (the hotbar counts them; a toast each would queue behind the checklist).

**The crafting panel (S4).** `CraftingLayer` lists the bundle's recipes (`CraftingScreen.Rows` over the 3D simulation): the output's icon and word,
each ingredient as `have/need word`, the station (`Uses mesa`, or `By hand`) and a status: `Make` when it can be made here, else `Missing material`,
`Needs mesa` (the workbench is not within 6 blocks) or `Inventory full`. A click on a row queues one `Craft` command for the next tick (the same
command a script sends), so a craft is deterministic. Opening the panel counts each shown word as read once.

**The screens over the scene.** The hotbar, the toast, the inventory screen, the book and the crafting panel draw over the scene; `FrameView`
carries the `VoxelSimulation`, the first-person camera, the beacon, the figures, the block atlas (`VoxelIcons`, full colour), the `Objective`, the word
cards and the viewport size. New layers: the crosshair with a gather bar, the NPC labels, the beacon's label and
edge arrow, the plot's line (`PlotHudLayer`, Y10) and the 3D debug overlay. In the scene `OverlayMeshes` makes the block highlight, the beacon column and the plot outline (Y10: posts and a rail, `docs/game/architecture.md`).

**The guide (S3).** The turn worker starts with the game (typed and spoken, the loading screen until Vosk is ready), with a `VoxelSession` (in
`Vocaterra.Game`, the shared part is `GuideSession`) over the scene's blocks: `WorldFacts` answers "where is it" with the nearest block of a noun,
its direction from the facing and its distance in blocks. The panel (`DialogueModel`, `DialoguePanel`) is in the layer list; **E** (or **T**; **V**
talks) opens it when the crosshair is on an NPC's figure, the guide's, the shopkeeper's or the villager's since S6 (`NpcAim` on the figure's chest, reach 8 blocks, no block in between through
`LineOfSight.Blocked`, both in `Vocaterra.Game.Core`; the hint `E: talk` is `TalkHintLayer`). The panel takes the keyboard, the mouse is released and no walking happens while
it is open (the keys are in the S7 paragraph below). The arrow of the session is the beacon: `SyncBeacon` puts `Beacon.Set` on the pointer's
block (a review question shows `¿?`), and the beacon's own reach takes the arrow down. "¿qué es esto?" uses `VoxelSession.AimedCell`, the block under
the crosshair (`sim.Target`). `--talk` and `--say` are `ScenarioScript` steps: a `VoxelScript` walk to the stand-off cell of the NPC, a look at it, then the panel opens (the
NPC's host turn, or the guide's review question when one is due), each line is typed and sent, and the script waits for the turn to be applied. The
signpost's words are on its boards (S15), so the `WorldLabel` over it is gone.

**The shoulder view, the arm and the glow (S15).** `ViewModeState` holds the camera mode (first person at the start; **F5** flips it; `--third-person` starts in the shoulder view;
the save moved to **F6**). In the shoulder view `ThirdPersonRig.Eye` puts the camera 3.4 blocks behind the head, 0.4 up and 0.55 to the right, walking the ray in 0.1-block steps and stopping a quarter of a
block short of the first solid cell, and `VoxelRenderer.DrawWorld` draws the player's own figure (`PlayerAvatar`: the feet, the facing, the stride of the distance walked, the pitch for the head). In first
person it draws the arm last, over a cleared depth buffer so it never sinks into a wall: `ArmMeshes` builds the sleeve, the cuff and the hand once (boxes of the cloth and skin cells, shaded by face, no
outline) and the item the hand holds (`HeldItems.For`: a log, a torch with its flame drawn unlit; a bottle, a berry, a loaf, a coin, an apple, the axe, the book and every other item is its 16 x 16 icon extruded into voxels by `ItemModels`, one pixel thick, issue #44); the world matrix places it at the bottom right and applies
`ArmMotion` (a sway while walking, a chop while `GatherProgress` grows, a dip for `ArmMotion.PlaceTicks` after a place command), and `ArmLight` tints it by the cover above the head and the lanterns and torch
near it (the `DiffuseColor` of the alpha-test effect). Lanterns glow because `VoxelRenderer.FindLights` collects the lantern blocks (only the chunks whose kind mask holds one are searched) and
`ChunkMesher` builds, for the cube faces within `GlowRadius` (9 blocks) of one, a quad in the chunk's glow mesh (below), with a falloff by the square of the distance and the most on faces that look at the lantern; the dusk and the
night give the glow full strength (half strength by day). A lantern is a small unlit glass box (`BlockShape.Lantern`; with a torch's flame it is in the chunk's self-lit mesh, which no light tint touches) and the stall's goods are low boxes (`BlockShape.Pile`). Y03 added the bed and the bedroll as piles too: `ChunkMesher.PileHeight` gives the bed a box 0.5 high (a blanket that reads as a bed from eye height) and the bedroll 0.3, the goods 0.42. Their pictures are `BlockArtVillage`'s `Bed` cell (a wooden frame, a cream pillow at the head, a red blanket) and `Bedroll` cell (a roll of cloth with its spiral end); the atlas grew to 13 rows of colour cells (104 cells) because the 96 of the 12 rows were full; the mip chain is taken from the taller atlas. Y04 gave the bed a mesh of its own, `ChunkMesher.AddBed`: the mattress (0.5 high, the picture on its top: a plump pillow with a shadow, the sheet turned down, a blanket with a lit ridge over a dark crease) and a planks headboard (`BedHeadboardHeight`, 0.85) at the head, which lies against a wall beside the cell (north, west, east, then south; a bed in the open has its head to the north). The bed's mesh takes the same sun tint as the other opaque blocks (the merge of the weather series, Y06m).

### Light underground (B12)

The ground darkens with depth under the open sky, and lights are what is left in the dark. There is no light propagation (the biomes plan lists it as later work): the sky's share at a cell is a function of its depth under its column's surface alone, so a mesh stays a function of its blocks and the pure height model.

- **Darkening by depth.** `SkyDepthLight` (constants beside `LanternGlow`'s and `LightBands`'): the share of the sun's light a face keeps is 1 at or above the surface and falls in a straight line to `CaveMinimum` (0.28) at `FalloffBlocks` (6) blocks under it. A cube face takes the share from the cells of the layer in front of it that meet at each corner (the mean of the open ones, the face's own front cell always counting; a face whose front cell is open to the sky keeps the whole sun), so the dark comes on smoothly across a face; the faces of a cave that open sideways to the sky or look out of a shaft are in the open and unchanged. Faces open to the sky, every face above the ground, and the glowing lava itself are not touched.
- **Where the surface is.** `SkyExposure` asks the pure height model (`IPlaceInfo.NaturalSurfaceAt`: the ground as the world made it, with no tree, roof or edit in it, so a forest floor and a house stay as they were) and where the cell at that height is open (a cave entrance, the cut bank beside a river) lets the sky down to the first opaque block below it (at most 48 blocks); where the chunk at the natural surface is not resident the natural surface stands. `SkySurface` holds the surfaces of one chunk column and the ring round it (18 x 18) for the mesher (`MeshOptions.Sky`); `SkyExposure` keeps the tiles by chunk column and drops them when a chunk of the column is evicted. A chunk wholly above the highest surface of its tile is meshed as before. The renderer builds a tile on the loop thread when the first chunk of a column is meshed.
- **Lights.** The glow kinds (`BlockDefinition.Glows`) are the lantern, the torch (the player's placed `antorcha`, `BlockId.Torch`, since B12), the post torch and lava; `ChunkLightIndex` follows a light placed or removed and the chunks within `GlowRadius` (9 blocks) of it are meshed again, so a torch placed in a cave lights it at once. The glow pass is drawn at the look's lantern strength in the open and at full strength where the eye is underground (`VoxelRenderer.GlowStrength`, blended by the sky's share at the eye), and the clear colour and the fog darken by the same share (`EyeSky`), so the sky's bright haze does not show at the far end of a cave.
- **The near zone.** The residency's near zone is wider while the player is underground (6 chunk columns against 4, B07's `BandStreamPolicy`), and every chunk of a column inside it is resident; a cave is meshed once it has its 26 neighbours; a cave inside a distant mountain is outside the band and is never made.

### The day cycle: the sky and the light (E02)

`Simulation.Clock` (E01) gives the hour, the daylight (0 at night, rising across dawn, 1 by day, falling across dusk) and the phase; `VoxelGame` turns them into a `SkyLook` every frame and hands it to `SceneDrawer.DrawWorld`.
`SkyLook.At(SkyLookInput)` is a pure function with no engine type (the input is a record that the weather milestones add to): five keyframes, `SkyKeyframe.Day` (`SkyPalette`'s day set, so noon is the sky the
game always had), `Golden` (a low sun: a paler, warmer horizon, a softer and slightly dimmer dome, a creamy sun, a light of (1, 0.9, 0.76); E11), `Twilight` (`SkyPalette`'s dusk set; a dawn mirrors a dusk), `Night` (a dark readable blue,
the blue hour) and `DeepNight` (darker and less blue, a light of (0.21, 0.26, 0.40); E11), blended by `SkyBlend` (E11: the phase and the daylight share the moment between a day side, the twilight and a night side; a smoothstep of the sun's
elevation splits the day side, all Golden at 10 degrees and below and all Day at 42 and above, so about 6:00 to 8:00 and 18:00 to 20:00 are warm and 10:00 to 16:00 is the noon's own look; a smoothstep of how far the sun is under the
horizon splits the night side, all Night at 18 degrees under and all DeepNight at 56, so 22:00 and 4:00 are the blue hour and the deepest dark is round 1:00; the daylight across a dawn or a dusk is itself a smoothstep, so the look
runs Night, Twilight, Golden with no step), give the dome's three colours, the clouds' three, the sun's and the moon's
direction (`SkyArc`: the sun rises in the east at 5, is highest at 13 and sets in the west at 21 on a path tilted to the north, the moon from 18 to 6) and strength, the stars' amount, the fog's colour (the dome's colour
at the horizon) and the colour of the sun's light on the world (white by day, `ChunkMesher.DuskLight` at the middle of a dusk, `ChunkMesher.NightLight` at night), and how strongly the lanterns glow.
`SkyRenderer.SetLook` colours the dome again in place when its three colours move; `VoxelRenderer.SetAtmosphere(look)` moves the clear colour and the fog every frame and composes with `SetUnderwater` (under water the
water's colour and fog win, dimmed by the light's luminance so the water is dark at night).

**Weather (E05).** `VoxelGame` builds the `SkyLookInput` from `Simulation.Atmosphere` (the snapshot), so the look follows the cloud cover, the sunlight and the fog beside the hour; no file here names a kind's id, and
`--weather <id>` takes whatever ids the bundle has. Each channel moves the look continuously (`SkyWeather`), so the 45-second blend between two kinds shows no step; with the first kind's values (cover 0.42,
sunlight 1, fog 0) every value is exactly what the day cycle gives (`SkyWeatherLookTests`, and a camp screenshot at tick 300 that is pixel-identical to the build before). `Gloom` joins the cover past the fair 0.42 (weight
0.5) and the sunlight lost (weight 0.7) into 0 to 1: the dome's three colours go that far to the grey of their own brightness, darkened to 0.8 of it by day and to 0.55 by night (an overcast night has no blue glow, and a
dusk or a dawn loses its amber), and the clouds' top, sides and underside go to 0.65, 0.58 and 0.48 of theirs. The sun's disc and halo, the moon and the stars are multiplied by `BodyVisibility` (1 at the fair cover, a
smoothstep to 0 at a full cover). The colour of the sun's light on the world is multiplied by the sunlight channel, through E02's tint, so weather never rebuilds a mesh and never changes the band (the band follows the
daylight alone); the tint over a whole day runs 0.507 to 1.48 in clear weather (E02's range) and 0.253 to 0.74 at the data's lowest sunlight (0.5), never above E02's top. The fog keeps the horizon's colour (it greys with the
sky) and comes nearer with the fog channel: `VoxelRenderer.FogEndFor(viewDistance, fog)` is the clear distance times `SkyWeather.FogReach` (1 at no fog, 1/3 at full fog, straight between) and the start keeps
`FogStartFraction` of the end; it is read again when the look changes and when the preset or the view distance does, and composes with `SetUnderwater` as before. `CloudField.Generate(x, z, tick, boxes, cover)`
ranks every cell by its hash: at 0.42 the boxes are today's exactly; a higher cover only adds clouds (none moves or goes), and a cell near the threshold fades in by `CloudBox.Alpha` over 6 percent of cover
(`CellAlpha`, monotonic in the cover), so no cloud pops during a blend; at a full cover the field holds 467 boxes, 14 010 vertices (5 850 at the fair cover). A full cover is still scattered slabs over a grey dome, not a
solid ceiling. The lanterns' glow is its own layer (below) and is not dimmed by the weather.

**Light without a full remesh.** The light is baked per vertex, so it comes in four **bands** (`LightBand`, `LightBands`): Day (the sun's multiplier 1), Dusk and Dawn (both `DuskLight`, a deep amber of (0.88, 0.56, 0.46) since B22, each with its own sun, above) and
Night (`NightLight`); the bands bake the sun's light only, the lanterns' glow is its own layer (E02a). `MeshOptions.Band` says which a mesh bakes (the faces, the plants and the water take the band's sun multiplier; the lanterns' glass and a torch's flame, which are their own light, are
in a separate mesh with none). A dawn uses the Dawn band and a dusk the Dusk band (B22) except at the ends (`LightBands.DayFrom`, `NightUpTo`: daylight above 0.85 is the Day band, below 0.12 the Night band), so the band changes at about 4.43, 5.51, 20.49 and 21.57 o'clock (4.24, 5.7, 20.3 and 21.76 before E11 eased the daylight). A change of band makes the meshes of the other bands **stale**, not due (`ChunkVersionTracker.IsBandStale`; each mesh remembers its band): `Remesh` builds them again nearest and in view first within the
frame's budget, the old mesh staying on screen until its replacement is built. Chunks are drawn grouped by band, each group with `DiffuseColor = the look's sun light / the band's baked sun light`
(`LightBands.Tint`), so every chunk shows the same sun light during the swap and the light moves smoothly through the day with no step at a band's edge. The same `DiffuseColor` carries the light to the figures and the
avatar (the look's sun light itself) and to the held arm (`ArmLight.At` takes the look's sun light and lantern strength). The glow is not in the bands (E02a, below), so it shows no step.
Measured on this VM (software GL, `--quality low`/default): a whole band change takes about 6.5 s
(low) and 14 s (default) of building behind the frame budget, the worst frame about 20 ms (low) and 11 ms (default). A band's mesh is baked with its band's sun direction, so the lit side of a block changes with the band (a remesh, behind the budget); no cast shadows move with the sun.

**The lanterns' glow as its own layer (E02a).** The light a lantern or a post torch throws on the faces near it is not baked with the sun's: the base meshes (opaque, undergrowth, water) hold the face tint, the ambient occlusion and the band's sun
multiplier only, and `ChunkMeshes.GlowMesh` holds, for every cube face a light reaches (leaves too), the very same quad (the same four positions in the same order, the same texture coordinates), its vertex colour the reach of
the glow at that corner as a grey level, 0 at `GlowRadius` to 1 (`LanternGlow.VertexColour`; the falloff and the facing are as before). It is built with the chunk and does not depend on the band, so a band change builds it again
unchanged. `VoxelRenderer.DrawGlow` draws it after the opaque and the undergrowth passes: additive blending (the picture times the glow is added to what is there, the alpha is left alone), depth tested and not written, with the
same alpha test as the opaque pass (a leaf's clear pixels neither draw nor glow), and `DiffuseColor = LanternGlow.Amber x the look's LanternStrength` (0.5 by day, 1 at dusk and by night, a smooth blend through dawn and dusk, whatever the
band and not touched by the weather's sunlight). Because the quads are the opaque mesh's own, drawn with the same effect and transform, every pixel is at the depth the face wrote and the "less or equal" test passes it: no z-fighting
(`ChunkMesherGlowTests` checks the quads are the opaque mesh's own; a close screenshot at night shows a clean wall). The fog's colour is black for this pass, so the glow fades out with distance in the same range as the fog instead of adding haze.
What changed in the look: the old mix also cooled the lit face (less green, much less blue) before adding, and an additive pass cannot multiply, so `Amber` is (0.55, 0.27, 0.02) in place of the old add colour (0.55, 0.30, 0.08) and the
cooling is gone; and the old vertex colour was cut at 1 before the picture, where the glow now adds on top of a face in full sun (a little brighter beside a lantern at noon). Measured at midnight, beside a lantern, the wall's mean colour went from
(92, 42, 24) to (92, 43, 28) and the grass's from (56, 88, 19) to (56, 90, 24); at noon the wall from (169, 86, 44) to (177, 89, 48). Away from any lantern noon is pixel for pixel what it was. The glow of a village is 13 380 vertices (3 345 quads, 0.4 percent of the world's).

### Rain (E06)

The first particle effect: the precipitation the weather's snapshot carries is drawn after the water. `Graphics/Weather/` holds it, and nothing in it names a kind's id but the style table.

**The world's draw order** (`VoxelRenderer.DrawWorld`): the sky, the opaque chunks by light band, the self-lit pieces, the undergrowth, the lanterns' glow, the NPC figures and the avatar, the block frame, the
translucent water (alpha blend, depth read only, from the farthest chunk), **the rain** (the same blend and depth states, both faces), the beacon, the name plates, the held arm (depth cleared). The rain reads the depth the
opaque world wrote and writes none, so a drop behind a wall or a hill is hidden and a drop in front of the water shows over it.

**The field** (`PrecipitationField`, no engine type). The drops live in a box that wraps round the camera: 36 blocks wide both ways and 24 high (`BoxWidth`, `BoxHeight`), reaching 3 blocks further below the eye than above it
(`BoxLowering`), because most of the rain is seen below the horizon, on the ground in front. Each drop has a fixed place in a pattern from `PresentationRng` (seeded by a constant; no clock, no `System.Random`) that repeats
every box width east-west and north-south, so the field never ends and costs the same anywhere, and a camera moved by the box's size sees the same pattern (`PrecipitationFieldTests`). A drop falls at the style's speed
(the tick over 60, with a little spread per drop) and leans with the wind along a fixed heading (east and a little south, the way the clouds drift): its lateral drift is the style's slant times the wind channel times the
height, so its line is straight. The streak runs from the head (the drop) up its own line for the style's length. The drops alive are the style's share of the quality's budget times the intensity, one after another: the
last one alive fades in by the fraction, so a transition thickens and thins the rain with no pop, and the count is monotonic in the intensity and zero at zero. A drop also fades towards the box's walls, roof and floor
(where it wraps) and in front of the eye.

**The cover** (`ColumnCover`). Each column's cover is `VoxelGrid.HighestBlock` (trees, roofs and plants count), read by chunk column (256 columns a tile) and cached with the sum of the chunks' versions: a block placed or
removed in a chunk column makes that tile again at the next frame (`Refresh`, once a frame, for the tiles the box overlaps), and a quiet world makes none. A drop is not drawn below the top face of its column's cover
(`OverheadCover.Top`, in Core, so the sound uses the same rule): the rain stops at the eaves and under a tree, and a drop that reaches a roof or the ground is cut short into it, so no streak sinks into a surface. There
are no splashes, puddles or wet blocks.

**The style table** (`PrecipitationStyles.Table`, rows of `PrecipitationStyle`): per precipitation kind, keyed by the string of the snapshot's `PrecipitationKind`, the streak's length and width, the fall speed, the colour
and opacity, how much the wind slants it and the share of the budget at full intensity. It has one row, for the kind `game/weather.json` names; it is the one place the precipitation's id is written (`NoWeatherKindIdsInSourceTests`
allows that file, and `AmbientBeds.cs`, whose bed of the same word is a bed's id and not a decision on a kind). A test over the bundle's data holds that every precipitation kind of the weather has a row, so a kind added
later (snow) without a style fails there: it is a new row, a new loop and a bed, and a consumer of the channels. An id the table lacks draws nothing and is reported once in the console.

**The pass** (`PrecipitationRenderer`): the streaks as camera-facing quads (the side vector is the streak's direction crossed with the way to the eye) in one `DynamicVertexBuffer` with a fixed index buffer and a fixed
vertex array, so a frame allocates nothing; `BasicEffect` with vertex colour, no lighting, alpha blend (`NonPremultiplied`), depth read and not written. The colour is the style's times the look's sun light (a power of 0.7
with a floor of 0.3, so night rain is dim but shows, and a village's rain is not dark against its dark houses); the head has the style's opacity and the tail a little of it, so a streak fades up its line. A streak is
narrowed near the lens (to 0.3 of its width at the eye, full from six blocks out). It is fogged like the world: the pass takes the world's fog colour, start and end (the under-water blend included). The pass fades with
the under-water amount and is not drawn at 1, so nothing is blurred wrongly by the under-water view; a draw at 0 intensity, an unknown kind or a budget of 0 sends nothing. `VoxelRenderer.SetPrecipitation` takes the kind,
intensity and wind from `SceneDrawer` (which reads the snapshot's channels), and `DrawnRainVertices` is what the last frame sent (a scripted run's closing console line adds it when it rained).

**The quality field.** `QualityPreset.RainDrops` is the most drops at full intensity (a style takes its share): 2200 on low, 3400 on medium, 5400 on high (four vertices a drop: 3 772, 5 788 and 9 124 vertices drawn
in the camp view in the data's rain, which holds 0.8). The field's pattern is made for 6000 (`MaxDrops`). On the heaviest kind the camp view's frame time (scripted, real GPU) went from 2.7, 3.2 and 3.4 ms to 3.1, 3.9 and
4.5 ms on low, medium and high; on a software GL (llvmpipe) the low preset's 34.5 ms went to 35.1 ms (the run varies between two modes, 35 and 48 ms, with or without the rain). The cost is the managed work of
filling and building the quads, not the GL work.

**The HUD of S15.** `RecipePanelLayer` (model: `RecipePanelModel`, from the same crafting rows as the recipe book) shows the workbench's recipe at the right while the bench is in reach and no other panel is open;
`DialogueBubbleLayer` hangs the speaker's newest line over its head (`DialogueBubble.Place` keeps it in the window and the pointer on the head); `DialoguePanel`, the hotbar and the panels use
`DrawHelpers.RoundedPanel` (pixel-art rounded corners).

**The flame of the torch.** The torch placed on the fire ring's rim (`antorcha` at `21,12,21` in the bundle's route) stands above the one-block
ring, so its flame shows from the camp. A torch in the ring's centre would sit below the stones' top and be hidden from ground level, which is why
the objective counts the rim (`VoxelSession.AtFireRing`: the three-by-three square of the ring, on its layer or on top of a stone).

**The speech UX (S7).** The panel opens on its speech buttons (`SpeechButtons`: `[R] Repetir`, `[S] Más despacio`, `[H] Ayuda`, `[N] No sé`,
`[W] Escribir`, and the offered choices `[1]` to `[3]`; `DialogueLayout` is the one layout the drawing and the mouse hit-test share, as `CraftingLayout`
is for the crafting panel; `SpeechActionRunner` carries a press out and raises its voice event; `ILinePlayer` is the hook S9 fills to play the NPC's
line again, `SilentLinePlayer` until then). Hold **V** or **Space** to talk (Tab no longer holds push-to-talk: it opens the text box with W); in the text
box Enter sends and Tab or Esc return to the buttons. **Esc** undoes the newest thing: a recording, a turn that is "Thinking…" (`TurnWatchdog`'s five seconds of the
frame clock offer typing, `¿Escribes?`, with the Escribir button focused), the text box, and last the panel. `DialogueModel` holds the persistent
`Heard: «…» · Expected: «…»` line (`HeardLine`, from the phrase sheet) and the choices; `MicCheckModel` and `MicCheckLayer` are the mic-check screen (a live
level meter from `PushToTalk.LastLevel`, `Say "hola"`, what was heard, pass, retry or skip). `--mic-check` forces the screen, `--no-mic-check` skips it, and a
scripted run skips it unless asked (then the worker hears a synthesized "hola"). `--speak` and `--wav` are `ScenarioScript` steps like `--say`.

**Not in 3D yet:** the Alt-accent shortcuts of the panel (typed `é`, `ñ` and the
others come through the platform's text input).

**Where the pieces are:** `VoxelGame.cs` is the composition root and MonoGame's loop; the rules it wires live in `Vocaterra.Game.Core`
(the screens, the turn flow, the scripts, the tick and the session). This project keeps what needs MonoGame or draws: `Startup/` (the command
line, `SceneLocator`, the asset paths), `Diagnostics/` (the session log, the crash handler, the clocks), `Input/` (the devices, `InputSnapshot` and
`KeyMap`, Keys to `GameKey`), `Sound/` (the outputs and the microphone), `Graphics/` (the renderers and the meshers, by feature) and `UI/` (the
layers and their layouts, by feature). Both trees are in `docs/game/architecture.md`.

## Colour and antialiasing (B22)

**Colour by climate.** Grass, the oak's leaves, the tall grass and the fern take their colour from a map of the column's ground temperature and humidity (`Graphics/Colour/land-colours.json`, embedded, `LandColours`; 5 x 5 nodes each for grass and
leaves, a bilinear lookup, no biome id anywhere) instead of one lime green: hot and dry grass is olive-yellow, wet and cool grass blue-green, with the temperate meadow at (106, 166, 70) against the old (89, 179, 49): about 0.58 saturated, not 0.73.
A vertex colour can only darken a picture, so the block art of the grass top, its rim and the oak leaves is the pale, nearly neutral end of each green (`BlockArtNature`: `GrassBase`, `GrassRim`, `OakLeaves`, the tall grass and fern pictures) and
the data's colour is divided by the art's average per channel at load (the loader and `LandColoursTests` refuse a colour lighter than its art). The mesher reads the climate through `MeshOptions.Ground` (the renderer builds it from the scene's
`IPlaceInfo`: the temperature at the block, lowered with height, and the column's humidity; `GroundClimate.Of`), once per column of a chunk (`ColumnTints`); a grass block's top takes the whole tint, its sides 0.55 of it (the dirt under the rim is only a little
greened), the bottom none; the birch, spruce and bush pictures are desaturated a little and untinted. **The far view takes the same map** (`FarPalette.LandTint`, called by `FarTileMesher` with the face's own position and the settings' `Ground`, from
`StreamedWorldHost`): the climate is looked up per 16-block cell and rounded to steps of 128, so runs of far faces keep one colour and merge, and the near and the far meadow are the same green.

**Antialiasing.** The window opens with multisample antialiasing (4 samples by default, `GraphicsDeviceManager.PreferMultiSampling` and the presentation parameters' `MultiSampleCount`); `--msaa 0|2|4|8`
overrides it. Since B17c it is the **Antialiasing** row of the settings page's Graphics group (Off, 2x, 4x, 8x; default 4x, kept in a save's settings only when not 4, so `msaa` 0 is written): it is chosen when the window opens, so a change of the row takes effect at the next start (the console says so); it no longer follows the quality level. The underwater pass's scene target takes the device's count.

**The Graphics group (B17c, D-game-34 item 43).** *Quality*, *Draw distance*, *Field of view*, *Underwater blur*, *Screen effects*, then under the heading *Shaders and effects*: **Shaders** (B17b), **Grass** (B18's slider, moved here), **Antialiasing**, and one toggle for each effect of B17c and B22, each on by default and saved only when off (`SunFollowsHourOff`, `ClimateColoursOff`, `WaterShaderOff`, `HeightFogOff`, `FarLightOff` in `SaveSettings`): **Sun follows the hour**, **Climate colours**, **Water shader**, **Fog by height**, **Far view light**; a launch option for each (`--sun-follows-hour`, `--climate-colours`, `--water-shader`, `--height-fog`, `--far-light`, each `on` or `off`, a bare flag is on). Changing the quality level sets these rows from the level (`QualityPreset`: Low has the water shader and the fog by height off, Medium and High all on), and each can be changed alone after it; a `--quality` on the command line sets the ones the command line does not fix.

**Water by depth.** The surface of water is tinted per vertex by the mean depth of the four cells that meet at the corner (`WaterDepthTint`, land counting 0): white at the shore and in the shallows, (0.56, 0.70, 0.90) from six blocks down, smoothly between;
the clearer alpha at the shore corners is as it was. **Clouds** have a darker, bluer underside (`SkyPalette.CloudBottom` (150, 168, 204)) and boxes a little smaller (30 to 50 by 18 to 30); their height is still `CloudField.Altitude`, which becomes data in B14.

## The streamed world and the far view (B07)

`Streaming/` runs the streamed world; `Graphics/Far/` draws what lies beyond the chunks. Threads are only here: Core stays pure.

- **`JobPool`**: at most eight worker threads in all (`MaxWorkers`, the bound of the Worker threads setting, B07d; `Resize` changes the count while the game runs; `WorkerPlan` says Auto and keeps scripted runs at two), two lanes (the residency's jobs before the far tiles'), nearest first, stale jobs skipped; with no worker `RunInline` runs jobs on the caller's thread within a budget. Workers call the pure source and never touch `VoxelGrid`, the renderer or the simulation.
- **`ChunkResidency`** (game loop): plans with `BandStreamer` (the bands are made on the workers and cached in `ColumnBandCache`), asks for the missing columns nearest first (`StreamedWorld.GenerateLayers` on a worker, or inline within a per-frame budget), installs what comes back (a duplicate of a chunk a read generated is dropped) and drops the unchanged chunks the plan no longer holds, a bounded number a frame; a cap on resident chunks stops new requests. `Settle` brings everything wholly up to date (a scripted picture, a jump). **`StreamedWorldHost`** holds the pool, the residency and the far view for the drawer (`SceneDrawer.Streaming`); `ApplyPreset` follows the quality.
- **The renderer**: `ChunkViewSet` lists a chunk of a streamed grid only when all 26 neighbours are resident (kept as chunks come and go), measures the distance across, and lists at most `QualityPreset.MaxMeshedChunks`; the renderer peeks and never generates; `VoxelRenderer.FarPlaneFor(view, worldHeight)` covers a peak at the world's top while its foot is in view.
- **The far view** (B07, then B07c: the Voxy model, D-game-34 item 25): the land beyond the chunks is a **three-dimensional level of detail of cubic sections, drawn as blocks always**. `Vocaterra.Game.Core/World/Lod` is the store: `LodSection` (32 x 32 x 32 voxels, each a block and a light byte, sky in the high four bits, the light that reaches that cell; one block all through is four bytes, a mixed section is deflated), `LodSectionKey` (level and place; a voxel of level L is 2^L blocks a side, so a tree, an arch or a house keeps its shape at every level, only coarser), `LodStore` (every level the world has been given, a section never replaced by a worse one, every change on `Changed`, handed to `ILodPersistence`), `LodWorld` (the store and the model: a stack of sections, floor to ceiling, is always whole; a missing one is made from the model, real data replaces it key for key; `IngestStack` takes the generator's chunks in as real level-0 sections without overwriting the chunks an edit made, `IngestChunk` an edited chunk, and `Upgrade` mips every level above that is wholly real, the top and bottom of the world included). `World/Far` makes the data: `LodMip` (the mip: eight voxels to one; solid when two of the eight are (a one-block shell is never hollowed) or one is a trunk (a trunk that carries a crown stays); the block the exposed one; water when the upper four hold it and no solid; light the brightest; `Light` lights a stack from the sky down: air passes it, water and leaves take a level a block, any other block all), `LodModelSections` (where there is no real chunk yet, sections cut from `LodModelSource`'s columns: the generator's own model, with every tree the tree pass places, a forest above `TreeLevels` as a leaf run on a trunk, the regions' own blocks, rivers wider than a cell found from the river field) and the pure `TreePlacement` the tree pass shares. Game: `FileLodPersistence` (the sections on disk under the world's seed, generator version and world-generation hash, written by a thread of its own: a restart or a return is instant), `LodRing` (workers generate the real chunks of every footprint within `RealRadius` of the eye, nearest first, in the ring lane after the tiles, and the store takes them in), `LodEdits` (the player's edited chunks, copied on the game loop and voxelised on a worker); B07f: at the host's creation every chunk the loaded save changed is queued into it, so a save's buildings stand in the far view at the start, and the sections that still hold edits the world loaded does not have (another save of the seed, a new game, a run quit without saving) are dropped through the folder's edit journal `edits.txt` and made again by the ring; `FileLodPersistence` keeps that journal in its write queue, in order with the sections, and shuts down within a bound: the writer drains, syncs and closes its own files, the host waits for it at most 8 s, and exit never hangs; `LodFormat` is 3, so a folder from before starts again). `FarViewSettings` (range, a 32 x 32 tile, eight levels from 1 to 128 blocks a voxel, `BandStarts` in voxel view distances: level 0 from 0.88 of the voxel zone to 1.1 of it, then 1.5, 2.5, 4.5, 8.5, 16.5 and 32.5; `TexturedLevels`, `TreeLevels`, `RealRadius`), `FarTileSelector` (a quadtree over footprints: a tile is split while nearer than its level's start), `FarTileMesher` (a tile is one volume of the footprint's stack of sections; a face of a solid voxel wherever the next is air or water; textured levels draw each face with its block's atlas texture, mipmapped, the others flat averaged colours (`FarPalette`, read from the atlas art, so new blocks appear without a change here) with `FarNoise` and merge runs into one quad; light by the facing and the stored light in front of the face; water translucent and darker with depth; glowing blocks in no tint; a skirt on a tile's edge), `FarView` (tiles cached by key, built on the workers; a tile whose sections changed is made again while the old one is drawn; a built ancestor stands in for a leaf not built), `FarTerrainRenderer` (opaque, lit, then water far to near, with the atlas or the noise per tile; a frame hands the card at most 8 MB of meshes, new tiles before remade ones and the coarsest level first, `FarUploads`, so a settled view has its horizon in a few frames and a scripted picture waits until `Unuploaded` is none; a worker keeps its volume and geometry arrays between tiles, a tile whose build throws is released and counted as `Failed`). The ring and the tiles share the workers: the ring lane gets one job in five. The haze is one fog for both passes (`VoxelRenderer.FarFogEnd`). The game holds the simulation (`StreamedWorldHost.IsReadyAround`) while the columns round the player are being made.
- **Scripted runs**: each frame settles first; the counts of a streamed world are left out of the compared `voxel:` line and printed on the `stream:` line under `--stream-stats`.

## Architecture

The game's code is in two projects (R16, `docs/process/code-guidelines.md`): `Vocaterra.Game.Core` holds the systems and the mechanics (no package,
no engine type, no clock: its `ArchitectureRulesTests` fail on one), and this project holds the window, the drawing, the devices and the
layers. `docs/game/architecture.md` lists both trees, folder by folder.

The frame is: `Update` reads the clock, reads the keyboard and the mouse once (the panel's keys, the crafting clicks), the accumulator returns 0 to 5
ticks, and for each tick the commands are built (the script's or the device's, the queued crafts, the look first) and given to `VoxelSession.Step`
(the simulation's step, then the objective and the word cards from its events). A line the player sent (Enter, a released push-to-talk key, or the
script) is handed to the turn worker at the end of `Update`, after the tick loop and never inside a step; the frame goes on, and the start of a later
`Update` applies the finished turns in the order they were submitted (evaluation runs on one dedicated thread that owns Vosk). While the models load
the loading screen shows and the world does not step. `Draw` interpolates the eye between the last two ticks (`Alpha`), moves the camera, clears to
sky, draws the chunks and the figures, then every layer. The clock never reaches the simulation: it sees only the commands.
MonoGame's own fixed-step timer is off (`IsFixedTimeStep = false`) and MonoGame's initial extra `Update` is skipped, so a frame is one `Update` plus
one `Draw`.

## Demos (S4)

The camp, from the spawn (seed 42, `DISPLAY=:0`): gather two berries at the spawn, one more berry, five trunks, craft the torch at the workbench,
place it on the fire ring's rim and look at it. The console prints each step and the checklist reads `3/4`; the screenshot shows the HUD and the flame:

```
dotnet run --project src/Vocaterra.Game --no-build -- --seed 42 --interact 17,11,15 --interact 15,11,19 \
  --walk-to 17,20 --interact 16,11,23 --walk-to 11,17 --interact 9,11,17 --interact 9,12,17 --interact 9,13,17 --interact 9,14,17 \
  --walk-to 11,11 --interact 11,11,9 --walk-to 20,17 --craft antorcha \
  --walk-to 20,19 --place antorcha@21,12,21 --walk-to 22,19 --look 180,-15 --screenshot s4-camp.png
```

## Tests

`tests/Vocaterra.Game.Tests` (no GPU, no window, no microphone) tests this project's classes, its folders mirroring the sources': the cameras,
the meshers and the block atlas (`Graphics/`), the layers' layouts and the font (`UI/`), the session log, the crash handler and the frame timings
(`Diagnostics/`), the launch options (`Startup/`), the device input and `KeyMap` (`Input/`), and in `Component/` the tests that drive Core's
classes with this project's (the `*FrontendTests`, the check battery with the real session log). The architecture rules (`Architecture/`): no
`System.Random`, no `Guid`, no wall clock outside `WallClock.cs` and no `Stopwatch` outside `FrameClock.cs`. The tests of Core's classes are
in `tests/Vocaterra.Game.Core.Tests`, in the same folders as their classes (R16).

## Notes

- The sandbox or CI may have no display: `--screenshot` then needs `xvfb-run -a dotnet run ...`. The Linux runtime needs `libSDL2`
  and `libopenal` (the DesktopGL package ships its own copies; on a Linux x64 build they are copied next to the executable, see
  `docs/game/architecture.md` for the native-asset trim).
- The back buffer is read after the frame is drawn and before the swap; on DesktopGL it comes back upright (checked), not flipped.
- **S17.** `GuideApproach` opens the panel on the guide when the player walks up to her and her lesson is waiting (or her transfer offer is, once the camp is
  done); `SessionEnding` and `EndingLayer` are the closing card; `SpeechButtons.TextFor` and `HintsFor` add the English to the buttons and the hold-to-talk hint (a
  second row of buttons when they do not fit the panel); `--lesson-run <id>` runs a whole lesson in a scripted run (walks to the NPC, types each answer, walks to the
  fruit and back for a `do` step) and `--lesson` takes the guide's `first-words`; the recap ends on the book's phrases page (`LessonFrontend.OnRecapDone`; a
  scripted run does not open it and asks for `--journal --journal-phrases` instead). The post-check of a playtest starts when the session is complete
  (`PlaytestEnd.StartsOnItsOwn`: the camp done and the room at the inn taken) and the transfer offered.
