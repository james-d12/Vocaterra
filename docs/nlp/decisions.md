# Open decisions for the owner

A register of every decision only the owner can make, with the context, the options, the
orchestrator's or the research report's recommendation, and what is waiting on it. Resolve a line by
editing its status; the plan (`implementation-plan.md`) and ADRs follow.

| # | Decision | Status |
| --- | --- | --- |
| D1 | Workflow token scope (activate CI) | reopened (owner, 2026-10-23): CI planned with SonarCloud and a required LanguageTool step; waits for the owner to widen the token (workflow scope); see the orchestrator's handover |
| D2 | espeak-ng GPL code inside sherpa-onnx (ADR 0007) | decided (owner, 2026-10-21): accepted for private development builds; the GPL-free sherpa-onnx pin is a blocker on any distribution |
| D3 | A LICENSE file for the repository | decided (owner, 2026-10-21): Apache-2.0; repository stays private for now (M19e); since 2026-10-23 the whole repository, game included (game D-game-20, R01) |
| D4 | OpenSubtitles terms behind the frequency list | deferred (owner, 2026-10-21): keep the entry, do not raise it until a public release is planned |
| D5 | Package feed for the `Vocaterra.Nlp.*` packages | decided (owner, 2026-10-21): no hosted feed; superseded the same day for the game, which now lives in this repository (`game/`) and references the core's projects (game D-game-7) |
| D6 | Conversation state-machine engine: core or game | decided (M19a): core, as a generic conversation agent |
| D7 | Blacksmith sample content stays as the core's demo | superseded (owner, 2026-10-23, R02): the sample is deleted; the core's tests, fuzz suite and demo run on the game's bundle `content/game-v1` |
| D8 | Online or offline | open |
| D9 | Determinism strictness (ADR 0010) | open |
| D10 | Licence stance on MPL-2.0 and on GPL/LGPL dev-time processes | partly decided (owner, 2026-10-22): GPL/LGPL tools allowed as separate, local, development-time processes (LanguageTool, M30); MPL-2.0 still open |
| D11 | Cloud cost cap for an optional tutor | open |
| D12 | Game engine | moved (owner, 2026-10-21): a Vocaterra repository decision; not tracked here |
| D13 | Pronunciation ambition | open, after the speech check |
| D14 | German and a multi-language core | decided (owner, 2026-10-22): record the roadmap only (run nothing yet); German in the core only; Piper thorsten for private German speech; register per agent, Sie for strangers; amended (owner, 2026-10-26): one neutral engine plus language-pack projects, Phase A (M31 to M35) unblocked with M52 and M53 |

## D1. Workflow token scope
**Status (2026-10-21):** deferred by the owner. CI is not a requirement at the moment; the workflow stays parked and the orchestrator does not raise this item until the owner reopens it.
The GitHub token lacks the `workflow` scope, so nothing under `.github/workflows/` can be pushed.
The full CI (build and test on three platforms, golden, license scan, model-backed tests, fuzz, weekly
fuzz soak) is parked in `../.github/workflows-pending/` (repository root). **To resolve:** `gh auth refresh -h github.com
-s workflow`, then, from the repository root, `git mv .github/workflows-pending/ci.yml .github/workflows/ci.yml` and push. The
same scope is needed on `Vocaterra` if it gets CI. Since M19a the workflow reads the repository variable
`VOCATERRA_RUN_MODEL_TESTS` (rename it if `SLG_RUN_MODEL_TESTS` was ever set). **Waiting on it:** CI for both repositories, and
package publishing (D5).

