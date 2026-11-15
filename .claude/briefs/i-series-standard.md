# The I series: the part every milestone brief shares

Read this first, then your milestone's own brief (named in your prompt). It is the template of `docs/process/milestone-briefs.md` filled
in for the I series (inventory and equipment; `docs/game/inventory-plan.md`, D-game-35), on the feature branch `feature/inventory`.

```
The repository (C#, .NET 10): the language core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.*
test and tool projects; the game is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and
tools/Vocaterra.SpeechRender. Every I milestone is a game milestone.

Where to work: the worktree and branch named in your prompt, already created and cut from `feature/inventory`. Work only there (never in
/home/james/Dev/Vocaterra itself or another worktree); do not create another branch. Commit after each item that builds and passes its
tests, with your milestone's prefix (for example `I01: `). Do not push your branch: it stays local and the gate merges it from there.
Never push or merge `feature/inventory` or `main`. When you finish, leave the worktree on your branch, clean.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
speech prerender and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly (content-loop.sh refuses to
run under limited.sh). Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell
wait loop on `pgrep -f`. While iterating run `scripts/test.sh fast` or `scripts/test.sh project <test project>`; run `scripts/test.sh
full` (the whole suite, as the gate) once before reporting. Exit 137 means the 6 GB cap killed it: lower the parallelism, do not work
round the cap. Another agent and other sessions build on this machine: a heavy command may wait for a slot, which is normal.
While the owner uses the machine (owner, 2026-11-15, D-game-34 item 17): prefix every heavy command with VOCATERRA_HEAVY_SLOTS=1 (one
machine-wide slot for all; a wait for the slot is normal and may be long), and open no game window: no screenshot, no game run, no
scripted run that opens a window, unless your prompt says the owner has finished. Headless host runs (`--transcript`, `--validate-content`)
and tests are fine.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
use a wrapper script outside the repository, or scripts/content-loop.sh, which does the whole loop). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Rules: edit only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/, and take the core as it is (no
edit of a Vocaterra.Nlp project or of docs/nlp/; a missing core call stops that part: report it). A regression check is a C# test, never
a script with text baselines. Never create or edit anything under .github/workflows/. Do not edit either implementation-plan.md, either
decisions.md, docs/game/inventory-plan.md, docs/game/findings.md, docs/open-items.md, docs/handovers/ or .claude/briefs/ (the orchestrator
does). Engine code only in src/Vocaterra.Game (D-game-1); no engine type, no System.Random and no clock (DateTime, Stopwatch,
TimeProvider) in Vocaterra.Game.Core (tests/Vocaterra.Game.Core.Tests/Architecture/ArchitectureRulesTests.cs). No custom shader (.fx):
BasicEffect, AlphaTestEffect, SpriteBatch and render targets only. Assets CC0 only (D-game-5). One type per file, folders as namespaces,
XML doc comments in the style of the files around you, Given_When_Then test names in test folders that mirror the source
(docs/process/code-guidelines.md). Code open to later causes and sources without edits (the owner: "the code for it should be
adaptable and follow solid principles"): interfaces with one first implementation, data rows for kinds. No migration code for old saves (the game is unreleased), but the save fixtures must keep loading.
Never weaken a fuzz oracle or an unrelated test to pass; an existing test changed on purpose is named in the report with its reason.

Read: CLAUDE.md; docs/game/inventory-plan.md (sections 3.1, your milestone's design section and section 4); docs/game/implementation-plan.md sections 3 and 6;
docs/process/code-guidelines.md; then what your own brief names. Read the sections named, never whole long files; heavy output goes to
log files, at most 30 lines printed.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report passed, failed and skipped; the count on `feature/inventory` is given in your prompt); scripts/limited.sh dotnet
format Vocaterra.sln --verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or
artifacts/. A failure of a core fuzz test (tests/Vocaterra.Nlp.Fuzzing.Tests) that your change cannot have caused is reported with its
seed and input, not fixed and not hidden.

Commit messages end with
Co-Authored-By: Claude <your model, for example Sonnet 5.5> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC

Report, one page: branch, commit SHA, worktree path; what was built (types and files); test counts; measurements your brief asks for;
every existing test, golden or fixture that changed and why; bugs found and fixed (where, symptom, fix, test); known-not-fixed with
reasoning; files touched outside the game's area; anything left undone and why. No file-by-file narrative and no restatement of the
brief.
```
