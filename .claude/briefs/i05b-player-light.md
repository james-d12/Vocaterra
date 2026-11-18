# Brief: I05b, light round the player (a spike)

Sent 2026-11-18 to a general-purpose agent, Sonnet 5.5 at high effort (the plan's section 4). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i05b`, branch `I05b-player-light`, cut from `feature/inventory` at `0e492fba` (after I04b).
The shared part is `.claude/briefs/i-series-standard.md`. The owner (2026-11-18): "you can run that now to check the light". Game windows
are allowed for this milestone.

```
You are implementing I05b, a spike: light round the player at night from a worn item's `light` stat (the inventory plan's section 3.8,
the stats table in 3.5, the risk in section 7; D-game-35). Read .claude/briefs/i-series-standard.md (shared rules; this brief overrides its
window rule: you may open game windows for screenshots and frame-time runs, one at a time, through limited.sh), then the plan's sections
3.5, 3.8 and 7, and .claude/briefs/i03a-report.md, i05a-report.md and i09-report.md (stats, their consumers, the off-hand item drawn).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i05b, branch `I05b-player-light`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i05b &&`; absolute paths for file tools. Commits prefixed `I05b: `. Do not push.

What exists: the stats (`Stats/`: `StatKeys`, `StatRules`, `StatSheet`, contributors from worn items); the off-hand item drawn in both
views (I09). Since the plan was written, main's series added world light: `LightMap` in the simulation (integer levels 0 to 16: sky
columns, a placed torch 16 losing 2 a step), baked into chunk vertices (`ChunkMesher`, `ChunkLightIndex`, `SkyDepthLight`), lanterns'
additive glow (E02a), `ArmLight` for a held torch in first person, and the day's light (`day.json`, the atmosphere). Read these first and
say in the report how the world is lit today.

What to build:
1. The `light` stat: a key in `StatKeys` and a rule in `StatRules` (a sum, clamped to a sane range you state), contributed by any worn
   item's `stats.light` (the off-hand and accessories alike; nothing names an item or a group). Core, tested.
2. The light at the player, drawn only (no simulation state, no digest change, no save change): when the sheet's `light` is above zero
   and the place is dark (night by the day's light, or underground by the light map; say which signal you use), the world round the player
   is lit with a strength and radius from the stat. Do not rebuild chunk meshes every frame and do not add the player to `LightMap` (it is
   simulation state). The plan's first try: a term on the effects the world is drawn with (BasicEffect / AlphaTestEffect parameters, e.g.
   fog or a per-vertex term the shader already has; no custom .fx); else a small additive glow mesh at the player like the lanterns'
   (E02a). Pick by what looks right and costs least, and say why. The radius and strength per point of `light` are data
   (`game/` json with its schema, or the stat rule's row), not constants in code. Engine code only in Vocaterra.Game.
3. Measure: frame time on the low preset (`--quality low`, 1280x720) at night in the valley and in a cave, with and without the light,
   median and 95th percentile over the same frames (find how the game prints frame times; the S13 demos in docs/game/running.md print
   them). The machine is shared with other sessions: run each pair back to back and say the load (`uptime`) at the time.
4. Screenshots (stop 5): night in the open and underground, without the light, with a weak light and with a strong one, first person and
   third person (`--third-person`). Nothing shipped has a `light` stat yet: make a test-only copy of content/game-v1 and content/locale in
   your scratchpad (side by side), add two test items (an off-hand lantern with `wear` kind `offhand` and a strong light, an accessory with
   a weak light; borrow existing icon keys), rehash the copy with `scripts/content.sh hash <copy> --bundle-id game-v1` through a wrapper
   script, and run the game with `--content <copy>`, `--equip <name>`, `--time 1`, `--seed 42 --mute --no-autosave --quality low --frames
   <n> --screenshot <png>` (check the options in LaunchOptions.cs). Leave the PNGs in your scratchpad and list their paths.

Tests (Given_When_Then, mirrored folders): the stat's rule and contributors; the light's parameters from the stat and data (pure functions
in Game or Core: strength and radius, off by day, on at night and underground); nothing worn means no light term and no extra draw. No
existing digest, golden or fixture may move.

Documents: docs/game/architecture.md (how the player light works and how to tune it).

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full (13,301
passed, 9 skipped on feature/inventory after I04b); scripts/test.sh timing (a CPU-budget failure in world code you did not touch is load
from other sessions: report it); format; content validate.

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i05b, at most
30 lines printed; never background them, never poll. Another agent of this series (I04c, the panel's headings) and other sessions build
on this machine: a wait for a limited.sh slot is normal.

Report, one page: commits; how the world is lit today and the approach chosen (and the one rejected, why); the data and its numbers;
the frame times with the load; the screenshot paths, each with what it shows; tests; anything that would change for shipped items (I08b's
lantern, torch and amulet). Leave the worktree clean on your branch with no process of yours running. Commit messages: subject, blank
line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
