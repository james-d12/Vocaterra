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
Design Brief, v1.2); its plan, milestone by milestone, is [docs/SCENARIO_PLAN.md](docs/SCENARIO_PLAN.md). The scenario takes 10 to 15 minutes:

- **Forest camp.** Gather wood and berries from the trees and bushes, then craft a torch at the workbench.
- **The stall.** The shopkeeper asks `¿Qué necesitas?`; the player answers `Quiero agua, por favor.` The shopkeeper offers `¿Quieres pan?`, answered
  with `Sí, por favor.` or `No, gracias.`
- **The villager and the river.** The player asks `¿Dónde está el río?`, follows the directions to the river and fills a bottle.
- **The transfer moment.** After the objective, one phrase is revisited without the original prompt, or with a different NPC or context.

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
assets/                          CC0 art, fonts and sounds, each listed in assets/ATTRIBUTION.md
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
three houses and the signpost `Tienda / Río`, and the player's own camp on a cliff ledge at the top of a switchback trail) at `--seed` (default 42; the
seed changes the land between the sites and the forest, never a site), and shows it from the player's eyes. A new game starts at the player's camp
(facing south-east, a tent and a cold fire ring beside it) with the beacon on the guide's camp: the first thing to do is the walk down the trail.
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
journal, **F3** the debug overlay. **Aim the crosshair at the guide's figure**: the hint `E: hablar` shows, and **E** (or **T** to type, or **V**
to talk: hold it, the clip goes when it is let go) opens the dialogue panel, which takes the keyboard and frees the mouse. **The panel has
speech buttons (S7)**: `[R] Repetir`, `[S] Más despacio`, `[H] Ayuda`, `[N] No sé` and `[W] Escribir` (keys or the mouse); hold **V** or **Space** to talk;
`Escribir` (W or Tab) opens the text box, where Enter sends and Tab or Esc go back to the buttons; Esc cancels a turn that is "Pensando…", then closes the
panel. After a spoken turn the line `Oí: «…» · Esperaba: «…»` stays under the conversation; when the NPC offers choices they are buttons too (keys 1 to 3).
A mic check (`Di: hola`, a level meter, what was heard) opens an interactive run (`--no-mic-check` skips it, `--mic-check` forces it). Her answers point the beacon at the named noun's nearest block (the arrow at the screen edge when it is off
screen); "¿qué es esto?" names the block under the crosshair. **The shopkeeper and the villager are talked to the same way** (S6): aim at any figure, `E: hablar`; the panel's title is the NPC's name.

**The camp objective (S4).** The checklist top right, `Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1`, is completed by the
game's events and the inventory, never by a language score: five trunks, three berries, the torch placed at the fire ring, and the water: the shopkeeper hands over an empty bottle (`la botella`), and holding the left button on the river with it in the
inventory fills it (half a second; without a bottle a toast says `Necesitas una botella.` and nothing is blocked). Each step that completes shows a toast. The first time an item enters the inventory (a pickup, a craft)
its word card shows, `la madera`, `la fruta`, `la antorcha`, with an empty audio slot for S9; the word counts as heard and read, never as a quiz.

To check it without playing, script it and save a screenshot (needs a display; every line also prints what its step did):

```bash
# the camp (S4): gather, craft at the workbench, place the torch on the fire ring's rim, look at it: the checklist reads 3/4
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --walk-to 176,186 --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 --walk-to 176,184 --look 180,-15 --screenshot /tmp/s4-camp.png
# the guide answers "where is the river?": the console prints the verdict, her line and the arrow; the PNG shows the panel and the arrow
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --look 120,-5 --say "¿dónde está el río?" --screenshot /tmp/s3-river.png --frames 300
# the village: walk to the square's north side and look at the stall: the shopkeeper behind the counter facing the player, with his label
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --beacon none --teleport 282,211 --look 90,-8 --screenshot /tmp/s3-stall.png --frames 150
# the signpost, from the east side of the path: the label "Tienda / Río" over the board
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --beacon none --teleport 283,221 --look 270,-6 --screenshot /tmp/s3-sign.png --frames 150
# the camp from the player's eyes at spawn: the guide's figure and label, the beacon on the river (an arrow at the edge when it is off screen)
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --look 120,-5 --beacon river --screenshot /tmp/s2-camp.png --frames 240
# S6, the whole route in one run: the camp, the stall (the shopkeeper's "¿Qué necesitas?" is a core host turn, the bottle comes with his answer),
# the villager (the beacon on the river), the river (hold the button on the water: the bottle fills, the checklist reads 4/4), then back to the
# guide for her transfer offer "¿Quieres agua?" and to the villager for bread. The console prints each turn, each game event (phrase uses,
# world events) and the objective's steps; the PNGs show the stall exchange and the river after the fill
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 \
  --walk-to 176,186 --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --shot /tmp/s6-stall.png --say "sí, por favor" \
  --talk villager --say "¿dónde está el río?" \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 --shot /tmp/s6-river.png \
  --talk guide --say "sí, por favor" --talk villager --say "quiero pan, por favor" \
  --screenshot /tmp/s6-end.png --frames 60
# the host's stall prompt (no display): the request is understood
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
# the stall's key words, then the question with the card (the two PNGs), a use, ten minutes later the guide asks for it, the journal's phrases page
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --typed-credit full --no-speech --talk shopkeeper --shot /tmp/s8-intro.png --wait 3 --shot /tmp/s8-stall.png \
  --say "quiero agua, por favor" --skip 660 --talk guide --say "quiero agua, por favor" --journal-phrases --screenshot /tmp/s8-journal.png --frames 60
```

**Save, sessions and resume (S10).** `--save <file>` writes the game when the run ends (on quit, or at the end of a script), and **F5** writes it in an
interactive run (to the `--save` file, or `vocaterra.save.json` in the working folder); a turn still thinking means "not now", and a quit waits up to five
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
# transfer's --leave, in the order the console prints as "check order:"; see the S11 lines above for the shape of a check's answers)
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --playtest --seed 42 --session-dir /tmp/playtest1 \
  --pick 3 --pick 2 --pick 2 --say "¿Dónde está el río?" --pick 1 --say "No, gracias." --pick 3 --say "Quiero agua, por favor." --say "Sí, por favor." --pick 3 \
  --walk-to 176,186 --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" --talk villager --say "¿dónde está el río?" \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 --talk guide --say "sí, por favor" --leave \
  --say "Sí, por favor." --say "No, gracias." --pick 1 --pick 2 --pick 2 --say "¿Dónde está el río?" --say "Quiero agua, por favor." --pick 1 --pick 3 --pick 1 \
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
```

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
[src/Vocaterra.Game/README.md](src/Vocaterra.Game/README.md)). A data-driven lesson runner (`content/game-v1/game/lessons.json`) ships with a two-step sample at the
guide (L starts it, Enter continues, K skips a step, H helps). A scripted run through it prints each step and its outcome:

```bash
DISPLAY=:0 dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --mute --talk guide --lesson sample --say hola --say gracias --screenshot /tmp/s16.png --frames 60
```

