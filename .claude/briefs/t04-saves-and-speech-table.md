You are implementing the game milestone **T04, saves per language and the speech table**, in the Vocaterra repository (C#, .NET 10).
The game is `Vocaterra.Game.Core` (engine-free logic), `Vocaterra.Game` (MonoGame), `Vocaterra.Host` and `tools/Vocaterra.SpeechRender`;
the language core (`src/Vocaterra.Nlp*`) is not yours and must not change. Do the work yourself; do not spawn subagents. Commit prefix
`T04: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/t04`, already on the branch `T04-saves-and-speech` (from
`feature/german` after T03 merged; no build yet). cd there first; never edit another worktree. Commit after each step that builds and
passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the
foreground and finish in one turn (no background commands, monitors or wait loops). Never use `git stash`; set work aside with a WIP
commit. **Other sessions build on this machine:** every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it
wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/t04/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment**, **Rules** and **Gate**: they apply to you, with `/tmp/claude-1000/t04/` for your logs, **10580 passed, 3 skipped** as the
count before you, and these changes. **What this milestone edits:** the game projects, their tests, `content/game-v1/game/` and
`content/schema/` for a new speech table, `content/README.md` for its row, and `docs/game/` (not the plans or the decisions). Never the
core, `content/locale/`, `.claude/` or `.github/workflows/`. A content edit means the content loop (the w07 brief's last Rules bullet and
`content/README.md`, "The edit loop"): `scripts/content-loop.sh --grammar` after each content edit (run directly, never under limited.sh),
`--tier full` once before the report instead of `scripts/test.sh full`. **Saves:** the format changes, so raise the save version (T03 made
it 8), refuse older saves with a clear message and re-record `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json` and `save-v4.json`;
no migration code (the game is unreleased; owner, 2026-11-04). **The scripted runs** (`scripts/scripted-runs.sh` before your first edit,
complete, and after your last, compared with `python3 -I scripts/compare-runs.py`, one run at a time if the whole capture is slow): no
transcript may change; save files and save paths will (say exactly how); explain any screenshot difference.

## Why
The owner decided on 2026-11-08 (D-game-27, `docs/game/decisions.md`, items 2 and 3): one save profile per target language under
`saves/<language>/` with a required `language` field, skill ids unqualified inside a profile, older saves refused; the save records the
bundle hashes (both, once T05 splits the world from the language: leave room for a second hash, but T05 adds it). Specification:
`docs/research/2026-11-04-german-in-the-game.md` section 4.1 rows G6 and G8, section 5.3; the row T04 in
`docs/game/implementation-plan.md` section 4.10; T03's section 5 entry (the save version it raised).

## What to build (each committed)
1. **The language in the save.** A required `language` (the bundle manifest's language; a bundle with none is `es`, ADR 0011) written
   and checked on load: a save whose language differs from the loaded bundle's is refused with a clear message, as a wrong version is.
2. **Profiles per language.** The save folder becomes `saves/<language>/` (find where the save path is built: the host, the game's
   launch options, the title screen's continue and new-game paths, crash and report tools that read the save). The title screen, the
   load and the `--save`/`--load` style options keep working for Spanish; there is no language choice in the UI yet (T09).
3. **Gender `n`.** `LearnerWords.Gender` (and anything that validates a gender: words, nouns, the Book's article table) accepts `n`
   alongside `m` and `f`; a test with a neuter noun.
4. **The speech table from data (G6).** `SpeechEngine` (`src/Vocaterra.Game.Core/Language/Speech/SpeechEngine.cs`, about lines 24 to 33)
   hard-codes the ASR model (`vosk-model-small-es-0.42`), the TTS engine and the guide's voice (Kokoro speaker 28 `ef_dora`), and
   `SpeechRules.cs` (about lines 18 and 28) its thresholds tuned on Vosk small Spanish. Move them into a per-language speech table in the
   bundle's game data (for example `game/speech.json`: ASR model id from `models/manifest.json`, TTS engine id, the guide's voice,
   thresholds keyed by ASR model), with a JSON Schema, loaded through `GameData`; the NPCs' voices stay in `game/npcs/<id>.json`. A
   missing or unknown model id fails the content validation, not at run time. Spanish values identical to today's constants.
5. **Tests:** the save round trip with `language`, the refusal of a wrong language and of an old version, the profile path, a speech
   table with a non-Spanish model id (it is read, not run).

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; the save's new fields, version and path, and every place that builds the path; the speech table's
shape and who reads it; the scripted runs' comparison; the content loop's results (bundle hash); test counts (`--tier full`); the gate's
results; core gaps (reported, not patched); what T05 should know.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
