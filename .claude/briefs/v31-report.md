V31 report (Sonnet 5.5 medium, 2026-11-07). Branch V31-open-decode (local) at 634915ef. Full suite with models 9915 passed, 0 failed, 3
skipped (was 9901/3); build clean, format clean, validate passes. O7 did not fail.

Built: SpokenUtteranceEvaluator holds a second pipeline (OpenRecognitionFallback on, the core's default 0.5 floor); Analyze(clip, prompt,
freeTalk) uses it only in free talk (NpcService.Take passes `scene.PromptOverride is null && !scene.Review`) at a prompt in FreeTalkPrompts.
LineReading.Read overload with OtherPass: when the chosen reading is not taken, reads OtherPass with the vocative out and the greeting rest;
a re-read free recognition is held to PipelineOptions.DefaultOpenPassWordFloor (the core's constant). Credit: GuideTurn.HeardOpen and
CreditKind (typed when heard open; Input stays Spoken); word credit and PhraseUse use CreditKind. Timings: TimedRecognizer sums a turn's
decodes; SpeechEngine.BeginTurn(), RecognizeCount; TurnTimings.Decodes. Telemetry: VoiceEventKind.HeardOpen (`heard_open`); the session
log's turn record has heard_open (when true) and decodes (above 1); console ", heard open". Scripted runs keep 0.0 ms.
Tests: OpenDecodeTests (14): scripted by mode (Te entiendo x3, allowed line outside rows, vocative, lesson step / review / open prompt / mic
check decode once, a word under 0.5 not taken, mean under 0.55 gives «No te entendí bien», typed credit, voice event and Decodes); Kokoro
at each NPC, a lesson step with a counting recognizer, the worker (2 decodes off-prompt, 1 for a row). ConstrainedRecognitionTests
unchanged; "heard_open" added to SpeechUxTests' wire names. Docs: docs/game/architecture.md speech bullet.
Kokoro (ef_dora): smith «me gusta el río» Te entiendo; smith «mañana voy a comprar pan» state_action as typed; fisher, farmer «tengo dos
manzanas» Te entiendo; baker «hoy llueve mucho» «¡Sí!»; shopkeeper «el pan es muy bueno» «Sí, es verdad.». Swapped lines: the constrained
decode forced a confident row (baker «el río es grande» -> «es gracias», thank; shopkeeper «me gusta el río» -> «me cuesta el río»,
ask_price); unheard: «mi hermano vive en Madrid», «la luna brilla».
Design vs code: PromptSpec.PromptId; villager-open and guide-open are Open prompts, so the constrained free-talk set is baker, smith, farmer,
fisher -open, stall-need, stall-offer, guide-offer (the villager decodes once); reviews are scene.Review; the vocative re-read goes through
AnalyzeRecognized with the fallback off.
Scripted runs: only r03-npcs, r11-mic, r17-speak, r24-followups captured (same, order only) — the orchestrator sent it back for the full set.
For the orchestrator: D-game-9 note: "V31 (2026-11-07): free talk at the seven prompts (baker-open, smith-open, farmer-open, fisher-open,
stall-need, stall-offer, guide-offer) decodes a missed line a second time, freely (core M80, 0.5 floor). A line heard so is answered as the
typed line is, credited as typed (half), and marked by the `heard_open` voice event and the session log. Lesson steps, reviews and the mic
check decode once. The playtest counts `heard_open`." Village plan section 5: close the open point on spoken off-prompt lines.
Left: accent risk unmeasured; the constrained decode can force a wrong row confidently (never reaches the open decode).
