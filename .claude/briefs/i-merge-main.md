# Brief: I-merge, main (the biomes series, PR #69) into feature/inventory

Sent 2026-11-18 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i-merge-main`, branch `I-merge-main`, cut from `feature/inventory` at `9c2ca67b`. The owner
(2026-11-18): "we have merged feature/biomes into main, so first, we need to merge and resolve conflicts". The shared part is
`.claude/briefs/i-series-standard.md`; this brief overrides it where they differ (the documents it lets you edit, the window rule).

```
You are implementing I-merge: merge `origin/main` (472 commits: the biomes series, PR #69, and what main had before) into the inventory
series and resolve every conflict so that both sides' behaviour survives. Read .claude/briefs/i-series-standard.md first; its rules hold
except where this brief says otherwise.

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i-merge-main, branch `I-merge-main` (already created from feature/inventory). Start
every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/i-merge-main &&` and use absolute paths for file tools; never edit
another checkout. `git fetch origin`, then `git merge origin/main` (no rebase, no squash, no history rewriting). Commit the merge once
its conflicts are resolved and the solution builds (`I-merge: Merge origin/main (the biomes series, PR #69) into the inventory series`,
the body listing each conflicted file and how it was resolved); later fixes are separate commits prefixed `I-merge: `. Do not push.

The machine: the biomes work is finished and the owner is not gaming, so the VOCATERRA_HEAVY_SLOTS=1 prefix is not needed (limited.sh
still, one heavy command at a time). No game window is needed for this milestone; open none.

A trial merge conflicts in 37 files: CLAUDE.md, content/README.md, content/game-v1/manifest.json, content/locale/manifest.json,
docs/game/{README,architecture,decisions,findings,implementation-plan,saves}.md, docs/handovers/README.md, docs/open-items.md,
scripts/content-loop.sh, src/Vocaterra.Game.Core/{Content/GameData.cs, README.md, Save/SaveDocument.cs, Save/SaveGame.cs,
Scripted/ScenarioHooks.cs, Session/VoxelSession.cs, Simulation/VoxelSimulation.cs}, src/Vocaterra.Game/{Graphics/World/VoxelRenderer.cs,
Startup/LaunchOptions.cs, VoxelGame.cs}, the fixture save-v9.json, about a dozen save and session test files, and
tests/Vocaterra.Game.Tests/Vocaterra.Game.Tests.csproj.

What each side did:
- **Inventory (this branch, I00 to I04; reports in .claude/briefs/i01-report.md ... i04-report.md):** items as data (`items.json`,
  `IItemCatalogue`), temperature (`temperature.json`, `PlaceTemperature`), equipment (`equipment.json`, `Items/Gear/`, `Stats/`,
  `Damage/`), the bag with a worn backpack and active slot count, `Items/Containers/` and `VoxelCommandKind.SlotMove = 10`,
  `SimulationEventKind.MoveRefused = 21`, the equipment panel (`UI/Inventory/`), gear drawn on the player (`PlayerGear`). Save version
  10 (`SaveGame.CurrentVersion = 10`): items by name, the active slots, `Worn` (`Save/WornSave.cs`); the fixture `save-v10.json` replaced
  `save-v9.json`. New launch options `--temperature`, `--equip`, `--move`.
- **Main (biomes, plan docs/game/biomes-plan.md on origin/main, its last handover in docs/handovers/ there):** biomes, rivers, lakes,
  caves, generator version 4, the far view, the east pass (`PassOpen` on the save, optional, no version), `WorldDims` (check which side
  it really belongs to), ambient sound beds, new launch options and script steps, many re-recorded pins (procedural goldens, the
  atmosphere pins, save-v9.json's bundle hash and state digest). Main stays at save version 9.

Rules for the meeting points:
- The save is version 10 (this branch's), with main's optional fields (`PassOpen` and anything else main added) kept on
  `SaveDocument` with their write conditions. No migration code. Keep the oldest readable version as this branch has it.
- Fixtures: `save-v10.json` is the fixture; carry into it what main's commits changed in `save-v9.json` (its digest, any new field)
  and delete `save-v9.json` (it was renamed on this side). Tests main changed that point at save-v9 point at save-v10.
- Code (GameData, SaveGame, ScenarioHooks, VoxelSession, VoxelSimulation, VoxelRenderer, LaunchOptions, VoxelGame and the rest): keep
  every feature of both sides: both sides' data loads in GameData, both sides' launch options and help text, both sides' script steps,
  both sides' event and command numbers (if a number collides, the side already on main keeps it and this side's moves; say so). Keep
  both sides' tests; where one side's test assumed the other's absence (a count of options, of nouns, of item rows, a digest), change
  it to the merged state and list it with the reason.
- The bundle: resolve content/game-v1 as a union; then rehash (`scripts/content.sh hash content/game-v1 --bundle-id game-v1`
  through a wrapper script in your scratchpad), validate, the A2 content gate (`scripts/content.sh lint content/game-v1 --strict --level
  a2 --fail-on-level --host-pools 'guide.*'`) and `scripts/content-loop.sh --grammar --speech` (render nothing that exists). The new
  bundle hash into `save-v10.json`; the state digest re-recorded only if the merged world moved it (say from which side). Record the
  end-to-end and procedural goldens (`VOCATERRA_RECORD_GOLDEN=1`) only where the merge moved them, and say which and why: this side
  changed item rows and nouns, main changed the world.
- Documents: you may edit CLAUDE.md, docs/game/{README,architecture,decisions,findings,implementation-plan,saves}.md,
  docs/handovers/README.md, docs/open-items.md, content/README.md and src/Vocaterra.Game.Core/README.md only to resolve the merge: a
  union keeping both sides' entries in order (both open handovers or main's moved to done/ as main has it, D-game-35 from here and
  main's decisions, both sides' plan rows and findings). saves.md describes one save: version 10 with main's optional fields. Change
  no decision's wording. Leave docs/game/inventory-plan.md and .claude/briefs/ alone.
- scripts/content-loop.sh: keep both sides' changes; it is outside the game's area, so name it in the report.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report passed, failed, skipped, and compare with the two sides: feature/inventory after I04 is 12,102 passed, 3 skipped; main's count
is in its last biomes handover or report, else say you did not find it); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; the A2 content gate clean. Do not commit bin/, obj/,
artifacts/ or logs; name your logs with `i-merge` in your scratchpad.

Report, one page: the merge commit and later commits; each conflicted file and its resolution in a line; number collisions and how
settled; every test, golden, digest and fixture changed and why; the counts; files touched outside the game's area; anything left
undone. Leave the worktree clean on your branch with no process of yours running.
```
