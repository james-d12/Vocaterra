> **Archived 2026-11-03** from `docs/organisation-plan.md`: a proposed documentation layout, not carried out. The layout adopted instead: [the documentation rules](../process/documentation.md).

# Documentation review and organisation plan

Reviewed: 2026-11-03. Status: proposed migration; only the documentation index and the new handover convention are implemented.

## What the repository has

The `docs/game/` and `docs/nlp/` split matches the project boundary and should stay. Each area already has a README, an implementation
plan and a decision register. NLP also has useful `adr/`, `research/`, `history/` and `diagrams/` folders; game research and the shared
`qa-reports/` folder are already grouped sensibly.

The problems are navigation, document purpose and freshness:

- There was no `docs/README.md`. Shared engineering guidance, performance work, a bottlenecks study, two handovers and a Word design
  document sat together at the root without an index.
- Session state is scattered across `HANDOVER_*.md`, dated sections of the orchestrator, `R16_PHASE2.md` and a research document.
  The orchestrator contains snapshots from October 23 and 26 and November 1 and 2, including instructions for branches and merges
  that must be checked against current git state.
- Current guidance shares folders with completed migration plans and original design sources. For example, `R16_PHASE2.md` is
  explicitly marked done, but sits next to the current architecture; `PROTOTYPE_BRIEF.md` has a Word source alongside it.
- The game README is 1,210 lines and the NLP README 559. Both combine onboarding with substantial reference material and milestone
  history. The implementation plans are 2,359 and 2,068 lines, combining live status with a long findings log.
- Some descriptions have drifted: the game README's opening says Terraria-like while the specification describes the voxel
  prototype; the NLP README's known gaps still describe CI activation as blocked, although CI is now active. Moving files alone
  will not fix this.
- Paths are used outside Markdown links. `Directory.Build.props` includes `docs/nlp/PACKAGE_README.md` as package content, and briefs,
  instructions and commands name document paths literally. A link check alone will miss those references.
- `docs/Vocaterra_Adaptation_Design_Document.docx` is an existing **untracked** file, about 32 MB. Its contents were not reviewed.
  Leave it untouched and untracked during this cleanup; its eventual inclusion and role need a separate owner decision.

## Proposed layout

Keep stable entry points and the existing domain split. Add small purpose-based folders only where they make documents easier to find.
This is a target layout, not a list of folders to create empty.

```text
docs/
  README.md                       navigation and authoritative entry points
  organisation-plan.md            this migration plan
  handover/
    README.md                     naming, template and resumption rules
    YYYY-MM-DD_HH-mm-ssZ_<name>.md  new session snapshots in UTC
    legacy/                       older snapshots with their original names/dates
  engineering/
    code-guidelines.md
    test-performance-plan.md
    research/development-bottlenecks.md
  game/
    README.md                     quick start and links
    SPECIFICATION.md
    ARCHITECTURE.md
    IMPLEMENTATION_PLAN.md        milestone status and current findings
    DECISIONS.md
    plans/                        feature plans still used for work
    design/                       readable briefs and owner design notes
      sources/                    original Word documents
    guides/                       running, playtesting, content, speech, saves, UI
    research/                     studies and reviews; keep noun-town/ together
    history/                      completed plans and older findings, with status notes
    voice-trial/                  recordings, trial.json and their instructions together
  nlp/
    README.md                     quick start and links
    API.md, PIPELINE.md, REPLAY.md published technical reference paths retained
    IMPLEMENTATION_PLAN.md
    DECISIONS.md
    PACKAGE_README.md             retained: referenced by package metadata
    plans/                        specialist plans still in use
    guides/                       worked CLI examples and other extracted how-to material
    adr/, research/, history/, diagrams/
  qa-reports/
    README.md
    <existing report convention>
```

Keep `content/README.md`, `content/PHRASES.md`, `scripts/README.md`, `models/README.md` and test READMEs beside the things they describe;
link to them from the documentation index rather than duplicating or relocating them.

## Move and extraction map

All paths below are relative to `docs/`. Update incoming references and the moved document's relative links in the same change.

| Current document(s) | Destination or treatment |
| --- | --- |
| `HANDOVER_AFTER_FOLLOWUPS.md`, `HANDOVER_VILLAGE_PHASE_B.md` | `handover/legacy/` with original basenames; preserve known dates, do not invent times |
| Dated handovers embedded in the orchestrator | Extract into `handover/legacy/` with their recorded dates; keep durable operating rules in the agent and link to snapshots |
| NLP language-pack research section 7 | Separate session state into `handover/legacy/`; retain the research and a link to the snapshot |
| `code_guidelines.md` | `engineering/code-guidelines.md` |
| `TEST_PERFORMANCE_PLAN.md` | `engineering/test-performance-plan.md`; separate durable test-tier instructions from completed optimisation history |
| `DEVELOPMENT_BOTTLENECKS.md` | `engineering/research/development-bottlenecks.md`; retain its date, evidence and historical measurements |
| `game/ACTIVITIES_PLAN.md`, `CONTENT_PLAN.md`, `VILLAGE_PLAN.md`, `SCENARIO_PLAN.md` | `game/plans/` while still used; label completed or superseded series and archive only once their live dependencies are resolved |
| `game/R16_PHASE2.md`, `GAME_PROJECTS_LAYOUT.md` | `game/history/` as completed migration records; current layout remains in `ARCHITECTURE.md` |
| `game/PROTOTYPE_BRIEF.md`, `Vocaterra_Crafting_Cooking.md` | `game/design/`; retain their relationship to the specification, review and W-series plan |
| The two tracked Word documents in `game/` | `game/design/sources/`, linked to their readable brief or review; preserve the originals |
| `game/CORE_SYSTEMS_REVIEW.md` | `game/research/core-systems-review.md`; link the source document and resulting activities plan |
| `game/PLAYTEST.md`, `SCRIPTED_RUNS.md`, `SPEECH_CHECKLIST.md` | `game/guides/`; preserve run commands and the checklist's evidence/status |
| Long game README sections | Extract coherent guides for world/running, content and speech, lessons, saves and UI; leave quick start and a guide index |
| NLP README demo material and `CLI_EXAMPLES.md` | One CLI guide under `nlp/guides/`; retain a minimal demo in the README |
| `nlp/FUZZING_PLAN.md` | `nlp/plans/`; keep execution instructions linked to the fuzz test project's README |
| `nlp/API.md`, `PIPELINE.md`, `REPLAY.md`, `LEXICON.md`, `TOOLS.md`, `CONTRIBUTING.md`, `PACKAGE_README.md` | Retain these established paths; link them clearly from the NLP README |
| `game/voice-trial/`, domain research, NLP ADRs/diagrams and `qa-reports/` | Keep their current grouping and filenames |

