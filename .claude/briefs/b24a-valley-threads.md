You are implementing B24a, "The valley underground's threads and its timing test", for the Vocaterra repository (C#, .NET 10). The game
is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not
yours. This is a small milestone: src/Vocaterra.Game.Core/World/Generation/ValleyUnderground.cs (`Lay`, the `Parallel.For` at about line
97), its callers, and tests/Vocaterra.Game.Core.Tests/World/Underground/ValleyUndergroundTests.cs (the timing test at about line 412).
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b24a, already on the local branch `B24a-valley-threads` (cut from
`feature/biomes` at __BASE__). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B24a: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format through scripts/limited.sh (builds `-m:4`, tests `-m:2`; the script's caps); one
heavy command at a time, no `&`, no wait loops on `pgrep -f`. Other agents build at the same time: expect waits. Iterate with
`scripts/test.sh project tests/Vocaterra.Game.Core.Tests` (or a `--filter`); `scripts/test.sh full` once at the end. Scratch on disk under
/home/james/Dev/Vocaterra/artifacts/tmp/b24a/, removed at the end. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1. No game windows are needed.

Why: the owner said "go ahead with all the fixes recommended from review" (D-game-34 item 37). The codebase review
(docs/research/2026-11-16-codebase-review.md, section 1, C9) found that `ValleyUnderground.Lay` runs `Parallel.For` on up to eight
threads (`MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8)`), which breaks the game's two-worker rule and the machine's
limits. Separately, `Given_TheValleyBuiltWithAndWithoutTheUnderground_When_TheyAreTimed_Then_TheUndergroundCostsUnderOneSecond` failed
four full-suite runs in a row on 2026-11-17 at load 11 to 19 ("the underground adds 1650 ms"), while it passes alone in about 8 s.
It compares wall-clock times, so other tests and agents on the machine make it fail.

Read first: CLAUDE.md; docs/game/implementation-plan.md section 4.14 (your row, B24a); `ValleyUnderground.cs` and its callers
(`ValleyBuild`, the scene build, the streamed world's region copy if it calls `Lay`); docs/process/test-performance-plan.md (the `TestSpeed`
trait).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. The valley's blocks must not change: `SceneTests.GoldenDigest`, the 18 procedural goldens and the save-v9
fixture's state digest stay as they are (the result must not depend on the thread count; there is a test for that already, keep it).

Deliverables:
1. `Lay` runs on at most two threads. Either a fixed bound of `Math.Min(2, Environment.ProcessorCount)`, or a degree the caller passes
   (default 2); say which and why. Measure the valley build with the underground before and after, alone, and say the cost.
2. The timing test checks something that holds under load and still catches a real slowdown. For example: the laying thread's CPU time
   rather than wall clock, a count of work done (cells visited) against a bound, or the fastest of more attempts with a bound set from the
   measured cost with a stated margin, marked `[Trait(TestSpeed.Trait, TestSpeed.Slow)]`. Say what you chose and the measured numbers.
   Do not simply raise the bound to something it can never reach.
3. Tests: the thread bound (the degree used is at most 2), the result identical at degree 1 and 2.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,453 passed on feature/biomes before, plus your new tests); format
clean. A timing test that fails under load: rerun it alone and say so. Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, a few lines: branch, head commit, worktree; the thread bound and the measured cost; the test's new check and its numbers; the suite
result; anything left. No file dumps or long logs.
