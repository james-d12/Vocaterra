# ADR 0009: The core and the game boundary

> **Note (2026-10-23, R02):** there is one content bundle now, `content/game-v1`. The sample bundle `content/v1` that this record keeps in the core as its demo and
> fixture (decision D7) was deleted; the core's tests, its fuzz suite, its demo CLI and its embedded seed lexicon read `content/game-v1`, found by walking up from the
> test assembly (nothing is copied). The boundary is otherwise unchanged: the game still never reaches into core source, the core still names no scene, item, price or
> reward, and effect kinds are still opaque (the game's effect table is empty today; the core's tests keep effects and curves covered with a table built in the
> test). A game edit that breaks something the core relies on fails a core test, which is wanted. Where the text below says "the sample bundle" or `content/v1`, read the
> game's bundle (D7 superseded; the owner's decision file records it).

> **Note (M19e, 2026-10-21):** game-side matters in this record, such as the engine choice and the game's scenes, are now decided in the Vocaterra game repository, not in this one. The text below is unchanged.
>
> **Note (2026-10-21, later; game decision D-game-7):** the two repositories are one again, as sibling folders: this record's "core" is `nlp/`, the "game" is `game/`. The boundary is unchanged and is now a folder boundary: the game references the core's projects (no packages), never copies core source, and a core change the game needs is a core milestone. The text below is unchanged.

> **Note (2026-10-23, R01):** the repository now has one root layout (`src/`, `tests/`, `tools/`, `docs/nlp/`, `docs/game/`, `content/`, `assets/`, `models/`, `scripts/`) and one solution. The boundary is by project name, not by folder: the core is the `Vocaterra.Nlp.*` projects (with `content/v1`, `tests/golden` and `docs/nlp/`), the game is `Vocaterra.Simulation`, `Vocaterra.Game`, `Vocaterra.MonoGame`, `Vocaterra.Host` and `Vocaterra.SpeechRender` (with their tests, `content/game-v1`, `assets/` and `docs/game/`). The rules are unchanged: the game references the core's projects (no packages), never copies core source, and a core change the game needs is a core milestone. The text below is unchanged.

> **Note (2026-10-26, M52):** the core is one engine assembly, `Vocaterra.Nlp` (`src/Vocaterra.Nlp`, one folder per former project), with language packs beside it (ADR 0012). Where this record says `Vocaterra.Nlp.*` projects, read the engine and its packs. The boundary is unchanged: the game references the core's one project and never copies core source. The table in section 1 is updated for that.

> **Note (2026-10-27, M34):** the analysis side of the language (morphology, analysis rules, template functions, lexicon profile, error tag ranks) is behind the language pack too (ADR 0011, amendment M34). The boundary is unchanged: the additions are public seams of the core, the game still calls `bundle.Morphology` (the Spanish `MorphologyGenerator`) as before, and no game word moved into the core.

> **Note (2026-10-27, M53):** the Spanish pack is the core project `Vocaterra.Nlp.Es` (`src/Vocaterra.Nlp.Es`, ADR 0012 amendment M53). The boundary is unchanged: it is core code, the game references it by project (`Game.Core` and `Host`, as it references the engine) and registers it where it loads its bundle (`SpanishContent.Load`); the game keeps the words, scenes and effect meanings.

> **Note (2026-10-27, M32):** the target language is a property of the content bundle (`manifest.json` `language`, absent means `es`), resolved to an `ILanguagePack` through a registry; see [ADR 0011](0011-target-language-as-a-bundle-property.md). The changes are additive (`ContentManifest.Language`, `ContentBundle.Language`, `ContentLoadOptions.LanguagePacks`, the `Vocaterra.Nlp.Language.Packs` types) and the boundary is unchanged in kind: a language, like the lexicon, is content, and the core still names no game word. A Spanish bundle without `language` keeps its hash.

