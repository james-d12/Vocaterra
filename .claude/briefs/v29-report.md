# V29 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `V29-weather-wellbeing-replies`, final commit `4909f9b1` (first `e7042d54`), pushed, worktree clean. Base b7ef9a9c.

- Acknowledgement pools and slot replies are data in the NPC files (`acknowledgementPools`, `slotReplies`; schema and `GameDataValidator`);
  `NpcTurnRules.Compose` takes the `NpcEntry`; variants are drawn through `NpcTurnRules.Pick`, seeded by world seed and interaction counter
  (stream `npc.reply`).
- `state_weather` and `ask_weather` are allowed at the six open prompts, `stall-need` and `guide-review` (which `SpacedCreditTests` keeps equal to
  `guide-open`); `ask_wellbeing` is added at `stall-need`.
- Four pools per NPC (7 NPCs): `<p>_yes` «¡Sí!», `<p>_weather` «Sí, hace {weather}.», `<p>_sun` «Hoy hace sol.», `<p>_well` «Bien, gracias.».
  The echo covers the grammar's 11 weather words; `llueve`, `nieva` and `está nublado` get «¡Sí!». `<npc>_sun` is the seam for real weather.
  The template pool is reached through a host event `<npc>.weather` that never fires.
- Speech: 98 lines, 196 WAVs; total 639 lines, 1278 files. English in `content/locale/en/lines.json`. M74's `ask_wellbeing` twins removed.
- Book: free talk already credits `calor` once as a lexicon noun; tested.
- `ask_craft` at `smith-open` left off: allowing it reads `quiero hacer un hacha` as ask_craft (pinned by a test).
- `GameAndHostRepliesTests`: 9 lines give identical replies through the host and the game.
- Bundle hash `1392a61949ddc6df116e48c716c641cf59a3a9cef3aaff3312d2426c52128b98` (save-v1 and save-v4 fixtures).
- Scripted runs: 24 before and after, only the speech count line differs.
- Core tests moved: 37 identity-baseline entries; about 50 grading rows from unknown/off-prompt to the understood intent at open prompts;
  `¿qué tiempo hace?` at smith-open and farmer-open now ask_weather; LintTests `reach.intent` expectation empty.
- Tests: with models about 9063 passed, 3 skipped, 1 failed; without models 9021 passed, 45 skipped, 1 failed. Build 0 warnings, format clean,
  validate passes, A2 lint clean.
- **Blocker:** `GrammarTests.TheCommittedCacheReplaysTheBundleOfflineWithItsWaivers` fails: `content/grammar/game-v1-cache.json` lacks the new
  strings (the loop's refresh counts 39 misses). In the sandbox the dotnet process cannot reach LanguageTool on localhost (curl can; the
  orchestrator confirmed it on 2026-11-05 with `localhost` and `127.0.0.1`, with and without limited.sh). The agent's request to run outside the
  sandbox was denied, so it is the owner's: run `scripts/content-loop.sh --grammar --tier none` once outside the sandbox on the branch, read the
  new findings, commit the cache.
- Village plan: only its "Open points" subsection edited (speech count, V29 bullet).
