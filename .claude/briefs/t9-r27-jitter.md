You are implementing **T9, the jittering `r27-bread` capture**, in the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`; the language core (`Vocaterra.Nlp*`) is not yours. Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/t9`, already on the branch `T9-r27-jitter` (from `feature/bread-pilot` at its tip). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`adaptive`, `german`, `m83` are not yours); do not switch branches. The worktree is new and not built: build once first.
- **Commit after each step that builds and passes its tests**, with the prefix `T9: `. Never end your turn with uncommitted work.
- **Do not push.** Never push or merge `feature/*` or `main`. Leave the worktree clean on your branch.
- **Run everything in the foreground and finish in one turn.** No background commands, monitors or wait loops. Another agent builds M83 at the same time; `scripts/limited.sh` shares the machine, so a heavy command may wait for a slot.

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/t9/` for your logs and session folders, about 10 GB of disk free (**do not copy the repository or build a second checkout**: compare builds of your own worktree before and after your fix), and **9938 passed and 3 skipped** as the count before you. No `content/game-v1` edit.

## The specification
`docs/process/test-performance-plan.md`, section **T9** (and **T8** above it: how the `r30-no-classes` capture was fixed, `LaunchOptions.TurnClock`). The facts: `scripts/scripted-runs.sh` runs `r27-bread` (its row in `docs/game/scripted-runs.md`; it uses `--skip-to-session 10` and Luz's offer); the first capture's tick was 2460 on some builds and 2461 on others (W02: the base's first capture 2461, three later captures and a rebuilt base 2460; W03: 2461 before, 2460 after, 2461 on a rerun of the after build; W04 and V31: no jitter). So it can differ between two runs of one build, not only between builds. The tick is in the capture's text (`scripts/compare-runs.py` shows the difference).

## What to do
1. **Reproduce:** run `r27-bread` alone (find how `scripts/scripted-runs.sh` runs one run, or run the host with that run's arguments) at least six times on one build; record each first-capture tick. If it never jitters in six, try ten more, and with the other agent's build or test running beside you (load is a likely trigger); report the counts.
2. **Find the source** (a frame-timing race, the turn worker finishing a frame late, an asynchronous voice or speech-render step, the clock, a wait on a file, the skip's setup) and **fix it at the source**, so the capture's tick no longer depends on timing. Widening the comparison, masking the tick or excluding the run is not a fix. If the cause is in code, a C# test pins it.
3. **Prove it:** the run's first-capture tick identical in at least ten runs of the fixed build (with load beside some of them); then the whole set (`scripts/scripted-runs.sh`) before your fix and after, path masked, compared: every run unchanged apart from `r27-bread`'s tick settling on one value; explain any other difference.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
```

## Report (half a page)
The branch and final SHA (local); the reproduction counts; the cause, with evidence; the fix (files) and its test; the proof (the ten runs; the full-set comparison); the test count before and after; anything left undone. **If you cannot find the cause, do not guess a fix**: report what you ruled out, with the counts.

Before your report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs|limited.sh'`, kill only your own).
