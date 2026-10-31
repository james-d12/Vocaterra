# Vocaterra

Vocaterra is a Terraria-like game in which learning Spanish is one of the main pillars: exploring, progression and looting, with speech and
dynamic conversation in Spanish. The game design is in [docs/game/SPECIFICATION.md](SPECIFICATION.md).

The Spanish language processing is the language core **Vocaterra.NLP**, the `Vocaterra.Nlp.*` projects ([`docs/nlp/README.md`](../nlp/README.md)) of this repository
(Apache-2.0). It takes a learner's utterance to a verdict (what they meant, how well they said it), feedback, a conversation step and the
agent's response text. The game references the core's projects directly and copies nothing of its source.
[docs/game/ARCHITECTURE.md](ARCHITECTURE.md) says what the game takes from the core and what the game owns.

The prototype is the 3D scenario in the next section. The engine for it is MonoGame (decision D-game-1 in [docs/game/DECISIONS.md](DECISIONS.md);
the final engine is open), and all engine code lives in `src/Vocaterra.Game`. The console host stays as the cheapest way to exercise the
core's calls and the content. The 2D slice (P1 to P7) was retired at S4; its code is tagged `2d-slice-p7` (the last commit that holds it).

## The prototype scenario

The prototype is the Minecraft-inspired first-person voxel slice in [docs/game/PROTOTYPE_BRIEF.md](PROTOTYPE_BRIEF.md) (the owner's Prototype
Design Brief, v1.2); its plan, milestone by milestone, is [docs/game/SCENARIO_PLAN.md](SCENARIO_PLAN.md). The scenario takes 10 to 15 minutes beside the
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

**Later sessions.** A load is the next play session, and so is a sleep at the player's own fire (V12c: "The next session: sleep at the fire" below), and each later
session unlocks one lesson: sessions 2 and 3 at the villager and the stall (S26,
S27), and since phase A of [docs/game/VILLAGE_PLAN.md](VILLAGE_PLAN.md) (D-game-22) sessions 4 to 6 in a village grown inside its own clearing, with four
more people: the fisher Mar on the bridge's east landing (session 4, "El pueblo": places and the way to them), the baker Luz at the bakery (session 5,
"Números y pan": numbers, prices and bread for coins), the smith Domingo at the smithy and the farmer León at the wheat field (session 6, "Gente del
pueblo": names, jobs and someone else in three times). Coins, apples and an axe are items, and three orders pay in them ("The village: sessions 4 to 6"
below). The first session needs none of this: the four stand in the world and answer when talked to.

## Layout

The game is one half of the repository: the projects below, `content/game-v1`, `assets/` and these documents. The shared solution, build settings, package versions and scripts are at the root
(see the [root README](../../README.md)).

```
Directory.Build.props            (at the root) the shared build settings, with the game's Linux-only native trim
src/Vocaterra.Game.Core/         the game's engine-free systems: the voxel world and its 60 Hz tick, the session, the guide's turns, the
                                 language systems, the screens' models and their keys (no packages, no engine; docs/game/ARCHITECTURE.md)
src/Vocaterra.Game/              the MonoGame DesktopGL front-end: the window, the drawing, the devices, the UI's layers (the only engine code)
src/Vocaterra.Host/              the console host: loads the bundle, evaluates lines, runs the text loop
tests/                           one test project per source project (Game.Core, Game, Host), their folders mirroring the sources'
tools/Vocaterra.SpeechRender/    the speech prerender tool
content/game-v1/                 the game's content bundle (see below)
assets/                          CC0 art, fonts and sounds, each listed in assets/ATTRIBUTION.md (sounds: assets/audio/SOURCES.md)
docs/game/                       SPECIFICATION (design), ARCHITECTURE (boundary), DECISIONS, IMPLEMENTATION_PLAN (the milestones and the findings log)
scripts/content.sh               validate, hash and check a bundle with the core's content tool
scripts/merge-gate.sh            the orchestrator's merge gate (the whole repository)
.claude/agents/orchestrator.md   the orchestrator's role file (how the repository is developed)
```

## Clone, build and run

All commands below run from the repository root.

```bash
git clone https://github.com/james-d12/Vocaterra.git
cd Vocaterra
dotnet build Vocaterra.sln -warnaserror     # the core (Vocaterra.Nlp.*) and the game in one solution
dotnet test Vocaterra.sln
```

The .NET SDK is pinned in `global.json` (10.0.402, `latestPatch`). A user-local install is enough. The core's projects are built from the
same commit; there is no pack step and no local feed.

On the development machine the tests run through `scripts/test.sh` ([scripts/README.md](../../scripts/README.md)), which keeps them inside
`scripts/limited.sh`'s caps. `scripts/test.sh fast` (under a minute) is the check after every step: it leaves out the tests marked
`[Trait(TestSpeed.Trait, TestSpeed.Slow)]` (the world-generation classes, the model-gated tests, the playtest report's process tests, the
whole-route meshing and a few other long single tests, and the core's fuzz suite and CLI tests). `scripts/test.sh full` is the whole suite, as
the merge gate and CI run it, and `scripts/test.sh project Vocaterra.Game.Core.Tests` one project. The tests share one bundle load
(`TestGame.Bundle`) and one build of the route per seed (`RouteScene.Build`, `TestGame.Route`: each test gets a clone it may edit); a test of
the scene builder itself calls `SceneBuilder.Build`.

The speech models are not in the repository. Without them seventeen model-gated tests of the game are skipped (34 in the whole suite with the core's, against 3 of the core's with
the models; V09), and the speech runs (the mic check, `--speak`,
`--playtest`) cannot hear. Fetch the pinned Vosk model and the Kokoro model once into a folder outside the repository, then point
`VOCATERRA_MODELS_DIR` at it for the tests and the runs:

```bash
export VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
scripts/fetch-models.sh vosk-model-small-es-0.42 kokoro-multi-lang-v1_0   # needs curl, python3, sha256sum, unzip or tar with bzip2
```

On a Linux x64 build host the build copies only the linux-x64 native libraries (about 400 MB less per build); the commands and the output
paths are the same. `dotnet build -p:VocaterraAllRuntimes=true` copies every platform's natives again (for a release build). See
[docs/game/ARCHITECTURE.md](ARCHITECTURE.md).

Run the host (the default bundle is `content/game-v1` in the current directory, else the game's bundle found above the executable; `--content <dir>`
names another):

```bash
# one line, evaluated once and printed (the form the tests and scripts use)
dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need
# a number mistake (noun.number), and the stall's other prompt: stall-offer, then the villager: villager-open
dotnet run --project src/Vocaterra.Host -- --transcript "quiero dos agua por favor" --prompt stall-need
dotnet run --project src/Vocaterra.Host -- --transcript "¿dónde está el río?" --prompt villager-open

# the text loop: shows each prompt (and the agent's line, when the prompt has one), then reads a line;
# /prompt <id> switches prompt, /quit leaves
dotnet run --project src/Vocaterra.Host
```

The host makes these calls of the core's API, in order, on the packages (the details are in
[docs/game/ARCHITECTURE.md](ARCHITECTURE.md)): `ContentLoader.Load` for the bundle; `PipelineServices` and `UtterancePipeline.Evaluate`
for each typed line; `ConversationContext.Create` for the utterance's context and `ConversationContext.Next` after it, which applies the core's
`RepeatCounts.Next` to the error tags. The host owns the world seed, the stored time (fixed for `--transcript`, the clock in the loop),
the learner model and the repeat counts.

## The 3D world (S2 to S4)

