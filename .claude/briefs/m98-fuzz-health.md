You are implementing the core milestone **M98, the fuzz suite's health**, in the Vocaterra repository (C#, .NET 10). The language core
is `src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and tools; the game
(`Vocaterra.Game*`, `Vocaterra.Host`, `tools/Vocaterra.SpeechRender`, `tools/Vocaterra.WorldPreview`) is not yours and must not change. Do
the work yourself; do not spawn subagents. Commit prefix `M98: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m98`, already on the local branch `M98-fuzz-health` (from
`feature/biomes` at ac8b1391). cd there first; never edit `/home/james/Dev/Vocaterra` or another worktree. Commit after each step that builds
and passes its tests. Do not push; never push or merge `feature/*` or `main`. Wait for each command to finish (no wait loops on `pgrep -f`).

## Resources, environment, rules
- Every dotnet restore, build, test and format through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`), one heavy command at a time,
  no `&`. Four other agents build at the same time; expect waits. Build the core first (`Vocaterra.Nlp.slnf`); iterate with the fuzz
  project or a `--filter`; run `scripts/test.sh full` once at the end. Caps: 10 GB a command, 16 GB in all (the script's defaults).
- Prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses the words `eval`
  and `hash` in a command line). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. You run no game.
- Read CLAUDE.md, `docs/nlp/changing-the-core.md`, `docs/nlp/adr/0009-core-and-game-boundary.md`, `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md`.
  Edit only `tests/Vocaterra.Nlp*`, `src/Vocaterra.Nlp*` where a step below allows it, `tools/Vocaterra.Nlp*` and `docs/nlp/` (not
  `docs/nlp/decisions.md` or `docs/nlp/implementation-plan.md`: say in the report what they should say). Do not edit `content/game-v1`.
- **The fuzz suite is a gate: never weaken or narrow an oracle to make it pass** (CLAUDE.md). M97 (`.claude/briefs/m97-fuzz-seeds.md`,
  `m97-report.md`) is the model.

## The work (the core plan's row M98; the owner said "go ahead with all the fixes recommended from review", D-game-34 item 37)
1. **A realisation-rate gate (the codebase review's T3, docs/research/2026-11-16-codebase-review.md section 3).** The fuzz generator
   (`tests/Vocaterra.Nlp.Fuzzing.Tests/Generation/SentenceRealizer.cs`, about 2,350 lines) holds about 54 re-roll rules: a recipe it
   cannot realise safely is skipped (the run prints "N skipped"), so each new rule narrows the suite with nothing to show it. Count the
   recipes realised and the skips per skip reason and per pattern family, print them in the test output, and pin a floor on the
   realisation rate per family (from the measured rates on a fixed seed, with a stated margin), so a change that makes the generator avoid
   much more fails a test. Fixed seeds for the pinned measurement so it does not vary run to run. Say the rates you measured.
2. **M97's fixed-seed coverage test flaked inside a full run** (`OpenFallback_TheGeneratorReachesEveryPath_OnAFixedSeed`, in B18's gate on
   2026-11-16, passing alone). A fixed seed should give a fixed count: find what still varies (CsCheck's parallel sampling across threads,
   shared static state between tests, an iteration count read from the environment, test order) and make it truly deterministic without
   lowering its floor. Show it with the same count over several runs, including inside a full run of its project.
3. **A new seed, `7q7l8dSNG6n6`** (O3, `CorrectSentenceFuzzTests.CorrectSentences_AnalyzeClean_MatchTheirGrammar_AndYieldTheirSlots`,
   found 2026-11-17; the core findings log's last entry): the pattern `[[ADV]? [NOUN:item]? [VERB:aclarar] [VERB]]` realised «alrededor
   aclara construir» with `alrededor` as the item noun; the analyser yields no item slot. Replay:
   `CsCheck_Seed=7q7l8dSNG6n6 scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests -m:2 --filter "FullyQualifiedName~CorrectSentenceFuzzTests"`.
   Find the cause: whether the lexicon's noun reading of «alrededor» is wrong or rare (an item?), the realiser should not offer it as an
   item, or the analyser's adverb reading wins where the noun was meant. **If the fault is the generator's or the oracle's, fix it there**
   (with a regression fact and its neighbours). **If the fix belongs in the engine or the Spanish pack, do not change them:** describe the
   cause and the proposed fix in the findings entry and the report (the owner decides engine fixes for fuzz seeds, as for the open silent-h
   seed `a2RKk5N6Ibt4`), and leave the seed failing.

## Deliverables
- 1 to 3 as above with tests; the fuzz project run three times in a row with fresh seeds (say what failed, if anything, with its seed).
- `docs/nlp/findings.md`: an entry per item (cause, fix or proposal, seeds, the rates).
- No change in the end-to-end goldens.

## Gate
Build with -warnaserror (0 warnings); `scripts/test.sh full` (12,453 passed on feature/biomes before, plus your new tests); format clean;
the core demo line from CLAUDE.md unchanged in substance. A timing test that fails under load: rerun it alone and say so. Commit messages
end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

## Report (half a page)
Branch, head commit, worktree; each item's cause and fix (or proposal); the rates and floors; the tests added; the three fresh runs; the
full suite's count; what the core plan's row should say; anything left. No file dumps or long logs.
