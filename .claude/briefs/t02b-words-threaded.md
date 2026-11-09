You are implementing the game milestone **T02b, the last Spanish words out of code (threaded)**, in the Vocaterra repository (C#,
.NET 10). The game is `Vocaterra.Game.Core` (engine-free logic), `Vocaterra.Game` (MonoGame), `Vocaterra.Host` and
`tools/Vocaterra.SpeechRender`; the language core (`src/Vocaterra.Nlp*`) is not yours and must not change. Do the work yourself; do not
spawn subagents. Commit prefix `T02b: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/t02b`, already on the branch `T02b-words-threaded` (from
`feature/german` at `7bbe6c27`; no build yet). cd there first; never edit `/home/james/Dev/Vocaterra` or another worktree (`m36` runs
beside you, in the core's tools). Commit after each step that builds and passes its tests; never end your turn with uncommitted work. Do
not push; never push or merge `feature/*` or `main`. Run everything in the foreground and finish in one turn (no background commands,
monitors or wait loops). Never use `git stash` (shared with other sessions); set work aside with a WIP commit. **Other sessions build on
this machine:** every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/t02b/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment**, **Rules** and **Gate**: they apply to you, with `/tmp/claude-1000/t02b/` for your logs, **10418 passed, 3
skipped** as the count before you (the open-fallback fuzz reach check `PipelineFuzzTests.OpenFallback_LeavesATrustedFirstReadingAlone_…`
is flaky, M89: if it alone fails, rerun it alone and say so), and these changes. **What this milestone edits:** the game projects, their
tests, `content/game-v1/game/words.json` and its JSON Schema (T02 found where it lives; grep `typing` in the schemas), and `docs/game/`
(not the plans or the decisions). Never the core, `content/locale/`, `.claude/` or `.github/workflows/`. Editing `words.json` means the
content loop (the w07 brief's last Rules bullet and `content/README.md`, "The edit loop"): `scripts/content-loop.sh --grammar` after each
content edit (run directly, never under limited.sh), `--tier full` once before the report instead of `scripts/test.sh full`; read every
grammar finding it prints; the save fixtures take the new bundle hash. **The scripted runs** (`scripts/scripted-runs.sh` before your first
edit and after your last, compared with `python3 -I scripts/compare-runs.py`) are this milestone's main guard: no transcript may change;
explain any screenshot difference.

## Why
The owner decided on 2026-11-08 (D-game-27) to make German a second target language; the game becomes language-neutral first, with Spanish
behaviour unchanged. T02 (merged) moved the typing map into `words.json` and added `SpanishLiteralGuardTests`
(`tests/Vocaterra.Game.Core.Tests/Architecture/`), whose allow-list still carries entries marked **OPEN**: Spanish words the game matches
that are still C# literals. T02b removes every OPEN entry. Rows: `T02b` in `docs/game/implementation-plan.md` section 4.10 and T02's
entry in section 5 (grep `T02, the Spanish words`). The words file's design: `docs/game/content-plan.md` (grep `game/words.json`).

## What to build (each committed)
1. **Read the OPEN entries** in `SpanishLiteralGuardTests` (about 20: greeting openings in `Npc/Greeting.cs`; `tú`/`usted`/`ustedes` in
   `Npc/NpcTurnRules.cs`; the determiners before a name in `Npc/RecastNames.cs`; the binders before a trailing name in `Npc/Vocative.cs`;
   `un`/`una` in `Npc/NpcHostEvents.cs`'s quantity slot) and trace who calls each (also `LineReading`, `NpcService`, `GuideSession` and the
   host's `ConversationSession`; about 35 call sites).
2. **Move each word list into `game/words.json`** under clearly named keys (for example `greetings`, `addressPronouns` with the informal,
   formal and plural forms by role, `nameDeterminers`, `vocativeBinders`, `quantityArticles`), the schema updated, loaded through the
   existing `GameData` / `GameWords` path. Where a list is really grammar the pack already knows, use the pack instead
   (`bundle.Language.GrammarDefaults.SubjectPronouns`, `bundle.LanguageMorphology.Article`); T02 found the pack has no article,
   preposition or determiner lists, so those go to the file. Keep order and case-folding exactly as the code had them, so matching is
   unchanged.
3. **Thread `GameWords`** (or the narrow list a class needs) to each class through its constructor or method arguments, from where the
   bundle or `GameData` is already available. No statics, no service locator, no default that silently falls back to Spanish. Static helper
   classes become instance classes or take the list as a parameter, whichever changes fewer call sites.
4. **Empty the OPEN entries** from the allow-list; every remaining entry keeps a reason (phrase ids, JSON keys, locale codes). Add a unit
   test per moved list that proves a non-Spanish list works (for example a German greeting `hallo`, binders `von`/`mit`, determiners
   `der`/`die`/`das`), as T02 did for the typing map.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; each OPEN entry and where it went (key, loaded by, passed to); the call sites changed; the guard's
remaining allow-list; the scripted runs' comparison; the content loop's results (grammar findings, bundle hash); test counts (`--tier
full`); the gate's results; core gaps (reported, not patched); anything left for T03.

Commit messages end with
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
```
