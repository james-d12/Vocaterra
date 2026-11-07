You are implementing the core milestone **M84, free talk's forced rows**, in the Vocaterra repository (C#, .NET 10). The language core is the engine `src/Vocaterra.Nlp`, the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects. The game (`Vocaterra.Game*`, `Vocaterra.Host`) is not yours. Do the work yourself; do not spawn subagents.

## The problem
M80 (merged) gives a spoken line a second, open decode when the constrained decode's reading is **not taken**. V31 (merged; the game turns this on in free talk at seven prompts) found the gap: the constrained decode can force a different line onto an answer row **with confidence**, so the reading is taken and the open decode never runs. In V31's Kokoro tests (voice `ef_dora`): at the baker, «el río es grande» was heard «es gracias» and read as `thank`; at the shopkeeper, «me gusta el río» was heard «me cuesta el río» and read as `ask_price`. The owner (2026-11-07, `docs/game/decisions.md` D-game-23 item 5) asked for the fix. The row: `docs/nlp/implementation-plan.md` §5, M84.

## Read first
- `docs/nlp/implementation-plan.md` §5b, M80's entry (what was built and measured); `docs/nlp/pipeline.md` and `docs/nlp/api.md` on `OpenRecognitionFallback`, `OpenPassWordFloor`, `UtteranceAnalysis.OtherPass`.
- The reports `/home/user/Dev/Vocaterra/.claude/briefs/m80-report.md`, `m80-report-2.md` and `v31-report.md`.
- The study `docs/research/2026-11-04-spoken-off-prompt-lines.md` sections 2 and 5 (`sed -n 34,78p`, `sed -n 139,193p`): the facts about the constrained decode, `[unk]`, and the forced-but-weak count.
- The code: `src/Vocaterra.Nlp/Pipeline/UtterancePipeline.cs` (where the second pass is decided), `src/Vocaterra.Nlp/Asr/` (the recognizer's words, confidences, times, `[unk]`), and the measurement `tests/Vocaterra.Nlp.Tests/Pipeline/OpenFallbackMeasurementTests.cs`.

## What to build
1. **Measure before choosing.** Extend the model-gated measurement's corpus with V31's forced lines (render them as the existing clips are, for every voice variant M80 uses) and record, for every clip whose constrained reading is taken, the signals a trigger could use: the share of the clip's voiced audio the recognized words cover (word start and end times against the clip's speech), the clip's speech length against the row's expected length, the weakest and mean word confidence, `[unk]` tokens dropped. Compare the forced lines with the correctly heard rows.
2. **Choose a trigger** from that evidence: with `OpenRecognitionFallback` on, a taken constrained reading that looks forced also gets the open decode, and the free reading **wins only when it is taken, clears `OpenPassWordFloor`, and has a different intent**; otherwise the constrained reading stands. Name the rule and its threshold(s) as public `PipelineOptions` with defaults in the style of `OpenPassWordFloor` (validated ranges), used only when the fallback is on. Keep the exposure as it is (`OtherPass` holds the reading not chosen), so the game's V31 code picks up the result unchanged if it can; if the game would need a change (credit, logging, timing), say exactly what in the report: that would be V32, not yours.
3. **The bars**, on the extended corpus with the defaults: **zero false takes** (M80's oracle, unchanged); **zero correct rows replaced** (a clip of an answer row heard and read correctly must keep its reading); report how many forced lines the trigger rescues, the wrong takes, the extra open decodes per turn (how often the trigger fires on correct rows) and their time (median and p95). If no threshold meets both zero bars and rescues anything, do not ship a trigger that breaks a bar: report the trade-off with the figures (the owner decides).
4. **Scripted tests** (no models): the trigger fires and does not fire on each signal; a forced row replaced by a different taken free reading; a correct row kept; the option's defaults and range; nothing changes with the fallback off. Extend the O14 fuzz oracle so it checks the new rule (strengthen, never weaken). With the fallback off, **no golden, identity baseline, replay or stream digest may move**; do not re-record any.
5. **Docs:** `docs/nlp/api.md` (the new options), `docs/nlp/pipeline.md`, the fuzz README, and anything M80 wrote that describes when the second pass runs.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m84`, already on the branch `M84-forced-rows` (from `feature/bread-pilot` at its tip). cd there first; edit nothing elsewhere and never touch another worktree. The worktree is new and not built: build once first. Commit with the prefix `M84: ` after each step that builds and passes its tests. **Do not push.** Never push or merge `feature/*` or `main`. Run everything in the foreground and finish in one turn: no background commands, monitors or wait loops. Leave the worktree clean on your branch. Another agent builds in another worktree at the same time; `scripts/limited.sh` shares the machine, so a heavy command may wait for a slot.

## Resources and environment
Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Run every `dotnet build|test|format|run` and `scripts/content.sh` call through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/test.sh` calls it itself. One heavy command at a time. Exit 137 or 143 means a cap killed the command: rerun; never raise a cap. The sandbox refuses command lines containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/m84/`. Logs under `/tmp/claude-1000/m84/`; print at most 30 lines; prefer `grep -n` and `sed -n` ranges. Do not copy the repository. A timing test (`LoadingTheBundleIsFastEnough`, `TheBundleLintTakesUnderFiveSeconds`) can fail while another suite runs beside yours: if it is the only failure, rerun it alone and report both.

## Rules
Edit only the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp*`, `tools/Vocaterra.Nlp.*`, `docs/nlp/` except `implementation-plan.md` and `decisions.md`). No `content/` edit and no game project edit. Determinism: nothing in the evaluation path reads the clock or draws random numbers; clocks only in the Bench tool. A regression check is a C# test.

## Gate (yours, before reporting)
`scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings); `scripts/test.sh full` with the models (the count before you is given in your prompt); `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`; `scripts/limited.sh scripts/content.sh validate`. Do not commit `bin/`, `obj/` or `artifacts/`.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 600 words)
The final SHA (local); the signals measured (forced lines against correct rows, by voice); the trigger chosen, its options and defaults, and why; the bars (false takes, correct rows replaced: both zero, or the trade-off if not); lines rescued, wrong takes, extra decodes and their time; the tests; goldens and digests unchanged; whether the game needs a V32 and what it would change; the test count; anything left undone. The accent risk on real speech stays unmeasured: say so.

Before your report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|limited.sh'`, kill only your own).
