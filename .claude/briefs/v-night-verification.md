You are verifying, in the game, the B-series milestones merged into `feature/biomes` on 2026-11-15 and 16 while game windows were not
allowed, for the Vocaterra repository (C#, .NET 10; the game is Vocaterra.Game.Core, Vocaterra.Game (MonoGame) and Vocaterra.Host). This is
a check, not a milestone: **you change no source, test, content or document in the repository and commit nothing**. You write a report and
pictures outside git. If you find a bug, describe it with how to reproduce it; the orchestrator plans the fix.

Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/verify (detached at feature/biomes 4fc0a00f) for the "after" build,
and /home/james/Dev/Vocaterra/.claude/worktrees/verify-base (detached at `bb145f22`, feature/biomes when the work stopped on 2026-11-15,
before B02, B04b, B08a, B06, B09, B08b, B10, B15, M96, B10b and B11) for the "before" build. Both worktrees already exist. Build each
once (`scripts/limited.sh dotnet build Vocaterra.sln -m:4`); run nothing heavy outside scripts/limited.sh; one heavy command at a time,
no `&`, no wait loops on `pgrep -f`. Other agents build at the same time; expect waits. Environment: prefix every command with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Set `VOCATERRA_RUNS_WORK=/tmp/claude-1000/verify-runs` for every scripted run,
so you never share a work folder with another agent. Game windows are allowed (the owner is asleep).

Read first: CLAUDE.md; docs/game/running.md; docs/game/scripted-runs.md; scripts/scripted-runs.sh and scripts/compare-runs.py (how to
capture and compare); the reports in /home/james/Dev/Vocaterra/.claude/worktrees/biomes/.claude/briefs/: b02-report.md, b04b-report.md,
b08a-report.md, b08a-merge-report.md, b06-report.md, b09-report.md, b08b-report.md, b10-report.md, b10b-report.md, b11-report.md,
b15-report.md, m96-report.md (each says what should and should not change in the game).

Do:
1. Scripted runs: capture "before" (from verify-base) and "after" (from verify) with scripts/scripted-runs.sh (GAME_BIN pointing at each
   worktree's build, run from the matching worktree), then compare with `python3 -I scripts/compare-runs.py`. For every run that differs,
   say which milestone explains each difference (expected so far: B08a's pass and rockfall in the east ring and its scene line; the state
   digest lines from B05's header, B06's template digest and world.json hash, B11's generator version; the bundle hash; B08b's pass
   section; nothing from B04b, B09, B10, B10b, M96 or B15 in the valley's pictures; the known timing noise of r01-route's sounds.log and
   r17-speak's voice order). Anything else that differs is a finding: look at both pictures and say what changed.
2. Pictures (look at every one you take with the Read tool before you describe it), at midday in clear weather, High quality, into
   /home/james/Dev/Vocaterra/artifacts/b-series/verify/ with an index.md:
   - B08b's three: the rockfall from the road at about x 335 to 340, z 208, looking east to (355, 200), closed; the same with
     `--pass open`; the road at about (380, 208), y 58, looking east, with `--pass open`.
   - B08a's road from the village to the pass and the cutting, from the village and from the rim.
   - The valley from three or four known viewpoints (the camp, the village, the peaks: docs/game/running.md's S13 demos), before and
     after, to confirm B04b's renderer and B09's masks left them identical.
3. Play-check with the scripted steps the runs support: walking the road through the open pass to the edge (the edge stops the body in
   the bounded valley); the cheat setting; a save and load with the pass open (the rockfall stays gone).
4. The default game start, the guide's first exchange and the core demo line, to confirm nothing in play broke.

Report (one page): the two builds' commits; the scripted-run comparison (runs identical, runs that differ with the explaining milestone,
anything unexplained); what you saw in each picture, plainly, with anything that looks wrong; play-check results; bugs found, each with a
reproduction; the pictures' folder. Leave out file dumps and long logs. Do not remove the two worktrees (the orchestrator does).