> **Note (2026-11-01, M64; the review of `feature/village`, R2):** M56 to M62 added public surface that this record did not note; `docs/nlp/api.md` lists each change. `IAnalysisRules` (`Vocaterra.Nlp.Language.Analysis`) gained four members with no default implementation, so each is breaking for a host that implements the interface: `IsElapsedTimePreposition` and `IsElapsedTimeNoun` (M59, the preposition of `hace dos días` and the nouns of time it takes) and `IsPostnominalAdjective` and `IsDegreeAdverb` (M62, the colours that go after their noun, and `muy` and `tan`). `IMorphology` (`Vocaterra.Nlp.Language.Morphology`) gained two default members, `TryGetRegularizationOfWord` and `TryGetPluralSlip`, which find none unless a pack overrides them, and the public record `PluralSlip` (M58). The Spanish pack gained the public static class `SpanishTimeMarkers` (M57, `Vocaterra.Nlp.Es.Analysis`), the table of time markers that its checker and the fuzz suite's sentence generator both read; since M64 its lists are read-only collections, with the property types unchanged. M64 itself adds no public type or member. The boundary is unchanged in kind: each addition is a seam of the language pack or a Spanish word table of the pack, and no game word (no scene, character, item, price or reward) moved into the core.

> **Note (2026-11-01, M66; the grading gaps of sessions 7 to 10, handover 9.3 B):** `IAnalysisRules` gained two members, each with a default implementation that finds none, so neither breaks a host that implements the interface: `IsModalVerb(lemma, linker)` (the verbs that take an infinitive right after them, or after a linking word: Spanish `poder`, `querer`, `deber`, `necesitar`, and `tener` with `que`) and `ModalTakesNounObject(lemma)` (those of them that also take a noun: `querer`, `deber`, `necesitar`); the shared reading resolver reads them to take a finite verb after a modal for the learner's slip (`puedo pago`). `IMorphology.TryGetPluralSlip` and the record `PluralSlip` keep their signatures; their documentation now covers an adjective's plural slip (`azuls`) as well as a noun's. No public type was added, and the boundary is unchanged in kind: the new members are seams of the language pack, and the one bundle edit (`ti` in `lexicon/supplement.json`) is a word of the language, not of the game.

