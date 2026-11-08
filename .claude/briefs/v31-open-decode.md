You are implementing the game milestone **V31, spoken lines heard on the open decode**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`; the language core (`Vocaterra.Nlp*`) is not yours. The core's M80 (merged into `feature/bread-pilot`) added a second, open decode for spoken lines; **V31 turns it on in free talk and handles what it hears**.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/v31`, already on the branch `V31-open-decode` (from `feature/bread-pilot` at its tip, with M80 merged). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`adaptive`, `german` and `w03` are not yours: never touch them); do not switch branches. The worktree is new and not built: build once first.
- **Commit after each item that builds and passes its tests**, with the prefix `V31: `. Never end your turn with uncommitted work.
- **Do not push.** Never push or merge `feature/*` or `main`. Leave the worktree clean on your branch.
- **Run everything in the foreground and finish in one turn.** No background commands, monitors or wait loops. Another agent builds in another worktree at the same time; `scripts/limited.sh` shares the machine, so a heavy command may wait for a slot.

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/v31/` for your logs and session folders, about 8 GB of disk free (build once; do not copy the repository), and the count M80's gate reports (given in your prompt) as the count before you. Run the full suite **with the models** (`VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`): your acceptance tests are model-gated. **No `content/game-v1` edit** is expected; if you find you need one, the Rules' content-loop point applies; say why in the report. **No core edit**: a need in the core is reported, not patched. Do not edit `docs/game/implementation-plan.md`, `docs/game/village-plan.md` or `docs/game/decisions.md`: put the text the orchestrator should add there (the D-game-9 note, the village plan's open point to close) in your report.

## The specification
- **The design:** `docs/research/2026-11-04-spoken-off-prompt-lines.md` section 6 (`sed -n 194,213p`), with sections 1 and 2 (`sed -n 9,78p`) for the facts. That section is the milestone. Where it and the code disagree on a name or a line number, the code wins; say so in the report.
- **The owner's answers** (`docs/game/decisions.md`, `grep -n 'spoken off-prompt lines' docs/game/decisions.md` and the six points under it, plus "Owner's answer, 2026-11-07 (M80's two false takes)"): the open decode only in free talk, on the **seven free-talk prompts** (the four NPC open prompts, `stall-need`, `stall-offer`, `guide-offer`); lesson steps, reviews and the mic check stay constrained and decode once. A spoken line the prompt allows but not among its rows is understood and answered as the same typed line is. **Credit for a line heard on the open decode: half, as a typed line.** The 0.5 weakest-word floor is the core's (`PipelineOptions.OpenPassWordFloor`, default): use the default; do not add a second floor in the game.
- **What M80 built:** `docs/nlp/api.md` (`PipelineOptions.OpenRecognitionFallback`, `OpenPassWordFloor`, `UtteranceAnalysis.OtherPass`) and `docs/nlp/pipeline.md`; the report `/home/user/Dev/Vocaterra/.claude/briefs/m80-report-2.md`.

## What to build (each committed)
1. **The switch in free talk:** `SpokenUtteranceEvaluator` (or the code's equivalent) holds two pipelines, the fallback on only for free talk (`scene.PromptOverride is null`, or the code's test) at the seven prompts; everything else unchanged.
2. **Line reading:** `LineReading` tries the vocative and greeting readings on `OtherPass` when the chosen reading is not taken.
3. **Credit:** a line taken on the open decode is credited as a typed line (half), wherever spoken and typed credit differ today.
4. **Timings and telemetry:** `TimedRecognizer` sums the decodes of a turn and `TurnTimings` shows the count; the session log and the voice event mark a line heard on the open decode. A scripted run must still show its turn timings at 0.0 ms (T8): keep that.
5. **Tests:** the five of section 6 (Kokoro model tests at each affected NPC: a spoken off-prompt line gets its «Te entiendo…»; a spoken allowed line outside the rows is understood; a lesson step decodes once, with a counting recognizer; `ConstrainedRecognitionTests`' garbled stall line still ends in choices; a low-confidence free pass keeps «No te entendí bien»), plus: half credit for an open-decode line; the mic check and a review decode once; the telemetry mark. Model tests are marked `[Trait(TestSpeed.Trait, TestSpeed.Slow)]` and render their clips the way the existing model-gated game tests do.
6. **Docs:** the speech bullet of `docs/game/architecture.md`. The D-game-9 note and the village plan's open point: text in your report.
7. **The scripted runs** (the Gate section): capture before your first edit into `/tmp/claude-1000/v31/runs-before` and after; mask the work-folder path. Runs are typed, so they should be unchanged; explain any difference. `r27-bread` once took its first capture one tick late (2461 instead of 2460); if you see that, rerun it once and report both.

## Known flake, not yours
The fuzz oracle O7 (`IntentResolverFuzzTests`) fails on CsCheck seed `8eu5g1sTe_A8` (a core issue, proposed as M83). If the full suite fails there and nowhere else, rerun once and report it; do not patch the core.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 600 words)
The branch and final SHA (local); each deliverable (files, types); the tests and what the model tests heard (by voice and prompt, any line not understood); the credit and telemetry; the scripted-run comparison; the test count before and after; the D-game-9 note and the village plan text for the orchestrator; design versus code; anything left undone.

Before your report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs|limited.sh'`, kill only your own).
