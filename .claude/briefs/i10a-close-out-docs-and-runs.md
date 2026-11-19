# Brief: I10a, the close-out's documents and scripted runs

For a general-purpose agent, Sonnet 5.5 at medium effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/i10a`, branch
`I10a-close-out`, cut from `feature/inventory` after I09b is gated. The shared part is `.claude/briefs/i-series-standard.md`. The rest of
I10 (the QA passes, the screenshot tour, the pull request) is the orchestrator's.

```
You are implementing I10a: the inventory series' documents and its scripted runs, before the pull request. Read
.claude/briefs/i-series-standard.md (shared rules; game windows are allowed for this milestone's scripted runs, one at a time through
limited.sh), docs/game/inventory-plan.md (all of section 3, 3.13a, section 8's table), and skim every report in .claude/briefs/ named
i0*-report.md and i-merge*-report.md (what was built, in order).

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i10a, branch `I10a-close-out`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i10a &&`; absolute paths for file tools. Commits prefixed `I10a: `. Do not push.

1. **content/README.md:** two complete recipes a content author follows without reading code: "Adding an item" (the items.json row and
   its fields: name, id, stack, icon, held, tags, wear, stats, look, capacity, durability; the icon painted in code or by key; the noun in
   game/nouns.json, plural-only nouns, the English in the locale, the lexicon entry, the CEFR world words; recipes including repair rows;
   selling it through an NPC's `sell` prices and answer sets; the content loop and rehash) and "Adding a slot group" (an equipment.json
   group: slots or count with a wear kind, accepts, layer, heading, labels, noDuplicates, statsApply; how the panel and the avatar pick it up;
   what needs code, if anything). Check each step against the code and the shipped data; fix what the earlier sections say wrongly.
2. **docs/game/architecture.md:** one coherent section on inventory and equipment (items as data, containers and moves, equipment and
   layers, stats and their consumers, damage, temperature and its terms, the player light, durability, the panel and the HUD's off-hand
   cell, gear on the avatar), merging the paragraphs each milestone appended; no history narrative, just how it works and where to extend.
3. **docs/game/saves.md:** version 11 described once and completely (the worn items, the active slots, wear, `systems.temperature`, the
   biomes' fields), with the fixture's name.
4. **docs/game/running.md:** every launch option the series added (`--temperature`, `--equip`, `--move`, `--wear`, `--repair`, and any
   other), with an example each; make sure `--move` has a help line in LaunchOptions' help text (I-merge-fix found none) and the help text
   lists every series option.
5. **Scripted runs** (docs/game/scripted-runs.md, scripts/scripted-runs.sh): run the whole set (`VOCATERRA_RUNS_WORK` a folder of your
   own) against the current tree and list each run's difference from what its doc row describes. Runs whose pictures or numbers moved
   because of the series (worn starting clothes, the panel's new layout, the temperature, the off-hand cell, 30 bag slots) are expected:
   say which and why, and update their rows. A run that crashes, hangs or shows a bug is a finding: report it, do not fix code. Add three
   runs: r-gear (buy a shirt from Pablo by voice with coins given, put it on, the panel open), r-cold (night in the rain on the peak without
   and with the coat, F3 on), r-durability (an axe at `--wear` near its end, chop until it wears out, repair it at a table). Use the
   existing runs' style.

No code change other than the help text (item 4). No content change.

Gate (main's form): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh full (the
count is in your prompt); format; the doc-link test (`scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Tests -c Release --no-build
--filter FullyQualifiedName~MarkdownLinkTests`).

Long commands run in the foreground with the Bash tool's timeout at 600000 ms, output to a log in your scratchpad named with i10a, at most
30 lines printed; never background them, never poll. A wait for a limited.sh slot is normal.

Report, one page: commits; what each document gained or lost; the scripted runs' results table (run, result, difference, expected or a
finding); the three new runs and their captures' paths; the counts. Leave the worktree clean on your branch with no process of yours
running. Commit messages: subject, blank line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
