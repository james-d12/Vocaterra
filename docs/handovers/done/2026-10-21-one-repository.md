# Handover: the game before the one repository, and the prototype brief (2026-10-21)

> Moved on 2026-11-03 from `docs/game/IMPLEMENTATION_PLAN.md` section 4, where it stood above the milestone table; the text is unchanged. Historical: verify any state against git before acting on it.

## Handover (2026-10-21, end of session; the owner plans to merge the game and the core into one repository)

**Done, 2026-10-21 (later the same day):** the repositories are one (D-game-7). The core is `nlp/`, the game is `game/`; the notes below
were followed as listed (see the section 5 entry "One repository"). P8 and later are unblocked.

Where things stand (as written before the merge):
- **Done and pushed:** G0, G1, G2, R1, P1, P2, P3, P4, P4b, P6, G6, P6b, G7, P5, P7. Core pin `53c52b7` (M26a to M26e). Tests on `main`:
  625 (8 need the speech models in `VOCATERRA_MODELS_DIR`). Bundle `game-v1` `17ae6d14…`, 28 files. Licence MIT (D-game-2). No worktrees,
  no agent branches, no agent running.
- **The owner stopped after P7** (2026-10-21): P8 (the guide speaks), P9 (save and load), P10, G3 and G4 wait for the merged repository
  (D-game-7). The models P8 needs are fetched (`~/.local/share/vocaterra/models`: Vosk small es, Kokoro).
- **Core requests:** done in the core but not in the pin: CR-010, CR-011 (M26f, content tool), CR-015 (`UtterancePipeline.Advance` with
  `onHostEvent` transitions: the guide's review question can become a core turn), CR-017 (`Analyze` then `Complete`: the guide can evaluate
  once and drop the double evaluation). Accepted, not done: CR-014, CR-016, CR-018 (M28). Open: CR-019 to CR-022 (P5), CR-024 (fresh-clone
  pack). Each of these becomes ordinary work once the repositories are one.
- **Fresh clone today:** see `CLAUDE.md` (CR-024): the core's pack fails on a clean machine unless `external/Vocaterra.NLP/nuget.config`
  (uncommitted, `<clear/>` + nuget.org) exists first; after a pull that moves the submodule, `git submodule update --init`, delete
  `~/.nuget/packages/vocaterra.nlp.*` and pack again. The README's build steps (line 46) still lack this note.
- **Owner items still open:** the orchestrator defaults in the P4b entry (wrong names earn nothing; Esc ends a review); the ASR confidence
  choice in P5 (CR-022: Vosk asked for one hypothesis, so the core's confidence gate is off); live push-to-talk with real speech is untried;
  content pass items in the P4, P5, P6b and G6 entries (recast wording, "Habla más despacio" for an unknown intent, "¿de dónde sale la
  madera?" while carrying wood, "Usa mesa" without the article).

**Notes for merging the game and the core into one repository (D-game-7).** What exists only because they are two repositories, and what
to do with each:
1. *Packages only* (core ADR 0009, CLAUDE.md, section 3 rule 1): becomes project references inside one solution; `Directory.Packages.props`
   drops the `Vocaterra.Nlp.*` entries; the direct `Vosk` and `Microsoft.ML.OnnxRuntime` (`ExcludeAssets="native"`) references (P5,
   CR-021) are still needed in the executables and test projects that run Vosk and Kokoro.
2. *The submodule, the local feed and `nuget.config`*: remove `external/`, `.gitmodules` and the local source; `scripts/content.sh` installs
   the content tool from the feed (`.tools/`) and becomes a `dotnet run --project` of the core's ContentBuilder (or keep the tool, packed
   in-tree). The stale 0.1.0 cache problem (P4, CR-024) and the pack-before-build step disappear.
3. *The merge gate*: `scripts/merge-gate.sh` (game) and the core's `scripts/merge-gate.sh` become one; keep the game's package-folder
   isolation only if packages remain, the `VOCATERRA_MODELS_DIR` export, the content validation, and the core's own checks (licence scan,
   goldens).
4. *Two orchestrators and their files*: `.claude/agents/orchestrator.md` (game) and the core's role file become one; `docs/game/CORE_REQUESTS.md`
   rows that are still open become plan items; the two `IMPLEMENTATION_PLAN.md` files and the two `DECISIONS.md` files merge (the core's
   D1 to D13 and the game's D-game-1 to D-game-7; ids stay as they are).
5. *Determinism and replay*: unchanged; `HostDigest.Of` and the game's host slots stay as they are.
6. *Bundles*: `content/game-v1` (the game's) and the core's sample `content/v1` both stay; D-game-4 (diverge) still holds.
7. *Models*: `ModelStore` reads `manifest.json` from the models folder; in one repository the manifest path is fixed and CR-019's fallback
   (`VOCATERRA_MODELS_MANIFEST`, the submodule path in `SpeechEngine`) changes to the in-repo path.
8. *Licences*: the game is MIT, the core Apache-2.0 (core D3): the merged repository needs one decision (or per-directory licences).
9. *CI*: still deferred (D-game-3, core D1).

**Now (2026-10-21, night): the S series is running** on `feature/prototype` (owner: through S12 overnight, push after each merge; the Opus
review of the phrase sheet and the content hash step approved; the owner's own steps, listening to the S9 WAVs and following the S12
checklist, wait for the owner). Design per milestone: section 4.4 and `docs/game/SCENARIO_PLAN.md`.

**Previously (2026-10-21, evening): the Prototype Design Brief.** The owner's brief (`docs/game/design/sources/Vocaterra_Prototype_Design_Brief.docx`, v1.2) replaces the
Terraria design: a 3D first-person voxel slice on MonoGame (forest, path, village stall, river; two or three NPCs; five phrases; speech UX and
learning checks). The plan for it is `docs/game/SCENARIO_PLAN.md` (milestones S0 to S12, decisions D-game-8 to D-game-12, D-game-6 amended). The
orchestrator runs **S0** first: it folds that document into this section, records the decisions, and marks P8, P9, G3 and G4 superseded.
Until S0 is done, `docs/game/SCENARIO_PLAN.md` is the plan of record for the scenario.
