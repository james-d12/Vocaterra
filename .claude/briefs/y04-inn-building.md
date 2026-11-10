You are implementing Y04, "The inn building and the meadows", for the Vocaterra repository (C#, .NET 10). The language core is the
engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core
(the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the
game, and most of it is content: the scene.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y04, which already exists on the branch `Y04-inn-building` (cut from
`feature/settlement`). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y04 &&` and use absolute paths for file
tools; your shell may start in another checkout, which you must not edit. Commit after each item that builds and passes its tests, with
the prefix `Y04: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge `feature/settlement`
or `main`. When you finish, leave the worktree on your branch, clean.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and screenshot or scripted run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly. Never start two heavy
commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Other agents build
on this machine too, so a heavy command will often wait for a free slot: that is normal, let it wait, and build and test in few,
well-chosen runs. While iterating run `scripts/test.sh fast` or `scripts/test.sh project <test project>`; run `scripts/test.sh full`
once before reporting. Exit 137 means the 6 GB cap killed it: lower the parallelism, do not work round the cap. The scripted runs
share a default work folder with other sessions: set `VOCATERRA_RUNS_WORK` to a folder of your own (in your scratchpad) for every
`scripts/scripted-runs.sh` call, the same folder for the capture before and the capture after.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
or a CLI `eval` run use a wrapper script outside the repository). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/process/code-guidelines.md; content/README.md ("The edit loop", and the rule for a word the content uses
that is on no level list: grep `cefr/a1.txt`); in docs/game/settlement-plan.md section 8.2, the paragraph "Y04" of 8.3, and sections
9.1 and 9.2 whole (they are your specification: the prop, the stamp, the cells, the meadows, what names `house3`); in
docs/game/decisions.md the section "D-game-32" (grep, read only it). Then: content/game-v1/game/scene.json (whole);
content/game-v1/game/needs.json (`sleepPlaces`); src/Vocaterra.Game.Core/World/Generation/SceneBuilder.cs (`BuildHouse`, `BuildStamp`,
`FlatHeight`, `AddPoint`, `FeetCell`, by name); the tests SceneTests.cs, VillageTests.cs, ScenePlacesTests.cs and
SceneBuildTimeTests.cs under tests/Vocaterra.Game.Core.Tests. Read the parts named, never whole long files; never
docs/game/speech-checklist.md or content/game-v1/manifest.json. Heavy output goes to log files; print at most 30 lines.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it
is (a missing core call stops that part: report it, do not copy core source or edit a Vocaterra.Nlp project); a regression check is a
C# test, never a script with text baselines; tests are named Given_When_Then in folders that mirror the source; do not edit CLAUDE.md, either
implementation-plan.md, either decisions.md or docs/game/settlement-plan.md (the orchestrator does); engine code only in
src/Vocaterra.Game; assets CC0 only; nothing under .github/workflows/. This is the one milestone before Y08 that edits the scene: put
every scene change of this brief in it, and none beside.

What this milestone is for. The player's first bed will be at an inn in the village, and the land they later buy lies in two meadows
outside it. Y00 measured where both go (section 9). You build the inn as scenery (a house, its inside, its bed, the place its keeper
will stand) and clear the meadows. No one can sleep in the bed yet and no innkeeper stands there: Y05 and Y06 do that.

Deliverables:

1. **The inn.** In content/game-v1/game/scene.json, section 9.1's `house` prop (9 x 7 at the village's dx -16, dz 8, the door to the
   north, name `inn`, label `posada`), then its interior stamp with the block `bed` (Y03 added it; `bedroll` too) where 9.1 has
   `crate`, `lamp.inn` in place of `lamp.house3`, and the points `bed.inn` and `npc.innkeeper` at 9.1's cells. Improve the inside
   where it reads badly in a screenshot (it is a first draft), but keep the door, the counter, the keeper's cell and the bed's cell
   where they are, or say exactly what moved.
2. **Three houses stay.** The fisher's lesson says the village has «una tienda, una panadería y tres casas», and the inn stands where
   `house3` is. Move `house3` (5 x 5, its name and its label `casa` kept) to a free place inside the village's levelled disc where
   it clears every other prop, the paving and the road, and where a walker can reach its door. Prove the place by a build and a
   screenshot. If no place takes it, stop and report: do not change the lesson's line.
