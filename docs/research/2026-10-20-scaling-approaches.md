# Scaling the Spanish processing core: research and recommendation

> **Note (M19e, 2026-10-21):** game-side matters in this record, such as the engine choice and the game's scenes, are now decided in the Vocaterra game repository, not in this one. The text below is unchanged.

Research report, 2026-10-20, written against `main` at `5f816de` (M17a done, M18 in progress). It answers the owner's request to
"find better methods that scale up, so we don't need to build an entire processing application for Spanish". Every external claim
carries a link in [section 7](#7-sources); claims marked **unverified** could not be confirmed from a primary source, and numbers marked
**est.** are mine.

**The short answer.** Keep the parts of the repository that are game infrastructure: the verdict contract, determinism primitives,
replay, content bundle and provenance, NPC state machines, learner model, feedback policy, templates, effects, G2P and the fuzz suite.
Keep the hand-written error checks too. Two quick experiments (sections 3.1 and 3.2) show that they catch more learner errors than
LanguageTool does, and that their false positives come from not knowing each word's part of speech. Stop writing Spanish linguistics by
hand at the input side: take part of speech, lemmas and dependencies from a trained Universal Dependencies tagger, which resolves both
M17a false positives in the experiment. Generate intents, examples and dialogue with an LLM at build time under the existing Freeze
pattern. Later, add an optional LLM tutor at run time. Record its output the way the speech recognizer's output is recorded today (the
Cache pattern), validate it with deterministic code, and never let it touch prices, rewards, NPC state or scheduling.

## 1. What the current approach does well, and where it strains

### What is worth keeping

The repository is two things at once: well-built **game infrastructure** that is language-agnostic, and a hand-built **Spanish NLP
stack**. The first part is good and cheap to keep:

- **The verdict contract** separates *communicated* from *correct* and carries stable error tags with spans and expected forms. That
  is the right waist for the system, and it does not care what produces it.
- **Determinism primitives and replay.** Seeded RNG, stored timestamps, canonical JSON and SHA-256 pins are small and done. More
  important, the replay envelope already records the outputs of the model-backed stages (`asr`, `posteriors`), so everything after
  recognition is a pure function of recorded inputs (`docs/replay.md`). That is exactly the hook that makes it safe to put *more*
  models, even non-deterministic ones, in front of the verdict: record their output and replay it.
- **Content bundle with provenance.** `provenance.json` already has `generatedBy`, `approvedBy` and `approvedOn` per file, and the
  loader enforces the `wiktionary-import` marking. The Freeze pattern has its data model; it only lacks a generator.
- **Decision and response layers.** NPC state machines, BKT, FSRS, the feedback policy table, the template engine with morphological
  generation and the effect curves are data-driven, small (Decision 1.3k lines, Response 2.3k) and correct by the fuzz oracles.
- **G2P.** Spanish spelling is shallow, so a rule-based converter is the right tool, not a stopgap (1.9k lines, 1 788-line golden set).
- **Morphological generation.** Producing `tengo` from `tener` + 1sg, or `espadas` from `espada` + plural, is something taggers do not
  do. `MorphologyGenerator` plus the Wiktionary paradigms is the right source of *expected* forms for corrections and recasts.
- **The fuzz suite.** Sixteen oracles found eight real bugs (fuzz README) and the O4/O5 generate, mutate, analyze, recast loop is an
  excellent labelled-test generator, whatever analyzer sits behind it.

### Where it strains

| Strain | Evidence (repository) |
| --- | --- |
| The analyzer is not part-of-speech aware, and vocabulary growth multiplies the damage | After M17a, forms with more than one reading rose from 89 to 7 216 (22 % of forms); 339 of 1 583 usable nouns (21 %) are also verb forms; `ayer compré un libro` yields a false `conj.tense` (`libro` read as `librar`) and `cuánto cuesta la espada` a false `gender.agree`; four more analyzer bugs surfaced in the first run on the new words (`lexicon.md`, findings log) |
| Every error type is a hand-written check | `AgreementChecker.cs` is 475 lines of linear scans; `ser_estar` is decided by a list of 23 adjectives, `conj.tense` by three past adverbs (`AnalysisTables.cs`); subjunctive, por/para and pronoun placement are still open questions in the design |
| Every intent, grammar and line is hand-authored | Six intents, 20 slot grammars, eight keyword rules, 31 examples, one NPC, 14 prompts; `ayer tengo dos espadas` is not understood because no grammar allows a leading adverb (CLI-examples review); M17b, ten intents and two NPCs, is sized L |
| The fuzz oracles check agreement between two hand-written models, not Spanish | `SentenceRealizer` "is a model of the analyzer, so the oracles are an agreement check between two implementations" (fuzz README); homographs are skipped and counted rather than tested, so a linguistic mistake both models share is invisible |
| A lexicon used as the analyzer costs run time too | 3 000 lemmas became 41 078 entries; bundle load went from 8 ms to 217–267 ms; 5 000 lemmas failed the 300 ms gate (`lexicon.md`) |
| The model-backed half mostly does not run yet | The phoneme model has no ONNX export, so no `pron.*` tag can fire outside tests; Parakeet is unbenchmarked; Kokoro is blocked by GPL espeak-ng inside sherpa-onnx (ADR 0007); the golden learner set holds three synthetic entries; there is no microphone adapter and no engine (README, Known gaps) |

Code size makes the split visible. Of about 21 400 lines in `src/`, the language-understanding code in `Vocaterra.Nlp.Language` is about 5 900
(analysis 716, morphology 743, slot grammar 573, intent 1 010, lexicon 883, matching 449, normalization 856, embeddings 672), with
4 700 lines of unit tests and a large share of the 10 900-line fuzz project behind it. The mechanism in that code (lexicon loading,
grammar matching, margins) is sound; the knowledge (which words take *estar*, which adverbs mark the past, which phrasings mean
`buy_item`) is where each new word, tag or scene costs a milestone.

The deepest problem is not any single bug. The HLD's biggest risk is recognizing and grading real learner speech, and after eighteen
milestones it is still unmeasured. Meanwhile effort went into the one part of the analyzer that trained models already do better:
choosing each word's reading.

## 2. What "scaling up" should mean for this owner

The owner is one learner who wants to practise Spanish by playing. For that goal, "scale" has five dimensions, and one quality rule
above them all.

1. **Vocabulary.** Any of the few thousand most frequent lemmas is recognized and analyzed without false tags.
2. **Grammar coverage.** The A1–B1 error types a learner actually makes, including the ones the HLD left open (subjunctive, por/para,
   pronoun placement, *gustar*-type verbs), without writing a checker per type.
3. **Natural phrasing.** "¿Me das una espada?", "quisiera la espada" and "dame esa espada" all reach `buy_item` without a grammar each.
4. **More scenes.** A new NPC or intent costs an evening of reviewing generated content, not a milestone.
5. **Pronunciation feedback** that runs on the owner's own voice.

The quality rule: **a false correction is worse than a missed one.** A learner who is told that correct Spanish is wrong learns wrong
Spanish and stops trusting the game. Precision of error tags matters more than recall, and "communicated" should err towards yes,
which the design already does.

