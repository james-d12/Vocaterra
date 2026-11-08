You are implementing the core milestone **M87, the language-neutral surface the game reads from a pack**, in the Vocaterra repository
(C#, .NET 10). The language core is `src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*`
tests and tools; the game (`Vocaterra.Game*`, `Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) is not yours and must not change. Do the
work yourself; do not spawn subagents. Commit prefix `M87: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m87`, already on the branch `M87-pack-surface` (from
`feature/german` at `9b62f7dc`; it has no build yet). cd there first; never edit `/home/james/Dev/Vocaterra` or another worktree (`german`,
`bugfixes`, `water` are not yours; another session may build beside you). Commit after each step that builds and passes its tests; never
end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the foreground and finish in
one turn (no background commands, monitors or wait loops).

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m87/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/m87/` for your logs, **10395 passed, 3
skipped** as the count before you (re-measure it if the first full run differs, and say so), and these changes: skip the scripted runs (no
game file changes); add `bash scripts/license-scan.sh --offline` (PASSED) and the core demo, `dotnet run --project tools/Vocaterra.Nlp.Cli
-- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1`, still giving exactly `conj.person` and
`noun.number` (through a wrapper script: the sandbox refuses the word `eval`). Check `df -h /home` before a build; stop and report under
6 GB. Build the core first (`Vocaterra.Nlp.slnf`); the whole solution for the final gate (the game must still build, unchanged).
Rules: edit only `src/Vocaterra.Nlp`, `src/Vocaterra.Nlp.Es`, `tools/Vocaterra.Nlp.Cli`, `tools/Vocaterra.Nlp.ContentBuilder` (only if
(d) needs it), their tests and `docs/nlp/` (not the implementation plan, not `decisions.md`; `docs/nlp/api.md` must describe every new
public member). Never edit `content/`, the game projects or anything under `.claude/` or `.github/workflows/`. Read
`docs/nlp/changing-the-core.md` and ADR 0011 and 0012 (`docs/nlp/adr/`) before changing the engine. Never weaken a fuzz oracle; a
regression check is a C# test. No `System.Random`, no clock.

## Why
The owner decided on 2026-11-08 (D-game-27, `docs/game/decisions.md`) to make German a second target language of the game. The first game
milestone, T01 (`docs/game/implementation-plan.md` section 4.10), removes every direct use of the Spanish pack from the game: it will load
packs through a registry and read everything from `ContentBundle.Language` (`ILanguagePack`) and `ContentBundle.LanguageMorphology`
(`IMorphology`). It can do that only if the engine offers, language-neutrally, everything the game now takes from Spanish classes. That is
this milestone. The specification is `docs/research/2026-11-04-german-in-the-game.md` sections 3.2 ("Smaller leftovers") and 4.1 (G2, G3,
G7); the row is `grep -n '^| M87 ' docs/nlp/implementation-plan.md`.

## What to do (each committed)
1. **Audit.** List every use of a Spanish-pack type or member from the game and the host: `grep -rn 'Spanish\|Vocaterra.Nlp.Es'
   src/Vocaterra.Game.Core src/Vocaterra.Game src/Vocaterra.Host tools/Vocaterra.SpeechRender --include=*.cs`. Known: `SpanishMorphology()`
   at `BookPages.cs:44,83`, `NounCatalog.cs:227`, `GuideSession.cs:205`, `src/Vocaterra.Game/UI/Book/BookLayer.cs:468`, and what
   `ConjugationTable.cs`, `VerbReading.cs` and `LessonBook.cs` take from `MorphologyGenerator`; `SpanishNumbers` (`GuideFacts.cs:49`,
   `SessionSkip.cs:152,186`); `SpanishTextNormalizer` (`WordProgress.cs:222,481`); `SpanishAccentNotes` (`NpcService.cs:772`,
   `Host/VerdictReport.cs:85`; also the CLI's `Program.cs:236`). For each, write down the member used and whether `ILanguagePack` /
   `IMorphology` already offers it. Leave `SpanishContent.Load`/`Options` alone: T01 replaces those with a game registry.
2. **(a) Orthography notes.** An `IOrthographyNotes` (name it to fit the code) on `ILanguagePack`, Spanish's built from today's
   `SpanishAccentNotes`, the toy `xx` pack's a trivial one. Move the CLI to it.
3. **(b) Morphology the Book needs.** Whatever members of `MorphologyGenerator` the game calls that `IMorphology` lacks (paradigm rows,
   the persons and tenses a conjugation table shows, lemma lookups, noun gender and plural), offered neutrally: on `IMorphology` with a
   default where possible, or as a small companion interface the pack exposes. A German pack will need a conjugation table with different
   persons (`ich du er/sie/es wir ihr sie/Sie`), stem changes and separable verbs, so describe persons and tenses as data the pack
   supplies, never as a Spanish enum the game hard-codes. Keep `bundle.SpanishMorphology()` working (the game still calls it until T01).
4. **(c)** `PredictedErrorMatcher` (`src/Vocaterra.Nlp/...PredictedErrorMatcher.cs`, around lines 109 to 117) ranks tags by the bundle's
   `ErrorTagSet`, not `ErrorTags.All`. **(d)** `Lexicon.ReadFile` (around `Lexicon.cs:135,149`) takes the pack's `LexiconProfile` instead
   of defaulting to V1 where a pack is known. Numbers and the normalizer are already pack members (`ILanguagePack.Numbers`,
   `.Normalizer`): confirm they cover the calls in step 1, and add only what is missing.
5. **Spanish byte-identical:** `SpanishIdentityTests`, the goldens (no re-record), the fuzz suite and the core demo unchanged. A unit test
   per new member on the Spanish pack (the same output as the Spanish class it replaces) and on the `xx` pack (it works with no Spanish
   registered).

## Report (one page, under 500 words)
Branch, final SHA, worktree clean. **The mapping table T01 will use:** each game call site from step 1 and the neutral call that replaces
it. The new public members (they are in `docs/nlp/api.md`). Test counts before and after (`scripts/test.sh full`); the gate's results;
anything the game needs that you could not offer neutrally, and why; what the orchestrator should record in the plan's §5 and §5b.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
