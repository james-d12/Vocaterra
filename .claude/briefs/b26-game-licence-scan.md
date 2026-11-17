You are implementing B26, "The game's packages in the licence scan", for the Vocaterra repository (C#, .NET 10). A small milestone: the
merge gate's licence scan, its override and allow lists, and a document. Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b26 (an absolute path; cd there first), already on the local branch
`B26-game-licence-scan` (cut from `feature/biomes` at 0d72b26b). Commit after each step with the prefix `B26: `. Do not push. Never push or
merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore and build, and the licence scan, through scripts/limited.sh; one heavy command at a time, no `&`, no wait
loops on `pgrep -f` or on a file. Other agents build at the same time: expect waits. Environment: prefix every command with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1. No game windows.

Why: the owner, 2026-11-17: "Sure, add the game to the license scan" (D-game-34 item 44), after B17a's spike found that the gate's scan
covers only the core (`Vocaterra.Nlp.slnf`) and `tests/Vocaterra.Game.Core.Tests`, while a scan of the game project FAILs or reads UNKNOWN
on FontStashSharp (Zlib), MonoGame's native libraries (SDL, OpenAL), NVorbis and Microsoft.DotNet.PlatformAbstractions (MIT, licence file
only). Read docs/game/b17-spike.md ("The gate"), scripts/license-scan.sh (its usage and how overrides work), scripts/license-overrides.txt
and scripts/license-allowlist.txt (their header comments), and scripts/merge-gate.sh (the licence step, about lines 130 to 140).

Rules: the project is Apache-2.0; game assets are CC0 only (D-game-5). Never accept a licence that is not permissive and compatible with
Apache-2.0; an override needs evidence (the licence text you read: the package's own licence file in ~/.nuget/packages/<id>/<version>/,
or its project's licence URL), exactly one package id and version per line, and today's date. Do not edit src/, tests/, content/, either
plan or either decisions file, or .github/workflows/.

Deliverables:
1. Run `scripts/license-scan.sh --offline --target src/Vocaterra.Game/Vocaterra.Game.csproj` and the same for `src/Vocaterra.Host` and
   `tools/Vocaterra.SpeechRender` (the game's shipped projects); list every FAIL and UNKNOWN with its package, version and the licence it
   really has.
2. For each: an override line with evidence in scripts/license-overrides.txt; `Zlib` added to scripts/license-allowlist.txt if it is not
   there (Zlib is permissive and Apache-compatible; the owner approved FontStashSharp under it); anything that is not permissive: stop and
   report it instead of overriding.
3. scripts/merge-gate.sh: the licence step also scans the game's projects (one `--target` line each, as Game.Core.Tests is scanned, its
   output in the same log, failing the gate like the others); its header comment updated.
4. Each scan passes; a short note in docs/game/building-and-testing.md (what the gate's licence scan covers now).
Report, a few lines: branch, head commit; the packages overridden with their licences; the scan results; anything you stopped on.
Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6
