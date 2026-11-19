# I03a's report (the equipment core)

The agent's report, 2026-11-16 (Sonnet 5.5, high). Commits `64ef3fda`, `09093a37` on `I03a-equipment-core`, gated into
`feature/inventory` at `1627482f` (11,751 passed, 3 skipped).

- **Content:** `game/equipment.json` (six groups: clothing, armour, cosmetic, accessories 3, offhand, backpack), its schema;
  `items.schema.json` gains optional `wear`, `stats`, `look`; provenance; the hash in `save-v9.json`.
- **Items:** `ItemDefinition` gains `Wear`, `Stats` (sorted by key), `Look` (`ItemLook`, `LookColour`); the catalogue refuses a bad wear,
  stat key, stat value (limit ±100000) or colour.
- **`Items/Gear/`** (not `Items/Equipment/`: a namespace of that name would hide the type): `Equipment`, `EquipSlot`, `EquipmentData`,
  `EquipmentGroup`, `EquipmentSlotSpec`, `EquipResult`, `EquipChange`, `IEquipRule` with `WearMatchesRule`, `AcceptsTagRule`,
  `NoDuplicateRule`, `EquipRules.Standard()`, `EquipmentStats` (the first contributor), `EquipmentValidator`.
- **`Stats/`:** `StatSheet`, `StatBuilder`, `IStatContributor`, `StatPipeline`, `StatKeys`. Values are doubles (sum times factor;
  `GetWhole` for whole stats such as warmth).
- **Simulation:** `VoxelSimulation.Equipment.cs` (`Equipment`, `Stats`, `Equip`, `Unequip`, `WearFromStart`); the sheet is rebuilt
  at each tick's end, on equip or unequip and on restore. `StatWarmth` replaces `NoWarmth` in `VoxelSession.Temperature`.
- **Commands:** `Equip = 8` (A bag slot, B group index, C slot index); `Unequip = 9` (A group, B slot, C bag slot or -1 for the first
  free: use the `VoxelCommand.Unequip` factory). Events `ItemEquipped = 18`, `ItemUnequipped = 19` (Detail item id, Amount flat slot),
  `EquipRefused = 20` (Detail reason, Amount flat slot or -1).
- **Reason codes:** Done 0, NoSuchSlot 1, NoSuchBagSlot 2, NothingToEquip 3, NothingWorn 4, NotWearable 5, WrongSlot 6, WrongKind 7,
  Duplicate 8, StackTooLarge 9, BagSlotOccupied 10, BagFull 11; appended, never renumbered.
- **Save:** `VoxelSimulationState.Worn` (null when nothing is worn) round-trips in memory; `SaveGame` does not write it, so worn items are
  lost on save until I03b. The save's shape is unchanged.
- **Determinism:** worn slots join the digest only when something is worn (a test compares a walk with and without the groups; a
  put-on-then-off run returns to the unworn digest; two runs with equips agree at every tick).
- **`--equip <item>`:** a repeatable scripted step (`ScriptStepKind.Equip`, `ScenarioHooks.Equip`): gives the item if absent and wears it
  in the first slot that takes it (an empty one first); a refusal gives nothing and prints the reason. No scripted-step test (the harness
  needs the speech worker); `WearFromStart` and the parsing are tested.
- **Labels:** `cabeza`, `cuerpo`, `pierna`, `pie`, `mano` (off-hand), `espalda` (backpack), all in the lexicon; accessories have none
  (no `accesorio` in the lexicon), left for I04.
- **Existing changed:** `save-v9.json` (hash); `NoLiteralTextTests` (the validator's file allowed). 119 new tests.
- **Process note (orchestrator):** the agent left a shell wait loop on `pgrep -f "content-loop.sh --tier none"` in its worktree, which
  matched itself and never ended (the brief forbids it); the orchestrator killed it and stopped the agent.
