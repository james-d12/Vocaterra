You are implementing B11d, "Cave mouths in hill and cliff faces", for the Vocaterra repository (C#, .NET 10; the game is
Vocaterra.Game.Core, Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b11d (absolute; cd there first), on the local branch
`B11d-hillside-mouths` (cut from `feature/biomes` at 21f632e0). Commit after each step that builds and passes, prefix `B11d: `. Do not push;
never push or merge feature/biomes or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b11d/, removed at the end.
Resources and waiting: every dotnet restore, build, test and format, every game or scripted run through scripts/limited.sh (builds `-m:4`,
tests `-m:2`); one heavy command at a time, no `&`. Run a long command with the Bash tool's `run_in_background: true`, end your turn and
wait for its notification; never poll with `sleep` or `until` loops, never `nohup ... &`. Game runs: a Release build,
`scripts/limited.sh timeout 1200 <command>`, `--far-view off` unless the far view matters. Other agents build at the same time: expect
waits. Iterate with `scripts/test.sh fast` plus the classes you touch; **merge feature/biomes into your branch before your one
`scripts/test.sh full`**, and report only after it finishes. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. The
sandbox refuses the words `eval` and `hash` in a command line (run `scripts/content.sh hash ...` from a wrapper script).

Why: the owner, 2026-11-17: "Seven, also do that as well" (D-game-34 item 48): cave entrances mostly open as holes in flat ground
(B11b places them on dry level land; .claude/briefs/b11b-report.md: "hillside mouths rare in the valley world"); mouths that open sideways
into hill and cliff faces, as in Minecraft, should be common. Read b11b-report.md (the entrance sites, `EntranceSites`, `EntranceRamp`,
the winding passage, the rules for water, regions and depth) and b11c-report.md (`CaveCoverTests`: no pit outside an entrance's passage).
Your row: docs/game/implementation-plan.md section 4.14, B11d.
Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/ (unless named), either implementation-plan.md, either
decisions.md or docs/game/biomes-plan.md (say in the report what they should say). No biome or ore id in source. The goal tonight
(the owner, 2026-11-17): feature/biomes ready to merge into main by morning, so keep the change contained and the suite green. Keep B11b's rules (no entrance in water or within its reach, none in a region, the passage's depth and headroom) and B11c's test.

Deliverables:
1. Entrance sites favour slopes and cliff faces: where the ground falls steeply within the site, the mouth opens sideways into the slope
   (an arch in the face, a passage running in level or gently down), and flat-ground holes become the minority; say the share of each on
   seeds 42, 7 and 2026 before and after.
2. Tests over three seeds: a share of entrances (state the floor from your measurement) open into a slope; every mouth is walkable from
   outside (a player can walk in from the ground beside it); `CaveCoverTests` and B11b's water tests still pass.
3. Pins re-recorded **once**, at the end (the goldens and the fixture's state digest that moved); say which. B02d (lake levels) runs at
   the same time and also moves pins: whoever merges second re-records.
4. Pictures (Release, High, midday) of three hillside mouths and one cliff mouth, from outside and from a few blocks in, in
   /home/james/Dev/Vocaterra/artifacts/b-series/b11d/ with an index.md. Look at each.
Gate: build -warnaserror (0 warnings); scripts/test.sh full (12,766 passed on feature/biomes with B17c, plus your tests); format clean;
content validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6
Report, a few lines: branch, head, worktree; how sites choose slopes; the shares before and after; the tests; pins moved; the pictures;
the suite's count. No file dumps.
