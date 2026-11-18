You are implementing B29d, "The last wall-clock timing", for the Vocaterra repository (C#, .NET 10). It changes four test classes only; no
game or core behaviour.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b29d (an absolute path; cd there first), already on the local
branch `B29d-cpu-timing`, cut from `feature/meta_improvements` at BASE `9f2f3a31`. Commit with the prefix `B29d: `. Do not push. Never
push or merge feature/meta_improvements or main. Leave the worktree clean on its branch. Another agent (B29e) works beside you on
`scripts/hooks/` only.
Resources: every dotnet build, test and format runs through scripts/limited.sh (builds `-m:4`, tests `-m:2`); scripts/test.sh calls it
itself. One heavy command at a time. Waiting: run a long command with the Bash tool's `run_in_background: true`, end your turn and wait
for its notification; never poll with `sleep` or loops, never `nohup` (a hook blocks them). Exit 137 or 143: lower the parallelism. Output
to log files under /home/james/Dev/Vocaterra/artifacts/tmp/b29d/ (removed at the end), at most 30 lines printed.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1.

Why: the owner's decision D-game-34 item 60 (docs/game/decisions.md); your row docs/game/implementation-plan.md section 4.14, B29d. B29b
(.claude/briefs/b29b-report.md, "Left") found these four classes in the timing collection still measure with a wall `Stopwatch`:
`ChunkIndexTests`, `RegionTimeTests`, `ProceduralTimingTests`, `ProceduralChunkTimeTests` (all under tests/Vocaterra.Game.Core.Tests).
Read first: CLAUDE.md; `scripts/test.sh`'s header (Release by default; `timing` mode); tests/Vocaterra.Game.Core.Tests/TestSupport/CpuTime.cs;
tests/Vocaterra.Game.Core.Tests/Timing/SpeedChecksTimingTests.cs (the model: `CpuTime.Fastest(3, ...)`, `CpuTime.Budget(...)`, the
collection and the `Category=Timing` trait); the four classes.

Rules: keep each test's meaning and its outcome asserts; never remove a test. Each speed check measures with `CpuTime` and holds a budget
of twice its Release cost (measured with `scripts/test.sh timing`, three runs; say each: old bound, Release cost, new budget). A cost below
the CPU clock's resolution (about 10 ms) is measured over enough repeats and given a small fixed budget, as B29b did; say which. If a part
of a class already uses `CpuTime`, leave it. `WallClockRulesTests` (tests/Vocaterra.Game.Core.Tests/Architecture/) must still pass; if it
skips timing files, that does not change. Do not edit CLAUDE.md, `.claude/`, scripts/, either plan or either decisions file.

Gate (from your worktree root): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh
timing passes three times in a row (23 tests on the base, give the new count); scripts/test.sh full passes (12,631 on the base);
scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDc2yqvjCy1ntkujnVS
Report, a third of a page: branch, head commit; each class's change with its budgets; the three timing runs; test counts; anything left.
