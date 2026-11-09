You are implementing the game milestone **U08, the conversation history kept in the save** (GitHub issue #5, step 2), of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game's dialogue model and its save**: since U06 each NPC keeps its own page of conversation lines in the panel, but the pages live only in memory and are gone after a restart.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/u08`, already on the branch `U08-history-save` (from `feature/ui-issues` at its tip, with U05 and U06 merged). cd there first; never edit `/home/james/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit after each item that builds and passes its tests**, with the prefix `U08: `. Never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there. Never push or merge `feature/ui-issues` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish in one turn.** Do not start background commands or monitors and end your turn to wait for them.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/u08/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with these differences:
- Your logs and session folders go under `/tmp/claude-1000/u08/`.
- The count before you is U06's gate, **10419 passed and 3 skipped**.
- **Another agent (U07, the book) builds and tests at the same time as you, and other sessions use this machine too (the owner, 2026-11-08): keep your CPU and memory use low.** `scripts/limited.sh` queues your heavy commands; a wait of minutes is normal. Never bypass it, never raise a cap or a slot count. On top of the W07 rules:
  - While iterating, build only the projects you touch (`scripts/limited.sh dotnet build tests/Vocaterra.Game.Core.Tests -m:2`) and test with `scripts/test.sh project <test project>` or a `--filter`; never `scripts/test.sh fast` after every small step. One `scripts/test.sh full` at the end, before the report.
  - **Game runs:** one game process at a time, never two. While iterating, run only a scripted run that saves and loads (find one in `docs/game/scripted-runs.md`) or a short run of your own. The whole set once as the baseline before your first edit and once before the report, each in the foreground, one at a time.
  - Close every game you launch before the next command; check with `pgrep -fa Vocaterra.Game` that none of yours is left.
- This milestone does **not** edit `content/game-v1` or `content/locale`.
- **Saves:** the game is unreleased and old saves need not load if a format change breaks them (the owner; no migration code). Prefer an **optional** field, as H11 did with `SaveSettings.UnderwaterBlur` ("save version stays 7"), so every existing save and fixture still loads with no version bump; bump the version only if an optional field cannot work, and then say why.
- Rules' edit list for you: `src/Vocaterra.Game.Core`, `src/Vocaterra.Game` (only if the save is wired there), their tests (`tests/Vocaterra.Game*.Tests`), the save fixtures under `tests/Vocaterra.Game.Core.Tests/Fixtures/` only if their digests must be re-recorded, `docs/game/saves.md`, `docs/game/running.md` and `docs/game/architecture.md`.

## The specification
The plan's section 4.10 (`docs/game/implementation-plan.md`; `grep -n "### 4.10"`, then the #5 findings and the **U08** paragraph). The owner's issue, verbatim: "In the user interface, when we are interacting with NPCs, we should be allowed to scroll through the chat window to view the full history. Pre-requisites: We may need to investigate potential storage solutions for user data. Perhaps a simple file process database like sqlite could help." The plan answers the storage question: **no SQLite**; the save is one JSON document (`SaveDocument`) that already keeps every learner line (`LearnerRecordSave.History`), and only the NPC's side is missing. `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`).

What exists: `DialogueModel.Pages` (U06: `IReadOnlyDictionary<string, List<DialogueLine>>`, one page per NPC id, `MaxLines` 200 a page; `DialogueLine` is a speaker kind and a text); `SwitchTo` selects a page; `Open`'s opening rule (`opening.HostTurn || first || _lines.Count == 0`, with `first` from the `_opened` set, which is not saved). The save: `src/Vocaterra.Game.Core/Save/` (`SaveDocument`, `SaveGame`, `SaveService`, `SaveFile`), `docs/game/saves.md`. Find where a save is written and read and how the dialogue model can be reached there (it is not today: `grep -n Dialogue src/Vocaterra.Game.Core/Save/SaveGame.cs` finds nothing).

## What to build (each committed)
1. **The pages in the save:** an optional field in `SaveDocument` (for example `DialoguePages`: NPC id to its lines, each line its speaker kind as a stable string and its text), written from `DialogueModel.Pages` on every save and read back into the model on load, so the panel scrolls back over earlier sessions. Capped at `MaxLines` a page as the model is. A save without the field loads with empty pages. The speaker kind's names are a stable format: a test pins them.
2. **The opening after a reload:** decide what the first talk with an NPC after a load does (with `_opened` not saved, `first` is true again and the NPC's opening is appended to its restored page, as a new session's greeting). Keep that unless it duplicates a line visibly in an ordinary run; say what you chose and why.
3. **Docs:** `docs/game/saves.md` (the field, its cap, that it is optional), and a line in `running.md` where the panel's history is described.
4. **Tests:** a round trip of several NPCs' pages through `SaveDocument` (JSON) and back into a `DialogueModel`; the cap; a save without the field; the panel's page after a reload shows the earlier lines (`Expanded` and `PanelRows`); the stable speaker names; the existing save fixtures still load.

**Check in the game.** A short scripted run (or two) that talks to an NPC, saves, quits, loads and expands the panel (`--expand`, `--scroll N` from U06): a screenshot showing the earlier session's lines after the load. Keep it under `/tmp/claude-1000/u08/`. Say which save file size the run produced before and after.

**Scripted runs.** Stdout lines and world digests must not move, unless a run loads a save and now shows more lines in a picture: then explain each difference.

Out of scope: the book's "What I said" (it already reads the learner's history), the panel's look (U05, U06), the book (U07), SQLite or any other store.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words), also written to `/tmp/claude-1000/u08/report.md`
- The branch and the final commit SHA (local, not pushed).
- Each item: what changed (file and member), its tests.
- Whether the save version changed, and why or why not; the fixtures re-recorded, if any.
- What the first talk after a reload does.
- The screenshot's path and what it shows; the save's size before and after.
- The scripted runs' differences, the test count before and after, anything left undone and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs'`, kill only your own).
