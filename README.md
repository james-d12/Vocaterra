# Vocaterra

A Spanish-learning game and the language core that grades what the player says. One repository, two halves:

| Folder | What it is | Licence |
| --- | --- | --- |
| [`nlp/`](nlp/README.md) | The language core, the `Vocaterra.Nlp.*` libraries: audio, speech recognition, phonology, pronunciation, analysis, evaluation, decision, response, content tooling. Engine-agnostic, deterministic, fuzzed. | Apache-2.0 (`nlp/LICENSE`, `nlp/NOTICE`) |
| [`game/`](game/README.md) | The game: a Minecraft-inspired 3D voxel prototype, played in the first person, where the player talks to villagers in Spanish to get supplies and directions (MonoGame), plus the engine-free simulation, the game layer and a console host. | MIT (`game/LICENSE`) |

The game references the core's projects directly. What the core owns and what the game owns is fixed in
[`nlp/docs/adr/0009-core-and-game-boundary.md`](nlp/docs/adr/0009-core-and-game-boundary.md); the calls a host makes are in
[`nlp/docs/API.md`](nlp/docs/API.md). Until 2026-10-21 the two were separate repositories; the core's history is under `nlp/`.

The game's prototype is the Minecraft-inspired first-person voxel slice in [`game/docs/PROTOTYPE_BRIEF.md`](game/docs/PROTOTYPE_BRIEF.md)
(the owner's Prototype Design Brief, v1.2), planned milestone by milestone in [`game/docs/SCENARIO_PLAN.md`](game/docs/SCENARIO_PLAN.md). The
S series that builds it lands on the branch `feature/prototype`, not on `main`.

## Layout

```
Vocaterra.sln              every project (solution folders nlp/src, nlp/tests, nlp/tools, game/src, game/tests)
Directory.Build.props      shared build settings; nlp/ and game/ add their own in their folders
Directory.Packages.props   one version list for both halves
nuget.config               nuget.org only
scripts/                   merge-gate.sh (the orchestrator's gate), content.sh (the game bundle through the core's content tool)
nlp/                       the core: src/, tests/, tools/, samples/, content/v1, golden/, models/, docs/, scripts/, its plan and README
game/                      the game: src/, tests/, assets/, content/game-v1, docs/, its plan and README
.claude/agents/            the orchestrator's role file (the entry point for a Claude Code session; see CLAUDE.md)
.github/workflows-pending/ the parked CI workflow (deferred)
```

## Build

The .NET SDK is pinned in `global.json` (10.0.402, `latestPatch`); a user-local install under `~/.dotnet` is enough.

```bash
dotnet build Vocaterra.sln -warnaserror
dotnet test Vocaterra.sln            # the core's unit, golden and fuzz suites and the game's tests
dotnet run --project game/src/Vocaterra.MonoGame
```

Tests that need the speech models read `VOCATERRA_MODELS_DIR` and are skipped without it; `nlp/scripts/fetch-models.sh` fetches the pinned
models (`nlp/models/manifest.json`). `dotnet build nlp/Vocaterra.Nlp.sln` builds the core alone, and `nlp/scripts/pack.sh` still packs it
for a consumer outside this repository.

## Working on it

`CLAUDE.md` is the entry point for a Claude Code session. Each half keeps its own plan (`nlp/IMPLEMENTATION_PLAN.md`,
`game/IMPLEMENTATION_PLAN.md`) and its own owner decisions (`nlp/docs/DECISIONS.md`, `game/docs/DECISIONS.md`); the ids in them are
unchanged from the separate repositories. The game's content bundle is edited with `scripts/content.sh` (validate, hash, cefr-check);
the core's sample bundle follows `nlp/CONTRIBUTING.md`.
