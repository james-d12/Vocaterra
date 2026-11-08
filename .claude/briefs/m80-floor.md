You are finishing the core milestone **M80, the open-recognition fallback for free talk**, in the Vocaterra repository (C#, .NET 10): the owner's answer to its two false takes is **a 0.5 weakest-word floor on the free pass, in the core**. The language core is `src/Vocaterra.Nlp`, `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects. Do the work yourself; do not spawn subagents.

## What is there
The branch `M80-open-fallback` (local) holds M80 as built: `cdb37709` (the switch `PipelineOptions.OpenRecognitionFallback`, `UtteranceAnalysis.OtherPass`, `ScriptedWordRecognizer` by mode, `OpenFallbackTests`, the O14 fuzz fact, docs), `141c750a` (Bench `--open-fallback`, `--open-vosk`), `8e676da6` (the model-gated `OpenFallbackMeasurementTests`, red on two false takes) and `4e7ca7f9` (a test that typed and free-pass readings agree). Read the original brief `/home/user/Dev/Vocaterra/.claude/briefs/m80-open-fallback.md` (its rules, resources, environment and gate apply to you unchanged, with `/tmp/claude-1000/m80/` for logs) and the report `/home/user/Dev/Vocaterra/.claude/briefs/m80-report.md` (the measurement, the false takes, the floor's effect).

## The owner's answer (2026-11-07)
`docs/game/decisions.md` on `feature/bread-pilot`, "Owner's answer, 2026-11-07 (M80's two false takes)": the free pass's reading is taken only when its **weakest word's confidence is at least 0.5**; the floor is the core's. The zero-false-take bar stands.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m80`, on `M80-open-fallback`. cd there first; edit nothing elsewhere and never touch another worktree. Its build output was deleted for disk: build once first. **First merge the feature branch in**: `git merge --no-edit feature/bread-pilot` (it adds W02 and documents; there should be no conflict in your files; if there is one, resolve it keeping both sides' intent and say so). Commit with the prefix `M80: ` after each step that builds and passes its tests. **Do not push.** Run everything in the foreground and finish in one turn. Leave the worktree clean on your branch.

## What to build (each committed)
1. **The floor.** A public option on `PipelineOptions` for the free pass's weakest-word floor (name it in the style of `OpenRecognitionFallback`, default **0.5**), used only by the second, open pass: its reading is taken only when the lowest word confidence of its recognition is at least the floor; otherwise the first pass's result stands (as when the free reading is not taken). Decide and document what an empty recognition or one with no word confidences does (not taken). Validate the option's range as the other options are validated. With the fallback off, nothing changes: **no golden, identity baseline, replay or fuzz stream digest may move**; do not re-record any.
2. **Scripted tests** (no models): a free reading whose weakest word is under the floor is not taken; at exactly the floor it is; the floor does not touch the first pass; a floor of 0 behaves as before the floor. Extend the O14 fuzz oracle so it also checks that no taken free reading has a word under the floor (strengthen, never weaken).
3. **The measurement** (`OpenFallbackMeasurementTests`): run with the floor at its default; it must pass with **zero false takes** and print the figures. Keep printing what the floor removed (understood lines lost, wrong takes stopped), by voice. If a false take remains, do not weaken the oracle or drop the line: report it.
4. **Docs:** `docs/nlp/api.md` (the new option), `docs/nlp/pipeline.md`, and anything else M80 wrote that describes when the free reading is taken.

## Known flake, not yours
The fuzz oracle O7 (`IntentResolverFuzzTests`) fails on CsCheck seed `8eu5g1sTe_A8` on the feature branch (a core issue logged for the owner as M83). If the full suite fails there and nowhere else, rerun the suite once and report it; do not patch it.

## Gate (yours, before reporting)
`scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings); `scripts/test.sh full` with the models (`VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`; report the count; `feature/bread-pilot` alone has 9855 passed and 3 skipped); `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes` clean; `scripts/limited.sh scripts/content.sh validate` passes.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (half a page)
The final SHA (local); the option's name, default and validation; the tests; confirmation that goldens, identity, replay and digests are unchanged; **the measurement with the floor**: false takes (zero is the bar), wrong takes, understood after the free pass, the lines the floor lost (each with its clip, voice and reading), by voice; the test count; anything left undone.

Before your report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|limited.sh'`, kill only your own).
