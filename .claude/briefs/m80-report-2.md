M80 floor report (Sonnet 5.5 high, 2026-11-07). Branch M80-open-fallback (local) at a84b15e2: 29f25db4 merge of feature/bread-pilot (no
conflict), f4bb1d04 (the floor, tests, O14), 301a09af (measurement at the floor), a84b15e2 (docs).

Option: PipelineOptions.OpenPassWordFloor (double, default PipelineOptions.DefaultOpenPassWordFloor = 0.5), used only by the open pass: its
reading is taken only when taken as before and its lowest AsrWord.Confidence >= the floor (exactly the floor counts); otherwise the first
pass stands and the free recognition is OtherPass. Empty or no word confidences: not taken. Floor 0 = before the floor. No effect with the
fallback off. Out of [0, 1] or NaN: ArgumentOutOfRangeException from the UtterancePipeline constructor (the first validated option).

Tests: OpenFallbackTests +8 (default and range; 0.49 not taken, 0.5 taken; the floor moves the line; 0 as before; no confidences; first
pass untouched at 1.0; no effect with the fallback off). O14 strengthened: random word confidences and floors 0/0.5/0.8/1; fails on a taken
free reading under the floor or with no words; a new `second-under-floor` count must be reached. Measurement runs at the default floor
(passes) and at 0 (memoised decodes) and prints what the floor removed. Goldens, identity, replay, digests unchanged; nothing re-recorded.

Measurement (8 voice variants, 621 evaluations, 345 clips, Vosk small, Kokoro only): false takes 0. Off-prompt reach 87/120, understood
63/87 (72%); allowed-outside-rows 44/45. Wrong takes 4: «usted hace hachas muy buenas» heard «si usted hace (hachas) muy buenas» -> confirm
(ef_dora@1.0, em_santa@1.0, at guide-offer and smith-open); typed «si usted…» is confirm too; weakest word >= 0.56, the floor cannot help.
Floor removed: both false takes («ese libro es muy interesante», ef_dora@1.0, fisher-open and farmer-open, `el` 0.45) and 2 of 6 wrong takes
(em_alex@1.0, «si usted hace años…», `años` 0.26). Understood lost: 1 of 108 (pm_santa(pt)@1.0, «la tienda está en el pueblo», farmer-open,
`la` 0.46; 0.6 would lose 6). Noise/silence 21/21 untaken; first-pass rows left 8 («hola»); forced but weak 8/391. Time: constrained median
30 ms (p95 60), free median 237 ms (p95 342). Accent risk on real speech unmeasured.

Docs: api.md ("no confidence gate" replaced, figures added), pipeline.md, replay.md, building-and-testing.md, fuzzing-plan.md, ADR 0009
note, fuzz README. Gate (its own): build clean, full suite with models 9893 passed, 0 failed, 3 skipped; format clean; validate passes.
