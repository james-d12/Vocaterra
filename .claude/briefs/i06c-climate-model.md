# Brief: I06c, the temperature as a climate model

Sent 2026-11-18 to a general-purpose agent, Sonnet 5.5 at high effort (design-heavy). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i06c`, branch `I06c-climate-model`, cut from `feature/inventory` at `92eb5fc5`. The shared
part is `.claude/briefs/i-series-standard.md`.

The owner (2026-11-18): "really we need some kind of dynamic system where certain biomes have temperatures, altitude, and then, you know,
allow it to be influenced by the weather and potentially future systems. So the system needs to be modular and extendable." The
orchestrator's review found five gaps (below); the owner said "I06c can start".

```
You are implementing I06c: the player's temperature becomes a climate model: the biome sets the baseline (warm and cold), height lowers
it, weather and the day act according to the place, caves settle to a steady temperature, heat matters as cold does, and new terms are
added by data and one class. Read .claude/briefs/i-series-standard.md (shared rules), the inventory plan's section 3.7 and 3.13a (the
clothes' warmth: +4 starting, +10 the warmest), .claude/briefs/i06-report.md and i06b-report.md, and the code in
src/Vocaterra.Game.Core/Temperature/ and the generator's climate (World/Climate/, World/Biomes/, game/world.json, game/biomes.json).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i06c, branch `I06c-climate-model`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i06c &&`; absolute paths for file tools. Commits prefixed `I06c: `, one per step that
builds and passes. Do not push.

Today: `PlaceTemperature` sums `ITemperatureTerm`s (`TemperatureTerms.Standard`, a hard-coded list): base +14 everywhere, day ±6, weather
by channel (cloud, precipitation, wind), climate (I06b: only cools, by the generator's `IPlaceInfo.TemperatureAt`, at most 12), shelter in
rain, fire. `TemperatureSituation` is a fixed record every term receives. `PlayerTemperature` adds warmth (`IWarmthSource`) and picks a
band (`temperature.json`: cool, cold walk 85%, very cold walk 65%; nothing for heat). The value is instant.

The gaps to close:
1. **The biome is the baseline.** Replace the constant base and the cooling-only climate term with one climate baseline: the generator's
   temperature at the player's column and height, mapped to degrees (warm places warm, cold places cold), through the generator's own
   call (no copied formula). Per-biome adjustments are data: a biome row (in temperature.json keyed by biome id, or in biomes.json; say why)
   may add an offset and scale the day's swing (a desert swings more) and the weather's effect. Code never names a biome id (a test like the
   weather kinds' enforces it).
2. **Weather and the day by place.** The day's swing and each weather channel scale by the place's data (above); precipitation above the
   snow line (world.json's snow threshold, through the generator) cools more (snow), as data.
3. **Underground.** Below the surface the temperature eases from the surface value toward a steady ground temperature with depth (data:
   the ground temperature and the depth over which it takes over); the day and the weather fade out underground. Use the generator's
   surface height or the sky exposure the game already computes (say which); the lapse alone must no longer make caves warmer.
4. **Heat.** Bands above warm as data (for example hot and very hot), each with its effects as data: the walk factor and a thirst drain
   factor through the stat consumers I05a built (find how hunger and thirst factors apply; a band's factor multiplies them), and a line
   like «Tengo frío.» («Tengo calor.»; A1, through the same `hudLines` pattern, with speech). Propose numbers and list them: the owner
   reviews them at stop 2.
5. **Modular terms.** Terms are listed in temperature.json (id plus parameters); a small registry maps each id to a factory, so a future
   system is one `ITemperatureTerm` class, one registry line and a data row; an unknown id is a validation error. Inputs: replace the fixed
   record with an interface or context that terms read what they need from (the atmosphere, the position, the world's climate, the fires,
   the shelter), so a new input does not change every term. The F3 breakdown keeps showing each term by name.
6. **Lag.** What the player feels moves toward the place's value over time (a rate in data, per tick, deterministic; `--temperature` and
   the tests may bypass it). It must not reach the save unless it must (if it does, say so; the game is unreleased: no migration).

Must hold: the valley, the village and Luna's camp feel as today within half a degree at noon and at night on seed 42 (the clothes' numbers
were set against them); the snowy peak stays very cold at a clear night and cool with +10 warmth; determinism; no simulation digest change
unless the lag is state (then name it). Report a feel table on seed 42: the camp, the village, a mid slope, the highest peak, a real cave
(find one: the biomes series' caves and entrances), the warmest and the coldest biome you find, at noon and at night, clear and in rain,
with and without +10 warmth.

Tests (Given_When_Then, mirrored folders): each term; the registry and the data's validation (unknown id, missing parameters); a fourth
term added from data with a test term; biome adjustments from data with no biome id in src/; the snow cooling; the underground easing; the
heat bands and their effects; the lag's approach and its determinism; the valley's values unchanged.

Content: temperature.json and its schema (and biomes.json if chosen); the heat line through the content loop with `--grammar --speech`;
`scripts/rehash-bundle.sh`. The I08a and I08b agents edit other content files: before your gate merge `feature/inventory` into your
branch (a manifest conflict: take either side, run rehash-bundle.sh); I08a adds starting clothes (+4 warmth) that some temperature tests
take off with `GearItems.Undress`.

Documents: docs/game/architecture.md (the temperature model: its terms, the registry, how to add a term, how to tune a biome).

No game window. Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings);
scripts/test.sh full (the count is in your prompt); scripts/test.sh timing (a CPU-budget failure in world code you did not touch is load:
report it); format; content validate; the A2 content gate.

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i06c, at most
30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal.

Report, one page: commits; the model (terms, registry, inputs, data); the biome rows and why there; the heat bands and their numbers; the
lag; the feel table; tests added and changed; the counts. Leave the worktree clean on your branch with no process of yours running. Commit
messages: subject, blank line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
