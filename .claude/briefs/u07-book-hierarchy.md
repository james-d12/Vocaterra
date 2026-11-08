You are implementing the game milestone **U07, the book's hierarchy** (GitHub issue #7), of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game's book**: its five pages (Nouns, Verbs, Phrases, Lessons, What I said) have no visual hierarchy, and the owner approved a mockup that gives them one.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/u07`, already on the branch `U07-book-hierarchy` (from `feature/ui-issues` at its tip, with U05 and U06 merged). cd there first; never edit `/home/james/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit after each item that builds and passes its tests**, with the prefix `U07: `. Never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there. Never push or merge `feature/ui-issues` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish in one turn.** Do not start background commands or monitors and end your turn to wait for them.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/u07/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with these differences:
- Your logs and session folders go under `/tmp/claude-1000/u07/` (the mockup's files are already there; do not delete them).
- The count before you is U06's gate, **10419 passed and 3 skipped**.
- **Another agent (U08, the conversation history in the save) builds and tests at the same time as you, and other sessions use this machine too (the owner, 2026-11-08): keep your CPU and memory use low.** `scripts/limited.sh` queues your heavy commands; a wait of minutes is normal. Never bypass it, never raise a cap or a slot count. On top of the W07 rules:
  - While iterating, build only the projects you touch (`scripts/limited.sh dotnet build tests/Vocaterra.Game.Core.Tests -m:2`, the same for `tests/Vocaterra.Game.Tests`) and test with `scripts/test.sh project <test project>` or a `--filter`; never `scripts/test.sh fast` after every small step. One `scripts/test.sh full` at the end, before the report.
  - **Game runs:** one game process at a time, never two. While iterating, capture only the scripted runs that show the book (`grep -n "book\|--journal\|--book" docs/game/scripted-runs.md`; `r20-sleep` and `r21-skip` show the Lessons page). The whole set once as the baseline before your first edit and once before the report, each in the foreground, one at a time.
  - Close every game you launch before the next command; check with `pgrep -fa Vocaterra.Game` that none of yours is left.
- This milestone does **not** edit `content/game-v1`. It may edit `content/locale/en/ui.json` (outside game-v1's hash; then `scripts/content.sh refresh` as U06 did, and say so) for new book strings: section names, the right page's labels, "(nothing heard)", the pips' key.
- Rules' edit list for you: `src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, their tests (`tests/Vocaterra.Game*.Tests`), `content/locale/en/ui.json` and its manifest, `docs/game/running.md` and `docs/game/architecture.md` where they describe the book.

## The specification
- **The spec: `/home/james/Dev/Vocaterra/.claude/worktrees/u07/.claude/briefs/u07-book-spec.md`.** It is the milestone: sizes and inks, the line kinds, each page's list and right page, what must stay, the tests. Follow it; where the code shows it cannot work as written, choose the smallest change that keeps its intent and say so in the report.
- **The owner's decisions: `docs/game/decisions.md`, D-game-27** (`grep -n "D-game-27"`): the double strike only for known entries, the right page's section labels and the next lesson's line; a lesson's phrases move to the right page with their speaker buttons (right-page hit tests); Phrases grouped by help.
- **The mockup** the owner approved: `/tmp/claude-1000/u07/mockup.html` (539 KB; the spreads drawn at the game's pixel sizes). Read its text with the data URIs stripped (`python3 -I` with a regex replacing `data:[^)'"]+` by `DATA`), never the whole file raw. Match it where the spec is silent.
- The plan's section 4.10 (`docs/game/implementation-plan.md`, the #7 findings and the U07 paragraph) for the background.
- The code: `src/Vocaterra.Game.Core/Language/Book/` (`BookPages`, `BookLine`, `BookTone`, `BookGeometry`, `BookSpeakers`, `BookSpeechControls`, `BookPractise`, `BookScreen`, `BookModel`), `src/Vocaterra.Game.Core/Language/Words/JournalRows.cs`, `src/Vocaterra.Game/UI/Book/BookLayer.cs` and `ConjugationLayout.cs`, `src/Vocaterra.Game/UI/Common/UiPalette.cs` (the inks, `Ribbons` and the `TextPairs` contrast checks).

## What to build (each committed)
1. **Line kinds and the model:** `BookLineKind` on `BookLine` (`Section`, `Entry`, `Meta`, `Gap`, `Tiles`, `Detail`, as the spec), set by `BookPages` for every page; `ListFor` drops `Detail`, `BlockOf` returns an entry's `Detail` lines; the speakers' and practise buttons' row arithmetic unchanged for the list. Tests for each page's kinds and sections.
2. **Inks and geometry:** `UiPalette` gains `InkMeta` (replacing `InkHint`'s uses), `BandMuted` and the outcome marks; every new pair in `TextPairs`; `BookGeometry`'s `TitleTop`, `RuleTop`, `ContentTop` as the spec; the layout tests at every scale and 800 x 600.
3. **The drawing:** `BookLayer` draws by kind: the 32 px page title in the ribbon's colour with its rule, section bands, entry rows, tiles, the selection's accent edge, the double strike where D-game-27 allows it; the right page per page as the spec (32 px item title, labels with rules, number strips, the Verbs legend as swatches).
4. **Per page,** in this order, each its own commit: Nouns, Verbs, Phrases, Lessons (with the right page's speaker hit tests), What I said (sections per session, outcome marks from `InputEntry` as the spec says).
5. **Tests** as the spec's list; no window. The right page's speaker hit tests are Core tests.

**Screenshots.** Each page after, at the default size and at 800 x 600 (`--journal` or the book's scripted steps; find them in `docs/game/scripted-runs.md` and `running.md`). Look at each PNG against the mockup and fix what differs before the report. Keep them under `/tmp/claude-1000/u07/shots/`.

**Scripted runs.** Pictures that show the book will move (expected); stdout lines and world digests must not. List every run whose pictures differ and confirm each is the book.

Out of scope: the conversation panel (U05, U06, U08), new book pages, the recipe book (W30), content changes.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 500 words), also written to `/tmp/claude-1000/u07/report.md`
- The branch and the final commit SHA (local, not pushed).
- Each item: what changed (file and member), its tests.
- Every place you departed from the spec, and why.
- The rows per page at 1200 x 720 and 800 x 600.
- The screenshots' paths and what each shows, and what still differs from the mockup.
- The scripted runs' differences, the test count before and after, anything left undone and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs'`, kill only your own).
