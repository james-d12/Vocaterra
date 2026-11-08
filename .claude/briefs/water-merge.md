You are merging `main` (PR #45, the water physics series, `feature/water`) into the bread pilot's branch in the Vocaterra repository
(C#, .NET 10, MonoGame). Do the work yourself; do not spawn subagents. Commit prefix for any fix-up commit: `Water merge: `.

## Where you work
- Worktree `/home/user/Dev/Vocaterra/.claude/worktrees/t9`, branch `water-merge`, created from `feature/bread-pilot` at `45499631`
  (it already has bin/ and obj/ from an earlier build). Never switch branch, never push, never touch `main` or `feature/bread-pilot`.
- Merge exactly `22caa388` (`origin/main`, "Merge pull request #45 from james-d12/feature/water"): `git merge --no-ff 22caa388`.
  The commit message: `Merge main (PR #45, the water series) into feature/bread-pilot`. Commit the resolved merge, then commit each later
  fix separately (often), so a crash loses little.

## The conflicts (17, from a trial merge)
`docs/handovers/README.md`, `scripts/limited.sh`, `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`, `save-v4.json`, and in code:
`Game.Core`: `Physics/PlayerBody.cs`, `Save/SaveDocument.cs`, `Save/SaveGame.cs`, `Session/VoxelSession.cs`, `Simulation/VoxelSimulation.cs`,
`Sound/SoundCue.cs`, `Sound/SoundSynth.cs`, `World/Generation/SceneBuilder.cs`, `World/Scene.cs`; `Game`: `Startup/LaunchOptions.cs`,
`UI/Common/LayerStack.cs`, `UI/Hud/HotbarLayer.cs`, `VoxelGame.cs`. Expect also non-conflicting semantic breaks (build errors, tests).

## How to resolve (read first)
- **The water plan** `docs/game/water-plan.md` (on `main`; `git show 22caa388:docs/game/water-plan.md`), section 3 "What
  `feature/bread-pilot` touches", its table "Hooks in files both branches touch": the resolution for each shared file is written there.
  Follow it. Sections 4.1 to 4.11 explain the water design; section 5's H11 row is the follow-up milestone.
- For each conflicted file, read both sides' history: `git log --oneline 4c93aa6c..45499631 -- <file>` (ours) and
  `git log --oneline 4c93aa6c..22caa388 -- <file>` (theirs), and the commits' messages. The rule is **keep both behaviours**: nothing
  either side built may be lost or changed. The bread pilot's design: `docs/game/activities-plan.md`, `docs/game/village-plan.md`.
- **Save version:** both sides bumped `SaveGame.CurrentVersion` 5 → 6 for different fields (water: `Air`, `Flow` on the document; ours:
  `Systems`, and `SaveSettings` fields). Per the water plan: take the higher number plus one, so **version 7** holding all of them; no
  migration code for the two different version-6 shapes (the game is unreleased), but a version-5-or-earlier save must still load as it
  does on each side today. Update the docs comments' version numbers, and the fixtures and tests that pin the version, so each fixture
  tests what it tested before.
- **Do only the merge**, plus the small mechanical parts the conflicts force: pass bread-pilot's `speedPermille` through to the swim step;
  `BreathHudLayer` stacked just above the needs HUD in `LayerStack`; `SoundCue` keeps both appended cues; launch options keep both sets.
  **Do not build H11's new behaviour** (drowning damaging `PlayerNeeds` through an `IPlayerHealth` adapter, the `player_drowned` hint, the
  blur row in the settings screen, `UnderwaterBlur` in `SaveSettings`): that is the follow-up milestone. If a conflict cannot be resolved
  without part of H11, do the least that compiles and keeps both sides' tests green, and say exactly what in the report.
- `scripts/limited.sh`: keep **both**: ours (`e0f5e974`, two heavy slots inside one capped slice, 8 GB and 14 cores together, peak memory
  printed) and theirs (`e71af2a2`, H08a: a named scope so an OOM kill inside it is detected and reported as exit 137). Read both commits
  in full; the result must still be re-entrant and keep the slot lock. Test it by hand with a tiny command (`scripts/limited.sh true`, and
  a nested call) before any build.
- `docs/handovers/README.md`: keep both index rows (the open handovers are ours `2026-11-07-1345-bread-pilot.md`, the adaptation's
  `2026-11-05-2031-adaptive-design.md`, and water's `2026-11-07-1846-water.md`; open first, then done). Do not edit any other handover.
- Do not edit the plans or decision registers beyond what the merge brings in as is; no edits to `.github/workflows/`; no core change
  (`src/Vocaterra.Nlp*`); `content/game-v1` is untouched by `main`, so no re-hash, and the save fixtures keep our bundle hash.

## Machine rules (mandatory; the desktop crashed from overload before)
- Prefix .NET commands with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`. Every heavy command goes through
  `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/test.sh` and `scripts/content-loop.sh` call it themselves. Use
  `scripts/test.sh fast` while you iterate and `scripts/test.sh full` once at the end. Never raise a cap. Logs under
  `/tmp/claude-1000/water-merge/`.
- The sandbox refuses command lines containing the words `eval` or `hash`; run such a command from a wrapper script in your log folder.

## Checks before you report
1. `dotnet build Vocaterra.sln -warnaserror` clean; `dotnet format Vocaterra.sln --verify-no-changes` exit 0.
2. `scripts/test.sh full` with `VOCATERRA_MODELS_DIR=~/.local/share/vocaterra/models`: every assembly reports, 0 failed. Give the counts;
   the sides before: ours 9941 passed and 3 skipped at `45499631` (M83's report), water's 9203 at `8fb248eb`. Name any test you changed
   and why; never weaken a test or an oracle to pass (the fuzz suite is a gate).
3. `scripts/content.sh validate` exit 0; the A2 lint (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level
   --host-pools 'guide.*'`) clean.
4. Scripted runs (`scripts/scripted-runs.sh`, `scripts/compare-runs.py`): run the bread pilot's set on this branch before the merge (at
   `45499631`, a clean build) and after, and compare: all identical except the known `r27-bread` capture tick (2460 or 2461) and anything
   water explains (a scene with water now flowing: say which and why). Then run the water series' tour or its scripted run (the lake: walk
   in, dive, surface, swim to the bank, hop out; water plan section 8) and confirm swimming, breath and the bubbles still work beside the
   needs HUD (one screenshot path).

## Report (short; the orchestrator reads it)
Final SHA, worktree clean or not; each conflicted file in one line (how resolved); any non-conflict break fixed; the save version and
fixtures; limited.sh's result; the check results with numbers and log paths; anything of H11 you had to do; anything you could not resolve.
