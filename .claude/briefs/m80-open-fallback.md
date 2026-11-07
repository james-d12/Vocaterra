You are implementing the core milestone **M80, the open-recognition fallback for free talk**, in the Vocaterra repository (C#, .NET 10). The language core is the engine `src/Vocaterra.Nlp`, the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects. The game (`Vocaterra.Game*`, `Vocaterra.Host`) uses the core; the game's use of this switch is a later milestone (V31), not yours. Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m80`, already on the branch `M80-open-fallback` (from `feature/bread-pilot`). cd there first; edit nothing elsewhere and never touch another worktree. Commit with the prefix `M80: ` after each step that builds and passes its tests. **Do not push**: the branch stays local and the gate merges it. Never push or merge `feature/*` or `main`. Run everything in the foreground and finish in one turn: no background commands or monitors, no wait loops. Leave the worktree clean on your branch.

## Resources and environment
Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Run every `dotnet build|test|format|run` and every `scripts/content.sh` call through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/test.sh` calls it itself. One heavy command at a time. Exit 137 or 143 means a cap killed the command: lower the parallelism or rerun; never raise a cap. The sandbox refuses command lines containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/m80/`. Send heavy output to logs under `/tmp/claude-1000/m80/` and print at most 30 lines. Prefer `grep -n` and `sed -n` ranges under about 150 lines. The worktree is new and not built: build once first.

## The specification
- **The design:** `docs/research/2026-11-04-spoken-off-prompt-lines.md` section 5 (`sed -n 139,193p`): the switch, when the second pass runs, which pass wins, what is exposed, replay, the test helper, the docs, the files, the scripted tests, what to measure with the models, the acceptance. Read sections 1 and 2 (`sed -n 9,78p`) for the facts it rests on. That section is the milestone; where the code and the study disagree on a name or a file, the code wins: say so in the report.
- **The owner's answers (2026-11-06)**, `docs/game/decisions.md` (`grep -n 'spoken off-prompt lines' docs/game/decisions.md`, then the six points under it): option 1 (a second, open decode only when the constrained one is not taken, only where the host turns it on); a spoken line the prompt allows but not among its rows is understood as the typed line is; **no owner recordings**: measure on the Kokoro voices only (the Spanish voices and the Italian and Portuguese stand-ins), report the accent risk as unmeasured on real speech; **zero false takes in the test corpus is the acceptance bar**. Credit (half for a line heard on the open decode) is the game's (V31), not yours.
- The core's plan row: `docs/nlp/implementation-plan.md` §5, row M80 (`grep -n '^| M80' docs/nlp/implementation-plan.md`). Do not edit the plan.
- How to change the core: `docs/nlp/changing-the-core.md`; the boundary `docs/nlp/adr/0009-core-and-game-boundary.md`; the public API record `docs/nlp/api.md` (update it for every public type you change).

## What to build (each committed)
1. The switch, the second pass and its exposure, as section 5 says, with the stages after recognition factored into one private method and the phoneme posteriors computed once.
2. The scripted tests of section 5 (no models), including the new fuzz oracle. With the option off, every golden, the identity baseline, the replay tests and the fuzz stream digests must be unchanged: **do not re-record any golden**; if one moves with the option off, that is a bug to fix.
3. The Bench tool's `--open-fallback` and a way to force Vosk for open mode; clocks only in the Bench tool.
4. The model-gated measurement test and its corpus as section 5 describes (Kokoro clips over the seven free-talk prompts; voices 28, 29 and 53 at speeds 1.0 and 0.8, `im_nicola`, `pm_santa`), marked `[Trait(TestSpeed.Trait, TestSpeed.Slow)]`. It asserts the invariants and zero false takes, and prints the figures. Render the clips the way the existing model-gated corpora are rendered (find how `ConstrainedRecognitionTests` gets its audio); generated audio that is checked in must follow that test's existing pattern.
5. The docs section 5 names.

**If the corpus shows a false take** (a line no intent should take, taken at 0.55 or more on the free pass), do not weaken the oracle or drop the line: report it with its clip, voice and reading, and say what would stop it (for example a confidence floor on the free pass). The owner decides.

## Rules
Edit only the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp*`, `tools/Vocaterra.Nlp.*`, `docs/nlp/` except `implementation-plan.md` and `decisions.md`). **No `content/` edit and no game project edit.** Never weaken a fuzz oracle. A regression check is a C# test, never a script with a text baseline. Determinism: nothing in the evaluation path reads the clock or draws random numbers.

## Gate (yours, before reporting)
`scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings); `scripts/test.sh full` with the models (**9852 passed and 3 skipped** before you; report the new count); `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes` clean; `scripts/limited.sh scripts/content.sh validate` passes. Do not commit `bin/`, `obj/` or `artifacts/`.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 600 words)
- The branch and the final commit SHA (local, not pushed).
- Each deliverable: files and public types (and the `api.md` change).
- The tests, and confirmation that the goldens, identity baseline, replay and stream digests are unchanged with the option off.
- **The measurement:** the trigger's reach, the share understood after the free pass with the right intent, the false takes (zero is the bar; list any), recognition time per pass (median and 95th percentile by clip length), the forced-but-weak count; by voice. State the accent risk as unmeasured on real speech.
- Design versus code; the test count before and after; anything left undone and why.

Before your report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|limited.sh'`, kill only your own).
