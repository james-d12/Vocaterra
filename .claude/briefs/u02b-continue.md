You are implementing the game milestone **U02b, the conversation surface's QA fixes**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.Game.Core` (engine-free logic), `Vocaterra.Game` (MonoGame) and `Vocaterra.Host`.

Do the work yourself; do not spawn subagents.

## You continue a first attempt (read this first)
A first agent did most of U02b and stopped; its work is committed on `U02b-qa-fixes` at `a2860c96` (report: `.claude/briefs/u02b-report-1.md`). Do not redo what it did; check it and finish:

1. **Correct items 3 and 4 (the first attempt went the wrong way).** «gracias» at the shopkeeper's prompt is not a line the NPC failed to understand: the core found an intent off the prompt (`GuideTurn.OffPromptIntent`, see its doc comment in `src/Vocaterra.Game.Core/Npc/GuideTurn.cs`: such a line is never `Communicated`, but it is `Taken`), and the NPC answered it with its off-prompt line «Te entiendo. ¿Quieres comprar algo?». So:
   - **ui-4 is not a bug:** restore U02's rule in `DialogueModel.CoachingFor`: a turn not `Taken` gets the not-understood feedback; a turn with `OffPromptIntent` (typed, not `LessonHandled`, as U02 had it) gets `ui.coachOffPrompt` ("The NPC understood, but that is not what was asked. Answer the question to practise it."); restore that string and its key in `LocaleText`. Restore U02's test of it (the first attempt renamed and rewrote `ATypedLineOffThePrompt…`; bring back its U02 meaning).
   - **ui-3, corrected:** `LocaleText.FeedbackFor` must not say "Good. {npc} understood you." for an off-prompt turn, and must not say "did not understand" either (the NPC replied "Te entiendo"). For a turn with `OffPromptIntent`, give the off-prompt wording instead of the outcome line (reuse `coachOffPrompt`'s text, or a new `feedback.outcome.off_prompt` in `content/locale/en/feedback.json`: "{npc} understood, but that is not what was asked."; your choice, say which). A turn neither communicated nor taken keeps `not_understood`; a communicated turn keeps `understood` / `understood_with_mistake`. Tests for all four cases.
2. **Finish the gate and checks the first attempt left:**
   - The scripted runs' clean baseline: the first agent started `scripts/scripted-runs.sh /tmp/claude-1000/u02b/base2` from a temporary worktree `/tmp/claude-1000/u02b/basewt` at `d647908a`. If it is still running (`pgrep -fa scripted-runs`), wait for it in the foreground (`while pgrep -f 'scripted-runs.sh /tmp/claude-1000/u02b/base2' >/dev/null; do sleep 20; done` is acceptable here, in the foreground, and it ends when the run does; check its log `/tmp/claude-1000/u02b/base2.log` ends with `exit`). Then re-capture the after runs from your final commit (`/tmp/claude-1000/u02b/after2`), compare (`python3 -I scripts/compare-runs.py /tmp/claude-1000/u02b/base2 /tmp/claude-1000/u02b/after2`) and explain each difference.
   - Then remove the temporary worktree: `git worktree remove --force /tmp/claude-1000/u02b/basewt` (run from your worktree).
   - The behaviour screenshots listed at the end of this brief (look at each), plus «gracias» alone showing the off-prompt coaching and no "Good" line.
   - `scripts/test.sh full` once more (9561 passed and 3 skipped before U02b, plus your new tests: report the total from the run's summary line, the sum over every assembly); CI's case once (the full suite with `VOCATERRA_MODELS_DIR` an empty folder); build with `-warnaserror`; format; `content.sh validate`.
3. Never start anything with `&`. Finish with `git status` clean and no process of yours running.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/u02b`, already on the branch `U02b-qa-fixes` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit often, push never** (the owner's rules, 2026-10-30 and 2026-11-05): commit after each item that builds and passes its tests, with the prefix `U02b: `. Do not push the branch: it stays local and the gate merges it from there.
- Never push anything (not `main`, not `feature/adaptive_design`, not your branch), and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
U02 (gated 2026-11-06 at fe986862) built the conversation surface: the dialogue panel shows the current exchange, older lines behind PageUp/O, one coaching line, the lesson heading in the panel header. Its findings entry is in `docs/game/implementation-plan.md` section 5 (search "U02, the conversation surface"); its report `.claude/briefs/u02-report.md`. A QA pass found the bugs below; the report is `docs/qa-reports/2026-11-06-ui.md` (read its "Bugs" section; its screenshots are under `/tmp/vocaterra-qa/ui2/`). Fix each, with a test that fails before the fix:

1. **QA-2026-11-06-ui-1, the expanded panel cuts the oldest lines.** `DialoguePanel.DrawLines` (`src/Vocaterra.Game/UI/Dialogue/DialoguePanel.cs`) draws only the newest rows that fit (`first = rows.Count - capacity`) and then shows no hidden-lines row. Expanded must make every older line reachable: scroll within the panel (PageUp again, or PageUp/PageDown, or the arrow keys while expanded; your choice, keep Tab as Escribir per D-game-9 and do not take a key the dialogue already uses), and whenever rows are out of view say so with a row ("N earlier lines above"). Put the logic (which rows show, the scroll offset, the count of hidden rows) in `Vocaterra.Game.Core` so it is unit-tested; the Game project only draws.
2. **QA-2026-11-06-ui-2, at 800x600 and 150 % text the collapsed panel shows only the coaching line.** The current exchange (the learner's line, the NPC's reply, the coaching line) must stay readable at every supported size and text scale (800x600 and 1280x720 at 100, 125 and 150 %): when it cannot all fit, drop the reply's English before the Spanish, and the hidden-lines row before the current exchange, never the learner's line or the reply. Test the fit with the layout tests' pattern (`tests/Vocaterra.Game.Tests/UI/Dialogue/ConversationSurfaceLayoutTests.cs`).
3. **QA-2026-11-06-ui-3, a turn not understood shows "Good. The shopkeeper understood you."** Reproduce with `--seed 42 --mute --talk shopkeeper --say "gracias" --expand --screenshot <png> --frames 10` (stdout: `"gracias" -> unknown, not understood`). Find where the game picks `feedback.outcome.understood` (`content/locale/en/feedback.json`) for that turn. If the game picks the wrong key, fix it in the game. If the core reports the turn as understood in the field the game reads, do not patch the core: report the core field and value, and make the game not contradict its own "not understood" verdict if it can from what it has.
4. **QA-2026-11-06-ui-4, a typed turn not understood gets the off-prompt coaching line** ("The NPC understood, but that is not what was asked"). `DialogueModel.CoachingFor`: off prompt applies only to a turn the core understood as another intent; a turn not understood gets a fitting line (the existing locale feedback for not understood if there is one; otherwise a new `ui.*` string in plain English, with the help keys, for example "Not understood. Try a phrase from the card, or press H for help.").
5. **QA-2026-11-06-ui-5, at 800x600 and 150 % text the lesson heading is cut before the step total and kind.** Shorten from the left part first (the lesson's title) so `step n of m · kind` stays; test it at the sizes above.
6. **The hidden-lines row's plural:** "1 earlier lines hidden" must read "1 earlier line hidden" (the locale string takes a singular and a plural form; follow how `content/locale/en/ui.json` already handles a count, if it does).

Keep every D-game-9 route reachable by key and its tests unchanged. Update `docs/game/running.md` for any new key.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/u02b/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 13 GB free once you build. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- **You are the only agent building.** A known issue: `scripts/test.sh full` can be killed by the OOM killer (exit 143, "Test host process crashed") when `Vocaterra.Game.Core.Tests` (about 5.4 GB alone) overlaps another test assembly inside the 6 GB cap; it is not yours (a fix, T7, is proposed). If that happens, rerun once and say so in the report; never raise a cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/u02b/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/u02b/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
- After any edit of `content/game-v1`: `scripts/content-loop.sh --speech` (it refreshes the hash, the save fixtures, the LanguageTool cache and the WAVs; `--record` when the goldens move; `--tier full` once before the report). Content outside game-v1's hash: `content/locale/` (the English), `content/schema/`. How to edit content: `content/README.md`.

## Rules
- **What this milestone edits:** the game's code and tests (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tests/Vocaterra.Game*.Tests`, `tests/Vocaterra.Host.Tests`), `content/game-v1`, `content/locale`, `content/schema`, `assets/` and `docs/game/` (not the plans or the decisions).
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched; a core test that fails after a content edit is wanted feedback: report it.
- A regression check is a C# test, never a script with text baselines.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`). Logic in Core, drawing and devices in Game (`docs/process/code-guidelines.md`, `docs/game/architecture.md`).
- Do not edit either `implementation-plan.md`, `docs/game/activities-plan.md`, `docs/game/village-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, the design `.docx`, or anything under `.claude/` or `.github/workflows/`.
- Determinism: no `System.Random` and no wall clock in the game's logic; the host owns the stored time, the seed and the repeat counts. Assets CC0 only (D-game-5).
- Every save version from 1 loads; a save round-trips.

## The grammar cache
The LanguageTool check works from this sandbox (since 2026-11-05): after a content edit that adds Spanish, `scripts/content-loop.sh --grammar --speech` refreshes `content/grammar/` and reports new findings; never edit `content/grammar/` by hand. This milestone adds no Spanish unless a line is unavoidable (a hint is English, the support language); if it edits `content/game-v1`, run the loop with `--grammar --speech`.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9561 passed and 3 skipped before you; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`).
- **The scripted runs** (`docs/game/scripted-runs.md`): `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`, one at a time through `scripts/limited.sh`. Explain every difference; re-baseline only the runs this milestone is meant to move.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any wall-clock test within four times of its budget.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean (the branch committed, not pushed).

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf
```

## Report (one page, under 500 words)
- The branch and the final commit SHA (local).
- Each deliverable: what you built (file and type), its tests.
- **Values chosen** where the design left one open, and every place the design and the code disagreed.
- The behaviour checks' lines; the scripted runs' differences; the save versions tested.
- The test count before and after; CI's case; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).


## Read first (the sections named, not whole files)
- `docs/qa-reports/2026-11-06-ui.md`, "Bugs" (and look at the screenshots it names that you need).
- `.claude/briefs/u02-report.md`; `docs/game/implementation-plan.md` section 5, the entry "U02, the conversation surface".
- `src/Vocaterra.Game.Core/Dialogue/DialogueModel.cs` (`PanelRows`, `CoachingLine`, `CoachingFor`, `HiddenLineCount`), `PanelRow.cs`, `TextWrap.cs`; `src/Vocaterra.Game/UI/Dialogue/DialoguePanel.cs`; `src/Vocaterra.Game.Core/Dialogue/DialogueScreen.cs` (the keys); the tests `tests/Vocaterra.Game.Core.Tests/Dialogue/ConversationSurfaceTests.cs` and `tests/Vocaterra.Game.Tests/UI/Dialogue/ConversationSurfaceLayoutTests.cs`.
- `docs/game/decisions.md` D-game-9 (the keys that must stay reachable).

## Behaviour checks (in the report)
Screenshots (look at each) of: the QA's commands for ui-1 to ui-5 after the fix (expanded with 3 and 8 turns, scrolled to the top; 800x600 at 150 % collapsed and expanded; «gracias» alone; the lesson heading at 800x600 and 150 %).
