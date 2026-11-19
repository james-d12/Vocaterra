# Brief: I10b, the QA passes' fixes

Sent 2026-11-19 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/i10b`,
branch `I10b-qa-fixes`, cut from `feature/inventory` at `ffb150fb`. The shared part is `.claude/briefs/i-series-standard.md`.

The orchestrator ran three QA passes on 2026-11-19 (`docs/qa-reports/2026-11-19-ui.md`, `2026-11-19-save.md`,
`2026-11-19-progression.md`). The owner chose colder nights and hotter warm biomes, and left the smith's day-one offer for main.

```
You are implementing I10b: the fixes for the inventory series' QA findings. Read .claude/briefs/i-series-standard.md (shared rules), the
three QA reports named above (each bug has its reproduction, its evidence and its likely files), and the series' reports in
.claude/briefs/ for the parts you touch (i04b/i04c for the panel, i05b for the light, i06c for the temperature, i07 for repair rows and
the crafting panel, i08a/i08b for the shop and the equip rules).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i10b, branch `I10b-qa-fixes`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i10b &&`; absolute paths for file tools. Commits prefixed `I10b: `, one per fix that
builds and passes, naming the QA id. Do not push.

Fixes:
1. **QA-2026-11-19-ui-1 (major):** the crafting panel never runs off the window: when its rows do not fit, it scrolls (wheel, and a
   visible scroll mark) or pages, keeping the header and the footer on screen, at every size from 640x480 to 3840x2160 and every text
   scale; repair rows stay reachable. Tests on the layout at the sizes the report names with 24 or more rows.
2. **QA-2026-11-19-ui-2:** the stats line shows a `light` chip when the sheet's light is above 0 (an icon in `StatIcons`, a row in
   `StatDisplay`), within I04b's chip placer at every layout.
3. **QA-2026-11-19-ui-3:** `--equip` of an item that cannot be worn leaves it in the bag, as the help text says (or change both to agree
   on giving it; the help text is the contract: keep it).
4. **QA-2026-11-19-ui-4:** a cut bag label never reads the same as another's in the same bag: at compact and tight sizes cut each label to
   the shortest prefix that tells it apart from the other labels shown (at least 3 letters), or use a smaller font for long names; say
   which. Hover keeps the full name.
5. **QA-2026-11-19-ui-5 and the progression report's `--give` note:** the `give:` console line names the item (`session.Data.Items`), and
   says when nothing went in (a full bag).
6. **QA-2026-11-19-save-1:** a save naming an item this bundle lacks, in a worn slot or a bag slot, loads with that slot empty as
   saves.md says (the digest check must not reject it: decide how the digest treats a dropped item, e.g. compare after the drop or skip the
   check when items were dropped and log which), and a slot with no `count` loads as 1 or is refused with a message naming the slot. The
   save's refusal messages read as sentences (no .NET parameter name; the report's "(Parameter 'state')" case). A worn item in a group that
   does not accept it is dropped to the bag on load (the fit check), with a log line.
7. **QA-2026-11-19-progression-1 (major):** a successful purchase (and any line that asked for and got something) resets the NPC's
   unproductive count (`NpcService.FailForward`, `agent.Unproductive`), so fail-forward fires only after lines that asked for nothing, as
   its comment says. Test: four purchases in a visit give four items and no bottle; four lines asking for nothing still fail forward.
8. **QA-2026-11-19-progression-3 (major):** putting on a garment (shift-click, `--equip`, the equip command) whose stats-applying slot is
   taken swaps it in there (the worn piece goes to the bag, or to the held stack if the bag is full: refused with the refusal line) instead
   of falling to a group with `statsApply: false`; a cosmetic slot is filled only when the player drops the item on that cell. Rule as data
   or a rule class, not a special case per group id. Tests: the coat over the shirt swaps; a coat dropped on the cosmetic body cell goes
   there; a full bag refuses the swap.
9. **QA-2026-11-19-progression-4 (the owner: "colder nights"):** retune `game/temperature.json` (and the rain kind's channels in
   `weather.json` only if needed) so that, on seed 42 in the valley (the camp, the village): a rainy night with the starting clothes (+4)
   is cold (felt below 3.0; aim about 1); with the coat swapped in for the shirt (+8) it is cool; a clear night with the starting clothes is
   cool, not cold; a clear noon stays warm (not hot) with the starting clothes. Warm biomes hotter: a beach or plains column at a clear noon
   with the coat outfit is hot (28 or more), with the starting clothes warm. The peaks stay very cold at a clear night. Report a feel table
   (the camp, the village, a beach, plains, the highest peak, a deep cave; noon and night, clear and rain; starting clothes and coat) before
   and after. Change tests that pinned the old numbers and name them; the valley "unchanged" tests of I06b/I06c become tests of the new
   targets.

Not in scope: the smith's day-one axe offer (QA-2026-11-19-progression-2: left for main); documents other than the lines your fixes make
wrong in docs/game/saves.md and content/README.md (another agent, I10a, is rewriting architecture.md, running.md and scripted-runs.md: do
not edit those three; list in your report any line in them your fixes make wrong).

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full (the
count is in your prompt); scripts/test.sh timing; format; content validate; the A2 content gate; the content loop if content changed
(`scripts/rehash-bundle.sh` for the manifests). If `feature/inventory` moves while you work, merge it in before your gate.

You may open game windows to check the panel and the temperature, one at a time through limited.sh, with a `--session-dir` of your own in
your scratchpad. Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with
i10b, at most 30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal.

Report, one page: commits; each QA id and its fix; the feel table before and after; tests added and changed; the doc lines elsewhere that
need changing; the counts. Leave the worktree clean on your branch with no process of yours running. Commit messages: subject, blank line,
body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
