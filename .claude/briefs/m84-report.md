M84 report (Sonnet 5.5 high, 2026-11-08). Branch M84-forced-rows (local) at 4ccf2fc4 from ed1aa937; worktree clean.
Measured: 2541 evaluations, 843 clips, 8 voices; 1501 taken constrained readings: 852 correct rows, 241 other lines read as typed, 408
forced. Coverage of voiced audio: correct 0.76-1.00 (median 1.00), forced median 0.54 (0.06-1.00). Weakest word: correct median 1.00 (min
0.36), forced median 0.71. Answer match: correct Exact 804, Alias 11, Fuzzy 37, None 0; forced Exact 0, Alias 45, Fuzzy 65, None 298.
Speech per letter and [unk] separate poorly. Coverage alone misses V31's shopkeeper case (coverage 1.00, weakest 0.98).
Trigger (PipelineOptions, only with OpenRecognitionFallback): OpenPassOnLooseMatch (default true: match None or Fuzzy) and
OpenPassMinCoverage (default 0.6, 0..1, 0 off); the free reading wins only when taken, over OpenPassWordFloor, different intent. Both off =
M80. Code: Pipeline/ForcedReading.cs, EnergyVad.SpeechFrames.
Bars: 0 of 852 correct rows replaced, 0 of 241 other correct lines, 0 false takes. Rescued 297 of 408 (every V31 line). 1 wrong take
(«usted hace hachas…» as confirm, as in M80). Extra open decodes 680 of 1501 (45%, an off-prompt-heavy corpus); on correct rows 37 of 852
(4%). Extra time median 199 ms, p95 365 ms. Cheaper option: coverage < 0.8 alone fires on 28%, rescues 224, misses the shopkeeper case.
Tests: ForcedRowsTests (scripted); O14 strengthened (mutation-checked); model-gated measurement asserts both bars and >= 100 rescues.
Docs: api.md, pipeline.md, replay.md, ADR 0009 note, fuzz README, fuzzing-plan.md, building-and-testing.md, Bench help.
Checks: build 0 warnings; full suite 10361 passed, 3 skipped; format, validate clean; no golden, baseline, replay or digest moved.
V32 not needed: LineReading/NpcService use Asr.Mode and OtherPass unchanged; a replaced row shows as heard_open, credited as typed. Game
wording to fix: docs/game/architecture.md and the D-game-9 note say the second decode runs only for a missed line; heard_open and decodes
counts rise; about +200 ms on affected turns. Risks: accent unmeasured; coverage uses -40 dBFS on the raw clip (noise costs decodes, not
wrong replacements); a replay has an empty clip, so only loose-match fires there (same verdict).