### Which design constraints are worth keeping

The determinism tiers were written for a game in general. For a single-player learning game some of them buy little and cost a lot.

| Constraint (HLD, ADR 0002) | Verdict | Why |
| --- | --- | --- |
| Seeded RNG, stored timestamps, canonical JSON, hashed content and models | **Keep** | Already built, nearly free, and they make saves, bug reports and tests reproducible |
| Game state is D0: prices, rewards, NPC transitions, effects, learner model, FSRS scheduling | **Keep, as the hard rule** | These are the game's economy and the learner's schedule; a model must never decide them |
| A verdict can be replayed exactly | **Keep, by recording** | Record every model output in the replay envelope and the save (the HLD's Cache pattern); replay then needs no model and no bit-identical inference |
| D1 (frozen model, greedy decoding, one thread) for taggers, classifiers, ASR | **Keep** | Taggers and classifiers are argmax by nature; the rule costs nothing there |
| "No N-class output may change the verdict" | **Relax** | Allow an LLM's tags and corrections into the verdict when they are recorded (Cache) and confirmed or bounded by D0 checks (section 3.8). There is no opponent to be unfair to and no multiplayer state to desynchronise |
| Bit-identical golden results across x64, ARM64, Windows, macOS | **Relax** | Keep margins; run goldens on recorded model outputs; the matrix protects fairness between players this game does not have |
| Offline, no paid or hosted service | **Relax to "offline-capable"** | The core loop (constrained prompts, tagger-based checks, templates) stays offline; an online tutor is optional and costs cents per hour (section 3.8) |
| Permissive licences for linked code | **Keep for shipping; allow a process boundary for personal use** | The stance is right for a release; a copyleft or non-commercial tool in a separate process is a reasonable trade while the game is personal (section 6) |
| Min-spec 8 GB, 4 cores, CPU only | **Keep as the target** | Everything recommended below fits; a local LLM is the one component that strains it |

## 3. Survey

Tier labels follow the HLD: **D0** algorithmic, **D1** frozen model with argmax or greedy decoding, **N** sampled or not reproducible.
Latency classes for one short utterance on the 4-core min-spec are: **A** under about 10 ms, **B** 10–50 ms, **C** 50–500 ms,
**D** over half a second. They are estimates unless a number is cited. Speech recognizers get times instead of classes.

### 3.1 Morphology, part of speech, lemmas and dependency parsing

Here is the direct answer to the homograph problem. Every serious Spanish pipeline is trained on the Universal Dependencies (UD)
treebanks and returns, for each token, a part of speech (UPOS), morphological features, a lemma and a dependency head with a relation.
All of them sit in tier D1.

| Tool | Code licence | Spanish model licence | Size | UPOS on AnCora | .NET route | Passes the stance? |
| --- | --- | --- | --- | --- | --- | --- |
| UDPipe 2 | MPL-2.0 [3] | CC BY-NC-SA [1] | server-side | 99.05 % [1] | Python sidecar or LINDAT REST [6] | No: the models are non-commercial |
| UDPipe 1 | MPL-2.0 [3] | CC BY-NC-SA for the stock UD 2.5 models [2]; a model you train yourself is yours | 20 MB (AnCora) [2] | 98.3 % [2] | native C# SWIG binding [3], community NuGet [23] | **Yes if self-trained** on AnCora and MPL-2.0 is accepted (section 6) |
| Stanza | Apache-2.0 [9] | Apache-2.0 [12] | PyTorch, a few hundred MB (**unverified**) | 98.96 % [10] | Python sidecar | Yes, as a separate process |
| spaCy `es_core_news_*` | MIT | **GPL-3.0** [7] | 12 MB (sm) [7] | 98.13 % (sm) [7] | Python sidecar | No |
| PlanTL `es_bsc_demo_md` (spaCy) | MIT | MIT [8] | about 117 MB | 98.60 % [8] | Python sidecar, pinned to spaCy 3.4 | Yes, but unmaintained since 2022 |
| Catalyst | MIT [13] | MIT [14] | 107 MB package [14] | 96.0 %, own dev score; no morphological features [13] | **native NuGet** | Yes; weakest |
| Trankit | Apache-2.0 [20] | not stated | XLM-R base | 99.02 % [20] | Python sidecar | Unclear; last release 2021 |
| FreeLing | AGPL-3.0 [15] | AGPL / LGPLLR | – | not checked | socket server | No |
| Apertium `apertium-spa` | GPL-2.0 [16] | GPL-2.0 | – | not a UD tagset | CLI | No |
| Hugging Face token classifier (`xlm-roberta-base-ft-udpos28-es`) | – | Apache-2.0 [21] | 1.1 GB [21] | 95.5 %, test set unstated [21] | ONNX Runtime after your own export, `Microsoft.ML.Tokenizers` [22] | Yes; UPOS only |

The treebanks themselves decide what a self-trained model may carry: UD_Spanish-AnCora (547 558 tokens [19]) is **CC BY 4.0** [17];
UD_Spanish-GSD is CC BY-SA 4.0 [18]. A model trained on AnCora alone needs only attribution. UDPipe 1 trains from the command line
[3] and runs at about 1 700 words per second for the full pipeline on one core [4]. A five-word utterance is therefore class A. UDPipe 2
shows the pattern to copy: from version 2.1 it can restrict predictions to the analyses a morphological dictionary allows [5]. Here the
Wiktionary lexicon proposes the candidate readings, and the tagger picks one.

**Experiment.** I sent the two M17a failures, the homographs from the fuzz README and a set of learner errors to UDPipe 2 through the
LINDAT service [6] (models `spanish-ancora-ud-2.17-251125` and `spanish-gsd-ud-2.17-251125`, pre-segmented, one sentence per line). I
ran the same sentences through this repository's CLI (`eval --transcript … --prompt trade-free-conversation`, Release build of
`5f816de`). The service's non-commercial terms are fine for a test. They are why it cannot be the product.

| Correct sentence | Current analyzer | UDPipe 2 (AnCora model) |
| --- | --- | --- |
| ayer compré un libro | false `conj.tense` (`libro` → `libré`) | `libro` NOUN ✓ |
| ¿cuánto cuesta la espada? | false `gender.agree` (`cuánto` → `cuánta`) | `cuánto` PRON, PronType=Int ✓ |
| ayer vi una lanza | false `conj.tense` (`lanza` → `lanzó`) | `lanza` NOUN ✓ |
| ayer recibí tus ayudas | false `conj.tense` (`ayudas` → `ayudaste`) | `ayudas` NOUN ✓ |
| nosotros comemos como reyes; yo sé la respuesta; la casa es grande; tengo una cuenta; el trabajo es difícil; ellos fueron felices | no tag | all tagged correctly (`como` SCONJ, `sé` = `saber` 1sg) ✓ |
| ayer bebí vino | no tag | `bebí` mis-tagged ADJ (the GSD model gets VERB) ✗ |
| ellos fueron al mercado; ayer fui a la herrería | no tag | lemma `ser` instead of `ir` in both models ✗ |

