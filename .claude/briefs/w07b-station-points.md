You are implementing the game milestone **W07b, the station view's three small points**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`; the language core (`Vocaterra.Nlp*`) is taken as it is. **This milestone's work is in the game**: three small changes to W07's station view.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/w07b`, already on the branch `W07b-station-points` (from `feature/bread-pilot` at its tip, with W05 to W07 merged). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`.claude/worktrees/adaptive` is the owner's playtest checkout: never touch it); do not switch branches.
- **Commit after each item that builds and passes its tests**, with the prefix `W07b: `. Never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there. Never push or merge `feature/bread-pilot`, `feature/adaptive_design` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish in one turn.** Do not start background commands or monitors and end your turn to wait for them.

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/w07b/` for your logs and session folders, and **9737 passed and 3 skipped** as the count before you. Read W07's report, `.claude/briefs/w07-report.md`, for what it built and the names it used. The station code: `src/Vocaterra.Game.Core/Activities/` (`StationScreen`, `StationPanel`, `InstructionStrip`, `ProcedureRunner`), `src/Vocaterra.Game/UI/Station/`.

## The three points
1. **The panel's title is the station's name, not the word "Station".** Use the station's label from the locale when one exists (a key such as `station.<id>` in `content/locale/en/`, as the design's 10.16 asks for "the station's labels"); otherwise the Spanish noun of the station's id with its article when the id is a catalogue noun (`el horno`); otherwise the id. Add the locale key's handling, not any station's label (there is no station in the bundle yet).
2. **Help selects what it highlights.** Design 10.10 (`sed -n 895,930p /tmp/claude-1000/orch/adaptation-design.txt`): "Help makes the required action explicit." After help (H, the Ayuda button, `--proc-help`), and after the highlight a third slip shows, set the held selection to the highlighted held thing and the target to the highlighted target, so Enter performs the shown act; the learner may still choose otherwise. Help's credit rules (W06) do not change. Update the selection line accordingly.
3. **The mastered recipe's one-click bake is offered at the station.** Design 10.15 (`sed -n 1000,1001p` of the same file): when the procedure is mastered (`Procedures.Progress.IsMastered`), the station offers the bake as one click through W06's `GuideSession.BakeMastered`, which logs `item_crafted`; the guided bake stays on offer and comes first (selected by default) when the goal row is due again (`PhraseProgress`'s due rule for that row); never forced, no batch. Show it as a choice in the panel by keyboard and mouse, with `ui.station*` strings in `content/locale/en/ui.json` (English support text only; the Spanish invitation "¿Hacemos pan juntos?" is W10's content, not yours). Add a scripted step only if a test needs one (for example `--proc-quick`), and document it in `docs/game/scripted-runs.md` if you do.

Tests for each: the title's three cases; the selection after help and after the third slip; the offer absent before mastery, present after it, the guided bake first when the goal row is due, the one-click bake consuming `uses` and giving `makes` once. Layout tests at every UI scale still pass with the offer shown.

**Screenshot.** As W07 did, copy the bundle to `/tmp/claude-1000/w07b/bundle/`, add a test station and procedure there (never in `content/game-v1`), re-hash the copy through a wrapper script, and capture the panel after `--proc-help` (the selection on the highlighted things, the title the station's name). Look at the PNG. Delete the copy when done. Add nothing to `content/game-v1`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words)
The branch and final SHA (local); each point: what you changed (file and type) and its tests; values chosen; the screenshot's path and what it shows; the scripted runs' differences (none expected; `r30-no-classes`' `declined.png` is known to differ between captures); the test count before and after; anything left undone. Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs'`, kill only your own).
