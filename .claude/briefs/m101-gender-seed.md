You are implementing the core milestone **M101, a fuzz seed on an adjective's gender**, in the Vocaterra repository (C#, .NET 10). The
language core is `src/Vocaterra.Nlp`, `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` tests and tools; the game is not yours and must
not change. Do the work yourself; do not spawn subagents. Commit prefix `M101: `.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/m101 (absolute; cd there first), on the local branch
`M101-gender-seed` (from `feature/biomes` at 21f632e0). Commit after each step that builds and passes. Do not push; never push or merge
`feature/*` or `main`.
Resources and waiting: every dotnet command through scripts/limited.sh (builds `-m:4`, tests `-m:2`), prefixed with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; one heavy command at a time, no `&`; long commands with
`run_in_background: true`, waiting for the notification; never `sleep`/`until` polls or `nohup ... &`. Other agents build now.
Read CLAUDE.md, docs/nlp/changing-the-core.md, tests/Vocaterra.Nlp.Fuzzing.Tests/README.md, and M98's report (.claude/briefs/m98-report.md:
the model for this, item 3). Edit only `tests/Vocaterra.Nlp*`, `src/Vocaterra.Nlp*` only if the step below allows it, and `docs/nlp/`
(not decisions.md or implementation-plan.md). **The fuzz suite is a gate: never weaken or narrow an oracle to make it pass.**
The seed (docs/nlp/findings.md, the last entry): O3, `CorrectSentenceFuzzTests.CorrectSentences_AnalyzeClean_MatchTheirGrammar_AndYieldTheirSlots`,
seed `3gcdMpDLcrQ4`: the pattern `[[NOUN:item]? [WORD:qty]? [ADJ] [PRON] ([PRON] [DET]|[DET]|[ART] [ART])?]` realised «Laná doloroso les»
as correct; the analyser reports `gender.agree@5+8`, expected «dolorosa». Replay:
`CsCheck_Seed=3gcdMpDLcrQ4 scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests -m:2 --filter "FullyQualifiedName~CorrectSentenceFuzzTests"`.
Find the cause: what «Laná» is (a generated nonce item noun? its gender in the lexicon?), whether the realiser should have agreed the
adjective with it, or the analyser misreads its gender. **If the fault is the realiser's or the oracle's, fix it there** (a regression fact
and its neighbours; the realisation-rate floors from M98 must still hold). **If the fix belongs in the engine or the Spanish pack, do not
change them:** describe the cause and the proposed fix in a findings entry and the report, and leave the seed failing.
Deliverables: the cause; the fix (or the proposal); the fuzz project three times with fresh seeds (say any failure and its seed); a
findings entry. Gate: build -warnaserror (0 warnings), `scripts/test.sh full` (12,766 passed at your base), format clean. Commit messages
end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6
Report in a few lines: head, the cause, the fix or proposal, the fresh runs, the suite count.
