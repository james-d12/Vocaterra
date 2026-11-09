You are implementing the core milestone **M39, the German A1 analyzer**, in the Vocaterra repository (C#, .NET 10). The language core
is `src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es`, `src/Vocaterra.Nlp.De` (the German pack, M37 and M38) and the
`Vocaterra.Nlp.*` tests and tools; the game is not yours and must not change. Do the work yourself; do not spawn subagents. Commit prefix
`M39: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m39`, already on the branch `M39-german-analyzer` (from
`feature/german` after M38 and M95 merged; no build yet). cd there first; never edit another worktree (M40, the German grammar options, may run
beside you in `m40` and also touches `src/Vocaterra.Nlp.De`: keep your edits to `GermanLanguagePack.cs` small and say which lines).
Commit after each step that builds and passes its tests; never end your turn with uncommitted work (the owner sometimes has to stop
agents; committed work survives). Do not push; never push or merge `feature/*` or `main`. Run everything in the foreground and finish in
one turn (no background commands, monitors or wait loops). Never use `git stash`. **Other sessions build on this machine:** every heavy
command goes through `scripts/limited.sh`; if it waits for a slot, let it wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m39/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you, with `/tmp/claude-1000/m39/` for your logs, **11219 passed, 3 skipped** as the count before
you, and these changes: skip the scripted runs; add `bash scripts/license-scan.sh --offline` (PASSED) and the core demo
(`docs/process/milestone-briefs.md`, "Repository facts", through a wrapper script outside the repository). Rules: edit
`src/Vocaterra.Nlp.De`, its tests, `src/Vocaterra.Nlp` only for an additive hook spike R5 shows the pack needs (with a default, Spanish
byte-identical; say why), the engine's tests, the fuzzing project's German parts and `docs/nlp/` (not the implementation plan, not
`decisions.md`; `docs/nlp/api.md` for new public members). **No Spanish file changes.** Never edit `content/`, the game, `.claude/` or
`.github/workflows/`. Read `docs/nlp/changing-the-core.md`, ADR 0012 and the analysis stage's code (`src/Vocaterra.Nlp/Analysis/`, the
`ReadingResolver`, `IAnalysisRules`, `AnalysisSlotFinder`) first. Never weaken a fuzz oracle; no `System.Random`, no clock. **D-game-9:
never shame correct speech.** A false tag on a correct sentence is the worst outcome; when unsure, do not tag.

## Why
D-game-27 and D14 amended authorise M36 to M43. M37 and M38 gave the German text layer and morphology; M88 the interface hooks. Read the
plan's section 5b entries for M88, M37 and M38 (grep `M88, the`, `M37, `, `M38, `): their notes for M39 (set `Register=formal` on `Sie`;
raise `orth.capital` from `GermanRecastPolicy.MissingCapitals` on typed input only; `AnalysisSlotFinder.IsPolitePhraseEnd` knows only the
trailing phrase; regularised forms come with a `GermanRegularizedForm.Kind`, a separable verb's regularised form reports the simple verb;
`fahrt`/`lest` are also legitimate 2pl forms, so compare with the subject; use `GermanMorphology.Auxiliary`, `Participle`, `Split` for the
bracket). The row: `docs/research/2026-10-22-multilingual.md` line 429 and that report's section 2 on German (its section 2.2 lists the
tags); `docs/research/2026-11-04-german-in-the-game.md` section 3.2 gap 6 and spikes R5 and R6 (section 7); `grep -n '^| M39 '
docs/nlp/implementation-plan.md`.

## What to do (each committed)
1. **Spike R5 first** (a short section in the report, and in `docs/nlp/` beside the analysis docs): prototype the chunker on 100 German
   A1 sentences outside the pipeline; decide whether the shared `ReadingResolver`'s steps fit German, or the pack needs a pre-resolution
   hook. If a hook, add it to the engine additively (default: no hook), with a test that Spanish is unchanged.
2. **`GermanAnalysisRules`** returned by `CreateAnalysisRules`, and the **chunker**: noun phrases with case, gender and number
   intersection (article, adjective, noun); a preposition-case table (A1: `in an auf mit zu von bei nach aus für ohne um durch gegen`,
   two-way prepositions by motion vs place); a valency table for about 60 A1 verbs (accusative or dative objects); subject-verb agreement
   (with formal `Sie`); V2 and verb-bracket checks; separable particle and modal-infinitive checks; resolver rules for article and pronoun
   homographs (`die`, `der`, `sie`/`Sie`) and lower-case noun/verb pairs (`essen`/`Essen`). Use the error tags of the multilingual
   report's section 2.2 that exist in the engine's taxonomy; propose (do not add to `ErrorTags.All`) any missing tag and use the German
   `ErrorTagSet` for it, as M88 did for `orth.capital`.
3. **The false-positive gate** (a C# test, the merge gate): a hand-written corpus of **500 correct A1 sentences** (the village's
   register: greetings, names, buying bread, directions, numbers and prices, family, food, the time, wishes with `möchte`, `du` and
   `Sie`) gives **0 false tags in typed form** and **at most 2% in lower-case "speech" form**; **200 learner-error sentences** (wrong
   article case, gender, agreement, verb position, missing particle, wrong auxiliary, regularised strong forms) give the expected tag in
   at least 90%. Check the 500 correct sentences with LanguageTool `de-DE` if the repository's grammar tooling can (see
   `scripts/content.sh grammar`; report if it cannot) and with your own review; they must be correct standard German. Keep the corpus as
   test data in the German test project, so T07 can add the German bundle's own model answers to it later (R6).
4. **An M26b-style report** of the gate's results (grep `M26b` in `docs/nlp/` for the form) and the fuzz: run any German oracle the
   fuzzing project can run without a bundle.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; R5's answer; what the chunker and rules cover and miss; the gate's numbers (typed, lower-case, error
recall per tag); engine changes (and why); tags proposed; test counts before and after (`scripts/test.sh full`); the gate's results; what
M41 and T07 must know; what the orchestrator should record in §5 and §5b.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
