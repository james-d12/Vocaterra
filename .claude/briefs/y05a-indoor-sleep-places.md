You are implementing Y05a, "Indoor sleep places", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender; the language core (Vocaterra.Nlp*) is
not touched. This is a small bug fix in the game.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y05a, which already exists on the branch `Y05a-indoor-sleep-places` (cut
from `feature/settlement`). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y05a &&` and use absolute paths for
file tools; your shell may start in another checkout, which you must not edit. Commit after each item that builds and passes its
tests, with the prefix `Y05a: `. Do not push your branch. Never push or merge `feature/settlement` or `main`. When you finish, leave the
worktree on your branch, clean, with no process of yours still running.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh calls it itself: run it directly. Never start two heavy commands at once (no `&`, no
background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Another session builds on this machine, so a
heavy command may wait for a slot: let it wait. While iterating run `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run
`scripts/test.sh full` once before reporting. Exit 137 means the 6 GB cap killed it: lower the parallelism. Set `VOCATERRA_RUNS_WORK`
to a folder of your own (in your scratchpad) for every `scripts/scripted-runs.sh` call, the same folder before and after.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

The bug (found by the orchestrator on the merged build of Y05, 2026-11-11). Run:
`--seed 42 --mute --needs on --dev --lodging --skip-to-session 2 --walk-to 278,231 --wait 1 --sleep --wait 2 --need thirst 0 --need
health 5 --wait 25 --screenshot <png> --frames 30 --save <file>`. The player sleeps in the inn's bed, dies of thirst, and wakes at
(278, 38, 233): on the grass outside the inn's south wall. Standing there, the HUD offers "E: sleep until morning" for the bed inside.
Two causes:
1. `VoxelSession.Death.cs` `WakeAtSleepPlace` takes `NearestFreeGround(place.Cell, WakeFarSquared, WakeNearSquared, ...)`: the nearest
   free ground 2 to 5 blocks across from the place's cell, by straight distance, through walls. The guest room is small and the minimum
   of 2 (meant to keep the player out of a fire ring) pushes the cell outside.
2. `Session/SleepPlaces.cs` (the test near line 137 behind `UsableAt` and `LockedBedAt`): "at the place" is a radius and a height,
   so it also passes through walls.

Read first: CLAUDE.md; docs/process/code-guidelines.md; docs/game/settlement-plan.md section 8.3, the paragraphs Y05a (yours), Y05 and
Y12 (the camp's bedroll and ring will be sleep places made in play, in the open); in docs/game/decisions.md "D-game-24" point 1. Then
the code, by member name: `VoxelSession.Death.cs` (`WakeAtSleepPlace`, `NearestFreeGround`, `IsFreeGround`, the constants
`WakeNearSquared`, `WakeFarSquared`); `SleepPlaces.cs` (whole); `SleepOffer.cs` (`AtFire`, `AtLockedBed`); `VoxelWalker.FindPath`
(the walk search `InnTests` uses); content/game-v1/game/needs.json (`camp.fire`, `inn.bed`: point `bed.inn`, the stand cell (278, 231),
beside the west bed at (277, 231); the inn's door is (280, 226)); tests/Vocaterra.Game.Core.Tests/World/Generation/InnTests.cs (how
it builds the scene and walks to the bed), LodgingTests, DeathTests, SleepPlacesTests. Read the members named, never whole long files.
Heavy output goes to log files; print at most 30 lines.

Rules: edit only the game projects, their tests and docs/game/; not content/game-v1 unless a per-place number in `needs.json` is the
clean fix (then run `scripts/content-loop.sh --grammar --speech` and put the new hash in the save fixture, as CLAUDE.md says). Tests are
named Given_When_Then in folders that mirror the source; do not edit CLAUDE.md, either implementation-plan.md, either decisions.md or
docs/game/settlement-plan.md; no System.Random and no clock; nothing under .github/workflows/.

Deliverables:
1. **The wake cell is one the player can walk to from the place.** For every sleep place, the death's wake cell is free ground reached
   on foot from the place's cell (a bounded walk, not through walls or closed doors), the nearest such by walking or by distance as you
   judge cleanest. Keep the fire's minimum distance where it serves (not inside a ring); a bed may wake the player at its stand cell or
   beside it. If no such cell exists, fall back as today. Keep the chest rule (`avoid`).
2. **"At the place" stops at walls.** `UsableAt` and `LockedBedAt` (and anything else that asks "is the player at this place")
   answer yes only when the player's cell is within the radius and height *and* reachable on foot from the place's cell within a few
   steps (or by a test of your choice that a wall blocks: say which and why). From outside the inn's walls, no prompt; inside the guest
   room, the prompt as today.
3. **A fire in the open is unchanged:** `r26-death` still wakes at (152, 73, 106), and the prompt at the mountain fire and Luna's ring
   is as before. Capture `r21-skip`, `r26-death` and `r03-npcs` before your first change and after your last and compare them
   (`scripts/scripted-runs.sh`, `scripts/compare-runs.py`); report any difference.
4. **Tests on game-v1's inn** (the real scene, as `InnTests` builds it): a death after a night at `inn.bed` wakes inside the guest room
   (a cell whose path to the bed's stand cell does not leave the inn); from (278, 233), outside the south wall, the player is not at
   `inn.bed`; from (278, 231) they are. Plus a fire-place test that the wake cell is unchanged. Existing tests keep what they assert.
5. **The run above in the game again:** report the wake cell, and put a screenshot in `artifacts/y05a/` (git-ignored).
6. **Cost:** the "at the place" test runs every frame; keep it cheap (a radius test first, the walk only for places that pass it, and a
   small bound). Say what it costs.

Not in this milestone: the innkeeper (Y06), the console's sleep line "ends at the fire" (Y08), the camp (Y12), any scene edit.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count; the feature branch has 10,759 passing and 3 skipped today); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, half a page to a page: branch, commit SHA, worktree path; what changed and why; the walk test you chose and its cost; test
counts and the tests added or changed; the run in the game; the scripted runs' comparison; anything Y06, Y07 or Y12 should know;
known-not-fixed with reasoning. Leave out how you searched.
