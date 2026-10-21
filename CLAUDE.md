# Vocaterra (the game): instructions for Claude Code

This repository is the game, `james-d12/Vocaterra`, locally `/home/user/Dev/Vocaterra`. It is developed by an **orchestrator** session
that plans and delegates all implementation to subagents and only plans, gates, merges and pushes. To continue the project, act as the
orchestrator defined in `.claude/agents/orchestrator.md` (or start with `claude --agent orchestrator`). Read `IMPLEMENTATION_PLAN.md`
(section 4 for status, section 5 for findings) and `docs/DECISIONS.md` first. The owner prefers Haiku 5.5 for delegated work; Sonnet only
for design-heavy milestones; Opus only on request.

The Spanish language processing is a separate repository, the language core **Vocaterra.NLP** (`james-d12/Vocaterra.NLP`, locally
`/home/user/Dev/Vocaterra.NLP`). It has its own orchestrator and plan, and runs only the core. This repository includes the core as the git
submodule `external/Vocaterra.NLP` and consumes it only as the NuGet packages `Vocaterra.Nlp.*` 0.1.0. The core never plans game content;
this repository never plans core internals.

Facts every session needs:
- **Packages only.** Never add a project reference to the core, never copy its source, never edit inside `external/Vocaterra.NLP` except
  to bump its pinned commit (procedure in the role file). A call the packages do not expose goes to `docs/CORE_REQUESTS.md` as a row.
- **Fresh checkout, the pack fails without this (CR-024):** before the first pack, create the uncommitted file
  `external/Vocaterra.NLP/nuget.config` with `<configuration><packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>`;
  otherwise the core's restore finds this repository's `nuget.config`, whose local feed does not exist yet (NU1301). After any `git pull`
  that moves the submodule, run `git submodule update --init`, delete `~/.nuget/packages/vocaterra.nlp.*` (the version stays 0.1.0, so a
  cached copy hides the new core) and pack again.
- **Fresh checkout:** `git submodule update --init`, then `bash external/Vocaterra.NLP/scripts/pack.sh` fills the local feed that
  `nuget.config` reads (`external/Vocaterra.NLP/artifacts/feed`). The pack takes minutes and writes about 3 GB, all git-ignored in the
  submodule.
- **.NET SDK is user-local:** prefix commands with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- **Build, test, format:** `dotnet build Vocaterra.sln -warnaserror`, `dotnet test Vocaterra.sln`, `dotnet format Vocaterra.sln --verify-no-changes`.
- **Content bundle `content/game-v1`:** `scripts/content.sh validate`. After any edit, `scripts/content.sh hash content/game-v1 --bundle-id game-v1`
  (rewrites `manifest.json`), then validate and test again. The tool is the core's `vocaterra-content` 0.1.0, installed under `.tools/`
  from the feed on first use. `cefr-check`, `pack` and `verify-pack` pass through the same script. The sandbox refuses command lines that
  contain the words `eval` or `hash`; run such a command from a wrapper script outside the repository.
- **Merge gate:** `scripts/merge-gate.sh "<label>:<branch>"`, run from main only; logs land in `.claude/gate-logs/`.
- **Never write under `.github/workflows/`.** The token lacks the workflow scope. CI is parked in `.github/workflows-pending/ci.yml` and
  deferred by the owner (D-game-3); do not raise it.
- **Engine (D-game-1, owner 2026-10-21):** MonoGame for the Terraria-inspired prototype (plan P1 to P10); the final engine is open, Godot .NET
  the candidate. Engine code lives only in `src/Vocaterra.MonoGame`, never in `src/Vocaterra.Host`, `src/Vocaterra.Simulation` or
  `src/Vocaterra.Game`. Assets are CC0 only (D-game-5).
- **Open decisions** (`docs/DECISIONS.md`): this repository's licence (D-game-2; no licence is granted until chosen; the core is Apache-2.0). The bundle diverges from the core's sample (D-game-4, decided):
  no re-sync on core releases.
- **Game words live here:** scenes, characters, items, spells, prices, rewards, dialogue and the meaning of each effect kind. The core needs
  only opaque effect kinds and host event names.
- **Determinism:** the host owns the world seed, the stored time, the learner model and the repeat counts. Nothing in the evaluation path
  reads the clock or draws random numbers; a `--transcript` run uses a fixed time.
