# Vocaterra (the game): instructions for Claude Code

This repository is the game. The Spanish language processing is a separate repository, the core **Vocaterra.NLP**
(`james-d12/Vocaterra.NLP`, locally `/home/user/Dev/SpanishLearningGame`), consumed here only as the NuGet packages `Vocaterra.Nlp.*` 0.1.0.
Read `docs/ARCHITECTURE.md` for what the game takes from the core and what it owns (ADR 0009 in the core is the rule).

The plan for this repository is the core's `IMPLEMENTATION_PLAN.md` section 8 (the standalone-core roadmap, milestone M19c) until the game
has a plan of its own. The core's orchestrator conventions apply here too: plan and delegate, merge and push through the owner's gate, and
never implement directly in the orchestrator's session. The owner prefers Haiku 5.5 for delegated work.

Facts every session needs:
- The game references only the packages. Never add a project reference to the core, and never copy its source into this repository. A
  call the packages do not expose is an API gap to report to the core, not a reason to reach into its source.
- The packages come from the core's local feed (`nuget.config` points at `../SpanishLearningGame/artifacts/feed`; run `scripts/pack.sh` in the
  core checkout first). Versions live in `Directory.Packages.props`.
- .NET SDK is user-local: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`.
- Build with `dotnet build Vocaterra.sln -warnaserror`; the format gate is `dotnet format Vocaterra.sln --verify-no-changes`.
- The content bundle is `content/game-v1`. After an edit, `scripts/content.sh validate` and `scripts/content.sh hash content/game-v1
  --bundle-id game-v1`; the ContentBuilder comes from the core checkout (`VOCATERRA_NLP_DIR`).
- Never commit under `.github/workflows/` (the token lacks the workflow scope). CI is parked in `.github/workflows-pending/ci.yml` until the
  scope and the feed (core D1 and D5) are settled.
- No licence is chosen (core D3). Do not add a licence to the project metadata without the owner's decision.
- The engine is not chosen (core D12). The host is a console program on purpose; keep engine-specific code out of `src/Vocaterra.Host`.
- Game words (scenes, items, spells, prices) belong here, not in the core. A word the core needs for its own boundary (an opaque effect
  kind or host event) is passed through as a string.
