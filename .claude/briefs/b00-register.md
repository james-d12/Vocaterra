You are implementing B00, "Register the B series", for the Vocaterra repository (C#, .NET 10). This milestone edits documents only:
no code, no content, no tests.

Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b00, already on the local branch `B00-register` (cut from
`feature/biomes` at b311b2e8). Run every command from that directory. Commit with the prefix `B00: `. Do not push your branch, and never
push or merge `feature/biomes` or `main`. When you finish, leave the worktree on `B00-register`, clean.

Background: the owner accepted the B series (a tall procedural world round hand-crafted regions) on 2026-11-15 as decision D-game-34.
The plan is docs/game/biomes-plan.md; read its status block (lines 15 to 25), section 4 (the milestone table and the order) and section 5
(the owner's answers). D-game-34 is already recorded in docs/game/decisions.md; do not edit that file.

This brief names, as the files you may edit: docs/game/implementation-plan.md (section 4 only), docs/open-items.md, docs/README.md,
docs/game/README.md and docs/game/biomes-plan.md (section 8 only). Edit nothing else.

Deliverables:
1. docs/game/implementation-plan.md: a new section "### 4.14 A tall procedural world and biomes (the B series; owner, 2026-11-15,
   D-game-34)", placed after section 4.13's table and before "## 5. Findings log". Model it on section 4.12 and 4.13 (read lines 501 to
   547): a short paragraph (branch `feature/biomes`, commit prefix `B<nn>:`, milestone branches `B<nn>-<name>`; the design, the owner's
   answers and the progress table are in `[the biomes plan](biomes-plan.md)`; the statuses follow its section 8), then a table with the
   columns `| ID | What | Depends on | Size | Model | Status |`. One row per milestone B00 to B15 (B04a and B04b as separate rows), taken
   from the plan's section 4 table: "What" a one-line summary of the plan's milestone text, "Depends on" and "Size" as in the plan,
   "Model" written as `Haiku 5.5`, `Sonnet 5.5, medium` or `Sonnet 5.5, high` (the plan's "Haiku", "Sonnet medium", "Sonnet high").
   Status: B00 `in progress`; every other row `planned`. Add one sentence after the table naming the build order of the plan's section
   4 ("Order") and the owner-review stops: B02 (the maps, before tuning), B07 (the performance gate and the pictures), B08 (the pass),
   B13 (caves and ores in the valley).
2. docs/open-items.md: remove the `D-game-34` row from section 1 (the decision is answered); add a row to section 3 after the `Game Y`
   row with four cells. Series: Game B. Items: B00 to B15: a tall procedural world round hand-crafted regions (biomes, ranges, rivers,
   lakes and sea; caves, lava, rock layers and ores; the pass out of the valley, opened by the lessons). State: registered 2026-11-15
   (D-game-34); in progress on `feature/biomes`. Detail: a link "biomes plan" to `game/biomes-plan.md`, then "section 4".
3. docs/README.md and docs/game/README.md: in the biomes-plan rows, change "proposed" to "decided" (the docs index row ends "D-game-34
   proposed": make it "D-game-34 decided"; the game README row says "The B series proposal" and "(D-game-34, proposed)": make them "The B
   series" and "(D-game-34)"; in the docs index row, "proposed 2026-11-15 on `feature/biomes`" becomes "registered 2026-11-15 on
   `feature/biomes`").
4. docs/game/biomes-plan.md section 8: add a row `| B00 | registered 2026-11-15 | (filled by the orchestrator) | section 4.14 of the
   game plan; open items and index rows |` after the last row.

Check (documents only, so no build or full suite): run the doc-link test from the worktree root:
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Tests -m:2 --filter MarkdownLinkTests
It must pass. Run it through scripts/limited.sh as shown (the machine has crashed from overload), never in the background. Do not commit
bin/, obj/ or artifacts/. Keep the existing wording style: plain sentences, no em dashes, British spelling as the files use.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>

Report, a few lines: branch, commit SHA, worktree path; the files changed with their line counts (`git diff --stat b311b2e8..HEAD`);
the doc-link test's result; anything you could not do and why.
