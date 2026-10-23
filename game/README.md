# Vocaterra

Vocaterra is a Terraria-like game in which learning Spanish is one of the main pillars: exploring, progression and looting, with speech and
dynamic conversation in Spanish. The game design is in [docs/SPECIFICATION.md](docs/SPECIFICATION.md).

The Spanish language processing is the language core **Vocaterra.NLP**, the sibling folder [`nlp/`](../nlp/README.md) of this repository
(Apache-2.0). It takes a learner's utterance to a verdict (what they meant, how well they said it), feedback, a conversation step and the
agent's response text. The game references the core's projects directly and copies nothing of its source.
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) says what the game takes from the core and what the game owns.

The prototype is the 3D scenario in the next section. The engine for it is MonoGame (decision D-game-1 in [docs/DECISIONS.md](docs/DECISIONS.md);
the final engine is open), and all engine code lives in `src/Vocaterra.MonoGame`. The console host stays as the cheapest way to exercise the
core's calls and the content. The 2D slice (P1 to P7) was retired at S4; its code is tagged `2d-slice-p7` (the last commit that holds it).

## The prototype scenario

The prototype is the Minecraft-inspired first-person voxel slice in [docs/PROTOTYPE_BRIEF.md](docs/PROTOTYPE_BRIEF.md) (the owner's Prototype
Design Brief, v1.2); its plan, milestone by milestone, is [docs/SCENARIO_PLAN.md](docs/SCENARIO_PLAN.md). The scenario takes 10 to 15 minutes beside the
guide's lesson (S17):

- **The opening (S17).** The player wakes alone at their own camp on the cliff. English tips teach looking, walking, jumping, a note at the tent that says the
  guide's camp is down the trail, collecting and the inventory, one at a time. The objective area says `Go down the trail and find the guide`.
- **The guide's lesson (S17).** Luna, the guide, greets the player who walks up to her and teaches six short units (hello, yes and no, what is this, please and
  thank you, getting unstuck, what the camp needs), about eight minutes, with the English thinning as phrases are used again.
- **Forest camp.** Gather wood and berries from the trees and bushes, then craft a torch at the workbench.
- **The stall.** The shopkeeper asks `¿Qué necesitas?`; the player answers `Quiero agua, por favor.` The shopkeeper offers `¿Quieres pan?`, answered
  with `Sí, por favor.` or `No, gracias.`
- **The villager and the river.** The player asks `¿Dónde está el río?`, follows the directions to the river and fills a bottle.
- **The transfer moment.** After the objective, one phrase is revisited without the original prompt, or with a different NPC or context.
- **The ending (S17).** The complete checklist sends the player back up the trail with a spare torch; a torch on the rim of the fire ring at their own camp
  lights it, the guide's closing line is said and shown in Spanish and English, and the post-check is offered (playtest).

These are the brief's five target phrases. Nothing in the scenario ends by a language mistake: no death, no timers, no language gate. The run
lines for the 3D world are in "The 3D world" below; they grow as S5 onward land.

## Layout

This folder, `game/`, is one half of the repository; the shared solution, build settings, package versions and scripts are at the root
(see the [root README](../README.md)).

```
Directory.Build.props            the game's additions to the shared build settings (the Linux-only native trim)
src/Vocaterra.Simulation/        the 3D voxel world and the player body: the scene, gathering, crafting, a fixed 60 Hz tick (no packages, no engine)
src/Vocaterra.Game/              the 3D session, the guide's turns, the word progress, the camp objective and the facts about the world (no engine)
src/Vocaterra.MonoGame/          the MonoGame DesktopGL front-end: the first-person window, drawing, input, the HUD (the only engine code)
src/Vocaterra.Host/              the console host: loads the bundle, evaluates lines, runs the text loop
tests/                           one test project per source project (Simulation, Game, MonoGame, Host)
content/game-v1/                 the game's content bundle (see below)
assets/                          CC0 art, fonts and sounds, each listed in assets/ATTRIBUTION.md (sounds: assets/audio/SOURCES.md)
docs/                            SPECIFICATION (design), ARCHITECTURE (boundary), DECISIONS
IMPLEMENTATION_PLAN.md           the milestones and the findings log
../scripts/content.sh            validate, hash and check a bundle with the core's content tool
../scripts/merge-gate.sh         the orchestrator's merge gate (the whole repository)
../.claude/agents/orchestrator.md   the orchestrator's role file (how the repository is developed)
```

## Clone, build and run

All commands below run from the repository root (the parent of this folder).

```bash
git clone https://github.com/james-d12/Vocaterra.git
cd Vocaterra
dotnet build Vocaterra.sln -warnaserror     # the core (nlp/) and the game (game/) in one solution
dotnet test Vocaterra.sln
```

The .NET SDK is pinned in `global.json` (10.0.402, `latestPatch`). A user-local install is enough. The core's projects are built from the
same commit; there is no pack step and no local feed.

The speech models are not in the repository. Without them five model-gated tests are skipped, and the speech runs (the mic check, `--speak`,
`--playtest`) cannot hear. Fetch the pinned Vosk model and the Kokoro model once into a folder outside the repository, then point
`VOCATERRA_MODELS_DIR` at it for the tests and the runs:

```bash
export VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
nlp/scripts/fetch-models.sh vosk-model-small-es-0.42 kokoro-multi-lang-v1_0   # needs curl, python3, sha256sum, unzip or tar with bzip2
```

On a Linux x64 build host the build copies only the linux-x64 native libraries (about 400 MB less per build); the commands and the output
paths are the same. `dotnet build -p:VocaterraAllRuntimes=true` copies every platform's natives again (for a release build). See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

Run the host (the default bundle is `content/game-v1` in the current directory, else the game's bundle found above the executable; `--content <dir>`
names another):

```bash
# one line, evaluated once and printed (the form the tests and scripts use)
dotnet run --project game/src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need
# a number mistake (noun.number), and the stall's other prompt: stall-offer, then the villager: villager-open
dotnet run --project game/src/Vocaterra.Host -- --transcript "quiero dos agua por favor" --prompt stall-need
dotnet run --project game/src/Vocaterra.Host -- --transcript "¿dónde está el río?" --prompt villager-open

# the text loop: shows each prompt (and the agent's line, when the prompt has one), then reads a line;
# /prompt <id> switches prompt, /quit leaves
dotnet run --project game/src/Vocaterra.Host
```

