# Brief: I09b, the player's looks and the axe's durability

Sent 2026-11-19 to a general-purpose agent, Sonnet 5.5 at medium effort (small). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i09b`, branch `I09b-looks`, cut from `feature/inventory` at I07's record. The shared part is
`.claude/briefs/i-series-standard.md`.

The owner (2026-11-19), after the pictures of I08a and I08b: "Axe durability increase to 350. Torch lasts forever currently. Fix the
looks."

```
You are implementing I09b: three fixes to how gear looks on the player, and one durability number. Read
.claude/briefs/i-series-standard.md (shared rules), .claude/briefs/i09-report.md (how gear is drawn: `Appearance`, `PlayerGear`,
`GearFigureParts`), i08a-report.md and i08b-report.md (the shipped items and their `look` rows), i07-report.md (durability).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i09b, branch `I09b-looks`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i09b &&`; absolute paths for file tools. Commits prefixed `I09b: `. Do not push.

The fixes:
1. **No pack without a bag.** The player's own figure has a leather backpack built in (`NpcFigures.cs`, the player's parts, the
   "the pack" box at about line 180), so the player always looks as if wearing a bag, and a worn bag draws a second pack in the same place
   (`GearFigureParts`, `appearance.Back`). Take the built-in pack off the player's figure only (the NPCs keep theirs); the pack shows only
   when a bag is worn. Check first person and third person, and any test or golden that counts the player's boxes or hashes its mesh (say
   what moved and why).
2. **Each bag its own colour.** `saco` and `mochila` have no `look`, so both draw as the default leather. Give each a `look` with a `back`
   colour matching its icon (the sack tan, the backpack green; read the icon's colours in `BlockArtCarried.cs`).
3. **Hats do not cover the face.** Head gear paints the whole head box, face included (I09's one-box head). Head pieces (clothing,
   armour, cosmetic) draw as a cap over the top and the back and sides of the head, leaving the face (the front's lower part) as it is: a
   shape row in `GearFigureParts`, not a special case per item. A helmet may come lower at the sides than a hat, if a row per kind is cheap;
   otherwise one shape for all head pieces.
4. **The axe lasts 350 gathers** (items.json, its `durability.max`, from 200). The torch keeps no durability (the owner: it lasts for ever).

Tests (Given_When_Then, mirrored folders): the player's figure without gear has no pack box; with a bag one pack in the bag's colour; each
bag's colour from data; a head piece leaves the face uncovered (the face box or its front quads untouched); the axe's durability 350; NPC
figures unchanged.

Content: items.json (two looks, the axe); `scripts/content-loop.sh` (no new Spanish expected), `scripts/rehash-bundle.sh`. Documents:
docs/game/architecture.md (the head piece's shape, the pack).

Screenshots: you may open game windows for this milestone, one at a time through limited.sh: third person from behind and from the front
(`--third-person`, `--look <yaw>,<pitch>`), the starting clothes with no bag, then `--equip sack`, then `--equip backpack --equip gorro`,
then `--equip helmet`, each `--seed 42 --mute --no-autosave --quality low --time 12 --size 1280x720 --frames 30 --screenshot <png>` and a
`--session-dir` of your own in your scratchpad. List the PNG paths in the report.

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full (the
count is in your prompt); scripts/test.sh timing (a CPU-budget failure in world code you did not touch is load: report it); format;
content validate.

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i09b, at most
30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal.

Report, one page: commits; each fix and what moved; tests; the counts; the screenshot paths. Leave the worktree clean on your branch with
no process of yours running. Commit messages: subject, blank line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