> **Note (2026-11-02, M70; the core findings of the QA pass on the village, and the owner's answers of 2026-11-01, D-game-22 items 2 and 3):** a public addition for the feedback. The record `AccentNote` and the enum `AccentMark` (`Vocaterra.Nlp.Core.Stages`) name a verb the learner wrote without the accent that marks its tense, read as the accented form at no cost (`compre` as `compré`): the analysis keeps them on `AnalysisResult.Notes`, and the pipeline joins those of the answer match on `PipelineResult.Notes` and `UtteranceAnalysis.Notes`. Each member is new, optional and null when there is no note, and is left out of the JSON while null, so a result without a note serializes as before; none is part of the verdict or of the replay record. The Spanish pack gained the public static class `SpanishAccentNotes` (`Vocaterra.Nlp.Es.Text`), the wording of a note in Spanish («compré», con tilde: es pasado); a host may word it itself, in the learner's language, from the note's members. `ILexiconFolding` (`Vocaterra.Nlp.Language.Packs`) gained `UnmarkedSpellings(form)` with a default implementation that gives none, so it breaks no host that implements the interface: the Spanish pack reads a word typed with `n` for `ñ` as the word when that spelling is no word of its own (`manana` for `mañana`, not `ano` for `año`), through `Lexicon.Lookup`'s accent-insensitive fallback. The boundary is unchanged in kind: the note is a fact about the learner's Spanish that the host shows as it likes, and the bundle was not edited; the content change M70 found (a name slot for `me llamo`) is the game's, for V21.

> **Note (2026-11-02, M69; the left points of M56, M58 and M62, handover 9.3 B):** `IAnalysisRules` gained three members, each with a default implementation that finds none, so none breaks a host that implements the interface: `IsLocationAdverb(lemma)` (Spanish `lejos`, `cerca`: a copula's form that is also a noun before one is the copula), `CallsForSlip(tokens, index, slip)` (where the words around a lexicon word of another class call for the learner's slip it may stand for: `yo podo ir`, `yo esto cansado`) and `IsPredicateAdjective(lemma)` (the common descriptive adjectives whose noun homographs are no bare predicate: `él es alta`). No public type was added and no bundle file changed; the boundary is unchanged in kind: the new members are seams of the language pack.

> **Note (2026-11-02, M72; the off-prompt lines, the owner's answers of 2026-11-02, D-game-22 option 2):** one optional public member on two records. `IntentResolution.OffPromptIntent` and `Verdict.OffPromptIntent` (`IntentId?`, the last positional parameter of each, defaulting to null) name the intent a line has over the whole intent catalog when the prompt's allowed intents resolve nothing: the resolver runs its grammar and keyword rungs again over every intent (no embedding), and the verdict carries the result only for a line the prompt did not take (an unknown intent, no answer matched, so never a communicated line). `Intent`, `Communicated`, the slots and the repair ladder are unchanged; the core plans no reply to it, and the host decides what an NPC says (the game's V27). Each member is left out of the JSON while null, so a verdict without it serializes as before M72, and the end-to-end goldens did not move; `Verdict.Equals` and `GetHashCode` include it. With it, the derived `vocab.wrong` (M67) is narrowed to a miss it can name: a correct line that is simply not among a prompt's several answers gets no error (it was 0.4). Old replay logs with such a line show a difference when replayed, as after M67: the verdict lost its `vocab.wrong` and gained the member. The bundle's one edit, a tighter keyword rule of `ask_activity` (`hace frío` is not a question about work), is content, not boundary.

> **Note (2026-11-02, M73; the small core points after the follow-ups):** `IAnalysisRules` gained one member with a default implementation that finds none, so it breaks no host that implements the interface: `IsNeuterPronoun(word)` (Spanish `todo`, `eso`, `esto`, `aquello`: a word that is a noun and a finite verb right after the interrogative quantifier and before one is the verb, `¿cuántas cuesta todo?`). No public type was added; the intent catalog of `content/game-v1` changed (keyword rules of `ask_activity` and `ask_craft`, a grammar of `ask_craft` and of `greet`), which is the game's content and no core contract. The boundary is unchanged in kind.

> **Note (2026-11-04, M75; the near-miss rule, the owner's answer of 2026-11-02, D-game-22: "a near miss must name its word"):** public additions, none breaking. The record `AnswerNearMiss` and the enum `SlipPair` (`Vocaterra.Nlp.Core.Stages`) name the one word by which a line that matched no answer misses the nearest answer phrase, a slip pair; `AnswerNearMisses.Find` (`Vocaterra.Nlp.Language.Matching`) finds it; `VerdictInputs` gained the optional trailing `NearMiss` (null by default), from which the derived `vocab.wrong` takes its word and the answer's word as `Expected`; `IAnalysisRules` gained `IsQuestionWord(word)` with a default implementation that finds none. The verdict, its JSON and the replay record are unchanged in shape: a host sees the rule only in the derived error, whose `Expected` is now filled for a near miss (`cuál es esto` at `lesson-what`: `qué`), and in the correct lines that no longer carry it (`quiero hacer un hacha` at `smith-open`). The boundary is unchanged in kind.

- Status: accepted
- Date: 2026-10-20
- Deciders: the repository owner (repository names, the rename to `Vocaterra.Nlp.*`, and decisions D6 and D7 in [docs/nlp/decisions.md](../decisions.md)); the implementation plan (`implementation-plan.md` section 8, milestone M19a)

This record fixes what the language core `Vocaterra.NLP` (this repository) owns and what the game `Vocaterra` (a separate repository) owns. It
is the rule every later change to the core is checked against.

## Context

The core was built inside one game project, so game words reached its code: the conversation state machine was called an NPC machine, the
verdict context was a `GameContext` with NPC fields, and the effect table named two outcome kinds for spells and trade prices. The research
report (`docs/research/2026-10-20-scaling-approaches.md`) and the plan's section 8 both assume the core will be consumed as NuGet packages by a game that
is not in this repository. That needs a boundary the code can be checked against, and names that do not presuppose a game.

## Decision

### 1. What stays in the core and what moves to the game

| Stays in the core (the engine `Vocaterra.Nlp` and its packs: `Vocaterra.Nlp.Es`) | Moves to the game repository (`Vocaterra`) |
| --- | --- |
| The contracts: verdict, prompt spec, error tags, replay envelope | `specification.md` and the game design |
| Audio, ASR, phonology, pronunciation, NLP, evaluation | Character definitions (blacksmith, innkeeper, captain) and their dialogue pools |
| Decision: learner model (BKT), scheduling (FSRS), feedback policy, and the conversation state-machine **engine** (`ConversationAgents`) | Items, spells, quests, prices and rewards: the effect tables and the names of host events |
| Response: templates, recasts, speech synthesis, and the effect resolver with opaque outcome kinds | The game's content bundles, built with the core's ContentBuilder |
| Content: bundle format, loader, validator, provenance, lexicon import, CEFR lists | Engine integration (Unity, Godot or MonoGame), rendering, device audio capture |
| The pipeline with a game-agnostic `ConversationContext` | Glue that maps outcomes to prices, spell power and quest state |
| Tools (Cli, Bench, ContentBuilder), fuzz and golden suites, a sample bundle | |
| The Spanish language pack `Vocaterra.Nlp.Es` (M53): the Spanish normalizer, numbers, morphology, analysis rules, G2P and seed lexicon, and the host helpers `SpanishContent` and `SpanishLexicon` | The game references it (`Game.Core`, `Host`) and registers it where it loads its bundle |

The sample bundle in `content/v1` (a blacksmith's dialogue, spells and items) stays in the core as its demo and as the fixture the goldens,
the fuzz suite and the docs run against (decision D7). The game keeps its own bundle.

> Note (M19c, 2026-10-20): the game design moved out of this repository. The `specification.md` row in the table above now refers to
> `docs/game/specification.md` in the game repository ([Vocaterra](https://github.com/james-d12/Vocaterra)), and the game's bundle is its
> `content/game-v1`. The table records the boundary as it was decided.

### 2. What the core knows, and what it does not

The core knows nothing about prices, rewards, items, spells or quests. Outcome kinds and host events are opaque to it:

- An effect's `kind` is a non-empty string the host interprets. The core computes the value of an effect from its curve and passes the kind
  through unchanged. The sample bundle uses `trade_price` and `spell_power` as plain strings; nothing in the core branches on them.
- A conversation action's `hostEvent` is a string the core copies into the step result. The core never acts on it.
- An effect's `effectId` must be unique within a bundle; the effect table refuses a duplicate. Kinds are not required to be unique, because
  two intents may legitimately share one (the sample's `trade_price` serves `buy_item` and `sell_item`).
- The host supplies the stored state of the world through `ConversationContext`: a world seed, the agent id and its state, a stored
  timestamp, the learner model, repeat counts and `HostStateHash`, a digest of the rest of the host's state. The core copies the hash into
  the replay record and never interprets it.

### 3. The renames that follow the boundary

| Before (`Slg.*` era) | After (`Vocaterra.Nlp.*`) |
| --- | --- |
| Solution `SpanishLearningGame.sln`, projects `Slg.X`, `tests/Slg.X.Tests`, `tools/Slg.Cli`, `tools/Slg.Bench`, `tools/Slg.ContentBuilder` | `Vocaterra.Nlp.sln`, projects `Vocaterra.Nlp.X` (folders, assemblies and namespaces) |
| `GameContext` (`NpcId`, `NpcState`, `GameStateHash`) | `ConversationContext` (`AgentId`, `AgentState`, `HostStateHash`) |
| `Slg.Decision.Npc` with `NpcDefinition`, `NpcStateDefinition`, `NpcTransition`, `NpcCondition`, `NpcActionDefinition`, `NpcFallback`, `NpcStateMachine`, `NpcState`, `NpcAction`, `NpcStepResult` | `Vocaterra.Nlp.Decision.ConversationAgents` with `AgentDefinition`, `AgentStateDefinition`, `AgentTransition`, `AgentCondition`, `AgentActionDefinition`, `AgentFallback`, `AgentStateMachine`, `AgentState`, `AgentAction`, `AgentStepResult` |
| `GameEffect` | `ResolvedEffect` |
| `EffectKinds` (`SpellPower`, `TradePrice`, `QuestFlag`, `None`) | removed; kinds are opaque strings (see 2) |
| `ContentBundle.Npcs`, `ContentPaths.NpcFolder`, folder `content/v1/npcs/` | `ContentBundle.Agents`, `ContentPaths.AgentFolder`, folder `content/v1/agents/` |
| JSON keys `npcId`, `gameEvent`, `gameStateHash`, `npc` (in decisions) | `agentId`, `hostEvent`, `hostStateHash`, `agent` |
| CLI `--npc-state`, output lines `npc:` | CLI `--agent-state`, output lines `agent:` |
| `SLG_MODELS_DIR`, `SLG_RECORD_GOLDEN`, `SLG_LEXICON_SOURCES_DIR`, `SLG_RUN_MODEL_TESTS`, `SLGFUZZ_*` | `VOCATERRA_MODELS_DIR`, `VOCATERRA_RECORD_GOLDEN`, `VOCATERRA_LEXICON_SOURCES_DIR`, `VOCATERRA_RUN_MODEL_TESTS`, `VOCATERRA_FUZZ_*`; the old names are read for one release |

Persisted and hashing strings were not renamed where a rename would change a hash the core promises: the runtime cache folder
`slg-vosk-runtime` and the version string `vosk-runtime-1` that keys it, and the bundle id `v1`. The embedded lexicon resource and the
namespaces moved with the code. `content/v1/lexicon/game.json` keeps its file name as sample data.

The namespace `Vocaterra.Nlp` is the only new root namespace. The package ids follow the projects (`Vocaterra.Nlp.<Project>`), as planned in
M19b.

## Consequences

- Replay logs and end-to-end goldens recorded before this change do not replay: the replay envelope and the verdict's bundle hash carry the
  renamed keys and the new bundle hash. The goldens were re-recorded (only the names, the bundle hash and the host state hash changed).
- The bundle hash changed with the file and key renames. `tests/golden/manifest.jsonl` is untouched, and `cefr-check` at a1 and a2 is unchanged.
- The game repository consumes the core through `docs/nlp/api.md`. A change to a public type listed there is a change to the boundary and
  needs this record updated.
- Anything a host needs that the core does not provide (microphone capture, save files, rendering) stays out of the core.

## Alternatives considered

- **The state-machine engine in the game (decision D6, alternative).** The core would stop at the verdict and the feedback plan. The engine is
  content-driven interaction logic that every host would otherwise reimplement, and its tests are part of the core's fuzz suite, so it stays.
- **A neutral sample bundle (decision D7, alternative).** It would remove the game flavour from the demo but would invalidate every golden and
  every documented output. The blacksmith stays until the game repository has its own bundle.
- **Keeping `Slg.*` until packages are published.** Package ids cannot change after publication, so the names were fixed before M19b.

## Open items

- The brief for M19a asked for "outcome kinds validated for uniqueness within a bundle". The sample kinds are not unique across intents, so the
  validation enforces unique effect ids, and kinds are checked only to be non-empty. If the owner meant unique `(intent, kind)` pairs, the
  change is one check in the effect table.
- The ContentBuilder and bench tools still read the sample bundle; a neutral or game bundle is M19c.
