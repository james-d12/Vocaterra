You are implementing the game milestone **U05, the NPC's name as the conversation panel's title** (GitHub issue #6), of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game's UI only**: the name of the NPC the learner talks to is drawn at the body's size, so the panel has no title the eye finds first.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/u05`, already on the branch `U05-npc-title` (from `feature/ui-issues` at `498dbf26`, which is `main` at `ca4b69c8` plus the plan). cd there first; never edit `/home/james/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit after each item that builds and passes its tests**, with the prefix `U05: `. Never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there. Never push or merge `feature/ui-issues` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish in one turn.** Do not start background commands or monitors and end your turn to wait for them.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/u05/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with these differences:
- Your logs and session folders go under `/tmp/claude-1000/u05/`.
- The count before you is H12's gate, **10395 passed and 3 skipped**; confirm it with your first full run if the numbers differ and say so.
- **Other agents are building, testing and running the game on this machine at the same time (the owner, 2026-11-08): keep your CPU and memory use low.** `scripts/limited.sh` queues your heavy commands behind theirs; a wait of minutes is normal. Never bypass it, never raise a cap or a slot count. On top of the W07 rules:
  - While iterating, build only the projects you touch (`scripts/limited.sh dotnet build tests/Vocaterra.Game.Tests -m:2`) and test with `scripts/test.sh project <test project>` or a `--filter`; never `scripts/test.sh fast` after every small step. One `scripts/test.sh full` at the end, before the report.
  - **Game runs:** one game process at a time, never two. Capture only the scripted runs that show the dialogue panel (`scripts/scripted-runs.sh OUT <name...>`; find them with `grep` in `docs/game/scripted-runs.md`), not the whole set while iterating. The whole set once before the report and once as the baseline before your first edit, each in the foreground, one at a time.
  - Close every game you launch before the next command; check with `pgrep -fa Vocaterra.Game` that none of yours is left.
- This milestone does **not** edit `content/game-v1`. It may edit `content/locale` (outside game-v1's hash) only if a string must change; none is expected.
- Rules' edit list for you: `src/Vocaterra.Game`, `src/Vocaterra.Game.Core` (only if a model-side change is needed), their tests (`tests/Vocaterra.Game*.Tests`), `docs/game/running.md` and `docs/game/architecture.md` where they describe the panel's header, and the scripted-run baselines that move (see below).

## The specification
The plan's section 4.10 (`docs/game/implementation-plan.md`, `grep -n "### 4.10" docs/game/implementation-plan.md`, then read the section's findings and its **U05** paragraph). In short, the investigation found: `DialoguePanel.Draw` (`src/Vocaterra.Game/UI/Dialogue/DialoguePanel.cs`) draws `dialogue.NpcName` at `FontProvider.NativeSize` (16 px) in `NameColor`; `DrawLessonHeading` puts the lesson's heading beside it in the brighter `HintColor` at the same size; every NPC line repeats the name as a prefix ("Guía: "); `DialogueLayout` (`src/Vocaterra.Game/UI/Dialogue/DialogueLayout.cs`) places the lines at a fixed `Panel.Y + 8 + LineHeight + 6`.

The owner's issue, verbatim: "Increase size of the NPC name in the chat window, its not obvious and doesnt follow simple hierachy rules. The name should be large so its understood who we are talking to."

## What to build (each committed)
1. **The header's height in the layout.** A `DialogueLayout.HeaderHeight` (or a per-layout property, since the compact panel differs) that `LinesTop`, the status and the lesson heading all use, replacing the hard-coded `8 + LineHeight + 6`. No visual change yet; existing tests pass.
2. **The name at 32 px.** `FontProvider.NativeSize * 2` (PixelOperator is sharp only at whole multiples of 16; the book's titles already use 32), in `NameColor`, with the rule under it moved down to suit. The header grows by about 12 px. **In the compact panel** (`DialogueLayout.CompactBelow`, windows under 540 px high) the name stays at 16 px so the few rows are kept.
3. **The lesson heading stops competing.** It stays in the header band, to the right of the name (its x measured with the font the name was drawn in), in the muted colour (`UiPalette.Muted`, the panel's `NoteColor`) instead of `HintColor`, aligned to the name's baseline; still shortened with `FitTo` and still clear of the status room (`HeaderStatusRoom`). Listening/Thinking/notice status unchanged.
4. **The NPC's lines lose the "Name: " prefix** (the speakers `Guide`, `World` and `Correction` in `DrawLines`). Their colours already set them apart; the learner's "You: " / "You (voice): " prefixes and the English lines' prefix stay. Check that nothing else (scripted-run stdout, the report, tests) depends on the prefix; say what you found.
5. **Fit.** U02b's promise holds: the current exchange fits at 800 x 600 and at 150 % UI scale (`ConversationSurfaceLayoutTests`, `PanelFitLayoutTests`, `UiScaleLayoutTests`, `ConversationLayoutTests`). If the taller header breaks a fit test, find the reason; do not weaken the assertion. If the only way is fewer rows, say so in the report with the numbers.
6. **Tests:** the header height for the normal and compact panels; the lines start below the larger header; the buttons' hit boxes unchanged for the same state; the NPC lines drawn without the prefix and the learner's with it (test the text the panel builds; factor a pure helper out of `DrawLines` if needed, no window).

**Screenshots.** Capture the panel with a scripted run (`docs/game/scripted-runs.md`; e.g. the run that talks to the guide) before and after, at the default size and at 800 x 600; look at the PNGs; keep them under `/tmp/claude-1000/u05/`.

**Scripted runs.** Pictures that show the dialogue panel will move (expected); the stdout lines and the world digests must not. List every run whose pictures differ and confirm each is the header change. Re-record the baselines the way `docs/game/scripted-runs.md` says, if baselines are committed.

Out of scope: scrolling and the mouse wheel (U06), per-NPC pages (U06), the book (U07), saving the history (U08), the bubble over the NPC's head (`DialogueBubbleLayer`, keeps its small name).

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words), also written to `/tmp/claude-1000/u05/report.md`
- The branch and the final commit SHA (local, not pushed).
- Each item: what changed (file and member), its tests.
- The header height (normal and compact) and the rows of conversation at 800 x 600 before and after.
- What depended on the "Name: " prefix, if anything.
- The screenshots' paths and what each shows.
- The scripted runs' differences, the test count before and after, anything left undone and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs'`, kill only your own).
