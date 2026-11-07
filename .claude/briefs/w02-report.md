W02 report (Haiku 5.5, 2026-11-07). Branch W02-tools-materials (local), final SHA ef1dfbb5 (8f91d985 Material, ef1dfbb5 ToolSpec), on 54e6625c.

Built: `enum Material : byte { None, Wood, Plant, Stone, Soil, Water }` in Voxel/BlockDefinition.cs, an optional last parameter
`Material Material = Material.None` on BlockDefinition; `record ToolSpec(Material Class, int Speed, int Yield = 1)` and
`ItemDefinition.Tool` (axe `(Wood, 4)`) in Items/ItemDefinition.cs; VoxelSimulation.GatherTicksOf divides by the held tool's speed when its
class matches a gatherable block's material (floor 1); AxeSpeedUp removed. VoxelLayers.cs: comment only (the bar already reads GatherTicksOf).

Materials: Wood: trunks (oak, spruce, birch), planks, timber, table, door, crate, chest, bridge, fence, stall, roof, signs. Plant: leaves (3),
bush, berry bush, tall grass, fern, reeds, yellow and white flower, wheat. Stone: stone, cobble, mossy stone, fire ring. Soil: grass, dirt, sand,
path, farmland. Water: water. None: air, snow, props, lanterns, lamps, awnings, canvas, apples, loaves, bottles and the rest. Only trunks (60)
and the berry bush (45) are gatherable, so no gather time changes.

Tests (GatheringAndPlacingTests): trunk 15 with the axe, 60 by hand, progress event 15; hands on a berry bush 45 with the event's total; material
and tool declarations; the axe on a berry bush and on stone changes nothing; the existing "axe does not hurry what is no trunk" kept.

Count: 9852/3 before; after 9854 passed, 3 skipped, 1 failed. The failure is IntentResolverFuzzTests (O7), CsCheck seed 8eu5g1sTe_A8, which
fails the same way on an untouched 54e6625c: «ayuda por favor por favor» at a prompt allowing request_item but not ask_help; the analyzer matched
request_item's '[ART]? [NUM:qty]? [NOUN:item] [ADJ]? por favor', which ask_help's "ayuda (por favor)?" outranks, but the resolution is Grammar
'request_item'. Pre-existing core issue; not patched. Log: /tmp/claude-1000/orch/fuzz-8eu5g1sTe_A8.log.

Gate (its own): build -warnaserror clean; format clean; content validate passes; no content edit.

Scripted runs (27, baseline from 54e6625c): 26 identical (order-only differences in 13); r30 identical. r27-bread's first baseline capture was
at tick 2461 for the first shot, every later capture (3 on W02, a rebuilt 54e6625c) at 2460 and identical with the path masked, PNGs included:
an unexplained one-off in the baseline, not from W02.

Design vs code: material is an optional last record parameter rather than positional; ToolSpec.Yield recorded, unused; the material list
goes beyond the paragraph's two examples. Left undone: Yield's effect, durability, new tools; the fuzz failure (a core milestone).
