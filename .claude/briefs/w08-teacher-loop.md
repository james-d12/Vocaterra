You are implementing the game milestone **W08, the teacher in the loop**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game and its content**: while a procedure runs at a station, its teacher (the first is Luz, `baker`) listens at a constrained teaching prompt, and what the learner says there repeats, slows, helps, gives the missing ingredients or teaches a recipe.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/w08`, already on the branch `W08-teacher-loop` (from `feature/bread-pilot` at its tip, with W05 to W07b and T8 merged). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`.claude/worktrees/adaptive` is the owner's playtest checkout: never touch it); do not switch branches.
- **Commit after each item that builds and passes its tests**, with the prefix `W08: `. Never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there. Never push or merge `feature/bread-pilot`, `feature/adaptive_design` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish in one turn.** Do not start background commands or monitors and end your turn to wait for them; earlier agents did that and stopped with their work unchecked. The content loop and a scripted run take minutes in the foreground; wait for them.

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/w08/` for your logs and session folders, and **9759 passed and 3 skipped** as the count before you. This milestone **does** edit `content/game-v1`, so the Rules' last point applies: `scripts/content-loop.sh --speech` after each content edit (run directly, never under limited.sh) and `scripts/content-loop.sh --speech --tier full` once before the report instead of `scripts/test.sh full`. Read `content/README.md` for how to edit content and `docs/nlp/changing-the-core.md`'s part on the shared bundle (the goldens and `save-v1.json`'s bundle hash after a re-hash).

## The specification
- `docs/game/activities-plan.md`: section 4.4 (`sed -n 127,133p`) and section 6's paragraph **W08** (`sed -n 283,288p`). That paragraph's code, content and tests are the milestone.
- The owner's design document, extracted one paragraph per line at `/tmp/claude-1000/orch/adaptation-design.txt`: **10.14 "The teacher in the loop" (`sed -n 972,999p`)**: the teaching state, the table of lines, intents and host kinds, starting the first bake, the request to be taught; **decision 20 "Ingredient grants" (`sed -n 1469,1471p`)**. Read only those ranges.
- Decision 20 is adopted provisionally (`docs/game/decisions.md`, `sed -n 430,437p`): where it leaves a value open (which bake states permit a grant), take **the smallest choice that keeps the no-dead-end rule and saves every persistent state**, and list it in the report for the owner. The class's do step is W10's lesson: leave a clearly named way for W10 to allow the grant there, and build no lesson.
- The spoken-lines answers (`docs/game/decisions.md`, `sed -n 351,364p`) are for free talk and are M80/V31's work, not yours: at the teaching prompt, a spoken line outside the answer set still reads as its nearest answer (10.14's last sentence).
- **What W05 to W07b built** (their reports in `.claude/briefs/`: `w06-report.md`, `w07-report.md`, `w07b-report.md`): `GuideSession.StartProcedure`, `ActInProcedure`, `HelpInProcedure`, `Procedures.Repeat/Slower/Leave/Abandon/View/Progress`, `BakeMastered`; the station's hook **`StationPanel.OnTalk`** (`Action<StationTalk>`: Speak for V/Space, Type for W; null until you): push-to-talk and typing in the station view go to the teacher through it.
- **Where the pattern lives:** `HostEventTable` and how host events are applied on the game thread; `NpcService.PromptAfter` and how an NPC's agent states and prompts are loaded (`content/game-v1/agents/*.json`, `prompts/prompts.json`, `answer-sets/`, `game/npcs/*.json`); V29's reply-pool declarations in `game/npcs/*.json` (its report, `.claude/briefs/v29-report.md`). All the intents of 10.14's table already exist in `content/game-v1/intents/intents.json` (`ask_repeat`, `ask_slower`, `ask_help`, `dont_know`, `ask_availability`, `request_item`, `thank`, `confirm`, `deny`, `ask_craft`).
- Where the design and the code disagree on a file or a name, the code wins; say so in the report. List every value you choose where the design leaves one open.

