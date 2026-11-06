N08 (a berry planted at the camp) is done, committed on branch N08-camp-berry in the n08 worktree, not pushed. Worktree clean. Final commit 45417644 (local). Commits 312995d5, 71475853, 6cb8fd0e, e5d16bd5, 45417644.

Deliverables
1. Rule in data: optional `planting` in content/game-v1/game/needs.json {item berry, block bush, grownBlock berry-bush, radiusBlocks 8, maxBushes 6}; schema in content/schema/needs.schema.json; `PlantingRule` record and parse in Game.Core/Needs/NeedsData.cs; `CheckPlanting` in Content/GameDataValidator.cs (unknown item/block, grown block with no regrowth rule, radius or count <= 0). Tests: Game.Core.Tests/Needs/PlantingTests.cs.
2. Action: Game.Core/Session/VoxelSession.Planting.cs (`PlantOn`, `PlantResult` NotApplied/Planted/Refused, `PlantedAlive`, `PlantedBushes`), Game.Core/Input/PlantingUse.cs, wired in Game/Input/WorldInput.cs and VoxelGame.cs. Input split: a right click with the berry held and the crosshair on grass or dirt plants; a click in the air (or on any other block) still eats as before. Outside the radius, on an occupied cell, or at the cap: a toast (content/locale/en/ui.json keys plantedBush, plantFar, plantOccupied, plantFull), berry kept. No berry or needs off: NotApplied, click falls through to eating. Event `bush_planted` (detail `fruta`) added to WorldEvents.All.
3. Night and saves: the planted cell is recorded as picked, so N06's RenewAtNight grows it. The picked list needed no change, but the cap needs to know which bushes are planted, so `systems.planted` (optional BushSave[]) is a new field; save version stays 6; absent field loads as none. Tests: round trip plus a night after load, and a save without the field.
4. Test aid: scripted `--plant x,y,z` (ground cell), in ScriptStepKind/ScenarioScript/LaunchOptions (args list and help)/VoxelGame. docs/game/running.md (one line) and docs/game/saves.md updated.

Values chosen
- Input: right click on grass or dirt plants; air eats. Berry on other blocks still eats.
- Radius: horizontal, squared distance <= 8^2 from the `firering` point (the guide camp's ring), inclusive.
- Free cell: air or undergrowth (decor), as placement does.
- Cap counts planted cells that still hold bush or berry-bush.
- Validator adds one check beyond the brief: the planted block must be what the grown block regrows from (bush).
- Toast wording: "You plant the berry. It bears fruit after a night." and three refusals.

Design vs code
- The camp ring (`firering`) is the guide's camp; `--sleep` needs the player's own fire (`firering.player`). Planting and the night are at different fires, so a behaviour check must go to the player's fire to sleep. Noted, not changed.

Behaviour checks (--needs on, --dev, seed 42)
- `plant: Planted at 170,42,186, berries 0, planted 1` / `event: world bush_planted (fruta)`
- `sleep: --sleep: session 1 ends at the fire, session 2 begins`
- `interact 170,43,186: gathered berry-bush in 45 ticks (now bush, +1 berry)`
- Screenshots: /tmp/claude-1000/n08/check-b/before.png (plain bush, toast "You plant the berry") and after.png (after the night, the bush carries berries). The difference is small at that distance.

Scripted runs
- Baseline built from base commit 41191a57 (/tmp/claude-1000/n08/runs-base); after = /tmp/claude-1000/n08/runs-after.
- 25 of 26 runs identical. r30-no-classes: only declined.png bytes differ. A second run of the same edited binary (runs-after2) also differed from runs-after, so the difference is timing noise, not this change.

Gate
- Build Vocaterra.sln -warnaserror: 0 warnings.
- scripts/test.sh full: 9516 passed, 0 failed, 3 skipped (was 9501 + 3). Game.Core 2332.
- No-models full suite (CI's case): 9474 passed, 0 failed, 45 skipped. No wall-clock budget warnings.
- dotnet format --verify-no-changes: clean.
- content.sh validate: clean. Lint at A2: 0 errors, 0 warnings.
- content-loop --speech --tier none run after each content edit; fixtures save-v1 and save-v4 hash updated by the loop; no Spanish added, no grammar change.

Not done
- No shelter (out of scope). Core unchanged; no core gap found.
- The `--plant` launch option's help line edits LaunchOptions.cs, which is inside the game's files.