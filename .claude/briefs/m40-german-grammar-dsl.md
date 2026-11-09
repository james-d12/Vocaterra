You are implementing the core milestone **M40, German grammar options and template filters**, in the Vocaterra repository (C#, .NET 10).
The language core is `src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es`, `src/Vocaterra.Nlp.De` (the German pack, M37 and M38) and
the `Vocaterra.Nlp.*` tests and tools; the game is not yours and must not change. Do the work yourself; do not spawn subagents. Commit
prefix `M40: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m40`, already on the branch `M40-german-grammar` (from
`feature/german` after M38 merged; no build yet). cd there first; never edit another worktree (M39, the German analyzer, may run beside
you in `m39` and also touches `src/Vocaterra.Nlp.De`: keep your edits to `GermanLanguagePack.cs` small and say which lines). Commit after
each step that builds and passes its tests; never end your turn with uncommitted work (the owner sometimes has to stop agents; committed
work survives). Do not push; never push or merge `feature/*` or `main`. Run everything in the foreground and finish in one turn (no
background commands, monitors or wait loops). Never use `git stash`. **Other sessions build on this machine:** every heavy command goes
through `scripts/limited.sh`; if it waits for a slot, let it wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m40/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you, with `/tmp/claude-1000/m40/` for your logs, **11196 passed, 3 skipped** as the count before
you, and these changes: skip the scripted runs; add `bash scripts/license-scan.sh --offline` (PASSED) and the core demo
(`docs/process/milestone-briefs.md`, "Repository facts", through a wrapper script outside the repository: the sandbox refuses the word
`eval`). Rules: edit `src/Vocaterra.Nlp.De`, its tests, `src/Vocaterra.Nlp` where M88's stubs need their real body (the V2 expansion and
its call), the engine's tests, the fuzzing project's German parts and `docs/nlp/` (not the implementation plan, not `decisions.md`;
`docs/nlp/api.md` for new public members). **No Spanish file changes**; the Spanish grammar and template files must parse to the same
canonical patterns (prove it with a test). Never edit `content/`, the game, `.claude/` or `.github/workflows/`. Read
`docs/nlp/changing-the-core.md` first. Never weaken a fuzz oracle; no `System.Random`, no clock.

## Why
D-game-27 and D14 amended authorise M36 to M43. M88 added the hooks: `ILanguagePack.SupportsV2Order` (false), an intent's `"order": "v2"`
loaded as `IntentDefinition.Order`, `V2GrammarExpansion.Expand` an internal stub nothing calls (M88's report: the call probably belongs in
`IntentCatalog.Build`), `GrammarDefaults.PolitePositions`, the template filters `article(def,acc)`, `words(…,case)`, `decl(…)` and
`particle` behind support flags. M38 gave `GermanMorphology` (`DeclineAdjective`, `SeparableParticle`, `Split`, the case `Article`, nouns
returned capitalised, the indefinite plural `""`, `Conjugate` giving only the finite stem for a separable verb; its noun, article and
adjective methods are German-only public members). Read the plan's section 5b entries for M88, M37 and M38 (grep `M88, the`, `M37, `,
`M38, `). The row: `docs/research/2026-10-22-multilingual.md` line 430; `grep -n '^| M40 ' docs/nlp/implementation-plan.md`.

## What to do (each committed)
1. **The V2 expansion.** `V2GrammarExpansion.Expand` gets its real body: a grammar marked `"order": "v2"` is expanded at load into the
   alternatives German allows (the finite verb second: subject first, or an adverbial or object first with the subject after the verb;
   questions verb-first; the infinitive or participle and a separable particle at the end of the clause), so the matcher itself is
   unchanged. Call it where M88 said. `GermanLanguagePack.SupportsV2Order` becomes true. Tests: a set of German grammar lines and the
   sentences each expansion accepts and rejects; the number of alternatives per grammar stays bounded (state the bound).
2. **A separable-verb grammar element** (for example `[VERB:aufstehen]` matching `stehe … auf` and `aufstehen` after a modal), built on
   `GermanMorphology.Split`.
3. **German template functions.** `GermanLanguagePack.CreateTemplateFunctions` returns `GermanTemplateFunctions`: `article(def|indef,case)`
   through the case `Article`, `decl(gender,number,case[,def|indef])` through `DeclineAdjective`, `particle` through `SeparableParticle`,
   German `words` (number words with case for `ein`), no inverted marks; flip the support flags. If a filter needs a German-only member
   of `GermanMorphology`, use it inside the German pack (no engine cast); add an engine member only if the engine itself must call it.
4. **Tests and fuzz:** template tests for each filter on 30 or more German cases; the Spanish templates and grammars parse to identical
   canonical patterns; any German fuzz oracle the fuzzing project can now run without a bundle.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; the V2 expansion's rules and bound; the separable element; each template filter; engine changes (and
why); test counts before and after (`scripts/test.sh full`); the gate's results; what M39 and M41 must know; what the orchestrator should
record in §5 and §5b.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
