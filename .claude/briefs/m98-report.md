M98 report (agent a7428425740f65d81, Sonnet 5.5 medium, 2026-11-17 about 07:50 UTC)

Branch `M98-fuzz-health`, head 643bd63f (3703bd5d, 643bd63f on ac8b1391), worktree clean. Only the fuzz project and docs/nlp/findings.md.
1. Realisation-rate gate: `RealisationRateTests` draws 2,000 O3 recipes (one case per fixed seed `M98R<i>`), prints per family the recipes
   realised and the skips and rejected draws by reason, and pins two floors per family about five points under the measured rates:
   catalog 99.57% realised (floor 0.95), 95.12% draws kept (0.90); skeleton 100% (0.95), 98.62% (0.93); random 97.38% (0.92), 86.48%
   (0.80); all 98.95% (0.95), 92.87% (0.88). The only skips: `numeral-next-to-article`, `adjacent-numerals`.
2. M97's fixed-seed coverage test: CsCheck seeds only a run's first case, so the "fixed seed" fixed 1 case in 300. Now one case per seed
   (`M98Open<i>`, 300 cases), floor `min(5, iterations / 60)` unchanged; identical counts in three full runs of the project (lowest 9).
   The M97 README sentence claiming fixed counts corrected.
3. Seed `7q7l8dSNG6n6`: the realiser's fault. `alrededor` is rightly both noun and adverb; the realiser drew it as the item beside an
   optional `[ADV]`, the matcher gives it to `[ADV]`, so no item slot. New re-roll `skip:noun-reads-as-adverb` (a noun that is also an
   adverb, where the pattern has an optional or grouped `[ADV]`); oracle unchanged. `HomographRerollTests` section 7: three facts. The
   seed passes on replay; `FuzzLanguageStreamTests` digests unchanged.
Fresh runs: three, 138/138 each. Build 0 warnings; format: fuzz project only at the end (the solution once, failing on whitespace, fixed).
Full suite 12,456 passed, 2 failed (the valley and streamed-world timing tests, passing alone). Goldens unchanged. CLI demo not run.
Left open: the silent-h seed `a2RKk5N6Ibt4` and `ContentSchemaFuzzTests`' seed `2zwDo8nRB_f7` (open items), untouched.
