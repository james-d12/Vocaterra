You are implementing **T8, the nondeterministic `r30-no-classes` capture**, for the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`; the language core (`Vocaterra.Nlp*`) is taken as it is. **This work is in the game and its scripted-run tooling.**

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/t8`, already on the branch `T8-r30-determinism` (from `feature/bread-pilot` at its tip). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`.claude/worktrees/adaptive` is the owner's playtest checkout: never touch it); do not switch branches.
- **Commit after each step that builds and passes its tests**, with the prefix `T8: `. Never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there. Never push or merge `feature/bread-pilot`, `feature/adaptive_design` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish in one turn.** Do not start background commands or monitors and end your turn to wait for them; earlier agents did that and stopped with their work unchecked. A single scripted run takes about a minute in the foreground; wait for it.

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment** and **Rules**: they apply to you unchanged, with `/tmp/claude-1000/t8/` for your logs and session folders. You may also edit `scripts/scripted-runs.sh`, `scripts/compare-runs.py` and `docs/game/scripted-runs.md`.

## The problem
`docs/process/test-performance-plan.md`, section **T8** (`grep -n '### T8' …`). W05 and W07 found that two captures of `r30-no-classes` from one build differ in `declined.png`'s bytes, so `python3 -I scripts/compare-runs.py BASE NEW` reports a difference no change caused. r30's line is in `scripts/scripted-runs.sh` (`grep -n r30 scripts/scripted-runs.sh`): `declined.png` is shot right after `--say "¿dónde está el río?"` at the villager. `docs/game/scripted-runs.md` (`sed -n 20,40p` and `sed -n 128,140p`) explains the masks and the order-only rule, and notes that a turn's latency label has moved pictures before (r19, r24).

## What to do
1. **Reproduce.** Build the game once (`scripts/limited.sh dotnet build src/Vocaterra.Game -m:4`), then capture r30 alone three times into separate folders. scripted-runs.sh takes run names after the output folder (`scripts/scripted-runs.sh <dir> r30-no-classes`); run it through `scripts/limited.sh` if it does not call it itself. Diff the three `declined.png` files pixel by pixel with a short Python script (`python3 -I`, Pillow if present, else compare the raw bytes after decoding with the standard library's `zlib`) and find the region that differs. Write down its box.
2. **Find the source** of that region: a latency or timing label, the "thinking" indicator's frame, a voice or turn worker that finishes on another frame, the clock, an unseeded draw, the frame counter of `--shot`. Read the code that draws that part of the screen.
3. **Fix it at the source** so that a scripted run's picture does not depend on timing: for example, the scripted path waits for the turn to finish before `--shot`, or a label that shows wall time is hidden or fixed in scripted runs, as other runs' latency labels were handled. **Widening the comparison's tolerance, masking the region or dropping the picture is not a fix**; if the only honest fix is a mask (the region shows real latency that a player should see), stop and say so in the report with the evidence, without making it.
4. **Pin it** with a C# test if the cause is in code (for example: the scripted wait waits for the turn; the label is fixed in scripted runs).
5. **Prove it**: three captures of r30 from the fixed build are identical (`compare-runs.py` says same for each pair); then the whole set once before your change and once after (`scripts/scripted-runs.sh` with no run names, before into one folder and after into another, compared with `compare-runs.py`): no run moves except as your fix explains.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full`, all passing: report the count (the gate before you is recorded in `docs/game/implementation-plan.md` section 5's last entry).
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words)
The branch and final SHA (local); the differing region's box and what draws it; the cause; the fix (file and type) and its test; the three captures' comparison and the whole set's comparison; the test count; anything left undone. Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs'`, kill only your own).
