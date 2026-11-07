M80 report (2026-11-07, Sonnet 5.5 high). Branch M80-open-fallback (local): cdb37709 (switch, tests, docs), 141c750a (Bench), 8e676da6
(measurement). Build 0 warnings, format clean, validate passes. Full suite 9874 passed, 1 failed (the measurement), 3 skipped (was 9852/3).

Built: PipelineOptions.OpenRecognitionFallback (default false); UtteranceAnalysis.OtherPass; one private Read for the stages after
recognition; posteriors once; second decode only when on, Constrained prompt and first recognition, first reading not taken; the taken
second wins. ScriptedWordRecognizer(constrained, open) + Requests (new ctor makes `new ScriptedWordRecognizer(x, null)` ambiguous; unused).
Bench --open-fallback (upper bound) and --open-vosk; BenchRunner.TimedRecognize the only clock. Docs api.md 2/3/8, pipeline.md, replay.md,
ADR 0009 note, building-and-testing.md, fuzz README and O14. OpenFallbackTests (18), BenchTests (+3), fuzz O14 fact. Goldens, identity,
replay, stream digests unchanged.

Measurement (8 voice variants, 621 evaluations, 345 clips, Vosk small): trigger reach 87/120 off-prompt (73%); understood after the free
pass 63/87 off-prompt (72%), 45/45 allowed-outside-rows reached (33% of 136); FALSE TAKES 2/96: «ese libro es muy interesante», ef_dora@1.0,
fisher-open and farmer-open, heard «el libro es muy interesante» (el 0.45, mean 0.89) -> describe; a weakest-word floor 0.5 stops both and
loses 1 of 108 understood (0.6 loses 6). Wrong takes 6: «usted hace hachas muy buenas» heard with a leading «si» -> confirm (voices 28,
29, 53 at 1.0); floor 0.5 stops 2. Noise/silence 21/21 untaken. Rows left by first pass: 8 (hola, ef_dora@1.0 and im_nicola). Forced but
weak 8/391. Time: constrained median 24 ms (p95 50); free median 190 ms (p95 268); over 3 s 336-369 ms. Accent risk unmeasured.
Design vs code: test in Nlp.Tests (no game SpeechEngine); resamples without the game's silence trim; answer-row coverage on two voices.

Orchestrator (2026-11-07): typed «ese/el libro es muy interesante» at fisher-open is unknown in the CLI, so the free pass taking it
contradicts "spoken and typed alike"; the agent was sent back to find the cause before the owner is asked.

Follow-up (4e7ca7f9): not a bug. The CLI also prints `off-prompt: describe` for typed «el libro es muy interesante»; typed and the free
pass agree (new test OpenFallbackTests.TheFreePassReadsAHeardLineAsTheSameTextTypedIsRead, 7 texts x 7 prompts). The false take is Vosk
hearing «ese» as «el» (0.45). The wrong takes match typed «si usted hace hachas…» (off-prompt confirm). Measurement unchanged. Test count
9881 passed + 1 failed (the measurement) by the agent's arithmetic; full suite not rerun.
