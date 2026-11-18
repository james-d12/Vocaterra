You are implementing the core milestone **M99, two silent h's in a row**, in the Vocaterra repository (C#, .NET 10). The language core is
`src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and tools; the game
(`Vocaterra.Game*`, `Vocaterra.Host`, `tools/Vocaterra.SpeechRender`, `tools/Vocaterra.WorldPreview`) is not yours and must not change. Do
the work yourself; do not spawn subagents. Commit prefix `M99: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m99` (an absolute path; cd there first), already on the local
branch `M99-silent-h` (from `feature/biomes` at 0d72b26b). Never edit another worktree. Commit after each step that builds and passes its
tests. Do not push; never push or merge `feature/*` or `main`. Wait for each command to finish (no wait loops on `pgrep -f` or on a file:
a loop left behind runs forever).

## Resources, environment, rules
- Every dotnet restore, build, test and format through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`), one heavy command at a time,
  no `&`. Other agents build at the same time; expect waits. Build the core first (`Vocaterra.Nlp.slnf`); iterate with a project or a
  `--filter`; run `scripts/test.sh full` once at the end.
- Prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses the words `eval`
  and `hash` in a command line). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. You run no game.
- Read CLAUDE.md, `docs/nlp/changing-the-core.md`, `docs/nlp/adr/0009-core-and-game-boundary.md`, `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md`.
  Edit only `src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp*`, `tools/Vocaterra.Nlp*` and `docs/nlp/` (not `docs/nlp/decisions.md` or
  `docs/nlp/implementation-plan.md`: say in the report what they should say). Do not edit `content/game-v1`. A change to a public type in
  `docs/nlp/api.md` updates that record.
- **The fuzz suite is a gate: never weaken or narrow an oracle to make it pass** (CLAUDE.md). M97 (`.claude/briefs/m97-fuzz-seeds.md`,
  `m97-report.md`) and M98 (`m98-fuzz-health.md`, `m98-report.md`) are the models.

## The work (the owner, 2026-11-17: "Fix the silent HC, yeah, yep. Yeah, fix number five"; D-game-34 item 46, core findings)
The seed (docs/nlp/findings.md, the entry with `a2RKk5N6Ibt4`): `PronunciationFuzzTests.SpokenPhrases_ScorePerfectly_OrYieldExactlyTheInjectedPredictedError`,
«ah hambrientos» with `pron.silent_h` injected at «ah» (heard [a h a m b ɾ j e n t o s] for [a a m b ɾ j e n t o s]); the scorer reports
`pron.silent_h` twice, at «ah» and again on the h of «hambrientos». Replay:
`CsCheck_Seed=a2RKk5N6Ibt4 scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests -m:2 --filter "FullyQualifiedName~PronunciationFuzzTests"`.
1. Find the cause in the pronunciation scorer: how the alignment places one inserted [h] between two words whose spellings both hold a
   silent h, and why it is charged to both. Say it plainly.
2. Fix it in the engine or the Spanish pack, wherever the cause is: one inserted [h] yields one `pron.silent_h`, at the word it belongs
   to (here «ah», where it was injected; say how the scorer decides when the [h] could belong to either word).
3. A regression fact for this phrase and its neighbours (an [h] injected at «hambrientos» instead; two [h]s injected, one at each word;
   a silent h followed by a word starting with a vowel; a single word with a silent h), in the core's pronunciation tests.
4. The fuzz project three times in a row with fresh seeds (say what failed, if anything, with its seed); the seed passes on replay.

## Deliverables
- 1 to 4 with tests; `docs/nlp/findings.md`: an entry (cause, fix, seed). No change in the end-to-end goldens (if one moves, say why and
  re-record it with `VOCATERRA_RECORD_GOLDEN=1` only if the change is the intended one).

## Gate
Build with -warnaserror (0 warnings); `scripts/test.sh full` (12,531 passed on feature/biomes at your base, plus your new tests); format
clean; the core demo line from CLAUDE.md unchanged in substance. A timing test that fails under load: rerun it alone and say so. Commit
messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

## Report (a few lines)
Branch, head commit, worktree; the cause; the fix; the tests; the three fresh runs; the full suite's count; what the core plan's row
should say; anything left. No file dumps or long logs.
