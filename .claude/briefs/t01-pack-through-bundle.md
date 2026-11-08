You are implementing the game milestone **T01, the pack through the bundle**, in the Vocaterra repository (C#, .NET 10). The game is
`Vocaterra.Game.Core` (engine-free logic), `Vocaterra.Game` (MonoGame), `Vocaterra.Host` and `tools/Vocaterra.SpeechRender`; the language
core (`src/Vocaterra.Nlp`, the Spanish pack `src/Vocaterra.Nlp.Es`, the `Vocaterra.Nlp.*` tests and tools) is not yours and must not
change. Do the work yourself; do not spawn subagents. Commit prefix `T01: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/t01`, already on the branch `T01-pack-through-bundle` (from
`feature/german` after M87 merged; no build yet). cd there first; never edit `/home/james/Dev/Vocaterra` or another worktree. Commit after
each step that builds and passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or
`main`. Run everything in the foreground and finish in one turn (no background commands, monitors or wait loops). **Other sessions build
on this machine and it is short of memory:** every heavy command goes through `scripts/limited.sh` (it waits for a slot; let it wait).

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/t01/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment**, **Rules** and **Gate**: they apply to you, with `/tmp/claude-1000/t01/` for your logs, @@COUNT@@ as the
count before you, and these changes. **What this milestone edits:** `src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`,
`tools/Vocaterra.SpeechRender` (only if it names a Spanish class), their tests and their `.csproj` files, and `docs/game/` (not the plans
or the decisions). Never `content/`, the core, `.claude/` or `.github/workflows/`. "Every save version from 1 still loads" stays true
(saves change in T04). **The scripted runs are this milestone's main guard:** run `scripts/scripted-runs.sh` before your first edit and
after your last, compare with `python3 -I scripts/compare-runs.py`; no transcript may change, and any screenshot difference is explained.

## Why
The owner decided on 2026-11-08 (D-game-27, `docs/game/decisions.md`) to make German a second target language; the game first becomes
language-neutral with Spanish behaviour unchanged. The specification is `docs/research/2026-11-04-german-in-the-game.md` section 4.1, rows
G1, G2, G3 and G7; the row is T01 in `docs/game/implementation-plan.md` section 4.10. The core milestone M87 (merged) added what the game
needs; its mapping table is below.

## M87's mapping (game call site → neutral call)
- `bundle.SpanishMorphology()` (`BookPages.cs:44,83`, `NounCatalog.cs:248`, `GuideSession.cs:208`, `src/Vocaterra.Game/UI/Book/BookLayer.cs:468`)
  → `bundle.LanguageMorphology` (`IMorphology`). The game calls only `Conjugate`, `Article` (four arguments) and `Pluralize`, all on
  `IMorphology`. Retype the `MorphologyGenerator` parameters in `BookPages.Verbs`, `ConjugationTable` (around lines 56, 109, 114, 125),
  `VerbReading.cs:37` and `LessonBook.cs:232` to `IMorphology`.
- `ConjugationTable.Persons` (five hard-coded rows) → `morphology.Paradigm.Persons`; the columns `pres`, `pret`, `fut` →
  `Paradigm.Tenses` (`ParadigmOf(lemma)` gives ready cells). The near-future and obligation columns are periphrases built from game words:
  they stay game logic (the joiner words come from data where they are not already).
- `SpanishNumbers.ToWords` (`GuideFacts.cs:49`) → `bundle.Language.Numbers.ToWords`; `SpanishNumbers.TryParsePrefix`
  (`SessionSkip.cs:163,197`) → `bundle.Language.Numbers.TryParsePrefix`.
- `SpanishTextNormalizer.Normalize` (`WordProgress.cs:222,481`) → `bundle.Language.Normalizer.Normalize`.
- `SpanishAccentNotes.Describe` (`NpcService.cs:821`, `Host/VerdictReport.cs:85`) → `bundle.Language.Orthography.Describe`.
- `ErrorTags.All` (`GameDataValidator.cs:95`, `LocaleText.cs:493`) → the bundle's `Language.ErrorTags` (`ErrorTagSet`).
Line numbers may have drifted; grep.

## What to build (each committed)
1. **`GameLanguages`**, a small game-owned registry in `Vocaterra.Game.Core` (the one place that references `Vocaterra.Nlp.Es`): it builds
   the `LanguagePacks` the game supports (Spanish now) and the `ContentLoadOptions` for loading. `GameBundle.cs` (around line 39) and
   `Host/Program.cs` (around line 56) load through it instead of `SpanishContent.Load`; the bundle's manifest picks the pack (a bundle with
   no `language` is Spanish, ADR 0011). Remove the direct `Vocaterra.Nlp.Es` project reference from `Vocaterra.Host.csproj` if the host
   can go through `Game.Core`; keep `Game.Core`'s.
2. **The calls in M87's mapping**, each through the bundle (pass the bundle, its `Language` or its `IMorphology` down where a class has
   none; do not add statics).
3. **An architecture test** (beside `ArchitectureRulesTests`): no source file in `Vocaterra.Game.Core`, `Vocaterra.Game` or
   `Vocaterra.Host` outside `GameLanguages` names a type from the `Vocaterra.Nlp.Es` namespace or a `Spanish*` type of the core. Spanish
   words in comments, data and test fixtures are not the target; the remaining Spanish *literals* in code are T02's (do not move them
   now).
4. Tests that used `SpanishNumbers` and the like directly (`WorldFactsTests`, `GuideTurnTests`) may keep the Spanish classes, since tests
   are outside the rule; prefer the bundle's members where it is cheap.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; what changed per item; the architecture test and what it allows; the scripted runs' comparison; test
counts before and after (`scripts/test.sh full`); the gate's results; core gaps found (what call, why; reported, not patched); what the
orchestrator should record.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
