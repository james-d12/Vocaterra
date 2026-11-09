You are merging **`origin/main` into `feature/german`** in the Vocaterra repository (C#, .NET 10), on a branch the orchestrator then
gates. The game is `Vocaterra.Game.Core`, `Vocaterra.Game`, `Vocaterra.Host`, `tools/Vocaterra.SpeechRender`; the core is
`src/Vocaterra.Nlp*`. Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/merge-main`, on the branch `merge-main-into-german` (at
`feature/german` `ed272020`). cd there first; never edit another worktree. Run `git merge --no-ff origin/main` (main is at `fbcd18a6`;
`git fetch origin` first and say if it moved). Resolve every conflict, then commit the merge with the message
`Merge main (PRs #46, #47, #48, #50) into feature/german` plus the trailer below; adaptations after it (guards, re-hash, fixtures) are
separate commits with the prefix `Merge main: `. Never end your turn with uncommitted work (the owner sometimes has to stop agents). Do
not push; never push or merge `feature/*` or `main`. Run everything in the foreground and finish in one turn (no background commands).
Never use `git stash`. **The machine is shared:** every heavy command through `scripts/limited.sh`; if it waits for a slot, let it wait.
Prefix every dotnet command with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`. The sandbox refuses a
command line containing the words `eval` or `hash`: run such commands from a wrapper script outside the repository
(`/tmp/claude-1000/merge-main/`, also your logs).

## What the two sides did
- **`feature/german`** (PR #51, D-game-27, `docs/game/decisions.md`): the game made language-neutral. T01: the game reaches the Spanish
  pack only through `GameLanguages` and the bundle (`bundle.LanguageMorphology`, `bundle.Language.*`), guarded by
  `LanguageNeutralityTests`. T02/T02b: Spanish words the game says or matches live in `game/words.json` (`GameWords`, passed as
  parameters), guarded by `SpanishLiteralGuardTests`. T03: world data keyed by neutral ids, `game/nouns.json` maps id → lemma, guarded by
  `WorldDataNeutralityTests`. T04: save version 9 with a required `language`, profiles under `saves/<language>/`, the speech table
  `game/speech.json`. Core: M87 (`IOrthographyNotes`, `IMorphology.Paradigm`), M88 (case on `Article` and `Agree`, `PolitePositions`, V2
  hooks, `RestoredCapitals`, `decl`/`particle`), M89, M92, M36 (the German lexicon), M37/M38 (the new `src/Vocaterra.Nlp.De`). The
  game plan's section 4.10 and section 5 entries for T01 to T04 explain each.
- **`main`**: PR #46 (the menu), #47 (UI issues U05 to U08 and the core's M91; U08 changed the save: history), #48 (bug fixes:
  objectives, item models, NPC phrases and voices), #50 (the hand-over of an order's goods by speech, issue #42). Their plans' entries are
  in the same documents on main.

## How to resolve
1. **Keep both sides' behaviour.** Where main added code that calls a Spanish class, names a Spanish word as a literal, or keys world data
   by a Spanish lemma, adapt it to `feature/german`'s neutral way (the bundle, `GameWords`, `nouns.json` ids). The three guard tests must
   pass with no new allow-list entry unless it carries a real reason (say each).
2. **Core conflicts** (`TextSideInterfaces.cs`, `TemplateEngine.cs`): keep main's change and M88's additions together; Spanish output
   byte-identical (the goldens, `SpanishIdentityTests`, the core demo: exactly `conj.person` and `noun.number`).
3. **The save:** both sides changed it. The merged format carries both (main's history and the German side's `language` and profiles),
   with a version above both sides' (refuse older; no migration, the game is unreleased). Re-record
   `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json` and `save-v4.json` for it.
4. **Content and manifests:** take both sides' content edits (main's under `content/game-v1` must also follow T03's ids where they are
   world data); never hand-merge `manifest.json`: resolve to either side, then run the content loop (`content/README.md`, "The edit
   loop": `scripts/content-loop.sh --grammar`, run directly, never under limited.sh) which re-hashes and puts the new hash in the
   fixtures. Read every grammar finding. If main added speech lines, the loop's `--speech` renders them.
5. **Docs** (both plans, `docs/game/decisions.md`, `docs/handovers/README.md`): keep every row and entry from both sides in order. Main
   renumbered its book decision to D-game-28 because D-game-27 is German's; keep both. Milestone ids: main has M91; feature/german has
   M92, M93, M95 (another session holds M94): no clash.
6. **Verify:** `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings); `scripts/content-loop.sh --grammar --tier
   full` (the whole suite; report the count); `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`; the scripted runs
   (`scripts/scripted-runs.sh` on the merged tree, all exit 0; compare with a baseline captured from `feature/german` `ed272020` in a
   scratch worktree under `/tmp/claude-1000/merge-main/`, removed afterwards, with `python3 -I scripts/compare-runs.py` one run at a time):
   every transcript difference must be explained by one of main's PRs (name it) or it is a merge bug to fix.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; each conflict and how it was resolved; main's code adapted to the neutral APIs (file, what); the save
format and version; the content loop (bundle hash, grammar findings, speech); test counts; build and format; the scripted runs'
differences, each with its PR; anything you could not resolve and why.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
