You are implementing the game milestone **T02, the last Spanish words out of code**, in the Vocaterra repository (C#, .NET 10). The game
is `Vocaterra.Game.Core` (engine-free logic), `Vocaterra.Game` (MonoGame), `Vocaterra.Host` and `tools/Vocaterra.SpeechRender`; the
language core (`src/Vocaterra.Nlp*`) is not yours and must not change. Do the work yourself; do not spawn subagents. Commit prefix `T02: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/t02`, already on the branch `T02-words-out-of-code` (from
`feature/german` after T01 merged; no build yet). cd there first; never edit `/home/james/Dev/Vocaterra` or another worktree. Commit after
each step that builds and passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or
`main`. Run everything in the foreground and finish in one turn (no background commands, monitors or wait loops). Never use `git stash`
(the stash is shared with other sessions); set work aside with a WIP commit. **Other sessions build on this machine and it is short of
memory:** every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/t02/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment**, **Rules** and **Gate**: they apply to you, with `/tmp/claude-1000/t02/` for your logs, **10413 passed, 3
skipped** as the count before you (the open-fallback fuzz reach check is flaky, M89: if it alone fails, rerun it alone and say so), and
these changes. **What this milestone edits:** the game projects, their tests, `content/game-v1/game/words.json` and its schema (wherever
`words.json`'s JSON Schema lives; grep `schemaVersion` and `words.schema`), `content/locale/en/` only for a hint that names the typing
keys, and `docs/game/` (not the plans or the decisions). Never the core, `.claude/` or `.github/workflows/`. Editing `words.json` means the
content loop (the w07 brief's last Rules bullet and `content/README.md`, "The edit loop"): `scripts/content-loop.sh --grammar` after each
content edit (run directly, never under limited.sh), `--tier full` once before the report instead of `scripts/test.sh full`; read every
grammar finding it prints. **The scripted runs** (`scripts/scripted-runs.sh` before your first edit and after your last, compared with
`python3 -I scripts/compare-runs.py`): no transcript may change; explain any screenshot difference.

## Why
The owner decided on 2026-11-08 (D-game-27) to make German a second target language; the game becomes language-neutral first, with Spanish
behaviour unchanged. T01 (merged) moved every Spanish *class* call behind the bundle and added `LanguageNeutralityTests`. T02 moves the
Spanish *words* still written in C# into data. Specification: `docs/research/2026-11-04-german-in-the-game.md` section 4.1, rows G4 and
G5; the row T02 in `docs/game/implementation-plan.md` section 4.10; C05's design for `words.json` in `docs/game/content-plan.md` (grep
`game/words.json`, around line 376).

## What to build (each committed)
1. **Find them.** Spanish words in C# string literals in `src/Vocaterra.Game.Core`, `src/Vocaterra.Game` and `src/Vocaterra.Host` (not
   comments, not tests). Known (line numbers drift; grep): `Npc/Vocative.cs` (about lines 32, 37: article and preposition lists),
   `Npc/RecastNames.cs` (about 17, with `¿¡`), `NpcTurnRules.cs` (about 277: `tú`/`usted`), `GuideSession.cs` (about 79, 82, 265, 747),
   `VoxelSession.cs` (río, agua, fuego, antorcha), `BookVerbLabels.cs` (about 86), `src/Vocaterra.Game/UI/Checks/CheckLayer.cs` (about 44,
   "no sé"), `PhraseIds.Hello = "hola"` (`NpcHostEvents.cs`, about 349), and `ConjugationTable`'s periphrasis ids `ir-a` and `tener-que`
   if they surface as words. List what you find, with your decision for each, in the report.
2. **Move each** to the right home: words the game says or matches go into `game/words.json` (new keys, schema updated, loaded through
   the existing `GameData` path); articles, prepositions and pronouns that the core's pack already knows come from the pack instead where
   it offers them (`bundle.Language.Lint` vocabulary, `bundle.LanguageMorphology.Article`, `GrammarDefaults.SubjectPronouns`); ids that
   are only keys (a phrase id, a lemma used as an id) may stay as ids, but say which and why: T03 neutralises world ids. Item and noun
   lemmas in `VoxelSession` that are really world ids (río, agua, fuego, antorcha) belong to T03; if one is a *word* the game matches, move
   it; if it is an id, leave it and list it for T03.
3. **The typing map (G5).** `src/Vocaterra.Game/Input/AccentShortcuts.cs` (Alt + vowel → accented vowel, Alt+n → ñ, `¿ ¡`) becomes
   per-language data in `words.json` (for example `"typing": [{"key": "a", "alt": "á"}, …]`), read by `AccentShortcuts`; the English hint
   that lists the letters (find it: a locale key reached from `ReportLayer.HintsFirst` or the dialogue panel) is built from that map, so a
   German bundle's map (Alt+a/o/u → ä/ö/ü, Alt+s → ß) would show its own letters with no code change. Add a unit test with a non-Spanish
   map.
4. **A guard:** extend T01's `LanguageNeutralityTests` (or add one beside it) so a new Spanish literal in game code fails: a list of the
   Spanish words now in data (taken from `words.json` at test time) must not appear as C# string literals in the three projects. Keep it
   precise enough not to trip over ids you justified keeping (an allow-list with a reason per entry).

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; the table of literals found and where each went; the typing map and hint; the guard; the scripted runs'
comparison; the content loop's results (grammar findings, bundle hash); test counts (`--tier full`); the gate's results; ids left for T03;
core gaps (reported, not patched).

Commit messages end with
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
```
