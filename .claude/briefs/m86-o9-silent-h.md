You are implementing the core milestone **M86, O9's double silent h**, in the Vocaterra repository (C#, .NET 10). The language core is
`src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and tools; the game
(`Vocaterra.Game*`, `Vocaterra.Host`) is not yours and must not change. Do the work yourself; do not spawn subagents. Commit prefix `M86: `.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m86`, already on the branch `M86-o9-silent-h` (from
`feature/bread-pilot` at its tip; no build yet). cd there first; never edit `/home/user/Dev/Vocaterra` or another worktree. Commit after
each step that builds and passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or
`main`. Run everything in the foreground and finish in one turn (no background commands, monitors or wait loops).

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**,
**Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/m86/` for your logs, about 14 GB of disk free, and **10395
passed, 3 skipped** as the count before you. Build the core first (`Vocaterra.Nlp.slnf`); the whole solution only for the final run. Do not
edit `content/game-v1`, `docs/nlp/decisions.md` or either implementation plan; say in the report what they should say. Read
`docs/nlp/changing-the-core.md` before changing the engine. **The fuzz suite is a gate: never weaken an oracle to make it pass** (CLAUDE.md).

## The failure (CI on PR #40, 2026-11-08; reproduced locally)
`tests/Vocaterra.Nlp.Fuzzing.Tests/PronunciationFuzzTests.cs`, `SpokenPhrases_ScorePerfectly_OrYieldExactlyTheInjectedPredictedError`
(oracle O9, `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` line 39 and its section), with `CsCheck_Seed=8HYgz_I9Ysn1`:
```
text="ligaba eh hieran" dialect=1 error=850 shape=FrameShape { FramesPerPhone = 3, BlankFramesBetween = 1, EdgeBlankFrames = 0 } mumble= mode=Gop difficulty=Beginner
[O9] Injected pron.silent_h at TextSpan { Start = 10, Length = 1, End = 11 } (heard [l i ɣ a β a e h j e ɾ a n] for [l i ɣ a β a e j e ɾ a n])
but the scorer reported [pron.silent_h@TextSpan { Start = 8 } '' -> 'h'; pron.silent_h@TextSpan { Start = 10 } '' -> 'h'].
```
Reproduce: `CsCheck_Seed=8HYgz_I9Ysn1 dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests --filter "FullyQualifiedName~SpokenPhrases_ScorePerfectly"`
(through `scripts/limited.sh`). One heard [h] sits between the final silent h of «eh» and the initial silent h of «hieran»; both
predicted-error variants (`src/Vocaterra.Nlp/Phonology/PredictedErrorVariants.cs` and the pronunciation scorer under
`src/Vocaterra.Nlp/Pronunciation/`) claim it, so one inserted phone becomes two errors.

## What to do (each committed)
1. **Find the cause** in the scorer's alignment or error attribution (how predicted-error variants of neighbouring words compete for one
   inserted phone). Say whether `main` has the same bug (the code's history since `origin/main`; it is likely old and only now hit).
2. **Fix it at the source:** one heard phone is attributed to at most one predicted error. Where two silent letters could each own it,
   attribute it deterministically and sensibly (the oracle expects the injected letter: an [h] heard before a word's vowel or glide is that
   word's initial h); say the rule in the code and in the report. Do not change O9's assertion or its generator to dodge the case.
3. **A regression test:** the failing input as a `[Fact]` in the owning module's test project (the oracle's "Replay" line says how), plus
   the mirror case if one exists (an [h] injected for «eh»'s final h, if the generator can make it, gives only that one).
4. **Proof:** the seed passes; the whole fuzz project three times with random seeds; `scripts/test.sh full` once; no golden, replay or
   baseline moved (or each moved one explained).

## Report (half a page)
Branch, final SHA, worktree clean; the cause; whether `main` has it; the fix and its attribution rule; the tests; the proof runs with
counts; what the orchestrator should record in §5 and §5b; anything left.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
