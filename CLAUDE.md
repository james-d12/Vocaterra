# Vocaterra: instructions for Claude Code

This repository, `james-d12/Vocaterra` (locally `/home/james/Dev/Vocaterra`), holds both halves of the project in one root layout (`src/`, `tests/`, `tools/`, `docs/nlp/`, `docs/game/`, `content/`, `assets/`, `models/`, `scripts/`,
one `Vocaterra.sln`; R01, 2026-10-23). The halves are told apart by project name:

- **The core**: the `Vocaterra.Nlp` engine (one project since M52, `src/Vocaterra.Nlp`, its stages as folders; layering enforced by
  `LayeringTests`, ADR 0012) and the `Vocaterra.Nlp.*` test, tool and language-pack projects (the Spanish pack `src/Vocaterra.Nlp.Es` since M53; hosts register it with `SpanishContent`, ADR 0012) (with `tests/golden`, `models/` and `docs/nlp/`), the language core (Spanish utterance to verdict, feedback, conversation step, response text, speech).
  Its plan is `docs/nlp/implementation-plan.md` (section 5 the milestone table, 7 and 10 the expansion plans; its findings log, "5b", is `docs/nlp/findings.md`), its owner
  decisions `docs/nlp/decisions.md` (D1 to D14), its boundary `docs/nlp/adr/0009-core-and-game-boundary.md`, its API `docs/nlp/api.md`.
  Licence Apache-2.0 (`LICENSE`, `NOTICE`).
- **The game**: `Vocaterra.Game.Core` (the engine-free systems), `Vocaterra.Game` (MonoGame), `Vocaterra.Host` and `tools/Vocaterra.SpeechRender` (with their tests, `content/game-v1`,
  `assets/` and `docs/game/`). Its plan is
  `docs/game/implementation-plan.md` (section 4 status; its findings log, "section 5", is `docs/game/findings.md`), its decisions `docs/game/decisions.md` (D-game-1 to D-game-28).
  Licence Apache-2.0 like the core (one `LICENSE` and `NOTICE` at the root; the game was MIT until R01).

The two were separate repositories until 2026-10-21 (game D-game-7); both sets of ids (M-numbers,
D-numbers, G- and P-numbers) stay as they were. The game references the core's projects directly; it never copies core source. The core
never plans game content, scenes or engines; the game plan never plans core internals. A core change the game needs is a core milestone.

The repository is developed by one **orchestrator** session that plans and delegates all implementation to subagents and only plans,
gates, merges and pushes. To continue, act as the orchestrator defined in `.claude/agents/orchestrator.md` (or start with
`claude --agent orchestrator`). A QA agent, `.claude/agents/qa.md`, tests one area of the game (ui, language, speech, world,
progression, save, content) and reports bugs to `docs/qa-reports/` without fixing them; the orchestrator turns its reports into milestones. The owner's models (2026-11-06): Haiku 5.5 for small tasks; Sonnet 5.5 at medium effort for medium tasks, at high for large or design-heavy ones; Opus only on request or where a decision names it. The village series
(`docs/game/village-plan.md`) follows the same size rule since 2026-11-06 (D-game-22; it was Opus 5.5 at xhigh for every V milestone before).

Facts every session needs:
- **Docs and session state:** the index is `docs/README.md`; what is open (owner decisions, parked and planned work, known gaps) is listed in `docs/open-items.md`. To resume work, read the open handovers at the top of `docs/handovers/`
  (finished ones are in `done/`); the orchestrator keeps the open one current after every gate, launch and owner decision (owner, 2026-11-05), so a crash or a fresh session loses nothing. Research goes to `docs/research/`, bug reports and their triage
  table to `docs/qa-reports/`, superseded documents to `docs/archive/`; the rules are `docs/process/documentation.md`.
- **.NET SDK is user-local:** prefix commands with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- **Machine resources (the desktop crashed twice from overload, 2026-10-22):** at most two building agents at a time (the owner, 2026-11-05; one until then; one again on 2026-11-15, "only have 1 agent max", D-game-34 item 17; up to four since that evening, "you can have up to 4 agents now", item 19; up to five since 2026-11-16 22:30 UTC, "5 agents and 3 gates looks ok", item 38); every heavy command (dotnet
  restore/build/test/format/run, `scripts/content.sh`, prerender, screenshots) runs through `scripts/limited.sh`, which runs at most three at
  once machine-wide, caps each at 10 GB of RAM (6 GB until 2026-11-16), no swap and 10 of 16 cores, and all of them together at 16 GB (8 GB until 2026-11-16, raised by the owner) and 14 cores. Builds use `-m:4`, tests `-m:2`. Details in the orchestrator's
  "Machine resources" section.
- **Build, test, format (from the root):** `dotnet build Vocaterra.sln -warnaserror`, `dotnet test Vocaterra.sln`,
  `dotnet format Vocaterra.sln --verify-no-changes`. `Vocaterra.Nlp.slnf` builds the core alone (its pack and licence scan use it).
  No pack step, no local feed: `nuget.config` lists nuget.org only.
