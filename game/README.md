# Vocaterra

Vocaterra is a Terraria-like game in which learning Spanish is one of the main pillars: exploring, progression and looting, with speech and
dynamic conversation in Spanish. The game design is in [docs/SPECIFICATION.md](docs/SPECIFICATION.md).

The Spanish language processing is the language core **Vocaterra.NLP**, the sibling folder [`nlp/`](../nlp/README.md) of this repository
(Apache-2.0). It takes a learner's utterance to a verdict (what they meant, how well they said it), feedback, a conversation step and the
agent's response text. The game references the core's projects directly and copies nothing of its source.
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) says what the game takes from the core and what the game owns.

The prototype is the 3D scenario in the next section. The engine for it is MonoGame (decision D-game-1 in [docs/DECISIONS.md](docs/DECISIONS.md);
the final engine is open), and all engine code lives in `src/Vocaterra.MonoGame`. The console host stays as the cheapest way to exercise the
core's calls and the content. The 2D slice (P1 to P7), retired at S4, was a small Terraria-like slice: you could walk through a generated
forest, meet the guide, type Spanish to her (`¿dónde está el árbol?`) and follow the arrow she puts out; see "The guide, typed" below.

## The prototype scenario

The prototype is the Minecraft-inspired first-person voxel slice in [docs/PROTOTYPE_BRIEF.md](docs/PROTOTYPE_BRIEF.md) (the owner's Prototype
Design Brief, v1.2); its plan, milestone by milestone, is [docs/SCENARIO_PLAN.md](docs/SCENARIO_PLAN.md). The scenario takes 10 to 15 minutes:

- **Forest camp.** Gather wood and berries from the trees and bushes, then craft a torch at the workbench.
- **The stall.** The shopkeeper asks `¿Qué necesitas?`; the player answers `Quiero agua, por favor.` The shopkeeper offers `¿Quieres pan?`, answered
  with `Sí, por favor.` or `No, gracias.`
- **The villager and the river.** The player asks `¿Dónde está el río?`, follows the directions to the river and fills a bottle.
- **The transfer moment.** After the objective, one phrase is revisited without the original prompt, or with a different NPC or context.

These are the brief's five target phrases. Nothing in the scenario ends by a language mistake: no death, no timers, no language gate. The run
lines for the 3D build are in "3D (S2)" below and grow as S3 onward land; the 2D run lines after them are the 2D slice (P1 to P7), retired at S4.

## Layout

This folder, `game/`, is one half of the repository; the shared solution, build settings, package versions and scripts are at the root
(see the [root README](../README.md)).

