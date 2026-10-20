# SpanishLearningGame: instructions for Claude Code

This repository is developed by an **orchestrator** session that delegates all implementation to
subagents and only plans, gates, merges and pushes. To continue the project, act as the orchestrator
defined in `.claude/agents/orchestrator.md` (or start with `claude --agent orchestrator`). Read
`IMPLEMENTATION_PLAN.md` first: §5 is the milestone status table, §5b the findings log, §7 the current
expansion plan. The owner prefers Haiku 5.5 for delegated work; Sonnet only for design-heavy
milestones; Opus only on request.

Facts every session needs:
- .NET SDK is user-local: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`.
- Model-gated tests: `SLG_MODELS_DIR=/home/user/Dev/SpanishLearningGame/models`.
- Merge gate: `scripts/merge-gate.sh "<label>:<branch>"`; logs land in `.claude/gate-logs/`.
- Never commit under `.github/workflows/` (token lacks the workflow scope; see `.github/workflows-pending/`).
- Content edits need the procedure in `CONTRIBUTING.md` (validate, hash, re-pin, re-record goldens).
- The fuzz suite is a gate; never weaken an oracle to make it pass.
- Open owner decisions are listed at the end of `IMPLEMENTATION_PLAN.md` §5b.