## What to build (each committed)
1. **Core of the game (`Vocaterra.Game.Core`):** the host kinds `procedure_repeat`, `procedure_slower`, `procedure_help`, `give_missing`, `teach_recipe` in `HostEventTable` (and `game/host-events.json`'s schema, as `docs/game/activities-plan.md`'s content table, `sed -n 446,458p`, says), each reaching the runner on the game thread; `<npc>.teach_start` and `<npc>.teach_end` raised as host turns when a procedure with a teacher starts and ends; the agent at its `teaching` state's prompt while it runs.
2. **The daily grant:** `give_missing` adds the procedure's missing `uses` to the bag, always for the first guided bake and then at most once per in-game day; persisted in the save (`SystemsSave`, as W06 added `procedures`), so repeated requests and a reload do not duplicate it. Every save version from 1 still loads.
3. **`teach_recipe`:** adds the procedure to the learner's known list (the list W30's recipe book will show; name it plainly).
4. **The station's talk:** `StationPanel.OnTalk` sends push-to-talk (V/Space) and typing (W) to the teacher at the teaching prompt; the teacher's answer line shows and plays as in a conversation (`NpcVoice`), without closing the station.
5. **Content, the first instance (Luz):** `agents/baker.json` gains the `teaching` state; `prompts/prompts.json` `baker-proc` with an answer set of 10.14's lines; `game/npcs/baker.json` names it; Luz's `ask_craft` answer and the offer of 10.14 ("¿Quieres hacer pan conmigo?"; Sí opens the station at rung 1 and enters teaching; No, gracias costs nothing; once per in-game day until the player has baked). **The bundle has no `pan` procedure or station until W10**: the offer and the teaching state must stay silent in play while the teacher has no procedure in the bundle (the game unchanged), and work as soon as one exists. New Spanish lines: as few as the table needs, A2, in Luz's voice, passing the content loop's lint and grammar check; list every new Spanish line in the report (the orchestrator has them reviewed). Recordings through the content loop's `--speech`.
6. **The request to be taught** ("¿Me enseñas a hacer pan?"): check in the core's CLI how it is read today (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "¿me enseñas a hacer pan?" --prompt baker-open --content content/game-v1`, through `limited.sh`) and report the verdict. **Do not patch the core**: if it needs a core intent, say so in the report; the offer and a "Hacer pan" button stay the routes.
7. **Tests:** the agent's transitions at `baker-proc` with typed lines; each kind reaches the runner; the daily grant survives a reload and is not duplicated by a repeated request, with the first-bake rule; `teach_recipe` adds the procedure to the known list; the offer absent without a procedure in the bundle; the station's talk reaches the teacher and leaves the station open. Use a test bundle or fixtures for the procedure, never `content/game-v1`.

**Screenshot.** As W07 did, copy the bundle to `/tmp/claude-1000/w08/bundle/`, add a test station and procedure there (never in `content/game-v1`), re-hash the copy through a wrapper script, and capture the station after a typed `Otra vez, por favor.` and after `¿Tienes harina?` (or the test procedure's missing item): Luz's answer showing, the station still open. Look at the PNGs. Delete the copy when done.

Out of scope: flour, the bread procedure and the lesson (W10); a core intent for being taught (a core milestone, if needed); fail-forward patience (W03); prices (W04); telemetry (W11); the recipe book (W30).

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 500 words)
- The branch and the final commit SHA (local, not pushed).
- Each deliverable: what you built (file and type), its tests.
- **Values chosen** where the design left one open (decision 20's grant states first), and every place the design and the code disagreed.
- Every new Spanish line, with its row id.
- The CLI's verdict on "¿me enseñas a hacer pan?" and whether a core intent is needed.
- The screenshots' paths (keep the PNGs under `/tmp/claude-1000/w08/`) and what each shows.
- The scripted runs' differences (none expected after T8), the goldens re-recorded if any and why, the test count before and after, anything left undone and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs|content-loop'`, kill only your own).
