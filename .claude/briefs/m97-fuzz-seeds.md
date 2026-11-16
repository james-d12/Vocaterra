You are implementing the core milestone **M97, two fuzz seeds and a load-sensitive oracle**, in the Vocaterra repository (C#, .NET 10).
The language core is `src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and
tools; the game (`Vocaterra.Game*`, `Vocaterra.Host`, `tools/Vocaterra.SpeechRender`, `tools/Vocaterra.WorldPreview`) is not yours and
must not change. Do the work yourself; do not spawn subagents. Commit prefix `M97: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m97`, already on the local branch `M97-fuzz-seeds` (from
`feature/biomes` at @@BASE@@). cd there first; never edit `/home/james/Dev/Vocaterra` or another worktree. Commit after each step that builds
and passes its tests. Do not push; never push or merge `feature/*` or `main`. Wait for each command to finish (no wait loops on `pgrep -f`).

## Resources, environment, rules
- Every dotnet restore, build, test and format through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`), one heavy command at a time,
  no `&`. Three other agents build at the same time; expect waits. Build the core first (`Vocaterra.Nlp.slnf`); iterate with the fuzz
  project or a `--filter`; run `scripts/test.sh full` once at the end. Caps: 10 GB a command, 16 GB in all (the script's defaults).
- Prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses the words `eval`
  and `hash` in a command line). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. You run no game.
- Read CLAUDE.md, `docs/nlp/changing-the-core.md` (before changing the engine), `docs/nlp/adr/0009-core-and-game-boundary.md` and, if a
  public type changes, update `docs/nlp/api.md`. Edit only `src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp*`, `tools/Vocaterra.Nlp*` and
  `docs/nlp/` (not `docs/nlp/decisions.md` or `docs/nlp/implementation-plan.md`: say in the report what they should say). Do not edit
  `content/game-v1` (the shared bundle): if a root cause is in the bundle, stop and report it.
- **The fuzz suite is a gate: never weaken or narrow an oracle to make it pass** (CLAUDE.md). Each fix is at its source, with regression
  tests. M96 (`.claude/briefs/m96-porque-noun-reading.md` and `m96-report.md`, `AccentVariantChoice.DisagreesWithDeterminerBefore`) is the
  model of how such a seed was fixed last time.

## The failures (all three found on `feature/biomes` on 2026-11-16; the core findings log, `docs/nlp/findings.md`, its last two entries)
1. **O5, seed `2LxdNIrNHnQ6`** (`RecastLoopFuzzTests.Recast_RestoresTheCorrectedSentence_AndReAnalyzesClean`): the pattern
   `[NOUN:item] [NOUN:qty] [NUM] [NOUN:spell] [VERB:transportar]`, text «cuanto almuerzos veintiocho señoras transportan», mutated to «cuanto
   almuerzos veintiocho zeponefu transportan» (an unknown word after the number); the recast normalizes to «cuanto almuerzo 28 zeponefu
   transportan» instead of the corrected «cuanto almuerzos 28 zeponefu transportan»: the plural is lost. Replay:
   `CsCheck_Seed=2LxdNIrNHnQ6 scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests -m:2 --filter "FullyQualifiedName~RecastLoopFuzzTests"`.
2. **O3, seed `9kz6LGNo8tf2`** (`CorrectSentenceFuzzTests`): a correct sentence graded at the prompt `smith-receive`, which takes none of
   its intents, gets `vocab.wrong@4+2 expected 'tal' actual 'me'` (intent unknown, off the prompt none): «que me» is flagged as a wrong word
   for 'tal'. Replay with `--filter "FullyQualifiedName~CorrectSentenceFuzzTests"`.
3. **A load-sensitive coverage oracle:** `PipelineFuzzTests.OpenFallback_LeavesATrustedFirstReadingAlone...` failed once in a full run
   under load with "the generator no longer reaches fallback:second-chosen" (count 4) and passes alone. Find why the reached count depends
   on load (a time budget on sampling, an iteration count read from the environment, a timeout, shared state between tests), and make the
   oracle's requirement independent of the machine's load without lowering what it demands (for example a fixed number of samples or a
   seeded generator for the coverage part). If you find the oracle cannot be made load-independent without narrowing it, stop and say why
   instead of changing it.

## Deliverables
- The cause of each, in a sentence or two; the fix at the source; regression facts per seed (the exact inputs as [Fact]s in the owning
  module's test project, and their neighbours that must stay as they are: correct plurals after numbers, «que me» elsewhere, the
  smith-receive prompt's own correct lines); the three oracles unchanged in what they check.
- Each seed replayed after the fix (passes), and the fuzz project run three times in a row with fresh seeds.
- No change in the end-to-end goldens unless the fix moves them (re-record with `VOCATERRA_RECORD_GOLDEN=1` only then, and say why).
- `docs/nlp/findings.md`: an entry per finding resolved (cause, fix, seeds).

## Gate
Build with -warnaserror (0 warnings); `scripts/test.sh full` (@@COUNT@@ passed before, plus your new tests); format clean; the core
demo line from CLAUDE.md unchanged in substance. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

## Report (half a page)
Branch, head commit, worktree; each finding's cause and fix; the tests added; the replays; the full suite's count; what the core plan's row
and findings should say; anything left. No file dumps or long logs.