For the learner errors, the tags carry exactly the facts the agreement checks need, attached to the right word. `yo` (Person=1) is the
`nsubj` of `tiene` (Person=3). `dos` is the `nummod` of the singular `espada`. Masculine `un` and `el` are the `det` of feminine
`espada` and `mesa`. `tú` (Person=2) is the subject of `habla` (Person=3). The irregular error `sabo` got the invented lemma `sar`,
which is why expected forms must keep coming from `MorphologyGenerator`, not from the tagger.

**Conclusion.** A stock UD tagger removes the four false tags in the experiment, which should also let the fuzz generators stop
skipping homographs, and it keeps the information the existing checks use. It is not perfect. It was trained on newspaper text, so first- and second-person forms and dialogue are weaker (`bebí`),
and the `ir`/`ser` preterite needs a small D0 rule (`fui`/`fueron` + `a` + place reads as `ir`). The fuzz suite's `SentenceRealizer`
can also generate thousands of correct, automatically annotated dialogue sentences to add to AnCora when training your own model. The
recommended route is **UDPipe 1 trained on AnCora**, called through its C# binding, or through its CLI if MPL-2.0 is not accepted.
Spike Stanza (permissive, best ready-made numbers, recently given silver data for `como` as a verb [11]) as the comparison. Catalyst is
the native fallback, but it gives UPOS only.

### 3.2 Grammar error detection and correction

| Tag | Learner sentences tested | Current analyzer | LanguageTool 6.9-SNAPSHOT (public API [27]) |
| --- | --- | --- | --- |
| `conj.person` | yo tiene, tú habla, nosotros tiene, yo sabe | 4/4 | 3/4 (misses *tú habla*) |
| `noun.number` | dos espada, las casa, quiero dos pocion, buenas tarde | 3/4 (*buenas tarde* corrected to *buena*) | 3/4, plus *pocion* only as a misspelling |
| `gender.agree` | un espada, el mesa, la mesa es rojo, la casa es bonito, una mujer muy simpático | 2/5 | 3/5 |
| `ser_estar` | soy cansado, estoy inteligente | 2/2 | 0/2 |
| `conj.tense` | ayer como pan, ayer yo compro | 2/2 | 0/2 |
| `word_order` | el rojo coche | 1/1 | 0/1 |
| `conj.irregular` | yo sabo, yo poní | 2/2 (*sé*, *puse*) | 0/2 (misspelling flags with wrong suggestions) |
| False tags on 15 correct sentences | | **4** | 0 (ignoring capitalisation and punctuation rules, which do not apply to ASR text) |

On this taxonomy the hand-written checks find 16 of 20 errors against LanguageTool's 9 (plus 3 flagged only as misspellings);
*estoy alto* was left out as debatable. Their one
weakness is the four false tags, and section 3.1 removes those. The external options compare as follows.

- **LanguageTool**: LGPL-2.1 [24], and so is its Spanish tagger dictionary [28]. It has 1 644 XML rules for Spanish [25] and a JSON HTTP
  server that needs Java [26]. The public API allows 20 requests per minute and no bulk use [27]. It produces `gender.agree`,
  `noun.number` and `conj.person`, plus spelling, which maps onto `vocab.unknown`. It has no rule for ser/estar, tense against time
  adverbs or adjective order. That is expected: it was built for native writers, not learners. **Use:** a development-time oracle, run
  as a separate process, that is independent of the analyzer.
- **Spanish GEC models and data**: COWS-L2H [29] is an Apache-2.0 learner corpus. Its annotation covers agreement, ser/estar and
  adjective placement, close to this taxonomy, and its baseline neural corrector reached only F0.5 0.224 [30]. No Spanish ERRANT
  (automatic error-type annotation) exists [31]. The MultiGEC-2025 shared task has no Spanish [32]. The Hugging Face Spanish
  correctors are hobby fine-tunes, and the most interesting one has no licence [33]. Any of them returns a corrected sentence that would
  still need diffing and classifying. **Use:** COWS-L2H as evaluation data.
- **An LLM as judge**: it can name any tag, including the ones the HLD left open. Its precision is the problem. On Spanish, GPT-3.5
  "substantially alters the source sentences, including their semantics" and over-corrects [34], and ChatGPT ignores minimal edits
  [35]. A GPT-3.5 grader for A1 Spanish writing flagged correct sentences such as *La motocicleta es negra* as agreement errors and
  invented ser/estar errors [37]. Two techniques help: extracting edits first and then explaining each one raised correct explanations
  from 40.6 % to over 92 % [36], and asking for minimal edits added 1–11 F0.5 points [38]. **Use:** section 3.8.

### 3.3 Intent and slot understanding

| Option | Licence | Size | Latency | Tier | Role |
| --- | --- | --- | --- | --- | --- |
| Slot grammars and keyword rules (current) | own | – | A | D0 | Keep grammars for closed prompts and slot extraction; retire keyword rules |
| multilingual-e5-small nearest neighbour (current) | MIT [39] | 118M, ONNX in the repo [39] | A (est.) | D1 | Keep as the backbone |
| granite-embedding-97m-multilingual-r2 | Apache-2.0 [41] | 97M, ONNX [41] | A (est.) | D1 | Drop-in candidate |
| multilingual-e5-base | MIT [40] | 278M, ONNX | B (est.) | D1 | Larger, better scores [47] |
| Qwen3-Embedding-0.6B | Apache-2.0 [42] | 0.6B | C (est.) | D1 | Best MMTEB score of the permissive models (64.33) [42] |
| EmbeddingGemma, jina-embeddings-v3 | Gemma terms [43]; CC BY-NC 4.0 [44] | | | | Excluded by licence |
| mDeBERTa-v3 NLI zero-shot | MIT [45] | 279M, ONNX [45] | B per intent × number of intents (est.) | D1 | Tie-breaker at most |
| SetFit classifier trained on generated examples | Apache-2.0, exports one ONNX graph [46] | backbone size | A–B (est.) | D1 | **Main rung for open prompts** |
| LLM with schema-constrained output | see 3.8 | 0.4–2.5 GB (local) or cloud | D | N | Fallback when the classifier is uncertain |

The literature favours generating data with an LLM and distilling it into a small classifier. Students trained on LLM-generated data
beat the generator's own zero-shot accuracy, by up to 30 % in high-resource languages, though Spanish was not among the languages
tested [51]. Across 8 languages, zero-shot LLMs won only on sentiment [52]. On real intent sets, the best LLMs reached F1 ≈ 0.735
against SetFit's 0.600 (0.658 with negative examples). SetFit answered in a median 30 ms against about 1.7 s for the LLM, and sending
only the uncertain queries to the LLM came within 2 % of its accuracy at half the latency [53]. That is the current ladder with a
better middle rung. Two pitfalls: generated examples drift into neighbouring intents [49], and gains flatten as generated data grows
[51]. Hence near-miss negatives and human review, with mixed relabelling as in PromptMix [50] and LINGUIST [48].

