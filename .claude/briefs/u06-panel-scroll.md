You are implementing the game milestone **U06, the conversation panel scrolls** (GitHub issue #5, step 1), of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game's UI and its dialogue model**: the panel's older lines exist but are reached only by PageUp/O and the arrow keys, nothing on screen shows them once room is short, and the history is wiped when the learner talks to another NPC.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/u06`, already on the branch `U06-panel-scroll` (from `feature/ui-issues` at `10f81caf`, which has U05 merged: the panel's 32 px title, `DialogueLayout.Header`, `DialoguePanel.LinePrefix`). cd there first; never edit `/home/james/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit after each item that builds and passes its tests**, with the prefix `U06: `. Never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there. Never push or merge `feature/ui-issues` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish in one turn.** Do not start background commands or monitors and end your turn to wait for them.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/u06/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with these differences:
- Your logs and session folders go under `/tmp/claude-1000/u06/`.
- The count before you is U05's gate, **10402 passed and 3 skipped**.
- **Other agents are building, testing and running the game on this machine at the same time (the owner, 2026-11-08): keep your CPU and memory use low.** `scripts/limited.sh` queues your heavy commands behind theirs; a wait of minutes is normal. Never bypass it, never raise a cap or a slot count. On top of the W07 rules:
  - While iterating, build only the projects you touch (`scripts/limited.sh dotnet build tests/Vocaterra.Game.Core.Tests -m:2`, the same for `tests/Vocaterra.Game.Tests`) and test with `scripts/test.sh project <test project>` or a `--filter`; never `scripts/test.sh fast` after every small step. One `scripts/test.sh full` at the end, before the report.
  - **Game runs:** one game process at a time, never two. While iterating, capture only the scripted runs that show the dialogue panel (`scripts/scripted-runs.sh OUT <name...>`; U05 found `r27-bread` and `r30-no-classes` among them; find others with `grep` in `docs/game/scripted-runs.md`). The whole set once as the baseline before your first edit and once before the report, each in the foreground, one at a time.
  - Close every game you launch before the next command; check with `pgrep -fa Vocaterra.Game` that none of yours is left.
- This milestone does **not** edit `content/game-v1`. It may edit `content/locale/en/ui.json` (outside game-v1's hash) for the hint strings below.
- Rules' edit list for you: `src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, their tests (`tests/Vocaterra.Game*.Tests`), `content/locale/en/ui.json`, `docs/game/running.md`, `docs/game/architecture.md` and `docs/game/scripted-runs.md` where they describe the panel's scrolling.

## The specification
The plan's section 4.10 (`docs/game/implementation-plan.md`; `grep -n "### 4.10"`, then read the findings' **#5** bullets and the **U06** paragraph). The owner's issue, verbatim: "In the user interface, when we are interacting with NPCs, we should be allowed to scroll through the chat window to view the full history." Saving the history across restarts is **U08, not yours**; keep the per-NPC pages in a shape U08 can save (plain lists of `DialogueLine` keyed by NPC id).

What exists (read these first): `DialogueModel` (`src/Vocaterra.Game.Core/Dialogue/DialogueModel.cs`: `MaxLines`, `_lines`, `Expanded`, `ToggleExpanded`, `ScrollBack`, `ScrollOlder`, `ScrollNewer`, `PanelRows`, `SwitchTo`, `Open`, `Add`); `DialogueScreen` (`src/Vocaterra.Game.Core/Dialogue/DialogueScreen.cs`, the keys, around line 168: PageUp/O, Up/Down); `PanelFitter` (`src/Vocaterra.Game.Core/Dialogue/PanelFit.cs`); `DialoguePanel.DrawLines` (`src/Vocaterra.Game/UI/Dialogue/DialoguePanel.cs`); the book's wheel, the pattern to follow (`BookScreen.HandleMouse`, `src/Vocaterra.Game.Core/Language/Book/BookScreen.cs`, `input.Wheel`, 40 units a notch); the world's wheel changes the hotbar slot (`src/Vocaterra.Game/Input/WorldInput.cs`).

## What to build (each committed)
1. **The wheel scrolls the panel.** In `DialogueScreen` (Core, through `IInputFrame.Wheel`): a notch up on a collapsed panel expands it (as PageUp) and further notches scroll older (`ScrollOlder`, one block a notch); a notch down scrolls newer, and a notch down at the newest line (`ScrollBack == 0`) collapses the panel back to the current exchange. Works while typing too (the wheel types nothing). **While the panel is open the wheel must not also change the hotbar slot**: find where the world's input runs while a dialogue is open and prove it with a test. PageUp, O, Up and Down keep working unchanged (D-game-9: every route by key).
2. **A scrollbar.** A thin bar (about 4 px) at the right edge of the lines' area whenever lines are out of view (collapsed with hidden lines, or expanded with lines above or below): the track over the lines' height, the thumb's size and place from the fit (rows shown against all rows; its place from `ScrollBack`). Compute it in a pure, testable type (Core or a Game layout record, no SpriteBatch); `DialoguePanel` only draws it, in the panel's muted colours. It is not clickable (out of scope). The "N earlier lines hidden" / "above" / "below" rows stay as they are when there is room; the bar shows even when `PanelFitter` drops those rows.
3. **The hint strings name the wheel.** `ui.dialogueOlder.*` "(PageUp or O)" becomes "(scroll up or PageUp)", `ui.dialogueAbove.*` "(Up to scroll)" becomes "(scroll or Up)", `ui.dialogueBelow.*` "(Down to scroll)" becomes "(scroll or Down)"; keep each plural pair, check every test that pins the old text. The controls help (F1) gains the wheel for the panel if it lists the panel's keys (find it; `content/game-v1/game/controls.json` is inside game-v1's hash: **if the panel's controls live there, leave them and say so in the report** rather than editing game-v1).
4. **One page per NPC.** `DialogueModel` keeps a page of lines for each NPC id (a dictionary of lists) instead of one list that `SwitchTo` clears: talking to the baker and coming back to Luna shows Luna's earlier lines. What resets on a switch stays as now (`Heard`, `Choices`, `HighlightedLine`, `UncertainStreak`, `CoachingLine`, `Expanded`, `ScrollBack`). `MaxLines` applies per page and rises to 200. Check the opening rule (`Open`: lines added when `opening.HostTurn || first || _lines.Count == 0`): with pages kept, `_lines.Count == 0` means "this NPC's page is empty"; keep the behaviour the existing tests expect and say in the report if any test had to change and why. `Lines` (and anything the book or the scripted runs read) shows the open NPC's page.
5. **Tests (Core first, no window):** wheel up expands and scrolls, wheel down to the newest collapses; the wheel does nothing to the hotbar while the panel is open; the scrollbar's thumb for collapsed, expanded at the newest, and scrolled to the oldest; pages per NPC kept across a switch and the cap per page; the opening rule; the hint strings.

**Screenshots.** Capture the panel collapsed with hidden lines (the bar shows) and expanded, scrolled back (the thumb moved), with a scripted run (`--expand` exists; add a scripted step for scrolling only if one is needed and cheap, in the existing `ScenarioScript` style, and document it in `docs/game/scripted-runs.md` and `running.md`). Default size and 800 x 600. Look at the PNGs; keep them under `/tmp/claude-1000/u06/`.

**Scripted runs.** Pictures with the panel may move (the bar); stdout lines and world digests must not, unless a run talks to two NPCs in turn and now keeps the first one's page: then explain each difference.

Out of scope: saving the pages (U08), clicking or dragging the bar, the book (U07), the bubble over the head.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words), also written to `/tmp/claude-1000/u06/report.md`
- The branch and the final commit SHA (local, not pushed).
- Each item: what changed (file and member), its tests.
- Where the hotbar's wheel was stopped while the panel is open; where the controls help lists the panel's keys and whether you changed it.
- Any existing test that changed, and why.
- The screenshots' paths and what each shows.
- The scripted runs' differences, the test count before and after, anything left undone and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs'`, kill only your own).
