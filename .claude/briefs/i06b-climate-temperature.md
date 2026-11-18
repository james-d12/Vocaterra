# Brief: I06b, the world's climate in what the player feels

Sent 2026-11-18 to a general-purpose agent, Sonnet 5.5 at medium effort. Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i06b`, branch `I06b-climate-temperature`, cut from `feature/inventory`. The shared part is
`.claude/briefs/i-series-standard.md`. The owner (2026-11-18, stop 3's choice 5): "Yes, I would kick off an agent to add temperature to the
top of the mountain".

```
You are implementing I06b: the player's temperature gains a term from the world's climate, so high and snowy places feel colder than the
valley. Read .claude/briefs/i-series-standard.md (shared rules), the inventory plan's sections 3.7 and 3.13a (the cold's numbers and
the clothes' warmth), and .claude/briefs/i06-report.md (what I06 built).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i06b, branch `I06b-climate-temperature`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i06b &&`; absolute paths for file tools. Commits prefixed `I06b: `. Do not push.

Today: `Vocaterra.Game.Core/Temperature/` sums terms (`ITemperatureTerm`: base 14, day ±6, weather by channel, shelter, fire), in tenths of
a degree, into `PlaceTemperature`, then warmth from the stat sheet and the bands (`game/temperature.json`: cool from 3, cold from -5 at
walk 85%, very cold below at 65%). The world's generator (the biomes series) has its own climate: `World/Climate/ClimateSampler.cs`,
`ClimateLapse.cs`, world.json's `lapsePer100Blocks`, biomes.json's `surfaceTemperature` and snow caps. The two never meet: a snowy peak
feels like the valley.

What to build:
1. A new `ITemperatureTerm` from the climate at the player's column and height: colder with height above a reference (the valley floor
   or sea level: choose and say) and colder in cold biomes, through what the generator already computes (call it; do not copy its
   formulas). Deterministic (a pure function of the seed, the column and the height), cheap per tick (cache per column if a sample is
   costly; measure), no new simulation state, no save change.
2. Its numbers are data in `game/temperature.json` (and its schema and validator): how many tenths per 100 blocks of height or per unit
   of the climate's temperature, a reference, a floor. Tune so that: the valley and the village feel as today (within half a degree; prove
   it on seed 42 at the village and Luna's camp); the highest peaks on seed 42 at a clear noon are cool, and at a clear night cold to very
   cold; with the warmest clothes (+10, section 3.13a) a clear night on a peak is no worse than cold. Report the feel at five places on
   seed 42 (the camp, the village, a mid slope, a snowy peak, a cave) at noon and at night, with and without +10 warmth.
3. F3 and `--temperature` show the new term like the others.

Tests (Given_When_Then, mirrored folders): the term from height and climate; the valley unchanged within its tolerance; the data's
validation; determinism; the term's order among the others does not matter. No digest, golden or fixture may move unless the term reaches
the state digest (it should not: the temperature is derived each tick; if a digest moves, stop and report why).

Documents: docs/game/architecture.md (the temperature's terms).

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full (the
count on feature/inventory when you start is in your prompt); scripts/test.sh timing (a CPU-budget failure in world code you did not touch
is load from other sessions: report it); format; content validate; the A2 content gate if content changed beyond temperature.json.

No game window. Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with
i06b, at most 30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal.

Report, one page: commits; the term and its data; the feel table; tests; the counts. Leave the worktree clean on your branch with no
process of yours running. Commit messages: subject, blank line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
