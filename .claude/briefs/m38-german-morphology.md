You are implementing the core milestone **M38, German morphology**, in the Vocaterra repository (C#, .NET 10). The language core is
`src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack), `src/Vocaterra.Nlp.De` (the German pack, started by M37) and
the `Vocaterra.Nlp.*` tests and tools; the game is not yours and must not change. Do the work yourself; do not spawn subagents. Commit
prefix `M38: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m38`, already on the branch `M38-german-morphology` (from
`feature/german` after M37 merged; no build yet). cd there first; never edit another worktree. Commit after each step that builds and
passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the
foreground and finish in one turn (no background commands, monitors or wait loops). Never use `git stash`. **Other sessions build on this
machine:** every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m38/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you, with `/tmp/claude-1000/m38/` for your logs, **11093 passed, 3 skipped** as the count before
you, and these changes: skip the scripted runs; add `bash scripts/license-scan.sh --offline` (PASSED) and the core demo
(`docs/process/milestone-briefs.md`, "Repository facts", through a wrapper script outside the repository because the sandbox refuses the
word `eval`). Rules: edit `src/Vocaterra.Nlp.De`, `tests/Vocaterra.Nlp.De.Tests`, the fuzzing project's German parts and `docs/nlp/` (not
the implementation plan, not `decisions.md`; `docs/nlp/api.md` for new public members); `src/Vocaterra.Nlp` only if a member the German
morphology needs is missing from `IMorphology` (additive, with a default, Spanish byte-identical; say why). **No Spanish file changes.**
Never edit `content/`, the game projects, `.claude/` or `.github/workflows/`. Read `docs/nlp/changing-the-core.md` first. Never weaken a
fuzz oracle; no `System.Random`, no clock.

## Why
D-game-27 and D14 amended authorise M36 to M43. M36 gave the German lexicon (`content/game-de-v1/lexicon/wiktionary-de.json`, lower-cased
forms, its feature values in `GermanLanguagePack.Profile`: genders `m f n`, cases `nom acc dat gen`, verb forms `inf part1 part2
part2-haben part2-sein`, declensions, 64 separable particles); M87 gave `IMorphology.Paradigm`/`ParadigmOf`; M88 gave the case on
`Article` and `Agree`, `decl()` and `particle()` behind support flags; M37 started the pack, whose `CreateMorphology` throws until now.
Read the plan's section 5b entries for M36, M87, M88 and M37 (grep `M36 with M90`, `M87, the pack`, `M88, the`, `M37, `). The row:
`docs/research/2026-10-22-multilingual.md` line 428 and that report's section 2 on German; `grep -n '^| M38 '
docs/nlp/implementation-plan.md`. Watch point carried from M87: `Conjugate` returns a string, so a separable verb's particle placement
needs `particle()` or a richer member.

## What to do (each committed)
1. **`GermanMorphology : IMorphology`** returned by `GermanLanguagePack.CreateMorphology`: articles by case (definite, indefinite, `kein`,
   the A1 possessives `mein dein sein ihr unser euer Ihr`), noun forms by number and case (lexicon first; n-Deklination `Junge → Jungen`;
   dative plural `-n`), `Pluralize`/`Singularize`/`InflectNoun` lexicon first, conjugation (present with stem changes `fährt`, `liest`,
   `nimmt`; imperative; the Perfekt participle with its auxiliary from the lexicon; Präteritum of `sein`, `haben` and the modals), the
   separable split (`aufstehen` → `stehe … auf`).
2. **The Paradigm** for German: persons `ich du er/sie/es wir ihr sie/Sie` (formal `Sie` sharing 3 pl, as Spanish shares `usted`), tenses
   `pres` and `perf` (and `prät` where the lexicon has it); `ParadigmOf(lemma)` gives the Book's cells.
3. **Template functions' morphology parts that M40 will expose:** `DeclineAdjective` (strong, weak, mixed by the article; lexicon first,
   rule fallback) and `SeparableParticle` implemented on the morphology side so M40 only wires them (do not flip the template support
   flags; M40 does).
4. **Regularised strong forms** that learners produce (`fahrt` for `fährt`, `gehte` for `ging`, `gegeht` for `gegangen`) are recognised as
   readings of the right lemma with a marker the analyzer (M39) can use; generation never produces them.
5. **Tests:** paradigm tests against 100 frequent lexicon lemmas (nouns: gender, plural, case forms; verbs: present, participle,
   auxiliary), every generated form round-trips through lexicon lookup, the regularised forms, the separable split. No Spanish test
   changes.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; the morphology's coverage and its gaps (with examples); engine changes (if any, and why); test counts
before and after (`scripts/test.sh full`); the gate's results; what M39 and M40 must know; what the orchestrator should record in §5 and
§5b.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
