You are implementing the game milestone **T05a, the world bundle split from the language bundle**, in the Vocaterra repository (C#,
.NET 10). The game is `Vocaterra.Game.Core` (engine-free logic), `Vocaterra.Game` (MonoGame), `Vocaterra.Host` and
`tools/Vocaterra.SpeechRender`; the language core (`src/Vocaterra.Nlp*`) is not yours and must not change. Do the work yourself; do not
spawn subagents. Commit prefix `T05a: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/t05a`, already on the branch `T05a-world-bundle` (from
`feature/german` after T04 merged; no build yet). cd there first; never edit another worktree. Commit after each step that builds and
passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the
foreground and finish in one turn (no background commands, monitors or wait loops). Never use `git stash`; set work aside with a WIP
commit. **Other sessions build on this machine:** every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it
wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/t05a/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment**, **Rules** and **Gate**: they apply to you, with `/tmp/claude-1000/t05a/` for your logs, @@COUNT@@ as the
count before you, and these changes. **What this milestone edits:** the game projects, their tests, `content/game-v1/` (files move out
of it), the new `content/world-v1/`, `content/schema/`, `content/README.md`, `scripts/content.sh`, `scripts/content-loop.sh`,
`scripts/merge-gate.sh`, `scripts/prerender-speech.sh` and `scripts/playtest-build.sh` only where they must know the world bundle, and
`docs/game/` (not the plans or the decisions). Never the core, `content/locale/` (T05b), `.claude/` or `.github/workflows/` (T05b). The
content loop applies to every content edit (`scripts/content-loop.sh --grammar` after each, run directly, never under limited.sh;
`--tier full` once before the report instead of `scripts/test.sh full`). **Saves:** the format changes (a second hash), so raise the
save version (T04 set it), refuse older saves, re-record `save-v1.json` and `save-v4.json`; no migration. **The scripted runs**
(`scripts/scripted-runs.sh` complete before your first edit and after your last, compared with `python3 -I scripts/compare-runs.py`,
one run at a time if slow): no transcript may change; saves will (say how); explain any screenshot difference. **Hashes:** re-record the
bundle hashes once, in their own commit, after the move is final.

## Why
The owner decided on 2026-11-08 (D-game-27 item 2, `docs/game/decisions.md`): split bundles. `content/world-v1` holds the
language-neutral world data; `content/game-v1` (Spanish) and later `content/game-de-v1` (German) hold only target-language content; the
save records both hashes. Specification: `docs/research/2026-11-04-german-in-the-game.md` section 5.1 option B and rows G16, G17 (the
gates and the core's tests stay on the Spanish bundle; G12, the locale, is T05b); the rows T05/T05a in `docs/game/implementation-plan.md`
section 4.10; **T03's section 5 entry** (grep `T03, neutral world ids`) lists which files are world and which are language, and what is
still lemma-keyed. ADR 0011 (`docs/nlp/adr/`): one bundle, one language; the core's `ContentLoader` reads the language bundle only.

## What to build (each committed)
1. **Decide the cut** (a table in the report): every file under `content/game-v1/game/` and the other folders, world or language, using
   T03's list. Expect world: recipes, stations, actions (minus their verbs: split a file if it mixes ids and words, the words go to the
   language side keyed by id), host-events, objectives (minus display titles), procedures (minus their `do` verbs), scene, needs, tips'
   triggers, session, controls, NPC figures and positions. Language: lexicon, intents, grammars, answers, dialogue, phrases, checks,
   lessons, nouns (id → lemma), words, NPC labels, names and key words, speech, CEFR lists, the speech table (T04).
2. **`content/world-v1`** with its own manifest (`world-v1`, hashed by `scripts/content.sh hash content/world-v1 --bundle-id world-v1`
   through a wrapper script, as for game-v1), its schemas, validated by `scripts/content.sh validate`. `game-v1`'s manifest names the
   world bundle it was written against (id and hash), so a language bundle on the wrong world fails at load with a clear message.
3. **Loading.** `GameData` (and `GameBundle`, the host's `ConversationSession`, SpeechRender if it reads world files) loads the world
   bundle and the language bundle and joins them (ids from the world, words from the language); the game's `--content` option keeps
   working for Spanish (the world bundle found beside it, or named by a new `--world` option; say which). `GameDataValidator` checks the
   join: every id the world uses has its words in the language bundle (nouns, places, verbs, NPC labels), so a German bundle missing a
   word fails validation, not at run time.
4. **The save records both hashes** (world and language) and refuses a mismatch of either as it refuses a wrong bundle today.
5. **The gates and scripts** that name `content/game-v1` and must now also see the world: `merge-gate.sh`'s content gate validates both,
   `content-loop.sh` re-hashes the bundle that changed, `prerender-speech.sh` and `playtest-build.sh` copy `world-v1` too. The core's
   tests, fuzz suite and demo still run on `content/game-v1` (R02) and must not need the world bundle (if one does, report it).
6. **Tests:** the join (a world id with no word fails validation), the wrong-world refusal, the save's two hashes, Spanish unchanged.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; the cut table; the manifests and how they reference each other; loading and the option for the world
folder; the save version and fields; the scripts changed; the scripted runs' comparison; the content loop's results (both hashes); test
counts (`--tier full`); the gate's results; core gaps (reported, not patched); what T05b and T06 must know.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
