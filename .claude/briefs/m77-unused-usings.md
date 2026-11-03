You are implementing **M77, unused usings as a build warning (IDE0005)**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects; the game is `Vocaterra.{Game.Core,Game,Host}` and `tools/Vocaterra.SpeechRender`. This milestone changes the build's configuration for every project of `Vocaterra.sln` and removes unused `using` lines. It changes no behaviour.

Do the work yourself; do not spawn subagents.

## Why (the owner, 2026-11-02)
- M76 audited `scripts/` on the owner's rule that a check is part of the build or a C# test, never a script.
- It found `scripts/audit-usings.py`, a one-off that lists unused `Vocaterra.*` usings because the build does not report them (IDE0005 is not enforced).
- The owner: "do the IDE0005 warning too". With `-warnaserror` (the gate and CI), an unused using then fails the build, and the script goes.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, then `git checkout -b M77-unused-usings @@BASE@@` (the tip of `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra` or any other worktree.
- **The owner wants work pushed often.** Commit after each step that builds and passes its tests, with the prefix `M77: `. Push after every commit: `git push -u origin M77-unused-usings` the first time, then `git push origin M77-unused-usings`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.
- Disk is short (about 11 GB free).

## The work
1. **Measure first:** the time of `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` after a `dotnet clean` (log it), so the cost of the change can be reported.
2. **Turn the rule on for every project of the solution** (core, game, tools and tests), in the root `Directory.Build.props` and `.editorconfig`:
   - `dotnet_diagnostic.IDE0005.severity = warning` in `.editorconfig`;
   - `EnforceCodeStyleInBuild` in `Directory.Build.props`;
   - IDE0005 reports on build only when a project generates its documentation file. Today only the core's `src/` projects do (`GenerateDocumentationFile`, `Directory.Build.props` about line 55). Turn it on for the rest with their missing-comment warnings suppressed (CS1591, and any other XML-doc warning that only appears because of this), and say which you suppressed and where.
3. **Only IDE0005 changes the build:**
   - `EnforceCodeStyleInBuild` also enforces every IDE rule `.editorconfig` already sets at `warning`, for example `csharp_style_namespace_declarations = file_scoped:warning`.
   - List every rule that fires once it is on.
   - Where a rule other than IDE0005 fires, keep it at the severity it effectively had (not enforced on build) and report it as a proposal. Do not change code for it.
4. **Remove every unused using** in `src/`, `tests/` and `tools/`:
   - Use the IDE's own fix (`scripts/limited.sh dotnet format Vocaterra.sln style --diagnostics IDE0005 --severity warn`, or the analyzer variant if that is where it lives), then review the diff.
   - Only `using` lines change. A file that needs anything else to build is a finding: report it.
   - Keep the global usings and the implicit usings as they are.
5. **Prove it:** add one unused using to a core file and one to a game file, see the `-warnaserror` build fail with IDE0005 naming each file, and revert both. Do not commit that.
6. **Delete `scripts/audit-usings.py`,** and make the documents that mention it say that the build reports unused usings now (`grep -rn audit-usings docs/ scripts/ *.md`; `scripts/README.md`; the R16 documents carry a dated note, as M76 did for `core-move-scan.py`).
7. **Measure again:** the same clean build's time, and `dotnet format Vocaterra.sln --verify-no-changes` still clean.

## Rules
- This brief lets M77 edit:
  - `Directory.Build.props` and `.editorconfig` at the root (and a project's `.csproj` only if a project cannot take the root's setting; say why);
  - the `using` lines of any `.cs` file in `src/`, `tests/` and `tools/`;
  - `scripts/audit-usings.py` (delete) and the documents that name it.
- Nothing else: no other code, no content, no test logic.
- Do not edit `scripts/merge-gate.sh`, `scripts/limited.sh`, `scripts/test.sh`, either `IMPLEMENTATION_PLAN.md`, either `DECISIONS.md`, `docs/HANDOVER_*.md`, `.claude/agents/`, or anything under `.github/workflows/`.
  - CI builds with `-warnaserror` and inherits the rule. If CI needs a workflow change for it, report it.
- `Vocaterra.Game.Core` references no package (its `ArchitectureRulesTests`): add no analyzer package anywhere. IDE0005 comes with the SDK.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files. Prefer `grep -n`, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/m77/` and print at most 30 lines of it (for the build, `grep -c 'IDE0005'` and the first lines).
- Wait for a long command by running it in the foreground, not by polling a log. **Never leave a wait loop or a background process behind.**

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run|clean` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- **Never run `scripts/test.sh` or `scripts/content-loop.sh` under `scripts/limited.sh`:** they call it themselves.
- Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/m77/` if you need one.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Read first
- `CLAUDE.md`.
- `Directory.Build.props`, `.editorconfig`, and `scripts/audit-usings.py` (its header).

## Your checks before reporting (from the worktree root)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/limited.sh dotnet build Vocaterra.Nlp.slnf -warnaserror -m:4`, 0 warnings: the core alone, as its licence scan builds it.
- The full suite: @@COUNT@@ passed and 3 skipped before; the same after.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes` clean.
- Do not commit `bin/`, `obj/` or `artifacts/`, or a generated documentation `.xml` file.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- **The settings:** what you set where, and which warnings you suppressed and why.
- **The rules** that fired once `EnforceCodeStyleInBuild` was on: their counts, and what you did with each.
- **The usings removed:** a count by project, and any file that needed more (a finding).
- **The proof** that a new unused using fails the build.
- **The time** of the clean build before and after.
- **The documents changed;** the test count before and after; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|limited.sh'`, kill only your own) and that the worktree is clean and pushed.
