# Brief: I-merge3, main's world-next series (PR #72) into feature/inventory

Sent 2026-11-19 to a general-purpose agent, Sonnet 5.5 at medium effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/i-merge3`,
branch `I-merge3`, cut from `feature/inventory` at `f8e76b3b`. The owner (2026-11-19): "Can you merge from main and fix conflicts".

```
You are implementing I-merge3: merge `origin/main` (now 11fc1415: 114 commits since ae30d65b, PR #72, the world-next series: far view,
LOD, flight, SDL display fixes) into the inventory series. Read .claude/briefs/i-series-standard.md (shared rules), .claude/briefs/i-merge-main.md
(how the two sides meet) and .claude/briefs/i-merge2-report.md (the last merge).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i-merge3, branch `I-merge3`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i-merge3 &&`; absolute paths for file tools. `git fetch origin`, `git merge origin/main` (no
rebase, no squash, no history rewriting); commit the merge once it builds (`I-merge3: Merge origin/main (the world-next series, PR #72)
into the inventory series`, the body listing each conflict and its resolution); later fixes separate, prefixed `I-merge3: `. Do not push.

The trial merge conflicts in four files:
- `src/Vocaterra.Game.Core/Simulation/VoxelCommandKind.cs`: main added `Descend = 8` (the fly cheat); this side has `Equip = 8`,
  `Unequip = 9`, `SlotMove = 10`. Main keeps its number; this side's three move to `Equip = 9`, `Unequip = 10`, `SlotMove = 11`, with
  their doc comments. Then find everything that hard-codes those numbers (tests, script steps, command logs, replay or digest pins, docs
  such as docs/game/architecture.md, saves.md, running.md, the I series' plan text only if it states the number as current) and fix it;
  if a pinned digest or golden moves because the command numbers changed, re-record it and say which.
- `content/locale/manifest.json`: take either side and rehash (see how it is regenerated; `scripts/rehash-bundle.sh` does game-v1; the
  content loop's refresh does both).
- `docs/game/findings.md`, `docs/handovers/README.md`: a union in order (main's world-next entries and this series' entries; main's new
  open handover row and this series' inventory row).
Also check that main's new features still work with the inventory's: flight and the off-hand light, the far view and the climate
temperature term (both read the generator), SDL changes and the inventory panel. Main's `scripts/` changes win.

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full
(feature/inventory before the merge: 13,871 passed, 9 skipped; say main's count if its last report gives it); scripts/test.sh timing;
format; content validate; the A2 content gate. No game window.

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i-merge3, at
most 30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal.

Report, one page: commits; each conflict and its resolution; every place the command numbers were changed; tests, goldens, digests or
fixtures changed and why; the counts. Leave the worktree clean on your branch with no process of yours running. Commit messages: subject,
blank line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
