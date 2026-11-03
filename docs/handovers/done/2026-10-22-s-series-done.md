# Handover: the S series done (2026-10-22, morning)

> Moved on 2026-11-03 from `docs/game/IMPLEMENTATION_PLAN.md` section 4, where it stood above the milestone table; the text is unchanged. Historical: verify any state against git before acting on it.

**Handover (2026-10-22, morning): the S series is done** on `feature/prototype` (pushed; `main` untouched). S0 to S12 and S4b merged
through the gate; core M29c, M29e, M29f, M29g, M29h merged; 3,889 tests pass (5 model-gated skips without models). Bundle `game-v1`
`cd63077b…` (253 files, 226 pre-rendered WAVs). The playtest: `--playtest` (mic check, pre-check, the route, post-check after the transfer
or F10, `playtest.save`, thank-you), recall with `--playtest --load <folder>/playtest.save`, `scripts/playtest-report.sh`, the private
linux-x64 folder from `scripts/playtest-build.sh` (no sherpa-onnx or ONNX Runtime native; Vosk model copied; 193 MiB).
**Waiting for the owner:** (1) listen to every WAV and mark `docs/game/SPEECH_CHECKLIST.md` (D-game-12 step 4); (2) the pre-flight in
`docs/game/PLAYTEST.md` section 12 — real keyboard, mouse look, E/T/V, push-to-talk with a real microphone, F10 and Esc were never exercised
by an agent (no input automation on this machine); (3) core D2: does handing the private build folder to testers count as distribution
(the WAVs were rendered by Kokoro through sherpa-onnx, whose native runtime carries GPL code; the folder ships only the managed wrapper);
(4) the native-speaker check stays deferred (D-game-12; rows provisional, reviewed by llm-opus); (5) whether to merge `feature/prototype`
into `main`; (6) freeze the bundle before testers' recall sessions (a save refuses another bundle hash). Proposed, not started: core M29b
(retry with the repair's request), M29d and M29i (fuzz findings), M28b (item-dependent replies: today "quiero pan" at the stall also gets
the bottle line), M28c, M28d.