## D2. espeak-ng GPL code inside sherpa-onnx
**Decided (2026-10-21, owner):** option (c) now, option (a) before release. The current sherpa-onnx packages are accepted for private, non-distributed development builds (GPL obligations arise on distribution only). Before any build, package or the content tool is distributed, pin the first sherpa-onnx release that contains the GPL-free build (upstream PR #4055) and feed Kokoro phones from the in-house G2P, as ADR 0007 describes; that pin is scheduled with M24 and is a hard blocker on distribution. Unaffected adapters: Vosk, the wav2vec2 phoneme model and the e5 embedding model, which do not link sherpa-onnx.
The Apache-2.0 `org.k2fsa.sherpa.onnx` native library statically contains espeak-ng (GPL-3.0), so any
build linking it (ASR or TTS) carries GPL code, contrary to the design doc's licensing table (ADR 0007).
**Path found by the research report:** sherpa-onnx upstream PR #4055 (merged 2026-10-20) makes a
GPL-free build; pin the first release after it and feed Kokoro phones from the in-house G2P. **Options:**
(a) pin that release when it ships (recommended); (b) run Parakeet through ONNX Runtime directly and
isolate TTS in a separate GPL process; (c) accept GPL for a personal, non-distributed build.
**Waiting on it:** TTS milestone (M24's TTS part), any distribution of the game.

## D3. LICENSE file
**Note (2026-10-23):** the game adopted the same licence (game D-game-20); `LICENSE` and `NOTICE` are at the repository root since R01.
**Decided (2026-10-21, owner):** Apache-2.0 for the code and hand-authored content; the Wiktionary-derived lexicon stays CC BY-SA 4.0. The repository stays private for now. Implemented in M19e (`LICENSE`, `NOTICE`, `PackageLicenseExpression`).
The repository has no LICENSE. The hand-authored content and code are the owner's; the generated
lexicon is CC BY-SA (Wiktionary) and must stay so. **Options:** MIT or Apache-2.0 for the code with a
separate CC BY-SA notice for `content/v1/lexicon/wiktionary-es.json` (recommended: Apache-2.0, for
its patent clause, matching most dependencies); or keep the repository private with no licence.
**Waiting on it:** publishing packages (M19b), making either repository public.

## D4. OpenSubtitles terms
**Status (2026-10-21):** deferred by the owner; keep the entry, do not raise it until a public release is planned.
The frequency list (`hermitdave/FrequencyWords`, `es_50k`) is CC BY-SA 4.0 for the counts, but the
terms of the underlying OpenSubtitles corpus were not confirmed. Only a ranking derived from the
counts is used; no counts or subtitle text ship. **Recommendation:** accept as is for a personal
project; before any public release, either confirm the terms or swap the ranking source for a Leipzig
Corpora or wordfreq-based list and regenerate (`docs/nlp/lexicon.md`). **Waiting on it:** public release only.

## D5. Package feed
**Decided (2026-10-21, owner):** no hosted feed. At first the Vocaterra game repository included this repository as a git submodule and consumed the local feed that `scripts/pack.sh` builds (M19e, `docs/nlp/PACKAGING.md`). Later the same day the two repositories became one (game decision D-game-7): the game is `game/`, this folder is `nlp/`, and the game references the core's projects directly. The feed remains for `samples/HostConsole` and for a consumer outside the repository; still no hosted feed.
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
**Superseded (owner, 2026-10-23; done in R02):** the owner judged the sample old and wants the core to break when game content breaks it. `content/v1` is deleted; the core runs on `content/game-v1`, with in-memory fixtures for what the game does not use (effects, quantity slots, the design document's worked example). The text below is unchanged.
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

**Owner, 2026-10-22 (the dev-time half).** After the LanguageTool research (`docs/research/2026-10-22-languagetool.md`), the owner accepted
LanguageTool as a separate, local, development-time grammar check called over HTTP: no LanguageTool code is linked, nothing is shipped,
and no content is sent to the public API. The check covers the Spanish content of both bundles, with plain `es` as the stand-in for
neutral Latin American Spanish. It is advisory in the merge gate until one round of waivers is done, then required. The MPL-2.0 half
(UDPipe for M20) stays open.

## D11. Cloud cost cap
If the tutor uses a hosted model, set a monthly cap. The report estimates about $0.30 per 1 000
utterances on Haiku-class models; an hour of play is a few hundred utterances. **Recommendation:** a
hard cap in the tutor adapter with a local fallback. **Waiting on it:** M25.

## D12. Game engine
**Moved (2026-10-21, owner):** the game's planning, including the engine, lives only in the Vocaterra repository. This repository documents the language core and does not track the decision.
Not chosen. It affects audio capture, threading, and how the core is consumed (NuGet works for
Godot .NET and MonoGame; Unity needs netstandard-compatible builds or source). **Recommendation from
the report:** Godot .NET. **Waiting on it:** M19c's host project shape, M24's capture path.

## D13. Pronunciation ambition
How far to take pronunciation scoring (four `pron.*` tags, GOP-style scoring, a new phoneme model).
**Recommendation:** decide after M24's speech reality check shows which recognizer preserves learner
errors; until then the phoneme stage stays optional. **Waiting on it:** M24 results.


## D14. German and a multi-language core

**Owner, 2026-10-22.** "See what it will take to make it also support German, and make the architecture for it support multi lang."
The research (`docs/research/2026-10-22-multilingual.md`) found about 23 % of the core Spanish-specific (about 6,400 lines). It proposed
the target language as a property of the content bundle (`manifest.json` `language`, absent meaning `es`), resolved through a language-pack
registry, with Spanish kept byte-identical behind an identity guard. It also proposed a staged roadmap, M31 to M48. The owner chose:
- **record only:** the roadmap is planned (`implementation-plan.md` §11) and nothing runs until the owner says so;
- **German in the core only:** the game stays Spanish; one target language per bundle;
- **German speech output:** Piper's `thorsten` voice (CC0 voice data) through sherpa-onnx for private builds, under D2's terms (the
  espeak-ng GPL issue blocks distribution). Distributed builds use pre-rendered lines. Revisit at the GPL-free pin;
- **register:** each agent declares du or Sie, with Sie for shopkeepers and strangers, and an opt-in register check (M48).
Still open: the variety (the research suggests de-DE, accepting Austrian and Swiss forms when grading); the tagger route (hand-written checks
first, M47 only if M39 or M44 fail their false-positive gates; spaCy de, MIT, and Stanza de, Apache-2.0, avoid the MPL-2.0 question); German
content trust (no reviewer; the Goethe A1 list has no licence, so it can only be a reference).

**Amended, owner, 2026-10-26.** "Move all the `src/Vocaterra.Nlp.*` projects into one Nlp project", and "i want the NLP to become
language agnostic kind of, so we have the current spanish implementation on top of the core shared NLP code". The owner chose
(`docs/research/2026-10-26-engine-and-language-packs.md`):
- **target shape:** one language-neutral engine `Vocaterra.Nlp` (today's 11 libraries, merged in M52) plus one project per language,
  `Vocaterra.Nlp.Es` first (M53), then `Vocaterra.Nlp.De`. This supersedes research §4.7's advice to extract Spanish "only if a third
  language arrives";
- **Phase A unblocked:** M31 to M35 run, in the order M31, M52, M32 to M35, M53. M36 onward (German) stays recorded only.

## Already decided (for the record)
- 2026-10-20: core repository renamed `Vocaterra.NLP`; game repository `Vocaterra` created; M19a
  renames `Vocaterra.Nlp.*` to `Vocaterra.Nlp.*`; Haiku 5.5 is the default model for delegated work; the
  research report's M18 stays as the seam the tagger plugs into.
- 2026-10-21: D1 and D4 deferred; D3 Apache-2.0; D5 git submodule; D12 moved to the Vocaterra repository; the core repository documents only the language core (M19e isolation audit).
- 2026-10-21 (later): the core and the game merged into one repository (game D-game-7); this folder is `nlp/`. D5's submodule route is superseded for the game; the ids here are unchanged.
- 2026-10-21: D2 accepted for private builds; the GPL-free sherpa-onnx pin blocks distribution (scheduled with M24).
- 2026-11-05: an intent needs at least one of `slotGrammars`, `keywordRules` or `examples`; the loader refuses one with none (the owner, on M81's finding; built by M82).
- 2026-11-05: `hace dos días compré pan` ("ago" with a past action) stays `state_action` with `item=pan`, as M74 left it: a correct statement keeps its reading and gets the NPC's reply to a statement (the owner, on M74's review point).
