You are implementing the game milestone **V19, the game findings of the review of `feature/village`**, of the Vocaterra repository (C#, .NET 10, a MonoGame game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** The owner asked on 2026-11-01 for every finding of the review (`docs/qa-reports/2026-11-review-feature-village.md`) to be fixed on `feature/village` before PR #20 is merged. The core's findings (R1, R2, R5, R6) were fixed by M64, which is merged; this milestone does R4, R7, R8, R9, R10, R1's game side (an answer set) and two of the review's risks. (This brief is also committed as `.claude/briefs/V19-review-game.md`.)

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the commit that adds this brief, `git log -1`).
- Create your branch with `git checkout -b V19-review-game feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `V19: `. Push after every commit: `git push -u origin V19-review-game` the first time, then `git push origin V19-review-game`.
- Never push `main` or `feature/village`, and do not merge.

## What to fix (each with a regression test that fails before the fix)
### R4: the listener's name after any greeting
`Npc/Vocative.cs` takes the listener's name out only at the start or after `hola` (`Greetings = ["hola"]`, line 25), while `Npc/Greeting.cs` knows `hola`, `buenos días`, `buenas tardes`, `buenas noches` and `buen día`. Sessions open in the morning, so `buenos días` is the likely greeting.
- Through the host today: `buenos días luz quiero pan` at `baker-open` gets "No te entendí"; `buenos días domingo vendo madera` at `smith-open` is read as a statement about Sunday (`state_action`, recast «domingo vendo madera»), and no sale happens.
- After the fix:
  - `buenos días luz quiero pan` buys the bread; `buenos días domingo vendo madera` sells the wood; `buenas tardes mar ¿dónde está la panadería?` points the beacon; `buen día león quiero tres manzanas` buys apples;
  - unchanged: `hola luz quiero pan`, `buenos días` alone and `buenos días luz` (a greeting), another NPC's name (`luz compró pescado` said to Mar), and every lesson step's line.
- **One place for the reading of a line.** The name-then-greeting sequence is written three times: the typed and the spoken paths of `NpcService.Take` (about lines 590 to 615) and `src/Vocaterra.Host/ConversationSession.cs`. R4 is the drift that copying causes. Put it in one helper that all three call; take the openings from `Greeting`, not a second list.
- Tests: `tests/Vocaterra.Game.Core.Tests/Npc/VocativeTests.cs`, `GreetingTests.cs`, and a host test where the host's tests are.

### R7: a catalogue noun that names no item is not the NPC's item
`NpcHostEvents.ItemSaidIn` (`Npc/NpcHostEvents.cs`, about line 150) counts a catalogue noun with no items (`noun.Items.Count == 0`) as asking for the effect's own item. That takes in 15 nouns of `content/game-v1/game/nouns.json`: `casa`, `río`, `piedra`, `árbol` and the rest. So `quiero casa` at the bakery buys bread, and `vendo piedra` to Domingo sells the player's wood.
- After the fix, such a noun is another thing, and the NPC answers `wont_sell` or `wont_buy`.
- A line with no item slot, or a word the catalogue lacks, still counts as the effect's item, as the doc comment says.
- Check every lesson step and scripted run that sells or buys. `vendo bloques de madera` and `quiero dos panes` must still work.

### R8: no room in the bag
`NpcService.Miss` (about line 811) checks the coins and the goods, but not the bag's room. `VoxelSession.Sell` and `Buy` (about lines 717 to 740) then return quietly when `CanGive` fails, after the NPC has said the success line. A lesson's `do` step waiting on the item would never advance.
- Add a miss for "no room": the items of a purchase, or the coins of a sale, do not fit. The NPC says why, and nothing changes hands.
- It needs a line for each NPC that sells or buys: the baker, the farmer and the smith (`game/host-events.json`). Add their pools and agent transitions the way V15 added `wont_buy` and `cant_sell` (`grep -rn cant_sell content/game-v1/agents content/game-v1/dialogue`).
- The Spanish is short and A1, for example `Tu bolsa está llena.`, and has an Opus pass (below).

### R9: no hidden cap on quantities
`NpcHostEvents.Quantity` caps a number at `MaxQuantity` (5). The V15 entry (`docs/game/IMPLEMENTATION_PLAN.md` section 5, "V15") says "No cap: the goods are the cap".
- Today, holding 8 wood, `vendo ocho bloques de madera` takes 5, while `vendo madera` takes all 8. With 15 coins, `quiero quince manzanas` at León gives 5 apples for 5 coins, with the success line. Session 10 teaches 11 to 20.
- After the fix:
  - a sale to an NPC (`buy`) takes the number said, at most what the bag holds;
  - a purchase (`sell`) gives the number said, bounded only by the coins (`cant_pay`) and the room (R8's miss).
- Keep a cap only where `MaxQuantity` guards something else (find every use), and say where.
- Guard the arithmetic: a number of 12 digits must not overflow `int` or the price times the count.

### R10: zero moves nothing
`Quantity` reads any number of 0 or less as 1, and the core gives `quiero cero panes` the slot `qty: "0"`, so that line buys one loaf.
- After the fix, a zero moves nothing, and the NPC says no success line.
- Prefer an existing line of the NPC that fits, for example the baker's «Aquí hay pan. ¿Cuántos panes quieres?»; otherwise add a short one, with speech.
- The same for `vendo cero bloques de madera`.

### R1's game side: session 8's answer with a subject
M64 fixed the core's false `conj.person` on a coordinated subject. Session 8's villager asks «¿Y Mar y tú? ¿Adónde fueron ayer?» at `lesson-we-went`, and the natural answer `mar y yo fuimos al río` is now graded right. It still gets `vocab.wrong` with correctness 0.4, because the step's answer set (`content/game-v1/answer-sets/lesson-we-went.json`) has no place for a subject.
- Add the natural answers with a subject as aliases, so they score as the canonical `fuimos al río` does: `mar y yo fuimos al río`, `tú y yo fuimos al río`, `luz y yo fuimos al río`, with `ayer` before or after.
- Check whether the lesson's other `1pl` steps (grep the lessons for `"person": "1pl"`) invite a subject in the same way, and add the same kind of aliases there only where the NPC's question invites one.
- Run each through the host at its prompt: correctness 1, no error.

### Two risks from the review
- **`SessionSkip.SkipBuy` calls `Sell`,** which returns quietly when it cannot buy, so a skip can leave a later coin order short. Make a skip that cannot complete a purchase fail loudly (`InvalidOperationException` naming the session and the item), with a test.
- **The doc comments and `docs/game/` text** that describe selling, buying, the greeting or the cap must read true after the fix: `docs/game/README.md` (the village section) and `content/README.md`. Do not edit `docs/game/IMPLEMENTATION_PLAN.md`; the orchestrator corrects the V15 entry.

## Read first (the sections named, not whole files: every line you read stays in your context)
- `CLAUDE.md`.
- `docs/qa-reports/2026-11-review-feature-village.md`: R4, R7 to R10 and "Risks".
- `docs/game/IMPLEMENTATION_PLAN.md` section 5: the V14 entry (about line 1826, `Vocative`), the V15 entry (about line 1921, selling, `Greeting`) and the V09-again entry (about line 1976).
- `docs/code_guidelines.md`.
- The code: `src/Vocaterra.Game.Core/Npc/{Vocative,Greeting,NpcHostEvents,NpcService}.cs`, `Session/VoxelSession.cs` (`Sell`, `Buy`, `Give`, `CanGive`), `Session/SessionSkip.cs`, `src/Vocaterra.Host/ConversationSession.cs`, and their tests: `tests/Vocaterra.Game.Core.Tests/Npc/`, `Session/SellingTests.cs`, `Session/OrdersAndSalesTests.cs` and `Session/SessionSkipTests.cs`.
- `content/README.md` ("The edit loop") and the header of `scripts/content-loop.sh`.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n` to find a place, and keep `sed -n` ranges under about 150 lines.
- Never read `docs/game/SPEECH_CHECKLIST.md`, `content/game-v1/manifest.json` or the whole of `content/PHRASES.md`; grep their rows.
- Send every heavy command's output to a log file under `/tmp/claude-1000/` and print at most 30 lines of it.
- Wait for a long command by running it in the foreground, not by polling a log.

