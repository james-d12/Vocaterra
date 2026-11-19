# Brief: I04c, the doll's headings as bare nouns

Sent 2026-11-18 to a general-purpose agent, Sonnet 5.5 at medium effort (a small change). Worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/i04c`, branch `I04c-bare-headings`, cut from `feature/inventory` at `0e492fba` (after I04b).
The shared part is `.claude/briefs/i-series-standard.md`.

The owner saw I04b's screenshots (2026-11-18): at 1280x720 the headings read «la ropa», «armadura», «apa». The owner: "for the headings,
instead of saying la or l, can we just put the word ... That would reduce it."

```
You are implementing I04c: the equipment panel's column headings show the bare noun, without its article. Read
.claude/briefs/i-series-standard.md (shared rules) and .claude/briefs/i04b-report.md (what I04b built: `HeadingText`, the `heading` key
on equipment.json's layered groups, `InventoryHitKind.Heading`).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i04c, branch `I04c-bare-headings`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i04c &&`; absolute paths for file tools. Commits prefixed `I04c: `. Do not push.

What to change:
1. The headings over the doll's layered columns are the bare noun: «ropa», «armadura», «apariencia» (no «la»). Only the headings change:
   item names, slot labels and the title «la bolsa» keep their articles. The hover over a heading may still show the noun with its article
   and the English, as other nouns' hovers do.
2. At the regular layout (1280x720 with 40 and 50 slots; the smallest regular window I04 found, about 950x700) each heading shows in full.
   The column pitch is about 66 px with 44 px cells and «apariencia» is wider than a cell: centre each heading over its column and let it
   use the gap to its neighbours and, for the outer columns, the free space beside the doll, as long as headings never overlap each other,
   the row labels or the bag. If a word still cannot fit at regular size, use a smaller font for all headings rather than a short mark.
   Compact and tight may keep a short mark with the word on hover (as now); say what each layout shows.
3. Tests (Given_When_Then, mirrored folders): at every regular layout case each heading's text is the full bare noun and its measured
   width fits its box; the boxes do not overlap each other, the row labels or the bag; a fourth layered group from data (I04b's test) still
   gets its heading. Change I04b's heading tests that expected the article, and list them.

No content change is expected (the nouns exist). No game window: the orchestrator retakes the screenshot after the gate. Run
`scripts/test.sh project <the Game and Game.Core test projects>` while iterating.

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full (the
count on feature/inventory after I04b is 13,301 passed, 9 skipped); scripts/test.sh timing (a CPU-budget failure in world code is load
from other sessions on this machine: report it, do not touch it); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes;
scripts/limited.sh scripts/content.sh validate.

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i04c, at most
30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal: another agent of this series and other
sessions build on this machine.

Report, one page: commits; what each layout shows for the headings and the font used; tests added and changed; the counts. Leave the
worktree clean on your branch with no process of yours running. Commit messages: subject, blank line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
