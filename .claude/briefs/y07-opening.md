You are implementing Y07, "The opening's logic", for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp,
the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the engine-free logic),
Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone is game logic in Vocaterra.Game.Core with some
content (content/game-v1); the core is taken as it is.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y07, which already exists on the branch `Y07-opening` (cut from
`feature/settlement` after Y06, the innkeeper, and Y06m, the merge of `main` with the weather series, were recorded; the sleep now wakes in
the morning and the save carries `systems.atmosphere`). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y07 &&` and
use absolute paths for file tools; your shell may start in another checkout, which you must not edit. Commit after each item that builds
and passes its tests, with the prefix `Y07: `. Do not push your branch. Never push or merge `feature/settlement` or `main`. When you finish,
leave the worktree on your branch, clean, with no process of yours still running.

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

Read first (lean: name the sections, never whole long files; never `speech-checklist.md`, `manifest.json` or the whole of `PHRASES.md`):
CLAUDE.md; docs/process/code-guidelines.md; content/README.md, "The edit loop" and the objectives' section; docs/game/settlement-plan.md
section 8.2 and the Y06, Y07 and Y08 paragraphs of 8.3 (Y08's tells you what is not yours); docs/game/decisions.md "D-game-32" and
"D-game-27", grep for them. The code: `Session/ObjectiveBook.cs` (the restore and completion hooks), `Session/VoxelSession.cs` (the hook
dispatch near `"spare-torch"` and `"scan-fire-ring"`, `ClosingLine`, `ClosingLineId`, `GrantLodging`), `Session/VoxelSession.Skip.cs`,
`Session/SessionSkip.cs`, `Session/SessionEnding.cs`, `Session/PlaytestFlow.cs`, `Session/PlaytestEnd.cs`, `Session/SleepOffer.cs`,
`Content/GameDataValidator.cs`, the objectives `game/objectives/camp.json` and `light-fire.json`, `game/session.json`,
`content/schema/objective.schema.json`, and in the game `SessionEndingFlow` (where the ending's voice is played). The reports of Y05 and Y06
in /home/james/Dev/Vocaterra/.claude/worktrees/settlement/.claude/briefs/ (`y05-report.md`, `y06-report.md`): the lodging, the
innkeeper's agent, her pools and her voice.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/locale, content/schema, assets/ and
docs/game/ and takes the core as it is: a line the core misreads is a core gap you report, never a patch in the game. A regression check
is a C# test, never a script with text baselines; tests are named Given_When_Then in folders that mirror the source; do not edit
CLAUDE.md, either implementation-plan.md, either decisions.md or docs/game/settlement-plan.md (the orchestrator does); engine code only
in src/Vocaterra.Game; no System.Random and no clock in the evaluation path; nothing under .github/workflows/. The content gate at the A2
ceiling must stay clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`), and
`content/PHRASES.md` is the source of truth for the learner's phrases. No save backward compatibility is needed (the game is unreleased):
old saves may break; write no migration code.

