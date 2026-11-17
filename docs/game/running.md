# Running the game

The 3D world, its command-line options and the demo runs that show each part of it, then the playtest. Run every command from the
repository root. The scripted regression runs are in [scripted-runs.md](scripted-runs.md), the facilitator's script in [playtest.md](playtest.md).

## The 3D world (S2 to S4)

The prototype's world is the first-person voxel scene: it loads the world from the bundle (`content/game-v1/game/scene.json`, S13: generator
parameters and named sites, not blocks, a 384 x 384 x 128 block valley with high snow-capped mountains along the north and the west, a waterfall off
the northern cliffs, a river through a dense forest to a lake, the guide's camp in a clearing, the village on the meadow across the river with the stall,
three timber-framed houses with stepped roofs and chimneys (house3 stands at the south-east corner since Y04), a stone tower, a fenced crop field, lanterns on posts and the signpost with its three boards
`Tienda`, `Río` and `Montaña` (S15); since village-plan V04 also a bakery with its oven, a smithy with a lit forge and an anvil, a well, the farmer's
apple crate and an orchard of three trees, and the fisher's crates and lamp on the bridge's east landing; since Y04 the inn (`posada`, nine by seven at the west edge, its door to the road, a counter, a keeper's cell, a partition with a gap and, in the guest room, two beds each two cells long (a head block with a pillow and a tall headboard against the wall, a foot block with a lower footboard; Y08); the points `bed.inn` and `npc.innkeeper`), and two meadows cleared of trees north and south of the village (the sites `meadow.north` and `meadow.south`, where the land the player buys will lie); and the bare mountain ledge at the top of a switchback trail, where the player arrives) at `--seed` (default 42; the
seed changes the land between the sites and the forest, never a site), and shows it from the player's eyes. A new game starts on the mountain ledge
(facing south-east, bare since Y08: no tent, chest, crate or fire ring; the first night is at the inn, and the player's own camp comes later on bought land) with the beacon on the guide's camp: the first thing to do is the walk down the trail.
`--gl-shaders on|off` (B17b) chooses how the chunks are drawn: through the game's own GLSL program on MonoGame's OpenGL context (the default; the settings page's Shaders row, in the Graphics group, saved with the settings, changes it while the game runs and the world builds again for a moment) or through MonoGame's effects (`off`; also what the game does by itself when the context is below OpenGL 3.3 or a shader does not compile, with one `gl:` line in the log saying why); `scripts/gl-compare.sh OUT` draws seven views both ways and compares the pictures, and `--stream-stats` prints the processor's and the card's time for the chunk passes. `--msaa 0|2|4|8` sets the window's multisample antialiasing (B22; the default is the quality's: 4 samples at every level; it is chosen when the window opens, so the menu's quality row takes effect for it at the next start). `--quality low|medium|high` sets how far the world is drawn and how much is drawn of the undergrowth, the occlusion and the clouds (see `src/Vocaterra.Game/README.md`); the default, high, is for a real graphics card, and
**on a machine with software GL (this VM) pass `--quality low`**. S14 gave it its look: code-drawn pixel-art textures (a 16 x 16 atlas, deterministic, CC0 by construction), oaks, spruces and birches,
tall grass, ferns, reeds and flowers that nothing collides with and the block ray looks through, sun shading and ambient occlusion in the vertex colours, a blue sky with a square sun and flat clouds,
haze that fades the far mountains into the horizon, translucent water with a lighter shore, and a waterfall. The player wakes on the ledge looking down (pitch -10) over a valley whose forest, river, village and lake are in the
picture; the trees in front of the ledge are cleared (a sight cone), the banks and the village are meadow and the ledge's foreground holds only low flowers. (`--voxel`, which selected the 3D world until S4, is accepted and ignored.)

```bash
dotnet run --project src/Vocaterra.Game -- --seed 42
```

**S13 demos** (the large world; `--quality low`, 1280 x 720; each prints the frame times and the mesh counts; `--teleport` and `--look` only set the
camera for a picture):

```bash
# from the mountain ledge (the spawn) looking south-east over the valley
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --look 128,-13 --screenshot /tmp/s13-camp.png --frames 3
# from the valley (the guide's clearing) looking north at the peaks, the cliffs and the waterfall
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 176,200 --look 25,8 --screenshot /tmp/s13-peaks.png --frames 3
# the village, from the road: the stall, the signpost, a house and the villager
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 272,224 --look 75,-4 --screenshot /tmp/s13-village.png --frames 3
# the village from its entrance, with the boards Tienda, Río and Montaña on the signpost, the stall's awning, the houses and the tower (S15)
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 266,223 --look 80,2 --screenshot /tmp/s15-village.png --frames 60
```

**S14 demos** (the look; `--quality low`, 1280 x 720; the default spawn look is yaw 135, pitch -10):

```bash
# the camp vista at the default spawn look (add --quality high for the whole valley and the far walls), then the sun (at the start hour, 8, it stands east-north-east, 27 degrees up) and the clouds
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --screenshot /tmp/s14-camp-low.png --frames 60
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --look 69,27 --screenshot /tmp/s14-sun.png --frames 60
# the valley looking north at the peaks and the waterfall, the forest, the riverbank (reeds, sand, clear water) and the falls close up
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 176,200 --look 25,8 --screenshot /tmp/s14-valley-north.png --frames 60
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 196,206 --look 100,-3 --screenshot /tmp/s14-forest.png --frames 60
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 222,222 --look 80,-16 --screenshot /tmp/s14-riverbank.png --frames 60
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 258,136 --look 0,14 --screenshot /tmp/s14-waterfall.png --frames 60
# the riverbank in greyscale: the trees, the grass and the water are grey (their nouns are not known), the reeds and the flowers stay in colour
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --greyscale --teleport 222,222 --look 80,-16 --screenshot /tmp/s14-greyscale.png --frames 60
```

**Lodging aid (Y05).** `--lodging` (only with `--dev`) gives the player lodging at the start, as the innkeeper will, so the inn's bed can be slept at; without it the bed shows "This bed is the inn's: ask for a room first" and E does nothing (the scripts' `--sleep` refuses there too). Example: `--seed 42 --mute --needs off --dev --lodging --skip-to-session 2 --walk-to 278,231 --wait 1 --sleep`.

**Stake aid (Y10).** `--mark-plot x,z` (only with `--dev`) makes the stake's use on that block column at the start, with no stake in the bag needed: the pending plot is marked round the column (the column is the plot's middle, so `266,165` marks the plot whose west-north corner is `261,160`), the console prints `land: the stake at x,z marked the pending plot cx,cz (10 by 10) (--mark-plot)` or `land: the stake at x,z was refused: <fault> (--mark-plot)` (`Protected, Village`, `OutsideMeadows`, `Tree`, ...), the hint line says the same in English, and a save carries `systems.land.pending`. In play the stake is the item `estaca` (`--give estaca` in a run; the mayor gives it in Y11), used with the right click on the ground; the plot's outline (posts and a rail) and the line `Plot 10 × 10 · price 10 coins` show near it. Example: `--seed 42 --mute --needs off --dev --skip-to-session 2 --give estaca --mark-plot 266,165 --teleport 266,175 --look 0,-4 --screenshot /tmp/y10-plot.png --frames 40`; the village (`--mark-plot 285,206`) is refused.

**Camp aid (Y12).** `--place-camp x,z,face` is a script step (needs `--screenshot` like the other steps; `face` is `north`, `east`, `south` or `west`): it is the camp kit's use on the ground of that column, the camp's front facing the side given and the fire ring's middle on the column. It needs the kit in the bag (`--give equipo`; the mayor sells it with the plot) and, as in play, land you own (`--dev --own-plot 261,160`) with every cell free and level; the console prints `camp: Placed at x,z facing North, fire x,y,z, bed x,y,z, wake x,y,z` or `camp: Refused at x,z facing ... (the reason)`, and a refusal changes nothing. The camp is `game/scene.json`'s `camp` stamp (5 by 8 blocks, turned to the side). Then `--place antorcha@x,y,z` on the ring's rim lights it (the `camp` and `fire` steps of «Mi terreno»), `--sleep` sleeps at the bedroll or the lit ring (the console says `ends at your camp`), and `--interact` held on any block of the camp packs it back into the kit (45 ticks). Example: `--seed 42 --mute --needs off --dev --skip-to-session 2 --own-plot 261,160 --give equipo --place-camp 266,163,north --screenshot /tmp/y12-camp.png --frames 40`. In play the kit is used with the right click on the ground, the camp facing the player.

**Land aid (Y09).** `--own-plot x,z` (only with `--dev`) gives the player a plot of land at the start, as the mayor will: `x,z` is the plot's west-north corner, the plot is the land file's size (10 by 10), and the land rules check it first (`LandRules.CheckPlot`: outside the village, the roads and Luna's camp, inside a meadow, grass or dirt, no water, no tree, level within a block). A refusal prints the reason (`Protected, Village`, `OutsideMeadows`, `Tree`, `NotGround`, `TooSteep`, `Water`) on stderr and exits 2; otherwise the game prints `land: the plot x,z (10 by 10) is owned (--own-plot)`, and a save carries `systems.land.owned`. No coins change hands and no stake is needed. A valid corner on seed 42 is `261,160` in the north meadow; plank blocks may then be placed and taken back inside it, and are refused (placing) in the village, on the roads and at Luna's camp, and kept (taking back) anywhere outside an owned plot, each with a hint line. Example: `--seed 42 --mute --needs off --dev --own-plot 261,160 --give madera --teleport 266,167 --place madera@266,37,165 --interact 266,37,165`. The scripted steps `--place`, `--interact` and `--pour` go through the same rules and the log names the refusal (`refused by the land rules`).

**Clean defaults (U01).** Ordinary play starts with no debug text: `--dev` starts the debug overlay and the dialogue's developer note on, F3 toggles both. The "Heard / Expected" line under a spoken turn is not the developer note and always stays. The first-time tip box stands under the overlay when F3 shows it. The speak hint is one: the tip `tip.speak` at the first conversation; once it was shown the dialogue footer keeps only its other keys (the speak key stays in the controls help on the menu's Controls page). The objective tracker (top right) is one line per objective, three at most, the one that advanced last first, the rest as "+n"; a lesson's `do` step shows in the lesson strip while the strip is up, not also in the tracker. The scripted runs pass `--dev` (all but the playtest) so their captures keep the overlay and the note.

**The needs (N01).** Hunger, thirst and sleep fall in active play and push health and stamina (design section 9). `--needs off` freezes them full (the scripted runs pass it, so
the language runs keep their baselines); `--set-need <need> <value>` sets one at launch, 0 to 100, repeatable (`hunger`, `thirst`, `sleep`, `health`, `stamina`):

```bash
# thirst starts at 30 and falls: a need_stage event (sed) when it crosses 25, and the save's systems.needs shows it
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --set-need thirst 30 --walk-to 176,186 --screenshot /tmp/n01.png --frames 60 --save /tmp/n01.save
```

**Sleeping (L01, Y08, Y12).** E at a sleep place sleeps from day 2 only while the sleep drive is below 75 (day 1: once its ending is reached); otherwise it shows «No tengo sueño.» and sleeps nothing. The places are Luna's lit fire, the inn's bed (once Rosa has given the room) and the camp's bedroll or lit ring; the console's night line says where it ended (`at the fire`, `at the inn`, `at your camp`, `wherever the player stood`, or `where the player fell` for a collapse). `--sleep` and a script's `sleep` step sleep at any drive, so a `--needs off` run (the sleep drive full) can still pass the night. No lesson, class or order holds sleep back.

**Sleep and the collapse (N03).** Every night (E at a sleep place, Enter on the closing card while the player stands at one, `--sleep`) applies the night to the needs: sleep to 100, health +20 (to 100),
hunger -15 and thirst -20, neither below 10 (a drive already below 10 stays). After 60 s of active play with sleep at its last stage the screen fades and the player sleeps where
they stand (sleep to 50, stamina full, the same costs, no health), wakes there, and the day and sitting move as for a night; a tip explains it once. To reach it in a script:
`--set-need sleep 0 --wait 62` (needs on; the collapse is the sleep drive's, so hunger 0 does not cause it). The line `sleep: collapse: session 1 ends where the player fell, ...`
is printed. A night with `--needs off` leaves the meters as they are.

**Moving in water and in lava (B10b).** In water the body wades (water below the waist: 0.8 of the walk) or swims (0.6 of the walk, sinking at most 1.5 blocks per second, up to 2.5 with jump held, a hop out onto a bank one block above the surface; `SwimMotion`). Lava is a thicker liquid (`LavaMotion`): the body is not stopped by it but enters it and sinks at most 0.5 blocks per second, wades at 0.35 of the walk or swims at 0.25, swims up at most 1.2 blocks per second with jump held and climbs out at an edge like from water; it never stands on a lava surface. It does no harm yet. Under lava the view takes a deep orange tint (`LavaVolume`, drawn after the water's tint). The walk planner and the sleep walk still treat lava as blocked. The ores' tiles are five clusters each of a strong colour with a dark rim and a light pixel (iron peach-tan, tin blue-tinted silver, copper orange with teal patina).

**Eating, drinking, sprinting (N02).** With a food selected in the hotbar, the right button eats it (a berry, an apple, bread; the water bottle is drunk and leaves the
empty bottle); at full hunger the food is kept and a line says so. **E** with the crosshair on water within reach (the river, the lake, the well) is a drink by hand, with
nothing held and without Spanish. **Hold Left Shift** while walking to sprint (about 1.6 times the walking speed) while stamina lasts and no stage forbids it; a gathered block
and a sprint cost stamina, and standing still within 3 blocks of a lit fire (the guide's camp fire once its torch is on the rim, the forge always)
doubles stamina's refill and health's recovery. The events `meal_eaten` (the food's noun) and `water_drunk` (`botella`, or `pozo`, `lago` or the river's noun) print like
`need_stage`. `--needs off` keeps the actions (the food is used up, the events are raised) and leaves the meters still. Two scripted steps drive them, both test aids:
`--eat <item>` gives the item if the bag has none and eats or drinks it as the right click does (`pan`, `fruta`, `manzana`, `agua`), `--give <item>` puts one in the bag (N07, a test aid: `--give manzana` then `--craft roast-apple` at a lit fire; the item key `roast-apple` names the roast apple, `manzana` the apple; `--eat` prints the health too for a food that heals), `--plant x,y,z` plants the held berry on the grass or dirt block at that cell as a right click on it does (N08; the line says what happened, and it is refused outside the camp's radius, on something, or at six planted bushes), `--drink` drinks by hand at the water the
crosshair is on (walk to the bank, `--look` at the water and `--wait 1` first: the look takes a tick). Each prints a line, `eat: Done (Bread), hunger 65, thirst 100` or `drink: Done, thirst 40`.

**The station (W07).** E with the crosshair on a cell of a station's prop (the oven; `stations.json` names the prop, the scene records the cells of each named stamp) opens the station overlay, after an NPC under the crosshair and before the workbench: the instruction strip across the top (the row in Spanish, its English at the first rung or after help, a dot per step, Repetir, Más despacio and Ayuda), the fixtures on the left, the targets in the middle, what can be held along the bottom. Keys: 1 to 9 hold the cell of the page, 0 the empty hand, Page Up and Page Down turn the page, Tab and Shift+Tab choose the target, Enter uses the selection (a pair with several verbs asks which), H help, R repeat, S slower, B bakes a mastered recipe at once (the offer beside the guided bake, also a button; W07b), V or Space (held) and W talk to the teacher (W08): the line goes to her at her teaching prompt, wherever she stands; she plays the instruction again or slowly, helps, gives the ingredients the run lacks (the first bake always, then once per in-game day) or says "De nada"; her answer shows on the line above the keys and plays in her voice, and the station stays open; Esc leaves with the bake saved. The offer, "¿Quieres hacer pan?", opens a talk with her until the learner has baked (once per in-game day); Sí opens the station. The panel's title is the station's name (its `ui.station.<id>` label, else its noun with the article). The world keeps running, the player does not walk or look, the need meters stand still. `--station <id>`, `--act <use>,<on>[,<verb>]`, `--proc-help` and `--station-say "<line>"` drive it from a script (`docs/game/scripted-runs.md`).

**The needs on screen (N05).** Health is a thin bar beside the hotbar; stamina has its own bar only while it is below its maximum, with a dashed end for what the drives cut from it. A drive at stage 1 or more shows
its icon (a loaf, a drop, a moon) with one to four notches and loses it on recovery. A stage change shows its line once above the hotbar, the Spanish with the English beneath («Tengo mucha sed.» / I'm very thirsty.),
and plays a soft tone that ducks under the voices. The first stage change of the first session shows the tip. The inventory (I) lists the five needs with their stage. View effects: dim edges from thirst stage 3, a slow blink
from sleep stage 3, red edges below 25 health; **F7** turns them off (the icons and lines stay), kept in the settings (`viewEffectsOff`); the launch flag `--view-effects on|off` sets it for a run. The HUD is hidden under `--needs off`.
**Relaxed needs** (settings `relaxedNeeds`, off by default; launch flag `--relaxed-needs`): every drive's rate halves; at zero health the player faints and wakes at the camp with the bag kept and no chest.
**The pass cheat** (B08b; settings `passCheat`, off by default, under *Cheats* at the foot of the settings' game rows, "Open the pass (cheat)"; launch flag `--pass open`): the pass through the east ring is locked until every lesson in `pass.requiredLessons` of `game/world.json` (all of the game's lessons today) is complete; the cheat opens it at once. Opening removes the rockfall at the road (the blocks of the scene prop `rockfall`, set to air like any edit, so the save keeps them as changed chunks) and logs the world event `pass_opened` (detail `lessons` or `cheat`); an open pass never closes again, turning the cheat off or loading the save leaves it open (the save's optional `passOpen`). With `--pass open` a new game starts with the pass open. In the streamed world the valley's outer edge is an invisible wall for the body while the pass is closed and lifts all the way round when it opens (B07; after a load of a save with the pass open it stays lifted).
**The streamed world and the far view** (B07; D-game-34 items 15 and 21). The game now starts in the streamed world: the valley as before, with the procedural land of the B series round it, read in as the player walks (`--world streamed`, the default). `--world valley` keeps the bounded valley alone, exactly as the game was (its scripted runs compare byte for byte; a `sample` value joins the same flag when B03b lands). Only the surface band of each column is resident at a distance (the chunks that meet the ground, padded by a chunk so each has all 26 neighbours; everything under the player within 4 chunks, 6 when underground); a chunk is meshed only when all 26 of its neighbours are resident; the unchanged chunks beyond the keep distance and a chunk of slack are dropped, a changed chunk never. Beyond the voxel chunks the **far view** draws the land the way the Voxy mod does (B07c): a three-dimensional level of detail (cubic sections of 32 x 32 x 32 voxels, a voxel of level L being 2^L blocks, so trees, arches and houses keep their shape at every level), built from the world's own chunks, drawn as blocks only. Workers generate the real chunks of the surface within a radius of the eye (the *ring*: 560 / 760 / 1,000 blocks at low / medium / high) and the store takes them in as level 0, mipping every level above that is wholly real; beyond the ring, and until real data arrives, sections are cut from the generator's own model (the height model, water, snow, the strata, the tree pass's trees, the regions' blocks) in the same format. The player's edited chunks go in too, so an edit shows far off. The sections are kept on disk under `VOCATERRA_LOD_DIR` (default `~/.local/share/vocaterra/lod`), in a folder named by the world's seed, generator version and world-generation hash, so a restart or a return is instant and a changed world starts again. The player's edits in those sections belong to the save that is loaded, not to the folder (B07f): the folder's `edits.txt` lists the edited chunks, and a start that loads another save of the seed, a new game or a save made before a quit without saving drops the sections that hold edits it does not have (the ring makes them again from the generator), while a save's own changed chunks are taken in at the start so its buildings are in the far view at once. The folder's place did not change. Tiles are chosen by a quadtree over footprints; the nearest levels (voxels of 1 and 2 blocks) draw each face with its block's own atlas texture, mipmapped, and the others flat averaged colours with a small noise texture, merged into large quads; every face is lit as the chunks are (the facing, the stored sky light under crowns and water, a little darkening at the foot of a step), water is translucent and darkens with depth, lava and lanterns glow at night, and haze fades it into the sky, in a pass of its own with its own depth range. **The presets' far settings:** the far range is 4,096 / 8,192 / 16,384 blocks; level 0 (blocks) is the ring from 0.88 to 1.1 of the voxel distance (224 / 304 / 400), then the voxel doubles (level 1 from 1.1, 2 from 1.5, 3 from 2.5, 4 from 4.5, 5 from 8.5, 6 from 16.5 and 7 from 32.5 voxel distances, so at high 2-block voxels to 600 blocks, 4 to 1,000, 8 to 1,800, 16 to 3,400, 32 to 6,800, 64 to 13,000 and 128 beyond); sections made from the model make every tree as its blocks below 2 / 3 / 4 levels (about 340, 760 and 1,800 blocks), and a forest above that is a raised leaf layer on a trunk. Player edits do not show at a distance until their chunk is ingested. Flags: `--prefetch <0|1|2>` (the worker threads that make columns, far tiles and, since B21, chunk meshes and grass layers beside the game loop (the bounded valley has a pool of its own for the meshes), 2 by default and never more; 0 keeps everything on the game loop's thread; the world, its state digests and its saves are the same either way), `--far-view <off|blocks>` (256 to 65536, default the preset's), `--stream-stats` (a scripted run prints the residency, mesh and far-view counts and timings, the frame percentiles and the time the streaming and the meshing took on a `stream:` line of their own, since B21 also the bytes the game thread allocated in those two phases a frame and the whole process's allocation rate, the meshing pipeline's counters (jobs, results refused as stale, edits meshed in their own frame) and how many chunks a read on the game thread generated; they are left out of the compared output otherwise), `--no-settle` (a scripted run streams the world as a live one does, a few chunks and tiles a frame within the budgets, and takes its pictures with whatever is in place: a test aid for the live path) and the existing `--view-distance`. At a start or a move (a `--teleport`, the menu's teleport, the respawn at the camp from far away, a load) the frame that finds the body in a new place makes its near zone then and there (B07b: the chunks under and round the player, two columns each way, planned from the new place and installed, waiting for the workers, within 1.5 seconds; on the live path about 0.8 to 1 second for a place a kilometre away, one long frame like a start's) and meshes it, and for two seconds after it the frames spend a larger install and mesh budget (6 and 10 ms) so the ground round the body is complete in one to two seconds more; the simulation waits until the near zone is resident, so the body never reads a world that is still being made (it gives up after 10 seconds), and what the old place had queued goes behind everything the new place needs. A scripted step after a `--teleport` (a `--shot`, a `--look`, a `--walk-to`, a `--wait`) runs only once the world round the new place is resident, so a shot is never of the view from before the jump; a bounded world (`--world valley`) has no hold and the next step runs at once. **Where a teleport puts the body (B07b):** `--teleport x,z` and the menu's teleport (one rule, `GroundFinder.StandingAt`) put the feet on the highest block of the column that is not a tree and has two free cells over it; over water (a pool, a lake, the sea) they go to the cell over the water's surface, so the body floats there and no longer stands on the bed under it (`--teleport 258,118` printed `standing at 258,38,118`, the pool's bed, and now `258,43,118`). The surface, not the nearest dry ground: the middle of a lake has none within a few blocks. `--stream-stats` prints one `move:` line for the start and each move (the time to the near zone resident and to the near view, the chunks within 64 blocks, meshed, and the longest frame) and, once the residency is settled and every chunk in view has its mesh, a second one (`whole view resident and meshed after N ms`, B21). The far land takes the chunks' own light (the same sun tint by the slope, the same day band) and their greyscale, and its haze starts at 6 percent of the range (about a kilometre at high), reaching the sky's colour at the range. In a scripted run each frame first settles (everything in view installed, meshed and the far tiles built), so a `--shot` shows the finished picture. Measurements and pictures: `/home/james/Dev/Vocaterra/artifacts/b-series/b07/` (outside git).

**The underground in play** (B12; biomes plan section 3.5). The ground darkens with depth under the open sky: a cube face keeps the whole sun at the surface and 0.28 of it six blocks down (`SkyDepthLight`, from the pure height model and the cells open to the sky; no light propagation), so the cave minimum is a dark you can still read; faces open to the sky, everything above the ground (the valley by day, its forest floors and houses) and the lava itself are not touched. The placed torch (`antorcha`) is a light kind with the lantern, the post torch and lava: a torch placed or removed in a cave re-meshes the chunks within nine blocks at once, and underground the glow is drawn at full strength whatever the hour; the fog and the clear colour darken with the eye's depth so a cave's far end is not a bright haze. The near zone is as B07 set it (6 chunk columns underground, 4 at the surface, `StreamedWorldHost.PolicyFor`; every layer under the player inside it, a cave meshed with its 26 neighbours, a cave in a distant mountain never made). The cave tour is `scripts/scripted-runs.sh OUT r50-cave-tour` (`docs/game/scripted-runs.md`). New script steps: `--teleport-to x,y,z` and signed `--teleport`, `--place`, `--pour`. Measurements and pictures: `/home/james/Dev/Vocaterra/artifacts/b-series/b12/` (outside git; Release, an RTX 3070, `--stream-stats`, seed 42, 16 km far view at high): the underground at each preset is as cheap as the surface (frame p50 2.0, 2.4 and 3.1 ms at low, medium and high, p95 10 ms, p99 12 to 15 ms, the start-up frames included; resident 6.4k, 10.0k and 15.3k chunks, against 5.5k, 9.2k and 14.4k at the surface; mesh time a chunk 1.2 ms on average). The sky tile's own cost was not measured apart.
**Integration (B14).** What the B series added is heard and checked together here. **The beds by place**: in the procedural land the ambient beds follow the biome (`ambient` in each row of `game/biomes.json`: wind, birds by day, insects by night, river, surf, from the columns round the feet, so a border is a slope and the mixer cross-fades), under the ground the surface beds fade out between 3 and 14 blocks down and a quiet cave bed (a draught and a slow drip) takes over, and inside the valley the beds are as they were; `--sound-log` writes `bed at <biome or underground>: ...` when they change (an entrance's open pit, about ten deep, is a mix of the cave and the open air). **The clouds' height** is `cloudAltitude` in `game/weather.json` (80 to 800, 168 now). **Script steps** take negative coordinates (`--walk-to`, `--interact`, `--place`, `--pour`, `--teleport`, `--teleport-to`). **The biome tour** is `scripts/scripted-runs.sh OUT r51-biome-tour` (`docs/game/scripted-runs.md`): the valley, the pass, each biome, the cave and the far land, 22 pictures; `GAME_BIN=src/Vocaterra.Game/bin/Release/net10.0/Vocaterra.Game` and a Release build give the pictures their frame rate. **The checks**: `ProceduralWaterSeedsTests` keep the water at rest and every water edge exitable in 21 windows of generated land (the pass, two rivers, two lakes and two coasts of seeds 42, 7 and 2026); `BiomeTourTests` keep the tour in step with the biomes. Pictures: `/home/james/Dev/Vocaterra/artifacts/b-series/b14/` (outside git; `index.md`).

**The menu (issue #34).** **Esc** in the world opens the menu and pauses the game as the other panels do (while the session's closing card is up the first Esc takes the card down; in a panel Esc closes the panel; the playtest's thank-you screen still closes the game on Esc). The main page has **Resume**, **Settings**, **Controls** and **Exit** (Exit saves, as F6 does, then closes the window). Up and Down choose, Enter or Space selects, the mouse clicks, drags a slider and scrolls with the wheel, Esc goes back a page and then closes. **Settings** (Left and Right or - and + change a row, Enter or Space flips or cycles it): *Sound*: ambient, weather, voice, effects and interface sounds volumes, 0 to 100 in steps of 5; *Graphics*: quality low, medium or high (default high; changes the view distance, the undergrowth, the shading and the clouds at once, the world's meshes are built again), draw distance (Auto, then 48 to 576 blocks in steps of 16; Auto is the quality's own distance, 224, 304 or 400; a chosen distance holds when the quality changes, and changing only the distance keeps the meshes already built), field of view 60 to 110 degrees (default 70), **grass** (B18: the decorative grass's density, 0 to 100 percent in steps of 10, default 60, applied at once; 0 is the look of before; `--grass <0-100>` sets it for a run, a value off the steps is refused; kept in the save's settings as `grass`, written when not the default, so 0 is written), underwater blur, screen effects; *Interface*: font size 100, 125 or 150 percent (default 150, the largest; **F9** steps the same setting); *Mouse*: sensitivity 25 to 300 percent, invert Y; *Needs*: the drive rate (0.5 to 4, N09) and the relaxed needs. All of them are kept in the save's settings (`SaveSettings`: the volumes, `textScale`, `quality`, `viewDistance` (0 or absent is Auto), `underwaterBlur`, `viewEffectsOff`, `mouseSensitivity`, `invertY`, `fieldOfView`, `needsRate`, `relaxedNeeds`; a default is not written), so they come back with a `--load`; a launch flag (`--voice-volume <0..1>`, `--sfx-volume`, `--ui-volume <0..1>`, `--ambient-volume`, `--weather-volume <0..1>` (E09, the rain), `--text-scale`, `--quality`, `--view-distance <blocks>` (48 to 576, kept to the menu's steps of 16), `--fov <degrees>` (60 to 110 in steps of 5), `--mouse-sensitivity <percent>` (25 to 300 in steps of 5), `--invert-y on|off`, `--underwater-blur`, `--view-effects`, `--needs-rate`, `--relaxed-needs`) wins over the saved value for that run; the menu still shows and changes the value while the game runs. A new game without `--load` starts from the defaults. A scripted run (`--screenshot`) keeps the text at 100 percent unless `--text-scale` says otherwise. The page ends with a note on what needs a restart (the window size, speech and microphone; the seed, content, scene, language and save to load; the sound log, the session and report folders and the scripted steps are command-line only). **Testing** (a labelled section at the foot of the Settings page under *Testing*, in groups; not kept in the save except a held clock or weather, which is simulation state (see Day, night and weather); left out of a `--playtest`, whose measurements these tools would change). *Needs*: needs running (`--needs`), a slider each for hunger, thirst, sleep, health and stamina, 0 to 100 in steps of 5 (live, as `--set-need`; stamina stops at its current maximum; health 0 is a death), refill all needs. *World and view*: debug overlay and notes (F3, `--dev`), camera over the shoulder (F5, `--third-person`), dusk lighting (`--dusk`: the day clock is held at 21, the middle of the dusk, and the sky, the fog, the world's light, the held arm and the ambient beds follow; switched off, the clock runs on from there; `--time <hour>` holds it at any hour, E02), greyscale view (`--greyscale`: unknown nouns' blocks grey), all nouns known (view only: no block is grey while the learner's words are untouched, so nothing of it reaches a save, unlike the launch flag `--assume-known <nouns>`, which credits the nouns it names), first-time tips (`--tips`; shown or hidden), beacon (off, or one of the scene's named points, as `--beacon`; "off" also dismisses the guide's arrow, as arriving would). *Teleport*: a place picker over the same named points, then "Teleport to the place" puts the player on the ground of the point's column, as `--teleport` does. *Sound*: all sounds but voices off (`--no-sounds`), voices off (`--mute`). *Scoring*: typed answers earn half or full credit (`--typed-credit`; the rule only changes what later uses earn, and is not in a save). *Files*: autosave on or off (`--no-autosave` starts it off; turning it on starts its five-minute count from then), save now (F6). Not offered because they need a restart, a path or a text field: `--sound-log`, `--session-dir`, `--report-dir`, `--report-kind` (the report panel has its own Bug and Feature buttons). **Controls** lists every key and mouse control, made from `ControlTable` (`src/Vocaterra.Game.Core/Input/ControlTable.cs`), the one table the key bindings and the panels read their keys from, so the page cannot drift from the game. Screenshots of the pages: `--menu`, `--settings`, `--controls` open the menu on that page once the steps are done; `--menu-scroll <rows>` scrolls the Settings or Controls page. The rate (settings `needsRate`, default 1, written only when not 1) multiplies hunger, thirst and sleep falls, from 0.5 to 4 in steps of 0.25, and multiplies with relaxed needs; stamina, health's recovery, food values and the collapse delay do not change. Launch flag `--needs-rate <x>` sets it for a run (a value outside 0.5 to 4 or off a step is refused). It is a setting, not world state: no meter, digest or replay holds it. When it is not 1, the session log's `session_summary` line carries `needsRate` and the console summary prints "drive rate ×x" (so a playtest's numbers say the rate). The needs playtest uses the slider to choose a fixed rate afterwards (D-game-24).

Death shows a fade, then «Estás de vuelta.» with its English and the cause line, for 4 seconds; the chest's beacon label is «tus cosas».

**Death (N04).** At zero health in active play the screen goes black and the player wakes two to five blocks from the place they last slept at (the inn's bed, or Luna's fire), or from Luna's fire ring (`firering`, lit or cold; `death.wakeAt`, the sleep place `guide.fire`) when they have slept nowhere, with
health, hunger, thirst and sleep at 50 and stamina full; `player_died` (`hambre` or `sed`) and `player_woke` (`death`) print. The day and the sitting do not move. Everything in the bag
but the items that stack to one (the book and the axe) stays in a chest on the nearest free ground where they fell (the beacon points to it); **E** on the chest takes back what fits,
and the next night removes it. `--take-belongings` is the scripted E (walk next to the chest and `--look` at it first); it prints `take-belongings: Done, chest gone`.
`--set-need thirst 0 --set-need health 5 --wait 25` is the quick death.

```bash
# hunger 40, a bread: the meter goes to 65 and meal_eaten (pan) is raised; a drink by hand at the river bank after a --look at the water
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --set-need hunger 40 --set-need thirst 30 --eat pan --teleport 234,222 --look 270,-70 --wait 1 --drink --screenshot /tmp/n02.png --frames 30
```

`--greyscale` is the playtest A/B switch (D-game-6 as amended): a block whose noun is not known yet is drawn grey, a known one in full colour, and
a noun that becomes known during play gets its blocks back in colour with the toast `¡Nueva palabra: el agua!`. The world is in full colour
without the flag; the figures, the sky, the undergrowth, the snow and the UI are always in colour.

```bash
dotnet run --project src/Vocaterra.Game -- --seed 42 --greyscale --look 120,-5 --screenshot /tmp/s4b-grey.png --frames 120
```

WASD or the arrows walk, Space jumps, the mouse looks (Esc opens the menu, which lets the mouse go; a click or Resume takes it back). **Hold the left button** on a trunk
(`madera`, five for the camp) or a berry bush (`fruta`, three) to gather it, the right button places the selected item, `1`..`0` or the wheel
pick the hotbar slot, **I** the inventory, **C** the crafting panel, **E** the inventory or, with the crosshair on the workbench (`mesa`), the
crafting panel; the same at a lit fire ring (`fuego`, N07: a ring with a torch in its three-by-three; an unlit ring does nothing, and the sleep offer waits while the crosshair is on a lit ring). The panel lists the recipes (the torch: `antorcha` from two `madera` at the workbench; the roast apple: `manzana` from one `manzana` at a lit fire, labelled `manzana asada`); a click on a row crafts it. **J** the
book, **F3** the debug overlay and the dialogue's developer note (the grey "intent, understood, 100 %, 12 ms" line under your line), both off at the start unless the run has `--dev` (U01; not in a playtest, where F3 does nothing), **F5** the view over your own shoulder and back (S15: first person is where every run starts; `--third-person` starts
in the shoulder view), **F6** saves. **Aim the crosshair at the guide's figure**: the hint `E: talk` shows, and **E** (or **T** to type, or **V**
to talk: hold it, the clip goes when it is let go) opens the dialogue panel, which takes the keyboard and frees the mouse. **The panel has
speech buttons (S7)**: `[R] Repetir`, `[S] Más despacio`, `[H] Ayuda`, `[N] No sé` and `[W] Escribir` (keys or the mouse); hold **V** or **Space** to talk;
`Escribir` (W or Tab) opens the text box, where Enter sends and Tab or Esc go back to the buttons; Esc cancels a turn that is "Thinking…", then closes the
panel. After a spoken turn the line `Heard: «…» · Expected: «…»` stays under the conversation; when the NPC offers choices they are buttons too (keys 1 to 3).
**The conversation surface (U02).** The panel shows the current exchange: your last line, the NPC's lines after it with their English at the phrase's rung, and one coaching line (the microphone's trouble for an unsure recognition, "not understood", the one recast of a slip; a line the NPC did not take for its prompt, typed or spoken, is "not understood"). The older lines are collapsed (the panel says how many); **PageUp** (or **O** off the text box) shows them and hides them again, in every state; while the page shows, **Up** and **Down** scroll it back to the oldest lines and forward to the newest, and a row says how many lines are out of view above or below (U02b); Tab is the Escribir key, so it is not the expand key. **The mouse wheel scrolls the panel (U06):** a notch up on the collapsed panel shows the older lines, further notches go back one block each, a notch down comes forward, and a notch down at the newest line folds the page back to the current exchange; it works while typing, and while the panel is open the wheel does not change the hotbar slot. A thin **scrollbar** at the panel's right edge shows whenever lines are out of view (the thumb is the rows in view against all rows). Each NPC keeps its own page of up to 200 lines for the session (coming back to Luna shows Luna's earlier lines); the pages are kept in the save (U08, `dialoguePages`, `docs/game/saves.md`), so after a load the panel scrolls back over the earlier sessions, and the first talk with an NPC then adds its greeting to its page. The book's "What I said" keeps the whole history. **The NPC's name is the panel's title (U05):** 32 px in the warm name colour, with a rule under it (16 px in a compact window under 540 px high, to keep the rows), and the NPC's lines carry no `Name:` prefix (yours keep `You:`). While a lesson step or offer is up, its heading (`Lesson · First words · step 1 of 7 · Say it`, the title shortened first at a narrow window, U02b) is in the panel's header, beside the name, in the muted colour, and the step's text and buttons stand flush on the panel, with no box of their own. Every button has its key in every state (idle, recording, thinking, after the timeout offers typing, after a turn): R, S, H, N, W or Tab, 1 to 3, Enter on a focused Escribir, Esc to cancel.
A mic check (`Say "hola"`, a level meter, what was heard) opens an interactive run (`--no-mic-check` skips it, `--mic-check` forces it). Her answers point the beacon at the named noun's nearest block (the arrow at the screen edge when it is off
screen); "¿qué es esto?" names the block under the crosshair. **The shopkeeper and the villager are talked to the same way** (S6), and so are the fisher, the baker, the smith and the farmer (village-plan V10 to V12): aim at any figure, `E: talk`; the panel's title is the NPC's name. Outside a lesson each shows the cards of the phrases its prompt practises (its `focus`, V12b): the fisher `¿Dónde está la panadería?` and `¿Dónde está la herrería?`, the baker `Quiero dos panes, por favor.`, `¿Cuánto cuesta?` and `¿Hay pan?` (the third collapses to a strip at 1280 x 720), the smith and the farmer `¿Cómo te llamas?` and `¿Qué haces?`; a turn is credited to the phrase it used there (`phrase quiero-dos-panes supported at baker`).

**The camp objective (S4).** The checklist top right, `Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1`, is completed by the
game's events and the inventory, never by a language score: five trunks, three berries, the torch placed at the fire ring, and the water: the shopkeeper hands over an empty bottle (`la botella`), and holding the left button on the river with it in the
inventory fills it (half a second; without a bottle a toast says `You need a bottle.` and nothing is blocked). Each step that completes shows a toast. The first time an item enters the inventory (a pickup, a craft)
its word card shows, `la madera`, `la fruta`, `la antorcha`, with an empty audio slot for S9; the word counts as heard and read, never as a quiz.

To check it without playing, script it and save a screenshot (needs a display; every line also prints what its step did):

```bash
# the camp (S4): gather, craft at the workbench, place the torch on the fire ring's rim, look at it: the checklist reads 3/4
dotnet run --project src/Vocaterra.Game -- --seed 42 --walk-to 176,186 --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 --walk-to 176,184 --look 180,-15 --screenshot /tmp/s4-camp.png
# the guide answers "where is the river?": the console prints the verdict, her line and the arrow; the PNG shows the panel and the arrow
dotnet run --project src/Vocaterra.Game -- --seed 42 --look 120,-5 --say "¿dónde está el río?" --screenshot /tmp/s3-river.png --frames 300
# the village: walk to the square's north side and look at the stall: the shopkeeper behind the counter facing the player, with his label
dotnet run --project src/Vocaterra.Game -- --seed 42 --beacon none --teleport 282,211 --look 90,-8 --screenshot /tmp/s3-stall.png --frames 150
# the signpost, from the road on its west side (S15): the boards Tienda, Río and Montaña with their arrows, in the letters of the block texture
dotnet run --project src/Vocaterra.Game -- --seed 42 --beacon none --teleport 272,222 --look 90,2 --screenshot /tmp/s3-sign.png --frames 150
# the camp from the player's eyes at spawn: the guide's figure and label, the beacon on the river (an arrow at the edge when it is off screen)
dotnet run --project src/Vocaterra.Game -- --seed 42 --look 120,-5 --beacon river --screenshot /tmp/s2-camp.png --frames 240
# S17, the whole route in one run, from the player's own camp: the walk down the trail to the guide, her lesson (`--lesson-run` does every step with typed
# answers and picks the fruit she offers from a bush), the camp (wood, fruit, the torch at her fire ring), the stall (the shopkeeper's "¿Qué necesitas?" is a
# core host turn, the bottle comes with his answer), the villager (the beacon on the river), the river (hold the button on the water: the bottle fills and the
# checklist is complete: Luna's line sends the player to the inn and the beacon points there, Y07), and the walk to the
# inn, where the innkeeper's «sí» gives the room: the closing card says "Session complete" (the innkeeper's line); the walk to the bed and `--sleep` begin session 2
# at the inn (Y13: this is r40-inn's route). The console prints every lesson step and its
# outcome, each turn, each game event and the objective's steps; the PNGs show the lesson's end, the stall, the river, the closing card and the morning. One step is spoken
# (S22): after the lesson the route greets the guide with `--wav` and the bundle's recorded "Hola." (the Kokoro line), so the step goes through Vosk (the
# console prints "heard "hola"" and the greet intent). It needs the Vosk model in VOCATERRA_MODELS_DIR; the other steps are typed.
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute \
  --lesson-run first-words --shot /tmp/s17-lesson.png \
  --talk guide --wav content/game-v1/speech/guide/phrase.hola.wav \
  --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --shot /tmp/s17-stall.png --say "sí, por favor" \
  --talk villager --say "¿dónde está el río?" \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 --shot /tmp/s17-river.png \
  --talk innkeeper --say "sí, por favor" --leave --wait 1 --shot /tmp/s17-ending.png \
  --walk-to 279,231 --look 270,-20 --sleep --look 129,-20 --wait 2 \
  --screenshot /tmp/s17-morning.png --frames 90
# the lesson alone, with its steps printed (the player is walked down the trail to the guide first); the book's phrases page at the end shows what was learnt
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --journal --journal-phrases --screenshot /tmp/s17-journal.png --frames 30
# the opening: the objective line and the first tip at the spawn (a scripted run draws no tips unless --tips is given)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --tips --screenshot /tmp/s17-opening.png --frames 90
dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need
```

**Day one, day two and the camp, scripted (Y13).** The settlement series' whole route, as `scripts/scripted-runs.sh` plays it (r40 to r43, [scripted-runs.md](scripted-runs.md)); every command is `--seed 42 --mute` and needs `--dev` for the aids named:

- **Day one, ending at the inn (r40-inn).** The route of the S17 block above without the guide's spoken hello: the camp's steps, the shopkeeper (`--talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" --leave`), the river (`--walk-to 234,222 --interact 233,34,222`: `agua 1/1`, `objective: complete`, `guide says: ¡Muy bien! Ahora ve a la posada.`, the beacon on the inn, `posada 46 blocks`), Rosa (`--talk innkeeper --say "sí, por favor"`: `innkeeper.room`, `lodging_granted`), the card, `--walk-to 279,231` (the bed's stand cell), `--sleep` (`session 1 ends at the inn, session 2 begins`) and the morning in the guest room, where the tracker shows «Mi terreno».
- **Day two, the land (r41-land).** `--skip-to-session 2 --mark-plot 266,165` (the stake's use on the meadow at the start), `--talk villager --say "Quiero una estaca, por favor."` (the mayor gives the stake), `--say "Quiero un terreno, por favor."` with no coin (`cant_pay`), ten `--give moneda`, the same line again (`plot_bought`, ten coins and the stake out, the camp kit in the bag), and the owned plot's outline. The stake in the village is refused (`--give estaca --mark-plot 285,206`: `land: the stake at 285,206 was refused: Protected, Village`, r43). `--own-plot x,z` skips the purchase (and raises no `plot_bought`, so the `terreno` step stays 0/1 until a load).
- **The camp (r42-camp).** `--own-plot 261,160 --give equipo --place-camp 263,161,north` (`camp: Placed at 263,161 facing North, fire 263,37,161, bed 263,37,166, wake 263,37,163`), `--give antorcha` and `--place antorcha@262,38,161` (the torch on the ring's rim: `fire_lit`), `--set-time 23`, `--sleep` (`session 2 ends at your camp`), `--plant 268,36,167` (a berry; `--needs on`), `--need health 5 --need thirst 0 --wait 25` away from the camp (`death: 1, woke at 263,37,163`), and a held `--interact 262,37,161` packing the camp back (45 ticks). The camp's wake cell is the camp's own: a death at the wake cell itself puts the chest there and the player on the next free cell.

The steps `--look yaw,pitch` (degrees, yaw 0 north and 90 east), `--walk-to x,z`, `--interact x,y,z`, `--place item@x,y,z`, `--select n` and
`--craft item` run in the order given, and so do the talk steps (S6): `--talk <npc>` (an id of `game/npcs.json`: `guide`, `shopkeeper`, `villager`,
`fisher`, `baker`, `smith`, `farmer` or `innkeeper`; walk up to the figure, stop about two blocks away facing it, open the panel), `--say <line>` (type and send a line to the NPC the panel is open on and wait for the reply; with no
panel open it first talks to the guide, as in S3), `--expand` (show or hide the older lines, U02), `--scroll N` (scroll the expanded page N blocks towards the older lines, or towards the newer for a negative N, U06), `--leave` (close the panel; walking on or talking to someone else closes it too) and `--shot
<png>` (save the screen at that point and go on); `--beacon <point>` stands the beacon column on a named point of the scene. Details are in `src/Vocaterra.Game/README.md`.
Spoken input (push-to-talk) works in the interactive game. The scripted spoken path (S7): `--speak "<line>"` synthesizes the line with Kokoro and
says it to the NPC the panel is open on as if spoken (Vosk hears it, in the core's Constrained mode at the stall's and the guide's offer prompts), `--wav
<file>` does the same from a recording, and `--mic-check --screenshot <png>` runs the mic check on a synthesized "hola" when there is no microphone:

```bash
dotnet run --project src/Vocaterra.Game -- --seed 42 --talk shopkeeper --speak "quiero agua" --screenshot /tmp/s7-stall.png
dotnet run --project src/Vocaterra.Game -- --seed 42 --talk shopkeeper --speak "agua agua mesa" --speak "agua agua mesa" --speak "agua agua mesa" --screenshot /tmp/s7-choices.png
dotnet run --project src/Vocaterra.Game -- --seed 42 --mic-check --screenshot /tmp/s7-mic.png
```

**Phrase progress and the fading support (S8).** The panel shows the phrase the NPC's prompt asks for: at the stall a card above the panel
(`Puedes decir` and the phrase, its English and the word glosses) and the model's audio cue, for as long as the phrase's support level is
`CardAndAudio`; after correct uses past the spacing gap the card goes (`AudioOnly`: a chip `[R] Escuchar la frase`, and Repetir plays the model),
then nothing until Ayuda shows it again; "no sé", a choice and a fail-forward bring the help back a step. The first time the shopkeeper's panel
opens, the key words `el agua` and `el pan` stand alone as word cards for two and a half seconds, then the question. **J** opens the journal;
**Tab** (or the arrows) turns it between the words and the **phrases** page (each phrase with its English, the help it has now and its counts:
with help, without help, choices, "no sé", another situation). The console prints `phrase: pedir-agua, supported 1, unaided 0, ...` after each
phrase use and a summary at exit. A due phrase (ten minutes after a credited use) is asked by the guide in context. Scripted: `--journal-phrases`
opens the phrases page, `--wait <seconds>` runs the game on, `--skip <seconds>` moves the stored time on:

**The book's hierarchy (U07, D-game-28).** The five pages follow one scheme. The left page's title is the page's name at 32 px in its ribbon's colour with a 2 px rule of that colour under it (the "Your book  -" prefix is gone: the cover and the ribbon say it); the list holds 28 rows at 1200 x 720 and 22 at 800 x 600. Rows are sections (a band in the page's accent with the count at the right; "Not met yet" is a taupe band), entries (one selectable row: a mark or icon, the text in the ink of its tone, an aside, pips or lamps, a speaker or a button), quiet notes, and rows of grey tiles for the nouns not met. The selected row keeps its gold and has a 3 px edge in the page's accent. The right page writes the selected entry's block: its title at 32 px, labels with a rule (the "strong" weight, the text drawn twice 1 px apart, is for known entries, those labels and the next lesson only), and numbers at 32 px over their captions. **Nouns:** Known, Learning and Not met yet (the last as tiles); the counts, due time, forms and last sentence are on the right page. **Verbs:** one row per verb, the table on the right with its legend as three swatches. **Phrases:** Without help, With help, Not met yet (grouped by help, not by lesson), the lamps named on the right page. **Lessons:** one row per lesson with a mark (ticked box done, half box under way, arrow next, dashed box waiting) and its state as the aside; the units, the help counts and the phrases, each phrase with its speaker button, are on the right page (a click on that button plays it; the keys still reach every recording from the Phrases page). **What I said:** a section per play session, each line with a mark for how the core took it (tick understood, "!" small mistake, cross not understood, dot held back or asked for help), and its outcome in words, source, place and feedback on the right page. A click on any entry's row selects it; Up and Down move among the entries. The scripted runs' pages are the same: `--book-page`, `--book-verb`, `--journal`.

```bash
# the stall's key words, then the question with the card (the two PNGs), a use, ten minutes later the guide asks for it, the book's phrases page
dotnet run --project src/Vocaterra.Game -- --seed 42 --typed-credit full --no-speech --talk shopkeeper --shot /tmp/s8-intro.png --wait 3 --shot /tmp/s8-stall.png \
  --say "quiero agua, por favor" --skip 660 --talk guide --say "quiero agua, por favor" --journal-phrases --screenshot /tmp/s8-journal.png --frames 60
```

**The village, the figures and the HUD (S15).** The valley holds the village the brief's references show. The houses are timber-framed (posts at the corners, a beam
under the eaves, plaster between) under roofs that step in a block a layer, with a cobble chimney at a corner the position picks; a stone tower with a door, four
window slits, a timber band and a flagpole stands behind the square; a crop field of tilled soil and rows of wheat (a seeded gap here and there) sits inside a
fence with a gate; fence lines run along the square; and six lanterns on posts stand by the doors, the square and the road. The signpost carries three boards
(`Tienda`, `Río`, `Montaña`), each an arrow and the word in a 3 x 5 pixel font on the plank of a block texture, with the accent and the tilde drawn; the words are on the faces that read left to right from the south and the west (the road comes in from the west), and the other sides are plain plank. The stall has
a red and white awning that reaches over the front, bread, apples and bottles on the counter and on two crates behind it (which of the three each cell holds comes
from the seed), and thin fence posts at the front, so the player looks past them at the shopkeeper. The guide's workbench stands under a timber pergola with a torch
on one post and a lantern hanging over the bench, with a chest and crates beside it; the mountain ledge is bare (Y08; the `tent` stamp stays defined for the campsite, Y12). The lanterns tint the faces within seven blocks warm (half as strongly by day, fully at dusk, at dawn and by night, and so with `--dusk` and `--time`).
Everything is placed by `SceneBuilder` from `scene.json` and the seed's `WorldRng` (the stall's goods, the field's gaps), so it is part of the scene digest and
a test pins the village's and the camps' structure.

**The figures.** The guide Luna (green jacket, yellow scarf, long hair, a pack), the shopkeeper (a white shirt with rolled sleeves, a leather apron, a beard and a
baker's hat), the villager (a blue tunic and a straw hat), since village-plan V05 the fisher Mar (a blue coat, tall boots, a knitted cap and a grey beard), the
baker Luz (a rose dress with flour-white sleeves, a white apron and cap), the smith Domingo (a rust shirt, a long dark apron, a moustache) and the farmer León (a
green shirt and the villager's straw hat in tan), since Y06 the innkeeper Rosa (a plum dress, a mustard shawl, silver hair pinned up and a ring of keys at her belt), and the player (a blue shirt and a backpack with a bedroll) are boxes of the atlas's cloth, skin and
hair pictures with a drawn face on the front of the head, shaded by the same sun table as the blocks. They idle (a breath, swaying arms, a head that looks about);
**when you talk to one it turns to you** (`FigureFacing`: the shorter way round, at most 80 angle steps a tick) and nods and waves its right arm, and turns back to
where the scene put it when the talk is over. **F5** shows you from behind your right shoulder (the camera stands three blocks back and never inside a wall), with
the figure walking when you walk; first person has the arm instead.

**The arm.** The first-person arm is a blocky forearm in the player's blue sleeve with a hand in the player's skin, drawn in 3D with the figures' cloth and skin
pictures, no outline, tinted by the light where you stand (dimmer under cover, warm near a lantern or with a torch in hand). It holds what the hotbar has selected:
a log for wood, a torch with its flame for a torch, a bottle, or a flat berry or loaf, and since village-plan V05 a coin, an apple or the axe; it sways when you walk, chops while you gather and dips once when you place.

**The HUD.** The hotbar is a row of dark rounded slots with the item's pixel-art icon and the count; the selected one has a bright frame. At the workbench a panel at the
right shows the recipe as icons: what it makes at the top and, under the arrow, each ingredient with `have/need` in green or red. The dialogue panel is a dark
rounded panel with the speaker's name in a warm colour, and the speaker's latest line also hangs in a bubble over the figure's head when it is on the screen.

**Save, sessions and resume (S10).** `--save <file>` writes the game when the run ends (on quit, or at the end of a script), and **F6** writes it in an
interactive run (S15: F5 became the view key) (to the `--save` file, or `vocaterra.save.json` in the working folder); a turn still thinking means "not now", and a quit waits up to five
seconds for it. `--load <file>` starts from a save: the same world (the trees and the scene come from the seed, and the save holds only the blocks the
game changed), the player where they stood, the hotbar, the inventory, the objective, every word and phrase count, each NPC's conversation state, and the
stored time. A load keeps the **day** as saved (V25: the objective, the orders, the lessons and the journal's header `Sesión n` stand where the save left them; only a sleep moves it) and begins the next **sitting**, which lifts the spacing gaps
of D-game-6 (a launch, a load and a sleep each begin a sitting; the save keeps it in `Sitting`, and a save from before V25 continues from its day), so a phrase or word whose ten-minute gap had not passed becomes creditable again. **Time rule:** a load adds nothing to the stored time (it
continues from the save: start, plus the ticks, plus the skipped seconds); the next-day recall check moves the stored time with `--skip <seconds>` after the
load when a scenario needs a day to pass. A load is refused, with the reason, when `--seed` names another world; a save whose restored world does not match its digest is refused too. A save made
from **another content bundle** is carried into this one (X01, below), unless the run is strict (`--strict-save`, or any `--playtest`), which refuses it. The file is canonical JSON (sorted keys, no whitespace):
the same state gives the same bytes, and a load followed by a save gives the same file except for the session number. Not saved: the open dialogue,
the review question, the word cards not yet shown, the replay records of the conversations (the session log is S11's). A sleep at a bed or a lit fire is a
load in the same run (V12c, below): the save it gives is the save of a save and a load.

```bash
# S6's whole route, then the game saved at its end: the console prints the file, the session, the tick, the stored time and the world digest
dotnet run --project src/Vocaterra.Game -- --seed 42 \
  --walk-to 176,186 --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" \
  --talk villager --say "¿dónde está el río?" \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 \
  --talk guide --say "sí, por favor" --talk villager --say "quiero pan, por favor" \
  --screenshot /tmp/s10-end.png --frames 60 --save /tmp/s10.save
# the same world, the same day (V25: a load keeps its day and moves the sitting): "load:" prints the saved session, its sitting and the digest check; the journal's phrase page reads "Sesión 2"
dotnet run --project src/Vocaterra.Game -- --load /tmp/s10.save --journal-phrases --screenshot /tmp/s10-load.png
# refused: the save is for world seed 42 (the message says so and nothing starts)
dotnet run --project src/Vocaterra.Game -- --load /tmp/s10.save --seed 7
```

**Checks, the session log and the report (S11).** `--check pre|post|recall` runs the check battery outside the world, on its own screen (the
items are `content/game-v1/game/checks.json`): items that play a Spanish line and offer three meanings (keys **1**, **2**, **3**; **R**
plays it again), and items that ask the learner to say a phrase in Spanish for an English situation (hold **V** to speak, **W** to type and
**Enter** to send, **N** for "no sé"). S11 had ten; since village-plan V07 a battery asks the items of the lessons its play session has unlocked and
always those of the first three sessions (`coreSessions: 3`): 31 items up to session 3 (16 heard, 15 said; the pilot's battery), then eight more with each
village lesson, 39 in session 4 to 79 in session 9, and all 87 of the bank in session 10. The order and the options' order come from the world seed and the kind, so a pre-check and a post-check
hold the same items in other orders. **Esc** ends the battery (the rest is skipped). `pre` is followed by the world; `post` and `recall` end the
run after the battery. A check never changes the learner model, the phrase progress, the conversations or the world. The answers go to the
session log: **`--session-dir <dir>`** names the folder (default: a new folder named by the local date and time and the session number, under
`~/.local/share/vocaterra/sessions/`), which holds `session.jsonl` (one JSON object per line: `session_start`, `turn`, `phrase`, `voice`, `world`, `session_summary` (X04: the closing card's lines),
`check_start`, `check_result`, `check_end`, `session_end`) and the core's `replay.jsonl`. A folder that already has a log is appended to. Scripted:
`--pick <n>` answers a comprehension item, `--say` and `--speak` answer a production item, `--shot` saves the screen where it stands; a scripted run
prints the item order and the right option of each item on the console.

```bash
# the pre-check, scripted (the console prints the order: "check order: ..."), a screenshot of the first comprehension item, then the world
dotnet run --project src/Vocaterra.Game -- --seed 42 --check pre --session-dir /tmp/sess1 --wait 2 --shot /tmp/s11-check.png \
  --pick 1 --say "¿Qué es esto?" --say "Sí." --pick 3 --say "No, gracias." --pick 2 --say "Necesitamos agua." --say "¿Dónde está el río?" --say "No." --say "Más despacio, por favor." --say "Otra vez, por favor." --say "Ella quiere fruta." --say "Tengo una fruta." --pick 2 --pick 3 --pick 3 --say "Ayer comí fruta." --pick 3 --pick 2 --pick 3 --pick 3 --pick 2 --pick 1 --say "Sí, por favor." --pick 3 --pick 3 --pick 2 --pick 2 --say "Voy a comer pan." --say "Quiero madera." --say "Quiero agua, por favor." \
  --screenshot /tmp/s11-world.png --frames 60 --save /tmp/s11.save
# the S6 route into the same folder (a second run in the folder: another session_start and session_end)
dotnet run --project src/Vocaterra.Game -- --seed 42 --session-dir /tmp/sess1 --talk shopkeeper --say "quiero agua, por favor" ...
# the recall check on the saved game, in the next play session (the probe prints "check order:" first; the answers follow it)
dotnet run --project src/Vocaterra.Game -- --load /tmp/s11.save --check recall --session-dir /tmp/sess2 --wait 2 --shot /tmp/s11-recall.png \
  --pick 2 --say "Otra vez, por favor." --pick 1 --pick 1 --pick 3 --pick 1 --pick 1 --say "Ella quiere fruta." --say "Más despacio, por favor." --pick 1 --pick 1 --say "¿Qué es esto?" --say "No, gracias." --pick 3 --pick 3 --say "Sí." --pick 3 --say "Voy a comer pan." --pick 1 --say "¿Dónde está el río?" --pick 3 --say "Quiero madera." --say "Ayer comí fruta." --say "Tengo una fruta." --pick 2 --pick 1 --say "Necesitamos agua." --pick 3 --say "No." --say "Sí, por favor." --say "Quiero agua, por favor." \
  --screenshot /tmp/s11-recall-end.png --frames 30
# the report: per folder and in total, the completion, retries, support, ASR latency, the checks by item kind, transfer, and the gates' evidence
bash scripts/playtest-report.sh /tmp/sess1 /tmp/sess2
```

The report (`scripts/playtest-report.sh`, bash and Python 3's standard library, run with `python3 -I`) reads only the session folders. Its gate lines
(Playable, Learning signal, Transfer, Enjoyable) are evidence, never a verdict on learning; Enjoyable needs observer notes.

## Day, night and weather (E01 to E06)

The day and the weather are the game's clock and sky: the data is `content/game-v1/game/day.json` and `game/weather.json`, the state is in
`Vocaterra.Game.Core/Atmosphere/`, and the drawing is in `Graphics/Sky` (the dome, clouds, sun, moon, stars and fog) and `Graphics/Weather` (the rain).
Both run on their own in play, and a screenshot run can hold either one.

- **The cycle.** One day and night is 2400 seconds (40 minutes) at 60 ticks a second. On a 24-hour dial, dawn begins at hour 4, day at 6, dusk at 20
  and night at 22: the light rises across dawn, holds through the day and falls across dusk, and it is out through the night (about 30 minutes of
  light and 10 of night). A new game starts at 8 (`startHour`); a sleep or a collapse wakes the player at 7 (`wakeHour`). The cycle runs whether or not
  the player sleeps.
- **What each hour looks like (E11).** The look follows the sun's height by day and its depth by night, and the daylight across dawn and dusk eases
  (a smoothstep, still 0.5 half way), so nothing steps. 4:00 is the night's blue hour; 4:00 to 5:00 the sky lifts through the twilight's amber (5:00 is the
  middle of the dawn, the same look as 21:00); 5:00 to 6:00 it goes on to the low sun's golden look; 6:00 to 10:00 the sky is a pale warm gold that clears to blue
  and white (8:00 is half way); 10:00 to 16:00 is noon, the sky the game has always had; 16:00 to 20:00 the gold comes back, warmer towards the evening; 20:00
  to 22:00 is the dusk (21:00 is `--dusk`: amber, the low warm light, the lanterns at full); 22:00 is the blue hour again and the night deepens, darker and
  less blue, to its darkest round 1:00 and lifts again to 4:00. The lanterns are at half by day, a little above it in the golden hours and at full from the dusk
  to the dawn. Weather composes after all of it, as before.
- **The weather.** A new game starts sunny. A change blends the channels over 45 seconds (`transitionSeconds`). A spell lasts a time drawn from its
  range, and when it ends the next kind is drawn by the weights of `next`; waking draws with the `morningWeight`s. The draws come from the world seed,
  so one seed has one weather.

  | Kind | Cloud cover | Sunlight | Fog | Wind | Falls | A spell lasts | Next (weight) | On waking |
  |---|---|---|---|---|---|---|---|---|
  | `sunny` | 0.42 | 1 | 0 | 0.2 | nothing | 4 to 10 min | `cloudy` (1) | 6 |
  | `cloudy` | 0.9 | 0.7 | 0.25 | 0.4 | nothing | 3 to 7 min | `sunny` (3), `rain` (2) | 3 |
  | `rain` | 1 | 0.5 | 0.5 | 0.6 | rain, intensity 0.8 | 2 to 5 min | `cloudy` (3), `sunny` (1) | 1 |

  Each kind's `say` is the dialogue key an NPC answers «¿Qué tiempo hace?» with: `sunny` says «sun» by day and «fair» at night, the others
  say their own name (`content/README.md`, "Adding a weather kind").
- **Holding the hour and the weather.** `--time <hour>` holds the clock at an hour from 0 to 24 (fractions allowed: `--time 7.5` is half past seven).
  `--dusk` holds it at 21, the middle of the dusk, with the amber sky, the low warm light and the lanterns at full strength, and the sun standing on
  the horizon in the west; with both, `--time` wins. `--weather <id>` holds the weather at a kind of the loaded bundle (`sunny`, `cloudy` and `rain` in
  `game-v1`; an unknown id is refused with the list of ids), from the first frame and with no blend. The two combine.
- **Scripted steps.** `--set-time <hour>` holds the clock from that step on, and `--set-time run` lets it run on; `--set-weather <id>` holds a kind
  from that step on, with no transition, and `--set-weather run` releases it. An hour outside 0 to 24 is a usage error, and an id the bundle lacks is
  refused at launch. The steps go in the order given with the others (`docs/game/scripted-runs.md`).
- **The testing menu.** Settings, *Testing*, *World and view* has two rows: **Time of day** (Running, then 0:00 to 23:00) and **Weather** (Running, then
  the ids). They hold and release the same things as the flags and steps, and show what the simulation holds: a clock held by `--time` or `--dusk`
  shows its hour. Running lets the clock run on from where it is.
- **The F3 line.** Under the position: the hour as `hh:mm` and the phase, with `held` when the clock is held; the weather's kind, and during a change the
  kind it leaves and the blend in percent; `held` when the weather is held.
- **Held is saved.** A held hour or weather is part of the simulation's state: it is in the state digest and in a save written during the run, which
  loads held (`docs/game/saves.md`, the `atmosphere` entry).
- **Pictures.** The hours worth a picture are 0 (night), 5 (dawn), 7 (morning), 13 (noon), 18 (evening), 21 (dusk) and 23 (night); the light changes bands at about
  4.43, 5.51, 20.49 and 21.57 (the meshes are built again in the background for the new band, a few seconds, and show no step in the sun's light):

  ```bash
  for h in 0 5 7 13 18 21 23; do
    DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --time $h --screenshot /tmp/e02-$h.png --frames 30
  done
  for k in sunny cloudy rain; do
    DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --time 13 --weather $k --look 128,-13 --screenshot /tmp/e05-$k.png --frames 30
  done
  ```

## The playtest (S12)

`--playtest` is the profile for the playtests (the facilitator's script is [docs/game/playtest.md](playtest.md)): no debug overlay (F3 is off),
the mic check on, the session log in its own folder under `~/.local/share/vocaterra/sessions/` (or `--session-dir`), the pre-check before the
world (a fresh start) or the recall check (with `--load`), and `--greyscale` only when given (the A/B arm B; arm A is the default). Nothing in
the profile blocks progress, and Esc always works. The run ends like this: the **post-check** starts by itself once the camp is complete and the
guide has made her transfer offer, and the tester has closed her panel (or at once with **F10**, which the screen labels for the facilitator);
then the game saves to `playtest.save` in the session folder and shows the thank-you screen until Esc.

```bash
# a playtest in a checkout (the speech models in VOCATERRA_MODELS_DIR; the pre-check first, then the world, then the post-check)
dotnet run --project src/Vocaterra.Game -- --playtest
dotnet run --project src/Vocaterra.Game -- --playtest --greyscale           # arm B of the A/B
# the next-day recall of a playtest's save: the recall check, then the thank-you; nothing is saved
dotnet run --project src/Vocaterra.Game -- --playtest --load ~/.local/share/vocaterra/sessions/<folder>/playtest.save
# the same run, scripted (a screenshot needs a display; the pre-check's answers are --pick and --say; the post-check's answers come after the
# transfer's --leave and the room at the inn (the playtest ends with it), in the order the console prints as "check order:" (both lists below are for seed 42 and the 31 items of the first session's battery; when they change, run once and read both orders from the console);
# see the S11 lines above for the shape of a check's answers)
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --playtest --seed 42 --session-dir /tmp/playtest1 \
  --pick 1 --say "¿Qué es esto?" --say "Sí." --pick 3 --say "No, gracias." --pick 2 --say "Necesitamos agua." --say "¿Dónde está el río?" --say "No." --say "Más despacio, por favor." --say "Otra vez, por favor." --say "Ella quiere fruta." --say "Tengo una fruta." --pick 2 --pick 3 --pick 3 --say "Ayer comí fruta." --pick 3 --pick 2 --pick 3 --pick 3 --pick 2 --pick 1 --say "Sí, por favor." --pick 3 --pick 3 --pick 2 --pick 2 --say "Voy a comer pan." --say "Quiero madera." --say "Quiero agua, por favor." \
  --lesson-run first-words --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" --talk villager --say "¿dónde está el río?" \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 --talk guide --say "sí, por favor" --leave \
  --walk-to 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 \
  --say "Necesitamos agua." --say "Más despacio, por favor." --pick 1 --say "Sí, por favor." --say "Ella quiere fruta." --pick 2 --pick 1 --pick 1 --pick 3 --pick 1 --say "Voy a comer pan." --pick 1 --say "Ayer comí fruta." --say "¿Dónde está el río?" --pick 2 --say "Quiero madera." --pick 3 --say "No." --pick 1 --say "No, gracias." --pick 2 --pick 3 --pick 1 --pick 2 --say "Otra vez, por favor." --pick 2 --say "Sí." --say "Tengo una fruta." --say "¿Qué es esto?" --pick 2 --say "Quiero agua, por favor." \
  --screenshot /tmp/playtest1-thanks.png --frames 60
```

**The build folder.** `scripts/playtest-build.sh <a new folder outside the repository>` publishes the game for linux-x64 as a self-contained
folder (a tester needs no .NET install), copies the bundle with its speech WAVs, the pinned Vosk model with its manifest (copied from
`VOCATERRA_MODELS_DIR`, after its checksums are checked), the tester's README (`scripts/playtest/tester-readme.md`) and the launcher
(`scripts/playtest/playtest.sh`, which runs `--playtest` with the folder's own models). Then it checks the folder: no sherpa-onnx or ONNX Runtime native
library, no espeak-ng, no sherpa model file, the Vosk model's checksums, every speech file the bundle's manifest lists (the check walks the folders); the build fails if one check does not pass. The
sherpa-onnx native library is left out by the build property `VocaterraPlaytest=true` (in `src/Vocaterra.Game/Vocaterra.Game.csproj`:
the native assets of the linux-x64 runtime pack are excluded, and the ONNX Runtime Windows DLLs that its props add are dropped from the publish).
The managed `sherpa-onnx.dll` stays, because the core's assemblies reference it. The playtest loads Vosk only; the NPCs' voices are the
pre-rendered WAVs (S9), and a developer build keeps Kokoro for `--speak`.

```bash
scripts/playtest-build.sh /tmp/playtest-build      # a new folder; ends with "all checks passed"
cd /tmp/playtest-build && ./playtest.sh             # the tester's run (./playtest.sh --greyscale for arm B)
bash scripts/playtest-report.sh ~/.local/share/vocaterra/sessions/<folder> [<folder> ...]   # from the repository root
```

The report (`scripts/playtest-report.sh`) prints each folder's profile and arm as well as the gates' evidence. The folder is private: the
speech WAVs are model output and the native code it runs is under core D2 (see the licence section).

## The world preview (B02)

`tools/Vocaterra.WorldPreview` draws the procedural world's climate, height and biome model as PNG pictures, so the maps can be looked at before any tuning (it only reads; README in its folder):
`scripts/limited.sh dotnet run --project tools/Vocaterra.WorldPreview -- maps --seed 42 --size 16384 --scale 16 --out artifacts/b-series/b02` writes a biome map (with a legend text file of each biome's colour and share) and a hill-shaded height map; `biomes`, `heights` and `slice` (a vertical section between two points, y -64 to 320) draw one each.
