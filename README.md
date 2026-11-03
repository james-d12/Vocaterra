# Vocaterra

A Spanish-learning game and the language core that grades what the player says. One repository, one solution, with two distinct components: **the game** and **the NLP language core**.

### The Game

The game is a **Minecraft-inspired 3D voxel game**, played from a first-person perspective. The player explores the world and speaks Spanish with villagers to get supplies, ask for directions, complete activities and progress.

The game is split across `Vocaterra.Simulation`, `Vocaterra.Game`, `Vocaterra.MonoGame`, `Vocaterra.Host` and `Vocaterra.SpeechRender`, keeping the underlying simulation and game logic separate from the MonoGame rendering and host layers.

Game design, implementation plans and decisions live under [`docs/game/`](docs/game/README.md).

### The NLP Language Core

The language core is the engine-agnostic system responsible for understanding and evaluating what the player says. It covers **audio, speech recognition, phonology, pronunciation, language analysis, evaluation, decision-making, responses and content tooling**.

It is implemented by the `Vocaterra.Nlp.*` projects and is designed to remain deterministic and independently testable, including golden tests and fuzz testing. The game consumes these projects directly, but the core does not depend on the game or MonoGame.

NLP architecture, implementation plans and decisions live under [`docs/nlp/`](docs/nlp/README.md).

The whole repository is licensed under the Apache License 2.0 ([`LICENSE`](LICENSE), [`NOTICE`](NOTICE)). Third-party data and models retain their own licences, listed in `NOTICE` and in the bundles' `ATTRIBUTION.md`.

The boundary between the language core and the game is defined in [`docs/nlp/adr/0009-core-and-game-boundary.md`](docs/nlp/adr/0009-core-and-game-boundary.md), while the APIs exposed to hosts are documented in [`docs/nlp/api.md`](docs/nlp/api.md).

Until 2026-10-21 the two components were maintained as separate repositories, and until R01 they remained separate sibling folders (`nlp/` and `game/`).

The game's prototype is the Minecraft-inspired first-person voxel slice described in [`docs/game/design/prototype-brief.md`](docs/game/design/prototype-brief.md) (the owner's Prototype Design Brief, v1.2), planned milestone by milestone in [`docs/game/scenario-plan.md`](docs/game/scenario-plan.md).

The [documentation index](docs/README.md) links the guides, plans, decisions, QA reports and [session handovers](docs/handovers/README.md).

## Layout

```text
Vocaterra.sln              every project (solution folders src, tests, tools)
Vocaterra.Nlp.slnf         the core alone (a solution filter; the licence scan and the core-only build use it)
Directory.Build.props      build settings: shared, then the groups keyed on the project name (Vocaterra.Nlp.* packing; the game's native trim)
Directory.Packages.props   one version list for both halves
nuget.config               nuget.org only
src/                       Vocaterra.Nlp.* (the core's libraries) and Vocaterra.Game.Core, .Game, .Host (the game)
tests/                     one test project per project; tests/golden is the core's golden learner-audio manifest; tests/Shared has the two source files (bundle locator, temp-copy helper) linked into the core's test projects
tools/                     Vocaterra.Nlp.{Cli,Bench,ContentBuilder}, Vocaterra.SpeechRender, manifest.json (the pinned grammar-check tools)
content/                   game-v1 (the one content bundle; the core runs on it too), grammar/ (the grammar-check caches), PHRASES.md, speech-index.json
assets/                    the game's CC0 art, fonts and sounds
models/                    the pinned speech-model manifest (the model files are fetched and ignored)
docs/nlp/, docs/game/      each half's plan, decisions, README and documents
docs/handovers/, research/, qa-reports/, archive/   dated records: session handovers, studies, bug reports, superseded documents
scripts/                   merge-gate.sh, content.sh, limited.sh, fetch-models.sh, license-scan.sh, playtest and speech scripts
.claude/agents/            the orchestrator's role file (the entry point for a Claude Code session; see CLAUDE.md)
.github/workflows/         ci.yml (build, test and the grammar checks)
```

## Build

The .NET SDK is pinned in `global.json` (10.0.402, `latestPatch`); a user-local install under `~/.dotnet` is enough.

```bash
dotnet build Vocaterra.sln -warnaserror
dotnet test Vocaterra.sln            # the core's unit, golden and fuzz suites and the game's tests
dotnet run --project src/Vocaterra.Game
```

Tests that need the speech models read `VOCATERRA_MODELS_DIR` and are skipped without it; `scripts/fetch-models.sh` fetches the pinned models (`models/manifest.json`).

`dotnet build Vocaterra.Nlp.slnf` builds the language core alone.

## Working on it

`CLAUDE.md` is the entry point for a Claude Code session.

Each component keeps its own implementation plan:

- `docs/nlp/implementation-plan.md`
- `docs/game/implementation-plan.md`

and its own owner decisions:

- `docs/nlp/decisions.md`
- `docs/game/decisions.md`

The IDs in those documents are unchanged from the separate repositories.

The shared content bundle, `content/game-v1`, is edited with `scripts/content.sh`:

```bash
scripts/content.sh validate
scripts/content.sh lint
scripts/content.sh hash
scripts/content.sh cefr-check
```

The language core's tests and demo CLI also run against this bundle. As a result, changing the game's language content re-runs the relevant core validation and test suite.

See [`docs/nlp/changing-the-core.md`](docs/nlp/changing-the-core.md) for the core's rules for a change and its recipes.