**Build-time generation under Freeze.** A `Vocaterra.Nlp.ContentBuilder draft` command sends an intent's definition, slot catalog, CEFR list and
existing examples to an LLM. It writes a draft file marked `llm-draft:<model>@<prompt-hash>` with 30–50 paraphrases, error-injected
variants labelled with their tags (which double as analyzer test cases), near-miss negatives, slot-grammar drafts for closed prompts,
dialogue variants (run through `cefr-check`) and hint text. The owner approves in a review command that fills `approvedBy` and
`approvedOn`. The loader already enforces provenance for `wiktionary-import` files. It can refuse unapproved `llm-draft` files the same
way. A training step then turns the approved examples into an ONNX classifier pinned in `models/manifest.json`.

### 3.4 Speech recognition

| Model | Licence | Size | Native Spanish WER | CPU, 3 s clip | Biasing | .NET route |
| --- | --- | --- | --- | --- | --- | --- |
| Vosk small es 0.42 (current) | Apache-2.0 [54] | 39 MB [54] | CV 16.0, MLS 11.2 [54] | fast, streaming; HLD target under 150 ms after key release (unmeasured) | runtime grammar on small models [55] | current |
| Parakeet TDT 0.6B v3 (pinned) | CC-BY-4.0 [56] | 0.6B; int8 about 670 MB (tools.md) | FLEURS 3.45, MLS 4.39 [56] | about 1.2 s: RTF 0.325 with 2 threads, CPU unnamed [57] | sherpa-onnx hotwords need a transducer and modified beam search [65]; with Parakeet v3 **unverified** | sherpa-onnx; Whisper.net's new Parakeet runtime [58] |
| Whisper small (whisper.cpp) | MIT [59] | 466 MiB [59] | FLEURS 5.6 [61] | seconds: always encodes a 30 s window (est.) | GBNF grammar, prompt [60] | Whisper.net [58] |
| Whisper large-v3 | MIT | 2.9 GiB [59] | FLEURS about 3 | too slow for min-spec (est.) | same | same |
| Canary-180M-flash | CC-BY-4.0 [62] | 182M [62] | MLS 3.17 [62] | under Parakeet (est.) | – | sherpa-onnx (package **unverified**) |
| Qwen3-ASR 0.6B | Apache-2.0 [63] | 0.6B | – | slow (LLM decoder, est.) | – | none documented [63] |
| Moonshine Spanish | non-commercial community licence [64] | 58M | – | fast | – | excluded |

Evidence on learner Spanish is thin. In the one study found, Whisper large-v3 transcribed English-L1 learners of Spanish (B2–C1) at
12 % WER and SeamlessM4T at 11 % [66], against about 3 % on native FLEURS. More important for this game, strong recognizers smooth
learner errors away. Error-preserving recognition of learner speech needed fine-tuning [67]. Of a vanilla Whisper's errors on learner
English, 54.2 % were attributable to learner-speech features [68]. Without a prompt, Whisper large-v3 removed disfluencies and made
grammatical changes ("there is the uh stars" → "there are stars") [69]. The repository's `Vocaterra.Nlp.Bench` already measures exactly this
("learner-error preservation"), but only on three synthetic entries.

**Recommendation.** Keep Vosk's grammar mode for closed prompts. Constrained decoding is the error-preserving path, and adding the
predicted wrong variants to the grammar (`tengo`/`tiene`, `espada`/`espadas`) is what the Alelo tutor did [97]. For open prompts,
measure before choosing: record the owner, then compare Parakeet (pinned, no 30 s window) with Whisper small with fallback disabled
(`-nf`, [60]) on WER and learner-error preservation. Every recognizer stays D1 and recorded.

### 3.5 Pronunciation assessment

| Option | Licence | Size | Output | .NET route | Realistic? |
| --- | --- | --- | --- | --- | --- |
| wav2vec2-xlsr-53-espeak-cv-ft (pinned) | Apache-2.0 | about 300M | CTC phone posteriors | needs an ONNX export nobody publishes | Yes, weaker: phone error rate (PFER) 11.88 [75] |
| **ZIPA-CR small** | code MIT, weights CC BY 4.0; the `-Ns` variants are CC BY-NC-SA [73] | 64M [73]; fp32, fp16 and int8 ONNX [74] | IPA CTC posteriors | ONNX Runtime plus an fbank front end | **Best option**: PFER 3.22 on seen languages [75] |
| wav2vec2-lv-60 ONNX (community export) | Apache-2.0 [76] | – | 392-phone logits | ONNX Runtime today | Usable now |
| CTC GOP, segmentation-free (Cao et al.) | paper; the code has no licence [71][72] | – | per-phone GOP | reimplement over existing posteriors | Yes |
| Montreal Forced Aligner | MIT; Spanish models CC BY 4.0 [77] | – | word and phone alignments | CLI | Development-time checks |
| Azure Pronunciation Assessment | commercial | cloud | es-ES and es-MX supported [78]; phoneme names only for en-US [79] | Speech SDK | Optional online path |
| SpeechAce | commercial | cloud | Spanish supported [80] | REST | Pricing not published |

The HLD cites arXiv 2309.07739 for GOP alignment, but that paper is a different method (IntraVerbalPA [70]). The CTC GOP work that
matches this repository's design is [71]. Its phone-level correlation with human scores is moderate: 0.450 for segmentation-free CTC GOP
against 0.361 for classic GOP, and 0.648 with a learned scorer on top [71]. Treat `pron.*` as hints on a few contrasts (tap and trill,
vowel quality, a pronounced *h*, stress), calibrated on the owner's recordings. No permissively licensed, phone-annotated L2 Spanish
corpus turned up. The practical step is to swap the phoneme model for ZIPA through ONNX Runtime, so the existing CTC alignment and GOP
code finally runs on real audio.

### 3.6 Text to speech

The ADR 0007 blocker is about to disappear. sherpa-onnx PR #4055, merged on 2026-10-20, states that "espeak-ng and piper-phonemize are
no longer built into sherpa-onnx" and that "sherpa-onnx is GPL-free after this PR" [81]. The latest release, v1.13.8, still bundles
espeak-ng [82], so pin the first release after the merge. Kokoro still needs phonemes. The in-house G2P can supply them, which issue
#3731 (out-of-vocabulary Spanish words skipped by the lexicon front end) argues for [83].

| Model | Licence | Spanish | Needs espeak-ng? | .NET route |
| --- | --- | --- | --- | --- |
| Kokoro-82M (pinned) | Apache-2.0 [84] | 3 voices [85] | by default yes [85]; phoneme input bypasses it [86][87][81] | ONNX Runtime directly, or the next sherpa-onnx release |
| Piper voices | engine GPL-3.0 (`piper1-gpl`) [88]; voice data licences range from CC0 to CC BY-SA [89] | 9 voices [89] | yes | sherpa-onnx VITS with your own phonemes |
| piper-plus | MIT [90] | only an experimental zero-shot model [90] | no | NuGet `PiperPlus.Core` |
| Supertonic 2 | code MIT, weights OpenRAIL-M [91] | yes | no | C# sample [91]; the weights carry use restrictions |
| XTTS-v2 | CPML, non-commercial [92] | yes | – | excluded |

