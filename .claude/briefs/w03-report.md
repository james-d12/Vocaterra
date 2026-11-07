W03 report (Sonnet 5.5 medium, 2026-11-07). Branch W03-fail-forward-data (local) at 5c177b2f: 6001360b (schema, loader, shopkeeper's
values), 5c177b2f (NpcService reads the rule; validator; tests).

Built: NpcPatienceRule(Misses, AfterHelp, Event, Prompt, Once) (Npc/NpcPatienceRule.cs); NpcEntry.Patience loaded by NpcRoster (PatienceOf);
content/schema/npc.schema.json optional `patience` (schemaVersion stays 2: optional and additive). NpcService keeps one rule per NPC;
ShopkeeperPatience became FailForward(rule, ...) for any NPC at the rule's prompt, not off-prompt; StallPatience, StallPatienceAfterHelp and
the shopkeeper-only branch gone. On firing: raises rule.Event; Once sets the saved BottleGiven; counters reset (as Note did).
shopkeeper.json: "patience": { "misses": 4, "afterHelp": 2, "event": "shopkeeper.give", "prompt": "stall-need", "once": true }.
Validator: misses, afterHelp >= 1; afterHelp <= misses; the event is taken by a state of the NPC's agent (onHostEvent); the prompt is one of
the NPC's prompts.

Tests (NpcPatienceTests; the stall's tests untouched and pass): shipped rule matches data, no other NPC has one; custom Misses; AfterHelp only
after help; no rule never fails forward; a rule at another prompt; Once true blocks a second firing, false allows it; a save keeps
BottleGiven and saves nothing of the rule; one validator test per rejection.

Scripted runs: 28 unchanged apart from the masked bundle hash and ordering, except r27-bread's first-capture tick: 2461 before, 2460 after,
2461 on a rerun of the after build (the jitter W02 saw; not W03's). Captures /tmp/claude-1000/w03/runs-{before,after,after2}.
Re-hash: manifest re-hashed; save-v1.json and save-v4.json bundle hash 09728e2e... -> 2f3acdba...; goldens, identity, digests unmoved.
Count: 9855/3 before; after content-loop --speech --tier full 9862 passed, 1 failed, 3 skipped: GeneratedLexiconTests.LoadingTheBundleIsFastEnough
(3 s bar, took 9 s while the gate's tests ran beside it; 1 s alone). O7 did not fail.
Design vs code: the event is shopkeeper.give (the agent's input), not stall.give_water (the world event its reply makes), so "exists in
host-events.json" became "taken by a state's onHostEvent"; schema stays 2; the rule names one prompt; once maps onto BottleGiven.
NpcService still uses ShopkeeperGive and BottleGiven for the revisit logic (outside W03).