3. **The meadows.** The sites `meadow.north` and `meadow.south` and their `trees.clearings` entries, as section 9.2 recommends
   (centres, radius 22, fade 12, keep 0). They level nothing.
4. **The word.** `posada` joins the bundle's lexicon (`content/game-v1/lexicon/game.json`, feminine) and the A2 level list, after
   the model content/README.md gives for such words (the owner decided this: D-game-32 (e)). Check through the core's CLI that
   «¿Dónde está la posada?» at the villager's prompt is read as a question about a place and names it, and in the game that the
   villager's answer points at the inn (`--talk villager --say "¿Dónde está la posada?"`).
5. **The bed as a sleep place.** (Y01 built `Session/SleepPlaces`; Y02 saves the lodging flag `VoxelSession.LodgingGiven`.) In
   `needs.json`'s `sleepPlaces`: `inn.bed`, the point `bed.inn`, the kind `bed`, a reach of 2 across
   and 2 in height, the access `lodging`. With that access nobody can use it yet, and the game plays as before.
6. **Tests.** The tests that name `house3` or pin the scene (section 9.1's list) follow the move; `SceneTests`' golden digest is
   pinned again after you have looked at what changed (say what did); `SceneBuildTimeTests` stays under its three seconds (report
   the time). New tests: the inn's cells (the door, the counter, the keeper's cell enclosed, the bed) are what 9.1 says; a body walks
   from the street through the door and the partition's gap to the bed's stand cell; the keeper's cell gives a valid standing cell
   for an NPC; the crosshair's line from the talk cell to the keeper's cell is clear and from the street it is not; on seeds 42, 7,
   1234 and 2025 no trunk and no leaves stand within 20 blocks of either meadow's centre; the village still has three houses
   labelled `casa`; `inn.bed` is listed, resolves to its point and is not usable.
7. **The scripted runs and the goldens.** Capture the whole set before your first change and after your last
   (`scripts/scripted-runs.sh <out>`), compare (`scripts/compare-runs.py`) and list every run that differs with the reason: a view
   that now shows the inn, the moved house or a cleared meadow is expected; anything else is a finding. Record the end-to-end goldens
   again only if the content loop says they moved, and say which.
8. **The bundle.** After each content edit `scripts/content-loop.sh --grammar --speech` (run directly), `--tier full` once before
   the report. No line is spoken yet, so no recording is made.
9. **Screenshots for the owner**, from the built game, into
   /home/james/Dev/Vocaterra/.claude/worktrees/settlement/artifacts/y04/ (git-ignored; the one write outside your worktree that is
   asked for): the inn from the road, the counter from the talk cell, the guest room with its beds, `house3` in its new place, each
   meadow from its edge, and the village from the bridge road.
10. **Documents.** Where a document or a help text lists the village's buildings or the scene's points (`house3`, "three houses",
    the `--beacon` help in src/Vocaterra.Game/Startup/LaunchOptions.cs, src/Vocaterra.Game/README.md, docs/game/running.md,
    docs/game/architecture.md): add the inn, the bed's point and the meadows, and correct what the move makes untrue.

Not in this milestone: the innkeeper (Y06; only the cell is named now), lodging and the bed's prompt (Y05), the mountain camp (Y08),
any land rule (Y09), any other word than `posada`, any change to a lesson, a line or a recording.

Gate (from your worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(report the count; the feature branch had 10,719 passing and 3 skipped after Y02); scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes, and the content gate of CLAUDE.md is clean; the scripted runs compared as deliverable 7 says. Two things you may meet in the full run that are not yours, and that you report and do not fix: the fuzz
test `CorrectSentenceFuzzTests` fails at random on a known core bug (the seed `0ybc3kyw33r3`, «nosotras vemos»), and the timing test
`LoadingTheBundleIsFastEnough` can go over its limit when the machine is loaded; run a failed test class again alone before you
conclude anything. Do not
commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfPceqswnMMBmTQzg9

Report, one page: branch, commit SHA, worktree path; what was built, with the final prop lines and the cells that differ from
section 9.1; where `house3` went and why there; the scene build's time and the new digest; test counts and the tests you changed;
the scripted runs that differ, each with its reason; whether the goldens moved; the screenshots' paths; core gaps (what call, why);
anything in Y05's, Y06's or Y09's plan that should change; anything left undone and why. Leave out how you searched.
