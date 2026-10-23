# Vocaterra: instructions for Claude Code

This repository, `james-d12/Vocaterra` (locally `/home/james/Dev/Vocaterra`), holds both halves of the project in one root layout (`src/`, `tests/`, `tools/`, `docs/nlp/`, `docs/game/`, `content/`, `assets/`, `models/`, `scripts/`,
one `Vocaterra.sln`; R01, 2026-10-23). The halves are told apart by project name:

- **The core**: the `Vocaterra.Nlp.*` projects (with `content/v1`, `tests/golden`, `models/` and `docs/nlp/`), the language core (Spanish utterance to verdict, feedback, conversation step, response text, speech).
  Its plan is `docs/nlp/IMPLEMENTATION_PLAN.md` (section 5 the milestone table, 5b the findings log, 7 and 10 the expansion plans), its owner
  decisions `docs/nlp/DECISIONS.md` (D1 to D13), its boundary `docs/nlp/adr/0009-core-and-game-boundary.md`, its API `docs/nlp/API.md`.
  Licence Apache-2.0 (`LICENSE`, `NOTICE`).
- **The game**: `Vocaterra.Simulation`, `Vocaterra.Game`, `Vocaterra.MonoGame`, `Vocaterra.Host` and `Vocaterra.SpeechRender` (with their tests, `content/game-v1`,
  `assets/` and `docs/game/`). Its plan is
  `docs/game/IMPLEMENTATION_PLAN.md` (section 4 status, section 5 findings), its decisions `docs/game/DECISIONS.md` (D-game-1 to D-game-7).
  Licence Apache-2.0 like the core (one `LICENSE` and `NOTICE` at the root; the game was MIT until R01).

The two were separate repositories until 2026-10-21 (game D-game-7); both sets of ids (M-numbers,
D-numbers, G- and P-numbers) stay as they were. The game references the core's projects directly; it never copies core source. The core
never plans game content, scenes or engines; the game plan never plans core internals. A core change the game needs is a core milestone.

The repository is developed by one **orchestrator** session that plans and delegates all implementation to subagents and only plans,
gates, merges and pushes. To continue, act as the orchestrator defined in `.claude/agents/orchestrator.md` (or start with
`claude --agent orchestrator`). The owner prefers Haiku 5.5 for delegated work; Sonnet only for design-heavy milestones; Opus only on request.

Facts every session needs:
- **.NET SDK is user-local:** prefix commands with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- **Machine resources (the desktop crashed twice from overload, 2026-10-22):** one agent at a time; every heavy command (dotnet
  restore/build/test/format/run, `scripts/content.sh`, prerender, screenshots) runs through `scripts/limited.sh`, which serialises them
  machine-wide and caps each at 6 GB of RAM, no swap and 10 of 16 cores. Builds use `-m:4`, tests `-m:2`. Details in the orchestrator's
  "Machine resources" section.
- **Build, test, format (from the root):** `dotnet build Vocaterra.sln -warnaserror`, `dotnet test Vocaterra.sln`,
  `dotnet format Vocaterra.sln --verify-no-changes`. `Vocaterra.Nlp.slnf` builds the core alone (its pack and licence scan use it).
  No pack step, no local feed: `nuget.config` lists nuget.org only.
- **Models:** model-gated tests and the game's speech read `VOCATERRA_MODELS_DIR` (the shared folder `~/.local/share/vocaterra/models`,
  filled by `scripts/fetch-models.sh`); the pinned manifest is `models/manifest.json`.
- **Game content bundle `content/game-v1`:** `scripts/content.sh validate`. After any edit,
  `scripts/content.sh hash content/game-v1 --bundle-id game-v1` (rewrites `manifest.json`), then validate and test again. The tool is
  the core's ContentBuilder run from source. The sandbox refuses command lines that contain the words `eval` or `hash`; run such a
  command from a wrapper script outside the repository. Core content edits (`content/v1`) follow `docs/nlp/CONTRIBUTING.md` (validate,
  hash, re-pin, re-record goldens).
- **Merge gate:** `scripts/merge-gate.sh "<label>:<branch>"`, run from main only; logs land in `.claude/gate-logs/`. The fuzz suite is a
  gate; never weaken an oracle to make it pass.
- **Never write under `.github/workflows/`** until the owner says the token has the workflow scope. CI was reopened on 2026-10-23
  (core D1, game D-game-3) and is planned (see the orchestrator's handover); the old `.github/workflows-pending/ci.yml` is out of date.
- **Engine (D-game-1):** MonoGame for the prototype; engine code lives only in `src/Vocaterra.MonoGame`, never in `Vocaterra.Host`,
  `Vocaterra.Simulation` or `Vocaterra.Game`. Assets are CC0 only (D-game-5).
- **Game words live in the game (`content/game-v1` and the game projects):** scenes, characters, items, spells, prices, rewards, dialogue and the meaning of each effect kind. The core
  needs only opaque effect kinds and host event names.
- **Determinism:** the host owns the world seed, the stored time, the learner model and the repeat counts. Nothing in the evaluation path
  reads the clock or draws random numbers; a `--transcript` run uses a fixed time.
- **The prototype (D-game-8):** the owner's brief `docs/game/PROTOTYPE_BRIEF.md` (source: the `.docx` beside it) and its plan
  `docs/game/SCENARIO_PLAN.md`, milestones S0 to S12 (branch `S<nn>-<name>`, prefix `S<nn>:`): a 3D first-person voxel slice on MonoGame,
  merged **directly into `main`** (owner, 2026-10-22; `feature/prototype` is retired) with `scripts/merge-gate.sh ...`. Content gate from S5:
  `scripts/content.sh lint content/game-v1 --strict --level a1` clean, `content/PHRASES.md` the source of truth (D-game-10, D-game-12).
- **Open owner decisions:** core D8 to D11 and D13 (`docs/nlp/DECISIONS.md`); D1 and D4 deferred, do not raise them.