The prototype's world is the first-person voxel scene: it loads the world from the bundle (`content/game-v1/game/scene.json`, S13: generator
parameters and named sites, not blocks, a 384 x 384 x 128 block valley with high snow-capped mountains along the north and the west, a waterfall off
the northern cliffs, a river through a dense forest to a lake, the guide's camp in a clearing, the village on the meadow across the river with the stall,
three timber-framed houses with stepped roofs and chimneys, a stone tower, a fenced crop field, lanterns on posts and the signpost with its three boards
`Tienda`, `Río` and `Montaña` (S15); since VILLAGE_PLAN V04 also a bakery with its oven, a smithy with a lit forge and an anvil, a well, the farmer's
apple crate and an orchard of three trees, and the fisher's crates and lamp on the bridge's east landing; and the player's own camp on a cliff ledge at the top of a switchback trail) at `--seed` (default 42; the
seed changes the land between the sites and the forest, never a site), and shows it from the player's eyes. A new game starts at the player's camp
(facing south-east, a canvas tent, a chest and a cold fire ring beside it) with the beacon on the guide's camp: the first thing to do is the walk down the trail.
`--quality low|medium|high` sets how far the world is drawn and how much is drawn of the undergrowth, the occlusion and the clouds (see `src/Vocaterra.Game/README.md`); the default, high, is for a real graphics card, and
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
# from the player's camp (the spawn) looking south-east over the valley
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
# the camp vista at the default spawn look (add --quality high for the whole valley and the far walls), then the sun and the clouds
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --screenshot /tmp/s14-camp-low.png --frames 60
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --look 314,36 --screenshot /tmp/s14-sun.png --frames 60
# the valley looking north at the peaks and the waterfall, the forest, the riverbank (reeds, sand, clear water) and the falls close up
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 176,200 --look 25,8 --screenshot /tmp/s14-valley-north.png --frames 60
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 196,206 --look 100,-3 --screenshot /tmp/s14-forest.png --frames 60
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 222,222 --look 80,-16 --screenshot /tmp/s14-riverbank.png --frames 60
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --teleport 258,136 --look 0,14 --screenshot /tmp/s14-waterfall.png --frames 60
# the riverbank in greyscale: the trees, the grass and the water are grey (their nouns are not known), the reeds and the flowers stay in colour
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --quality low --beacon none --greyscale --teleport 222,222 --look 80,-16 --screenshot /tmp/s14-greyscale.png --frames 60
```

`--greyscale` is the playtest A/B switch (D-game-6 as amended): a block whose noun is not known yet is drawn grey, a known one in full colour, and
a noun that becomes known during play gets its blocks back in colour with the toast `¡Nueva palabra: el agua!`. The world is in full colour
without the flag; the figures, the sky, the undergrowth, the snow and the UI are always in colour.

```bash
dotnet run --project src/Vocaterra.Game -- --seed 42 --greyscale --look 120,-5 --screenshot /tmp/s4b-grey.png --frames 120
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
screen); "¿qué es esto?" names the block under the crosshair. **The shopkeeper and the villager are talked to the same way** (S6), and so are the fisher, the baker, the smith and the farmer (VILLAGE_PLAN V10 to V12): aim at any figure, `E: talk`; the panel's title is the NPC's name. Outside a lesson each shows the cards of the phrases its prompt practises (its `focus`, V12b): the fisher `¿Dónde está la panadería?` and `¿Dónde está la herrería?`, the baker `Quiero dos panes, por favor.`, `¿Cuánto cuesta?` and `¿Hay pan?` (the third collapses to a strip at 1280 x 720), the smith and the farmer `¿Cómo te llamas?` and `¿Qué haces?`; a turn is credited to the phrase it used there (`phrase quiero-dos-panes supported at baker`).

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
# checklist is complete: a spare torch is in the bag and the beacon points up the trail), the guide's transfer offer "¿Quieres agua?", and the climb back to the
# player's camp, where a torch on the rim of the cold fire ring lights it: the closing card says "Session complete". The console prints every lesson step and its
# outcome, each turn, each game event and the objective's steps; the PNGs show the lesson's end, the stall, the river and the lit fire. One step is spoken
# (S22): after the lesson the route greets the guide with `--wav` and the bundle's recorded "Hola." (the Kokoro line), so the step goes through Vosk (the
# console prints "heard "hola"" and the greet intent). It needs the Vosk model in VOCATERRA_MODELS_DIR; the other steps are typed.
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute \
  --lesson-run first-words --shot /tmp/s17-lesson.png \
  --talk guide --wav content/game-v1/speech/phrase.hola.wav \
  --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --shot /tmp/s17-stall.png --say "sí, por favor" \
  --talk villager --say "¿dónde está el río?" \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 --shot /tmp/s17-river.png \
  --talk guide --say "sí, por favor" \
  --walk-to 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 \
  --screenshot /tmp/s17-fire.png --frames 90
# the lesson alone, with its steps printed (the player is walked down the trail to the guide first); the book's phrases page at the end shows what was learnt
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --journal --journal-phrases --screenshot /tmp/s17-journal.png --frames 30
# the opening: the objective line and the first tip at the spawn (a scripted run draws no tips unless --tips is given)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --tips --screenshot /tmp/s17-opening.png --frames 90
dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need
```

The steps `--look yaw,pitch` (degrees, yaw 0 north and 90 east), `--walk-to x,z`, `--interact x,y,z`, `--place item@x,y,z`, `--select n` and
`--craft item` run in the order given, and so do the talk steps (S6): `--talk <npc>` (an id of `game/npcs.json`: `guide`, `shopkeeper`, `villager`,
`fisher`, `baker`, `smith` or `farmer`; walk up to the figure, stop about two blocks away facing it, open the panel), `--say <line>` (type and send a line to the NPC the panel is open on and wait for the reply; with no
panel open it first talks to the guide, as in S3), `--leave` (close the panel; walking on or talking to someone else closes it too) and `--shot
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
on one post and a lantern hanging over the bench, with a chest and crates beside it; the player's camp has a canvas tent, a chest and a crate beside the cold
fire ring (which the S17 ending lights as before). The lanterns tint the faces within seven blocks warm (half as strongly by day, fully with `--dusk`).
Everything is placed by `SceneBuilder` from `scene.json` and the seed's `WorldRng` (the stall's goods, the field's gaps), so it is part of the scene digest and
a test pins the village's and the camps' structure.

