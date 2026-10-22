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

The prototype's world is the first-person voxel scene: it loads the prototype route from the bundle
(`content/game-v1/game/scene.json`, a 96 x 32 x 96 block world: the forest clearing with the camp, the workbench, the fire ring and the guide, the
path south-east, the village with the stall and its shopkeeper behind the counter, the villager on the square, three houses, the signpost
`Tienda / Río`, the river and its bridge) at `--seed` (default 42), and shows it from the player's eyes. (`--voxel`, which selected it until S4, is
accepted and ignored.)

```bash
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42
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
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --interact 17,11,15 --interact 15,11,19 \
  --walk-to 17,20 --interact 16,11,23 --walk-to 11,17 --interact 9,11,17 --interact 9,12,17 --interact 9,13,17 --interact 9,14,17 \
  --walk-to 11,11 --interact 11,11,9 --walk-to 20,17 --craft antorcha \
  --walk-to 20,19 --place antorcha@21,12,21 --walk-to 22,19 --look 180,-15 --screenshot /tmp/s4-camp.png
# the guide answers "where is the river?": the console prints the verdict, her line and the arrow; the PNG shows the panel and the arrow
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --look 120,-5 --say "¿dónde está el río?" --screenshot /tmp/s3-river.png --frames 300
# the village: walk to the square's north side and look at the stall: the shopkeeper behind the counter facing the player, with his label
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --walk-to 62,52 --look 180,-12 --screenshot /tmp/s3-stall.png --frames 150
# the signpost, from the east side of the path: the label "Tienda / Río" over the board
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --walk-to 64,53 --look 262,-8 --screenshot /tmp/s3-sign.png --frames 150
# the camp from the player's eyes at spawn: the guide's figure and label, the beacon on the river (an arrow at the edge when it is off screen)
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --look 120,-5 --beacon river --screenshot /tmp/s2-camp.png --frames 240
# S6, the whole route in one run: the camp, the stall (the shopkeeper's "¿Qué necesitas?" is a core host turn, the bottle comes with his answer),
# the villager (the beacon on the river), the river (hold the button on the water: the bottle fills, the checklist reads 4/4), then back to the
# guide for her transfer offer "¿Quieres agua?" and to the villager for bread. The console prints each turn, each game event (phrase uses,
# world events) and the objective's steps; the PNGs show the stall exchange and the river after the fill
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 \
  --interact 17,11,15 --interact 15,11,19 --walk-to 17,20 --interact 16,11,23 \
  --walk-to 11,17 --interact 9,11,17 --interact 9,12,17 --interact 9,13,17 --interact 9,14,17 --walk-to 11,11 --interact 11,11,9 \
  --walk-to 20,17 --craft antorcha --walk-to 20,19 --place antorcha@21,12,21 \
  --talk shopkeeper --say "quiero agua, por favor" --shot /tmp/s6-stall.png --say "sí, por favor" \
  --talk villager --say "¿dónde está el río?" \
  --walk-to 62,79 --interact 62,7,80 --look 180,-14 --shot /tmp/s6-river.png \
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
includes them until D2 is settled in the core.

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
