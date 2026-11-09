# Vocaterra: the game

Vocaterra is a Minecraft-inspired voxel game, played in the first person, in which the player learns practical spoken Spanish by talking to the
people of a valley for supplies, directions and work. The design is the owner's [prototype brief](design/prototype-brief.md), summed up in
[specification.md](specification.md).

The Spanish is graded by the language core in the same repository ([docs/nlp/README.md](../nlp/README.md)): it takes what the learner said to a
verdict, feedback, a conversation step and the agent's reply. The game references the core's projects directly and copies none of its source;
[architecture.md](architecture.md) says what each side owns, [the core's API](../nlp/api.md) lists the calls and
[ADR 0009](../nlp/adr/0009-core-and-game-boundary.md) fixes the boundary. The engine is MonoGame (D-game-1 in [decisions.md](decisions.md); the
final engine is open), and all engine code lives in `src/Vocaterra.Game`. The console host, `src/Vocaterra.Host`, is the cheapest way to try the
core's calls and the content.

## The prototype scenario

The prototype is the Minecraft-inspired first-person voxel slice in [docs/game/design/prototype-brief.md](design/prototype-brief.md) (the owner's Prototype
Design Brief, v1.2); its plan, milestone by milestone, is [docs/game/scenario-plan.md](scenario-plan.md). The scenario takes 10 to 15 minutes beside the
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
lines for the 3D world are in [running.md](running.md).

**Later sessions.** A sleep at the player's own fire (V12c: [lessons.md](lessons.md), "The next session: sleep at the fire") begins the next day, a load keeps the day (V25), and each later
session unlocks one lesson: sessions 2 and 3 at the villager and the stall (S26,
S27), and since phase A of [docs/game/village-plan.md](village-plan.md) (D-game-22) sessions 4 to 6 in a village grown inside its own clearing, with four
more people: the fisher Mar on the bridge's east landing (session 4, "El pueblo": places and the way to them), the baker Luz at the bakery (session 5,
"Números y pan": numbers, prices and bread for coins), the smith Domingo at the smithy and the farmer León at the wheat field (session 6, "Gente del
pueblo": names, jobs and someone else in three times). Coins, apples and an axe are items, and four orders pay in them. Phase B adds sessions 7 to 10,
all at A2, with the same seven people: the farmer's "¿Cómo es?" (adjectives and `gustar`), the villager's "Ayer en el pueblo" (the past in every person),
the baker's "Mañana en el pueblo" (the simple future) and the baker's "El mercado" (11 to 20, totals and selling); the series ends there ([lessons.md](lessons.md), "The village:
sessions 4 to 10"). The first session needs none of this: the four stand in the world and answer when talked to.

## Layout

The game is one half of the repository: the projects below, `content/game-v1`, `assets/` and these documents. The shared solution, build settings, package versions and scripts are at the root
(see the [root README](../../README.md)).

```
Directory.Build.props            (at the root) the shared build settings, with the game's Linux-only native trim
src/Vocaterra.Game.Core/         the game's engine-free systems: the voxel world and its 60 Hz tick, the session, the guide's turns, the
                                 language systems, the screens' models and their keys (no packages, no engine; docs/game/architecture.md)
src/Vocaterra.Game/              the MonoGame DesktopGL front-end: the window, the drawing, the devices, the UI's layers (the only engine code)
src/Vocaterra.Host/              the console host: loads the bundle, evaluates lines, runs the text loop
tests/                           one test project per source project (Game.Core, Game, Host), their folders mirroring the sources'
tools/Vocaterra.SpeechRender/    the speech prerender tool
content/game-v1/                 the game's content bundle (content.md)
assets/                          CC0 art, fonts and sounds, each listed in assets/ATTRIBUTION.md (sounds: assets/audio/SOURCES.md)
docs/game/                       this README, the guides below, the specification, architecture, plans and decisions (docs/README.md lists them)
scripts/content.sh               validate, hash and check a bundle with the core's content tool
scripts/merge-gate.sh            the orchestrator's merge gate (the whole repository)
.claude/agents/orchestrator.md   the orchestrator's role file (how the repository is developed)
```

## Quick start

From the repository root. The .NET SDK is pinned in `global.json`; a user-local install is enough.

```bash
dotnet build Vocaterra.sln -warnaserror     # the core and the game in one solution
scripts/test.sh fast                        # the quick tier; scripts/test.sh full is what the gate and CI run
export VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
scripts/fetch-models.sh vosk-model-small-es-0.42 kokoro-multi-lang-v1_0    # the speech models, once
dotnet run --project src/Vocaterra.Game -- --seed 42                       # the game; add --quality low on software GL
dotnet run --project src/Vocaterra.Host -- --transcript "quiero agua, por favor" --prompt stall-need    # one line through the core
```

Without the models the game still runs, but nothing spoken is heard and the model-gated tests are skipped. The details are in
[building and testing](building-and-testing.md).

## Guides

| Guide | What it covers |
| --- | --- |
| [Building, testing and CI](building-and-testing.md) | The build, the test tiers, the speech models, the native libraries, the host, CI and its timed tests |
| [Running the game](running.md) | The 3D world, its options and demo runs, screenshots, saving and loading, the checks, the playtest |
| [Content and the grammar check](content.md) | The bundle `content/game-v1`, its gate, the grammar check and refreshing its cache |
| [Lessons and sessions](lessons.md) | The English guidance, the guide's lesson, verbs and tenses, the village's sessions 4 to 10, sleeping into the next session, the summary card and the second try |
| [The book](book.md) | The learner's book, its pages, replay, the conjugation tables |
| [Saves](saves.md) | Saves across content updates, autosave and the crash report |
| [Sound and display](sound-and-display.md) | The audio system and sounds, UI scale and readable text |
| [playtest](playtest.md), [scripted-runs](scripted-runs.md) | The facilitator's script; the scripted runs, the front-end's regression set |
| [content/README.md](../../content/README.md) | Editing the content: the edit loop, lessons and their level |

The milestones are in [implementation-plan.md](implementation-plan.md), the findings log in [findings.md](findings.md), the owner's decisions in [decisions.md](decisions.md),
and the series plans are listed in the [docs index](../README.md). The village series' open points are in [village-plan.md](village-plan.md) section 5.

## Licence

- **This repository, the game and the language core alike:** Apache-2.0 (see [LICENSE](../../LICENSE) and [NOTICE](../../NOTICE)). The game was MIT until the owner relicensed the whole repository in R01 (D-game-2 superseded; recorded in [decisions.md](decisions.md)).
- **Content bundle:** the Wiktionary lexicon in `content/game-v1` stays CC BY-SA 4.0; its notices are in
  [content/game-v1/ATTRIBUTION.md](../../content/game-v1/ATTRIBUTION.md).
- **Language core:** the `Vocaterra.Nlp.*` projects are Apache-2.0 too (core D3, [LICENSE](../../LICENSE)). **MonoGame** is Ms-PL.
- **Art, font and sound:** CC0 only (D-game-5), each file listed in [assets/ATTRIBUTION.md](../../assets/ATTRIBUTION.md).
- **Native libraries:** a build that includes the sherpa-onnx native libraries (GPL-3.0 code, core D2) is not distributed while D2 is open.
