# M36 paused (2026-11-08, machine load): the agent's interim report, condensed

WIP commit `04f2f931` on `M36-german-lexicon` (local, unbuilt, untested); worktree `.claude/worktrees/m36` clean.

- Spike R4: the deprecated German extract is still published (`kaikki.org-dictionary-German.jsonl`, 1,095,258,648 bytes,
  SHA-256 `4fb15662a125085e1eb6b533a210b45edcdffa7b8e72e07634f46e1485e4e715`, enwiktionary dump 2026-10-01); the raw wiktextract
  file is 3.1 GB gz (24.8 GB), all languages, also deprecated; the de.wiktionary extract (310 MB) has German glosses. Chosen: the
  German extract, already in `~/.cache/vocaterra/lexicon-sources/`. `de_50k` pinned at FrequencyWords commit 525f9b56, SHA-256
  `d9e50546…`.
- The Spanish kaikki file upstream no longer matches the pin in `scripts/fetch-lexicon-sources.sh` (now 1,119,672,965 bytes,
  `336903d1…`). Not re-pinned; Spanish byte identity to be proven old importer vs new on the same file, plus a fixture test.
- In the WIP: `KaikkiLanguageProfile` (Spanish, German), `GermanKaikkiProfile`, `import-lexicon --language es|de`, a `FrequencyKey`
  hook (identity for Spanish), the sidecar version per profile (es 1.1.0, de-1.0.0).
- Next: build, the Spanish byte check, German import top 2000 (≤ 15 MB, ≤ 300 ms), the file and sidecar under
  `content/game-de-v1/lexicon/`, pins, docs, attribution, the spot-check test, the gate.
- Known gaps: no forms table for `ein` in the source; no comparatives or superlatives; reflexive verbs skipped; no Konjunktiv I or
  compound tenses.
