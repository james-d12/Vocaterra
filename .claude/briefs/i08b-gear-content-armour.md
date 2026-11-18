# Brief: I08b, gear content, second half (armour, accessories, bags, the off-hand)

For a general-purpose agent, Sonnet 5.5 at high effort (the plan's section 4: L). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i08b`, branch `I08b-gear-armour`, cut from `feature/inventory` after I08a is gated. The
shared part is `.claude/briefs/i-series-standard.md`. Stop 3 is settled (the owner, 2026-11-18): the list is the inventory plan's section
3.13a; the light's numbers come from I05b (stop 5).

```
You are implementing I08b: the second half of the gear as content: iron armour, the accessories with their effects, the two bags, the
shield and the lantern, the torch as an off-hand item, sold by Domingo (the smith) and Pablo (the shopkeeper) and made at the workbench.
Read .claude/briefs/i-series-standard.md (shared rules), the inventory plan's sections 3.3 to 3.6, 3.8, 3.13 and **3.13a (the settled
list: it overrides 3.13's draft; iron only, one tier; the body piece «la coraza»; the boots are armour, the clothes have none)**, and
content/README.md. Then .claude/briefs/i08a-report.md (how I08a added items, icons, words, recipes and purchases: follow its pattern) and
i05b-report.md (the `light` stat and `game/light.json`).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i08b, branch `I08b-gear-armour`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i08b &&`; absolute paths for file tools. Commits prefixed `I08b: `, one per item that
builds and passes. Do not push.

What to add (numbers from 3.13a):
1. **Armour** (tag the armour group accepts, stack 1, `stats.armour`, a `look`): `casco` (head, 2), `coraza` (body, 5), `grebas` (legs,
   3), `botas` (feet, 2); recipes in `hierro`; Domingo sells them.
2. **Accessories** (`wear` kind `accessory`): `anillo` (maxStamina +20), `pulsera` (walkSpeed x1.10), `cinturón` (hungerDrain x0.75),
   `collar` (thirstDrain x0.75), `amuleto` (light 3); Pablo sells them. Check `StatRules`' units (factor in permille or a fraction) and
   write the rows to match.
3. **Off-hand** (`wear` kind `offhand`): `escudo` (armour 3), `farol` (light 12), and today's `antorcha` gains `wear: offhand` and light
   9 while staying placeable (check that wearing and placing do not clash: placing takes from the hand slot, not the off-hand). Domingo
   sells the shield, the lantern and the torch.
4. **Bags** (`wear` kind `back`, `capacity`): `saco` (10), `mochila` (20); Pablo sells them; check I03b's bag rule with both (a full
   mochila cannot come off; the saco gives 40 slots, the mochila 50).
5. **Icons** for each new item, as I08a drew them; no borrowed icons.
6. **Words:** nouns in `game/nouns.json`, English in `locale/en/nouns.json`; lexicon entries for `coraza`, `grebas`, `pulsera`,
   `amuleto`, `mochila` (gender and plural right: la coraza, las grebas, la pulsera, el amuleto, la mochila); CEFR world words with
   reasons as I08a did. `grebas` and `botas` are plural nouns: the purchase and the article handle them as I08a handled `pantalones`.
   `botas` now names armour only: if I08a or older content uses «botas» for anything else, say so.
7. **Purchases** by naming the item, through the existing `sell` actions with prices; new Spanish pool lines A1 or A2, the strict A2 lint
   and the grammar check clean, speech rendered.

Tests (Given_When_Then, mirrored folders): every new item loads with its wear, stats and look; recipes craft; each purchase by voice; the
armour's damage reduction with the full set (12 points: 48%) and with the shield (60%) through `ArmourDamageModifier`; each accessory's
effect on the sheet; the lantern's and amulet's light through `PlayerLight`; the saco and mochila slot counts and the bag rule; the torch
still places from the hand.

Content loop: `scripts/content-loop.sh --grammar --speech`, the strict A2 lint, `--validate-content`, `scripts/rehash-bundle.sh`. If
`feature/inventory` moves while you work, merge it in before your gate (a manifest conflict: take either side, run rehash-bundle.sh).

No game window (the orchestrator takes the pictures after your gate: list the launch arguments for the armour, a bag and the lantern at
night).

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full (the
count when you start is in your prompt); scripts/test.sh timing (a CPU-budget failure in world code you did not touch is load: report it);
format; content validate; the A2 content gate.

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i08b, at most
30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal.

Report, one page: commits; the items with numbers, colours and icons; every Spanish word and line added with its English (the owner does not
read Spanish); recipes and prices; tests added and changed; the counts; the screenshot arguments. Leave the worktree clean on your branch
with no process of yours running. Commit messages: subject, blank line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
