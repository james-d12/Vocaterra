You are implementing the core milestone **M37, the German text layer**, in the Vocaterra repository (C#, .NET 10). The language core is
`src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and tools; the game
(`Vocaterra.Game*`, `Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) is not yours and must not change. This milestone **starts the German
pack**, a new project `src/Vocaterra.Nlp.De`. Do the work yourself; do not spawn subagents. Commit prefix `M37: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m37`, already on the branch `M37-german-text` (from
`feature/german` after M88 and T03 merged; no build yet). cd there first; never edit another worktree. Commit after each step that builds and
passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the
foreground and finish in one turn (no background commands, monitors or wait loops). Never use `git stash`. **Other sessions build on this
machine:** every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it wait. Check `df -h /home` before a build.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m37/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you, with `/tmp/claude-1000/m37/` for your logs, **10580 passed, 3 skipped** as the count before
you, and these changes: skip the scripted runs (no game file changes); add `bash scripts/license-scan.sh --offline` (PASSED) and the core
demo (`docs/process/milestone-briefs.md`, "Repository facts": exactly `conj.person` and `noun.number`, through a wrapper script outside
the repository because the sandbox refuses the word `eval`). Build the core first (`Vocaterra.Nlp.slnf`; add the new projects to it and
to `Vocaterra.sln`); the whole solution for the final gate. Rules: create `src/Vocaterra.Nlp.De` and `tests/Vocaterra.Nlp.De.Tests`;
edit `src/Vocaterra.Nlp` only where a German member cannot be added without it (say why, additive, Spanish byte-identical), the fuzzing
project (German generators and oracles), `docs/nlp/` (not the implementation plan, not `decisions.md`; `docs/nlp/api.md` describes the
German pack's public surface) and the solution files. **No Spanish file changes** (`src/Vocaterra.Nlp.Es` and its tests untouched).
Never edit `content/`, the game projects, `.claude/` or `.github/workflows/`. Read `docs/nlp/changing-the-core.md`, ADR 0011 and 0012
(`docs/nlp/adr/`) and `docs/research/2026-10-26-engine-and-language-packs.md` first. Never weaken a fuzz oracle; goldens are not
re-recorded; a regression check is a C# test; no `System.Random`, no clock. The licence scan's allowlist refuses copyleft code.

## Why
The owner decided on 2026-11-08 (D-game-27) to add German; D14 amended authorises M36 to M43. M36 (merged) gave the German lexicon
`content/game-de-v1/lexicon/wiktionary-de.json` (its forms are lower-cased); M87 and M88 (merged) gave the pack interfaces a language with
cases needs (`ILanguagePack.GrammaticalCases`, `SupportsV2Order`, `GrammarDefaults.PolitePositions`, `IRecastPolicy.RestoredCapitals`,
`ErrorTags.OrthCapital` outside `ErrorTags.All`, `decl()`/`particle()`; read M88's entry in the plan's section 5b, grep `M88, the`). The
row: `docs/research/2026-10-22-multilingual.md` line 427 (and that report's section 2 on German); the plan's row `grep -n '^| M37 '
docs/nlp/implementation-plan.md`; `docs/research/2026-11-04-german-in-the-game.md` sections 3.2 (gap 4) and 3.3 (the M79 note). Variety
(D-game-27 item 6, `docs/game/decisions.md`): de-DE, with Austrian and Swiss spellings accepted (Swiss `ss` for `ß` is not an error).

## What to do (each committed)
1. **The pack skeleton.** `GermanLanguagePack : ILanguagePack` (`LanguageId` `de`), registered by a `GermanContent` helper as Spanish
   has `SpanishContent` (read both Spanish files and copy their shape, not their Spanish). Members this milestone does not fill (morphology
   M38, analysis rules M39, template functions M40, G2P) get the engine's neutral defaults or a clear `NotSupported` placeholder, named in
   the report. `GrammaticalCases` = `nom acc dat gen`; `SupportsV2Order` stays false until M40; an `ErrorTagSet` for German that includes
   `orth.capital`. The `LayeringTests` and architecture rules hold.
2. **`GermanNormalizer`**: lower-casing that keeps `Token.Original`, contractions with their case (`am` = an dem, `im` = in dem, `zum` = zu
   dem, `zur` = zu der, `beim`, `vom`, `ins`, `ans`, `aufs`), the final mark (`?`, `!`), German quotes `„…“` and `»…«`.
3. **`GermanFolding`**: `ä ö ü` ↔ `ae oe ue`, `ß` ↔ `ss`, so typed `Strasse`, `Grüsse`, `schoen` reach the known word; a folded form that is
   itself a known word (`schon` vs `schön`) keeps its own reading, decided with the German lexicon. This runs before any typo reading (the
   M79 note).
4. **`GermanNumbers`**: 0 to 999,999 to words and back (`einundzwanzig`, `hundertvierzig`, `zweitausenddreihundert`), `TryParsePrefix`
   as Spanish's does, and `Agree` with case for `ein` (`ein`, `eine`, `einen`, `einem`, `einer`; M88's optional case).
5. **`GermanPhoneticKey`** (the same contract as Spanish's: what it is for, read its tests) and **`GermanRecastPolicy`**:
   `RestoredCapitals` from the resolved reading (nouns from the lexicon's POS, formal `Sie`, sentence start), the typed-input-only
   `orth.capital` tag, accents (umlauts) restored as Spanish restores accents.
6. **Tests and fuzz:** unit tests per class in `tests/Vocaterra.Nlp.De.Tests` (a table of at least 40 normalizer cases, 30 folding
   cases, 60 numbers, 20 capital restorations); O1 (normalizer idempotent) and O6 (answer matching metamorphic) run for German through the
   fuzzing project's `FuzzLanguage` route (M35's `LanguageOracles`), with a small German text generator. Spanish: the goldens, `SpanishIdentityTests`, the fuzz suite and the demo unchanged.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; the pack's members (filled, defaulted, placeholder); each class with its test count and any case it
cannot handle; the fuzz oracles added; engine changes (if any, and why); test counts before and after (`scripts/test.sh full`); the
gate's results; what M38 to M40 must know; what the orchestrator should record in §5 and §5b.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
