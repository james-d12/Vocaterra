W04b report (Sonnet 5.5 medium, 2026-11-08). Branch W04b-stall-prices (local) at d50c3f56 from be7cf972; worktree clean.
Data: stall.sell_flour is a priced sell ("slot": "item", "prices": { "harina": 1 }); manifest rehashed; save-v1/v4 fixtures take the new
bundle hash. W10's instead harina -> sell_flour kept (the only route from stall.give_water to the sale); pan -> give_bread kept;
buy_bread, agent states and dialogue untouched. Code: NpcHostEvents.ExchangeMiss for a gift with Asked treats an item word the noun
catalogue does not know as wont_sell («Quiero una espada.» got the bottle before); the villager's bread gift (same path) answers such a
word with villager_wont_sell too. Spanish unchanged (shop_wont_sell «No tengo eso. Tengo agua, pan y harina.»); no recordings (0 of 1382).
CLI at stall-need: «quiero una espada» request_item espada; «quiero manzanas» request_item manzana; «¿tienes manzanas?» and «¿Tienes una
espada?» read as state_have (a «Sí, es verdad.» line): a core gap, not patched. Tests in BreadPilotTests: flour by name with qty and too
few coins; unlisted espada, manzanas, madera -> wont_sell with nothing changed; water and bread free. 10363 -> 10368 passed, 3 skipped.
Content loop --grammar --speech --tier full passed; goldens unmoved; build, format, validate clean. Scripted runs: all the same.
Orchestrator's spot-check: «quiero comida» now gets wont_sell (was the bottle); «quiero algo de beber» and «quiero una botella de agua»
are unknown in the core (an older core gap).
