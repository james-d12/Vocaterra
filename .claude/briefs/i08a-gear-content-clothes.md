# Brief: I08a, gear content, first half (the materials and the clothes)

For a general-purpose agent, Sonnet 5.5 at high effort (the plan's section 4: L). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i08a`, branch `I08a-gear-clothes`, cut from `feature/inventory` when launched. The shared
part is `.claude/briefs/i-series-standard.md`. Stop 3 is settled (the owner, 2026-11-18): the list is the inventory plan's section 3.13a.

```
You are implementing I08a: the first half of the gear as content: the three materials and the clothes, craftable at the workbench and
bought from Pablo by naming them, and the starting clothes. Read .claude/briefs/i-series-standard.md (shared rules), the inventory plan's
sections 3.2, 3.4, 3.7, 3.11, 3.13 and **3.13a (the settled list: it overrides 3.13's draft)**, D-game-35 (docs/game/decisions.md), and
content/README.md ("The edit loop", the world words rule, "Items (I01)"). Reports worth a look: .claude/briefs/i01-report.md (items as
data, icons by key), i03a-report.md (wear, stats, look), i09-report.md (how `look` colours draw on the player), i04b-report.md (nouns
added, the content loop).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i08a, branch `I08a-gear-clothes`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i08a &&`; absolute paths for file tools. Commits prefixed `I08a: `, one per item that
builds and passes. Do not push.

What to add (all numbers from 3.13a; warmth there is in degrees, the data stores tenths):
1. **Items** in `game/items.json`: the materials `tela`, `cuero`, `hierro` (tag `material`, stack 99) and the clothes `sombrero`, `gorro`,
   `camisa`, `chaqueta`, `abrigo`, `pantalones`, `zapatos` (tag `clothing`, stack 1, `wear` the slot, `stats.warmth`, a `look` colour
   per covered part, the body pieces with a `sleeve` colour). Clothes have no boots: the boots are armour (I08b). Choose plain, readable
   colours that tell the pieces apart on the player.
2. **Icons:** each new item gets its own icon key, drawn the way the existing item icons are (find how: `BlockAtlasLayout.IconForKey` and
   the atlas; procedural pixel art in code if that is how they are made, or CC0 art only, D-game-5, recorded in the attribution file).
   No item borrows another's icon.
3. **Words:** each noun in `game/nouns.json` (its items), its English in `locale/en/nouns.json`; `tela`, `cuero`, `gorro` get lexicon
   entries (find where world nouns live in `lexicon/`; gender and plural right: la tela, el cuero, el gorro); every noun on neither CEFR
   list joins the world-words block at the end of `cefr/a1.txt` with its one-line reason (D-game-35 (a)). `pantalones` is used in the
   plural («los pantalones»): make sure the catalogue, the article and the purchase handle it.
4. **Recipes** at the workbench (`mesa`) in `game/recipes.json`, as 3.13a lists.
5. **Pablo sells them** (the shopkeeper; `game/host-events.json`'s `sell` action with `prices`, the existing `stall.sell_flour` pattern;
   his agent's states and pools in `agents/shopkeeper.json`): `tela`, `cuero` and every clothing item at 3.13a's prices, bought by naming
   them («Quiero una camisa», «¿Tienes pantalones?»). Domingo sells `hierro` (the smith; his agent and host events) at 3.13a's price. Where a
   pool line names flour only, generalise it or add lines so a clothes purchase reads right; every new Spanish line A1 or A2, the strict A2
   lint and the grammar check clean, with speech rendered (the content loop's `--speech`). Add answer-set examples where the purchase
   tests need them.
6. **The starting clothes:** a new game starts wearing `camisa`, `pantalones` and `zapatos` (find where a new session's inventory and
   equipment start; data, not code, if a place exists). Say what this moves (the new-game state digest, save fixtures, goldens, scripted
   runs) and re-record only what it moves, each named.
7. Nothing else: no armour, accessories, bags, shield or lantern (I08b), no durability (I07).

Tests (Given_When_Then, mirrored folders): every new item loads with its wear, stats and look; each recipe crafts at the workbench; buying
each item from Pablo (and `hierro` from Domingo) by voice through the existing purchase path (a transcript or session test like the flour
sale's); a new game wears the three starting pieces and the stats sheet shows their warmth (+4 degrees); the icons resolve (no fallback).

Content loop: `scripts/content-loop.sh --grammar --speech` (render only missing speech), the strict A2 lint, the host's
`--validate-content`, `scripts/rehash-bundle.sh` for the manifests. The save fixture pins no bundle hash (B29c).

Other agents of this series build beside you: I06b edits `game/temperature.json`. If `feature/inventory` moves while you work, merge it
into your branch before your gate (a manifest conflict is resolved by taking either side and running scripts/rehash-bundle.sh).

No game window (the orchestrator takes the pictures after your gate: list the launch arguments that show the clothes on the player and in
the panel, and a purchase).

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full (the
count on feature/inventory when you start is in your prompt); scripts/test.sh timing (a CPU-budget failure in world code you did not touch
is load from other sessions: report it); format; content validate; the A2 content gate (`scripts/content.sh lint content/game-v1 --strict
--level a2 --fail-on-level --host-pools 'guide.*'`).

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i08a, at most
30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal.

Report, one page: commits; the items with their numbers, colours and icons; every Spanish word and line added, with its English (the owner
does not read Spanish); recipes and prices; the starting clothes and what they moved; tests added and changed; the counts; the screenshot
arguments. Leave the worktree clean on your branch with no process of yours running. Commit messages: subject, blank line, body, blank
line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