**The figures.** The guide Luna (green jacket, yellow scarf, long hair, a pack), the shopkeeper (a white shirt with rolled sleeves, a leather apron, a beard and a
baker's hat), the villager (a blue tunic and a straw hat), since VILLAGE_PLAN V05 the fisher Mar (a blue coat, tall boots, a knitted cap and a grey beard), the
baker Luz (a rose dress with flour-white sleeves, a white apron and cap), the smith Domingo (a rust shirt, a long dark apron, a moustache) and the farmer León (a
green shirt and the villager's straw hat in tan), and the player (a blue shirt and a backpack with a bedroll) are boxes of the atlas's cloth, skin and
hair pictures with a drawn face on the front of the head, shaded by the same sun table as the blocks. They idle (a breath, swaying arms, a head that looks about);
**when you talk to one it turns to you** (`FigureFacing`: the shorter way round, at most 80 angle steps a tick) and nods and waves its right arm, and turns back to
where the scene put it when the talk is over. **F5** shows you from behind your right shoulder (the camera stands three blocks back and never inside a wall), with
the figure walking when you walk; first person has the arm instead.

**The arm.** The first-person arm is a blocky forearm in the player's blue sleeve with a hand in the player's skin, drawn in 3D with the figures' cloth and skin
pictures, no outline, tinted by the light where you stand (dimmer under cover, warm near a lantern or with a torch in hand). It holds what the hotbar has selected:
a log for wood, a torch with its flame for a torch, a bottle, or a flat berry or loaf, and since VILLAGE_PLAN V05 a coin, an apple or the axe; it sways when you walk, chops while you gather and dips once when you place.

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
load when a scenario needs a day to pass. A load is refused, with the reason, when `--seed` names another world; a save whose restored world does not match its digest is refused too. A save made
from **another content bundle** is carried into this one (X01, below), unless the run is strict (`--strict-save`, or any `--playtest`), which refuses it. The file is canonical JSON (sorted keys, no whitespace):
the same state gives the same bytes, and a load followed by a save gives the same file except for the session number. Not saved: the open dialogue,
the review question, the word cards not yet shown, the replay records of the conversations (the session log is S11's). A sleep at the player's own fire is a
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
# the same world in the next session: "load:" prints the saved and the next session and the digest check; the journal's phrase page reads "Sesión 2"
dotnet run --project src/Vocaterra.Game -- --load /tmp/s10.save --journal-phrases --screenshot /tmp/s10-load.png
# refused: the save is for world seed 42 (the message says so and nothing starts)
dotnet run --project src/Vocaterra.Game -- --load /tmp/s10.save --seed 7
```

**Checks, the session log and the report (S11).** `--check pre|post|recall` runs the check battery outside the world, on its own screen (the
items are `content/game-v1/game/checks.json`): items that play a Spanish line and offer three meanings (keys **1**, **2**, **3**; **R**
plays it again), and items that ask the learner to say a phrase in Spanish for an English situation (hold **V** to speak, **W** to type and
**Enter** to send, **N** for "no sé"). S11 had ten; since VILLAGE_PLAN V07 a battery asks the items of the lessons its play session has unlocked and
always those of the first three sessions (`coreSessions: 3`): 31 items up to session 3 (16 heard, 15 said; the pilot's battery), 39 in session 4, 47 in
session 5 and all 55 of the bank from session 6. The order and the options' order come from the world seed and the kind, so a pre-check and a post-check
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

## The playtest (S12)

`--playtest` is the profile for the playtests (the facilitator's script is [docs/game/PLAYTEST.md](PLAYTEST.md)): no debug overlay (F3 is off),
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
# transfer's --leave and the climb to the player's own fire, in the order the console prints as "check order:" (both lists below are for seed 42 and the 31 items of the first session's battery; when they change, run once and read both orders from the console);
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
`VOCATERRA_MODELS_DIR`, after its checksums are checked), the tester's README (`scripts/playtest/README-tester.md`) and the launcher
(`scripts/playtest/playtest.sh`, which runs `--playtest` with the folder's own models). Then it checks the folder: no sherpa-onnx or ONNX Runtime native
library, no espeak-ng, no sherpa model file, the Vosk model's checksums, every speech file the bundle's manifest lists (680 since VILLAGE_PLAN V12); the build fails if one check does not pass. The
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

## Content

`content/game-v1` is the game's bundle, under the bundle id `game-v1`. **The game owns it** (decision D-game-4, decided 2026-10-21: the bundle
diverges from the core's sample and is not re-synced). The core's former sample bundle (`content/v1`, a blacksmith with spells and items; deleted in R02, 2026-10-23, and kept in the git history)
was the starting point: the generic intents (`confirm`, `deny`, `farewell`, `thank`), their dialogue pools and the first feedback pools were
copied from it, reworded where they failed the game's A1 check, and stay credited to the sample (Apache-2.0) in `ATTRIBUTION.md`. Everything
added since is the game's own, hand-authored: the guide (agent `guide`, intents `ask_directions`, `ask_what` and `ask_help`, prompt
`guide-open`, the reply pools and the host-line pools the game renders itself), `answer-sets/places-basic.json` and `lexicon/world.json`, and
since S5 the prototype's stall and village: the agents `shopkeeper` and `villager`, the intents `request_item` and `dont_know`, the answer set
`supplies`, the prompts `stall-need`, `stall-offer`, `villager-open` and `guide-offer`; and since phase A of the village plan (V10 to V12) the
agents `fisher`, `baker`, `smith` and `farmer`, the intents `ask_far`, `ask_availability`, `ask_price`, `ask_name` and `ask_activity`, the answer sets
`places-village`, `supplies-bakery`, `people-smith` and `people-farmer`, and the prompts `fisher-open`, `baker-open`, `smith-open` and `farmer-open`
(with each lesson's `lesson-*` prompts and answer sets). The blacksmith and the spells left the bundle in S5 (they went with the core's former sample). **The phrase sheet [content/PHRASES.md](../../content/PHRASES.md) is the source of truth for the Spanish the learner is
taught** (decision D-game-10): every row is checked against the bundle by `tests/Vocaterra.Host.Tests/PhraseSheetTests.cs`. Since R02 this is the repository's only bundle: the
core's tests, its fuzz suite and its demo CLI run on it, so an edit of the game's content re-runs the core suite, and a core test that fails is the signal that the
edit broke something the core relies on (`docs/nlp/CONTRIBUTING.md`). The bundle has its own hash, which the loader
checks against `manifest.json`. After any edit, rewrite the manifest:

```bash
scripts/content.sh validate                                  # every file's hash and the cross-references
scripts/content.sh hash content/game-v1 --bundle-id game-v1  # rewrite manifest.json (keep the bundle id once it is set)
scripts/content.sh cefr-check content/game-v1 --level a2     # the bundle's ceiling: nothing listed (--level a1 is a report of what is above A1)
scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'   # 0 errors, 0 warnings (the guide's host-voiced pools are named)
scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --offline   # the grammar check from the committed cache, no server
```

**The grammar check (S23, core M30, D10).** The review chain is lint, `cefr-check`, grammar, then the Opus pass (D-game-12). The grammar step
sends every Spanish string the player hears or says to a LanguageTool server on this machine (never the public API) and reads the answers back
through a committed cache. It covers the bundle's dialogue, prompts, answer sets and intents, the phrase sheet (the phrases, the accepted variants
and the learner-error examples, with their tags), the Spanish side of `game/checks.json`, and the speech index (the support language, in `content/locale` since C03, is not checked). The
strings are written by the host from those sources, so the list is always the game's own:

```bash
dotnet run --project src/Vocaterra.Host -- --content content/game-v1 --grammar-strings content/grammar/game-v1-strings.jsonl
```

The committed files are `content/grammar/game-v1-strings.jsonl` (the strings), `game-v1-cache.json` (one result per string, keyed by the
LanguageTool version, the settings and the text) and `game-v1-waivers.json` (each accepted match, by rule and string id, with its reason; a
waiver with no reason is refused). Findings are warnings (agreement, spelling, grammar), info (style, and the learner-error oracle's facts) or
errors (a string with no cached result). The English support text is not checked (the owner's decision).

**Refreshing the cache after a content change.** `scripts/content-loop.sh --grammar` (V18) runs steps 1 to 5 and the check CI's `languagetool`
job makes (below) in one command, prints every new finding and stops the server even on a failure; it is the edit loop of
[content/README.md](../../content/README.md) ("The edit loop"), and the way to refresh the cache. Step 4 is still yours: read each finding it
prints. By hand, from the repository root:

1. Write the strings again (the command above). The strings file is checked by a test, so a stale file fails `dotnet test`.
2. Start the server: `scripts/languagetool.sh start` (it refuses below 4 GB free; `scripts/languagetool.sh status` shows it).
3. Check the new strings and prune the entries no string uses any more:
   `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --prune`
4. Read every warning and every new info finding. Fix the content, or add a waiver with a reason to `game-v1-waivers.json`. Do not
   waive a true positive to get a clean run.
5. Stop the server (`scripts/languagetool.sh stop`), run the offline command above (it must exit 0), and commit the strings, the cache and the
   waivers with the content change. The gate runs the offline command only and never needs the server.

CI runs the check twice, as two parallel jobs of `ci.yml` on every push to `main` and every pull request. The `build-and-test` job runs the
offline command. The `languagetool` job starts the pinned LanguageTool on the runner, writes the strings again, checks every string with an
empty cache, and fails when the result differs from the committed `content/grammar/` files or has an unwaived warning. On a failure, its
`grammar-refresh` artifact holds the refreshed strings and cache; review the diff and commit them, which stands in for steps 1 to 3 when no
local server is at hand.

Since S4 the catalogue holds only the nouns of the 3D world (the ores, tools and furniture of the 2D slice are gone), and the recipes are the
workbench's torch alone.

The content gate from S5 on: `lint --strict` clean (an example expectation for every phrase the learner says, in `intents/intents.json`), and
the phrase-sheet test green. Since V08 (VILLAGE_PLAN 3.9, D-game-22 (b)) the bundle's ceiling is A2: the lint runs at A2 with `--fail-on-level`,
`cefr-check` at A2 fails the gate on any lemma it lists, and `cefr-check` at A1 is a report for the owner that never fails; each lesson declares
its own `level` (`a1` or `a2`), and the host's `--validate-content` holds its Spanish and its tenses to it (`content/README.md`, "A lesson's
level"). `--host-pools 'guide.*'` tells the lint that the game voices the guide's templated world lines itself (they have no agent action);
without it those pools are reported as unreachable.

The script runs the core's content tool (`tools/Vocaterra.Nlp.ContentBuilder`, from source; see
[scripts/README.md](../../scripts/README.md)). `ATTRIBUTION.md` and `provenance.json` describe this bundle; the Wiktionary lexicon in it is CC BY-SA
and keeps its notice.

## Continuous integration

The repository's `.github/workflows-pending/ci.yml` is parked: it needs the workflow scope on the token (core D1), and the owner deferred CI
(D-game-3 in [docs/game/DECISIONS.md](DECISIONS.md)). Until then nothing runs in CI.

## The core's native libraries

The core depends on third-party native libraries, and one of them carries GPL-3.0 code (core D2); the game does not distribute a build that
includes them until D2 is settled in the core. The playtest folder (`scripts/playtest-build.sh`) leaves that native library out, and its check
says so; the owner decides whether handing the private folder to testers counts as distribution under D2.

## Licence

- **This repository, the game and the language core alike:** Apache-2.0 (see [LICENSE](../../LICENSE) and [NOTICE](../../NOTICE)). The game was MIT until the owner relicensed the whole repository in R01 (D-game-2 superseded; recorded in [DECISIONS.md](DECISIONS.md)).
- **Content bundle:** the Wiktionary lexicon in `content/game-v1` stays CC BY-SA 4.0; its notices are in
  [content/game-v1/ATTRIBUTION.md](../../content/game-v1/ATTRIBUTION.md).
- **Language core:** the `Vocaterra.Nlp.*` projects are Apache-2.0 too (core D3, [LICENSE](../../LICENSE)). **MonoGame** is Ms-PL.
- **Art, font and sound:** CC0 only (D-game-5), each file listed in [assets/ATTRIBUTION.md](../../assets/ATTRIBUTION.md).
- **Native libraries:** a build that includes the sherpa-onnx native libraries (GPL-3.0 code, core D2) is not distributed while D2 is open.

## Documentation

- [docs/game/SPECIFICATION.md](SPECIFICATION.md): the game design
- [docs/game/ARCHITECTURE.md](ARCHITECTURE.md): how the game consumes the core, the host's calls, what the host owns and what the game owns
- [docs/game/DECISIONS.md](DECISIONS.md): the game's open and decided questions
- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md): the milestones, the scene designs and the findings log
- [docs/nlp/API.md](../nlp/API.md): the calls a host makes, in order
- [docs/nlp/adr/0009-core-and-game-boundary.md](../nlp/adr/0009-core-and-game-boundary.md): what the core and the game each own
- [CLAUDE.md](../../CLAUDE.md): working notes for the agents and the owner

## English guidance and the lesson runner (S16)

English is drawn, never spoken (D-game-13): an English line under the guide's Spanish in its own colour (`English: ...`), the bilingual camp checklist, plain-English
feedback after each turn, first-time tips and the controls on F1; the support ladder starts with English and fades per phrase (see
[src/Vocaterra.Game/README.md](../../src/Vocaterra.Game/README.md)). A data-driven lesson runner (`content/game-v1/game/lessons.json`) walks the learner through the
guide's lesson (L starts it, Enter continues, K skips a step, H helps; S16 shipped a two-step sample, which S17 replaced with the real lesson). A scripted run
through it prints each step and its outcome:

```bash
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --talk guide --lesson first-words --lesson-next --lesson-next --say hola --screenshot /tmp/s16.png --frames 60
```

## The opening, the guide's lesson and the ending (S17)

- **The opening.** No quiz at the spawn. The first tips come one at a time, each at its delay (`delay` seconds in `game/tips.json`; their text is the locale's since C03): look at 0 s, walk at 9 s, jump at 18 s,
  the note pinned to the tent at 28 s (English, with the one Spanish word `camino`, saying the guide's camp is down the trail), collecting at 50 s; the first
  pickup then teaches the inventory. The objective area says `Go down the trail and find the guide` until the guide is met (the panel opened on her, or the first
  step of the checklist). The beacon points at her camp from the start.
- **The guide's lesson** (`first-words`, `game/lessons.json`). Luna, the guide, greets the player who comes within five blocks of her
  (`GuideApproach`; the same call as the E key, once per approach; Esc closes the panel and the lesson waits): the lesson starts by itself. Seven units since S26 (the verb unit
  below), 37 steps (S17: six units, 34 steps, about eight minutes at a learner's pace, estimated 4.8 minutes for a quick reader who answers in 4 s, 7.5 for a slow one, from the real WAV lengths, plus about a minute to walk down the trail): hello, yes and
  no, what is this, please and thank you (the guide offers fruit and the
  learner picks it from a bush: a `do` step with a beacon on the nearest berry bush), getting unstuck (`No sé`, `Otra vez, por favor`, `Más despacio, por favor`
  do what they say) and the camp's task, then a recap that opens the book's phrases page. While a lesson step takes a turn the guide's ordinary reply is not
  shown (the lesson speaks for her); a turn the core did not understand keeps her not-understood line and the English help.
- **The ending.** The complete checklist gives a spare torch, sets the beacon on the player's own camp and changes the objective line to `Go back up the trail and
  light your own fire`. A torch placed on the rim of the fire ring at `firering.player` lights it: the guide's closing line (`¡Muy bien! Ya tienes fuego.`) is
  said and shown with its English on a `Session complete` card, and in a playtest the post-check follows (the transfer offer first: she greets the player with
  it when they pass through her camp on the way back). The fire ring at the guide's camp keeps its role: the checklist's torch step. Outside a playtest the card
  offers the next session (`Enter: sleep until morning and start session 2`) and the objective line says `Session complete: sleep at your fire to start session 2`
  (V12c, "The next session: sleep at the fire" below); a playtest keeps `Session complete: your fire is lit`.
- **English on the buttons.** `[R] Repetir (repeat)`, `[S] Más despacio (slower)`, `[H] Ayuda (help)`, `[N] No sé (I don't know)`, `[W] Escribir (type)` and the hold-to-talk
  hint carry their English until the button (or its phrase) has been used three times; the keys and buttons work the same without it.

```bash
# the whole lesson with typed answers (every step and its outcome is printed), then the book's phrases page
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --journal --journal-phrases --screenshot /tmp/s17-journal.png --frames 30
```


## Verbs in the lessons, sessions and replay (S26, D-game-19)

- **The verb unit** (in `first-words`, after "please and thank you", five steps): the present yo and tú of `querer`, `tener` and `necesitar`. Luna asks `¿Qué quieres?`, `¿Tienes una fruta?` and
  `¿Qué necesitas?`, and the learner answers with the yo forms (`Quiero madera.`, `Tengo una fruta.`, `Necesito agua.`; the fruit is the one the learner just picked). The English names the
  pattern once (-o is I, -as or -es is you) and then thins. The answers are statements (`state_want`, `state_have`), so nothing is handed over; a tú form after `yo` is a `conj.person` slip
  and `teno` for `tengo` a `conj.irregular` one, and the step goes on either way (fail-forward).
- **Sessions.** A lesson has a `session` in `lessons.json` (default 1) and is offered only once the play session has reached it; the session is the save's number (a load is the next session).
  A lesson of a later session also has an English `objective` that names it: the objective area shows it (and the beacon points at the lesson's NPC) while the lesson is unlocked and not begun.
  `--session <n>` starts a run as play session n (`--session 2 --screenshot x.png --frames 120` shows session 2 opening at the camp: `Session 2: go to the village and talk to the villager to start
  the lesson “Personas”`, with the beacon on the villager). It only names the session: a lesson that comes `after` another still waits for it, so `--session 4` points at session 3's lesson.
  `--skip-to-session <n>` (V12c, a test aid) starts a new game where a learner who did sessions 1 to n-1 would stand, so session n's own lesson is open ("The next session" below). Since VILLAGE_PLAN V10 a lesson may also name the lesson it comes `after`, and is offered only once that one is done: sessions 4 to 6
  (`--session 4`, `--session 5`, `--session 6`) unlock `el-pueblo` after `ayer-y-manana`, `numeros-y-pan` after `el-pueblo` and `gente-del-pueblo` after `numeros-y-pan` ("The village: sessions 4
  to 6" below). `--lesson-run <id>` starts a lesson whatever came before.
- **Personas** (session 2, the villager then the shopkeeper, 22 steps since VILLAGE_PLAN V10 added its two `review` steps, `autoStart`): él, ella, usted, nosotros, ustedes and ellos of `querer`, `tener`, `necesitar`, `ser`, `estar` and `ir`. The learner
  says what Luna wants, what we need and what we want and answers the shopkeeper's `usted` questions about themself; the other forms are heard (listen steps) and listed once. A step may name its NPC
  (`npc`), and a `do` step may wait for the panel to open on an NPC (`at`): the walk from the villager to the stall. While such a step waits the objective says `Walk to the stall and talk to the
  shopkeeper`. The villager's and the shopkeeper's lines are in pools named `guide.lesson.villager.*` and `guide.lesson.shop.*` (the voices follow the prefix; `--host-pools 'guide.*'` counts them).
  `--session 2 --lesson-run personas` runs it with typed answers, walking from the player's camp to the villager and then the stall.
- **The forms a lesson teaches.** `lessons.json` declares them (`verbs`: lemma, person 1 to 3, number sg or pl, tense): the yo and tú of the three verbs (`first-words`) and the third person singular, the
  first and the third person plural of the verbs it uses (`personas`, 12 forms: only those heard, said or named; the conjugation of `ir` waits for session 3). A test checks each is a word the learner meets in the lesson and that the core's morphology knows it; the book's conjugation
  tables (S28) read them.
- **The level a lesson keeps to.** Every lesson declares `level`, `a1` or `a2` (VILLAGE_PLAN V08; the six lessons of sessions 1 to 6 are `a1`). The host's `--validate-content` holds the Spanish it
  teaches and says to the bundle's CEFR lists up to that level, its phrases' variants to A2 (the bundle's ceiling), and its tenses to the level's (`fut` only at `a2`); `content/README.md`, "A
  lesson's level", has the rule.
- **Practise again.** A completed lesson's heading on the book's Lessons page has a `Practise again` button (a click, or P with the page open). It closes the book, sets the objective line (`Practise
  again: talk to the guide to start “First words with Luna”`) and the beacon, and the lesson starts from its first step when the learner talks to its NPC. It stays done while it runs again.
  `--book-practise <id>` presses the button in a scripted run (`--lesson-run first-words --book-practise first-words --lesson-run first-words`).
- **The person of the answer's verb.** A step may declare the person and number its answer's verb must have (`person`: `1sg`, `3sg`, `1pl`...; and a `personRetry` line that names the ending).
  The runner reads the finite verb's person and number from the turn's word readings; a mismatch the core did not flag (no subject pronoun, so no `conj.person`: `quieres madera` at `¿Qué quieres?`,
  `quiero fruta` at `¿Qué quiere Luna?`) is a miss, not a success: the step stays, the retry names the ending, and after the step's attempts it moves on (fail-forward). The log's retry line says
  `wrong person: said 2sg, expected 1sg`. A wrong person next to a pronoun (`yo quieres`) stays a slip the core grades. Every verb step of both lessons declares its person.
- **The save** is version 4 (1 to 3 still load): each lesson keeps whether it was ever completed (`everCompleted`; a completed lesson of an older save counts) and the save keeps a replay that was asked for. Since C07 it is version 5 (1 to 4 still load): the objective's steps are keyed `<objective>.<step>` (`camp.wood`), and an older save's keys are mapped on load.

## Yesterday and tomorrow, the tense check (S27, D-game-19)

- **Ayer y mañana** (session 3, the villager then the shopkeeper, 21 steps with V10's two `review` steps, `autoStart`): the present of `ir` (voy, vas, va, vamos, van, listed once), the near future with `ir a` + an infinitive
  (`Voy a comer pan.`, `Vamos a comer fruta.`, `Mañana voy a ir al río.`; `Ellos van a comer pan.` is heard) and a few preterite forms, yo and tú (comí, bebí, fui; comiste, bebiste, fuiste), with the time words
  `hoy`, `ayer` and `mañana`. The villager covers today and tomorrow, a walk to the stall (the same short walk as Personas) leads to the shopkeeper, who asks about yesterday: `¿Qué comiste ayer?`,
  `¿Fuiste al río ayer?`, `¿Bebiste agua ayer?` and, last, `¿Qué vas a hacer mañana?`. The answers are statements about an action in any tense (the intent `state_action`), so nothing is handed over.
  The lesson of the newest session comes first at an NPC (a learner who reaches session 3 without session 2's `personas` is offered `ayer-y-manana`, and `personas` after it).
  `--session 3 --lesson-run ayer-y-manana` runs it with typed answers, walking from the player's camp to the villager and then the stall.
- **The tense of the answer's verb.** A step may declare `tense` (`pres`, `pret`, `ir-a` or, since V16, `fut`; below) with a `tenseRetry` line, next to S26's `person`. The runner reads the answer's finite verbs from the turn's word readings:
  for `pret` and `pres` one finite verb with that tense is enough; for `ir-a` it looks for a form of `ir` in the present, then `a`, then an infinitive (`voy a comer`; `vamos` is read as an imperative or a present, both count;
  `iba a comer` is a past, V16).
  A mismatch the core did not flag (a bare `como pan` or `comí pan` at `¿Qué vas a comer mañana?`, `voy a comer fruta` at `¿Qué comiste ayer?`) is a miss like a wrong person: the step stays, the retry names the form,
  the log says `wrong tense: said pres, expected ir-a`, and after the attempts the step moves on. Person is judged first. **Correct Spanish is never a miss:** a plan said in the present with a future-time word (`mañana voy al río`, `mañana como pan`; the words are `game/words.json`'s `nearFuture.timeWords`) is credited (the step ends Done, the log says `present for a plan, credited`)
  and the next step opens with a gentle English note naming the form the step practises (`That is right too. Here we practise “voy a ir”: Mañana voy a ir al río.`; the step's `presentNote`). A tense next to a time word is the core's `conj.tense` (M49: `mañana comí pan`,
  `ayer como fruta`, `voy a como pan`): a slip, the step ends. A miss shows none of the core's correction line (its recast would praise the learner's own wrong words).
- **A miss is not a use.** A turn the lesson counts as a miss for the person or the tense is no use of the phrase: it adds no phrase use (so no credit, no support step, no word of the phrase's progress); the
  right answer after it is the phrase's one use.
- **The near future in the conjugation tables.** `lessons.json` declares a taught form as `lemma`, `person`, `number`, `tense`; the tenses are `pres`, `pret`, `ir-a` (the near future) and `fut` (the simple future, A2; a step may
  expect it since V16, below). `ir-a` is a periphrastic form: it has no word of its own, so a form `{ "lemma": "comer", "person": "1", "number": "sg", "tense": "ir-a" }` is `voy a comer`: `ir` in the present for that person and
  number, `a`, then the lemma as the infinitive. The lemma of an `ir-a` form is the verb that follows the `a`. The book's table for a verb (S28) therefore has a column "future (ir a)" whose cells are built from the
  `ir` row of its present column and the verb's infinitive, never typed by hand; `TaughtVerbForm.Text(morphology)` gives the text of any declared form (`voy a comer`, `comí`, `fuiste`). The column and the preterite
  column stay hidden until a lesson declares a form of that tense.

## The simple future at a step (VILLAGE_PLAN V16, phase B)

Session 9 ("Mañana en el pueblo", V17) teaches the simple future (`compraré`, `iré`, `hará`) against `ir a`; V16 made the runner and the book ready for it. No lesson of the
bundle has a `fut` step yet; the tests use fixture lessons (`SimpleFutureStepTests`).

- **A `fut` step.** A repeat or answer step may declare `"tense": "fut"`, in an `a2` lesson only (the validator; V08 held a lesson's `verbs` to the same rule). Its answer is a finite
  verb the core reads in the future: the core's tense label is the lexicon's feature `fut`, mood `ind` (`compraré`, `iré`, `hará`), and the runner compares that as it is.
- **Correct Spanish for the future is never a miss.** At a `fut` step the near future (`voy a comprar pan`, `mañana voy a comprar pan`) and a plan in the present with a time word
  (`mañana compro pan`, `la semana que viene compro pan`) are credited: the step ends Done, and the next step opens with the step's `futureNote` (`That is right too. Here we
  practise “compraré”: …`). One note serves both, since it names the form the step practises whichever other form was said; a `fut` step has no `presentNote`. At an `ir-a` step
  the simple future (`mañana compraré pan`, `compraré pan`) is credited with the step's `presentNote`, which names the step's form in the same way, so a learner who has met the
  future in session 9 is not told off at a later `ir a` step. The log says `near future for a plan, credited`, `present for a plan, credited` or `simple future for a plan, credited`.
- **A wrong tense.** A preterite or a present without a time word at a `fut` step (`compré pan`, `compro pan`, `iba a comprar pan`) is the lesson's miss with the step's
  `tenseRetry`, as at the other tense steps. A wrong tense next to a time word is the core's `conj.tense` (`mañana compré pan` expects `voy a comprar`; `ayer compraré pan`
  expects `compré`): a slip, and the step ends.
- **The time words** (`game/words.json`, `nearFuture.timeWords`): `mañana`, `luego`, `después`, `pasado mañana` (V10) and the core's markers of a time noun with `próximo` or
  `que viene` for `semana`, `día`, `mes` and `año` (`próxima semana`, `semana que viene`, ...). A time word of several words is matched as a whole, its words in order, never one
  of them alone (`NearFutureWords.Mark`), and a verb inside one (the `viene` of `la semana que viene`) is not the answer's verb. `luego` and `después` are no markers to the core
  (`después compré pan` is narration, so the lesson judges it); `el día que viene` is left out, since it may be a relative clause (`el día que viene Luz`). The bundle's grammars
  take the later markers in V17, after the core's M57.
- **The book.** The conjugation tables gain a fourth column, the simple future (`ui.bookColumnFuture`, "future"), after the future (ir a): time runs left to right and the two
  futures stand in the order the lessons teach them. It shows once a lesson teaches a `fut` form of the verb or the learner says one, as the others do, and its counts come from
  the word record like the present's and the past's. Its forms come from the lexicon (the Wiktionary import: `compraré`, `comprarás`, `comprará`, `iré`, `buscaré`, `hará`,
  `tendré`, `trabajará`, `pescará`); the core's regular paradigm builds no simple future, so a verb the import lacks has no `fut` cell. A page too narrow for four columns stacks
  them in blocks (X08).

## The village: sessions 4 to 6 (VILLAGE_PLAN phase A, D-game-22)

The plan is [docs/game/VILLAGE_PLAN.md](VILLAGE_PLAN.md) (V01 to V07, V10 to V12); each lesson uses its verbs in the present, the preterite and the near future
around an order the learner carries out (the plan, the doing, the report).

- **The people.** Seven NPCs, in the order of `game/npcs.json`: the guide Luna, the shopkeeper (`Tendero`), the villager (`Aldeano`), the fisher Mar (`Pescador`),
  the baker Luz (`Panadera`), the smith Domingo (`Herrero`) and the farmer León (`Granjero`). An NPC is data alone (`game/npcs/<id>.json`, schema 2, V01: its
  prompts by state, its opening, its voice, its key words; `content/README.md`, "Adding an NPC"). The four new voices are provisional (D-game-22 (d): the
  fisher `em_santa` at 0.9, the baker `if_sara`, the smith `im_nicola`, the farmer `pm_santa`; `docs/game/SPEECH_CHECKLIST.md` and `docs/game/voice-trial/`).
- **Directions** (V02). The fisher and the villager point the beacon at the place the learner names (`¿Dónde está la panadería?`: the scene's labels, then a
  catalogue noun; any other word is "No sé dónde está."); every answer logs `place_asked` and the arrival at the arrow logs `beacon_reached`.
- **Coins, apples and the axe** (V03, V05): `moneda`, `manzana` and `hacha` are items with their icons and held sprites; the axe fells a trunk in a quarter of
  the time. An **order** is an objective shown and counted from its play session, delivered by walking up with the goods: the panel opens, the goods go, the
  NPC thanks and pays, and `order_delivered` is logged. `fruit-for-mar` (session 4: three fruits for two coins), `oven-wood` (session 5: five wood for four
  coins) and `axe-handle` (session 6: two wood and two coins for the axe). The baker sells bread at two coins a loaf (`Quiero dos panes, por favor.` buys
  two; with too few coins nothing changes hands and she says `Necesitas más monedas.`), and the stall says its price.
- **El pueblo** (session 4, after `ayer-y-manana`, the fisher at the bridge, 24 steps): `¿Dónde está…?`, `¿Está lejos?`, `¿Hay un pozo…?`, and the walk itself in
  three times (`Voy a la panadería.`, `Voy a comprar pan.`, back at the bridge `Sí, fui a la panadería.`): the arrow to the bakery and the smithy, the
  villager at the signpost, then back to the fisher, who asks for fruit (`fruit-for-mar`).
- **Números y pan** (session 5, after `el-pueblo`, the baker then the stall, 23 steps): the numbers to ten, plurals, `¿Cuánto cuesta?`, `Quiero dos panes, por
  favor.`, and the oven order in three times (`Voy a buscar madera.`, `Tengo cuatro monedas.`, `Compré dos panes.`: the regular `-ar` preterite).
- **Gente del pueblo** (session 6, after `numeros-y-pan`, the villager, the smith and the farmer, 28 steps): `¿Cómo te llamas?`, `¿Qué haces?`, `Es el herrero.`,
  `Vivo en el campamento.`, and the smith in the third person in three times (`Domingo trabaja en la herrería.`, `Va a hacer un hacha.`, `Domingo hizo un
  hacha.`) around the axe order.
- **¿Cómo es?** (session 7, after `gente-del-pueblo`, the farmer then the stall on the way to the river and back, 20 steps; the first `a2` lesson, phase B):
  `La manzana es roja.`, `Es grande.` (an adjective after its noun, agreeing with it), `Sí, me gusta el pan.`, `Sí, me gustan las manzanas.`, `Quiero tres
  manzanas rojas.` (one coin an apple: with no coins the farmer says `Necesitas más monedas.`), and the water order in three times (`Voy a buscar agua.`, the walk
  to the river, `Sí, fui al río.`) with `gustar` in the past (`Sí, me gustó el pan de Luz.`). The order gives three apples for a bottle of water; it names its
  lesson, so a learner who carries the bottle filled in session 1 says the plan and goes to the river before the farmer takes it.
- **Runs.** `--session 4 --lesson-run el-pueblo`, `--session 5 --lesson-run numeros-y-pan` and `--session 6 --lesson-run gente-del-pueblo` run them with typed
  answers, and `--skip-to-session 7 --lesson-run como-es` the farmer's. The scripted runs r18 (the bakery purchase) and r19 (asking the way) cover the sale and the
  directions (`docs/game/SCRIPTED_RUNS.md`). To play a village lesson by hand, `--skip-to-session 4` (or 5, 6, 7) starts at it with the sessions before counted as
  done ("The next session: sleep at the fire" below; r21).

```bash
# session 5: five trunks from the orchard, the oven order delivered at the baker (four coins), two loaves bought (r18 of the scripted runs);
# the inventory at the end holds the book and two loaves
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session 5 --teleport 309,216 --interact 311,38,214 --interact 311,39,214 --interact 311,40,214 \
  --interact 311,38,218 --interact 311,39,218 --talk baker --shot /tmp/v09-paid.png --say "quiero dos panes, por favor" --leave --inventory \
  --screenshot /tmp/v09-bakery.png --frames 30
# session 1: the fisher asked the way to the bakery, the arrow, the walk to its door (beacon_reached), the bakery and the baker (r19)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --teleport 236,206 --talk fisher --say "¿dónde está la panadería?" --shot /tmp/v09-arrow.png \
  --walk-to 282,206 --walk-to 282,212 --look 0,4 --screenshot /tmp/v09-bakery-door.png --frames 60
# session 4: the fisher's lesson with typed answers, from the player's camp
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session 4 --lesson-run el-pueblo --screenshot /tmp/v10-el-pueblo.png --frames 30
```

## The next session: sleep at the fire (V12c)

A play session used to advance only on a quit and a `--load`, which nothing in the game said. Now the player sleeps at their own fire, and the next session begins
in the same run.

- **Where.** The closing card of session 1 (the ending, S17) offers it: `Enter: sleep until morning and start session 2`, over its `Esc: keep looking around`. And
  the lit fire at the player's camp (`firering.player`) offers it whenever the session's work is done, the same moment the objective says to sleep (or that every
  lesson is done): standing within five blocks of it, the HUD's hint under the crosshair says `E: sleep until morning (session 3)` and **E** sleeps (E keeps
  talking to an NPC under the crosshair and opening the workbench first). On waking in a session whose lesson is left the fire offers nothing, so no sleep skips it. An open panel, a screen,
  a check, a recording or a lesson step that waits comes first: no prompt then. Every lesson starts by itself, so a lesson begun in the run stays engaged when its
  panel closes and holds the sleep back until it is done. A playtest plays one session per run (its post-check follows the ending) and offers no sleep.
- **A sleep is exactly a load** (`SaveGame.Sleep`). The game captures its own save and restores it into the running session as the next play session: what a save
  does not keep is cleared first (the review question, the second tries, the warm-up, which the new session offers again, the panel's support, the buttons' English
  counts, the word cards not yet shown), every part of the save is put back as a load puts it back, the session number goes up by one (which lifts the spacing gaps of
  D-game-6), and `BeginPlaySession` names the new session's lesson and points the beacon at its NPC. The stored time does not move, as a load adds none ("Time
  rule" above). A lesson under way stays at its step, not engaged, and takes up again when the learner talks to its NPC, as after a load. `SleepTests` holds the
  invariant: the save written after a sleep equals the save written after a save and a load of the same state (no field differs), and so does what the player
  meets next (the objective, the beacon, the lesson, the warm-up). Since V12c the guide's greeting on the way back reads her saved offer flag, so after a load or a
  sleep she does not greet the player again for an offer made.
- **The session log and the autosave do what a quit and a load into the same folder do.** The log writes the old session's `session_end` and the new session's
  `session_start` (no mic check, no check battery; a `slept` world event is the new session's first, `session 1 to 2`), and the `session_end` counts only its own
  session's events. The autosave writes the session as it ends (`autosave: ... written at sleep`, with no toast), so a load of the autosave starts the session the
  sleep starts; then its policy starts again from the sleep's tick, as a load's run starts it. The folder and the files stay the run's. The console says
  `sleep: ...: session 1 ends at the fire, session 2 begins (tick, stored time, world digest)` and a toast says `Good morning: session 2 begins`.
- **What the objective says next.** Once the session's work is done (session 1: its ending; from session 2: no lesson under way and every unlocked lesson done), the
  line says `Session complete: sleep at your fire to start session 2`, and when the work ends away from the fire (a lesson completed in the village) the beacon
  goes to the fire. When no later session has a lesson (after session 7 today) it says `Every lesson so far is done: practise any of them again in the book (J), or
  talk to the people of the village`; the fire still lets the player sleep, which lifts the spacing gaps. From session 2, while a lesson is under way or left, it
  says `Next: talk to the villager for the lesson “Personas”`. These lines come after a lesson's own line, a replay, a review and a new lesson, and over an order,
  as the new lesson's line does, except that an open order keeps its own line rather than `Every lesson so far is done`.
- **`--sleep`** is a script step: it closes the panels and sleeps as the player's E at the fire would, wherever the player stands (a script needs no walk); refused
  with a console line when the fire is not lit, a lesson step waits or the run is a playtest. The scripted run r20-sleep reaches session 1's ending the short way
  (the camp, the shopkeeper's bottle on a second visit, the river and the fire by `--teleport`), shows the card, sleeps, and shows session 2's objective, the beacon
  on the villager (no fire prompt: Personas is left) and the book's Lessons page.

**`--skip-to-session <n>`** (a test aid, `SessionSkip`): a new game starts as play session n (from 2 to the last session that has a lesson, 7 today) where a learner
who played sessions 1 to n-1 and did everything would stand, built from the data alone (the lessons' `session` and `after`, the objectives, the orders and the NPCs'
host events), so a later session needs no code. Every lesson of an earlier session is completed in its own session (its steps recorded as skipped: the learner
took none); session 1's ending is reached (the camp's steps counted, a torch on the rim of the player's fire ring, and the camp's bottle of water in the bag: the
shopkeeper gave it, the river filled it, and nothing in the game empties it, V13); each earlier order is delivered by the NPC's own `<npc>.delivered` thanks (what
the bag lacks of its goods brought, the coins it asks for paid from the bag, since only an NPC gives coins, its reward paid); and each earlier lesson's purchases are
made (a `do` step waiting for an NPC that sells, as many as the lesson's request names: the two loaves of `numeros-y-pan`). So the coins and the items follow
VILLAGE_PLAN 3.4: none at session 4, two coins at 5, two coins and two loaves at 6 (the axe order's two coins), no coins, two loaves and the axe at 7 (the farmer's
apples are the water order's). What the world gives (wood, fruit) is not carried over: the next lesson and order point at it. No
practice is faked: no turn is taken and the word and phrase records stay a new game's. The objective and the beacon then name session n's lesson and its NPC, as
after a load. The console says `session: skipped to play session 4 (sessions 1 to 3 counted as done; test aid)`, the session log has the same line (the world event
`session_skipped`) and `skipped_to` on its `session_start`, and the save carries `skippedTo` (left out of every other save), so no log or save made with it passes
for a learner's. It refuses `--load`, `--session` and `--playtest`.

```bash
# the short way to session 1's ending, the card's sleep, session 2's objective and beacon, the Lessons page (r20-sleep)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 \
  --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha \
  --walk-to 174,185 --place antorcha@175,44,187 --teleport 283,211 --talk shopkeeper --wait 3 --leave --talk shopkeeper --leave --teleport 234,222 --interact 233,34,222 \
  --teleport 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 --shot /tmp/v12c-ending.png --sleep --look 129,-20 --wait 2 --shot /tmp/v12c-session2.png \
  --book-page lessons --screenshot /tmp/v12c-lessons.png --frames 30
# session 4 at once: the fisher's lesson named and pointed at, the earlier sessions counted as done (r21-skip)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --skip-to-session 4 --wait 1 --shot /tmp/v12c-skip.png --book-page lessons --screenshot /tmp/v12c-skip-book.png --frames 30
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
  (`summary`). Since V12c a lesson not begun says why on its own line under its title: `next: talk to Luna` for the one the objective sends the learner to (the new
  session's, or with none under way the one to go on with; title and line in gold, so the eye finds it first), `open: talk to Luna` for another that is unlocked,
  `opens in session 4, Mar` when its session has not come (said first when it also waits for a lesson), `after “Ayer y mañana”` when the
  lesson before it is not done. The NPC is named as `game/npcs/<id>.json` calls it (its name, else its label), the earlier lesson by its short title. The lines
  are whole on the left page at every text size (at 150 % in an 800 x 600 window the longest, `opens in session 2, Aldeano`, is 169 of 174 px; `UiScaleLayoutTests`).
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

**Page models.** `src/Vocaterra.Game.Core/Language/Book/BookPages.cs` (what each page lists, as `BookLine` values: a tone, a text, an aside, a right-hand text,
an icon, pips, lamps or a tick), `VerbLabels.cs` (the English table of a verb form: person, number, tense and mood), `Book.cs` (`BookModel`, the pages in
`BookTabs`, `BookGeometry` for the panel, the tabs and the lines at a size, `BookScroll`). They are engine-free and tested in
`BookPagesTests`, `VerbLabelsTests` and `BookModelTests`; the drawing is `src/Vocaterra.Game/UI/Book/BookLayer.cs`.

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
dotnet run --project src/Vocaterra.Game -- --seed 42 --sound-log /tmp/s20b.log --lesson-run first-words --book-play fruta --shot /tmp/s20b-noun.png --book-play-slow phrase:pedir-agua --shot /tmp/s20b-phrase.png --screenshot /tmp/s20b-final.png --frames 30
# the Nouns and Phrases pages with their buttons (the speaker at each row's right edge; the selected row is gold)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --book-page nouns --screenshot /tmp/s20b-nouns.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --book-page phrases --screenshot /tmp/s20b-phrases.png --frames 30
```

```bash
# the five pages after the S17 lesson route (the lesson is walked first, then the book opens on the page named)
for page in nouns verbs phrases lessons said; do
  dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --book-page $page --screenshot /tmp/s20-$page.png --frames 30
done
```

The S17 route (`--lesson-run first-words` and the steps above it) still completes with `fire_lit` and `session_complete`.

## Saves survive content updates (X01)

Every content change used to make every save unloadable (the S10 rule: a save refuses another bundle hash), so a player lost all progress when the game
updated. Now a `--load` of a save made from another bundle **migrates** it (`SaveMigration`, engine-free, in `Vocaterra.Game/Save`) unless the run is strict.

- **Kept where the id still exists in this bundle:** the learner's record (history, word rows), the settings and the session number, the word records
  (by lemma), the carded nouns, the phrase records (one per sheet row, by id), the objective's counts (by step key), each NPC's conversation state and its
  learner-model skills (`word.<noun>` and `phrase.<id>` skills of content that is gone are dropped), the tips, the arrow, the inventory (items are an enum
  of the code, not of the bundle) and the lessons. A **lesson** is mapped by step id: a completed lesson stays completed (and `EverCompleted` stays) with the
  outcomes of the steps that still exist; a lesson in progress keeps the longest run of its steps, from the first, that are all among the finished ones,
  goes on at the next step, and does again a finished step that follows one the lesson has not finished (the tries on the step in progress reset when
  the step changed). A lesson that is gone is dropped, and so is an active mark or a replay that named it.
- **Dropped and logged:** records whose phrase row, noun, lesson, step, tip or step key is not in this bundle. **New** content (a phrase row, a lesson, a
  noun) starts empty, as in a fresh game.
- **World edits are applied by position.** The save holds whole chunks of the world it was made from (and, since X01, the grid's chunk counts, so a grid that
  changed size still maps by position). A saved cell is applied only if it is a change the player can make to the cell the new scene has: a gathered block
  (the cell now holds what a gather leaves) or a placed block (planks or a torch) in air or undergrowth that touches a block. Anything else is the old world
  generation, not the player's work: it is skipped and counted in the report. The world's digest is then **reported, not required** (it still matches, and
  the log says so, when the generation did not change). A player who would stand inside a block of the new world starts where a new game starts. A save of
  this very bundle is restored exactly as before: the exact diff and the digest check, which still refuses a damaged save.
- **Told and logged:** the toast "The game was updated. Your progress was kept; N old records no longer in the game were dropped.", the console lines
  `migrate: ...` (the areas, then each dropped id), and a `save_migration` line in the session log. `VoxelSession.Migration` holds the `SaveMigrationReport`.
- **Strict mode.** `--strict-save` refuses a save of another bundle with the old message plus the reason; **`--playtest` implies it** (a playtest's recall
  session needs the bundle frozen between the sessions, D-game-10; see `docs/game/PLAYTEST.md`). The library's `SaveGame.Load/Check/Restore` take
  `migrate` (default off: strict), and the front-end passes `!StrictSave`.

```bash
# a save on the current bundle, then the same save under a changed copy of the bundle (a lesson step removed, a tip reworded; the copy is re-stamped with
# scripts/content.sh): the strict run refuses, the default run keeps the progress and prints what was dropped; the toast is in the screenshot
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --save /tmp/x01.save --screenshot /tmp/x01-before.png --frames 30
dotnet run --project src/Vocaterra.Game -- --content /tmp/x01-bundle --mute --load /tmp/x01.save --strict-save --screenshot /tmp/x01-strict.png --frames 5
dotnet run --project src/Vocaterra.Game -- --content /tmp/x01-bundle --mute --load /tmp/x01.save --screenshot /tmp/x01-migrated.png --frames 30
```

## The session summary and the second try (X04, R01 and R02)

**The summary card.** When the session is complete (S17's closing card), the card shows **This session** under the closing line, in English: the lessons
completed or begun (with the steps done), the new nouns the learner's own lines used for the first time, the phrases said correctly without help, the verb
forms used (tense and person), up to two phrases to practise next time (a phrase said "no sé" first, then a slip, then a phrase whose help is still on), and
a line on how to replay a lesson (press J for the book, choose a finished lesson, Practise again). The card stays up for 45 seconds (it was 12) or until Esc,
and it fits at 1280 x 720 and 800 x 600. `SessionSummary` (engine-free, `Vocaterra.Game`) builds it from the learner's record and the run's events. It counts
things done only: no score, no CEFR label, no count of failures (D-game-10). The lines are written to `session.jsonl` as a `session_summary` line. The card is
not drawn when a session ends early (F6, F10, quit): that is left for later.

**The second try.** A phrase the learner says "no sé" to (typed, spoken, the button, or a lesson step's phrase; "ayuda" is help, not "no sé") waits in
`RetryQueue` for the next talk with its NPC. At that talk, when no lesson holds the conversation and no review is open, the guide asks it once more as her
review (the same request line and answer rules as a due phrase), with the English note *Let's try that one again: …*. The answer is logged as a world event
`second_try_answered`, detail `recovered` (the same phrase, said correctly, with or without the card) or `not_yet`. Why these rules:

- **A talk, not the next step.** A review opened between two lesson steps would take the next step's prompt (the core evaluates the answer on the review's
  prompt). So the retry waits for a talk, and never interrupts a lesson's own turn.
- **Once per session, two waiting at most** (repetition fatigue, R02). A second "no sé" on the retry does not bring it back.
- **Never in a check.** A check is graded and changes nothing else; a check's "no sé" queues nothing.
- **No credit before the gap.** The retry's use is counted like any review answer, so the spacing gap (D-game-6) still decides any credit.
- **Not saved.** A loaded game is a new play session, and the queue belongs to the session it came from.

Not done (R02 as written in the backlog): the retry is not asked one rung lower on the support ladder, and a "miss" at a phrase is not queued (only "no sé").

```bash
# S17's route with a "no sé" at the villager's first talk, then a second talk: the villager asks the river question again with the English note (the
# console prints second_try_asked and second_try_answered); the closing card, with its summary, is in the last PNG (--size 800x600 for the small window)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session-dir /tmp/x04/session --lesson-run first-words --shot /tmp/x04/lesson.png \
  --talk guide --wav content/game-v1/speech/phrase.hola.wav \
  --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --shot /tmp/x04/stall.png --say "sí, por favor" \
  --talk villager --say "no sé" --shot /tmp/x04/no-se.png --leave \
  --talk villager --say "¿dónde está el río?" --shot /tmp/x04/second-try.png \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 --shot /tmp/x04/river.png \
  --talk guide --say "sí, por favor" --walk-to 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 \
  --screenshot /tmp/x04/summary-card.png --frames 90
```

## The book as a book, with conjugation tables (S28, D-game-19)

- **The spread.** The book is an open two-page book drawn in code (no image files, D-game-5): a leather cover with brass corners, the paper with page edges, the spine with
  its stitches, and five ribbon bookmarks along the top edge (Nouns, Verbs, Phrases, Lessons, What I said; the page on screen hangs lower and brighter). A click on a ribbon, Tab,
  Left and Right turn the page; a page turn shows a short leaf hinged at the spine (0.3 s on the frame clock) and S24's `book_page` cue plays as before. The left page lists (the
  rows, the scroll, the speaker buttons of S20b, the "Practise again" buttons of S26, the selected row); the right page shows the selected item. `BookGeometry` (engine free,
  `Frontend/BookGeometry.cs`) gives every rectangle for a window size; the layer, the mouse and the tests read the same numbers.
- **The right page.** Verbs: the verb, its English and its conjugation table, then what you said (each form with its count and its last sentence, as the S20 page listed them).
  Up and Down or a click on a verb choose it. Every other page: the selected item's own lines (a noun's counts and last sentence, a phrase, a lesson with its units and phrases,
  a line you said), written out in full on the page, wrapped instead of cut short. The selected item is the selected row (the rows with a button) or, with none, the first on screen.
- **The table** (`ConjugationTable`, `Frontend/ConjugationTable.cs`). Rows: yo, tú, él/ella/usted, nosotros, ustedes/ellos (no vosotros row). Columns: present, past (preterite),
  future (ir a) and, since V16, the simple future ("future"). A cell is **highlighted** with its count when you have said the form (the learner's record), **plain** when a lesson that is completed or under way has taught it
  (`lessons.json` `verbs`) and you have not said it yet, and a dash when it is neither. A tense column shows once a lesson has taught a form of that tense for the verb or you have
  said one (what you said is never hidden); a verb with neither gets the present alone. Every form is the core's (`ContentBundle.Morphology`, `TaughtVerbForm.Text`); the future (ir a)
  of `comer` is `ir`'s present, `a` and the infinitive (`voy a comer`), counted from the history's words in order (`ir` form, `a`, the verb), over the lines the word record counts.
- **`vamos`.** The core reads `vamos` as an imperative with no tense, so the word record says `vamos (1 pl imp)`. The table fixes that in the game: a form of `ir` whose text is `vamos` counts in `ir`'s present column,
  nosotros, whatever the core made of it (so a bare `vamos` and the one in `vamos a comer` are `ir`, present, nosotros), and `vamos a comer` is also the nosotros cell of the
  future (ir a) of `comer`. The word record itself is unchanged.
- **Flags.** `--book-verb <verb>` opens the book on the Verbs page with that verb's table; `--size <W>x<H>` sets the window (`--size 800x600` for the smallest size the book is made for).
  Everything of S20 to S22 stays: `--book`, `--book-page`, `--book-play`, `--book-practise`, `--greyscale`.

```bash
# the table of ir after the three lessons: the S17 lesson, then each later session on the previous save (a load is the next session)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --save /tmp/s28-1.save --screenshot /tmp/s28-1.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --load /tmp/s28-1.save --lesson-run personas --save /tmp/s28-2.save --screenshot /tmp/s28-2.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --load /tmp/s28-2.save --lesson-run ayer-y-manana --save /tmp/s28-3.save --screenshot /tmp/s28-3.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --load /tmp/s28-3.save --book-verb ir --screenshot /tmp/s28-ir.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --size 800x600 --load /tmp/s28-3.save --book-verb comer --screenshot /tmp/s28-comer-800.png --frames 30
```

## Book polish (X03)

- **The right page scrolls.** The wheel over the right page scrolls that page, and over the list it scrolls the list (three lines a notch, as before). Shift with Up or Down moves the right page a line; Shift with Page Up or Page Down, a page of it. The list keeps Up, Down (the speaker rows), Page Up and Page Down. A "more below" marker shows while writing lies under the page. The right page goes back to its top when the selection or the page changes (`BookModel.DetailOffset`, `DetailScroll`, `BookModel.KeepDetail`). The detail is laid out as rows (`BookLayer.DetailSheet`), so a long block (a lesson with all its units and phrases) is read to its end at 800 x 600. `--book-detail <lines>` scrolls it when the book opens (a scripted run).
- **An imperative that is a present is that present.** The core reads some forms as an imperative with no tense (S27: `vamos`, `está`, `come`). When such a form's text is also the indicative present of the same verb (the bundle's morphology), the labels and the conjugation tables read it as that present: `vamos` is "we go", `está` is "he/she/it is" (estar's 3 sg cell), `come` is "he/she/it eats" (`VerbReading`). A form with no indicative twin (`ven`) keeps its imperative label. `vamos a comer` is not labelled `we are going to …`: the labels are per form, not per sentence, and the future (ir a) column of the table already counts it.
- **ser and estar.** The bundle's glosses (`game/glosses.json`) read `to be: what` (ser) and `to be: where or how` (estar), in the list and on the table's heading. The forms are still the forms of *to be* (`VerbLabels`). The lessons say the same (S26: "Estar is “to be” for where someone is; ser ... is for what someone is").
- **A narrow row keeps its note.** A row with an English aside (a phrase) keeps its Spanish and its English whole; its right-hand note is shortened with "..." to what is left, and is left off only when less than 90 pixels would be left (`MinNoteWidth`).
- **The hint line** now reads `wheel: scroll   Shift+Up/Down: right page`.

```bash
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --size 800x600 --load /tmp/s28-3.save --book-page lessons --book-detail 9 --screenshot /tmp/x03-lessons.png --frames 30
```

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
| The beds: wind on the cliff and the heights, the river and the lake, the waterfall (louder near it), the village murmur, the evening insects with `--dusk` | beds (ambient, looping; cross-fade over 1.5 s by position, panned to the source) | synthesized (loops, seamless) |

**Ambient beds and footsteps (S25).** The beds are `AmbientBeds` (`src/Vocaterra.Game.Core/Sound/AmbientBeds.cs`): the weight of each bed is a
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
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --sound-log /tmp/route-sounds.log --lesson-run first-words ...
```

## Autosave and the crash report (X05, R04)

The game saves itself at its safe points, and a crash leaves a report instead of losing the session.

**Autosave.** `AutosavePolicy` (`src/Vocaterra.Game/Autosave.cs`, engine-free) decides the frames; the game writes them in `VoxelGame.AutosaveDue`.

- **When.** A lesson step ends (done, skipped or moved on), a lesson completes, a camp objective step is done, the session completes, a dialogue closes, the
  player sleeps at their fire (V12c: the session as it ends, with no toast; the policy then starts again as a load's does), and
  every **five minutes of play** (18000 simulation ticks at 60 a second; the wall clock is not read). Five minutes: a crash loses at most that much of play, and a save is
  a few tens of kilobytes, so writing one every five minutes costs nothing a tester notices. An event that falls inside an open dialogue waits for the
  dialogue to close (a lesson runs inside its guide's panel, so its saves come when the panel shuts).
- **Only at rest.** Never while a turn is on the worker, key words are on show, a dialogue is open, the microphone is recording, a check battery or the mic check
  is on, or the report panel is open. The save is the same document F6 writes, so `--load` reads it like any save, in the next play session.
- **Where.** In the session folder: `autosave.save` (a playtest writes `playtest.save`, the file F6 writes, so its end-of-run save and its autosave are one file).
  Its path is printed at the start (`autosave: <path> (...)`), and each write is printed (`autosave: <path> written at <reason> (session, tick, stored time, digest, bytes)`).
- **How.** The document goes to `<file>.tmp`, is flushed to the disk, and is renamed over the save (`SaveFile.Write`). A crash or a failed write before the rename
  leaves the last autosave intact; a stale `.tmp` is overwritten by the next write. The save is taken on the game loop (the state must not move during it):
  the first one builds the fresh scene it is diffed against (about 0.7 s on the valley, once per run, then kept); later saves take about 20 ms.
- **The feedback.** A small "Saved" toast, four seconds, as the other toasts show. Nothing else on the screen changes, and no save changes the world, the digest or the replay:
  a save only reads the session.
- **Off.** `--no-autosave` writes no autosave (a playtest still writes `playtest.save` at its end). Scripted and test runs write their autosave into their session folder
  (`--session-dir` to choose it); use `--no-autosave` when a run must write nothing.

**Crash report.** `Program.cs` runs the game inside a handler. A crash on the game loop, an unhandled exception on any thread, or an unobserved task exception writes
`reports/crash-s<session>-t<tick>.md` in the session folder (`crash-<local time>.md` when the state cannot be read), beside the F8 reports, with:

- the exception, its stack and its inner exceptions, and where it came out;
- the game state the handler could read (the session, the tick, the bundle, the seed, where the player stands, the objective, the lesson, whether a dialogue is open),
  each row on its own, so a row that fails says so and the others are shown;
- the last-chance save: `crash.save` in the session folder, written only when the game is at rest and no dialogue is open. It never replaces the autosave or a player's save;
- the newest 200 lines of `session.jsonl`, and the build commit.

Then the handler prints both paths (`crash: report written to ...`, `crash: last-chance save written to ...`) and the process exits with code 1. An unobserved task
exception writes a report and no save, and the game goes on. The handler never throws: a folder that cannot be made, or a read that fails, is said on the console.

`scripts/playtest-report.sh` lists the crash reports with the F8 reports (kind `Crash`). For the facilitator, see `docs/game/PLAYTEST.md` section 10.

`--debug-crash-after <tick>` is a **test-only** hook (not in `--help`): the game throws on the game loop once the simulation reaches that tick, so the crash handler
can be seen working. Nothing in a playtest uses it.

```bash
# a lesson with autosave on (the saves are printed); the session folder is the scratch folder, so the user's own sessions are not touched
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session-dir /tmp/x05/lesson --lesson-run first-words --leave --wait 2 --screenshot /tmp/x05/lesson.png --frames 30
# the autosave loads: the objective and the lesson are where the save left them
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session-dir /tmp/x05/load --load /tmp/x05/lesson/autosave.save --screenshot /tmp/x05/load.png --frames 30
# a forced crash: the report and crash.save are written and the exit code is 1
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session-dir /tmp/x05/crash --debug-crash-after 600 --screenshot /tmp/x05/crash.png --frames 900; echo "exit $?"
```

## UI scale and readable text (X08, R09)

The interface text has three sizes: 100, 125 and 150 percent (`SaveSettings.TextScaleLevels`). `--text-scale <n>` sets it for a run, **F9** steps through them in play
(F9 had no other binding, and no text box reads it), and the choice is kept in the save's settings (`textScale`; a save from before has 100). 100 is the size the layouts
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
- **Report, mic check, inventory, crafting, F1 controls, closing card:** the report shows fewer text rows and wraps its hints; the inventory uses 44 x 56 slots; the crafting station column is left out when there is no room; the controls help scrolls (Up, Down, wheel); the closing card cuts the summary with an ellipsis.

Contrast: the dim lines and the paper book's inks are in `UiPalette`, and `UiScaleLayoutTests` measures every pair (WCAG 2.x, 4.5 to 1 for body text, on the panel and where a bright sky shows through a translucent panel).