```
Directory.Build.props            the game's additions to the shared build settings (the Linux-only native trim)
src/Vocaterra.Simulation/        the world and the player body: seeded generation, tiles in chunks, a fixed 60 Hz tick (no packages, no engine)
src/Vocaterra.Game/              the game session, the guide's typed turns and the facts about the world (the core's projects, no engine)
src/Vocaterra.MonoGame/          the MonoGame DesktopGL front-end: window, drawing, input, the dialogue panel (the only engine code)
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

## 3D (S2, S3)

The first-person voxel scene runs beside the 2D slice (which stays the default until S4): add `--voxel`. It loads the prototype route from the
bundle (`content/game-v1/game/scene.json`, a 96 x 32 x 96 block world: the forest clearing with the camp, the path south-east, the village with
the stall and its shopkeeper behind the counter, the villager on the square, three houses, the signpost `Tienda / Río`, the river and its bridge)
at `--seed` (default 42) and shows it from the player's eyes.

```bash
dotnet run --project game/src/Vocaterra.MonoGame -- --voxel --seed 42
```

WASD or the arrows walk, Space jumps, the mouse looks (Esc lets the mouse go, a click takes it back), **hold the left button** on a trunk or a
berry bush to gather it (`madera`, `fruta`), the right button places the selected item, `1`..`0` or the wheel pick the hotbar slot, **I** the
inventory, **J** the journal, **F3** the debug overlay. **Aim the crosshair at the guide's figure**: the hint `E: hablar` shows, and **E** (or
**T** to type, or **V** to talk: hold it, the clip goes when it is let go) opens the dialogue panel, which takes the keyboard and frees the mouse.
Enter sends, Esc closes, Tab holds push-to-talk. Her answers point the beacon at the named noun's nearest block (the arrow at the screen edge
when it is off screen); "¿qué es esto?" names the block under the crosshair. Other NPCs cannot be talked to yet (S6), and crafting is S4.

To check it without playing, script it and save a screenshot (needs a display; every line below also prints what the step did):

```bash
# the guide answers "where is the river?": the console prints the verdict, her two lines and the arrow; the PNG shows the panel and the arrow
dotnet run --project game/src/Vocaterra.MonoGame -- --voxel --seed 42 --look 120,-5 --say "¿dónde está el río?" --screenshot /tmp/s3-river.png --frames 300
# the village: walk to the square's north side and look at the stall: the shopkeeper behind the counter facing the player, with his label
dotnet run --project game/src/Vocaterra.MonoGame -- --voxel --seed 42 --walk-to 62,52 --look 180,-12 --screenshot /tmp/s3-stall.png --frames 150
# the signpost, from the east side of the path: the label "Tienda / Río" over the board
dotnet run --project game/src/Vocaterra.MonoGame -- --voxel --seed 42 --walk-to 64,53 --look 262,-8 --screenshot /tmp/s3-sign.png --frames 150
# the camp from the player's eyes at spawn: the guide's figure and label, the beacon on the river (an arrow at the edge when it is off screen)
dotnet run --project game/src/Vocaterra.MonoGame -- --voxel --seed 42 --look 120,-5 --beacon river --screenshot /tmp/s2-camp.png --frames 240
# walk to a tree, gather its four trunk blocks and look down: the trunk is gone and madera is in the hotbar
dotnet run --project game/src/Vocaterra.MonoGame -- --voxel --seed 42 --walk-to 11,17 --interact 9,11,17 --interact 9,12,17 --interact 9,13,17 --interact 9,14,17 --look 270,-45 --screenshot /tmp/s2-gather.png
# the host's stall prompt (no display): the request is understood
dotnet run --project game/src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need
```

The steps `--look yaw,pitch` (degrees, yaw 0 north and 90 east), `--walk-to x,z`, `--interact x,y,z`, `--place item@x,y,z`, `--select n` and
`--say <line>` (walk to the guide, face her, open the panel, type and send each line, and wait for her reply) run in the order given;
`--beacon <point>` stands the beacon column on a named point of the scene. Details are in `src/Vocaterra.MonoGame/README.md`.

## The guide, typed

> **The 2D slice (P1 to P7), retired at S4.** The run lines in this section are for the 2D slice.

The smallest vertical slice that proves the core inside the game loop (milestone P3). From the repository root, with a display:

```bash
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42
```

Walk with A and D (Space jumps) to the guide, the figure in the purple robe. When "Pulsa T para hablar" shows, press **T**: the dialogue panel
opens and takes the keyboard, so the movement keys type letters. Type a Spanish line and press **Enter**; **Esc** closes the panel. Without
an accent key, hold **Alt** with a vowel for `á é í ó ú`, **Alt+N** for `ñ`, **Alt+/** for `¿` and **Alt+1** for `¡` (accents are optional:
`dónde esta el arbol` is understood too). Try `¿dónde está el árbol?`, `¿dónde hay piedra?`, `¿qué es esto?` (with the mouse on something),
`ayuda`, `hola`, `gracias`, `adiós`. The panel shows your line, a note with the core's verdict, the guide's correction when you made a mistake,
her reply and, for a question about where something is, the game's own line with the direction and distance (`El árbol está a la derecha, a
doce bloques.`) while an arrow points at the tile until you reach it or press **Esc** with the panel closed.

To check it without typing, the front-end can script the whole thing and save a screenshot (see `src/Vocaterra.MonoGame/README.md`):

```bash
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --say "¿dónde está el árbol?" --screenshot /tmp/p3.png --frames 240
```

## The guide, spoken (P5)

> **The 2D slice (P1 to P7), retired at S4.** The run lines in this section are for the 2D slice.

Hold **Tab** while the dialogue is open, or hold **V** next to the guide (it opens the panel and listens), say the line, and let go: the clip is
trimmed, converted to 16 kHz, recognized by Vosk and evaluated like a typed line, on a worker thread, so the game never stalls ("Escuchando…",
then "Pensando…" in the panel). A spoken correct use earns full credit (three uses make a word known); a typed one earns half (six),
`--typed-credit full` gives typed input full credit again. Without a microphone the panel says so and typing works.

It needs the speech models, fetched outside the repository (`VOCATERRA_MODELS_DIR` names the folder; the core's `scripts/fetch-models.sh`
fills it with `vosk-model-small-es-0.42` and `kokoro-multi-lang-v1_0`). Without them the game starts and runs, typed only, and says why:

```bash
export VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42
```

Spoken input without a microphone, for demos and tests (each is an ordered script step like `--say`; they print the transcript, the
confidence and the timings in the turn line):

```bash
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --speak "¿dónde está el árbol?" --screenshot /tmp/p5.png   # Kokoro speaks it, Vosk hears it
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --wav line.wav --screenshot /tmp/p5-wav.png               # a 16-bit PCM WAV, any rate
dotnet run --project game/src/Vocaterra.MonoGame -- --seed 42 --listen 3 --screenshot /tmp/p5-mic.png                   # the real microphone for 3 s
```

Kokoro's audio is cached under `~/.cache/vocaterra/tts` (nothing of it is in the repository). See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
for the worker thread, the models folder and the native libraries.

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