## The content loop (for R8's lines and any line R10 adds)
- **An Opus pass** over every new Spanish string before any WAV: naturalness, a Latin American neutral register, words within A1, accents and `¿¡`. List the changes in the report. The orchestrator reads the new lines in the diff too.
- **Try the core first** on any new line a learner might say back. A wrong grade is a core gap: report it, do not patch the core.
- **After each round of content edits** run `scripts/content-loop.sh --grammar --speech` directly. Never run it under `scripts/limited.sh`: it calls it itself and refuses to run nested. It does the following and stops at the first failure with the next thing to do:
  - the refresh;
  - the LanguageTool cache, with CI's rebuilt-from-nothing check;
  - the new lines' WAVs, in the NPCs' existing provisional voices;
  - the `bundleHash` in the save fixtures;
  - the build, the goldens and the fast tier.
- **Read every grammar finding it prints:** fix the content, or add a waiver with its reason. With `--record` when the goldens move, in a commit of their own. Once with `--tier full` before the report.
- After it finishes, check that no `content-loop.sh` or wait loop of yours is still running (`pgrep -fa content-loop`).

## Behaviour to check (as a learner would meet it)
- **The host:** each R4, R7, R9 and R10 line above at its prompt, before and after the change. The host has no bag, so it prints a sale's success line whatever is held; the game decides.
  - `/tmp/claude-1000/review/host1.sh <file of prompt|line>` runs the built host on each line. Build first, and run it through `scripts/limited.sh`.
  - `/tmp/claude-1000/review/h1.txt` holds the review's selling and greeting lines.
