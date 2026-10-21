# Vocaterra.NLP (formerly SpanishLearningGame): instructions for Claude Code

This repository is developed by an **orchestrator** session that delegates all implementation to
subagents and only plans, gates, merges and pushes. To continue the project, act as the orchestrator
defined in `.claude/agents/orchestrator.md` (or start with `claude --agent orchestrator`). Read
`IMPLEMENTATION_PLAN.md` first: §5 is the milestone status table, §5b the findings log, §7 the current
expansion plan. The owner prefers Haiku 5.5 for delegated work; Sonnet only for design-heavy
milestones; Opus only on request.

Remote: `https://github.com/james-d12/Vocaterra.NLP.git`. This repository is the language core only; the
game (`james-d12/Vocaterra`, locally `/home/user/Dev/Vocaterra`) has its own orchestrator, plan and
decisions and includes this repository as a git submodule. Never plan or document game content, scenes
or engines here.

Cross-repository protocol: at the start of every session read `/home/user/Dev/Vocaterra/docs/CORE_REQUESTS.md`
(if the checkout exists). Each open row is a request from the game orchestrator for a core change (an
API gap, a bundle-format need, a bug). Accept it as a core milestone or answer in the row why not; when
the change is merged and pushed, mark the row done with the core commit so the game can bump its
submodule. Owner decisions for the core live in `docs/DECISIONS.md`.

Facts every session needs:
- .NET SDK is user-local: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`.
- Model-gated tests: `VOCATERRA_MODELS_DIR=/home/user/Dev/Vocaterra.NLP/models`.
- Merge gate: `scripts/merge-gate.sh "<label>:<branch>"`; logs land in `.claude/gate-logs/`.
- Never commit under `.github/workflows/` (token lacks the workflow scope; see `.github/workflows-pending/`).
- Content edits need the procedure in `CONTRIBUTING.md` (validate, hash, re-pin, re-record goldens).
- The fuzz suite is a gate; never weaken an oracle to make it pass.
- Open owner decisions: `docs/DECISIONS.md` (D8–D11, D13 open; D1 and D4 deferred, do not raise them).
