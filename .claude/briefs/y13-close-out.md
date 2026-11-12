You are implementing Y13, "Close-out", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core, Vocaterra.Game,
Vocaterra.Host and tools/Vocaterra.SpeechRender; the language core is Vocaterra.Nlp and its packs. This milestone adds three scripted
runs that cover the settlement series end to end and brings the documents up to date; the orchestrator runs the QA agents and opens the
pull request after you. No new gameplay.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y13, which already exists on the branch `Y13-close-out` (cut from
`feature/settlement` after Y12 was recorded; Y00 to Y12 are all in it). Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/y13 &&` and use absolute paths for file tools; your shell may start in another checkout,
which you must not edit. Commit after each item, with the prefix `Y13: `. Do not push your branch. Never push or merge
`feature/settlement` or `main`. When you finish, leave the worktree on its branch, clean, with no process of yours running.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh, scripts/content-loop.sh and scripts/scripted-runs.sh call it themselves where they need it:
run them directly. Never start two heavy commands at once, and write no shell wait loop on `pgrep -f`. Set `VOCATERRA_RUNS_WORK` to a
folder whose name is unique to Y13 (in your scratchpad) for every `scripts/scripted-runs.sh` call.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses
a command line containing the words `eval` or `hash`: use a wrapper script in your scratchpad for those). Model-gated tests and the speech
need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first (lean): CLAUDE.md; docs/process/documentation.md; docs/game/settlement-plan.md sections 8 (with every as-built note) and 8.5;
docs/game/scripted-runs.md and `scripts/scripted-runs.sh` (how a run is declared, the shared CAMP steps, the compare tool); the reports
of the series in /home/james/Dev/Vocaterra/.claude/worktrees/settlement/.claude/briefs/ (`y04-report.md` to `y12-report.md`, read their
"For Y13" and "known" parts); docs/game/running.md (the dev aids `--lodging`, `--own-plot`, `--mark-plot`, the camp's flag if Y12 added
one).

Rules: edit only `scripts/scripted-runs.sh`, `docs/game/` (not either implementation-plan.md, decisions.md or settlement-plan.md: the
orchestrator does those), `docs/process/documentation.md`'s list of series, `content/README.md`, the READMEs of the game projects, and
tests only if a run reveals a bug (then stop and report it instead of fixing it beyond a one-line fix). Do not edit CLAUDE.md: put the
line it needs in your report and the orchestrator edits it. No new gameplay; a bug found is reported, not fixed (unless trivial: say so).

Deliverables:

1. **Three scripted runs** in `scripts/scripted-runs.sh`, declared like the others, from a new world on seed 42 with `--mute --needs off`
   (and `--dev` where an aid needs it), each ending with a screenshot and a save:
   - `r40-inn`: day one as a player plays it: the camp checklist (the shared CAMP steps), Luna's line and the beacon to the inn, the walk
     to Rosa, «sí», the ending in her voice, the sleep in the inn's bed, the morning of session 2. Shots: the beacon, the talk, the
     ending card, the morning in the guest room.
   - `r41-land`: from session 2 (`--skip-to-session 2`): the mayor gives the stake, the stake in the village (refused), the stake in the
     north meadow (the outline), «Quiero un terreno, por favor.» with too few coins (`cant_pay`), coins earned or given by an aid, the
     purchase (`plot_bought`), the owned outline, «Mi terreno»'s first two steps. Shots: the refusal, the pending outline, the talk, the
     owned outline.
   - `r42-camp`: from an owned plot (`--own-plot` and the camp kit, or continuing r41's save if the runs can load one): the camp placed,
     a torch on its ring, a night at the bedroll, a death after it waking at the camp, a berry planted on the plot, the camp packed back.
     Shots: the camp by day, by night with the fire lit, the wake after the death.
   Run each twice and compare the two captures with the compare tool: report any difference that is not timing noise (r01's sounds are
   known noise). Add their rows to `docs/game/scripted-runs.md`.
2. **The documents:** `docs/game/saves.md` (version 8: `systems.sleep`, `systems.atmosphere`, `systems.land`, the camp; one description,
   checked against `SaveDocument.cs`), `docs/game/architecture.md` (sleep places, lodging, land rules, the stake, the exchange, the camp:
   a short section that points at the code), `docs/game/running.md` (every new flag of the series in its place, the day one walkthrough
   ending at the inn), `docs/game/playtest.md` and `docs/game/lessons.md` (day one ends at the inn; day 2 offers land), `content/README.md`
   (the new data files and kinds: `land.json`, `grant_lodging`, `sell_plot`, the camp shape, the checklist `my-land`),
   `docs/process/documentation.md` (the Y series in the list of series, if it lists them). Check every statement you write against the
   code; remove or correct what the series made false (grep the docs for `firering.player`, `light-fire`, `spare-torch`, «mi campamento»,
   "own fire", "up the trail", `camp.fire`, `chest.player`, `save-v1`, `save-v4`).
3. **CLAUDE.md's fixture line:** check what CLAUDE.md says about the save fixture and the bundle hash; if it is out of date, give the
   exact replacement line in your report.
4. **The known gaps the series leaves**, as a list in your report with one line of reason each, for the orchestrator's open items: the
   fire's sound at a placed camp (if Y12 left it), «mi campamento» not answered when asked for directions, one plot only, no meals at the
   inn, the take-back hint on scene planks (Y09), the price's plural ("1 coins", Y10), the outline's size limit (Y10), `patience`
   counting every line at the inn (Y06), and any other the reports name.
5. **The gate** (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4; scripts/test.sh full (report
   the count against Y12's); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes; scripts/limited.sh scripts/content.sh
   validate; the A2 content gate (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`).
   Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; the three runs (what each covers, their console's key lines, the twice-run
comparison); the documents changed and what was false and is now corrected; CLAUDE.md's line; the known gaps; any bug found (with its
reproduction); test counts. Leave out how you searched.