- **The game:** for R8 and R9, `--skip-to-session 10` with the selling and buying lines of `scripts/scripted-runs.sh`'s `r22-market`, plus a full bag and a number above 5. Check the coins, the items, the NPC's line and the log.
- **The scripted runs:** `scripts/scripted-runs.sh` before your first edit and after, compared with `scripts/compare-runs.py`. Explain every difference. `r22-market` and `r18-bakery` are the ones that sell or buy.
- **CI's case:** the full suite once without models, `VOCATERRA_MODELS_DIR=<an empty folder under /tmp/claude-1000/> scripts/test.sh full`. Report any wall-clock test within 4 times of its budget.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech render and every game or screenshot run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). The one exception is `scripts/content-loop.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh fast` or `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.
- Disk is tight (about 11 GB free): delete capture directories you no longer need, and keep scripted-run captures to the two you compare.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/` (the review's `batch.sh` runs the CLI).
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`.

## Rules
- **What a game milestone edits:**
  - the game projects (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) and their tests;
  - `content/game-v1`, `content/locale`, `content/schema`, `content/PHRASES.md`, `content/README.md`, `content/grammar` and `content/speech-index.json`;
  - `assets/` and `docs/game/`.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is, except for re-recording goldens and the identity baseline that a content edit moved.
- Engine code goes only in `src/Vocaterra.Game`. `Game.Core` uses no `System.Random`, no `Guid` and no clock (`ArchitectureRulesTests`).
- Do not edit `scripts/`, `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/IMPLEMENTATION_PLAN.md`, `docs/qa-reports/` or either `DECISIONS.md`.
- Never create or edit anything under `.github/workflows/`.
- A save must still load an older save (X01); a new field gets a default.
- Write code and content that read like the surrounding ones.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing (it was 7364 passed and 3 skipped, with models, after M64); report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/content.sh validate`.
- The content gate as `scripts/merge-gate.sh` runs it: the lint at A2 with fail-on-level and `--host-pools 'guide.*'`, and the cefr-checks.
- The host's `--validate-content`.
- `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --offline --strict`.
- **The host demos still hold:**
  - `--transcript "quiero agua, por favor" --prompt stall-need` gives `request_item`, communicated;
  - `--transcript "hola, quiero dos panes" --prompt baker-open` gives the sale;
  - `--transcript "vendo madera" --prompt smith-open` gives `offer_item`;
  - `--transcript "mañana compraré pan" --prompt baker-open` gives `state_action`, with no error.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words; the details belong in the commits)
- The branch and the final commit SHA, pushed.
- For each of R4, R7, R8, R9, R10, R1's answer set and the two risks: what was built, the test, and the host or game result before and after.
- The new Spanish lines, the Opus pass's changes, and the WAVs rendered.
- The scripted runs' differences, each explained.
- The test count before and after, with and without models; any timed test within 4 times of its budget.
- Core gaps found (the exact line and what it gave); anything left undone, and why.
