You are implementing the game milestone **T03, neutral world ids**, in the Vocaterra repository (C#, .NET 10). The game is
`Vocaterra.Game.Core` (engine-free logic), `Vocaterra.Game` (MonoGame), `Vocaterra.Host` and `tools/Vocaterra.SpeechRender`; the language
core (`src/Vocaterra.Nlp*`) is not yours and must not change. Do the work yourself; do not spawn subagents. Commit prefix `T03: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/t03`, already on the branch `T03-neutral-world-ids` (from
`feature/german` after T02b merged; no build yet). cd there first; never edit another worktree. Commit after each step that builds and
passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the
foreground and finish in one turn (no background commands, monitors or wait loops). Never use `git stash`; set work aside with a WIP
commit. **Other sessions build on this machine:** every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it
wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/t03/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment**, **Rules** and **Gate**: they apply to you, with `/tmp/claude-1000/t03/` for your logs, @@COUNT@@ as the
count before you (the open-fallback fuzz reach check is flaky unless M89 has merged: if it alone fails, rerun it alone and say so), and
these changes. **What this milestone edits:** the game projects, their tests, `content/game-v1/game/` and `content/schema/` for the files
you re-key, and `docs/game/` (not the plans or the decisions). Never the core, the lexicon, `content/locale/`, `.claude/` or
`.github/workflows/`. Content edits mean the content loop (the w07 brief's last Rules bullet and `content/README.md`, "The edit loop"):
`scripts/content-loop.sh --grammar` after each content edit (run directly, never under limited.sh), `--tier full` once before the report
instead of `scripts/test.sh full`; read every grammar finding; the save fixtures take the new bundle hash. **Saves:** if the save format
changes (an inventory, objective or station key that was a Spanish lemma), raise the save version, refuse older saves with a clear message
and re-record `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json` and `save-v4.json`; no migration code (the game is unreleased).
**The scripted runs** (`scripts/scripted-runs.sh` before your first edit and after your last, compared with
`python3 -I scripts/compare-runs.py`) are the main guard: no transcript may change; explain any screenshot difference.

## Why
The owner decided on 2026-11-08 (D-game-27) to make German a second target language, with world and language bundles split (T05). Before
the split, world data must stop naming Spanish lemmas. Specification: `docs/research/2026-11-04-german-in-the-game.md` section 4.1, row
G11, and section 5.1 option B (which files are world and which are language); the row T03 in `docs/game/implementation-plan.md` section
4.10, and T02's section 5 entry (grep `Ids for T03`).

## What to build (each committed)
1. **Audit** (a table in the report). Every place world data or game code keys something by a Spanish lemma: `game/recipes.json`
   (`output`, `item`, `station`: `antorcha`, `madera`, `mesa`, `manzana`, `fuego`), `game/objectives/*.json` (`noun`, `detail`),
   `game/stations.json` (`id: horno`, fixtures' `noun`), `game/actions.json` (`use`, `on`), `game/procedures/*.json`,
   `game/host-events.json`, `game/needs.json`, `game/tips.json` triggers, `game/scene.json`, `game/session.json`; in code
   `SceneBuilder.cs` (tienda, rio, montana), `BlockDefinition.cs` (sign ids), `BlockArtVillage.cs`, `VoxelSession` (río, agua, fuego,
   antorcha), and the host's event details. For each: world (re-key) or language (stays: lessons, phrases, dialogue, checks, grammars and
   intents are language content and keep their lemmas). `ItemId` and block ids are already neutral English: reuse them as the ids.
2. **`game/nouns.json` is the map.** It already lists each lemma with its `items` and `blocks`. Make the neutral id the key and the lemma
   the language side (for example `{ "id": "wood", "lemma": "madera", "items": ["wood"], … }`), so a German bundle maps `wood` to `Holz`
   with no world change. Add the ids that stations, fixtures and places need (`table`, `fire`, `oven`, `bowl`, …); schema updated.
3. **Re-key the world data** from step 1 to the neutral ids, and the code that reads it; where the game matches what the learner says or
   shows a word, it goes through `nouns.json` (id → lemma) instead of comparing with the id. Host event details sent to the core stay as
   the core expects them (if the core's grammars or intents name a lemma in an event detail, translate at the boundary and say so).
4. **A guard test:** world data files (the ones step 1 calls world) contain no string equal to a `nouns.json` lemma, except listed keys
   with a reason; extend `SpanishLiteralGuardTests` or add one beside it.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; the audit table (file, key, world or language, new id); the nouns map's shape; the save version (raised
or not, why); the guard; the scripted runs' comparison; the content loop's results (grammar findings, bundle hash); test counts (`--tier
full`); the gate's results; core gaps (reported, not patched); what T05 should know about which files are world.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
