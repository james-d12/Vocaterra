You are implementing B08b, "The pass's lock", for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp,
the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the engine-free logic),
Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game: Vocaterra.Game.Core,
the settings menu in Vocaterra.Game, content/game-v1 and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b08b, already on the local branch `B08b-pass-lock` (cut from
`feature/biomes` at 06e018a5, which holds B01 to B06, B04b and B08a). Run every command from that directory. Commit after each item that
builds and passes its tests, with the prefix `B08b: `. Do not push your branch. Never push or merge feature/biomes or main. When you
finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run and every scripts/content.sh call
through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`).
scripts/test.sh calls it itself: run it directly. Never start two heavy commands at once (no `&`, no background builds, no parallel test
runs), and write no shell wait loop on `pgrep -f`. Up to three other agents build at the same time; limited.sh queues you all, so expect
waits. While iterating run `scripts/test.sh fast` or `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full`
once at the end, after your last change. Exit 137 means the 6 GB cap killed it: lower the parallelism. No game windows: do not run the
game, screenshots or scripts/scripted-runs.sh (the owner may be using the screen); the orchestrator takes the pictures and runs later.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
write a two-line wrapper script in /tmp/claude-1000/ and run that). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md (in particular "Game content bundle" and the content gate); docs/game/implementation-plan.md sections 1, 3, 4.14
(your row) and 6; docs/game/biomes-plan.md section 3.6 ("The pass") and section 4 (your row); D-game-34 items 6, 15 and 17 in
docs/game/decisions.md (the pass opens once every lesson in a list in world.json is complete, the list holding all of today's lessons; a
cheat setting in the settings menu opens it at once; it never closes again; after it opens the player may leave anywhere, which B07
does by lifting the valley's edge wall); docs/process/code-guidelines.md; content/README.md. The work you build on:
.claude/briefs/b08a-report.md and b08a-merge-report.md (the pass: path `pass` to `paso.edge` at 383, 208, y 58; the sites `paso.gate`,
`paso.rise`, `paso.foot`, `paso`, `paso.edge`; the `rockfall` stamp and prop at 355, 200; PassTests) and .claude/briefs/b05-report.md
(saves version 9 keep changed chunks; the state digest's header hashes world.json). The code: src/Vocaterra.Game.Core/Menu/PlayerSettings.cs
and how a setting such as `viewEffectsOff` reaches the settings menu in src/Vocaterra.Game; how the game records a completed lesson
(src/Vocaterra.Game.Core/Language/Lessons/LessonRunner.cs, Event/LessonEvent.cs, the save); content/game-v1/game/world.json and its
schema and validator; content/game-v1/game/scene.json (the rockfall). Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, content/locale, assets/ and docs/game/
and takes the core as it is. A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit
either implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in
src/Vocaterra.Game. No lesson id in src (add a test like the existing "no biome id in src" one). No new Spanish content words; an English
label for the setting goes where the other settings' labels live. Keep off the files other agents are changing now: world.json's existing
sections and biomes.json, World/Climate, World/Generation/Procedural, World/Regions (B02b, B15), World/Generation/TerrainGenerator.cs (B15),
src/Vocaterra.Game.Core/Voxel/ and src/Vocaterra.Game/Graphics/ (B09). Your world.json numbers go in a new top-level section of their own.

Deliverables:
1. Data: a new `pass` section in world.json (with schema and validator): the required lessons (all of today's lessons, listed by id; the
   validator checks each exists), and what the lock closes (the rockfall prop's id, the pass's sites as needed). Re-hash, validate, put
   the new bundle hash into tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json and re-read its state digest (world.json is in the
   digest's header, so it moves).
2. The unlock rule in Core: a pure query over the learner's completed lessons (no lesson id in code) and the cheat setting, giving
   whether the pass is open; once open it stays open (recorded in the save, so turning the cheat off or loading the save keeps it open).
   Expose it so B07 can lift the edge wall (for example `PassState.IsOpen` and an event when it opens).
3. The cheat setting: a field of `PlayerSettings` (saved with the player's settings, not the game), shown in the settings menu with an
   English label; turning it on opens the pass at once.
4. The rockfall removed when the pass opens: the rockfall's blocks become what the valley would have there without the rockfall (the
   road's ground and air), as an event applied to the grid, so the save keeps it as changed chunks like any edit; it never comes back.
   In the bounded valley the outer edge still stops the body (B07 lifts it in the streamed world).
5. Tests: closed before the lessons are done; open when the last required lesson completes; the cheat opens it at once; open survives a
   save and load and the cheat being turned off; the rockfall's blocks are gone after opening and stay gone after a load; with the pass
   open a walk from the village reaches `paso.edge` (B08a's PassTests have the closed case); the validator refuses an unknown lesson
   id; no lesson id in src; `SceneTests.GoldenDigest` does not move (the valley is built as before; the rockfall goes only at runtime).
6. Documents: docs/game/running.md (the setting, and a launch flag if you add one for tests, such as `--pass open`), and a short note in
   the Game.Core README.
7. Pictures (D-game-34 item 12) are the orchestrator's, later: in your report list three shots with their `--teleport`/`--look` (the
   rockfall from the village side, closed; the same view open; the road at `paso.edge` looking out).

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,879 passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; the content gate line in CLAUDE.md (lint --strict
--level a2 --fail-on-level --host-pools 'guide.*') clean if you touched any text. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the data, the rule, the setting, the removal, what B07 reads); test
counts; the pins that moved; the three shots for the orchestrator; bugs found and fixed; known-not-fixed with reasoning; files touched
outside the milestone's area; anything left undone and why. Leave out file dumps and long logs.
