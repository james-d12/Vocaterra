You are implementing Y11, "Buying the plot", for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp,
the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* projects; the game is Vocaterra.Game.Core (the engine-free logic),
Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone is game logic (a host-event kind, one
exchange that cannot half happen) and content (the mayor's title, lines, phrases, speech and a checklist); the core is taken as it is.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y11, which already exists on the branch `Y11-buy-plot` (cut from
`feature/settlement` after Y10 was recorded). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y11 &&`
and use absolute paths for file tools; your shell may start in another checkout, which you must not edit. Commit after each item that
builds and passes its tests, with the prefix `Y11: `. Do not push your branch. Never push or merge `feature/settlement` or `main`. When
you finish, leave the worktree on your branch, clean, with no process of yours running. Another agent builds Y08 (the mountain, the strings, the rebaseline, two-block beds) at the same time in `.claude/worktrees/y08`: it
edits `scene.json`, `needs.json`, the bed blocks and `ChunkMesher`, the sleep places, `AmbientBeds`, the innkeeper's agent
(`agents/innkeeper.json`, her state after lodging), `scripts/scripted-runs.sh` (the CAMP steps, r10, r20, r30) and the mountain's
locale strings, and re-baselines the scripted runs. Stay out of those; add your locale strings as new keys only (at the end of their
block). Whichever of you is merged second merges the first and hashes the bundle again; the orchestrator will tell you.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
the prerender and every screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build
Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly
(content-loop refuses to run under limited.sh). Never start two heavy commands at once (no `&`, no background builds, no parallel test
runs), and write no shell wait loop on `pgrep -f`. Exit 137 means the 6 GB cap killed it: lower the parallelism. Set
`VOCATERRA_RUNS_WORK` to a folder whose name is unique to Y11 (in your scratchpad) for every `scripts/scripted-runs.sh` call.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`: for the core's CLI `eval` and for
`scripts/content.sh hash`, write a wrapper script in your scratchpad and run that). Model-gated tests and the speech need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first (lean: name the sections, never whole long files; never `speech-checklist.md`, `manifest.json` or the whole of `PHRASES.md`,
grep its rows): CLAUDE.md; docs/process/code-guidelines.md; docs/process/milestone-briefs.md, "What a content milestone adds";
content/README.md (NPCs, host events, objectives and checklists, "The edit loop", the level lists' world words); docs/game/settlement-plan.md
section 5 "Ownership and camp placement" (the purchase checks again and transfers coins and ownership as one operation; cancelling, a
failed check, a repeated confirmation or a reload never duplicate payment, land or equipment), 8.2, the Y09 to Y12 paragraphs of 8.3 with
their as-built notes, sections 9.3 (the price) and 9.4 (the language probe: what the core reads and what it does not); docs/game/decisions.md
"D-game-32" (points (c), (e)) and "D-game-27" (no one is locked out by Spanish), "D-game-25" (orders open at the first talk). The reports
in /home/james/Dev/Vocaterra/.claude/worktrees/settlement/.claude/briefs/: `y09-report.md` (`OwnPlot()`, `PendingPlot`, `PlotPrice`,
the refusal reasons), `y10-report.md` (the stake item, the pending plot it marks, `--mark-plot`), `y06-report.md` (an NPC's agent and
`patience`, as the innkeeper does it), `y06m-report.md` ("For Y07 to Y13": every NPC needs `weatherAnswers`, the
`<id>.sun/.cloudy/.rain/.fair` transitions; a pool moves `SpeechLinesTests` and the identity baseline). The code:
`Npc/HostEventTable.cs` and the dispatch of host-event kinds in `VoxelSession.cs` (as Y05 added `grant_lodging`), `Npc/NpcHostEvents.cs`
(the pre-checks), `NpcService.Miss` and the validator's miss states (grep how an existing kind with misses, such as the sale of bread or an
order, declares and answers them), `GuideScene` (the facts the dialogue worker gets), `Items/Inventory.cs` (`CanExchange`), the
villager Tomás (`game/npcs/villager.json`, `agents/villager.json`, his `give_item` row in `host-events.json`), an existing checklist
objective with `"session"` (grep `game/objectives/*.json`).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/locale, content/schema, assets/ and docs/game/
and takes the core as it is: a line the core misreads is a core gap you report, never a patch in the game. A regression check is a C#
test, never a script with text baselines; tests are named Given_When_Then in folders that mirror the source; do not edit CLAUDE.md,
either implementation-plan.md, either decisions.md or docs/game/settlement-plan.md (the orchestrator does); engine code only in
src/Vocaterra.Game; no System.Random and no clock in the evaluation path; nothing under .github/workflows/. The content gate at the A2
ceiling must stay clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`), and
`content/PHRASES.md` is the source of truth for the learner's phrases. No save backward compatibility.

What this milestone is for (D-game-32, 8.2): the village's mayor gives the player a stake, the player marks a plot in a meadow with it
(Y10), and buys it from the mayor for 10 coins (9.3); the purchase gives the plot and the camp kit at once. The land's steps are one
checklist, «Mi terreno», shown from day 2, which never holds sleep back. Nobody is locked out by Spanish: «no sé» or two misses help as
the innkeeper's `patience` does, but never hand over land without the coins (the coins are the game's rule, not the language's).

Deliverables:

1. **The mayor:** Tomás, the villager, is the mayor: his `label` becomes «Alcalde» (`alcalde` joins the level list's world words at the
   end of `cefr/a1.txt` with one reason and `lexicon/game.json` with its forms, as `posada` did; `terreno` the same, D-game-32 (e)),
   `npc.villager.role` "The mayor". Keep his directions, his bread and his weather as they are. His agent gains what the land needs: on
   day 2 or later (the checklist's session), when the player has no stake and no plot, his talk offers the stake (a yes-or-no prompt,
   `mayor-stake` or near); «sí» gives it through the existing `give_item` kind (`villager.give_stake`, item `stake`); once a plot is
   pending, his talk offers the sale (`mayor-sell`): «sí», «Quiero un terreno, por favor.», «¿Cuánto cuesta el terreno?» (he says the
   price), «Quiero la tierra.» as the core reads them (9.4: `request_item` {terreno}/{tierra}, `ask_price`, `confirm`; check each at
   your prompts with the core's CLI and report the readings; «Quiero comprar un terreno» and «Quiero este terreno» are known gaps:
   say what they do now and pick lines that read for the phrases).
2. **The `sell_plot` kind:** a host-event kind in `HostEventTable`, the schema and the validator, dispatched in the session (in
   `VoxelSession.Land.cs` or beside Y05's `GrantLodging`). Its pre-checks, in `NpcHostEvents` like the others, give the misses
   `no_plot` (nothing pending: he says to mark it with the stake, and gives a stake if the player has none), `cant_pay` (fewer coins than
   `PlotPrice`: he says the price and how to earn coins, for example selling wood to the baker), `no_room` (the bag cannot take the camp
   kit: he says so), `already_owned` (he says the land is the player's). Each miss state is declared as the validator wants and has its
   pool. The world event `plot_bought` (also an autosave reason, as `lodging_granted` is).
3. **One exchange that cannot half happen:** `Inventory.CanExchange` first (the coins out, the camp kit in), then `OwnPlot()` (Y09; it
   re-checks the world and may refuse), then the exchange, in that order, so that a refusal at any point leaves coins, bag and land as
   they were. Repeating «sí», a reload after the purchase, or a second talk never pays twice or gives a second kit (`already_owned`).
   Tests for each.
4. **The camp kit item** `camp-kit` (Y12 makes it placeable; here it is an item the mayor gives): `Items/ItemDefinition.cs`, its noun
   (find a Spanish noun that passes the A2 gate and does not clash with `tienda`, the stall's label: for example «el equipo de
   campamento» if it reads, or another; say what you chose and why; the orchestrator reads it), its English name, its icon drawn in code
   like the others, stacks of 1.
5. **The checklist `my-land`** («Mi terreno», `"session": 2`): steps `stake` (the stake in the bag or a plot pending: choose the trigger
   that a load restores) and `buy` (`plot_bought`, restored from `systems.land.owned`); Y12 adds `camp` and `fire`. It never holds sleep
   back and does not end a session. Its HUD text in the locale. When a plot is pending and not bought, the arrow (beacon) points at the
   mayor; after the purchase, at the plot (the beacon's point is the owned plot's centre: say how the beacon takes a point that is not
   a scene point).
6. **The lines** (pools `mayor_*` or `villager_land_*`): the stake's offer («Hola. ¿Quieres un terreno? Toma esta estaca.» or near), the
   stake given («Pon la estaca en el prado.» or near: `prado` must pass the gate; or «…cerca del pueblo»), the sale's offer with the
   price («El terreno cuesta diez monedas. ¿Lo quieres?» or near), the sale done («¡Muy bien! El terreno es tuyo. Toma el equipo.» or
   near), each miss, the help lines for «no sé» and two misses. Short, A1 where possible, every word through the gate; glosses in
   `locale/en/lines.json`. **The orchestrator reads every new Spanish line in your diff before merging**; list each with its gloss in
   your report. His speech in his voice (`em_santa`) through `scripts/prerender-speech.sh`; the provenance rows.
7. **The learner's phrases:** rows in `content/PHRASES.md` and `game/phrases.json` for «Quiero un terreno, por favor.» and «¿Cuánto
   cuesta el terreno?» (home prompt `mayor-sell`), `focus` on the prompts, the «sí, por favor» row where it fits.
8. **The content loop:** `scripts/content-loop.sh --grammar --speech` after each content edit; `--record` only if the end-to-end goldens
   move (say which and why); `--tier full` once before the report. The new bundle hash goes into the save fixture as CLAUDE.md says.
9. **Tests** (through talk turns, not reflection): from a new session 2, the mayor offers the stake and «sí» gives it; with no plot
   pending, «sí» at the sale gives `no_plot`; with a plot pending and 9 coins, `cant_pay` and nothing changes; with 10 coins, the
   purchase: coins 10 less, the plot owned, the camp kit in the bag, `plot_bought` once, `my-land`'s `buy` step done; a full bag gives
   `no_room` and nothing changes; a second «sí» or a reload gives `already_owned` and nothing more; a plot that became invalid between
   marking and buying (a test that changes the world) is refused with nothing changed; the validator accepts the bundle and refuses a
   `sell_plot` row with a missing miss state.
10. **Runs in the game, from a new world** (through limited.sh, `--seed 42 --mute --needs off --dev --skip-to-session 2`): talk to the
    mayor («sí»: the stake), `--mark-plot` in the north meadow (or use the stake), give coins as the aids allow (`--give moneda` ten
    times or the aid's form), talk to him again («Quiero un terreno, por favor.», then «sí» if he asks), `--save`; report the console's
    turns, the events and `systems.land`, the bag. The same with 9 coins (`cant_pay`), and with two misses at the sale. Screenshots of
    the talk panel and of the owned outline into `artifacts/y11/` (git-ignored).
11. **Unchanged behaviour:** capture `r03-npcs`, `r21-skip`, `r26-death` and any run that talks to Tomás before your first change and
    after your last; report each difference and why (his new label shows in some).
12. **Documents:** content/README.md (the kind, the item, the checklist), docs/game/lessons.md or the NPC list the game docs keep,
    docs/game/architecture.md (the exchange, a few lines), docs/game/saves.md if `systems.land` gains anything.

Not in this milestone: placing the camp kit, the camp's sleep places, planting on the plot (Y12); more than one plot; selling land back;
a fee at the inn.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count; 11,323 passed and 3 skipped after Y10); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; the A2 content gate clean; the full suite once without
models (`VOCATERRA_MODELS_DIR` an empty folder) passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built; every new Spanish line with its gloss and every new word (alcalde,
terreno, the kit's noun, prado or what you used); the core's reading of each learner line at each prompt; the exchange's order and what
each refusal leaves; test counts and the tests added or changed; the runs in the game (three cases); the scripted runs' comparison; core
gaps; what Y12 should call and know; known-not-fixed with reasoning; anything left undone and why. Leave out how you searched.
