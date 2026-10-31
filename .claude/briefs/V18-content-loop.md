You are implementing milestone **V18, the content loop in one command**, of the Vocaterra repository (C#, .NET 10: a Spanish-learning first-person voxel game on MonoGame, plus a language core). It is a tooling milestone of phase B of the village series (`docs/game/VILLAGE_PLAN.md`), asked for by the owner on 2026-10-31 after a study of the project's bottlenecks (`docs/DEVELOPMENT_BOTTLENECKS.md`, section 2.4 and action P2). The four content milestones still ahead (V14, V17, V15 and the sessions' follow-ups) each run the same mechanical loop by hand after every edit of `content/game-v1`; V18 makes it one command, so that no step is forgotten, no command line needs the words the sandbox refuses, and a grammar fault shows in about a minute instead of at the full run.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `@@HEAD@@`).
- Create your branch with `git checkout -b V18-content-loop feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step, with the prefix `V18: `. Push after every commit: `git push -u origin V18-content-loop` the first time, then `git push origin V18-content-loop`.
- Never push `main` or `feature/village`, and do not merge.

## The loop today (what an agent does by hand after each content edit; `docs/DEVELOPMENT_BOTTLENECKS.md` 2.4)
1. `scripts/content.sh refresh` (through `scripts/limited.sh`): the manifests of `content/game-v1` and `content/locale`, provenance, validate, the gate's lint and cefr-checks, the host's `--validate-content`, the grammar strings, and the grammar check offline from the committed cache.
2. On a cache miss (new Spanish): `scripts/languagetool.sh start`, `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --prune`, read the findings, stop the server; then CI's check that a cache rebuilt from nothing equals the committed one (start, `rm content/grammar/game-v1-cache.json`, the grammar command with `--strict`, `git diff --exit-code content/grammar/`, stop).
3. `scripts/prerender-speech.sh` for the new lines (it writes WAVs and provenance into the bundle, so the manifest must be rewritten after it).
4. The new `bundleHash` into the save fixtures that pin it (`tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`, `save-v4.json`, and any other: find them by the old hash, never by a list typed by hand).
5. The identity baseline and end-to-end goldens: `VOCATERRA_RECORD_GOLDEN=1 dotnet test tests/Vocaterra.Nlp.Golden.Tests`, in its own commit.
6. The fuzz suite's `FuzzLanguageStreamTests` digests: constants in the test's code, which move with the content until the core's M57 pins them to a fixture.
7. `scripts/test.sh fast`, then the fuzz suite (it is `Speed=Slow`, so grammar faults show only in a full run today), then `scripts/test.sh full`.

## What to build
1. **One command**, `scripts/content-loop.sh` (or a mode of `scripts/content.sh` if you can show it reads better; say which), that runs steps 1 to 7 in order and stops at the first failure with a message naming the next thing to do, then prints **one summary of at most 30 lines**: each step, passed or failed or skipped, what it changed (files, counts), and the log file of each step. Every step's full output goes to a log under `.claude/content-loop/<stamp>/` (git-ignored; add the ignore if needed), never to the terminal.
   - **The lock.** `scripts/limited.sh` holds one machine-wide lock (`flock`), and `scripts/test.sh` calls `limited.sh` itself. So the new command must **not** be run under `limited.sh`: it calls `limited.sh` for each heavy step (each `dotnet`, `content.sh` and prerender call) and `scripts/test.sh` directly. Say this in its header and its usage, and make it refuse to start when it detects that it is already inside `limited.sh` (an environment variable `limited.sh` sets, or the lock held), instead of deadlocking.
   - **Options** (names yours, documented in the header):
     - a grammar option that starts the LanguageTool server, refreshes the cache with `--prune` when there are misses, runs the rebuilt-from-nothing check, prints every new finding (the agent must still read them and fix the content or add a waiver), and always stops the server, even on a failure (a `trap`);
     - a speech option that prerenders the changed lines (print the `--check` summary first) and rewrites the manifest after it;
     - a record option, **off by default**, that re-records the end-to-end goldens and prints every moved entry (the `git diff --stat` and the count of changed entries per file), and for the fuzz stream digests prints the digests the test computed and the test to edit (do not rewrite C# from the script);
     - a tier option: `fast` (the default: the fast tier, then the fuzz suite's grammar-sensitive classes, `CorrectSentenceFuzzTests`, `MutationTagFuzzTests`, `RecastLoopFuzzTests`, `FuzzLanguageStreamTests`, at their full depth: the owner declined a shallower fuzz pass, T5 in `docs/TEST_PERFORMANCE_PLAN.md`), `full` (the whole suite, as the gate), or `none`.
   - **The save fixtures:** find every file under `tests/` that holds the old `bundleHash` and write the new one; print the files changed.
   - No command line it builds or prints for the user contains the words `eval` or `hash`, so an agent can run it directly (`content.sh hash` called inside the script is fine; the sandbox reads only the command line typed).
   - **Idempotent:** on a bundle with no edit it changes no file (prove it: run it twice on the current branch and show `git status` clean).
2. **Keep `scripts/content.sh refresh` working as it is** (the gate, the docs and CI's grammar job may use it). Do not edit `scripts/merge-gate.sh` or anything under `.github/workflows/`; if the gate or CI should call the new command, propose it in the report.
3. **Docs:** `content/README.md` ("The edit loop"), `docs/game/README.md` ("Refreshing the cache"), `scripts/README.md`, and the header of the script, so that a content milestone's brief can say "run `scripts/content-loop.sh --grammar --speech`" and nothing else. Do not edit `CLAUDE.md` or `.claude/agents/*` (the orchestrator does).
4. **Proof on a real edit**, on a throwaway branch made from yours and deleted afterwards (never pushed): add one new line to a lesson NPC's pool and one phrase variant, run the command with every option, and show the summary: the manifest, the provenance, the WAVs, the grammar cache, the save fixtures and the goldens moved, the suite passed. Then a second throwaway edit with a deliberate grammar fault in a new grammar (one the fuzz suite's correct-sentence oracle catches, for example an optional pronoun before a verb as V12 found with `qué tú hace`) and show it fails at the fuzz step in minutes, not at the full run. Put both summaries in the report; delete the throwaway branches and their WAVs.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `scripts/content.sh`, `scripts/limited.sh`, `scripts/test.sh`, `scripts/prerender-speech.sh`, `scripts/languagetool.sh`, `scripts/test-timings.py`, `scripts/README.md`.
- `content/README.md` ("The edit loop"), `docs/game/README.md` ("Refreshing the cache"), `docs/TEST_PERFORMANCE_PLAN.md` (T4, T5), `tests/Vocaterra.Nlp.Golden.Tests/EndToEndGoldenTests.cs` (the record variable), `tests/Vocaterra.Nlp.Fuzzing.Tests/FuzzLanguageStreamTests.cs` (the digests).
- The V10 to V13 entries at the end of `docs/game/IMPLEMENTATION_PLAN.md` section 5 (`grep -n '^- \*\*V1[0-3]'`), for what each content milestone ran.
- **Do not read** `docs/game/SPEECH_CHECKLIST.md`, `content/PHRASES.md`, `content/game-v1/manifest.json` or the goldens whole: grep.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file and print at most 30 lines of it; keep `sed -n` ranges under about 150 lines; wait for a long command in the foreground, not by polling a log.

## Resources (the desktop has crashed from overload)
- Every `dotnet` call, `scripts/content.sh` call, speech render and LanguageTool run goes through `scripts/limited.sh` (the new script does this for its own steps). Never start two heavy commands at once: no `&`, no background builds.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.
- Disk is tight (about 13 GB free): delete the throwaway WAVs and logs you no longer need.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/` for those until your command exists.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Rules
- A game milestone edits the game projects and their tests, `content/` (only on the throwaway branches here, except docs), `scripts/` (not `merge-gate.sh`), `assets/` and `docs/game/`; it takes the core as it is.
- Do not edit `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/IMPLEMENTATION_PLAN.md`, either `DECISIONS.md`, `CLAUDE.md`, `.claude/agents/*` or `.github/workflows/`.
- Never weaken a fuzz oracle or a gate check; the command runs the same checks faster and in order, it does not skip any the gate runs.
- Write shell that reads like the surrounding scripts (`set -uo pipefail`, the header comment style, `step` lines).

## Gate (from the worktree root)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings; `scripts/test.sh full` all passing (it was @@COUNT@@); `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh refresh` still passes on the branch.
- The new command, run twice with no edit, leaves `git status` clean; `bash -n` and `shellcheck` (if installed) on it are clean.
- Do not commit `bin/`, `obj/`, `artifacts/` or the loop's logs.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyPc3eAsDgGyBVKv6
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- The command, its options and the one line a content brief should carry.
- The two proof summaries (the real edit, and the grammar fault caught at the fuzz step, with its time).
- How the lock is handled.
- The test count before and after.
- What you propose for the gate or CI, if anything.
- Anything left undone, and why.