The host makes these calls of the core's API, in order, on the packages (the details are in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)): `ContentLoader.Load` for the bundle; `PipelineServices` and `UtterancePipeline.Evaluate`
for each typed line; `ConversationContext.Create` for the utterance's context and `ConversationContext.Next` after it, which applies the core's
`RepeatCounts.Next` to the error tags. The host owns the world seed, the stored time (fixed for `--transcript`, the clock in the loop),
the learner model and the repeat counts.

## The 3D world (S2 to S4)

The prototype's world is the first-person voxel scene: it loads the world from the bundle (`content/game-v1/game/scene.json`, S13: generator
parameters and named sites, not blocks, a 384 x 384 x 128 block valley with high snow-capped mountains along the north and the west, a waterfall off
the northern cliffs, a river through a dense forest to a lake, the guide's camp in a clearing, the village on the meadow across the river with the stall,
three timber-framed houses with stepped roofs and chimneys, a stone tower, a fenced crop field, lanterns on posts and the signpost with its three boards
`Tienda`, `Río` and `Montaña` (S15), and the player's own camp on a cliff ledge at the top of a switchback trail) at `--seed` (default 42; the
seed changes the land between the sites and the forest, never a site), and shows it from the player's eyes. A new game starts at the player's camp
(facing south-east, a canvas tent, a chest and a cold fire ring beside it) with the beacon on the guide's camp: the first thing to do is the walk down the trail.
`--quality low|medium|high` sets how far the world is drawn and how much is drawn of the undergrowth, the occlusion and the clouds (see `src/Vocaterra.MonoGame/README.md`); the default, high, is for a real graphics card, and
**on a machine with software GL (this VM) pass `--quality low`**. S14 gave it its look: code-drawn pixel-art textures (a 16 x 16 atlas, deterministic, CC0 by construction), oaks, spruces and birches,
tall grass, ferns, reeds and flowers that nothing collides with and the block ray looks through, sun shading and ambient occlusion in the vertex colours, a blue sky with a square sun and flat clouds,
haze that fades the far mountains into the horizon, translucent water with a lighter shore, and a waterfall. The player wakes on the ledge looking down (pitch -10) over a valley whose forest, river, village and lake are in the
picture; the trees in front of the ledge are cleared (a sight cone), the banks and the village are meadow and the ledge's foreground holds only low flowers. (`--voxel`, which selected the 3D world until S4, is accepted and ignored.)

```bash
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42
```

**S13 demos** (the large world; `--quality low`, 1280 x 720; each prints the frame times and the mesh counts; `--teleport` and `--look` only set the
camera for a picture):

```bash
# from the player's camp (the spawn) looking south-east over the valley
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --look 128,-13 --screenshot /tmp/s13-camp.png --frames 3
# from the valley (the guide's clearing) looking north at the peaks, the cliffs and the waterfall
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --teleport 176,200 --look 25,8 --screenshot /tmp/s13-peaks.png --frames 3
# the village, from the road: the stall, the signpost, a house and the villager
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --teleport 272,224 --look 75,-4 --screenshot /tmp/s13-village.png --frames 3
# the village from its entrance, with the boards Tienda, Río and Montaña on the signpost, the stall's awning, the houses and the tower (S15)
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --teleport 266,223 --look 80,2 --screenshot /tmp/s15-village.png --frames 60
```

**S14 demos** (the look; `--quality low`, 1280 x 720; the default spawn look is yaw 135, pitch -10):

```bash
# the camp vista at the default spawn look (add --quality high for the whole valley and the far walls), then the sun and the clouds
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --screenshot /tmp/s14-camp-low.png --frames 60
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --look 314,36 --screenshot /tmp/s14-sun.png --frames 60
# the valley looking north at the peaks and the waterfall, the forest, the riverbank (reeds, sand, clear water) and the falls close up
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --teleport 176,200 --look 25,8 --screenshot /tmp/s14-valley-north.png --frames 60
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --teleport 196,206 --look 100,-3 --screenshot /tmp/s14-forest.png --frames 60
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --teleport 222,222 --look 80,-16 --screenshot /tmp/s14-riverbank.png --frames 60
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --teleport 258,136 --look 0,14 --screenshot /tmp/s14-waterfall.png --frames 60
# the riverbank in greyscale: the trees, the grass and the water are grey (their nouns are not known), the reeds and the flowers stay in colour
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --quality low --beacon none --greyscale --teleport 222,222 --look 80,-16 --screenshot /tmp/s14-greyscale.png --frames 60
```

`--greyscale` is the playtest A/B switch (D-game-6 as amended): a block whose noun is not known yet is drawn grey, a known one in full colour, and
a noun that becomes known during play gets its blocks back in colour with the toast `¡Nueva palabra: el agua!`. The world is in full colour
without the flag; the figures, the sky, the undergrowth, the snow and the UI are always in colour.

```bash
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --greyscale --look 120,-5 --screenshot /tmp/s4b-grey.png --frames 120
```

WASD or the arrows walk, Space jumps, the mouse looks (Esc lets the mouse go, a click takes it back). **Hold the left button** on a trunk
(`madera`, five for the camp) or a berry bush (`fruta`, three) to gather it, the right button places the selected item, `1`..`0` or the wheel
pick the hotbar slot, **I** the inventory, **C** the crafting panel, **E** the inventory or, with the crosshair on the workbench (`mesa`), the
crafting panel. The panel lists the recipes (the torch: `antorcha` from two `madera` at the workbench); a click on a row crafts it. **J** the
book, **F3** the debug overlay, **F5** the view over your own shoulder and back (S15: first person is where every run starts; `--third-person` starts
in the shoulder view), **F6** saves. **Aim the crosshair at the guide's figure**: the hint `E: talk` shows, and **E** (or **T** to type, or **V**
to talk: hold it, the clip goes when it is let go) opens the dialogue panel, which takes the keyboard and frees the mouse. **The panel has
speech buttons (S7)**: `[R] Repetir`, `[S] Más despacio`, `[H] Ayuda`, `[N] No sé` and `[W] Escribir` (keys or the mouse); hold **V** or **Space** to talk;
`Escribir` (W or Tab) opens the text box, where Enter sends and Tab or Esc go back to the buttons; Esc cancels a turn that is "Thinking…", then closes the
panel. After a spoken turn the line `Heard: «…» · Expected: «…»` stays under the conversation; when the NPC offers choices they are buttons too (keys 1 to 3).
A mic check (`Say "hola"`, a level meter, what was heard) opens an interactive run (`--no-mic-check` skips it, `--mic-check` forces it). Her answers point the beacon at the named noun's nearest block (the arrow at the screen edge when it is off
screen); "¿qué es esto?" names the block under the crosshair. **The shopkeeper and the villager are talked to the same way** (S6): aim at any figure, `E: talk`; the panel's title is the NPC's name.

