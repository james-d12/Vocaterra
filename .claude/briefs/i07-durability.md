# Brief: I07, durability and repair

For a general-purpose agent, Sonnet 5.5 at medium effort (the plan's section 4: M). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i07`, branch `I07-durability`, cut from `feature/inventory` after I08b is gated. The shared
part is `.claude/briefs/i-series-standard.md`.

```
You are implementing I07: tools and gear wear out and are repaired (the inventory plan's section 3.12, D-game-35, W13 taken over). Read
.claude/briefs/i-series-standard.md (shared rules), the inventory plan's sections 3.2, 3.4, 3.5, 3.6, 3.12, 3.13a and 3.14, then
.claude/briefs/i02-report.md (containers and moves), i03b-report.md (the save's worn items), i04b-report.md and i04c-report.md (the
panel), i05a-report.md (stat consumers, the damage pipeline), i08a-report.md and i08b-report.md (the shipped gear and its rows).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i07, branch `I07-durability`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i07 &&`; absolute paths for file tools. Commits prefixed `I07: `, one per item that builds
and passes. Do not push.

What to build:
1. **Wear on the stack:** `ItemStack` gains `Wear` (a whole number), meaningful only for items with a `durability` in items.json (stack
   1 items: tools, clothes, armour, accessories, the shield, the lantern, the bags). Stacking, splitting, moving, sorting, equipping, the
   trash and the held stack keep the wear with the item; two stacks with different wear never merge.
2. **What wears an item** is an `IWearSource` per kind, chosen by data (the item row names its wear rule or the group it is worn in
   implies it; say which): **use** for tools (a gather or a chop adds wear: W13's rule; find where the axe is used), **time worn** for
   clothes, accessories and bags (a worn item gains wear per in-game hour, deterministic from the simulation's ticks), **damage absorbed**
   for armour and the shield (through I05a's `IDamageModifier` pipeline: drowning has no modifiers, so this wears nothing yet; prove it with
   a test cause). The lantern and torch in the off-hand wear by time worn. New sources need no edit to the others.
3. **Worn out:** at its durability an item is worn out and is never lost or destroyed: a worn-out tool works at bare hands' speed, worn-out
   gear gives no stats (warmth, armour, light, the accessories' effects), a worn-out bag still holds its slots (losing slots would drop
   items: say if you see a better rule). `tool_worn` fires once when a tool wears out (find how host events and HUD lines work; a short
   Spanish line in `hudLines` with its English, A1, speech if the pattern has it).
4. **Repair:** a repair recipe at the workbench (`mesa`) restores an item to no wear with its material from 3.13a (`tela` for cloth
   clothes, `cuero` for leather pieces, `hierro` for iron, `madera` for the axe and wooden tools; one material each, a count you propose).
   Find how the recipe panel works and add repair as rows or a mode; say which.
5. **The wear bar** under an item's icon in the hotbar, the bag and the equipment cells (green to red, hidden at no wear), drawn by the
   Game layer from a pure function in Core or a testable layout helper.
6. **Durability numbers** (data, items.json): propose them so that the axe lasts about a day of steady chopping, clothes about ten
   in-game days worn, accessories and bags longer, armour by damage; list them for the owner.
7. **The save:** worn stacks save their wear (the next save version, with its fixture; the game is unreleased: no migration); the state
   digest includes wear. `--wear <item>=<amount>` sets wear for a scripted run (check the launch options' style).

Tests (Given_When_Then, mirrored folders): wear moves with the item through every container move; no merge across wear; each source
(use, time worn, damage with a test cause); worn out: the tool's speed, the stats gone, nothing lost, the bag still holding; `tool_worn`
once; repair recipes; the wear bar's fraction and colour; the save round trip and the fixture; determinism.

Content: items.json `durability` and the wear rule, recipes, the line; the content loop with `--grammar --speech`;
`scripts/rehash-bundle.sh`. Documents: docs/game/architecture.md (durability), docs/game/saves.md (the new version), docs/game/running.md
(`--wear`).

No game window (the orchestrator takes the pictures: list the launch arguments for a worn axe, worn clothes and the repair).

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full (the
count is in your prompt); scripts/test.sh timing (a CPU-budget failure in world code you did not touch is load: report it); format;
content validate; the A2 content gate.

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i07, at most
30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal.

Report, one page: commits; the model (wear, sources, worn out, repair); the numbers proposed; every Spanish line with its English; the
save version and what moved; tests added and changed; the counts; the screenshot arguments. Leave the worktree clean on your branch with
no process of yours running. Commit messages: subject, blank line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
