You are implementing the game milestone **T05b, the locale split and the gates per bundle**, in the Vocaterra repository (C#, .NET 10).
The game is `Vocaterra.Game.Core`, `Vocaterra.Game`, `Vocaterra.Host` and `tools/Vocaterra.SpeechRender`; the language core
(`src/Vocaterra.Nlp*`) is not yours and must not change. Do the work yourself; do not spawn subagents. Commit prefix `T05b: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/t05b`, already on the branch `T05b-locale-and-gates` (from
`feature/german` after T05a merged; no build yet). cd there first; never edit another worktree. Commit after each step that builds and
passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the
foreground and finish in one turn (no background commands, monitors or wait loops). Never use `git stash`. **Other sessions build on this
machine:** every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/t05b/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment**, **Rules** and **Gate**: they apply to you, with `/tmp/claude-1000/t05b/` for your logs, @@COUNT@@ as the
count before you, and these changes. **What this milestone edits:** the game projects, their tests, `content/locale/`, `content/schema/`,
`content/README.md`, `scripts/content.sh`, `scripts/content-loop.sh`, `scripts/merge-gate.sh`, **`.github/workflows/ci.yml`** (named here:
the grammar step per bundle) and `docs/game/` (not the plans or the decisions). Never the core, `.claude/`, or a bundle's game data
beyond keys the locale move needs. The content loop applies to every content edit. **The scripted runs** (complete before the first edit
and after the last, compared with `python3 -I scripts/compare-runs.py`): no transcript may change and no English text the player sees may
change; explain any screenshot difference.

## Why
D-game-27 (`docs/game/decisions.md`): German is a second target language, and an English speaker learning German needs different glosses
and lesson text from one learning Spanish. Specification: `docs/research/2026-11-04-german-in-the-game.md` rows G12 (the locale), G16 (the
gates per bundle) and G17 (the core's tests per bundle), section 5.4; the rows T05/T05b in `docs/game/implementation-plan.md` section
4.10; T03's and T05a's section 5 entries (the locale is still keyed by Spanish lemmas: `noun.<lemma>`, `proc_<step id>`).

## What to build (each committed)
1. **The locale split.** `content/locale/en/` today mixes UI text (`ui.json`, `controls.json`, `keys.json`, `tips.json`, `feedback.json`
   if it is target-independent) with text that depends on the target language (`lines.json` glosses keyed by Spanish line ids,
   `phrases.json`, `lessons.json`, `checks.json`, `verbs.json`, `nouns.json`). UI text stays in `content/locale/<l1>/`; target-dependent
   text moves to `content/locale/<l1>/<l2>/` (here `content/locale/en/es/`). Re-key what T03 left lemma-keyed to neutral ids where the
   key is a world id (`noun.<id>`, procedure steps by id); keys that are language ids (line ids, phrase ids, lesson ids) stay. The
   locale's manifest and hash cover both levels; it stays outside the game bundles' hashes.
2. **The fallback chain** in `LocaleBundle`/`LocaleText` gains the target language: `<l1>/<l2>/` then `<l1>/` then the existing fallback;
   a missing target-dependent string is a validation error for that pair, not a silent English default. Tests with a second, made-up pair
   (`en/de` with one string) proving the lookup.
3. **The gates per bundle** (G16, G17): `merge-gate.sh` and `content-loop.sh` loop over the language bundles that have a manifest (today
   only `game-v1`; `content/game-de-v1` has no manifest yet and is skipped with a line saying so), each held to its own level and its
   own LanguageTool language (`es` now; `de-DE` for German when it exists); `ci.yml`'s grammar step does the same. The core's suite stays
   on `content/game-v1` (R02).

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; the locale files and where each went; keys re-keyed; the fallback chain; the gates and CI per bundle;
the scripted runs' comparison; the content loop's results; test counts (`--tier full`); the gate's results; what T06 must know.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
