W04 report (Sonnet 5.5 medium, 2026-11-07). Branch W04-price-lists (local) at 467f272b: 446b52d5 (feature and tests), 467f272b (import order).

Built: NpcEffect.Prices (catalogue noun -> coins), SellList/BuyList, Priced(noun), For(slots, nouns) (Npc/NpcHostEvents.cs); ExchangeMiss
judges a priced entry first: an unlisted item, a noun naming no item or no item in the line gives wont_sell (sell) or wont_buy (buy); a
listed item is judged by the fixed effect, so the other misses keep their order (none_asked, cant_pay, no_room, cant_sell, none_offered).
HostEventTable parses slot and prices beside item and price; InvalidDataException for both forms, a slot other than "item", no/empty/non-object
prices, a price not an integer >= 1. VoxelSession.ApplyHostEvents resolves through For; NpcService's cant_pay check unchanged (it goes
through ExchangeMiss); CanExchange keeps the exchange atomic. SessionSkip's purchase finder understands priced sells (else it matched Wood).
GameDataValidator: every listed key is a catalogue noun naming an item. content/schema/host-events.schema.json: slot (enum item), prices
(object, >= 1 entry, integers >= 1), oneOf item/price or slot/prices. Validate: 0 errors. No content/game-v1 edit.
Tests: PriceListTableTests (priced form, fixed form unchanged, 12 malformed cases, validator rejections, For and ExchangeMiss);
PriceListSessionTests (test bundle: baker sells bread 2, apples 1; smith buys wood 1, berries 3): two items, qty 2, cant_pay, wont_sell,
no_room, priced buy with wont_buy, the test bundle validates. Existing sell and buy tests pass unedited.
Count: 9901/3 -> 9924 passed, 3 skipped, 0 failed. Build and format clean. O7 did not fail.
Scripted runs: all same or order-only (compare-runs.py exit 0), r27-bread and r30 included; no jitter seen. r23-r30's before set was
retaken from 99598b64 after a capture spoiled by a rebuild mid-run. /tmp/claude-1000/w04/runs-{before,after}.
Design vs code: the plan's dont_sell is the existing <npc>.wont_sell (reused); buy uses wont_buy. Schema stays 2. The validator's "slot the
prompt's intents can fill" is only slot == "item" (no prompt-to-slot map exists).
Live use needs: a content milestone writing a priced host-events.json entry; agent states taking wont_sell (and cant_pay, none_asked,
no_room, wont_buy) with lines such as «No vendo …» (the validator demands them); the success event is one per entry, so its line cannot name
the item without a templated slot.
