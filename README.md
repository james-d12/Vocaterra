# Vocaterra

A Spanish-learning game and the language core that grades what the player says. One repository, one solution, two halves told apart by project name:

| Half | What it is | Projects | Documents | Licence |
| --- | --- | --- | --- | --- |
| The language core | The `Vocaterra.Nlp.*` libraries: audio, speech recognition, phonology, pronunciation, analysis, evaluation, decision, response, content tooling. Engine-agnostic, deterministic, fuzzed. | `Vocaterra.Nlp.*` in `src/`, `tests/`, `tools/` | [`docs/nlp/`](docs/nlp/README.md) | Apache-2.0 |
| The game | A Minecraft-inspired 3D voxel prototype, played in the first person, where the player talks to villagers in Spanish to get supplies and directions (MonoGame), plus the engine-free simulation, the game layer and a console host. | `Vocaterra.{Simulation,Game,MonoGame,Host,SpeechRender}` | [`docs/game/`](docs/game/README.md) | Apache-2.0 (relicensed from MIT in R01) |

The whole repository is licensed under the Apache License 2.0 ([`LICENSE`](LICENSE), [`NOTICE`](NOTICE)); third-party data and models keep their
own licences, listed in `NOTICE` and in the bundles' `ATTRIBUTION.md`.

The game references the core's projects directly. What the core owns and what the game owns is fixed in
[`docs/nlp/adr/0009-core-and-game-boundary.md`](docs/nlp/adr/0009-core-and-game-boundary.md); the calls a host makes are in
[`docs/nlp/API.md`](docs/nlp/API.md). Until 2026-10-21 the two were separate repositories, and until R01 they were sibling folders (`nlp/`, `game/`).

The game's prototype is the Minecraft-inspired first-person voxel slice in [`docs/game/PROTOTYPE_BRIEF.md`](docs/game/PROTOTYPE_BRIEF.md)
(the owner's Prototype Design Brief, v1.2), planned milestone by milestone in [`docs/game/SCENARIO_PLAN.md`](docs/game/SCENARIO_PLAN.md).

## Layout

```
Vocaterra.sln              every project (solution folders src, tests, tools)
Vocaterra.Nlp.slnf         the core alone (a solution filter; the licence scan and the core-only build use it)
Directory.Build.props      build settings: shared, then the groups keyed on the project name (Vocaterra.Nlp.* packing; the game's native trim)
Directory.Packages.props   one version list for both halves
nuget.config               nuget.org only
src/                       Vocaterra.Nlp.* (the core's libraries) and Vocaterra.Simulation, .Game, .MonoGame, .Host (the game)
tests/                     one test project per project; tests/golden is the core's golden learner-audio manifest; tests/Shared has the two source files (bundle locator, temp-copy helper) linked into the core's test projects
tools/                     Vocaterra.Nlp.{Cli,Bench,ContentBuilder}, Vocaterra.SpeechRender, manifest.json (the pinned grammar-check tools)
content/                   game-v1 (the one content bundle; the core runs on it too), grammar/ (the grammar-check caches), PHRASES.md, speech-index.json
assets/                    the game's CC0 art, fonts and sounds
models/                    the pinned speech-model manifest (the model files are fetched and ignored)
docs/nlp/, docs/game/      each half's plan, decisions, README and documents
scripts/                   merge-gate.sh, content.sh, limited.sh, fetch-models.sh, license-scan.sh, check-links.sh, playtest and speech scripts
.claude/agents/            the orchestrator's role file (the entry point for a Claude Code session; see CLAUDE.md)
.github/workflows-pending/ the parked CI workflow (deferred)
```

## Build

The .NET SDK is pinned in `global.json` (10.0.402, `latestPatch`); a user-local install under `~/.dotnet` is enough.

```bash
dotnet build Vocaterra.sln -warnaserror
dotnet test Vocaterra.sln            # the core's unit, golden and fuzz suites and the game's tests
dotnet run --project src/Vocaterra.MonoGame
```

Tests that need the speech models read `VOCATERRA_MODELS_DIR` and are skipped without it; `scripts/fetch-models.sh` fetches the pinned
models (`models/manifest.json`). `dotnet build Vocaterra.Nlp.slnf` builds the core alone.

## Working on it

`CLAUDE.md` is the entry point for a Claude Code session. Each half keeps its own plan (`docs/nlp/IMPLEMENTATION_PLAN.md`,
`docs/game/IMPLEMENTATION_PLAN.md`) and its own owner decisions (`docs/nlp/DECISIONS.md`, `docs/game/DECISIONS.md`); the ids in them are
unchanged from the separate repositories. The one content bundle, `content/game-v1`, is edited with `scripts/content.sh` (validate, lint, hash, cefr-check); the core's tests and its demo
CLI run on it, so an edit of the game's content re-runs the core suite (`docs/nlp/CONTRIBUTING.md`).
