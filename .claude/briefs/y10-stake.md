You are implementing Y10, "The stake and the plot's outline", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* projects; the game is Vocaterra.Game.Core (the engine-free logic),
Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone is an item, an input hook, an overlay and a
HUD line, on top of Y09's land rules; the core is taken as it is.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y10, which already exists on the branch `Y10-stake` (cut from
`feature/settlement` after Y09, land rules, was recorded). Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/y10 &&` and use absolute paths for file tools; your shell may start in another checkout,
which you must not edit. Commit after each item that builds and passes its tests, with the prefix `Y10: `. Do not push your branch. Never
push or merge `feature/settlement` or `main`. When you finish, leave the worktree on your branch, clean, with no process of yours running.

Another agent builds Y07 (the opening's logic) at the same time in `.claude/worktrees/y07`: it edits `VoxelSession.cs` (the objective
hooks, the ending), the objectives, `session.json`, the skip, `PlaytestFlow`, Luna's pools and some locale strings. Stay out of those;
add your locale strings as new keys only (at the end of their block). After Y07 the Y08 agent will edit `scene.json`, `needs.json`,
the beds and the mountain's strings: stay out of `scene.json` and `needs.json`. Whichever of you is merged second merges the first and
hashes the bundle again; the orchestrator handles that. Use a scripted-runs work folder whose name is unique to Y10.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly (content-loop refuses to run
under limited.sh). Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait
loop on `pgrep -f`. The other agent's heavy commands share the slots, so a command may wait: let it wait. Exit 137 means the 6 GB cap
killed it: lower the parallelism. Set `VOCATERRA_RUNS_WORK` to a folder of your own (in your scratchpad) for every
`scripts/scripted-runs.sh` call.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`: for `scripts/content.sh hash`, write a
wrapper script in your scratchpad and run that). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first (lean: name the sections, never whole long files): CLAUDE.md; docs/process/code-guidelines.md; docs/game/architecture.md (Core
and Game); content/README.md (items, the lexicon and the level lists' world words); docs/game/settlement-plan.md section 5 "Ownership and
camp placement" (a world preview with keyboard controls and explanations beyond colour alone), 8.2, the Y09, Y10, Y11 and Y12 paragraphs
of 8.3, sections 9.2 and 9.6; docs/game/decisions.md "D-game-32" (grep for it) and its point (e) on the words. The report
/home/james/Dev/Vocaterra/.claude/worktrees/settlement/.claude/briefs/y09-report.md (the land rules' API: `LandRules`, `PlotCheck` and its
reasons, `SetPendingPlot`, `IsOwned`, `systems.land`, `--own-plot`) and `y06m-report.md` ("For Y07 to Y13": any new opaque mesh takes
`ChunkMesher`'s `sunTint`). The code: `Items/ItemDefinition.cs` (an item, its noun, its icon), `Input/WorldInput.cs` (the berry's
`_plant` hook is the model for a use-item hook), `Graphics/World/OverlayMeshes.cs`, `SceneDrawer.DrawWorld`, the HUD's hint line and
its lines, `Startup/LaunchOptions.cs` (`--give`, `--place`, how a scripted aid is added and listed in the help text).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/locale, content/schema, assets/ and docs/game/
and takes the core as it is. A regression check is a C# test, never a script with text baselines; tests are named Given_When_Then in
folders that mirror the source; do not edit CLAUDE.md, either implementation-plan.md, either decisions.md or docs/game/settlement-plan.md
(the orchestrator does); engine code only in src/Vocaterra.Game (the overlay and the input are there; the logic of marking stays in Core);
no System.Random and no clock in the evaluation path; nothing under .github/workflows/. The content gate at the A2 ceiling must stay clean
(`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`). No save backward compatibility.

What this milestone is for: the mayor will give the player a stake (Y11) and sell them the plot they mark with it. This milestone makes
the stake an item that, used on the ground, marks the pending plot (Y09's `SetPendingPlot`) round the cell aimed at, shows the plot's
outline in the world and a HUD line with its size and price, or says on the hint line why it cannot be marked there. Marking again
moves the pending plot and costs nothing (8.2).

Deliverables:

1. **The item `stake`** in `Items/ItemDefinition.cs`: its Spanish noun `estaca` (8.2 and D-game-32 (e): `estaca` joins the level list's
   world words at the end of `cefr/a1.txt` with one reason, as `posada` did in Y04, and `lexicon/game.json` with its forms; check
   `scripts/content.sh cefr-check` and the A2 gate stay clean), its English name in the locale, its icon (in the item atlas the way the
   other items' icons are made; CC0 only, drawn in code like the others), stacks of 1. It places no block. `--give estaca` (or the aid's
   form for items) gives it for tests and runs.
2. **The use-item hook** in `Input/WorldInput.cs`, as the berry's `_plant` is: with the stake in hand, the use key on a ground cell asks
   the session (Core) to mark the pending plot with that cell as its anchor (centre or corner: choose so the outline lands where the
   player expects, and say which), facing the world's axes. The answer is Y09's `PlotCheck`: a refusal puts its reason on the hint line in
   English (`ui.plot*` keys, one per reason: protected, outside the meadows, too steep, water, a tree, not ground; "You can mark land only
   in the meadows." and so on; say "the meadow north of the village" or similar where it helps the player find it), an acceptance sets the
   pending plot (saved by Y09's `systems.land`) and says so. A use while a talk or a panel is open does nothing. Using the stake inside the
   owned plot does nothing new (say what it says).
3. **The outline** `OverlayMeshes.PlotOutline`, drawn through `SceneDrawer.DrawWorld`: corner posts and an edge line on the ground
   following the terrain's height, for the pending plot; for the owned plot too (a different look that does not rely on colour alone: for
   example posts with a flag, or a solid edge versus a dashed one). It takes the `sunTint` if it is an opaque mesh. It is drawn only near
   the player (a distance you choose) and never through the HUD. A setting or key to hide it is not needed.
4. **The HUD line** while a pending plot exists and the player is near it or holds the stake: "Plot 10 × 10 · price 10 coins" (from
   `land.json` through Y09's data), and "Your land" for the owned one; English UI text from the locale, numbers never as colour.
5. **A launch flag for the scripted runs:** `--mark-plot x,z` (with `--dev` if the others need it) that marks the pending plot as the
   stake would, reporting the answer on the console; the help text and docs/game/running.md.
6. **Tests:** using the stake in a meadow on seed 42 sets the pending plot and saves it; in the village it refuses with the protected
   reason and leaves the pending plot as it was; marking again moves it; the outline's mesh has four posts on the plot's corners at the
   terrain's height (a mesh test as the other overlay tests do); the HUD line's text for a pending and an owned plot; the item's noun reads
   (the core's CLI on «una estaca» and «la estaca» at a villager prompt: report the readings; a wrong reading is a core gap you report).
7. **Runs in the game, from a new world** (through limited.sh, `--seed 42 --mute --needs off --dev --skip-to-session 2 --give estaca`):
   use the stake in the village (refused, the hint), in the north meadow (marked: the outline and the HUD line), then `--save` and report
   `systems.land`; one with `--own-plot` (Y09) to see the owned outline. Screenshots into `artifacts/y10/` (git-ignored) of the refusal,
   the pending outline from a few blocks away, and the owned outline.
8. **Documents:** content/README.md (the item and the word), docs/game/architecture.md (the hook and the overlay, a few lines),
   docs/game/running.md (`--mark-plot`), the Game README if it lists the overlays.

Not in this milestone: the mayor giving the stake and selling the plot, coins, the camp kit and the checklist `my-land` (Y11); the
campsite (Y12); `scene.json` or `needs.json` edits; any Spanish line spoken by an NPC.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count; 11,298 passed and 3 skipped after Y09); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes (after `hash` through your wrapper, and the new bundle
hash into the save fixture as CLAUDE.md says; `scripts/content-loop.sh` does both); the A2 content gate clean; the full suite once
without models (`VOCATERRA_MODELS_DIR` an empty folder) passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHA, worktree path; what was built (the item and word, the hook, the outline, the HUD line, the flag);
the anchor you chose; the hint lines; the core's reading of the noun; test counts and the tests added or changed; the runs in the game;
what Y11 and Y12 should call and know; known-not-fixed with reasoning; anything left undone and why. Leave out how you searched.