Keep Kokoro, feed it phones from `Vocaterra.Nlp.Phonology` (ADR 0007's plan, now without the native-library problem), and pre-render static lines.

### 3.7 Whole-stack alternatives

| Project | Open | Licence | Stack | Lesson |
| --- | --- | --- | --- | --- |
| FreeLingo [93] | yes | AGPL-3.0 | faster-whisper, Ollama / OpenAI / Anthropic, Kokoro | Speech and LLM calls go through one backend; CEFR placement first |
| langteacher [94] | yes | PolyForm Noncommercial | faster-whisper, llama.cpp, Piper, offline on CPU | **At most one correction per turn** |
| LexiLingo [95] | yes | MIT | faster-whisper, Piper, phoneme scoring, Qwen3-1.7B | Errors map to concept IDs in a learner model |
| CALL-SLT [96] | research | – | grammar-based recognition; prompts give an abstract meaning | Closest to this design; measured a learning gain |
| Alelo TLTS [97] | no | – | recognizer with modelled learner errors | Put predictable errors into constrained recognition |
| KTH DEAL [98] | no | – | shopkeeper NPC plus a separate tutor | The NPC converses; a tutor corrects |
| Duolingo Max [99], Speak [100], Praktika [101] | no | – | cloud LLM, Whisper-class recognizer, separate progress agent | Humans author scenarios; feedback often after the conversation |

None of these has reusable code under a compatible licence that would save real work here. The reusable part is the design. Commercial
products converge on a cloud LLM, Whisper-class recognition, human-authored scenarios and a learner model; they separate the NPC's reply
from the tutor's correction and limit corrections per turn. This repository already has a stronger learner model and a stronger
determinism story than any of the open projects.

### 3.8 The LLM question, head-on

**What it would be.** One call per utterance, with the transcript, the tagger's analysis, the scene, the allowed intents and the slot
catalog. The call returns a Verdict-shaped JSON (intent, slots, errors with tag, span, expected and actual, a corrected sentence and a
short explanation), constrained by a JSON schema (Claude structured outputs guarantee schema-valid output except on refusal or
truncation [103]; llama.cpp offers GBNF and JSON-schema grammars [109], available from .NET through LLamaSharp [110] or ONNX Runtime GenAI
guidance [111]). Deterministic code then assembles the verdict, as today.

**Cost** (Claude prices [102]; my token estimate: a 1 500-token cached system prompt, 150 new input tokens, 250 output tokens plus up to
300 thinking tokens at low effort):

| Model | Per utterance | Per 1 000 utterances | Per heavy hour (about 200 utterances, est.) |
| --- | --- | --- | --- |
| Claude Haiku 5.5 ($0.10 / $0.50 per MTok, cache reads $0.01) | about $0.0003 | about $0.30 | about $0.06 |
| Claude Sonnet 5.5 ($2 / $10, cache reads $0.10) | about $0.006 | about $6 | about $1.20 |
| Claude Opus 5.5 ($4 / $20, cache reads $0.20) | about $0.012 | about $12 | about $2.40 |
| Local Qwen3-4B (Apache-2.0 [113]) | electricity | – | – |

**Latency.** A cloud call is a network round trip plus about 250 tokens of generation. I did not measure it, so treat it as "around a
second or more" and design for it: the NPC answers at once from the deterministic path, and the tutor's note arrives a moment later or
after the conversation, as Duolingo does [99]. Local is slower. A 3B model at Q4 measured about 43 tokens/s prompt and 11.8 tokens/s
generation on a 4-core i7 [112], so a 4B model needs about 25–30 s for a 250-token answer (est.). That rules out per-turn feedback on min-spec. A
cached system prompt and a 30–60-token intent answer take a few seconds, which is acceptable as a fallback for uncertain intents.
Permissive local candidates with good Spanish: Qwen3 (Apache-2.0) [113], Phi-4-mini (MIT, official ONNX) [114], SmolLM3 (Apache-2.0,
Spanish among six languages) [115], and BSC's Salamandra (Apache-2.0, 16 % Spanish pretraining, but described as a proof of concept)
[116].

**Determinism.** Claude Haiku 5.5 accepts only `temperature` 1 (any other value is a 400) [104], so cloud output is N by construction.
llama.cpp on CPU is bit-reproducible for one sequence with fixed batch settings on one machine [108]. It is explicitly not guaranteed
across batch sizes or prompt-cache paths [106], parallel server slots give different answers at temperature 0 [107], and the root cause
is kernels that are not batch-invariant [105]. Cross-machine equality is **unverified**. Treat every LLM as N and make it replayable by
recording its output (Cache).

**Hallucinated corrections** are the real risk (section 3.2 evidence [34][35][37]), along with CEFR-level drift in long dialogues
[117]. The mitigations are mechanical:

- ask for minimal edits;
- explain only edits that a deterministic diff extracted;
- re-analyze the corrected sentence and reject it if it gains errors;
- require every `expected` form to exist in the lexicon;
- score an LLM-only tag only when a D0 check confirms it, or when the tag is outside D0's reach. In that case it is marked
  `source: llm`, weighted lower, and it never flips `communicated` on its own.

**The hybrid split.**

| Decision | Decided by | Why |
| --- | --- | --- |
| Prices, rewards, effects, quest state, NPC transitions | D0 tables and state machines | The game's economy; must replay; no reason for a model |
| Learner model updates, FSRS scheduling, content picking | D0 | The learning schedule must be stable and explainable |
| `correctness`, `communicated`, `resolvedBy` | D0 arithmetic over tags | The verdict contract |
| Part of speech, lemma, parse | D1 tagger, recorded | Argmax, cheap, testable |
| Intent on open prompts | D1 classifier with margins; LLM only on `Uncertain` | Fast path stays local and testable |
| Tags D0 cannot check (ser/estar in context, por/para, subjunctive, pronoun placement) | LLM (N, cached), validated, lower weight | No hand-written checker per tag |
| Correction text, explanations, flavour lines | LLM (N, cached or Advisory) with template fallback | Presentation; no state change |

**Guarding it.** Unit tests and goldens use recorded LLM outputs through a recorded-tutor double, like `ReplayWordRecognizer` today, so
CI stays offline and deterministic. O14 extends naturally: replaying a recorded tutor output reproduces the verdict, and mutating it
produces a diff. Live models are judged by a separate evaluation suite outside CI, on a frozen labelled set: O4's single-error mutations
(each already carries its tag and expected form), approved generated learner sentences, a COWS-L2H sample and the owner's own replay
logs. It reports the false-correction rate on correct sentences, per-tag precision and recall, and schema violations. Changing a model
or a prompt is gated on that report.

## 4. Recommended target architecture

```text
 BUILD TIME (developer machine)                                         RUN TIME (player machine)
 ────────────────────────────────                                       ─────────────────────────
 LLM drafts (Freeze) ──► owner approves ──► content/v1 (hashed)         mic ─► Vocaterra.Nlp.Audio (keep) ─► PcmClip
   paraphrases, near-miss negatives,          provenance: llm-draft,              │
   error variants with tags, grammars,        approvedBy/approvedOn              ▼
   dialogue variants, hints, tips                  │                    ASR [D1, recorded]: Vosk grammar (closed)
                                                   │                    │  or Parakeet / Whisper (open, after benchmark)
 approved examples ──► SetFit / e5 ──► ONNX ───────┤                    ▼
 AnCora (CC BY 4.0) ──► UDPipe 1 train ──► model ──┤ models/manifest    Normalize, answer match (keep)
                                                   ▼                    ▼
                                         lexicon + paradigms (keep) ─►  UD tagger [D1, recorded]: UPOS, feats, lemma, head
                                                                        │  (lexicon proposes readings, tagger picks)
                                                                        ▼
                                          D0 checks over dependency arcs + MorphologyGenerator expected forms
                                                                        │
                                          intent: grammar (closed) ─► classifier [D1] ─► LLM [N, cached, optional]
                                                                        │
                                          tutor [N, cached, optional, async]: errors, correction, explanation
                                            └─► D0 validator (schema, spans, lexicon, re-analysis, confirmation)
                                                                        ▼
                                          Verdict (keep; ErrorTag gains a source) ─► Decision (keep, D0)
                                                                        ▼
                                          Response templates (keep) ─► Kokoro with in-house phones ─► audio
                                          Replay envelope + save: ASR, posteriors, tagger, classifier, tutor JSON
```

| Module | Fate |
| --- | --- |
| `Vocaterra.Nlp.Core` contracts and determinism primitives | **Stay.** Additive changes only: a morphosyntax result type, an error `source`, recorded tutor output in the envelope |
| `Vocaterra.Nlp.Audio`, `Vocaterra.Nlp.Phonology` (G2P) | **Stay.** G2P also feeds Kokoro |
| `Vocaterra.Nlp.Language` normalization, matching, lexicon loading, `MorphologyGenerator`, slot-grammar matcher | **Stay.** The lexicon's role shifts from analyzer to candidate generator and source of expected forms |
| `LinguisticAnalyzer` reading choice | **Replaced** by the UD tagger |
| `AgreementChecker` linear scans | **Rewritten** as checks over dependency arcs (`det`, `amod`, `nummod`, `nsubj`, `cop`); smaller |
| `AnalysisTables` (23 ser/estar adjectives, 3 past adverbs) | **Shrinks**: generated, approved lists for D0, and the tutor for context-dependent cases |
| Keyword rules | **Replaced** by the trained classifier |
| Hand-authored intent examples, grammars, dialogue | **Generated, then approved** (Freeze) |
| `Vocaterra.Nlp.Evaluation`, `Vocaterra.Nlp.Decision`, `Vocaterra.Nlp.Response`, `Vocaterra.Nlp.Content`, `Vocaterra.Nlp.Pipeline` | **Stay** |
| `Vocaterra.Nlp.Pronunciation` | **Stays**; the phoneme model changes to ZIPA via ONNX |
| `Vocaterra.Nlp.Asr` | **Stays**; the open-prompt model is chosen by benchmark |
| Fuzz and golden suites | **Stay**, plus an evaluation suite with independent oracles (tagger, LanguageTool, COWS-L2H) that ends the "two models of the analyzer" blind spot |

## 5. Migration plan

Each milestone leaves `main` playable through the CLI, keeps every fuzz oracle at default depth green, and records any new model output
in the replay envelope. Sizes use the plan's S/M/L.

| # | Milestone | What changes | Gate (beyond §6 of the plan) | Size |
| --- | --- | --- | --- | --- |
| 1 | **M18, rescoped: UD tagger** | Spike UDPipe 1 trained on AnCora, Stanza (sidecar) and Catalyst on the experiment sentences; adapter behind an interface; the tagger filters lexicon readings; checks unchanged; the `ir`/`ser` D0 rule | The four false tags of the experiment gone; the fuzz generators stop skipping verb-form homographs (skip counts near zero) with O3–O5 green; analysis within the 5 ms D0 budget plus tagger time (class A) | M |
| 2 | **M19: build-time drafting** | `ContentBuilder draft` and `approve`; `llm-draft` provenance; the loader refuses unapproved drafts | Provenance tests; the coverage tests from M17a | M |
| 3 | **M17b, using M19** | Ten intents and two NPCs, drafted then reviewed | As planned in §7.4 | M (was L) |
| 4 | **M20: tree-based checks and an evaluation set** | Agreement over dependency arcs; labelled set from O4 mutations, approved generated learner sentences, a COWS-L2H sample; LanguageTool as a development-time oracle in a separate process | False tags on correct sentences no higher than after M18; per-tag recall no lower | M |
| 5 | **M21: open-prompt classifier** | SetFit or fine-tuned e5/granite on approved examples, ONNX, pinned; keyword rules retired; repair ladder unchanged | Held-out accuracy reported; O7 rung order adjusted | M |
| 6 | **M22: speech reality check** (can start now, in parallel) | Record 100–200 of the owner's utterances, including deliberate errors; benchmark Vosk, Parakeet and Whisper small; ZIPA phoneme adapter; pin the GPL-free sherpa-onnx; Kokoro with in-house phones | Measured WER, learner-error preservation and latency; `pron.*` fires on real audio | M |
| 7 | **M23: optional tutor** | Tutor adapter (Claude Haiku over HTTP; LLamaSharp local fallback), D0 validator, envelope and save recording, asynchronous feedback, evaluation report | False-correction rate on correct sentences under a threshold the owner sets; CI on recorded outputs only | L |

Effort: six M milestones and one L, about seven of the twenty milestones done so far (M0–M17a), so roughly a third of the work to date.
It removes code rather than adding a parallel stack: keyword rules, most of the linear-scan checker, the reading heuristics added for
ambiguous readings in M17a, and most future hand-written grammars and dialogue. Milestones 1, 2 and 6 give the most per unit of
effort. Milestone 7 is optional and should wait until 6 has shown what recognition can be trusted with.

## 6. Decisions only the owner can make

1. **Online or offline.** *Recommendation:* an offline-capable core, with the online tutor optional. Every closed prompt, the tagger,
   the checks and the templates work offline. The tutor adds cents per hour with Haiku-class models (section 3.8). Sending transcripts
   to a cloud API is also a privacy decision. Audio would never leave the machine.
2. **How strict determinism should be.** *Recommendation:* make the verdict "replay-exact" rather than "re-inference-exact". Any model
   may contribute if its output is recorded and validated, while game state stays D0 without exception. This amends ADR 0002 rule 8 (no
   N-class code in `src/`) and the HLD's hard rule 1 (no N-class output may change the verdict); write it down as an ADR before
   milestone 7.
3. **Licence stance on weak-copyleft and non-commercial parts.** *Recommendation:*
   - accept MPL-2.0 for unmodified libraries such as UDPipe 1, whose copyleft is file-level; otherwise run it as a CLI sidecar;
   - allow LGPL (LanguageTool) and GPL (espeak-ng) only as separate processes at development time;
   - keep non-commercial models (stock UDPipe models, ZIPA `-Ns`) to experiments;
   - choose the repository licence before anything ships.
4. **Cloud cost.** *Recommendation:* Haiku 5.5 for the run-time tutor, and Sonnet or Opus for build-time drafting, where volume is
   tiny. Set a monthly cap and log usage per session.
5. **Game engine.** *Recommendation:* pick now, because no milestone has touched a game yet and the owner cannot learn Spanish from a
   CLI. Godot .NET is MIT-licensed [119], runs C# in-process with these libraries, and its `AudioEffectCapture` exposes microphone
   frames [120]. MonoGame is the code-first alternative.
6. **Pronunciation ambition.** *Recommendation:* decide after milestone 6. With moderate phone-level reliability (section 3.5),
   pronunciation hints on a few contrasts are realistic; graded pronunciation is not.

**What I could not verify:** cloud latency; CPU latency of the encoders, Parakeet and Whisper on the min-spec; whether llama.cpp output
is equal across thread counts and CPUs; Stanza's model sizes; whether sherpa-onnx hotwords work with Parakeet v3; the Kokoro Spanish
voices' quality. I did not train a UDPipe model on AnCora, so its accuracy on dialogue is inferred from the stock model. The experiments
used 34 hand-picked sentences, so they illustrate the failure modes rather than measure rates.

## 7. Sources

Accessed 2026-10-20.

**3.1 Morphology and part of speech**
1. UDPipe 2 models (licence, AnCora scores): https://ufal.mff.cuni.cz/udpipe/2/models
2. UDPipe 1 models (CC BY-NC-SA, AnCora 98.3 % UPOS): https://ufal.mff.cuni.cz/udpipe/1/models
3. UDPipe repository (MPL-2.0, bindings, training): https://github.com/ufal/udpipe
4. Straka and Straková, CoNLL 2017 (UDPipe speed): https://aclanthology.org/K17-3009/
5. UDPipe 2.1.0 release (dictionary-constrained prediction): https://github.com/ufal/udpipe/releases/tag/v2.1.0
6. LINDAT UDPipe service and API terms: https://lindat.mff.cuni.cz/services/udpipe/ , https://lindat.mff.cuni.cz/services/udpipe/api-reference.php
7. spaCy `es_core_news_sm` 3.8.0 (GPL-3.0, accuracy): https://github.com/explosion/spacy-models/releases/tag/es_core_news_sm-3.8.0
8. PlanTL `es_bsc_demo_md` (MIT): https://huggingface.co/PlanTL-GOB-ES/es_bsc_demo_md
9. Stanza licence: https://github.com/stanfordnlp/stanza/blob/main/LICENSE
10. Stanza performance: https://stanfordnlp.github.io/stanza/performance.html
11. Stanza releases: https://github.com/stanfordnlp/stanza/releases
12. Stanza Spanish models: https://huggingface.co/stanfordnlp/stanza-es
13. Catalyst: https://github.com/curiosity-ai/catalyst
14. Catalyst.Models.Spanish: https://www.nuget.org/packages/Catalyst.Models.Spanish/
15. FreeLing licence: https://github.com/TALP-UPC/FreeLing/blob/master/COPYING
16. apertium-spa: https://github.com/apertium/apertium-spa
17. UD_Spanish-AnCora licence (CC BY 4.0): https://github.com/UniversalDependencies/UD_Spanish-AnCora/blob/master/LICENSE.txt
18. UD_Spanish-GSD: https://github.com/UniversalDependencies/UD_Spanish-GSD
19. UD Spanish treebank comparison: https://universaldependencies.org/treebanks/es-comparison.html
20. Trankit: https://github.com/nlp-uoregon/trankit , https://trankit.readthedocs.io/en/latest/performance.html
21. `xlm-roberta-base-ft-udpos28-es`: https://huggingface.co/wietsedv/xlm-roberta-base-ft-udpos28-es
22. Microsoft.ML.Tokenizers SentencePiece: https://learn.microsoft.com/en-us/dotnet/api/microsoft.ml.tokenizers.sentencepiecetokenizer.create
23. UDPipe.CSharp.Wrapper: https://www.nuget.org/packages/UDPipe.CSharp.Wrapper/

**3.2 Grammar errors**
24. LanguageTool (LGPL-2.1): https://github.com/languagetool-org/languagetool
25. LanguageTool languages and rule counts: https://dev.languagetool.org/languages
26. LanguageTool HTTP server: https://dev.languagetool.org/http-server
27. LanguageTool public API (limits; the endpoint used in the experiment): https://dev.languagetool.org/public-http-api , https://api.languagetool.org/v2/check
28. Spanish tagger dictionary (LGPL): https://repo1.maven.org/maven2/org/softcatala/spanish-pos-dict/2.5/spanish-pos-dict-2.5.pom
29. COWS-L2H: https://github.com/ucdaviscl/cowsl2h
30. Davidson et al., LREC 2020: https://aclanthology.org/2020.lrec-1.894
31. ERRANT adaptations survey: https://arxiv.org/pdf/2506.07719
32. MultiGEC-2025: https://www.aclweb.org/portal/content/shared-task-multilingual-grammatical-error-correction-2025
33. SkitCon Spanish GEC models: https://huggingface.co/SkitCon/gec-spanish-BARTO-SYNTHETIC
34. Katinskaia and Yangarber 2024, "GPT-3.5 for Grammatical Error Correction": https://arxiv.org/abs/2405.08469
35. Fang et al. 2023: https://arxiv.org/abs/2304.01746
36. GEE, NAACL Findings 2024: https://arxiv.org/abs/2311.09517
37. Miller De Rutté and Correa, ReCALL 2026: https://www.cambridge.org/core/journals/recall/article/evaluating-gptgenerated-feedback-for-beginninglevel-spanish-writing-an-exploratory-study/9B1019F0ED0BEA63092CC1D31350047C
38. Minimal-edit prompting for GEC (2026): https://arxiv.org/html/2609.10810v1

**3.3 Intent**
39. multilingual-e5-small: https://huggingface.co/intfloat/multilingual-e5-small
40. multilingual-e5-base: https://huggingface.co/intfloat/multilingual-e5-base
41. granite-embedding-97m-multilingual-r2: https://huggingface.co/ibm-granite/granite-embedding-97m-multilingual-r2
42. Qwen3-Embedding-0.6B: https://huggingface.co/Qwen/Qwen3-Embedding-0.6B
43. EmbeddingGemma: https://huggingface.co/google/embeddinggemma-300m
44. jina-embeddings-v3: https://huggingface.co/jinaai/jina-embeddings-v3
45. mDeBERTa-v3 multilingual NLI: https://huggingface.co/MoritzLaurer/mDeBERTa-v3-base-xnli-multilingual-nli-2mil7
46. SetFit (Apache-2.0, ONNX export): https://github.com/huggingface/setfit
47. MMTEB: https://arxiv.org/abs/2502.13595
48. LINGUIST: https://arxiv.org/abs/2209.09900
49. Sahu et al. 2022: https://aclanthology.org/2022.nlp4convai-1.5
50. PromptMix: https://arxiv.org/abs/2310.14192
51. Pecher et al. 2026: https://arxiv.org/abs/2601.16278
52. Vajjala and Shimangaud 2025: https://arxiv.org/abs/2502.11830
53. Arora et al., EMNLP 2024 Industry: https://aclanthology.org/2024.emnlp-industry.114/

**3.4 Speech recognition**
54. Vosk models and WER: https://alphacephei.com/vosk/models
55. Vosk adaptation: https://alphacephei.com/vosk/adaptation
56. Parakeet TDT 0.6B v3: https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3
57. sherpa-onnx NeMo transducer models (RTF): https://k2-fsa.github.io/sherpa/onnx/pretrained_models/offline-transducer/nemo-transducer-models.html
58. Whisper.net (MIT, Parakeet runtime, grammar): https://github.com/sandrohanea/whisper.net
59. whisper.cpp: https://github.com/ggml-org/whisper.cpp
60. whisper.cpp CLI options: https://github.com/ggml-org/whisper.cpp/tree/master/examples/cli
61. Radford et al., Whisper: https://arxiv.org/abs/2212.04356
62. Canary-180M-flash: https://huggingface.co/nvidia/canary-180m-flash
63. Qwen3-ASR: https://github.com/QwenLM/Qwen3-ASR
64. Moonshine licence: https://raw.githubusercontent.com/moonshine-ai/moonshine/main/LICENSE
65. sherpa-onnx hotwords: https://k2-fsa.github.io/sherpa/onnx/hotwords/index.html
66. Sarymsakova and Martín-Rodilla, Phonica 21 (2025), Table 2: https://doi.org/10.1344/phonica2025.21.2
67. Michot et al., ACL 2024, error-preserving ASR: https://aclanthology.org/2024.acl-long.348/
68. LearnerVoice: https://arxiv.org/abs/2407.04280
69. Whisper on disfluent learner speech: https://arxiv.org/html/2503.06924v2

**3.5 Pronunciation**
70. arXiv 2309.07739 (the HLD's citation): https://arxiv.org/abs/2309.07739
71. Cao et al., segmentation-free CTC GOP: https://arxiv.org/abs/2507.16838
72. CTC-based-GOP code (no licence): https://github.com/frank613/CTC-based-GOP
73. ZIPA (code MIT, weight licences): https://github.com/lingjzhu/zipa
74. ZIPA-CR small ONNX: https://huggingface.co/anyspeech/zipa-small-crctc-300k
75. ZIPA paper (PFER comparison): https://arxiv.org/abs/2505.23170
76. wav2vec2 espeak CTC ONNX export: https://huggingface.co/sadda-speech/wav2vec2-espeak-ctc
77. Montreal Forced Aligner Spanish models: https://mfa-models.readthedocs.io/en/latest/acoustic/Spanish/index.html
78. Azure pronunciation assessment languages: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=pronunciation-assessment
79. Azure pronunciation assessment how-to: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-pronunciation-assessment
80. SpeechAce languages: https://api-docs.speechace.com/getting-started/supported-languages.md

**3.6 Text to speech**
81. sherpa-onnx PR #4055: https://github.com/k2-fsa/sherpa-onnx/pull/4055
82. sherpa-onnx releases: https://github.com/k2-fsa/sherpa-onnx/releases
83. sherpa-onnx issue #3731: https://github.com/k2-fsa/sherpa-onnx/issues/3731
84. Kokoro-82M: https://huggingface.co/hexgrad/Kokoro-82M
85. Kokoro voices: https://huggingface.co/hexgrad/Kokoro-82M/blob/main/VOICES.md
86. Kokoro ONNX (inputs): https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX
87. kokoro-onnx: https://github.com/thewh1teagle/kokoro-onnx
88. piper1-gpl: https://github.com/OHF-Voice/piper1-gpl
89. Piper voices: https://huggingface.co/rhasspy/piper-voices
90. piper-plus: https://github.com/ayutaz/piper-plus
91. Supertonic 2: https://huggingface.co/Supertone/supertonic-2 , https://github.com/supertone-inc/supertonic
92. XTTS-v2 licence: https://huggingface.co/coqui/XTTS-v2/raw/main/LICENSE.txt

**3.7 Whole-stack**
93. FreeLingo: https://github.com/artcc/freelingo
94. langteacher: https://github.com/janosdios/langteacher
95. LexiLingo: https://github.com/InfinityZero3000/LexiLingo
96. CALL-SLT, LiLT 2014: https://journals.colorado.edu/index.php/lilt/article/view/1353
97. Alelo TLTS, Interspeech 2005: https://www.isca-archive.org/interspeech_2005/sethy05b_interspeech.html
98. KTH DEAL, SIGdial 2007: https://preview.aclanthology.org/fix_video/2007.sigdial-1.24.pdf
99. Duolingo Max: https://blog.duolingo.com/duolingo-max
100. Speak live roleplays: https://speak.com/blog/live-roleplays
101. Praktika: https://openai.com/index/praktika

**3.8 LLMs**
102. Claude pricing: https://platform.claude.com/docs/en/about-claude/pricing
103. Claude structured outputs: https://platform.claude.com/docs/en/build-with-claude/structured-outputs
104. Claude Haiku 5.5 migration guide (sampling parameters): https://platform.claude.com/docs/en/models/haiku-5-5/migration-guide
105. Thinking Machines, "Defeating Nondeterminism in LLM Inference": https://thinkingmachines.ai/blog/defeating-nondeterminism-in-llm-inference/
106. llama.cpp server README: https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md
107. llama.cpp issue #7052: https://github.com/ggml-org/llama.cpp/issues/7052
108. llama.cpp PR #16016: https://github.com/ggml-org/llama.cpp/pull/16016
109. llama.cpp grammars: https://github.com/ggml-org/llama.cpp/blob/master/grammars/README.md
110. LLamaSharp: https://github.com/SciSharp/LLamaSharp , https://github.com/SciSharp/LLamaSharp/blob/master/LLama.Examples/Examples/GrammarJsonResponse.cs
111. ONNX Runtime GenAI: https://github.com/microsoft/onnxruntime-genai
112. llama.cpp CPU benchmark (i7-6700, 3B Q4): https://computingforgeeks.com/run-local-llm-llama-cpp/
113. Qwen3-4B-Instruct-2507: https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507
114. Phi-4-mini-instruct: https://huggingface.co/microsoft/Phi-4-mini-instruct
115. SmolLM3: https://huggingface.co/HuggingFaceTB/SmolLM3-3B
116. Salamandra: https://huggingface.co/BSC-LT/salamandra-2b-instruct
117. Almasi and Kristensen-McLachlan, BEA 2025: https://arxiv.org/abs/2505.08351

**6. Owner decisions**
119. Godot licence: https://godotengine.org/license/
120. Godot `AudioEffectCapture`: https://docs.godotengine.org/en/stable/classes/class_audioeffectcapture.html
