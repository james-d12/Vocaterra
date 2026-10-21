# Vocaterra: instructions for Claude Code

This repository, `james-d12/Vocaterra` (locally `/home/james/Dev/Vocaterra`), holds both halves of the project as sibling folders:

- **`nlp/`**: the language core, `Vocaterra.Nlp.*` (Spanish utterance to verdict, feedback, conversation step, response text, speech).
  Its plan is `nlp/IMPLEMENTATION_PLAN.md` (section 5 the milestone table, 5b the findings log, 7 and 10 the expansion plans), its owner
  decisions `nlp/docs/DECISIONS.md` (D1 to D13), its boundary `nlp/docs/adr/0009-core-and-game-boundary.md`, its API `nlp/docs/API.md`.
  Licence Apache-2.0 (`nlp/LICENSE`, `nlp/NOTICE`).
- **`game/`**: the game (`Vocaterra.Simulation`, `Vocaterra.Game`, `Vocaterra.MonoGame`, `Vocaterra.Host`). Its plan is
  `game/IMPLEMENTATION_PLAN.md` (section 4 status, section 5 findings), its decisions `game/docs/DECISIONS.md` (D-game-1 to D-game-7).
  Licence MIT (`game/LICENSE`).

The two were separate repositories until 2026-10-21 (game D-game-7); the core's history is under `nlp/` and both sets of ids (M-numbers,
D-numbers, G- and P-numbers) stay as they were. The game references the core's projects directly; it never copies core source. The core
never plans game content, scenes or engines; the game plan never plans core internals. A core change the game needs is a core milestone.

The repository is developed by one **orchestrator** session that plans and delegates all implementation to subagents and only plans,
gates, merges and pushes. To continue, act as the orchestrator defined in `.claude/agents/orchestrator.md` (or start with
`claude --agent orchestrator`). The owner prefers Haiku 5.5 for delegated work; Sonnet only for design-heavy milestones; Opus only on request.

Facts every session needs:
- **.NET SDK is user-local:** prefix commands with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- **Build, test, format (from the root):** `dotnet build Vocaterra.sln -warnaserror`, `dotnet test Vocaterra.sln`,
  `dotnet format Vocaterra.sln --verify-no-changes`. `nlp/Vocaterra.Nlp.sln` builds the core alone (its pack and licence scan use it).
  No pack step, no local feed: `nuget.config` lists nuget.org only.
- **Models:** model-gated tests and the game's speech read `VOCATERRA_MODELS_DIR` (the shared folder `~/.local/share/vocaterra/models`,
  filled by `nlp/scripts/fetch-models.sh`); the pinned manifest is `nlp/models/manifest.json`.
- **Game content bundle `game/content/game-v1`:** `scripts/content.sh validate`. After any edit,
  `scripts/content.sh hash game/content/game-v1 --bundle-id game-v1` (rewrites `manifest.json`), then validate and test again. The tool is
  the core's ContentBuilder run from source. The sandbox refuses command lines that contain the words `eval` or `hash`; run such a
  command from a wrapper script outside the repository. Core content edits (`nlp/content/v1`) follow `nlp/CONTRIBUTING.md` (validate,
  hash, re-pin, re-record goldens).
- **Merge gate:** `scripts/merge-gate.sh "<label>:<branch>"`, run from main only; logs land in `.claude/gate-logs/`. The fuzz suite is a
  gate; never weaken an oracle to make it pass.
- **Never write under `.github/workflows/`.** The token lacks the workflow scope. CI is parked in `.github/workflows-pending/ci.yml` and
  deferred by the owner (core D1, game D-game-3); do not raise it.
- **Engine (D-game-1):** MonoGame for the prototype; engine code lives only in `game/src/Vocaterra.MonoGame`, never in `Vocaterra.Host`,
  `Vocaterra.Simulation` or `Vocaterra.Game`. Assets are CC0 only (D-game-5).
- **Game words live in `game/`:** scenes, characters, items, spells, prices, rewards, dialogue and the meaning of each effect kind. The core
  needs only opaque effect kinds and host event names.
- **Determinism:** the host owns the world seed, the stored time, the learner model and the repeat counts. Nothing in the evaluation path
  reads the clock or draws random numbers; a `--transcript` run uses a fixed time.
- **Open owner decisions:** core D8 to D11 and D13 (`nlp/docs/DECISIONS.md`); D1 and D4 deferred, do not raise them.
