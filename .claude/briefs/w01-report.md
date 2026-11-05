# W01 report (Haiku 5.5, 2026-11-05), saved by the orchestrator

Branch `W01-item-crafted`, final commit `267f4a1d`, pushed, clean. Gated on `feature/adaptive_design` at `56f88b4b` (9385 passed, 3 skipped).

- `Event/WorldEvent.cs`: `WorldEvents.ItemCrafted = "item_crafted"`, in `All`.
- `Session/VoxelSession.cs`: the `ItemCrafted` case logs the world event with `Nouns.ForItem(item)?.Lemma` (falling back to the item name) and
  `_eventAmount = e.Amount`, after `LearnItem`.
- `content/schema/{lesson,tips,objective}.schema.json`: detail description text only (no event enum exists). No game-v1 edit, no Spanish.
- `Session/ItemCraftedTests.cs`, 3 tests: the torch craft's event; an objective step on `item_crafted/antorcha`; `Trigger.Parse`.
- Scripted runs: r01-route, r10-playtest, r20-sleep each gained the one event line and were re-baselined; the others identical.
- Not covered: a lesson `do` step on it (no shipped lesson has one; the trigger is shared). No save change.
- Counts: 9382 → 9385 passed, 3 skipped; CI's case passes. Build, format, validate, A2 lint clean.
