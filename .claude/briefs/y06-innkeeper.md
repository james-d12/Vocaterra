You are implementing Y06, "The innkeeper", for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp,
the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the engine-free logic),
Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone is mostly content (content/game-v1), with the
NPC's figure in the game; the core is taken as it is.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y06, which already exists on the branch `Y06-innkeeper` (cut from
`feature/settlement`). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y06 &&` and use absolute paths for file
tools; your shell may start in another checkout, which you must not edit. Commit after each item that builds and passes its tests, with
the prefix `Y06: `. Do not push your branch. Never push or merge `feature/settlement` or `main`. When you finish, leave the worktree on
your branch, clean, with no process of yours still running.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
the prerender and every screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build
Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly
(content-loop refuses to run under limited.sh). Never start two heavy commands at once (no `&`, no background builds, no parallel test
runs), and write no shell wait loop on `pgrep -f`. Another session builds on this machine, so a heavy command may wait for a slot: let
it wait. Exit 137 means the 6 GB cap killed it: lower the parallelism. Set `VOCATERRA_RUNS_WORK` to a folder of your own (in your
scratchpad) for every `scripts/scripted-runs.sh` call, the same folder before and after.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`: for the core's CLI `eval` and for
`scripts/content.sh hash`, write a wrapper script in your scratchpad and run that). Model-gated tests and the speech need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first (lean: name the sections, never whole long files; never `speech-checklist.md`, `manifest.json` or the whole of `PHRASES.md`,
grep its rows): CLAUDE.md; docs/process/code-guidelines.md; docs/process/milestone-briefs.md, "What a content milestone adds";
content/README.md, "Adding an NPC" (your seven steps) and "The edit loop"; docs/game/settlement-plan.md section 8.2, the Y05, Y05a, Y06
and Y07 paragraphs of 8.3, and sections 9.1 (the inn's cells), 9.4 (the language probe: what the core reads) and 9.5 (the voice);
docs/game/decisions.md "D-game-32" (points (b), (e), (f)) and "D-game-27" (no one is locked out by Spanish), grep for them. The worked
examples: the shopkeeper (`game/npcs/shopkeeper.json`, its agent `agents/shopkeeper.json`, its prompts `stall-need` and `stall-offer`,
its `patience` block and its pools `shop_*`) and the baker Luz (`game/npcs/baker.json`). The reports `.claude/briefs/y04-report.md`
(the keeper's cell and the counter) and `.claude/briefs/y05-report.md` (the action row) in the settlement worktree
(/home/james/Dev/Vocaterra/.claude/worktrees/settlement/.claude/briefs/), and `.claude/briefs/y05a-report.md` (a sleep place is "at" only on foot from its cell, so the bed's prompt stops at the walls).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/locale, content/schema, assets/ and
docs/game/ and takes the core as it is: a line the core misreads is a core gap you report, never a patch in the game. A regression check
is a C# test, never a script with text baselines; tests are named Given_When_Then in folders that mirror the source; do not edit
CLAUDE.md, either implementation-plan.md, either decisions.md or docs/game/settlement-plan.md (the orchestrator does); engine code only
in src/Vocaterra.Game; no System.Random and no clock in the evaluation path; nothing under .github/workflows/. The content gate at the A2
ceiling must stay clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`), and
`content/PHRASES.md` is the source of truth for the learner's phrases (D-game-10, D-game-12).

What this milestone is for. The inn (Y04) has a bed whose access is `lodging`, and Y05 added the host-event kind `grant_lodging`
(the world event `lodging_granted`). Y06 adds the innkeeper who gives the room. The owner's rules (D-game-32 (b), 8.2's first bullet):
the bed is free and the room is given for good; the innkeeper offers the room when the talk opens; «sí» or asking for it takes it;
«no sé» or two misses give it anyway (the shopkeeper's `patience`, D-game-27); a later visit says goodnight. Nobody is locked out of
sleep by Spanish.

Deliverables:

1. **The NPC `innkeeper`** by the seven steps of "Adding an NPC": `label` "Posadera" (check it passes the gate; if not, find the
   nearest label that does and say so), `name` "Rosa", `figure` "innkeeper", its id at the end of `game/npcs.json`. The agent
   `agents/innkeeper.json`: an idle state that offers the room on a yes-or-no prompt (`inn-offer`, after `stall-offer`), which takes
   «sí», «sí, por favor», «Quiero una habitación, por favor.», «Una habitación, por favor.» and «¿Hay una habitación?» (all read
   today, section 9.4) and raises the host event that grants the room; a state after the room is given (`inn-given`) that greets and
   says goodnight and takes a greeting or thanks. «No, gracias» leaves the offer open for the next talk. `patience` gives the room
   after «no sé» or two misses (`misses`, `afterHelp` as you judge from the shopkeeper's; `once`). Its opening event: the offer at
   the first talk (`first`), the goodnight state's greeting after.
2. **The host event row** in `game/host-events.json`: `{"npc": "innkeeper", "event": "innkeeper.room", "action": {"kind":
   "grant_lodging"}}` (the event's name is yours), and the same event from `patience`.
3. **The lines** (pools `innkeeper_*`): the offer («¡Hola! ¿Quieres una habitación?» or near it), the room given (it names the bed:
   «Tu cama está allí.», «La habitación es gratis.» or near), the goodnight line, the answer to «no», a help line for a miss. Short,
   A1 where possible, every word through the gate; the English glosses in `locale/en/lines.json`; the role `npc.innkeeper.role` in
   `locale/en/ui.json`. **The orchestrator reads the new Spanish in your diff before merging**; keep a list of every new line with
   its gloss in your report.
4. **The learner's phrases:** rows in `content/PHRASES.md` and `game/phrases.json` for «Quiero una habitación, por favor.» and
   «¿Hay una habitación?» (their home prompt `inn-offer`), `focus` on the prompts that are their home, and the existing «sí, por
   favor» / «no, gracias» rows where they fit. Try every learner line first through the core's CLI (`tools/Vocaterra.Nlp.Cli -- eval
   --transcript <line> --prompt inn-offer --content content/game-v1`, through your wrapper) and report the reading of each. A wrong
   reading is a core gap: report it; do not work round it in content beyond choosing lines that read.
5. **The scene:** the NPC in `game/scene.json` `npcs` at the point `npc.innkeeper` (282, 227), facing west, behind the counter. The
   player talks from (280, 227) facing east (section 9.1). Check `--talk innkeeper` reaches her and that no one talks to her through
   the walls from the street.
6. **The figure** `innkeeper` in `Graphics/Figures/NpcFigures.cs` (colours, geometry, face cell), distinct from the others at a
   glance; a screenshot of her behind the counter.
7. **The voice:** `pf_dora`, speaker 42 (D-game-32 (f), provisional), with a `gain` that matches the Spanish voices' loudness (the
   trial measured RMS 1800; compare with the voices in use and say what you chose). Render with `scripts/prerender-speech.sh` (it
   renders only the new NPC's files); the provenance items for each new file.
8. **The content loop:** `scripts/content-loop.sh --grammar --speech` after each content edit; `--record` only if the end-to-end
   goldens move (say which and why); `--tier full` once before the report. Read every grammar finding. The new bundle hash goes into
   the save fixture as CLAUDE.md says (the loop does it).
9. **Tests:** a test through a talk turn (not reflection) that «sí» at `inn-offer` grants lodging, raises `lodging_granted` once and
   moves the agent to `inn-given`; one for «Quiero una habitación, por favor.»; one for `patience` (two misses give the room); one
   that a second visit does not grant again; the validator accepts the bundle. Use the existing NPC tests as models (grep the tests
   that drive the shopkeeper's or the baker's talk). If Y05's `LodgingTests` reached `ApplyHostEvents` by reflection, replace that
   test with one through the innkeeper's talk turn.
10. **Runs in the game, from a new world:** `--seed 42 --mute --needs off --dev --skip-to-session 2 --teleport 280,221 --walk-to
    280,227 --talk innkeeper --say "sí" --wait 2 --walk-to 278,231 --wait 1 --sleep --wait 2 --save <file> --screenshot <png>
    --frames 30` (adjust as the aids need); report the console's turn, the events and the save's `systems.sleep`. The same with
    «Quiero una habitación, por favor.», and with two misses («perro», «azul» or whatever misses). Screenshots of her, of the talk
    panel and of the room given into `artifacts/y06/` (git-ignored).
11. **Unchanged behaviour:** capture `r03-npcs`, `r21-skip` and `r26-death` before your first change and after your last and compare
    them; report any difference and why (a new NPC may add a line to the NPC list in some runs: say so).
12. **Documents:** content/README.md (the innkeeper among the worked NPCs where it lists them), docs/game/lessons.md or the NPC list
    the game docs keep (grep for "baker" in docs/game to find them), src/Vocaterra.Game/README.md if it lists the figures.

Not in this milestone: the objective `inn-room`, any change to `session.json`, Luna's line that sends the player to the inn, or the
ending (Y07); the mountain's camp and the console's sleep line (Y08); any change to the inn's cells (the counter a player can step
onto is Y08's); `terreno`, `alcalde`, `estaca` (Y10, Y11), unless a line here needs one; a lesson for the innkeeper.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count; the feature branch has 10,763 passing and 3 skipped today); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes
clean; scripts/limited.sh scripts/content.sh validate passes; the A2 content gate above clean; the full suite once without models
(`VOCATERRA_MODELS_DIR` an empty folder) passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built; every new Spanish line with its gloss; the core's reading of
each learner line; the voice and gain; test counts and the tests added or changed; the runs in the game (three cases); the scripted
runs' comparison; core gaps; anything Y07, Y08 or Y11 should know; known-not-fixed with reasoning; anything left undone and why.
Leave out how you searched.
