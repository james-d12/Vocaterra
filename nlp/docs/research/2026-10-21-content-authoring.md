# Cheaper content authoring: intents and bundles

Research report, 2026-10-21. Question from the owner: writing the intents and the content folder is expensive, so how can authoring be
made cheaper, and what should the core build? `IMPLEMENTATION_PLAN.md` §10 lists six options; this report tests them as a hypothesis.
It does not repeat `docs/research/SCALING_APPROACHES.md`, which already argues for build-time LLM drafting (its §3.3, M21) and a trained
open-prompt classifier (M23).

Method: I read the sample bundle (`content/v1`), the game bundle (`~/Dev/Vocaterra/content/game-v1` at game `b968f2f`), the loaders,
validator, intent resolver and slot-grammar matcher, the plan's findings log, both repositories' git history, and the game's plan and
decisions. Counts come from two throwaway Python scripts run with `python3 -I` over the JSON. They are kept outside the repository and
described in §5. Web sources are cited inline by number (§8). Claims I could not confirm on a primary page are marked *unverified*.
No `dotnet` was run.

## Summary

- **Most of the game's bundle is copied.** Apart from imported data, the game's bundle is 1,606 hand-maintained lines. About 357 of them
  are the game's own work: the guide, 4 intents, 18 dialogue pools, 1 answer set, 2 prompts and the noun catalogue. The other ~1,250 lines
  are copies of the sample. Fifteen of the game's 27 files are byte-identical to the sample, and so are all 11 sample intents. §10's
  phrase "edited copies" is wrong: the game changed only 5 dialogue lines (to pass its A1 check) and `ATTRIBUTION.md`. About 680 of the
  copied lines (the blacksmith, trade and spell content) are used only by the game's console-host smoke tests; the game's own code uses
  just the guide's two prompts. Removing those lines is a deletion for the game, not a layering feature for the core.
- **One new intent costs 70–100 lines across six files.** M17b-core added 5 intents in 353 lines; game P3 added 3 intents and an agent in
  about 300. The intent entry itself is only about a quarter of that. The rest is agent transitions, dialogue pools, prompts and answer
  sets, and much of it is mechanical:
  - 107 of 107 dialogue `lineId`s are `<pool>.<n>`.
  - 58 of 58 agent actions repeat the pool name in `dialogueIntent` and `variantPoolId`.
  - The guide's 24 transitions contain 12 distinct (trigger, action, target) triples.
  - 31 of 57 answer aliases are just an article plus the canonical word.
  - Every keyword rule's `requiresSlots` equals its intent's `requiredSlots`.
- **Mistakes cluster in behaviour, not syntax.** The validator checks only references. The costly mistakes were grammars that are too
  narrow (M17c), too broad (`gracias` resolved to `cast_spell`, M17b-core), or blind to a homograph (M19f, CR-013), plus CEFR rewording
  (G2). Nothing checks that an intent's examples resolve to that intent, or that another intent's grammar does not also take them.
- **Suggesting grammars from examples is weak (prototype, §5).** Suggestions drawn from each intent's 5–6 examples cover those examples by
  construction with no cross-intent confusion, so the success measure in §10.3 is met trivially. Held out one at a time, they generalise
  to only 23 of 79 examples (28 with a noun-phrase heuristic). At least 12 of 59 suggestions carry a wrong part of speech: `tardes` is read
  as `tardar`, `acuerdo` as `acordar`, `nombre` as `nombrar`, and the names `Rosa`, `Sol`, `Luz` and `Mar` as nouns. Run as a coverage and
  confusion check, the same code found 4 real cross-intent matches in the game's hand grammars. One of them is the class of bug M17b-core
  fixed in the core.
- **Prior art.** The closest system is Home Assistant's `hassil` (Apache-2.0) with `home-assistant/intents` (CC BY 4.0). Its Spanish set
  has 38 intents, 205 sentence files and about 1,800 test sentences, all for smart-home control, so none of it maps to game intents. What
  is worth taking is the method:
  - shared per-language expansion rules and lists;
  - a test file per intent that the build checks against the templates;
  - user sentences that extend the built-in ones without forking them.

  Its Spanish rules spell verb inflections out by hand (`abr(a|e|ir|í)`), which the core's lexicon-backed `[VERB:lemma]` already avoids.
- **Recommendation, in order:**
  1. **M27a′, `vocaterra-content lint`** (S/M): coverage, confusion and reachability, with examples that can also act as tests.
  2. **M27b′, layering with `std-v1`** (M): redesigned as described in §7.
  3. **M27c, format de-duplication** (S): derived ids and defaults, any-state transitions, article aliases generated from the lexicon,
     and hassil-style macros in the grammar DSL.
  4. **M21, LLM drafting**, gated by the lint.

  Pattern suggestion survives only as a hint inside the lint. Character templates and embedding-only intents wait.

## 1. What an author writes today (cost model, part A)

### 1.1 Per content kind

Lines are measured in each repository; "fields" counts what an author types per unit. The `cefr` lists, `seed.json` and the Wiktionary
import (41,593 lines) are excluded from "hand lines".

