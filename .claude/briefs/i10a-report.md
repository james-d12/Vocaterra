# I10a report (the close-out's documents and scripted runs)

The agent's report, saved by the orchestrator (2026-11-19). Sonnet 5.5 at medium effort; brief `i10a-close-out-docs-and-runs.md`. Gated at
`38ad4243`: 13,796 passed, 0 failed, 9 skipped; timing 23; format clean; the doc-link test passes.

Branch `I10a-close-out`: bc846c6a (content/README recipes), 69a676ee (architecture, saves), 91e0e709 (running, the `--move` help line),
710351a1 (scripted-runs.md, scripts/scripted-runs.sh).

**Documents.** content/README.md: "Adding an item" (ten steps, a `flat-cap`/`gorra` example: fields, icon, noun and plural-only nouns,
locale, lexicon, CEFR world words, recipes and repair rows, selling through Pablo or Domingo: the `sell` price, Pablo's `instead` entry,
the answer set, the stock line; `session.json` `wears`/`grants`; the loop and rehash; the pinned counts a noun moves) and "Adding a slot
group" (the fields, how the panel and the avatar pick a group up, the four things that need code); stale lines fixed. architecture.md: one
"Inventory and equipment" section with eleven subsections, where to extend each; the felt temperature is saved (a wrong claim fixed).
saves.md: version 11 once and completely (fixture `save-v11.json`). running.md: a table with an example each for `--equip`, `--move`,
`--wear`, `--repair`, `--temperature`. `LaunchOptions.cs`: a `--move` help line (the only code change).

**Scripted runs** (r01 to r43, r50 to r53, with `GAME_BIN` at the Release build; the script defaults to Debug): all exit 0, no finding.
Expected differences: the starting clothes (+4), the F3 temperature line, the shirt sleeve, 30 bag slots, digests, the speech count (869
lines, 1,738 files), the scene line; the `--inventory` runs show the new panel; r08 22 crafting rows; r14 the clothes in third person; r26
the worn clothes stay at death, the wake position already stale in its row. Tours r51 to r53 ran (not compared). **New runs:** r44-gear (a
shirt bought from Pablo; shift-click put it in the empty appearance slot: the swap I10b fixes), r45-cold (rainy night on the stony peaks:
starting clothes -3.2, the coat without the shirt 0.0; needs `--pass open` and two teleport hops), r46-durability (the axe at 345: five
chops at 15 ticks, `tool_worn`, then 60 ticks; `--repair hacha` at the camp workbench). Captures: `scratchpad/i10a-out/`.
docs/nlp/changing-the-core.md still names save-v10.json (core territory).
