You are implementing the core milestone **M96, «porque» read as a plural noun**, in the Vocaterra repository (C#, .NET 10). The language
core is `src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and tools; the game
(`Vocaterra.Game*`, `Vocaterra.Host`, `tools/Vocaterra.SpeechRender`, `tools/Vocaterra.WorldPreview`) is not yours and must not change. Do
the work yourself; do not spawn subagents. Commit prefix `M96: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m96`, already on the local branch `M96-porque-noun-reading`
(from `feature/biomes` at @@BASE@@). cd there first; never edit `/home/james/Dev/Vocaterra` or another worktree. Commit after each step that
builds and passes its tests. Do not push; never push or merge `feature/*` or `main`. Wait for each command to finish (no wait loops on
`pgrep -f`).

## Resources, environment, rules
- Every dotnet restore, build, test and format through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`), one heavy command at a time,
  no `&`. Up to three other agents build at the same time; expect waits. Build the core first (`Vocaterra.Nlp.slnf`); iterate with the
  fuzz project or a `--filter`; run `scripts/test.sh full` once at the end. Exit 137 means the 6 GB cap: lower the parallelism.
- Prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses the words `eval`
  and `hash` in a command line). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. You run no game.
- Read CLAUDE.md, `docs/nlp/changing-the-core.md` (before changing the engine), `docs/nlp/adr/0009-core-and-game-boundary.md` and, if a
  public type changes, update `docs/nlp/api.md`. Edit only `src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp*`, `tools/Vocaterra.Nlp*` and
  `docs/nlp/` (not `docs/nlp/decisions.md` or `docs/nlp/implementation-plan.md`: say in the report what they should say). Do not edit
  `content/game-v1` (the shared bundle): if the root cause is in the bundle, stop and report it.
- **The fuzz suite is a gate: never weaken or narrow an oracle to make it pass** (CLAUDE.md). The fix is at the source, with regression
  tests.

## The failure (two oracles, random CsCheck seeds; both reproduce with the seed)
1. Seed `dTc3W4dw5Cg2`, found 2026-11-12 by the game's Y11 agent: `MutationTagFuzzTests.OneMutation_YieldsExactlyTheNamedTag…` (oracle
   O4) on «túneles unos porque»: the analyzer reads «porque» as a plural noun. It failed the same way on a branch without Y11.
2. Seed `7PcP-w7xauk1`, 2026-11-15, the merge gate of the game's B08b on `feature/biomes` (B08b touched no core input):
   `RecastLoopFuzzTests.Recast_RestoresTheCorrectedSentence_AndReAnalyzesClean` (oracle O5): `MutationCase { Kind = VocabUnknown, Seed =
   703 }`, pattern `[[NOUN:item] [ART]? [PRON]? porque? [PRON]?]`, «túneles unos porque» mutated to «vebegoz unos porque»; the recast
   writes «vebegoz unos porqués» instead of the corrected «vebegoz unos porque».
Reproduce: `CsCheck_Seed=7PcP-w7xauk1 scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests -m:2 --filter
"FullyQualifiedName~Recast_RestoresTheCorrectedSentence_AndReAnalyzesClean"` and `CsCheck_Seed=dTc3W4dw5Cg2 ... --filter
"FullyQualifiedName~OneMutation_YieldsExactlyTheNamedTag"`.
Background: `docs/nlp/findings.md` (the entry "A fuzz seed on «porque»" and M65's (c): the grammar match decides a literal's accent
reading, `porque`/`porqué`); `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` (the oracles O4 and O5).

## What to do
1. Find why «porque» after a plural article («unos porque») gets the noun reading of «porqué» (plural «porqués»): the lexicon's
   accent-insensitive lookup, the tagger, the agreement rule that pluralises it, or the recast's realiser. Say which in the report.
2. Fix it at the source so that: «porque» (the conjunction) is never read as the noun after an article unless the learner's text supports
   the noun («el porqué», «los porqués», and an unaccented «el porque» if the core already treats it as the noun with an accent note:
   keep whatever the core does for those today, and test it); the recast never invents «porqués» from «porque».
3. Tests: one regression fact per seed (the failing inputs, in the owning module's test project), facts for the correct neighbours above
   (they must not change), and both fuzz tests passing with their seeds. Record both seeds in the fuzz README's seed list if it keeps one.
4. Spot-check the core demo line from CLAUDE.md and a few natural learner lines with «porque» («quiero pan porque tengo hambre», «¿por qué?
   porque sí», «no sé el porqué»): report the verdicts before and after; none may change except the bug's.
5. Documents: `docs/nlp/findings.md` (an entry for M96) if your brief's rules allow it there; otherwise give its text in the report.

## Gate (from the worktree root)
`scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings); `scripts/test.sh full` (@@COUNT@@ passed before you, 3
skipped) after your last change; `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes` clean; the end-to-end goldens
unchanged unless the bug's fix moves them (then say exactly which and why). Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

## Report (one page)
Branch, commit SHA, worktree path; the root cause; the fix; the tests added; the test counts; the spot-check verdicts before and after;
public API changes (if any); what the core plan's row and findings should say; anything left undone and why. No file dumps or long logs.
