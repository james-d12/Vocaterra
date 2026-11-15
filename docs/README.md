# Documentation

The documents are sorted two ways. **Living documents** describe the project as it is now and sit with their half: `game/`, `nlp/`
(the language core) and `process/` (how we work on both). **Records** are dated, written once and kept as evidence; they sit by kind:
`handovers/`, `research/`, `qa-reports/` and `archive/`. Where a new document goes, and how one moves: [the documentation
rules](process/documentation.md).

## Start here

| Need | Read |
| --- | --- |
| Build and repository overview | [Root README](../README.md) |
| Resume work: the open handovers | [handovers/](handovers/README.md) |
| What is open: owner decisions, planned work, known gaps | [open-items](open-items.md) |
| Game setup and usage | [Game guide](game/README.md) |
| Language core setup and usage | [NLP guide](nlp/README.md) |
| Game milestones and owner decisions | [Implementation plan](game/implementation-plan.md), [decisions](game/decisions.md) |
| Core milestones and owner decisions | [Implementation plan](nlp/implementation-plan.md), [decisions](nlp/decisions.md) |
| Architecture and the public boundary | [Game architecture](game/architecture.md), [core API](nlp/api.md), [boundary ADR](nlp/adr/0009-core-and-game-boundary.md) |
| Authoring game content | [Content guide](../content/README.md), [phrase sheet](../content/PHRASES.md) |
| Coding and test conventions | [Code guidelines](process/code-guidelines.md), [test performance](process/test-performance-plan.md), [changing the core](nlp/changing-the-core.md) |
| Bugs found by QA and their status | [qa-reports/](qa-reports/README.md) |
| Studies and reviews, with their outcomes | [research/](research/README.md) |
| Superseded documents | [archive/](archive/README.md) |

## Layout

```text
docs/
  README.md        this index
  open-items.md    one page of what is open, pointing into the documents below
  handovers/       open session handovers; done/ holds the finished and replaced ones
  research/        studies and reviews for both halves, one dated file (or folder) each
  qa-reports/      bug reports from QA passes and reviews, dated, with a triage table of every bug
  archive/         superseded documents, frozen, at their original path under archive/
  process/         how we work: code guidelines, test performance, the documentation rules
  game/            the game, as it is now
    design/        the owner's design inputs; sources/ holds the original Word and OpenDocument files
  nlp/             the language core, as it is now; adr/ and diagrams/
```

Documents beside the code or data they describe stay there: `content/README.md`, `content/PHRASES.md`, `scripts/README.md`,
`models/README.md` and the READMEs under `src/` and `tests/`.

## The living documents

**Game (`game/`)**

| Document | What it is |
| --- | --- |
| [README](game/README.md) | What the game is, its layout and a quick start |
| [building-and-testing](game/building-and-testing.md) | The build, the test tiers, the speech models, the host, CI and its timed tests |
| [running](game/running.md) | The 3D world, its options and demo runs, saving and loading, the checks, the playtest |
| [content](game/content.md) | The content bundle, its gate and the grammar check |
| [lessons](game/lessons.md), [book](game/book.md) | How the game teaches: the lessons and sessions; the learner's book |
| [saves](game/saves.md), [sound-and-display](game/sound-and-display.md) | Saves across updates, autosave, the crash report; sounds, UI scale |
| [specification](game/specification.md) | The game in one paragraph, with its design, plan and decision |
| [architecture](game/architecture.md) | The game's projects and folders, and what may depend on what |
| [implementation-plan](game/implementation-plan.md), [findings](game/findings.md) | Section 4 the milestone tables; the findings log ("section 5") in its own file |
| [decisions](game/decisions.md) | The owner's decisions, D-game-1 onward |
| [scenario-plan](game/scenario-plan.md) | The S series, the prototype scenario (done) |
| [content-plan](game/content-plan.md) | The C series, content as data (closed 2026-10-29; C09 and C10 wait for the owner) |
| [village-plan](game/village-plan.md) | The V series, the village and its lessons |
| [settlement-plan](game/settlement-plan.md) | The inn, purchased land and the campsite: the proposal, its review and the Y series (the first slice, Y00 to Y14, on `feature/settlement` until its pull request); a larger village and map as later phases |
| [water-plan](game/water-plan.md) | The H series, flowing water, swimming, breath and the view under water (built 2026-11-07 to 2026-11-08; in `main`) |
| [weather-plan](game/weather-plan.md) | The E series, a day and night cycle and modular weather: sunny, cloudy and rain, and the NPCs' weather answer (built 2026-11-10 to 2026-11-11 on `feature/weather`, PR #64; D-game-31) |
| [activities-plan](game/activities-plan.md) | The W series, activities and the core systems (planned 2026-11-01; W01 to W10 built, W11 onward open; D-game-23 in part and D-game-24 decided 2026-11-03; implementation scope in the adaptation design, section 17.3) |
| [playtest](game/playtest.md) | The facilitator's script for playtests |
| [scripted-runs](game/scripted-runs.md) | The scripted runs, the front-end's regression set |
| [speech-checklist](game/speech-checklist.md) | The listening table, generated by `tools/Vocaterra.SpeechRender` |
| [design/](game/design/prototype-brief.md) | The owner's [prototype brief](game/design/prototype-brief.md), the [crafting and cooking principle](game/design/crafting-and-cooking.md), the [adaptation design](game/design/sources/Vocaterra_Adaptation_Design_Document.docx) (Word only; current specification: player needs, lessons as optional classes, the bread pilot, the presentation track and open owner decisions), the owner's [design notes](game/design/sources/Vocaterra_Design.odt) (OpenDocument: thirst, needs and attributes, in progress) and the Word sources |

**Language core (`nlp/`)**

| Document | What it is |
| --- | --- |
| [README](nlp/README.md) | What the core is, a quick start, the determinism rules |
| [architecture](nlp/architecture.md) | The stages and their dependencies, using the core from a host, the repository layout |
| [building-and-testing](nlp/building-and-testing.md) | Prerequisites, build, tests, the fuzz suite, the benchmark |
| [cli-examples](nlp/cli-examples.md) | The demo CLI with and without models, worked runs, replay logs, the content builder |
| [known-gaps](nlp/known-gaps.md) | What the core does not do yet |
| [api](nlp/api.md) | The public types, the boundary with the game |
| [pipeline](nlp/pipeline.md), [replay](nlp/replay.md), [lexicon](nlp/lexicon.md) | How an utterance is processed, replayed, and how the lexicon is built |
| [tools](nlp/tools.md) | The third-party tools, libraries, models and data sources |
| [changing-the-core](nlp/changing-the-core.md) | The rules for a change to the core and recipes for error tags, content, golden clips and model adapters |
| [fuzzing-plan](nlp/fuzzing-plan.md) | The fuzz suite's design (built at M16) |
| [implementation-plan](nlp/implementation-plan.md), [findings](nlp/findings.md) | §5 the milestone table; the findings log ("§5b") in its own file |
| [decisions](nlp/decisions.md) | The owner's decisions, D1 onward |
| [adr/](nlp/adr/0009-core-and-game-boundary.md) | Architecture decision records 0001 to 0012 |
| [package-readme](nlp/package-readme.md) | The NuGet package's readme (its path is in `Directory.Build.props`) |
