You are implementing the core milestone **M36, the German lexicon import**, in the Vocaterra repository (C#, .NET 10). The language core
is `src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and tools; the game
(`Vocaterra.Game*`, `Vocaterra.Host`, `tools/Vocaterra.SpeechRender`) is not yours and must not change. Do the work yourself; do not spawn
subagents. Commit prefix `M36: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m36`, already on the branch `M36-german-lexicon` (from
`feature/german` at its tip; no build yet). cd there first; never edit `/home/james/Dev/Vocaterra` or another worktree (`m87` runs beside
you and changes the engine's pack interfaces; `german`, `bugfixes`, `water` are not yours). Commit after each step that builds and passes
its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the
foreground and finish in one turn (no background commands, monitors or wait loops).

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m36/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/m36/` for your logs, **10395 passed, 3
skipped** as the count before you (re-measure if your first full run differs, and say so), and these changes: skip the scripted runs; add
`bash scripts/license-scan.sh --offline` (PASSED). Another agent builds beside you: every heavy command goes through `scripts/limited.sh`.
**Disk:** about 30 GB free on `/home`; downloads go under `~/.cache/vocaterra/lexicon-sources/` (never into the repository); keep your
downloads under 6 GB in total and delete any you do not pin; check `df -h /home` before each download and build and stop and report under
8 GB. Build the core first (`Vocaterra.Nlp.slnf`); the whole solution only for the final gate.
Rules: edit only `tools/Vocaterra.Nlp.ContentBuilder`, its tests, `scripts/fetch-lexicon-sources.sh`, `docs/nlp/lexicon.md`,
`ATTRIBUTION.md` (a German Wiktionary entry) and the new folder `content/game-de-v1/lexicon/`. Never edit `content/game-v1`, the engine's
pack interfaces (`src/Vocaterra.Nlp/Language/Packs/`, M87 is changing them), the game, either implementation plan, either `decisions.md`,
or anything under `.claude/` or `.github/workflows/`. The Spanish import's output must stay byte-identical. A regression check is a C#
test. Licences: CC BY-SA data is accepted for derived data (as `es_50k` and kaikki Spanish are); no copyleft code.

## Why
The owner decided on 2026-11-08 (D-game-27, `docs/game/decisions.md`; D14 amended, `docs/nlp/decisions.md`) to add German as a second
target language. M36 is the first step of the core's German pack. Its specification is the row in
`docs/research/2026-10-22-multilingual.md` line 426 (and that report's section 2 on German), updated by
`docs/research/2026-11-04-german-in-the-game.md` section 3.3 and spike R4 (section 7). The plan's row: `grep -n '^| M36 '
docs/nlp/implementation-plan.md`. How the Spanish lexicon is built: `docs/nlp/lexicon.md` and `scripts/fetch-lexicon-sources.sh`;
the importer is `tools/Vocaterra.Nlp.ContentBuilder/KaikkiImporter.cs` (it keeps only `lang_code == "es"` around lines 203 and 317).

## What to do (each committed)
1. **Spike R4: pick the source** (report it in a short table). kaikki.org's per-language German JSONL is marked deprecated ("will be
   removed in the near future"). Check, with HEAD requests and small ranged reads before any full download: (a) the deprecated German
   extract, still published?; (b) kaikki's raw wiktextract data (`https://kaikki.org/rawdata.html`) filtered to `lang_code == "de"` (size
   of the download, whether a smaller per-language raw file exists); (c) anything else kaikki now recommends. Pick the one that is
   pinnable by SHA-256, gives gender, plural, case forms and verb forms for A1/A2 lemmas, and fits the disk budget. If only a huge
   all-language file exists, stream-filter it once into a German-only file and pin the **filtered** file's hash plus the source's URL,
   date and hash, as the fetch script's comments explain for Spanish. Frequency: hermitdave FrequencyWords `de_50k` (pin a commit URL, as
   `es_50k`).
2. **Language profile in the importer**: the language code, the POS map, and German tags into `LexicalFeatures` (gender `m`/`f`/`n`, the
   four cases, plural forms, strong/weak/mixed adjective declension where present, separable prefix, the Perfekt auxiliary and the past
   participle), selected per language; Spanish through the same profile, its output byte-identical (prove it: rebuild the Spanish
   lexicon and compare bytes with `content/game-v1/lexicon/`).
3. **The German lexicon file** `content/game-de-v1/lexicon/wiktionary-de.json` (plus its sidecar, as the Spanish one has) at the largest
   top N by frequency (try 2,000) that keeps it at or under 15 MB and loads in at most 300 ms (measure with the core's `Lexicon` reader,
   Release, the bundle read before the clock). The folder is the future German bundle (T06); it has no manifest yet. Check that nothing
   globs `content/*` and trips over it (validators, CI, `scripts/content.sh`); if something does, report it rather than editing it.
4. **Reproducible:** running the import twice gives identical bytes (a test on a small fixture, plus one manual double run on the full
   source). Pins in `scripts/fetch-lexicon-sources.sh` (a `de` set beside the Spanish one; the script's existing behaviour for Spanish
   unchanged). `docs/nlp/lexicon.md` gains the German steps; `ATTRIBUTION.md` the German sources.
5. **Spot-check** (in the report, and as a C# test over the committed file): 50 A1 nouns (gender and plural, cross-checked against
   `german-nouns`, CC BY-SA 4.0, https://github.com/gambolputty/german-nouns, only if fetching it is cheap) and 30 verbs (strong or weak,
   separable, `haben`/`sein` auxiliary). List the mistakes you find and whether they come from the source or the importer.

## Report (one page, under 500 words)
Branch, final SHA, worktree clean; R4's table and the chosen source with its pin; the file's size, entry count and load time; the spot
check's results; the Spanish byte-identity proof; test counts before and after (`scripts/test.sh full`); the gate's results; anything left
undone and why; what the orchestrator should record in the plan's §5 and §5b.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
