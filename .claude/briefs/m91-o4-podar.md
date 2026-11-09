You are implementing the core milestone **M91, O4's `podar` before an infinitive**, in the Vocaterra repository (C#, .NET 10). The language
core is `src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and tools; the game
(`Vocaterra.Game*`, `Vocaterra.Host`) is not yours and must not change. Do the work yourself; do not spawn subagents. Commit prefix `M91: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m91`, already on the branch `M91-o4-podar` (from
`feature/ui-issues` at `f87776bb`, which has U05, U06 and U08 merged; no build yet). cd there first; never edit `/home/james/Dev/Vocaterra`
or another worktree. Commit after each step that builds and passes its tests; never end your turn with uncommitted work. Do not push; never
push or merge `feature/*` or `main`. Run everything in the foreground and finish in one turn (no background commands, monitors or wait loops).

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m91/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**,
**Environment**, **Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/m91/` for your logs and **10426 passed, 3
skipped** as the count before you (U08's run). Build the core first (`Vocaterra.Nlp.slnf`); the whole solution only for the final run. Do not
edit `content/game-v1`, `docs/nlp/decisions.md` or either implementation plan; say in the report what they should say. Read
`docs/nlp/changing-the-core.md` before changing the engine. **The fuzz suite is a gate: never weaken an oracle to make it pass** (CLAUDE.md).
**Another agent (U07, the game's book) builds on this machine at the same time, and other sessions too (the owner, 2026-11-08): keep your
CPU and memory low.** Everything heavy through `scripts/limited.sh`, one heavy command at a time, the fuzz project or a `--filter` while
iterating, `scripts/test.sh full` once at the end. You run no game.

## The failure (the merge gate of U08, 2026-11-08; reproduced on the code before U08)
`tests/Vocaterra.Nlp.Fuzzing.Tests/MutationTagFuzzTests.cs`, `OneMutation_YieldsExactlyTheNamedTag_WithTheOriginalFormAsCorrection`
(oracle O4, `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` line 34 and its sections), with `CsCheck_Seed=3m67O5Rpuya2`:
```
(MutationCase { Kind = ConjPerson, Seed = 1735878 }, TextNoise { CaseMode = 0, AccentMode = 0, Nfd = True, Punctuation = 0, Spaces = 0, NumberMode = 0, AdversarialIndex = -1, Seed = 11 })
[O4] The analyzer's errors differ from the mutation's: missing [conj.person@9+4 expected 'podáis' actual 'podo'] extra [conj.person@9+4 expected 'podéis' actual 'podo'; conj.irregular@9+4 expected 'puedo' actual 'podo'].
Input: "ConjPerson: pattern=[[VERB:podar:item] [VERB:rastrear]? [VERB:operar:qty]] text="vosotras podáis rastrear opero" => mutated="vosotras podo rastrear opero"
```
Reproduce: `CsCheck_Seed=3m67O5Rpuya2 scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests -m:2 --filter "FullyQualifiedName~OneMutation_YieldsExactlyTheNamedTag"`.
Background: M58 made «podo»/«pode» before an infinitive read as `poder`'s slip (`IAnalysisRules.CallsForSlip`, "`podar` takes no
infinitive": `yo podo ir` → `puedo`; `docs/nlp/implementation-plan.md`, `grep -n "CallsForSlip"`); M58 and M73 fixed earlier `podar`
findings and added homograph re-rolls (`grep -n "podamos\|re-roll\|HomographReroll" docs/nlp/implementation-plan.md`,
`tests/Vocaterra.Nlp.Fuzzing.Tests/HomographRerollTests.cs`).

## What to do (each committed)
1. **Find the cause and say which side is wrong.** The generator built `podar` followed by an infinitive («vosotras podáis rastrear»),
   which M58 decided reads as `poder`; the analyzer then also gives two corrections for one word (`conj.person` → `podéis` and
   `conj.irregular` → `puedo`, which disagree with each other and with «vosotras»). Decide, with the code and the earlier findings as
   evidence: (a) the generator should not realise `podar` before an infinitive (a re-roll in the existing style, as M58/M73's homograph
   re-rolls: the sentence is not one a reader takes as `podar`), and/or (b) the analyzer is wrong (for example the two contradictory
   corrections at one span, or the person: with «vosotras» the slip's correction should be `podéis`, never `puedo`). Fix what is wrong;
   if both are, fix both. Say whether `main` has the same behaviour.
2. **Never change O4's assertion.** A re-roll is allowed only for a sentence that is genuinely another reading (as M73's), stated in the code
   with the reason and the seed, and covered by a test in `HomographRerollTests` style; the stream digest re-recorded only if it moves, and
   say why.
3. **Regression tests:** the failing input as a `[Fact]` (the oracle's "Replay" line), and if you change the analyzer, its own test in the
   owning module's test project (for example `vosotras podo rastrear` and `yo podo ir` with the corrections you decided).
4. **Proof:** the seed passes; the whole fuzz project three times with random seeds; `scripts/test.sh full` once; no golden, replay or
   baseline moved (or each moved one explained).

## Report (half a page), also written to `/tmp/claude-1000/m91/report.md`
Branch, final SHA, worktree clean; the cause and which side was wrong; whether `main` has it; the fix; the tests; the proof runs with counts;
what the orchestrator should record in §5 and §5b; anything left.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
