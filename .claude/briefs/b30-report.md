B30 report (agent a7dbf9a168087dd05, Sonnet 5.5 medium, 2026-11-18 about 01:00 UTC)

Branch `B30-ci-ready`, head 2229d68a (two commits on 21f632e0), worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b30`.
Timing factor: `CpuTime.Factor` / `CpuTime.Budget` (Game.Core.Tests TestSupport) and `TimingFactor` (Nlp.Tests): 1 locally, 4 when `CI` or
`GITHUB_ACTIONS` is true, `VOCATERRA_TIMING_FACTOR` overrides. Local / CI budgets (ms): procedural chunk surface, stone, column, deep 14, 13,
41, 13 / 56, 52, 164, 52; streamed band, made, per chunk, plan 13, 23, 6, 2 / 52, 92, 24, 8; snapshot 5,900 / 23,600; valley underground
5,000 / 20,000; scene 7,300 / 29,200; lexicon 1,082 / 4,328. Also scaled: the lint (5 s), GuideTurnTests (250 ms a turn), WorldFactsTests
(1,500 / 3,000 ms), ChunkIndexTests (100 ms), WorldEditsSaveTests (2 s), FarTerrainSamplerTests (20 s), ProceduralReliefRegionTests (5 s),
DrainageNetworkTests' dry column (100 ms).
CI time: a run as CI does it (Debug, CI=true, an empty models folder, no GL tests, `taskset -c 0-3`) at load about 18: build 282 s, tests
2,602 s (about 43 minutes); every CPU timing test passed at factor 4; `build-and-test` `timeout-minutes: 120`; Debug kept.
Hazards checked: NamePlatesTests returns early without DISPLAY; live-GL tests skip; model-gated tests skip; the far store folder writable.
JobPool resize race: each job holds until all 8 workers have started one (a bounded 30 s wait on the thread count, a clear message); the
gate timeout 2 to 60 s; 16 JobPoolTests pass. The CI-like run before that fix: 12,769 passed, 2 failed (M101's fuzz seed, fixed on its
branch, and the JobPool race). Build and format clean; the full suite not re-run after the last edits.
docs/game/building-and-testing.md: "CI on a slow runner (B30)".