For the untracked adaptation design document, the potential home is `game/design/sources/` if the owner decides to include it. This
plan neither imports it nor makes its contents authoritative.

## Document rules

- **One home for each fact.** Implementation plans own milestone status; decision registers and ADRs own decisions; reference docs
  describe current behaviour; handovers record session state; QA reports and research preserve findings at a stated point in time.
- **Make freshness visible.** New or substantially revised plans and studies state their status (`draft`, `active`, `completed` or
  `superseded`), scope/branch where relevant, and last reviewed date near the top. Superseded documents link to their replacement.
- **Keep history without presenting it as instructions.** Preserve milestone and decision IDs, section anchors used by briefs,
  evidence and dates. Add a historical-status note when archiving. Do not restart ADR numbering or silently rewrite old decisions.
- **Use consistent new names.** New prose filenames use lowercase kebab-case; keep `README.md` for indexes and the stable filenames
  listed above. New handovers use the UTC convention in [handover/README.md](../handovers/README.md); QA retains its existing convention.
  Avoid renaming every established document just for casing.
- **Keep source and derived material connected.** A Word source stays intact; its Markdown brief or review says what it represents.
  Keep voice-trial audio beside its manifest and instructions. Leave generated run output and gate logs at their existing locations.
- **Keep agent definitions focused.** The orchestrator holds durable workflow rules and links to handovers; it does not accumulate
  another copy of project history at the start of every session.

## Migration sequence

1. **Navigation and future handovers (implemented in this change).** Add the docs index, handover convention/template and the
   orchestrator's write/read rules. Existing documents stay put. Link the docs index from the root README.
2. **Consolidate existing handovers and shared engineering docs.** Move the two root handovers into `handover/legacy/`; extract the
   embedded snapshots after separating their standing rules. Move the three engineering documents. Update `CLAUDE.md`, both agents,
   plans, briefs and literal command paths as well as Markdown links. Replace the orchestrator's historical blocks with links only
   after the context they carry is preserved. Review the resulting startup instructions for stale branch assumptions.
3. **Group game/NLP documents by purpose.** Apply the move map a small group at a time. Establish plan status from milestone tables
   and decisions before archiving. Keep the package README path and voice-trial folder intact. Do not change code or gameplay as part
   of these moves.
4. **Shorten READMEs and refresh current guidance.** Extract guides without duplicating their contents. Correct outdated engine,
   project-layout, packaging and CI statements against the current configuration and decision registers. Keep one quick start per
   domain. Add indexes to populated plans/guides/history folders when they help navigation.
5. **Trim the implementation plans last.** After links and guide structure settle, move completed findings into domain history files
   by series or period. Keep milestone IDs/status rows and links in the live plans. Preserve or provide forwarding sections for
   referenced anchors; update prompts and briefs that depend on section numbers. Do not truncate the findings logs blindly.

Each migration change should be reviewable on a feature branch and include moves plus reference repairs together. A file being old
does not prove its plan is complete; a file being long does not justify removing its evidence.

## Verification and completion

For this documentation-only change, run the existing `MarkdownLinkTests` and `git diff --check`. For later moves, also search for
every old path across the repository, including hidden `.claude` files, scripts, project metadata and command examples. Distinguish
intentional historical mentions from operational paths that must work. The link test checks target existence, not section anchors or
paths inside code spans; review those separately.

The focused check, from the repository root with the local SDK on `PATH`, is:

```bash
scripts/test.sh project Vocaterra.Nlp.Tests --no-build --filter FullyQualifiedName~MarkdownLinkTests
git diff --check
```

Use `--no-build` only when the test assembly already exists and matches the test sources; otherwise let the wrapper build it. Follow
the usual resource limits. No application rebuild or full gameplay suite is needed for prose and path moves alone; if a later change
edits MSBuild/package inputs or scripts, run the checks appropriate to those edits as well.

The cleanup is complete when readers can find setup, current plans, decisions, reference material and session state through the docs
index; new handovers land in the timestamped folder; historical plans identify their status; and all live links, anchors and literal
paths touched by the migration resolve. The larger migration remains proposed until carried out.
