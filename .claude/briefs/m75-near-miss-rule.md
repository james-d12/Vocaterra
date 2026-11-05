You are implementing the core milestone **M75, the near-miss rule**, for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the core.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m75`, already on the branch `M75-near-miss-rule` (from `feature/village-decisions` at 56caaa99, which holds M74). cd there first. Commit after each coherent step that builds and passes its tests, with the prefix `M75: `, and push after every commit (`git push -u origin M75-near-miss-rule` the first time). Never push or merge `feature/village-decisions` or `main`. When you finish, leave the worktree on your branch, clean.

## Resources (the machine has crashed from overload)
Run every dotnet restore, build, test, format and run, every scripts/content.sh call, speech prerender and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly (content-loop refuses limited.sh). Never start two heavy commands at once (no `&`, no background builds), and write no shell wait loop on `pgrep -f`. While iterating run `scripts/test.sh fast` or `scripts/test.sh project <test project>`; run `scripts/test.sh full` once before reporting. Exit 137 means the 6 GB cap killed it: lower the parallelism. Disk is about 17 GB free: build only what you need while iterating.

## Environment
Prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`: for `scripts/content.sh hash` or a CLI `eval` run, write a wrapper script under /tmp/claude-1000/ and run it). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

## Read first
CLAUDE.md; docs/nlp/implementation-plan.md §3, §4 and your §5 row (M75); `docs/handovers/done/2026-11-02-after-followups.md` section 5 (the spec; the owner chose the rule on 2026-11-02) and section 7; the §5b entries of M67, M72 and M74 (`grep -n '^- \*\*M67\|^- \*\*M72\|^- \*\*M74' docs/nlp/implementation-plan.md`); docs/nlp/changing-the-core.md; docs/nlp/api.md; `src/Vocaterra.Nlp/Evaluation/VerdictAssembler.cs` (`DeriveVocabWrong`, `ExplainsAMiss`, `IsNearMiss`) and `EvaluationConfig.cs`; the fuzz README's rows for O3 and O10 (`tests/Vocaterra.Nlp.Fuzzing.Tests/README.md`) and their tests (`VerdictAssemblyFuzzTests` and O3's verdict leg); the grading tests M76 made and the identity baseline. Read the sections named, never whole long files.

## The milestone (decided by the owner, 2026-11-02: "a near miss must name its word")
1. At a constrained prompt with no answer matched, keep the derived `vocab.wrong` only when the line and the nearest answer phrase have **the same number of words and differ in exactly one**, and the two differing words are **a slip pair**: close in spelling (within that word's own fuzzy budget), or both of one closed class (question words, articles, prepositions, pronouns). The rest of M67's conditions (no intent, or an intent from a keyword rule or the embedding rung) stay.
2. Report the answer's word as `Expected`, so the expected word is never empty.
3. The PR #28 review's item 8: a constrained prompt with **no answers** gives the matcher distance 0, so every unresolved line looks like a near miss (`IsNearMiss`). The new rule needs an answer word to name, so it holds no such case: test it.
4. The effect, each a regression test: `cuál es esto` at `lesson-what` keeps its tag, now naming `qué`; `quiero hacer un hacha` at `smith-open` (an added word), `pasado mañana iré al río con Mar` and `rápido por favor` (content words far apart) lose it, so the praise and the score agree.
5. Fuzz: O10's model and generator follow the new rule; O3's verdict leg becomes strict again (M72 had to admit these tags, about 1 in 2,000): a widening, never a narrowing. Update the fuzz README's rows.
6. The grading tests (M76) and the identity baseline move: re-record, and list every changed case by kind in the report (lost tag, gained `Expected`, other).
7. docs/nlp: the verdict rules' documentation; api.md if a public type changes.

Keep M67b (an answer's verb forms) parked. The alternatives the owner did not choose (handover section 5: keyword lines out, a note at weight 0, removing the case) are not to be built.

## Rules
A core milestone edits only the Vocaterra.Nlp projects, their tests, tests/golden and docs/nlp/ (content/game-v1 only if a core test needs it; it should not). Fix at the source with a regression test in the owning module; never weaken a fuzz oracle. A regression check is a C# test. Nothing under .github/workflows/; do not edit either implementation-plan.md or either decisions.md. No System.Random and no clock in the evaluation path. If a game test moves (the game shows `Expected` or a score), report it; fix a game test only where it pinned the old empty expected word, in one commit named for it. Before reporting, run the CLI on a few natural, correct lines at the NPCs' prompts (names, greetings, usted/ustedes, `hola luz quiero pan`, `buenos días domingo`, `¿cómo estás?`, `hace frío`) and a few true slips (`cuál es esto`, `quiero dos pam`); none of the correct lines may carry `vocab.wrong`. The known random fuzz family `2tpLGUOSqj41` (handover section 7) may fail once: check the seed with `CsCheck_Seed` and rerun once.

## Gate (from the worktree root)
scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full (8957 passed before you, with models); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; bash scripts/license-scan.sh --offline PASSED; the core demo (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1`) still gives exactly `conj.person` and `noun.number`. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf

## Report, one page
Branch, commit SHA, worktree path; the rule as built (where, the slip-pair test, the closed classes); test counts; the moved grading cases by kind; a before/after table of the lines in item 4 and the spot-check lines (intent, correctness, errors, expected); bugs found and fixed (where, symptom, fix, test); known-not-fixed with reasoning; files touched outside the core; anything left undone and why.
