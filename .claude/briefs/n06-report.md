# N06 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `N06-regrowth-gifts`, final commit `537780ea`, pushed, worktree clean. Gated on `feature/adaptive_design` at `468a1b29` (9382 passed, 3 skipped).

- `NeedsData` gains `Regrowth` and `Gifts` (`RegrowRule`, `GiftRule`); optional `regrowth` and `gifts` keys in `game/needs.json` and
  `content/schema/needs.schema.json`: `berry-bush` after a night; `shopkeeper:bread`, `villager:bread`, `guide:water-bottle` per day.
- `GameDataValidator.CheckRenewal` refuses a block that is not gatherable, an unknown item, an NPC not in the bundle or not giving that item,
  a duplicate rule, an unknown `after`/`per`, and an NPC whose agent has no state taking `<npc>.gift_used`.
- `Session/VoxelSession.Renewal.cs`: bushes picked and gifts given are recorded; `RunDayResets` calls `RenewAtNight` (a picked bush still a
  plain bush becomes a berry bush; the lists clear).
- `NpcHostEvents.GiftUsed` (`<npc>.gift_used`), answered by `NpcEffect.GiftMiss`; `GuideScene.ClosedGifts` reaches the NPC worker;
  `ApplyHostEvents` guards the same rule. The three agents take `gift_used`. No new Spanish.
- Save: `systems.bushes` (`BushSave`) and `systems.gifts`, omitted when empty; version stays 6; `docs/game/saves.md` updated.
- Tests: `Needs/RenewalTests.cs`, 19 tests. `SleepTests` regrows the camp's bushes in the loaded copy before comparing.
- Values chosen: both rules follow `--needs off`; a lesson-required gift (the active lesson's current `do` step is `item_given` at that NPC)
  is neither refused nor counted; the key is NPC plus item; the stall's bottle is not a daily gift; the used-gift lines are existing ones
  (stall `shop_price`, villager `villager_bye`, guide `guide_offer_no`). Wanted later: «Hoy no tengo más pan.», «Hoy ya no tengo agua.»
- Design against code: Luna is the `guide` agent, her water the one-time transfer offer; no shipped lesson has an `item_given` step at these
  NPCs; a regrown bush is the generated block again (digest equals the unpicked world); a bush picked in a pre-N06 save stays picked; the
  host's `ConversationSession` has no days and keeps the old rule.
- Scripted runs: all 25 identical to the baseline. Save fixtures v1 and v4 refreshed for the bundle hash.
- Counts: before 9363 passed, after 9382 passed, 3 skipped; without models 9340 passed, 45 skipped. Wall-clock budgets at 4x not checked.
- Gate (agent's): build clean, format clean, validate clean, A2 lint clean, grammar 0 new findings (cache unchanged, 1349 entries).