What this milestone is for. Day one used to end when the camp checklist was done and the player had lit their own fire up the trail
(the objective `light-fire`; the guide's closing line «¡Muy bien! Ya tienes fuego.»). From now on (D-game-32, 8.2): day one ends with
the introduction, Luna's camp task and a room at the inn. Lighting your own fire moves to the campsite (Y12). The mountain's tent, chest and
fire ring stay in the scene until Y08; this milestone only stops the opening from using them.

Deliverables:

1. **The objective `inn-room`** (`game/objectives/inn-room.json`, one step whose trigger is the world event `lodging_granted`; its
   restore from the save's `systems.sleep` lodging, by a restore hook if the trigger alone does not restore it on load: name it, add
   it to `ObjectiveBook.RestoreHooks`, the schema and the validator). Its HUD text in `locale/en/ui.json` ("Ask at the inn for a room"
   or near it) and any Spanish title the others carry. In `game/session.json` it takes the place of `light-fire` in `objectives`;
   `light-fire.json`, its manifest and provenance rows and its locale text go (or stay unused if a later milestone needs them: say
   which you chose and why; the campsite in Y12 adds its own steps to `my-land`, so nothing should need `light-fire`).
2. **The hooks retire:** `spare-torch`, `point-own-camp` and `scan-fire-ring` leave `ObjectiveBook`, `VoxelSession.cs`, the schema and
   `GameDataValidator`, with the code only they used (`GiveSpareTorch`, `PointAtOwnCamp`, `ScanPlayerFireRing` and whatever else
   nothing calls afterwards; leave `firering.player`, the fire-lit event and the torch hook at the player's ring alone if anything
   else still uses them, since Y08 removes the mountain camp and Y12 reuses the torch hook; say what you kept and why). A new
   completion hook `point-inn` on `camp.json`'s `onComplete`: the beacon points at the inn (the scene point `npc.innkeeper`, or the
   inn's door if the beacon needs a reachable cell; the point is content, not a constant in code).
3. **Luna's line that sends the player to the inn:** when the camp checklist completes, the guide says one short line (pool
   `guide.camp.inn` or near, under the `guide.*` host pools) such as «¡Muy bien! Ahora ve a la posada.» or «La posada está en el
   pueblo.» — short, A1 where possible, every word through the gate (`posada` is in the A1 world words since Y04). Find how the
   guide speaks a line when an objective completes today (grep the guide's pools that a completion or hand-over raises) and follow
   it; if no such path exists, the line goes with the `point-inn` hook. The English gloss in `locale/en/lines.json`; the speech in
   Luna's voice through `scripts/prerender-speech.sh`; the provenance rows. **The orchestrator reads every new Spanish line in your
   diff before merging**; keep a list with glosses in your report.
4. **The ending:** the closing pool (`session.json`'s `closingPool`) becomes the innkeeper's (a new pool `innkeeper.end` or reuse
   of her room-given pool: choose and say why; its text should fit the moment, for example «Buenas noches. Tu cama está allí.»).
   The ending takes its speaker from the closing pool's NPC (today it assumes the guide: find where `SessionEndingFlow` or the game
   picks the voice) and waits until the conversation that raised `lodging_granted` has closed, so the card does not cover her talk
   panel and her room line and the closing line do not overlap. The ending's English comes from the pool's gloss as now.
   `SessionComplete` must follow from the new objectives with no special case for the inn.
5. **The skip:** `VoxelSession.Skip.cs` (`SkipToEnding`, and `SessionSkip` for `--skip-to-session`) gives lodging (`GrantLodging`,
   raising `lodging_granted` as a real grant does) where it lit the player's ring; it stops placing the torch on the mountain ring.
   A skipped session 2 start finds the bed usable.
6. **The texts:** `PlaytestFlow` and `PlaytestEnd` (and the locale strings they show) where they speak of the fire or the camp as
   the end of day one; the sleep card (`SleepOffer`, `ui.sleepCard*`) if it names the fire. Grep `locale/en/ui.json` and
   `tips.json` for "own fire", "up the trail", "your camp" and change only what day one's flow says; the mountain's strings and the
   console's sleep line are Y08's.
7. **The content loop:** `scripts/content-loop.sh --grammar --speech` after each content edit; `--record` only if the end-to-end
   goldens move (say which and why); `--tier full` once before the report. Read every grammar finding. The new bundle hash goes into
   the save fixture as CLAUDE.md says (the loop does it).
8. **Tests** (through the session, not reflection): from a new session, completing the camp checklist points the beacon at the inn
   and has Luna say her line; taking the room at the innkeeper completes `inn-room`, and the session completes; the ending begins only
   after her conversation closes and its line and speaker are hers; `light-fire` no longer exists and a bundle naming one of the
   retired hooks fails validation with a clear message; a save taken after the grant restores `inn-room` as done without raising the
   event again; `--skip-to-session 2` (SessionSkip) leaves lodging given. Change the existing tests that name `light-fire`, the
   retired hooks or the old closing line; list them in the report.
9. **Runs in the game, from a new world** (through limited.sh, `--mute --needs off`): the whole of day one with the dev aids
   (complete the camp checklist the way the existing scripted runs do, then `--teleport 280,221 --walk-to 280,227 --talk innkeeper
   --say "sí"`, wait for the ending, sleep in the bed, `--save`, `--screenshot`); report the console's lines (Luna's inn line, the
   beacon, the grant, the ending's line and speaker, the sleep) and the save's objectives and `systems.sleep`. One run with
   `--skip-to-session 2` that sleeps in the bed with no `--lodging`. Screenshots of the beacon toward the inn and of the ending's
   card into `artifacts/y07/` (git-ignored).
10. **The scripted runs:** capture `r01`, `r10`, `r20` to `r24`, `r26`, `r27` and `r30` before your first change and after your
    last (a temporary detached worktree of `feature/settlement`'s base commit for the "before", under
    /home/james/Dev/Vocaterra/.claude/worktrees/, removed afterwards). Many will differ (the opening changed): for each, say what
    differs and whether it is the intended change. Do not re-baseline them: Y08 captures them again after the mountain goes.
11. **Documents:** docs/game/lessons.md, running.md and playtest.md where they describe day one's end (the inn, not the fire);
    content/README.md where it lists the hooks; src/Vocaterra.Game.Core's or src/Vocaterra.Game's README if they name the hooks.

Not in this milestone: removing the mountain's tent, chest, crate, `firering.player` or the mountain's sleep place, `death.wakeAt`, the
console's sleep line, `hud.camp`, the ambient beds, the tests that name `firering.player`, the digest (all Y08); the innkeeper's agent
beyond the ending's needs (Y06 built it); land, the stake, the mayor, the camp kit (Y09 to Y12); any `scene.json` edit.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count; the feature branch's count after Y06 is in y06-report.md); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; the A2 content gate above clean; the full suite once
without models (`VOCATERRA_MODELS_DIR` an empty folder) passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built; every new Spanish line with its gloss; the hooks retired and the
code removed or kept; the ending's speaker and wait; test counts and the tests added or changed; the runs in the game; the scripted runs'
differences; core gaps; anything Y08, Y12 or Y13 should know; known-not-fixed with reasoning; anything left undone and why. Leave out
how you searched.
