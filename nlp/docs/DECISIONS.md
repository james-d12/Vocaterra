# Open decisions for the owner

A register of every decision only the owner can make, with the context, the options, the
orchestrator's or the research report's recommendation, and what is waiting on it. Resolve a line by
editing its status; the plan (`IMPLEMENTATION_PLAN.md`) and ADRs follow.

| # | Decision | Status |
| --- | --- | --- |
| D1 | Workflow token scope (activate CI) | open |
| D2 | espeak-ng GPL code inside sherpa-onnx (ADR 0007) | open, path found |
| D3 | A LICENSE file for the repository | open |
| D4 | OpenSubtitles terms behind the frequency list | open |
| D5 | Package feed for the `Vocaterra.Nlp.*` packages | open |
| D6 | Conversation state-machine engine: core or game | decided (M19a): core, as a generic conversation agent |
| D7 | Blacksmith sample content stays as the core's demo | decided (M19a): kept as the sample and demo |
| D8 | Online or offline | open |
| D9 | Determinism strictness (ADR 0010) | open |
| D10 | Licence stance on MPL-2.0 and on GPL/LGPL dev-time processes | open |
| D11 | Cloud cost cap for an optional tutor | open |
| D12 | Game engine | open |
| D13 | Pronunciation ambition | open, after the speech check |

## D1. Workflow token scope
The GitHub token lacks the `workflow` scope, so nothing under `.github/workflows/` can be pushed.
The full CI (build and test on three platforms, golden, license scan, model-backed tests, fuzz, weekly
fuzz soak) is parked in `.github/workflows-pending/`. **To resolve:** `gh auth refresh -h github.com
-s workflow`, then `git mv .github/workflows-pending/ci.yml .github/workflows/ci.yml` and push. The
same scope is needed on `Vocaterra` if it gets CI. Since M19a the workflow reads the repository variable
`VOCATERRA_RUN_MODEL_TESTS` (rename it if `SLG_RUN_MODEL_TESTS` was ever set). **Waiting on it:** CI for both repositories, and
package publishing (D5).

## D2. espeak-ng GPL code inside sherpa-onnx
The Apache-2.0 `org.k2fsa.sherpa.onnx` native library statically contains espeak-ng (GPL-3.0), so any
build linking it (ASR or TTS) carries GPL code, contrary to the design doc's licensing table (ADR 0007).
**Path found by the research report:** sherpa-onnx upstream PR #4055 (merged 2026-10-20) makes a
GPL-free build; pin the first release after it and feed Kokoro phones from the in-house G2P. **Options:**
(a) pin that release when it ships (recommended); (b) run Parakeet through ONNX Runtime directly and
isolate TTS in a separate GPL process; (c) accept GPL for a personal, non-distributed build.
**Waiting on it:** TTS milestone (M24's TTS part), any distribution of the game.

## D3. LICENSE file
The repository has no LICENSE. The hand-authored content and code are the owner's; the generated
lexicon is CC BY-SA (Wiktionary) and must stay so. **Options:** MIT or Apache-2.0 for the code with a
separate CC BY-SA notice for `content/v1/lexicon/wiktionary-es.json` (recommended: Apache-2.0, for
its patent clause, matching most dependencies); or keep the repository private with no licence.
**Waiting on it:** publishing packages (M19b), making either repository public.

## D4. OpenSubtitles terms
The frequency list (`hermitdave/FrequencyWords`, `es_50k`) is CC BY-SA 4.0 for the counts, but the
terms of the underlying OpenSubtitles corpus were not confirmed. Only a ranking derived from the
counts is used; no counts or subtitle text ship. **Recommendation:** accept as is for a personal
project; before any public release, either confirm the terms or swap the ranking source for a Leipzig
Corpora or wordfreq-based list and regenerate (`docs/LEXICON.md`). **Waiting on it:** public release only.

## D5. Package feed
`Vocaterra.Nlp.*` packages (M19b) need a feed. **Options:** GitHub Packages (token also needs
`write:packages`; private, fits two private repositories; recommended), a local folder feed committed
as release artifacts (simplest, no token change), or nuget.org (public only). **Waiting on it:** M19b's
publish step; M19c consumes from whichever feed is chosen.

## D6. Conversation state-machine engine
The verdict-driven state machine (`Vocaterra.Nlp.Decision/ConversationAgents`, renamed from `Agent` in M19a) is
content-driven interaction logic. **Recommendation:** keep the engine in the core, move the definitions
(blacksmith, innkeeper, captain) to the game bundle. **Alternative:** move the engine to the game and
have the core stop at the verdict and feedback plan. **Decided (M19a, owner):** the engine stays in the core; see ADR 0009.

## D7. Sample content
The blacksmith bundle is exercised by the goldens, docs and fuzz suite. **Recommendation:** keep it
as the core's sample and demo; the game repository holds the real bundle. **Alternative:** replace it
with a neutral bundle (market stall) in M19c. **Decided (M19a, owner):** the sample stays; the game repository holds the real bundle.

## D8. Online or offline
From the research report. **Recommendation:** an offline-capable core (closed prompts, tagger,
checks, templates all local) with an online tutor as an optional layer whose output is recorded in
the replay envelope and validated deterministically. **Waiting on it:** M25 (tutor); shapes M23.

## D9. Determinism strictness
The design doc forbids non-deterministic components in the core and expects bit-identical goldens
across platforms. **Recommendation:** relax to "replay-exact": seeded RNG, stored time and hashing
stay, model outputs are recorded and replayed, cross-platform goldens compare recorded outputs rather
than recomputed inference. Write it as ADR 0010. **Waiting on it:** M20 onward (every model-backed
stage), the tutor design.

## D10. Licence stance on MPL-2.0 and dev-time GPL/LGPL
The allowlist refuses all copyleft for code dependencies. The best tagger route (UDPipe 1 with its
C# binding) is MPL-2.0; LanguageTool (LGPL) is useful only as a separate development-time oracle.
**Recommendation:** accept MPL-2.0 for unmodified libraries (add it to `scripts/license-allowlist.txt`
with a note), allow GPL/LGPL tools only as separate processes used at development time, never linked
or shipped. **Waiting on it:** M20 (tagger choice), M22 (evaluation oracles).

## D11. Cloud cost cap
If the tutor uses a hosted model, set a monthly cap. The report estimates about $0.30 per 1 000
utterances on Haiku-class models; an hour of play is a few hundred utterances. **Recommendation:** a
hard cap in the tutor adapter with a local fallback. **Waiting on it:** M25.

## D12. Game engine
Not chosen. It affects audio capture, threading, and how the core is consumed (NuGet works for
Godot .NET and MonoGame; Unity needs netstandard-compatible builds or source). **Recommendation from
the report:** Godot .NET. **Waiting on it:** M19c's host project shape, M24's capture path.

## D13. Pronunciation ambition
How far to take pronunciation scoring (four `pron.*` tags, GOP-style scoring, a new phoneme model).
**Recommendation:** decide after M24's speech reality check shows which recognizer preserves learner
errors; until then the phoneme stage stays optional. **Waiting on it:** M24 results.

## Already decided (for the record)
- 2026-10-20: core repository renamed `Vocaterra.NLP`; game repository `Vocaterra` created; M19a
  renames `Vocaterra.Nlp.*` to `Vocaterra.Nlp.*`; Haiku 5.5 is the default model for delegated work; the
  research report's M18 stays as the seam the tagger plugs into.