**The camp objective (S4).** The checklist top right, `Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1`, is completed by the
game's events and the inventory, never by a language score: five trunks, three berries, the torch placed at the fire ring, and the water: the shopkeeper hands over an empty bottle (`la botella`), and holding the left button on the river with it in the
inventory fills it (half a second; without a bottle a toast says `You need a bottle.` and nothing is blocked). Each step that completes shows a toast. The first time an item enters the inventory (a pickup, a craft)
its word card shows, `la madera`, `la fruta`, `la antorcha`, with an empty audio slot for S9; the word counts as heard and read, never as a quiz.

To check it without playing, script it and save a screenshot (needs a display; every line also prints what its step did):

```bash
# the camp (S4): gather, craft at the workbench, place the torch on the fire ring's rim, look at it: the checklist reads 3/4
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --walk-to 176,186 --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 --walk-to 176,184 --look 180,-15 --screenshot /tmp/s4-camp.png
# the guide answers "where is the river?": the console prints the verdict, her line and the arrow; the PNG shows the panel and the arrow
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --look 120,-5 --say "¿dónde está el río?" --screenshot /tmp/s3-river.png --frames 300
# the village: walk to the square's north side and look at the stall: the shopkeeper behind the counter facing the player, with his label
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --beacon none --teleport 282,211 --look 90,-8 --screenshot /tmp/s3-stall.png --frames 150
# the signpost, from the road on its west side (S15): the boards Tienda, Río and Montaña with their arrows, in the letters of the block texture
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --beacon none --teleport 272,222 --look 90,2 --screenshot /tmp/s3-sign.png --frames 150
# the camp from the player's eyes at spawn: the guide's figure and label, the beacon on the river (an arrow at the edge when it is off screen)
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --look 120,-5 --beacon river --screenshot /tmp/s2-camp.png --frames 240
# S17, the whole route in one run, from the player's own camp: the walk down the trail to the guide, her lesson (`--lesson-run` does every step with typed
# answers and picks the fruit she offers from a bush), the camp (wood, fruit, the torch at her fire ring), the stall (the shopkeeper's "¿Qué necesitas?" is a
# core host turn, the bottle comes with his answer), the villager (the beacon on the river), the river (hold the button on the water: the bottle fills and the
# checklist is complete: a spare torch is in the bag and the beacon points up the trail), the guide's transfer offer "¿Quieres agua?", and the climb back to the
# player's camp, where a torch on the rim of the cold fire ring lights it: the closing card says "Session complete". The console prints every lesson step and its
# outcome, each turn, each game event and the objective's steps; the PNGs show the lesson's end, the stall, the river and the lit fire. One step is spoken
# (S22): after the lesson the route greets the guide with `--wav` and the bundle's recorded "Hola." (the Kokoro line), so the step goes through Vosk (the
# console prints "heard "hola"" and the greet intent). It needs the Vosk model in VOCATERRA_MODELS_DIR; the other steps are typed.
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute \
  --lesson-run first-words --shot /tmp/s17-lesson.png \
  --talk guide --wav game/content/game-v1/speech/phrase.hola.wav \
  --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --shot /tmp/s17-stall.png --say "sí, por favor" \
  --talk villager --say "¿dónde está el río?" \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 --shot /tmp/s17-river.png \
  --talk guide --say "sí, por favor" \
  --walk-to 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 \
  --screenshot /tmp/s17-fire.png --frames 90
# the lesson alone, with its steps printed (the player is walked down the trail to the guide first); the book's phrases page at the end shows what was learnt
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute --lesson-run first-words --journal --journal-phrases --screenshot /tmp/s17-journal.png --frames 30
# the opening: the objective line and the first tip at the spawn (a scripted run draws no tips unless --tips is given)
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute --tips --screenshot /tmp/s17-opening.png --frames 90
dotnet run --project game/src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need
```

The steps `--look yaw,pitch` (degrees, yaw 0 north and 90 east), `--walk-to x,z`, `--interact x,y,z`, `--place item@x,y,z`, `--select n` and
`--craft item` run in the order given, and so do the talk steps (S6): `--talk <npc>` (`guide`, `shopkeeper`, `villager`: walk up to the figure, stop about
two blocks away facing it, open the panel), `--say <line>` (type and send a line to the NPC the panel is open on and wait for the reply; with no
panel open it first talks to the guide, as in S3), `--leave` (close the panel; walking on or talking to someone else closes it too) and `--shot
<png>` (save the screen at that point and go on); `--beacon <point>` stands the beacon column on a named point of the scene. Details are in `src/Vocaterra.MonoGame/README.md`.
Spoken input (push-to-talk) works in the interactive game. The scripted spoken path (S7): `--speak "<line>"` synthesizes the line with Kokoro and
says it to the NPC the panel is open on as if spoken (Vosk hears it, in the core's Constrained mode at the stall's and the guide's offer prompts), `--wav
<file>` does the same from a recording, and `--mic-check --screenshot <png>` runs the mic check on a synthesized "hola" when there is no microphone:

```bash
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --talk shopkeeper --speak "quiero agua" --screenshot /tmp/s7-stall.png
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --talk shopkeeper --speak "agua agua mesa" --speak "agua agua mesa" --speak "agua agua mesa" --screenshot /tmp/s7-choices.png
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mic-check --screenshot /tmp/s7-mic.png
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

```bash
# the stall's key words, then the question with the card (the two PNGs), a use, ten minutes later the guide asks for it, the book's phrases page
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --typed-credit full --no-speech --talk shopkeeper --shot /tmp/s8-intro.png --wait 3 --shot /tmp/s8-stall.png \
  --say "quiero agua, por favor" --skip 660 --talk guide --say "quiero agua, por favor" --journal-phrases --screenshot /tmp/s8-journal.png --frames 60
```

**The village, the figures and the HUD (S15).** The valley holds the village the brief's references show. The houses are timber-framed (posts at the corners, a beam
under the eaves, plaster between) under roofs that step in a block a layer, with a cobble chimney at a corner the position picks; a stone tower with a door, four
window slits, a timber band and a flagpole stands behind the square; a crop field of tilled soil and rows of wheat (a seeded gap here and there) sits inside a
fence with a gate; fence lines run along the square; and six lanterns on posts stand by the doors, the square and the road. The signpost carries three boards
(`Tienda`, `Río`, `Montaña`), each an arrow and the word in a 3 x 5 pixel font on the plank of a block texture, with the accent and the tilde drawn; the words are on the faces that read left to right from the south and the west (the road comes in from the west), and the other sides are plain plank. The stall has
a red and white awning that reaches over the front, bread, apples and bottles on the counter and on two crates behind it (which of the three each cell holds comes
from the seed), and thin fence posts at the front, so the player looks past them at the shopkeeper. The guide's workbench stands under a timber pergola with a torch
on one post and a lantern hanging over the bench, with a chest and crates beside it; the player's camp has a canvas tent, a chest and a crate beside the cold
fire ring (which the S17 ending lights as before). The lanterns tint the faces within seven blocks warm (half as strongly by day, fully with `--dusk`).
Everything is placed by `SceneBuilder` from `scene.json` and the seed's `WorldRng` (the stall's goods, the field's gaps), so it is part of the scene digest and
a test pins the village's and the camps' structure.

