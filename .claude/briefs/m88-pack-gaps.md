You are implementing the core milestone **M88, the pack-interface gaps German needs**, in the Vocaterra repository (C#, .NET 10). The
language core is `src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and tools;
the game (`Vocaterra.Game*`, `Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) is not yours and must not change. Do the work yourself; do
not spawn subagents. Commit prefix `M88: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m88`, already on the branch `M88-pack-gaps` (from
`feature/german`; no build yet). cd there first; never edit another worktree. Commit after each step that builds and passes its tests;
never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the foreground and
finish in one turn (no background commands, monitors or wait loops). Never use `git stash`. **Other sessions build on this machine:**
every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it wait. Check `df -h /home` before a build.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m88/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you, with `/tmp/claude-1000/m88/` for your logs, @@COUNT@@ as the count before
you, and these changes: skip the scripted runs (no game file changes); add `bash scripts/license-scan.sh --offline` (PASSED) and the core
demo (`docs/process/milestone-briefs.md`, "Repository facts": exactly `conj.person` and `noun.number`, through a wrapper script outside
the repository because the sandbox refuses the word `eval`). Build the core first (`Vocaterra.Nlp.slnf`); the whole solution for the final
gate (the game must still build, unchanged). Rules: edit only `src/Vocaterra.Nlp`, `src/Vocaterra.Nlp.Es`, `tools/Vocaterra.Nlp.Cli`,
their tests and `docs/nlp/` (not the implementation plan, not `decisions.md`; `docs/nlp/api.md` describes every new public member). Never
edit `content/`, the game projects, `.claude/` or `.github/workflows/`. Read `docs/nlp/changing-the-core.md` and ADR 0011 and 0012
(`docs/nlp/adr/`) first. Never weaken a fuzz oracle; goldens are not re-recorded; a regression check is a C# test; no `System.Random`, no
clock.

## Why
The owner decided on 2026-11-08 (D-game-27) to add German as a second target language; D14 amended authorises M36 to M43, with M87 and M88
first. M87 (merged) gave the game its language-neutral surface; M88 closes the interface gaps a German pack will hit, so M38 to M40 can
implement German without changing the engine's interfaces again. The specification is
`docs/research/2026-11-04-german-in-the-game.md` section 3.2, gaps 1 to 5 (gap 6 is spike R5 inside M39: not yours); the row is
`grep -n '^| M88 ' docs/nlp/implementation-plan.md`. M87's section 5b entry (grep `M87, the pack surface`) has watch points.

## What to do (each committed; every change additive, with a default that keeps Spanish byte-identical)
1. **Case on articles (gap 1).** `IMorphology.Article` and `ITemplateFunctions.Article` take an optional grammatical case (a string id
   such as `nom`, `acc`, `dat`, `gen`, null meaning the pack's default); the template syntax `article(def,acc)` parses, and a pack with no
   case rejects a case argument at template parse time with a clear error. Spanish ignores a null case and refuses a non-null one.
2. **Case on number agreement (gap 2).** `INumberWords.Agree` takes an optional case (German needs it for `ein`, `einen`, `einem`).
3. **Polite positions and V2 (gap 3).** `GrammarDefaults` gains `PolitePositions` (trailing, leading, after the finite verb; Spanish:
   trailing only, as today), honoured where `SlotGrammarMatcher` reads `TrailingPoliteWords`. A grammar file may carry `order: "v2"`: the
   loader accepts and records it and fails clearly if the pack does not declare V2 support; the expansion itself is M40's (a stub that
   throws `NotSupported` behind the flag is fine, but nothing in Spanish reaches it).
4. **Capitals in recasts (gap 4).** `IRecastPolicy` may restore capitals from the resolved reading (nouns, formal `Sie`, sentence start)
   using `Token.Original`; Spanish's policy keeps today's output exactly. Add the error tag `orth.capital` to the engine's taxonomy only
   if a pack can opt into it (Spanish's `ErrorTagSet` does not list it, so nothing changes for Spanish).
5. **`decl()` and `particle()` (gap 5).** Optional `ITemplateFunctions` members (adjective ending; separable particle), with a default
   "not supported by this language" error at template parse time.
6. **Tests:** a unit test per new member on the Spanish pack (output unchanged) and on the toy `xx` pack (it implements each new member
   trivially, so each path is exercised with no Spanish registered). `SpanishIdentityTests`, goldens, fuzz suite and the demo unchanged.
   Run the CLI on a few natural, correct Spanish lines at the NPCs' prompts (names, greetings, usted/ustedes) before reporting.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; each gap and the new members (signatures); the `xx` pack's coverage; test counts before and after
(`scripts/test.sh full`); the gate's results; anything you could not add neutrally, and why; what M38 to M40 must know; what the
orchestrator should record in §5 and §5b.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
