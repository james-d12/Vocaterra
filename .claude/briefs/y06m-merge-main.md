You are implementing Y06m, "Merge main", for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp, the
Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* projects; the game is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game
(MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. The task: merge `origin/main` into the settlement series and resolve every
conflict so that both sides' behaviour survives.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y06m, which already exists on the branch `Y06m-merge-main` (cut from
`feature/settlement` after Y06, the innkeeper, was recorded). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y06m &&`
and use absolute paths for file tools; your shell may start in another checkout, which you must not edit. Run `git fetch origin` and
`git merge origin/main` (no rebase, no squash). Commit the merge once its conflicts are resolved and the solution builds (prefix
`Y06m: Merge origin/main (the weather series, PR #64) into the settlement series`, listing the conflicted files and how each was
resolved in the body); commit later fixes separately with the prefix `Y06m: `. Do not push your branch. Never push or merge
`feature/settlement` or `main`. When you finish, leave the worktree on your branch, clean, with no process of yours still running.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
the prerender and every screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build
Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly
(content-loop refuses to run under limited.sh). Never start two heavy commands at once (no `&`, no background builds, no parallel test
runs), and write no shell wait loop on `pgrep -f`. Another agent may build on this machine, so a heavy command may wait for a slot: let it
wait. Exit 137 means the 6 GB cap killed it: lower the parallelism. Set `VOCATERRA_RUNS_WORK` to a folder of your own (in your scratchpad)
for every `scripts/scripted-runs.sh` call.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`: for `scripts/content.sh hash`, write a
wrapper script in your scratchpad and run that). Model-gated tests and the speech need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first (lean): CLAUDE.md; docs/process/code-guidelines.md; docs/game/settlement-plan.md section 8.3's table and its Y01, Y02, Y05,
Y05a and Y06m paragraphs, and 8.4 (what the two series share); `main`'s weather plan (grep docs/ on `origin/main` for D-game-31 and the
E series' plan: what E01 to E08 built, the day clock, `systems.atmosphere`, the sleep that wakes in the morning); docs/game/saves.md on
both sides. The reports of this series in /home/james/Dev/Vocaterra/.claude/worktrees/settlement/.claude/briefs/ (`y01-*`, `y02-*`,
`y05-report.md`, `y05a-report.md`, `y06-report.md`) say what the settlement side built.

What each side did, where they meet:
- **Settlement (this branch):** sleep places (`SleepPlaces`, `needs.json`'s `sleepPlaces`, kinds `fire` and `bed`, access `always` or
  `lodging`, `AtSleepPlace`, `WakeAtSleepPlace` bounded by walls); save version 8 (current and oldest readable both 8, old-version
  branches removed, `systems.sleep`: the lodging given, the last place slept, the places made in play); one version 8 fixture in
  place of `save-v1.json` and `save-v4.json`; bed and bedroll blocks in `BlockId`/`ChunkMesher`; the inn in `scene.json` (and
  `house3` moved); `grant_lodging` and `lodging_granted`; the innkeeper Rosa (agent, pools, speech, figure).
- **Main (PR #64 and earlier):** the day and night clock, the weather and its drawing (`Atmosphere/`, much of `Graphics/`, `Sound/`,
  `Menu/`), `systems.atmosphere` on `SystemsSave` (main left the save at version 7), a sleep that wakes in the morning, new
  launch options, new NPC pools and speech (42 new wav files, the agents' weather lines), `day.json` and `weather.json`, and docs.
- **The rules for the meeting points:** the save stays version 8 with the oldest readable 8; `SystemsSave` carries both `Sleep`
  and `Atmosphere`; no migration code (the game is unreleased). Fixtures: keep this branch's version 8 fixture and bring into it
  whatever main's fixture edits added (`atmosphere` if main's fixture tests need it); main's edits to `save-v1.json` and
  `save-v4.json` go with those files. A sleep at any usable sleep place (the inn's bed included) behaves as main's sleep at the fire
  now does: it wakes in the morning by main's clock, and the settlement's "last slept" entry is still written after the restore.
  A death still wakes at the last sleep place, inside the inn when that was the bed. `VoxelSession`, `VoxelSimulation`, `WorldEvent`,
  `GameDataValidator`, `ChunkMesher` (both sides' block shapes), `VoxelGame`, `LaunchOptions` and `Program.cs` (both sides' options
  and help text) keep every feature of both sides. Tests: keep both sides' tests; where one side's test assumed the other's absence
  (for example a sleep test that expects the old save version, or main's sleep test at the mountain fire), change it to the merged
  behaviour and list it.
- **The bundle:** resolve `dialogue.json`, the agents, `provenance.json` and `locale/` as a union of both sides' entries; then
  `scripts/content.sh hash content/game-v1 --bundle-id game-v1` (through your wrapper), validate, the A2 content gate
  (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`), and
  `scripts/content-loop.sh --grammar --speech`, which checks the speech files against the lines (render nothing that exists; if a
  line has no speech, render it with `scripts/prerender-speech.sh` and say so). The new bundle hash into the save fixture as
  CLAUDE.md says. Record the end-to-end goldens (`--record`) and the scene's digests only where the merge moved them, and say which
  and why.
- **The documents:** resolve the docs as a union, keeping both sides' entries in order: `CLAUDE.md`, `docs/README.md`,
  `docs/open-items.md`, `docs/handovers/README.md` (both open handovers, both done lists), `docs/game/decisions.md` (D-game-31 from
  main, D-game-32 from here, the register's rows for both), `docs/game/implementation-plan.md`, `docs/game/findings.md`,
  `docs/game/architecture.md`, `saves.md` (one description: version 8 with `systems.sleep` and `systems.atmosphere`), `lessons.md`,
  `running.md`, `content/README.md`, the READMEs. For this milestone you may edit those files, only to resolve the merge; change no
  decision's wording.

Checks after the merge:
1. The gate below.
2. Runs in the game from a new world (through limited.sh, `--mute --needs off`): `--seed 42 --dev --lodging --skip-to-session 2
   --teleport 280,221 --walk-to 278,231 --sleep --wait 2 --save <file> --screenshot <png>`: the sleep wakes in the morning by the
   clock, the save holds `systems.sleep` (`last: inn.bed`) and `systems.atmosphere`; a death after it wakes inside the guest room.
   One run at Luna's fire on day 1 as main's sleep runs do. A screenshot of the inn under main's sky and of the weather in the
   village into `artifacts/y06m/` (git-ignored).
3. The scripted runs that main's weather series captured last (see `main`'s E08 close-out) and `r26-death`: run them and report any
   difference from main's captures and why. Do not re-baseline them (Y08 does, after the mountain goes).

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count, and compare with the two sides' counts: `feature/settlement` after Y06 is in y06-report.md, main's in its last
handover or a run on `origin/main` if you need it, which you then report); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; the A2 content gate clean; the full suite once
without models (`VOCATERRA_MODELS_DIR` an empty folder) passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, one page: branch, commit SHAs, worktree path; the conflicted files and the resolution of each (one line each, grouped); the
behaviour decisions you made where the two sides met (the sleep, the save, the fixtures, the tests changed); the bundle hash and
anything rendered or recorded again; test counts; the runs in the game; the scripted runs' differences; anything Y07 to Y13 should know
(what of main's code they now must respect); known-not-fixed with reasoning. Leave out how you searched.