**The figures.** The guide Luna (green jacket, yellow scarf, long hair, a pack), the shopkeeper (a white shirt with rolled sleeves, a leather apron, a beard and a
baker's hat), the villager (a blue tunic and a straw hat) and the player (a blue shirt and a backpack with a bedroll) are boxes of the atlas's cloth, skin and
hair pictures with a drawn face on the front of the head, shaded by the same sun table as the blocks. They idle (a breath, swaying arms, a head that looks about);
**when you talk to one it turns to you** (`FigureFacing`: the shorter way round, at most 80 angle steps a tick) and nods and waves its right arm, and turns back to
where the scene put it when the talk is over. **F5** shows you from behind your right shoulder (the camera stands three blocks back and never inside a wall), with
the figure walking when you walk; first person has the arm instead.

**The arm.** The first-person arm is a blocky forearm in the player's blue sleeve with a hand in the player's skin, drawn in 3D with the figures' cloth and skin
pictures, no outline, tinted by the light where you stand (dimmer under cover, warm near a lantern or with a torch in hand). It holds what the hotbar has selected:
a log for wood, a torch with its flame for a torch, a bottle, or a flat berry or loaf; it sways when you walk, chops while you gather and dips once when you place.

**The HUD.** The hotbar is a row of dark rounded slots with the item's pixel-art icon and the count; the selected one has a bright frame. At the workbench a panel at the
right shows the recipe as icons: what it makes at the top and, under the arrow, each ingredient with `have/need` in green or red. The dialogue panel is a dark
rounded panel with the speaker's name in a warm colour, and the speaker's latest line also hangs in a bubble over the figure's head when it is on the screen.

**Save, sessions and resume (S10).** `--save <file>` writes the game when the run ends (on quit, or at the end of a script), and **F6** writes it in an
interactive run (S15: F5 became the view key) (to the `--save` file, or `vocaterra.save.json` in the working folder); a turn still thinking means "not now", and a quit waits up to five
seconds for it. `--load <file>` starts from a save: the same world (the trees and the scene come from the seed, and the save holds only the blocks the
game changed), the player where they stood, the hotbar, the inventory, the objective, every word and phrase count, each NPC's conversation state, and the
stored time. A load is the **next play session** (the journal's header and the debug overlay read `Sesión 2`), and a new session lifts the spacing gaps
of D-game-6, so a phrase or word whose ten-minute gap had not passed becomes creditable again. **Time rule:** a load adds nothing to the stored time (it
continues from the save: start, plus the ticks, plus the skipped seconds); the next-day recall check moves the stored time with `--skip <seconds>` after the
load when a scenario needs a day to pass. A load is refused, with the reason, when `--seed` names another world or when the content bundle is not the one
the save was made from; a save whose restored world does not match its digest is refused too. The file is canonical JSON (sorted keys, no whitespace):
the same state gives the same bytes, and a load followed by a save gives the same file except for the session number. Not saved: the open dialogue,
the review question, the word cards not yet shown, the replay records of the conversations (the session log is S11's).

```bash
# S6's whole route, then the game saved at its end: the console prints the file, the session, the tick, the stored time and the world digest
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 \
  --walk-to 176,186 --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" \
  --talk villager --say "¿dónde está el río?" \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 \
  --talk guide --say "sí, por favor" --talk villager --say "quiero pan, por favor" \
  --screenshot /tmp/s10-end.png --frames 60 --save /tmp/s10.save
# the same world in the next session: "load:" prints the saved and the next session and the digest check; the journal's phrase page reads "Sesión 2"
dotnet run --project game/src/Vocaterra.MonoGame -- --load /tmp/s10.save --journal-phrases --screenshot /tmp/s10-load.png
# refused: the save is for world seed 42 (the message says so and nothing starts)
dotnet run --project game/src/Vocaterra.MonoGame -- --load /tmp/s10.save --seed 7
```

**Checks, the session log and the report (S11).** `--check pre|post|recall` runs the check battery outside the world, on its own screen (the
items are `content/game-v1/game/checks.json`): ten items, six that play a Spanish line and offer three meanings (keys **1**, **2**, **3**; **R**
plays it again), and four that ask the learner to say a phrase in Spanish for an English situation (hold **V** to speak, **W** to type and
**Enter** to send, **N** for "no sé"). The order and the options' order come from the world seed and the kind, so a pre-check and a post-check
hold the same items in other orders. **Esc** ends the battery (the rest is skipped). `pre` is followed by the world; `post` and `recall` end the
run after the battery. A check never changes the learner model, the phrase progress, the conversations or the world. The answers go to the
session log: **`--session-dir <dir>`** names the folder (default: a new folder named by the local date and time and the session number, under
`~/.local/share/vocaterra/sessions/`), which holds `session.jsonl` (one JSON object per line: `session_start`, `turn`, `phrase`, `voice`, `world`,
`check_start`, `check_result`, `check_end`, `session_end`) and the core's `replay.jsonl`. A folder that already has a log is appended to. Scripted:
`--pick <n>` answers a comprehension item, `--say` and `--speak` answer a production item, `--shot` saves the screen where it stands; a scripted run
prints the item order and the right option of each item on the console.

```bash
# the pre-check, scripted (the console prints the order: "check order: ..."), a screenshot of the first comprehension item, then the world
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --check pre --session-dir /tmp/sess1 --wait 2 --shot /tmp/s11-check.png \
  --pick 3 --pick 2 --pick 2 --say "¿Dónde está el río?" --pick 1 --say "No, gracias." --pick 3 --speak "Quiero agua, por favor." \
  --say "Sí, por favor." --pick 3 --screenshot /tmp/s11-world.png --frames 60 --save /tmp/s11.save
# the S6 route into the same folder (a second run in the folder: another session_start and session_end)
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --session-dir /tmp/sess1 --talk shopkeeper --say "quiero agua, por favor" ...
# the recall check on the saved game, in the next play session (the probe prints "check order:" first; the answers follow it)
dotnet run --project game/src/Vocaterra.MonoGame -- --load /tmp/s11.save --check recall --session-dir /tmp/sess2 --wait 2 --shot /tmp/s11-recall.png \
  --pick 1 --pick 1 --pick 1 --say "No, gracias." --pick 1 --say "Sí, por favor." --say "necesito agua" --pick 3 --say "no sé" --pick 1 \
  --screenshot /tmp/s11-recall-end.png --frames 30
# the report: per folder and in total, the completion, retries, support, ASR latency, the checks by item kind, transfer, and the gates' evidence
bash scripts/playtest-report.sh /tmp/sess1 /tmp/sess2
```

The report (`scripts/playtest-report.sh`, bash and Python 3's standard library, run with `python3 -I`) reads only the session folders. Its gate lines
(Playable, Learning signal, Transfer, Enjoyable) are evidence, never a verdict on learning; Enjoyable needs observer notes.

## The playtest (S12)

`--playtest` is the profile for the playtests (the facilitator's script is [docs/PLAYTEST.md](docs/PLAYTEST.md)): no debug overlay (F3 is off),
the mic check on, the session log in its own folder under `~/.local/share/vocaterra/sessions/` (or `--session-dir`), the pre-check before the
world (a fresh start) or the recall check (with `--load`), and `--greyscale` only when given (the A/B arm B; arm A is the default). Nothing in
the profile blocks progress, and Esc always works. The run ends like this: the **post-check** starts by itself once the camp is complete and the
guide has made her transfer offer, and the tester has closed her panel (or at once with **F10**, which the screen labels for the facilitator);
then the game saves to `playtest.save` in the session folder and shows the thank-you screen until Esc.

```bash
# a playtest in a checkout (the speech models in VOCATERRA_MODELS_DIR; the pre-check first, then the world, then the post-check)
dotnet run --project game/src/Vocaterra.MonoGame -- --playtest
dotnet run --project game/src/Vocaterra.MonoGame -- --playtest --greyscale           # arm B of the A/B
# the next-day recall of a playtest's save: the recall check, then the thank-you; nothing is saved
dotnet run --project game/src/Vocaterra.MonoGame -- --playtest --load ~/.local/share/vocaterra/sessions/<folder>/playtest.save
# the same run, scripted (a screenshot needs a display; the pre-check's answers are --pick and --say; the post-check's answers come after the
# transfer's --leave and the climb to the player's own fire, in the order the console prints as "check order:" (both lists below are for seed 42);
# see the S11 lines above for the shape of a check's answers)
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --playtest --seed 42 --session-dir /tmp/playtest1 \
  --say "No, gracias." --pick 2 --pick 1 --say "Más despacio." --say "Sí." --say "No." --say "Quiero agua, por favor." --say "¿Qué es esto?" --pick 3 --pick 3 --pick 3 --say "Sí, por favor." --pick 2 --pick 2 --pick 3 --pick 3 --pick 2 --say "Otra vez." --say "¿Dónde está el río?" --pick 3 \
  --lesson-run first-words --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" --talk villager --say "¿dónde está el río?" \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 --talk guide --say "sí, por favor" --leave \
  --walk-to 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 \
  --pick 2 --say "Más despacio." --say "¿Qué es esto?" --say "Sí." --say "Otra vez." --pick 1 --say "No." --pick 3 --say "No, gracias." --pick 1 --pick 1 --pick 2 --pick 2 --say "Quiero agua, por favor." --pick 2 --pick 1 --say "¿Dónde está el río?" --say "Sí, por favor." --pick 2 --pick 1 \
  --screenshot /tmp/playtest1-thanks.png --frames 60
```

**The build folder.** `scripts/playtest-build.sh <a new folder outside the repository>` publishes the game for linux-x64 as a self-contained
folder (a tester needs no .NET install), copies the bundle with its speech WAVs, the pinned Vosk model with its manifest (copied from
`VOCATERRA_MODELS_DIR`, after its checksums are checked), the tester's README (`playtest/README-tester.md`) and the launcher
(`playtest/playtest.sh`, which runs `--playtest` with the folder's own models). Then it checks the folder: no sherpa-onnx or ONNX Runtime native
library, no espeak-ng, no sherpa model file, the Vosk model's checksums, 226 speech files; the build fails if one check does not pass. The
sherpa-onnx native library is left out by the build property `VocaterraPlaytest=true` (in `src/Vocaterra.MonoGame/Vocaterra.MonoGame.csproj`:
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

## Content

`content/game-v1` is the game's bundle, under the bundle id `game-v1`. **The game owns it** (decision D-game-4, decided 2026-10-21: the bundle
diverges from the core's sample and is not re-synced). The core's sample bundle (`content/v1` in the core, a blacksmith with spells and items)
was the starting point: the generic intents (`confirm`, `deny`, `farewell`, `thank`), their dialogue pools and the first feedback pools were
copied from it, reworded where they failed the game's A1 check, and stay credited to the sample (Apache-2.0) in `ATTRIBUTION.md`. Everything
added since is the game's own, hand-authored: the guide (agent `guide`, intents `ask_directions`, `ask_what` and `ask_help`, prompt
`guide-open`, the reply pools and the host-line pools the game renders itself), `answer-sets/places-basic.json` and `lexicon/world.json`, and
since S5 the prototype's stall and village: the agents `shopkeeper` and `villager`, the intents `request_item` and `dont_know`, the answer set
`supplies`, the prompts `stall-need`, `stall-offer`, `villager-open` and `guide-offer`. The blacksmith and the spells left the bundle in S5 (the
core's sample keeps them). **The phrase sheet [content/PHRASES.md](content/PHRASES.md) is the source of truth for the Spanish the learner is
taught** (decision D-game-10): every row is checked against the bundle by `tests/Vocaterra.Host.Tests/PhraseSheetTests.cs`. A milestone that
wants something else from the sample copies only that piece and records it in `provenance.json`. The bundle has its own hash, which the loader
checks against `manifest.json`. After any edit, rewrite the manifest:

```bash
scripts/content.sh validate                                  # every file's hash and the cross-references
scripts/content.sh hash game/content/game-v1 --bundle-id game-v1  # rewrite manifest.json (keep the bundle id once it is set)
scripts/content.sh cefr-check game/content/game-v1 --level a1     # and --level a2: the lemmas the templates use that the level list lacks
scripts/content.sh lint game/content/game-v1 --strict --level a1 --host-pools 'guide.*'   # 0 errors, 0 warnings (the guide's host-voiced pools are named)
scripts/content.sh grammar game/content/game-v1 --strings game/content/grammar/game-v1-strings.jsonl --strings-only --offline   # the grammar check from the committed cache, no server
```

**The grammar check (S23, core M30, D10).** The review chain is lint, `cefr-check`, grammar, then the Opus pass (D-game-12). The grammar step
sends every Spanish string the player hears or says to a LanguageTool server on this machine (never the public API) and reads the answers back
through a committed cache. It covers the bundle's dialogue, prompts, answer sets and intents, the phrase sheet (the phrases, the accepted variants
and the learner-error examples, with their tags), the Spanish sides of `game/english.json` and `game/checks.json`, and the speech index. The
strings are written by the host from those sources, so the list is always the game's own:

```bash
dotnet run --project game/src/Vocaterra.Host -- --content game/content/game-v1 --grammar-strings game/content/grammar/game-v1-strings.jsonl
```

The committed files are `game/content/grammar/game-v1-strings.jsonl` (the strings), `game-v1-cache.json` (one result per string, keyed by the
LanguageTool version, the settings and the text) and `game-v1-waivers.json` (each accepted match, by rule and string id, with its reason; a
waiver with no reason is refused). Findings are warnings (agreement, spelling, grammar), info (style, and the learner-error oracle's facts) or
errors (a string with no cached result). The English support text is not checked (the owner's decision).

**Refreshing the cache after a content change.** Run these from the repository root:

1. Write the strings again (the command above). The strings file is checked by a test, so a stale file fails `dotnet test`.
2. Start the server: `scripts/languagetool.sh start` (it refuses below 4 GB free; `scripts/languagetool.sh status` shows it).
3. Check the new strings and prune the entries no string uses any more:
   `scripts/content.sh grammar game/content/game-v1 --strings game/content/grammar/game-v1-strings.jsonl --strings-only --prune`
4. Read every warning and every new info finding. Fix the content, or add a waiver with a reason to `game-v1-waivers.json`. Do not
   waive a true positive to get a clean run.
5. Stop the server (`scripts/languagetool.sh stop`), run the offline command above (it must exit 0), and commit the strings, the cache and the
   waivers with the content change. The gate runs the offline command only and never needs the server.

Since S4 the catalogue holds only the nouns of the 3D world (the ores, tools and furniture of the 2D slice are gone), and the recipes are the
workbench's torch alone; the core's sample keeps the rest.

The content gate from S5 on: `lint --strict` at A1 clean (an example expectation for every phrase the learner says, in `intents/intents.json`),
`cefr-check` clean at A1 and A2, and the phrase-sheet test green. `--host-pools 'guide.*'` tells the lint that the game voices the guide's
templated world lines itself (they have no agent action); without it those pools are reported as unreachable.

The script runs the core's content tool (`nlp/tools/Vocaterra.Nlp.ContentBuilder`, from source; see
[scripts/README.md](../scripts/README.md)). `ATTRIBUTION.md` and `provenance.json` describe this bundle; the Wiktionary lexicon in it is CC BY-SA
and keeps its notice.

## Continuous integration

The repository's `.github/workflows-pending/ci.yml` is parked: it needs the workflow scope on the token (core D1), and the owner deferred CI
(D-game-3 in [docs/DECISIONS.md](docs/DECISIONS.md)). Until then nothing runs in CI.

## The core's native libraries

The core depends on third-party native libraries, and one of them carries GPL-3.0 code (core D2); the game does not distribute a build that
includes them until D2 is settled in the core. The playtest folder (`scripts/playtest-build.sh`) leaves that native library out, and its check
says so; the owner decides whether handing the private folder to testers counts as distribution under D2.

## Licence

- **This repository:** MIT (see [LICENSE](LICENSE); decision D-game-2 in [docs/DECISIONS.md](docs/DECISIONS.md)).
- **Content bundle:** the Wiktionary lexicon in `content/game-v1` stays CC BY-SA 4.0; its notices are in
  [content/game-v1/ATTRIBUTION.md](content/game-v1/ATTRIBUTION.md).
- **Language core:** `nlp/` is Apache-2.0 (core D3, [nlp/LICENSE](../nlp/LICENSE)). **MonoGame** is Ms-PL.
- **Art, font and sound:** CC0 only (D-game-5), each file listed in [assets/ATTRIBUTION.md](assets/ATTRIBUTION.md).
- **Native libraries:** a build that includes the sherpa-onnx native libraries (GPL-3.0 code, core D2) is not distributed while D2 is open.

## Documentation

- [docs/SPECIFICATION.md](docs/SPECIFICATION.md): the game design
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how the game consumes the core, the host's calls, what the host owns and what the game owns
- [docs/DECISIONS.md](docs/DECISIONS.md): the game's open and decided questions
- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md): the milestones, the scene designs and the findings log
- [nlp/docs/API.md](../nlp/docs/API.md): the calls a host makes, in order
- [nlp/docs/adr/0009-core-and-game-boundary.md](../nlp/docs/adr/0009-core-and-game-boundary.md): what the core and the game each own
- [CLAUDE.md](../CLAUDE.md): working notes for the agents and the owner

## English guidance and the lesson runner (S16)

English is drawn, never spoken (D-game-13): an English line under the guide's Spanish in its own colour (`English: ...`), the bilingual camp checklist, plain-English
feedback after each turn, first-time tips and the controls on F1; the support ladder starts with English and fades per phrase (see
[src/Vocaterra.Game/README.md](src/Vocaterra.Game/README.md)). A data-driven lesson runner (`content/game-v1/game/lessons.json`) walks the learner through the
guide's lesson (L starts it, Enter continues, K skips a step, H helps; S16 shipped a two-step sample, which S17 replaced with the real lesson). A scripted run
through it prints each step and its outcome:

```bash
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute --talk guide --lesson first-words --lesson-next --lesson-next --say hola --screenshot /tmp/s16.png --frames 60
```

## The opening, the guide's lesson and the ending (S17)

- **The opening.** No quiz at the spawn. The first tips come one at a time, each at its delay (`delay` seconds in `english.json`): look at 0 s, walk at 9 s, jump at 18 s,
  the note pinned to the tent at 28 s (English, with the one Spanish word `camino`, saying the guide's camp is down the trail), collecting at 50 s; the first
  pickup then teaches the inventory. The objective area says `Go down the trail and find the guide` until the guide is met (the panel opened on her, or the first
  step of the checklist). The beacon points at her camp from the start.
- **The guide's lesson** (`first-words`, `game/lessons.json`). Luna, the guide, greets the player who comes within five blocks of her
  (`GuideApproach`; the same call as the E key, once per approach; Esc closes the panel and the lesson waits): the lesson starts by itself. Six units, 34 steps, about
  eight minutes at a learner's pace (estimated 4.8 minutes for a quick reader who answers in 4 s, 7.5 for a slow one, from the real WAV lengths, plus about a minute to walk down the trail): hello, yes and
  no, what is this, please and thank you (the guide offers fruit and the
  learner picks it from a bush: a `do` step with a beacon on the nearest berry bush), getting unstuck (`No sé`, `Otra vez, por favor`, `Más despacio, por favor`
  do what they say) and the camp's task, then a recap that opens the book's phrases page. While a lesson step takes a turn the guide's ordinary reply is not
  shown (the lesson speaks for her); a turn the core did not understand keeps her not-understood line and the English help.
- **The ending.** The complete checklist gives a spare torch, sets the beacon on the player's own camp and changes the objective line to `Go back up the trail and
  light your own fire`. A torch placed on the rim of the fire ring at `firering.player` lights it: the guide's closing line (`¡Muy bien! Ya tienes fuego.`) is
  said and shown with its English on a `Session complete` card, and in a playtest the post-check follows (the transfer offer first: she greets the player with
  it when they pass through her camp on the way back). The fire ring at the guide's camp keeps its role: the checklist's torch step.
- **English on the buttons.** `[R] Repetir (repeat)`, `[S] Más despacio (slower)`, `[H] Ayuda (help)`, `[N] No sé (I don't know)`, `[W] Escribir (type)` and the hold-to-talk
  hint carry their English until the button (or its phrase) has been used three times; the keys and buttons work the same without it.

```bash
# the whole lesson with typed answers (every step and its outcome is printed), then the book's phrases page
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute --lesson-run first-words --journal --journal-phrases --screenshot /tmp/s17-journal.png --frames 30
```


## The book (S20, D-game-16)

The journal is the learner's book: an open book drawn in code over the scene, in the panel style of S15. It opens with **J** (or with the book
item, which the learner starts with at the camp: select it on the hotbar and use it with the right button). Both call the same opening. It covers the
screen and takes the keyboard while it is open. Every title, tab, label and key hint is English; the Spanish is what the learner learnt (D-game-13).

- **Pages.** Five tabs, turned with **Tab** (Shift and Tab turns back), **Left** and **Right**, or a click on a tab: **Nouns**, **Verbs**, **Phrases**,
  **Lessons**, **What I said**. The lists scroll with the wheel, **Page Up** and **Page Down**; **Up** and **Down** move the selected row (see the buttons below),
  or scroll a line on a page with no button; the page's line count is
  shown at the bottom right. The book reads at 1280 x 720.
- **Nouns.** The catalogue words as they were (seen or `¿?`, the icon, heard, read, said, correct and the three pips), then every other noun the learner
  used (from the record's word rows), each with its article from the bundle's morphology (`el agua`, `la botella`), the forms used with their counts, and
  the last sentence (with its English when the sentence is a phrase of the sheet). A fixed expression (`por favor`) and an interjection (`gracias`) are not
  nouns and are not listed.
- **Verbs.** The infinitive with its English from `game/glosses.json` (`querer — to want`), then each form used with a plain-English label built by
  `VerbLabels` (`quiero — I want`, `¿quieres? — do you want?`, `está — he/she/it is`), its counts and its last sentence. A form used in a question is a
  form of its own (the record keeps the question flag). `to be` has its own forms (am, is, are, was, were); an irregular third person or past comes from
  the glosses file; a verb with no gloss shows its infinitive alone.
- **Phrases.** The phrases page of S8, unchanged in meaning: the phrases the learner has met first, each with its Spanish, its English, its help now
  (the card and audio lamps) and its counts.
- **Lessons.** Every lesson of the bundle in its order: its state (completed in session n, in progress with the units done, not started), its units with a
  tick each, the phrases it taught (S17's recap), the help used and, once completed, its English summary, stored in `game/lessons.json` beside the lesson
  (`summary`).
- **What I said.** `Learner.Recent(500)`, newest first: the line, said or typed, where it was said (the NPC's English name, the lesson and its unit, or a
  check), and the core's feedback in English (`EnglishText.FeedbackFor(InputEntry)`, the same lines as S16's).
- **Speaker buttons (S20b).** A row with a recording has a small speaker at its right edge: every seen catalogue noun on the Nouns page (its word card,
  `card.<noun>`, with the accent folded in the file name: `card.rio`), every phrase on the Phrases page and every phrase a lesson taught on the Lessons
  page. A click plays the normal recording and **Shift**+click the slow one. From the keyboard, **Up** and **Down** move a selection (a gold bar) among
  the rows with a speaker, the selected row stays on screen, **Enter** plays it and **Shift+Enter** plays its slow recording. The first such row is
  selected until the learner moves. An unmet noun (`¿?`) and a noun used outside the catalogue have no button, so nothing is given away; so do the verb
  forms. A phrase the learner only hears has no file of its own: its speaker plays the NPC line that speaks it (`¿Qué necesitas?` is the shopkeeper's
  `shop_need.1`, `Me llamo Luna.` is `guide.lesson.name.1`; the mapping is the phrase sheet's `audioLineIds`).
  The sound goes through the voice (the sfx and ambient ducks under it; `--mute` keeps it silent), and each play that is accepted is a mark in the sound
  log: `000123 mark    speak card.madera slow`. The rules are in `Frontend/BookRecordings.cs` (which recording a row plays and the button's
  geometry), `Frontend/BookSpeechControls.cs` (the click, Enter, the arrows and the scripted step) and `Frontend/BookSpeaker.cs`; tested in `BookSpeechTests`.

**Page models.** `src/Vocaterra.MonoGame/Frontend/BookPages.cs` (what each page lists, as `BookLine` values: a tone, a text, an aside, a right-hand text,
an icon, pips, lamps or a tick), `VerbLabels.cs` (the English table of a verb form: person, number, tense and mood), `Book.cs` (`BookModel`, the pages in
`BookTabs`, `BookGeometry` for the panel, the tabs and the lines at a size, `BookScroll`). They are engine-free and tested in
`BookPagesTests`, `VerbLabelsTests` and `BookModelTests`; the drawing is `src/Vocaterra.MonoGame/BookLayer.cs`.

**The record and the bundle (what the book reads).** The learner's record gains, per form, the question flag and the last use (`FormRow.Question`,
`FormRow.Last`; a save of version 2 keeps them). Lessons gain `summary` (optional) and the step ids (`LessonRecordRow.StepIds`, from which the book groups
the steps into units by their prefix: `u1-hello` is unit `u1`). `game/glosses.json` is new (the verbs' English, `VerbGlosses`; it is in the manifest and
`provenance.json`). `btnHold` is removed from `english.json` and from `EnglishText.UiKeys`. The book item is `ItemId.Book` (29), given by
`VoxelSession` at a new game; a loaded save puts its own slots back.

**Demo and flags.** The flags open the book for screenshots once the steps are done: `--book` (the nouns page, the same as `--journal`),
`--book-page nouns|verbs|phrases|lessons|said`, and `--journal-phrases` (the phrases page, as before). `--book-play <noun|phrase:id>` (and
`--book-play-slow`) is a scripted step: it opens the book on the target's page, selects its row and plays it as Enter does, so a script can play a
word or a phrase and take a screenshot in the same run. A target with no button yet (a noun not met) says so and plays nothing.

```bash
# S20b: after the S17 lesson route, play a noun from the book (normal) and a phrase (slow), with the sound log (the plays are the "speak" marks)
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --sound-log /tmp/s20b.log --lesson-run first-words --book-play fruta --shot /tmp/s20b-noun.png --book-play-slow phrase:pedir-agua --shot /tmp/s20b-phrase.png --screenshot /tmp/s20b-final.png --frames 30
# the Nouns and Phrases pages with their buttons (the speaker at each row's right edge; the selected row is gold)
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute --lesson-run first-words --book-page nouns --screenshot /tmp/s20b-nouns.png --frames 30
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute --lesson-run first-words --book-page phrases --screenshot /tmp/s20b-phrases.png --frames 30
```

```bash
# the five pages after the S17 lesson route (the lesson is walked first, then the book opens on the page named)
for page in nouns verbs phrases lessons said; do
  dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute --lesson-run first-words --book-page $page --screenshot /tmp/s20-$page.png --frames 30
done
```

The S17 route (`--lesson-run first-words` and the steps above it) still completes with `fire_lit` and `session_complete`.

## Sounds (S24)

The sound system (D-game-18): interaction, progress, movement and ambient sounds through a mixer with four categories, and no music. Every file is CC0 (Kenney's Impact Sounds, RPG Audio and
Interface Sounds, listed in [assets/audio/SOURCES.md](assets/audio/SOURCES.md)); the chimes, the push-to-talk beeps, the sparkle, the bottle and the fire's whoosh are
made in code at load from a fixed seed. The audio is under 1 MB (the budget is 15 MB for S24 and S25 together). Sounds react to events and never touch the simulation, a
turn, a verdict, a state hash or the replay.

**The mixer** (`src/Vocaterra.MonoGame/Frontend/SoundMixer.cs`, engine free; the engine output is `src/Vocaterra.MonoGame/SoundEffectOutput.cs`):
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
| The beds: wind on the cliff and the heights, the river and the lake, the waterfall (louder near it), the village murmur, the evening insects with `--dusk` | beds (ambient, looping; cross-fade over 1.5 s by position, panned to the source) | synthesized (loops, seamless) |

**Ambient beds and footsteps (S25).** The beds are `AmbientBeds` (`src/Vocaterra.MonoGame/Frontend/AmbientBeds.cs`): the weight of each bed is a
function of the player's place (the scene's sites, the river's path, the lake, the waterfall, the fire rings and the lamps), so the same route gives the same
weights at the same tick. Wind is full on the cliff (feet above y 68) and silent in the valley; birds sing in the lowland forest by day and the insects
take over at `--dusk`; the river follows its path, the waterfall is louder near its lip, and the village murmur is full by the houses and the stall. The
looping beds are made in code (`AmbientLoops.cs`): 10 seconds each, the tail faded into the head with an equal-power curve, so no click at the seam. The
scheduled birds and crackles (`AmbientSound.cs`) draw their times from the presentation generator, in ticks, and follow the bed's weight. Pan is the bearing
of the source from the way the player faces (simple stereo, not 3D). The footsteps (`Footsteps.cs`) read the block under the feet: every solid block kind
has a surface, and a step is every 2 blocks walked on the ground (about two a second at the walking speed); the take of a step never repeats twice in a
row. In water the steps splash. The beds and the scheduled sounds are silent while the microphone records and duck under a voice, like the other sounds.
The sound log writes a `mark` line when the place or its weights change (`bed at village: village=1.0`).

```bash
# the S17 route with the sound log: add --sound-log to the route above; the cues played are listed with their ticks, and the recording window of the --wav step is marked
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute --sound-log /tmp/route-sounds.log --lesson-run first-words ...
```