- **Models:** model-gated tests and the game's speech read `VOCATERRA_MODELS_DIR` (the shared folder `~/.local/share/vocaterra/models`,
  filled by `scripts/fetch-models.sh`); the pinned manifest is `models/manifest.json`.
- **Game content bundle `content/game-v1`:** `scripts/content.sh validate`. After any edit,
  `scripts/content.sh hash content/game-v1 --bundle-id game-v1` (rewrites `manifest.json`), then validate and test again. The tool is
  the core's ContentBuilder run from source. The sandbox refuses command lines that contain the words `eval` or `hash`; run such a
  command from a wrapper script outside the repository. One bundle (R02): the core's tests, fuzz suite and demo CLI run on `content/game-v1`, so any edit of it re-runs the core
  suite and a failing core test is wanted feedback. After `hash`, run the whole suite; re-record the end-to-end goldens if the edit moved them
  (`VOCATERRA_RECORD_GOLDEN=1 dotnet test tests/Vocaterra.Nlp.Golden.Tests`) and put the new bundle hash into
  `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v10.json` (since B05 its `stateDigest` depends on the world-generation files `world.json`, `biomes.json` and `underground.json` and the saved state, not on the bundle hash). No core test pins the hash (`docs/nlp/changing-the-core.md`). Core demo:
  `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1`.
- **Never push to `main` or merge into it** (owner, 2026-10-23). Work lands on a feature branch and is pushed there; the owner merges
  feature branches into `main` by pull request.
- **Merge gate:** `GATE_TARGET=<feature branch> scripts/merge-gate.sh "<label>:<branch>"`, run from that feature branch's checkout (it
  refuses `main`); logs land in `.claude/gate-logs/`. The fuzz suite is a
  gate; never weaken an oracle to make it pass.
- **CI is `.github/workflows/ci.yml`:** jobs `build-and-test` (build, test, offline grammar replay) and `languagetool` (live grammar check),
  run in parallel on pushes to `main`, pull requests and manual runs. The token has the workflow scope (2026-10-31), so workflow edits land
  on a feature branch like any other change.
- **Engine (D-game-1):** MonoGame for the prototype; engine code lives only in `src/Vocaterra.Game`, never in `Vocaterra.Host` or
  `Vocaterra.Game.Core` (which references no package; its `ArchitectureRulesTests` gate it). Core holds the game's logic, Game the drawing,
  the devices, the layers and the host (`docs/process/code-guidelines.md`, `docs/game/architecture.md`). Assets are CC0 only (D-game-5).
- **Game words live in the game (`content/game-v1` and the game projects):** scenes, characters, items, spells, prices, rewards, dialogue and the meaning of each effect kind. The core
  needs only opaque effect kinds and host event names.
- **Determinism:** the host owns the world seed, the stored time, the learner model and the repeat counts. Nothing in the evaluation path
  reads the clock or draws random numbers; a `--transcript` run uses a fixed time.
- **The prototype (D-game-8):** the owner's brief `docs/game/design/prototype-brief.md` (source: the `.docx` in `docs/game/design/sources/`) and its plan
  `docs/game/scenario-plan.md`, milestones S0 to S12 (branch `S<nn>-<name>`, prefix `S<nn>:`): a 3D first-person voxel slice on MonoGame,
  merged into `main` until 2026-10-22 (`feature/prototype` is retired); new work goes to a feature branch, never `main`. Content gate from S5, at the
  A2 ceiling since V08 (D-game-22 (b)): `scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`
  clean, each lesson held to its own `level` (`a1` | `a2`) by the host's `--validate-content`, `content/PHRASES.md` the source of truth
  (D-game-10, D-game-12).
- **Day, night and weather (D-game-31):** `docs/game/weather-plan.md`, milestones E00 to E08 (prefix `E<nn>:`), built on `feature/weather`
  in the worktree `.claude/worktrees/weather` (2026-11-10). The data is `content/game-v1/game/day.json` and `weather.json`; the logic is
  `Vocaterra.Game.Core/Atmosphere`, the drawing `Vocaterra.Game/Graphics/Sky` and `Graphics/Weather`. A weather kind is a row of data:
  consumers read the `AtmosphereSnapshot`'s channels, and no file under `src/` names a kind's id (two tests enforce it). `--time <hour>`
  and `--weather <id>` hold them for a run.
- **Content as data (D-game-21):** `docs/game/content-plan.md`, milestones C01 to C10 (prefix `C<nn>:`), all committed straight to
  `feature/content` (no milestone branches) and gated there: JSON Schemas, one `GameData` load, the L1 locale bundle `content/locale/` (outside game-v1's hash), objectives and NPCs as data.
  Closed on 2026-10-29: C01 to C08 are done (PR #16), C09 is assessed, and C10, the authoring CLI, is specified in the plan's section 3.8.
  Do not start C09 or C10 unless the owner asks. How to edit content: `content/README.md`.
- **Open owner decisions:** core D8 to D11 and D13 (`docs/nlp/decisions.md`); D1 and D4 deferred, do not raise them.
