# Y12 report: the campsite

(The agent's report, 2026-11-12, condensed by the orchestrator.)

Branch `Y12-campsite`, HEAD `58d38637` (the milestone `66ba3c4a` and a commit for the dev aid's hints); worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/y12`. Everything in the brief is built, packing back included.

## Built
- One camp shape, a `camp` stamp in `game/scene.json` (5 wide, 8 deep): a fire ring, a free row, the tent's canvas rows (a test pins
  them to the `tent` stamp) and a two-cell bedroll inside. Stamps gain optional `anchors` (`fire`, `bed`, `wake`), known to the schema,
  parser and validator (`LandValidator` checks the anchors and that the footprint fits the plot either way round).
- Rotation helper `StampSpec.Turn/Cells/WidthFacing/DepthFacing` (`World/Layout/StampSpec.cs`) for `SceneBuilder.BuildStamp` and the
  placing in play; a test compares every scene stamp on four sides with the old formula; the scene digest unmoved.
- Placing (`Session/VoxelSession.Camp.cs`, `Land/CampPlan.cs`): right click with `camp-kit` selected (`CampKitUse`); the aimed column is
  the ring's middle, the front faces the player. Checks in order: every column owned, the ground at one height, every cell air or
  undergrowth, the player clear, a wake cell reachable out of the footprint (blocks set and put back within one call). Success sets the
  blocks directly, takes the kit, rescans the rings, adds `camp.bed` and `camp.fire`, raises `camp_placed`. A refusal changes nothing.
- Sleep places gain an optional `Wake` cell; a death wakes there while it is free ground; the console says "ends at your camp".
- Fire: a torch on the placed ring lights it (`torch_at_ring` and `fire_lit` with detail `camp.fire`); its sleep place is usable lit;
  cooking through the shared `Fires`; the fire's sound follows the placed ring (`campFire` on `AmbientBeds.At` and its callers).
- Planting on the owned plot as well as at Luna's ring, a separate bush count for each (`PlantedAliveOn`); `plantFar` mentions "your own
  land".
- `my-land` gains `camp` (noun `campamento`, hook `camp-placed`) and `fire` (noun `fuego`, hook `camp-fire-lit`); `campamento` a new
  catalogue noun with its word card. The checklist completes with four steps and ends no session.
- Packing back: holding gather 45 ticks on any camp block (`IBuildRules.IsCampBlock`, `CampPackRequested`) packs the whole camp; a full
  bag refuses with a hint; ring torches go back to the bag, bushes stay; the last place slept falls back to the inn's bed with lodging,
  else Luna's fire.
- Load: a standing camp comes back with its places; a missing block drops the places and gives the kit back once (owed in
  `systems.sleep.kitOwed` if the bag is full, given on the first tick with room, after the digest check). Identity
  `systems.sleep.camp` = `{x, z, ground, facing}`; never rediscovered from `Scene.Points`.
- Dev aid `--place-camp x,z,face` (a script step; `--give equipo` first).

## Lines (`ui.camp*`)
"Put your camp on your own land." / "The ground is not level enough here." / "Something is in the way." / "Stand clear of the camp." /
"There would be no way in or out of the camp there." / "You already have a camp. Hold the gather button on it to pack it up." / "Your
bag is full. Make room for the camp kit to pack the camp up." Success: "Camp set up. Put a torch on the fire ring to light it.", "You
pack the camp up. The camp kit is in your bag.", "Your camp is gone. The camp kit is back in your bag.", `endedAtCamp` "at your camp".

## Gate
Build 0 warnings, format clean, validate and A2 lint clean, bundle rehashed into `save-v8.json`, goldens unmoved. With models 11,457
passed, 3 skipped; without 11,410 passed, 50 skipped. Added `Land/CampPlacementTests`, `CampLifeTests`, `CampInputTests`, `CampRig`, an
`AmbientBedsTests` and a `DevLaunchTests` case; changed `ObjectiveBookTests`, `PlotPurchaseTests` (the checklist no longer completes on
the purchase alone), the noun counts (32 → 33).

## Runs in the game (seed 42, `--dev --skip-to-session 2 --own-plot 261,160`)
`--give equipo --give estaca --place-camp 263,161,north`: placed, fire 263,37,161, bed 263,37,166, wake 263,37,163, `camp_placed`; a
torch on the rim: `torch_at_ring (camp.fire)`, `fire_lit (camp.fire)`, `fuego 1/1`; `--sleep` at the bedroll: "session 2 ends at your
camp"; the save: `last` `camp.bed`, both places with `wake`, `camp` {261, 160, 36, north}. (`terreno` 0/1 because `--own-plot` raises
no `plot_bought`; a load restores it.) Outside the plot (255,160 north; 263,161 east, hanging off the plot): "Put your camp on your own
land.", nothing changed. Packing after a load: canvas gathered in 45 ticks, kit and torch back, last place `inn.bed`, placeable again.
Screenshots in `artifacts/y12-check/` of the settlement worktree. The tent is the stepped canvas shape the `tent` stamp always was.

## Scripted runs (r01, r03, r10, r20, r21, r26, r30; none plants)
r03, r10 the same; r01, r20, r21, r26, r30: speech 832 → 833 lines (the `campamento` card) and the tracker's four «Mi terreno» steps in
pictures; r01's sound noise as before; no save differences.

## Known gaps for Y13
«mi campamento» not answered when asked for directions (no point or label for the placed camp); one plot and one camp; no meals,
comfort or warmth at the camp; the camp is no destination for the dialogue worker or navigation (only sleep, cooking, planting and
sound follow it); planting needs needs on (the `--needs off` runs plant nothing); no meal at the inn.

## Known, not fixed
A body can climb the tent's steps and walk over it (the wake check models it). The wake check sets and restores the blocks within one
call. A full bag repeats the "bag full" hint every 45 ticks while gather is held.
