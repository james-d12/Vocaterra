You are implementing the core milestone **M100, the lexicon load test on CPU time**, in the Vocaterra repository (C#, .NET 10). The
language core is `src/Vocaterra.Nlp`, `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` tests and tools; the game is not yours and must
not change. A small milestone. Do the work yourself; do not spawn subagents. Commit prefix `M100: `.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/m100 (an absolute path; cd there first), on the local branch
`M100-lexicon-timing` (from `feature/biomes` at 24e8066e). Commit after each step that builds and passes. Do not push; never push or merge
`feature/*` or `main`.
Resources and waiting: every dotnet command through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`), prefixed with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; one heavy command at a time, no `&`. Run a long command with the Bash
tool's `run_in_background: true`, end your turn and wait for its notification; never poll with `sleep` or `until` loops, never
`nohup ... &`. Other agents build now: expect waits.
Why: `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` (tests/Vocaterra.Nlp.Tests/Content/GeneratedLexiconTests.cs) takes the median of
three wall-clock loads against 3 s and fails gates when other agents load the machine (B24's report; the codebase review's T2;
docs/research/2026-11-17-ai-development-throughput.md section 2.6). The game's timing tests were moved to CPU time by B24a and B24: read
how (`CpuTime` or the process CPU clock, the fastest of three, a non-parallel "timing" collection that runs after the rest), in
tests/Vocaterra.Game.Core.Tests (search for `CpuTime` and the timing collection).
Rules: edit only `tests/Vocaterra.Nlp*` and `docs/nlp/` (not `docs/nlp/decisions.md` or `docs/nlp/implementation-plan.md`). Never weaken
the test: it must still catch a real slowdown (a doubling of the load's cost).
Deliverables: the test measures the load's CPU time (the fastest of three), runs alone in a non-parallel collection, with a budget twice
the measured cost (say the numbers); a findings entry in docs/nlp/findings.md. Gate: build -warnaserror (0 warnings), `scripts/test.sh full`
(12,643 passed at your base), format clean. Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6
Report in three lines: head, the measured cost and budget, the suite count.