| Kind (file) | Sample lines | Game lines | Unit and fields per unit | Mechanical | Judgement |
| --- | --- | --- | --- | --- | --- |
| Intent (`intents/intents.json`) | 215 (11 intents) | 292 (15) | intent: id, requiredSlots, ~2.9 grammars, ~1.2 rules, ~5.3 examples | rule `requiresSlots` (always = requiredSlots), rule lemma (usually the grammar's head verb), noun-phrase tails `[ART]? [NUM:qty]? [NOUN:x]` (17 of 44 grammars) | which phrasings are allowed, what is optional, which literal words, the learner-error example |
| Answer set (`answer-sets/*.json`) | 180 (7 sets, 19 answers) | 215 (8, 24) | answer: answerId, canonical, aliases, dialects | article aliases (31 of 57), `dialects` (the same 3 codes on all 24) | canonical word, real synonyms (`roca`, `pócima`) |
| Prompt (`prompts/prompts.json`) | 207 (18) | 227 (20) | 8 fields | agentId, answerSetId, mode, difficulty; `slotGrammarIds` repeat grammar text verbatim (21 copies) | which intents are allowed, `text` |
| Dialogue pool (`dialogue/dialogue.json`) | 180 (23 pools, 61 lines) | 316 (41, 107) | variant: lineId, text | lineId (107 of 107 are `<pool>.<n>`) | the Spanish line at the right CEFR level |
| Agent (`agents/*.json`) | 190 (1 agent, 27 transitions) | 255 (2, 51) | transition: on {intent, communicated}, to, actions {dialogueIntent, hostEvent, variantPoolId} | `variantPoolId` (= dialogueIntent in 58 of 58), the `variantPools` list (its counts are the dialogue's), the same transition repeated per state (guide 24 → 12 distinct) | states, the host event names |
| Feedback policy | 9 | 9 (identical) | 4 rules | all, once | none per game so far |
| Evaluation config | 68 | 68 (identical) | thresholds | all, once | none per game so far |
| Effects | 35 | 35 (identical) | effect: id, intent, kind, curve | | the curve |
| Hand lexicons (`game.json`, `supplement.json`, `world.json`) | 70 | 79 | entry: form, lemma, pos, features | inflected forms (could be generated) | that a word is missing |
| Provenance, attribution, manifest | 105 | 128 | | manifest (tool-written) | attribution wording |

Totals: the sample has about 1,234 hand lines and the game about 1,606. §10 estimated ~1,100 and ~1,500.

### 1.2 Per intent, end to end

| Change | Intents | Content lines added | Per intent |
| --- | --- | --- | --- |
| M17b-core `14f9963` (confirm, deny, farewell, thank, introduce_self) | 5 | intents 90, agent 78, answer sets 81, prompts 52, dialogue 48, provenance 4 = 353 | ~71 |
| Game P3 `ed5133c` (ask_directions, ask_what, ask_help, plus the guide agent) | 3 | intents 61, agent 61, dialogue 99, answer set 35, prompts 10, lexicon 11, cefr 5, attribution and provenance 16 ≈ 298 | ~100 |
| Game P4 `07137ed` (name_object) | 1 | intents 16, dialogue 29, agent 4, nouns 11, cefr 3 ≈ 63 | ~63 |

The intent entry is only 20–30 % of the work. Every edit then runs the procedure in `CONTRIBUTING.md`:
1. validate;
2. update `provenance.json`;
3. hash, which rewrites the manifest;
4. validate again;
5. run `cefr-check`;
6. re-pin `GoldenBundleHash`;
7. re-record the end-to-end golden.

Agent sandboxes refuse the `hash` subcommand (§5b, "Sandbox keyword refusals"), so the orchestrator runs it by hand.

### 1.3 Duplication between `content/v1` and `game-v1`

Diffed file by file and intent by intent (`<` is the sample, `>` is the game):

| File | Status | What the game changed |
| --- | --- | --- |
| `answer-sets/` (7 files), `agents/blacksmith.json`, `effects/`, `evaluation/`, `feedback/`, `lexicon/game.json`, `lexicon/seed.json`, `lexicon/wiktionary-es*.json`, `cefr/a2.txt` | byte-identical (15 files) | nothing |
| `intents/intents.json` | 0 lines removed, 77 added | the 11 sample intents are identical; 4 game intents appended |
| `prompts/prompts.json` | 0 removed, 20 added | 18 sample prompts identical; 2 guide prompts |
| `dialogue/dialogue.json` | 5 changed, 136 added | `confirm_prompt.1–3` (drops `monedas`), `introduce_reply.1` (`herrería` → `tienda`), `thank_reply.1` (`De nada` → `Con gusto`), all to pass the game's A1 check; 18 guide pools added |
| `lexicon/supplement.json` | 1 added | `esta` as a form of `estar` (the CR-013 workaround) |
| `cefr/a1.txt` | 9 added | `bloque`, `flecha`, `guía`, `piedra`, `árbol`, `algo`, `casi`, `eso`, `esto` |
| `ATTRIBUTION.md`, `provenance.json`, `manifest.json` | rewritten | bundle id, the game's wording (CR-011) |
| `agents/guide.json`, `answer-sets/places-basic.json`, `game/nouns.json`, `lexicon/world.json` | new | |

The ~1,250 copied lines fall into three groups:
- **Generic material, about 445 lines.** The 6 generic intents (~92 lines), 5 generic answer sets (116), feedback and evaluation (77),
  16 of the 23 sample pools (41 of 61 variants, ~115 lines) and the generic prompts (~45). This is what a standard library would hold.
- **Blacksmith material, about 680 lines.** Five trade and spell intents, the blacksmith agent, items and spells answer sets, effects,
  13 prompts, 7 pools and `lexicon/game.json`. Only `tests/Vocaterra.Host.Tests` uses it; `src/Vocaterra.Game` refers only to `guide-open`
  and `guide-review`.
- **Imported data, 41,600 lines.** The lexicon and CEFR lists, copied as data.

The game owner has already decided to diverge (game D-game-4: "the game owns all of `content/game-v1`: no re-sync on core releases").
G2 measured three costs of tracking the sample:
- the sample's templates failed the game's A1 check;
- `ATTRIBUTION.md` had to be rewritten;
- the bundle id had to be reset.

A layering design must remove exactly these three costs, or the game has no reason to adopt it.

### 1.4 The mistakes the history shows

| Mistake class | Evidence | Caught today by | Would be caught by |
| --- | --- | --- | --- |
| Grammar too narrow (an ordinary sentence not understood) | `ayer tengo dos espadas` not understood: no `tener` rule, no leading adverb (CLI review → M17c, `b234506`); spells need three extra grammars (M13b, `34f6927`) | a human writing README examples | coverage of held-out or test sentences |
| Grammar too broad (it takes another intent's sentence) | `gracias` resolved to `cast_spell` through `[VERB:lanzar]? [ART]? [NOUN:spell] [ADJ]?`; fixed by a core tie-break, not in content (M17b-core) | the fuzz suite, by chance | a cross-intent confusion check (§5 still finds `gracias` and `ayuda` matched by `cast_spell`) |
| Grammar construct with a hidden reading problem | an optional `[WORD:item]?` absorbs `ayer` (M19f); an alternation plus `[WORD]` swallows a mis-conjugated verb (M26b follow-up); `[VERB:estar]` does not bind `esta` (CR-013, game supplement entry) | fuzz oracles O5 and O3 | lint warnings on optional `[WORD]` next to closed-class words; examples with expected tags |
| Lexicon gap for a word the content uses | `cuál` missing (M16b); `bloque`, `esta` (game P3) | `LexiconCoverageTests` (core only) | the same check in `validate`, so the game gets it |
| Template above the learner's level | 3 sample templates failed the game's A1 check (G2); 27 A1 and 15 A2 lemmas unlisted; prompt text not checked (CR-010) | `cefr-check`, advisory, never fails | a failing mode and a per-layer level |
| Dialogue semantics | recast pool praises after a correction (`Ah, el árbol. ¡Perfecto!`, game P4) | a playtest | out of scope here |
| Copied text that names the wrong owner | sample `ATTRIBUTION.md` (CR-011) | a human | per-layer notices |

## 2. Prior art (part B)

### 2.1 Template-sentence intent systems

**Home Assistant: `hassil` and `home-assistant/intents`.**
- **Licences.** `hassil` is the parser, Apache-2.0 [1]. `home-assistant/intents` is the data, CC BY 4.0 per its `LICENSE.md`; I read the
  licence in a shallow clone at commit `e77dd13` of 2026-10-21 [2].
- **Layout.** Per language there are `sentences/<lang>/<Intent>/*.yaml`, `rules/<lang>/*.yaml` (shared `expansion_rules`),
  `lists/<lang>/*.yaml` (shared slot lists), `responses/<lang>/` and `tests/<lang>/<Intent>/*.yaml`. The test files hold sentences with
  their expected intent and response, and the repository's script checks them against the templates [2][3].
- **Syntax.** `(a|b)` for alternatives, `[optional]`, `{list:slot}`, `<rule>`, permutations `(a; b)`, ranges `{0..100:x}`, and `skip_words`
  [1][3].
- **User extensions are additive.** A user adds `config/custom_sentences/<lang>/*.yaml` and the sentences extend the built-in intents
  rather than replacing them [4]. I found no documented way to remove a built-in sentence (*unverified*).
- **Spanish (`es`).** 38 intent folders, 205 sentence files, 3,986 lines and 1,818 test-sentence lines, all smart-home: lights, timers,
  media, shopping lists, `HassNevermind` ("da igual", "no importa"). The rules spell verb forms out by hand, for example
  `abre: (abr(a|e|ir|í)|<sube>)` and `añadir: (añad(a|e|ir|í)|apunt[a|e|ar|á]|…)`, because hassil has no lexicon.
- **Reuse verdict.** No game intent maps to this data, and the sentences are native-speaker commands with no learner errors. Importing
  any of it would add a CC BY 4.0 notice for little gain. What is worth taking is the method:
  - (a) shared expansion rules and lists per language;
  - (b) a test file per intent that the build checks against the templates;
  - (c) extension without forking.

**Rhasspy 2.** `sentences.ini` uses `(a | b)`, `[optional]`, `$slot` lists from files, `<rule>`, cross-intent rules `<Intent.rule>`, and
tags `{entity}` and `{entity:value}` [5]. Its successor work is hassil (*unverified* lineage).

**Snips NLU and Chatito.**
- Snips NLU (Apache-2.0) [6] trains on a JSON dataset of annotated utterances plus built-in entities. That it has been dormant since about
  2020 is *unverified*.
- Chatito (MIT) [7] generates training sets from a DSL (`%[intent]`, `@[slot]`, `~[alias]`). It samples combinations and does no
  consistency checks.

**Rasa.**
- Rasa Open Source is Apache-2.0 and in maintenance mode; development has moved to CALM in the licensed Rasa Pro [8].
- NLU data is YAML: intents with example lists, inline entities `[text]{"entity": …, "value": …}`, `synonym:`, `regex:` and `lookup:`
  blocks, split across any number of files [9].
- `rasa data validate` checks the domain, the NLU data, flows and stories, and flags intents that share identical examples [10]. This is the
  closest existing equivalent of a content lint.
- CALM replaces example authoring with flow descriptions read by an LLM command generator [8]. That is non-deterministic at run time, so
  it does not fit D0.

**Dialogflow and Lex.**
- Amazon Lex V2 has documented authoring-time LLM drafting [11][12]:
  - "utterance generation" proposes sample utterances from the intent's name, description and existing utterances, 5 per click and up to
    25 per intent;
  - generated utterances are marked in the console;
  - the documentation tells authors to review them, since output can be inaccurate.

  This is the workflow §10 option 6 proposes.
- Dialogflow's system entities and "automated expansion" of entity values (*unverified*, [13]) correspond to the core's lexicon-backed
  `[NOUN:x]`.

**Mycroft and OVOS.** Padatious (Apache-2.0) [14] trains a small neural model from `.intent` sample lines with `(a|b)` and `{entity}`.
Adapt is keyword-based, close to the core's rules (*unverified* details).

### 2.2 Dialogue tools (reply pools and character reuse)

**Ink** (MIT [15]).
- Inline variation: `{a|b|c}` sequence, `{&a|b}` cycle, `{!a|b}` once-only, `{~a|b}` shuffle.
- `INCLUDE` for splitting files [16].
- A pool is a single line of text. There is no id per variant, so ids are implied.

**Yarn Spinner** (MIT [17]).
- "Line groups" are lines prefixed with `=>`, each with an optional condition and `once` [18].
- "Node groups" are nodes that share a title, selected by `when:` headers through a saliency strategy (First, Best, or custom) [19].
- The documentation warns that with the First strategy, "if a node group's nodes are split up across more than one file, the ordering … is
  not defined" [19]. A layering design must fix its merge order explicitly.

**Lesson for this repository.** Neither tool asks authors for per-line ids or a separate list of how many lines a pool has. Both treat
"same reaction in any state" as the default and express exceptions locally. Neither has parameterised character templates as a core
concept.

### 2.3 Few-shot embedding intents and thresholds

- SetFit (Apache-2.0) was evaluated at N = 8 and N = 64 labelled examples per class. At N = 8 it is about level with T-Few 3B (62.3 vs
  63.4 average) [20].
- The sample has about 5 examples per intent, below the smallest setting reported.
- CLINC150 shows that intent classifiers "struggle to identify out-of-scope queries" [21]. Learner speech is largely out of scope or
  malformed, so thresholds must be set on held-out in-scope and out-of-scope sets.
- The core's embedding rung uses fixed defaults (`EmbeddingTop1` 0.80 ± 0.02, `EmbeddingGap` 0.05 ± 0.01), calibrated on nothing.
  `multilingual-e5-small` is still not exported or pinned (`models/manifest.json`, "TODO").
- MASSIVE (CC BY 4.0) has `es-ES` with 11,514 / 2,033 / 2,974 train, dev and test utterances [22]. It is a possible source of generic
  out-of-scope negatives. That its 60 intents include greetings is *unverified*.

### 2.4 LLM-generated intent data

- Amazon Lex's draft-then-review flow is the only vendor workflow I confirmed [11].
- The earlier report already covers the literature: drift into neighbouring intents [Sahu et al., 23] and gains flattening as generated
  data grows.
- The failure that matters here is drift into a neighbouring intent, which is exactly what a confusion check detects. Draft generation
  therefore needs the lint first.

### 2.5 Layering and overrides

- **Minecraft data packs.** A file at the same namespaced path replaces the lower pack's file whole. Tag lists are the exception: they
  append by default unless `"replace": true` [24][25]. A `filter` block in `pack.mcmeta` hides lower-pack files, which works as a removal
  operator. Overlays apply per format version [26].
- **Factorio.** Mods mutate a shared `data.raw` in three ordered stages. Load order is dependencies first, then dependency depth, then the
  mod name [27]. Multiplayer requires matching content checksums (forum source, *unverified* [28]).
- **Bethesda plugins.** The last-loaded record wins whole, so two mods that touch different fields of one record need a hand-made "patch"
  plugin [29]. This is the cost of whole-item replacement when overlays stack.
- **Merge semantics.**
  - JSON Merge Patch (RFC 7386, superseded by RFC 7396) replaces arrays whole and deletes with `null` [30].
  - JSON Patch (RFC 6902) addresses array elements by index, which is fragile [31].
  - The CSS cascade ends in "later wins" after origin and specificity [32].
- **Content-addressed pins.** `go.sum` holds a hash per module version, and a mismatch is a hard security error, never a fallback [33].
  Nix `flake.lock` records `narHash` [34] and npm lockfiles an `integrity` hash per package [35].

**Lesson for this repository.**
- Address items by id, never by index.
- Replace whole items, which is deterministic and needs no array rules.
- Make an override explicit: a silent collision is how the Bethesda and Paradox communities get "conflicts".
- Reference the base by id and hash and fail hard on a mismatch.
- Allow one level of layering, so the stacking problem never arises.

### 2.6 Grammar induction

- Alignment-Based Learning aligns sentence pairs and treats the spans that differ as constituents of the same type [36].
- ADIOS extracts patterns statistically from large corpora [37].
- Log-template miners such as Drain3 merge lines into templates with untyped wildcards [38].
- All three assume many examples. I found no deterministic method for inducing typed slot templates from five sentences. §5 measures why.

## 3. The options (part C)

The resolver ladder, the replay record and determinism are unchanged by every option below except O5.

### O1. Content layering and a standard library (§10 M27a)

- **Build.** The manifest names one base; the loader merges by id; `std-v1` is carved out of the sample; the tool understands layered
  bundles (§7 has the redesign).
- **Reduction on the game.** About 445 hand lines plus 41,600 lines of imported data and the CEFR lists stop being copied. Lexicon fixes
  (`cuál`, CR-013, a future M20 tagger) and `ATTRIBUTION` corrections then reach the game by a submodule bump. The blacksmith's ~680 lines
  are removed only if the game deletes them, which is its own decision.
- **Effort** M. **Tiers:** D0, if merge order and hashing are fixed.
- **Risks:**
  - silent shadowing, mitigated by explicit overrides;
  - `std` drifting above A1, mitigated by making `std` pass `cefr-check --level a1` with no unlisted lemmas;
  - adoption depends on the game's D-game-4.
- **Licence:** each layer carries its own notice; the Wiktionary CC BY-SA notice moves with the lexicon into `std`. **Decisions:** none.

### O2. Pattern suggestion (§10 M27b)

- **Build.** `suggest --intent`, as §10.3 specifies.
- **Reduction.** Small. Typing a grammar is cheap; choosing it is not (§5).
- **Effort** M. **Tiers:** D0.
- **Risks.** It produces grammars that look plausible but are wrong on homographs, and on names that are also nouns (all four names in
  `introduce_self`). Reviewing them costs about as much as writing them, and the success measure is met trivially.
- **Decisions:** none. It improves after M20 (the tagger), which is D9 and D10 territory.
- **Keep** only its by-product: a coverage and confusion report.

### O3. Answer sets and replies from a noun catalogue and the lexicon

- **Build.** An answer-set option `"articles": true` generates `el/la/un/una <canonical>` from the lexicon's gender. An optional
  `"answers": [{"lemma": "árbol"}]` derives `answerId` and `canonical`. Set-level `dialects`.
- **Reduction.** On the game, 31 alias strings and 24 `dialects` lines, which is 215 → about 150 lines. A future crafting catalogue (game
  §4.3 lists more than 25 nouns) scales linearly instead.
- **Effort** S. **Tiers:** D0.
- **Risk.** Wrong-article forms must stay out of the aliases, so that `la árbol` still reaches the agreement check.
- A game noun catalogue (`game/nouns.json`) is host-owned. Generating answer sets *from* it would make the core read a game format, so do
  not; the article option needs only the lexicon.

### O4. Character templates

- **Build.** Parameterised state machines.
- **Evidence against it now.** There are two agents of different shapes (shop, guide). A template needs a third instance to be designed
  well (*my judgement*). Most of the duplication is the same transition repeated per state: the guide has 24 transitions and 12 distinct
  ones. An `anyState` block plus "stay in the current state" removes that duplication without templates.
- **Effort.** Templates M; `anyState` S.
- **Recommendation.** Do `anyState` inside O7.

### O5. Intents from examples via embeddings

- **Build.** Model export and pinning, threshold calibration on held-out in-scope and out-of-scope sentences, and a per-intent margin
  report.
- **Reduction.** It would remove grammars for slot-less intents only. Slot extraction and error tagging still need grammars or rules.
- **Effort** M. **Tiers:** D1.
- **Decisions.** D9 (margins and goldens across platforms); D8 does not apply (local).
- **Blockers.** The model is unpinned, examples are below SetFit's smallest setting, and there is no out-of-scope set.
- **Recommendation.** Later, after the lint has produced labelled data.

### O6. LLM drafting at authoring time (= the earlier report's M21)

- **Build.** `draft` writes an `llm-draft` file of examples, near-miss negatives, error variants with expected tags, and reply variants.
  `approve` fills `approvedBy` and `approvedOn`. The loader already refuses unapproved `llm-draft` files (`ContentValidator`, "must be
  approved by a person").
- **Reduction.** Typing dialogue (316 lines, the largest kind) and examples. Review remains.
- **Effort** M. **Tiers:** none at run time; committed output is D0.
- **Decisions.** Not D8 or D11, which are run-time questions. The owner pays for authoring calls, and the drafting can even be done by the
  orchestrator's own agents with no core code beyond the approve step.
- **Licence.** Generated text is the owner's to licence (check the provider's terms; *unverified*).
- **Risk.** Drift into neighbouring intents and above-level vocabulary; the gates are O7 and `cefr-check` in a failing mode.

### O7 (new). Content lint: coverage, confusion and reachability

- **Build.** `vocaterra-content lint <bundle> [--strict]`, also run by `validate` as warnings. It runs the real analyzer and the
  `IntentResolver`, not an approximation:
  1. **Coverage.** Each example resolves to its own intent, and the report says by which rung (grammar or rule). An example covered only
     by a keyword rule is reported, for example `muchas gracias por todo`.
  2. **Confusion per prompt.** For each prompt, an example of an allowed intent that also matches another allowed intent's grammar.
     Confusion is checked within the prompt's allow-list, because that is what the resolver sees.
  3. **Grammar hazards.** An optional `[WORD]` next to an optional element; a grammar literal or lemma the lexicon lacks; a `[VERB:x]`
     lemma whose forms are also noun forms (homograph warning).
  4. **Reachability.** Pools no agent uses; intents no prompt allows; answer sets no prompt names; agent states with no way in.
  5. **CEFR.** Dialogue, prompt text (which absorbs CR-010) and answer-set canonicals; a `--fail-on-level` switch.
  6. **Optional example expectations**, as in hassil's test files. An example may be written as
     `{"text": "yo es Rosa", "errors": ["conj.person"]}`, and the lint checks the verdict's tags. This makes the per-intent checklist's
     "one learner-error example" a regression test.
- **Reduction.** No lines. It cuts the cost of mistakes: every row of §1.4 except dialogue semantics is caught at authoring time. It is
  also the oracle O2 and O6 need.
- **Effort** S/M. **Tiers:** a D0 tool; with an embedding model it would also report rung 3, but not by default.
- **Risk.** False alarms on deliberately broad grammars, mitigated by a per-intent `"lint": {"allowOverlap": ["thank"]}` waiver.

### O8 (new). Format de-duplication (shorthand that loads to the same objects)

The following become optional and default to the values they always hold today:
- `lineId` (`<pool>.<n>`);
- `variantPoolId` (= `dialogueIntent`);
- the agent's `variantPools` list (derived from the dialogue);
- a keyword rule's `requiresSlots` (= the intent's `requiredSlots`);
- answer `dialects` (a set-level default).

Also included:
- `anyState` transitions, with `to: "$same"`;
- named grammars: `{"id": "buy.want", "pattern": "…"}`, so prompts reference an id rather than a copied pattern string;
- folders `intents/*.json`, `prompts/*.json` and `dialogue/*.json`, as `answer-sets/` and `agents/` already are, so a scene lives in its
  own files;
- hassil-style expansion rules in the grammar DSL, as macros: `<np:item>` = `[ART]? [NUM:qty]? [NOUN:item]` (17 of 44 game grammars),
  `<want>` = `[VERB:querer]`. They are defined in `std` and expanded at parse time.

Existing files stay valid, and the in-memory catalogs are identical, so goldens change only by hash. On the game this removes about 107
lineIds, 58 pool ids, 24 pool declarations, 18 `requiresSlots`, 24 `dialects` arrays and about 12 duplicate transitions, roughly 250 typed
fields; agents drop from 255 to about 150 lines. Effort S/M; D0; no decisions.

### Summary table

| Option | Build | Est. reduction on the game bundle | Effort | Tier | Waits on |
| --- | --- | --- | --- | --- | --- |
| O7 lint | tool + validate warnings | 0 lines; most §1.4 mistakes caught before merge | S/M | D0 | — |
| O1 layering + std | manifest base, loader merge, `std-v1` | ~445 hand lines + all imported data no longer copied | M | D0 | game D-game-4 for adoption |
| O8 de-duplication | optional fields, anyState, macros, folders | ~250 fields; agents −40 % lines | S/M | D0 | — |
| O3 article aliases | answer-set option | ~65 lines; linear for catalogues | S | D0 | — |
| O6 LLM drafting | `draft` / `approve` | typing of examples and dialogue; review stays | M | none at run time | owner's authoring budget |
| O2 suggestion | `suggest` | small (§5) | M | D0 | M20 for quality |
| O4 templates | template format | not measurable with 2 agents | M | D0 | a third agent |
| O5 embeddings | model pin + calibration | grammars for slot-less intents | M | D1 | D9, model export |

### Before and after: `ask_directions` and the guide (game P3)

Today the author writes the following, in six files:
- 23 lines in `intents.json`;
- a prompt;
- the `guide_point` pool with explicit line ids;
- the transition three times in `guide.json` (once per state), each repeating the pool name twice;
- a `variantPools` entry;
- a 35-line answer set with article aliases.

The fragment of `guide.json` today:

```json
{ "on": {"intent": "ask_directions", "communicated": true}, "to": "pointing",
  "actions": [{"dialogueIntent": "guide_point", "hostEvent": "guide.point", "variantPoolId": "guide_point"}] }
```

That line appears in `idle`, `talking` and `pointing`. With O1, O7 and O8, the game's overlay carries only the guide:

```json
// intents/guide.json (overlay on std-v1, which supplies greet, thank, farewell and the <np:…> macro)
{ "intentId": "ask_directions", "requiredSlots": ["place"],
  "slotGrammars": ["dónde [VERB:estar] <np:place>", "dónde [VERB:haber] <np:place>",
                   "[VERB:buscar] <np:place>", "[VERB:querer] [VERB:encontrar] <np:place>"],
  "keywordRules": [{"lemmas": ["dónde"], "slotFromNoun": "place"}, {"lemmas": ["buscar"], "slotFromNoun": "place"}],
  "examples": ["dónde está el árbol", "dónde hay piedra", "busco la hierba", "quiero encontrar tierra",
               {"text": "dónde está la árbol", "errors": ["gender.agree"]}] }

// agents/guide.json
{ "agentId": "guide", "initialState": "idle",
  "anyState": [
    {"on": {"intent": "ask_directions", "communicated": true}, "to": "pointing",
     "actions": [{"dialogueIntent": "guide_point", "hostEvent": "guide.point"}]},
    {"on": {"communicated": false}, "to": "$same", "actions": [{"dialogueIntent": "not_understood"}]} ],
  "states": [{"id": "idle"}, {"id": "talking"}, {"id": "pointing"}] }

// dialogue/guide.json
{ "pools": { "guide_point": { "variants": [{"text": "A ver, miro."}, {"text": "Un minuto, miro."}, {"text": "¡Claro! Miro."}] } } }

// answer-sets/places-basic.json
{ "answerSetId": "places-basic", "articles": true, "dialects": ["es-ES", "es-MX", "es-AR"],
  "answers": [{"answerId": "tree", "canonical": "árbol"}, {"answerId": "stone", "canonical": "piedra", "aliases": ["roca", "la roca"]}] }
```

The author then runs `vocaterra-content lint content/game-v1`. Its report would include lines such as:
- `guide-open: 'necesito ayuda' (ask_help) also matches buy_item` (only if `buy_item` were allowed there);
- `name_object: 6/6 by grammar`;
- `thank: 'muchas gracias por todo' by rule only`;
- `cefr a1: 'cueva' not listed`.

The author then runs `hash`. The golden re-pin is needed only in the core.

## 4. (Part C continued) What changes in the per-intent workflow

| Step | Today | After O7 + O1 + O8 |
| --- | --- | --- |
| Generic intents | copy and re-word | inherited; override by id only if needed |
| Grammars | by hand, checked by a playtest | by hand with macros; lint shows coverage and confusion at once |
| Learner-error example | unchecked string | example with expected tags, checked |
| Agent wiring | per state, pool name twice, pool count declared | one `anyState` line; pools derived |
| CEFR | advisory; prompt text not read | failing mode at the layer's level |
| Re-sync with the core | copy, re-word, rewrite attribution, reset id (G2) | bump the submodule; the merged hash follows the base |

## 5. Prototype: can grammars be derived from examples? (part E)

**Method.** A Python approximation of the matcher:
- whole-utterance match, with an optional leading subject pronoun and an optional trailing `por favor`;
- literals compare folded text or lemma;
- typed elements compare part of speech and lemma;
- `[WORD]` takes a word that is not only closed-class;
- lexicon lookup returns exact readings plus accent-folded variants, roughly M26b;
- no reading resolver and no tie-breaks.

Run over the game bundle's 15 intents and 79 examples. The suggester keeps closed-class words literal, abstracts articles to `[ART]`,
numerals to `[NUM:qty]`, nouns to the intent's noun slot and verbs to `[VERB:lemma]`, then merges skeletons that differ by one element
into an optional element. Variant 2 also normalises every noun phrase to `[ART]? [NUM:qty]? [NOUN:x]`.

| Measure | Hand grammars (+ rules) | Suggested | Suggested, variant 2 |
| --- | --- | --- | --- |
| Grammars | 44 (+18 rules) | 59 | 56 |
| Own examples covered by grammars | 77 / 79 (79 with rules) | 79 / 79 | 79 / 79 |
| Cross-intent example matches (confusion) | 4 | 0 | 6 |
| Leave-one-out: held-out example covered | n/a (written with all examples) | 23 / 79 (29 %) | 28 / 79 (35 %) |
| Suggestions with a wrong POS or slot | — | ≥ 12 of 59 | similar |

**Findings.**
- **Hand-grammar confusions.**
  - `cast_spell`'s bare-noun grammar still takes `gracias` and `ayuda`. This is the M17b-core bug class, now hidden only by the
    literal-count tie-break.
  - `buy_item`'s `[VERB:necesitar] … [NOUN:item]` takes `necesito ayuda` from `ask_help`. It is harmless today because no prompt allows
    both, and a prompt-aware lint would say so.
- **Variant 2 makes `name_object`'s article optional** and then takes `soy Sol` from `introduce_self`. The hand grammar keeps `[ART]`
  required, which is exactly the judgement examples cannot reveal.
- **Wrong readings from homographs.**
  - `buenas tardes` → `[VERB:tardar]`.
  - `de acuerdo` → `[VERB:acordar]`.
  - `mi nombre es` → `[VERB:nombrar]`.
  - `cuánto cuesta` → `cuesta` as a noun.
  - `lanzo un hechizo de fuego` → two spell slots.
  - All four example names in `introduce_self` are lexicon nouns, so `[WORD:name]` is never inferred.

**Conclusion.** With about 5 examples per intent, induction either memorises (low held-out coverage) or over-generalises (confusion). The
part-of-speech errors are the M18 and M20 problem again. The same code used as a *checker* immediately found real issues. Hence O7 first,
and suggestion reduced to "here is the skeleton of your uncovered example" inside the lint.

## 6. Recommendation (part D)

Ordered:

1. **M27a′, content lint (O7), Sonnet for the design, size S/M.**
   - **Scope:**
     - `vocaterra-content lint` with checks 1–5 of O7;
     - optional example objects with `errors` (check 6), in the intent schema;
     - `validate` prints lint warnings and never fails on them by default; `--strict` fails;
     - a per-intent waiver;
     - the same report on the sample and on a copy of the game bundle in a test fixture;
     - fold CR-010 into it (prompt text in `cefr-check`).
   - **Success:**
     - On the sample it reports the `cast_spell`/`thank` overlap and nothing the reviewer judges false, or with waivers recorded.
     - On `game-v1` it reproduces the 4 confusions above (filtered by prompt allow-lists) and lists the blacksmith pools as unreachable from
       the guide's prompts.
     - Runs in under 5 s on the sample.
     - Bundle hash unchanged unless examples gain expectations.

2. **M27b′, layering and `std-v1` (O1), Sonnet, size M.** This is §10.2 with the design changes in §7.
   - **Success:**
     - `content/v1` becomes `std-v1` plus an overlay of only blacksmith material;
     - every verdict in the goldens is unchanged apart from `contentHash`;
     - `std-v1` passes `cefr-check --level a1` with zero unlisted lemmas, which is G2's cost removed;
     - a layered bundle is packed, verified and hashed by the tool;
     - `docs/API.md` shows the route;
     - an answer to the game in `CORE_REQUESTS.md` or `NOTES_FROM_CORE.md`, with no game edits.

3. **M27c, de-duplication (O8 + O3), Haiku 5.5 with a precise brief, size S.** Additive schema; old files load identically.
   - **Success:** the sample bundle rewritten in the shorthand gives identical catalogs, and so identical goldens apart from the hash.

4. **M21, build-time drafting (O6)**, as the earlier report specified, with `lint --strict` and `cefr-check --fail-on-level` as its
   acceptance gate.

5. **Later:** O5 once D9 is decided, the e5 export is pinned and lint data gives held-out in-scope and out-of-scope sets for calibration;
   O4 when a third agent of an existing shape is planned; O2 revisited after M20.

**Are §10's M27a and M27b the right first two?** Layering, yes, as the second item and redesigned. Pattern suggestion, no. Its success
measure as written ("cover every example with no cross-intent confusion") is met by memorising the examples (§5) and says nothing about
cost. Its reporting half is the most valuable thing in §10 and is cheaper than layering, so it should go first. It also makes the layering
milestone safer: the overlay's behaviour can be checked against the sample's by lint as well as by goldens.

**What not to do:**
- **No deep merge, no array patches, no multi-level bases.** Whole-item replacement by id, one base.
- **No silent override.** An overlay id that exists in the base without a declared `replaces` is a load error.
- **Do not import Home Assistant or MASSIVE sentences into content.** The domain does not match and a CC BY notice would be needed. MASSIVE
  may later serve as an *evaluation* source of out-of-scope negatives only.
- **No `suggest` command as a milestone of its own.**
- **Do not make the lint a hard merge gate before one round of waivers.** The fuzz rule ("never weaken an oracle") applies once it is a
  gate.
- **Do not let the core read the game's `game/nouns.json` format.** Host-owned files stay host-owned (M26c).

## 7. Proposed changes to §10

- **§10.1.** Replace "carries all 11 sample intents as edited copies" with: "carries all 11 sample intents unchanged, plus 4 of its own; 15
  of 27 files are byte-identical; the only edits are 5 dialogue lines re-worded for A1 and the notice. About 445 copied lines are generic;
  about 680 are blacksmith material the game's code does not use". Add the per-intent cost (70–100 lines across six files) and the
  redundancy figures from §1.1.
- **New first item, M27a (lint).** Scope and success as in §6.1.
- **M27b (layering), design changes:**
  1. **Base reference.** In the manifest, `"base": {"bundleId": "std-v1", "bundleHash": "<sha256>", "path": "<relative dir>"}`. A host may
     also supply base directories through `ContentLoadOptions` (a packaged `std` in the content package). A hash mismatch is a hard error,
     as in `go.sum`.
  2. **Merged hash.** `Combine("layered", baseHash, overlayBundleHash)`. The overlay's own manifest is unchanged in form. `VersionStamp`
     records the merged hash; `provenance` and the replay header list both.
  3. **Granularity and per-kind rules:**
     - **Whole-item replacement by id:** intent, prompt, dialogue pool, answer set, agent, effect.
     - **Singleton replacement:** feedback policy, evaluation config.
     - **Union:** lexicon entries (as the folder merge already does) and CEFR lists.
     - **Per layer:** `ATTRIBUTION.md` (the loader exposes both notices). This also resolves CR-011 structurally.
  4. **Explicit operations.** The overlay manifest lists `replaces: [ids]` and `removes: [ids]` per kind. An undeclared collision, or a
     declared one that does not exist, is a validation error. `validate` prints the effective override table.
  5. **Folders first.** `intents/`, `prompts/` and `dialogue/` accept `*.json`, merged in ordinal path order with duplicate ids an error,
     so an overlay is "my scene's files" and not a fork of `intents.json`.
  6. **One level only.** `std-v1` itself has no base.
  7. **Level.** `std-v1` content passes A1 with zero unlisted lemmas, and the sample's blacksmith overlay keeps its own A2 words.
- **M27b → M27c (de-duplication), and suggestion moved to "later".** The suggestion skeleton for uncovered examples is part of the lint.
- **§10.4.** Keep templates and embeddings later, with the blockers named in O4 and O5. LLM drafting = M21, gated by the lint.

## 8. Sources

1. hassil (Apache-2.0): https://github.com/OHF-Voice/hassil
2. home-assistant/intents (CC BY 4.0; layout, `languages.yaml`, Spanish files read in a clone at `e77dd13`, 2026-10-21): https://github.com/home-assistant/intents
3. Template sentence syntax: https://contributors.home-assistant.io/translate/voice/template-syntax/
4. Custom sentences: https://www.home-assistant.io/voice_control/custom_sentences/ and https://www.home-assistant.io/voice_control/custom_sentences_yaml/
5. Rhasspy training (`sentences.ini`): https://rhasspy.readthedocs.io/en/latest/training/
6. Snips NLU (Apache-2.0): https://github.com/snipsco/snips-nlu
7. Chatito (MIT): https://github.com/rodrigopivi/Chatito
8. Rasa Open Source (Apache-2.0, maintenance mode, CALM): https://github.com/RasaHQ/rasa
9. Rasa training data format: https://rasa.com/docs/reference/primitives/training-data-format/
10. Rasa CLI, `rasa data validate`: https://rasa.com/docs/reference/api/command-line-interface/
11. Amazon Lex V2 utterance generation: https://docs.aws.amazon.com/lexv2/latest/dg/utterance-generation.html
12. Amazon Lex V2 generative features: https://docs.aws.amazon.com/lexv2/latest/dg/generative-features.html
13. Dialogflow ES entities (*unverified*, not fetched): https://cloud.google.com/dialogflow/es/docs/entities-overview
14. Padatious (Apache-2.0): https://github.com/MycroftAI/padatious
15. Ink licence (MIT): https://github.com/inkle/ink/blob/master/LICENSE.txt
16. Writing with Ink: https://github.com/inkle/ink/blob/master/Documentation/WritingWithInk.md
17. Yarn Spinner (MIT): https://github.com/YarnSpinnerTool/YarnSpinner
18. Yarn line groups: https://docs.yarnspinner.dev/write-yarn-scripts/scripting-fundamentals/line-groups
19. Yarn saliency and node groups: https://docs.yarnspinner.dev/write-yarn-scripts/advanced-scripting/saliency
20. SetFit, Tunstall et al. 2022: https://arxiv.org/abs/2209.11055 (numbers from the HTML version, https://arxiv.org/html/2209.11055)
21. CLINC150, Larson et al. 2019: https://aclanthology.org/D19-1131/
22. MASSIVE (CC BY 4.0): https://huggingface.co/datasets/AmazonScience/massive
23. Sahu et al. 2022: https://aclanthology.org/2022.nlp4convai-1.5
24. Minecraft data pack: https://minecraft.wiki/w/Data_pack
25. Minecraft tags: https://minecraft.wiki/w/Tag_(Java_Edition)
26. `pack.mcmeta` (filter, overlays): https://minecraft.wiki/w/Pack.mcmeta
27. Factorio data lifecycle: https://lua-api.factorio.com/latest/auxiliary/data-lifecycle.html
28. Factorio mod checksum (forum, *unverified*): https://forums.factorio.com/viewtopic.php?p=629718
29. Bethesda plugin merging: https://stepmodifications.org/wiki/Guide:Merging_Plugins
30. RFC 7386: https://www.rfc-editor.org/rfc/rfc7386
31. RFC 6902: https://datatracker.ietf.org/doc/html/rfc6902
32. CSS cascade: https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_cascade/Cascade
33. Go modules reference (`go.sum`): https://go.dev/ref/mod
34. Nix flakes: https://nix.dev/manual/nix/stable/command-ref/new-cli/nix3-flake.html
35. npm `package-lock.json`: https://docs.npmjs.com/cli/v10/configuring-npm/package-lock-json
36. Alignment-Based Learning, van Zaanen: https://arxiv.org/abs/cs/0104006
37. ADIOS, Solan et al. 2005: https://pmc.ncbi.nlm.nih.gov/articles/PMC1187953
38. Drain3: https://pypi.org/project/drain3/

Repository evidence: `IMPLEMENTATION_PLAN.md` §5b (M13b, M16b, M17c, M17b-core, M18, M19f, M26b, M26c); core commits `14f9963`, `b234506`,
`34f6927`; game commits `d17c674` (G2), `ed5133c` (P3), `07137ed` (P4); game `docs/DECISIONS.md` (D-game-4); game `IMPLEMENTATION_PLAN.md`
§5 (G2, P3, P4); `src/Vocaterra.Nlp.Content/ContentValidator.cs`, `ContentPaths.cs`;
`src/Vocaterra.Nlp.Language/Intent/IntentResolver.cs`, `IntentResolverOptions.cs`; `SlotGrammar/SlotGrammarMatcher.cs`.
