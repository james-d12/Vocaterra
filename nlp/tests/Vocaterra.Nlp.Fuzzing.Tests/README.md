# Vocaterra.Nlp.Fuzzing.Tests

Combinatorial, property-based fuzz testing for the voice pipeline, modelled on `CodeGuard.RuleFuzzing.Tests`. The design is
`docs/FUZZING_PLAN.md` (generators, oracles O1 to O16, tolerances, iteration budgets). In short: every generator walks **our own
catalogs** (the lexicon, the `IntentCatalog` grammars, `ErrorTags.All`, the `DialogueCatalog` templates, the `AnswerSet`s and the JSON files
of `content/v1`, `PhoneInventory`, `DialectConfig.AllShipped`, the agent definitions), mixes benign and adversarial inputs 3 to 1, and runs them
through the real stages looking for crashes, drift between components that must agree, and broken determinism. Because the generators read the
catalogs, new content widens the coverage with no test edits. Everything runs in process with fakes for the model-backed stages
(`ScriptedWordRecognizer`, `FakePhonemeRecognizer`, `FakeEmbeddingModel`, `SilentSynthesizer`); no model file is needed.

All sixteen oracles of the plan are here. **Stage 1 (M16a)** is the foundation and the language oracles (O1 to O7, O10, O12, O16). **Stage 2
(M16b)** adds the audio, phone, decision and content-mutation generators and the signal, decision, content and pipeline oracles (O8, O9, O11,
O13, O14, O15), the CI jobs, and the bug fixes listed below.

## Running

```bash
dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests
```

No flags are needed: every fuzz `[Fact]` runs its full default depth on a plain run (there is no "fast" tier to opt out of). Measured on a
development machine (4 cores), with the project already built: **about 17 s wall time** for the whole project at default depth (16 to 19 s over
25 runs; stage 1 alone was about 5 s), against the plan's budget of about 60 s. With the imported lexicon of M17a (3 000 lemmas, 41 000 entries) the
same project takes **18.7 to 20.3 s** on a 16-logical-core machine (three runs; the previous tree timed interleaved with it: 17.1 to 18.2 s), and the whole
solution's `dotnet test --no-build` takes 24.6 to 25.5 s (previous tree: 20.5 to 21.0 s). After M18 (the part-of-speech resolver, below) the same project takes **19.4 to 22.3 s (11 runs)** on the same machine (before it, measured the same day: **18.7 to 24.1 s (7 runs; four of them interleaved with the new tree, which was 20.1 to 22.3 s in the same interleaving)**), and the whole solution's `dotnet test --no-build` takes **24.6 to 25.4 s (three runs; the M17a figure was 24.6 to 25.5 s)**. xUnit runs the test classes in parallel, so the wall time is that
of the slowest chain, not the sum (about 70 s of test time in all). The slowest facts in that parallel run are O13's valid clips (about 13 s), O8
(9 s), the agent fact of O11 (6 s), O14 (6 s and 4 s) and O15; alone each takes between a third and a half of that.

| Oracle | What it checks | File | Default iterations | Env var |
| --- | --- | --- | --- | --- |
| O1 | Normalizer never crashes (also on lone surrogates), is idempotent, spans and originals are well-formed; `Accents.Strip` and `SpanishPhoneticKey.Compute` are total | `NormalizerFuzzTests` | 3 000 | `VOCATERRA_FUZZ_ITERATIONS` |
| O2 | Analyzer never crashes, is deterministic (also on a fresh instance), output is well-formed | `AnalyzerFuzzTests` | 3 000 | `VOCATERRA_FUZZ_ITERATIONS` |
| O3 | Correct sentences analyze clean, match their grammar, yield their slot values; every catalog grammar is realised | `CorrectSentenceFuzzTests` | 2 000 | `VOCATERRA_FUZZ_ITERATIONS_GRAMMAR` |
| O4 | One mutation yields exactly the named tag, with the original form as correction | `MutationTagFuzzTests` | 2 000 | `VOCATERRA_FUZZ_ITERATIONS_GRAMMAR` |
| O5 | The recast closes the loop: it restores the corrected sentence and re-analyzes clean | `RecastLoopFuzzTests` | 2 000 | `VOCATERRA_FUZZ_ITERATIONS_GRAMMAR` |
| O6 | Answer matching agrees with an independent model of its documented rules, plus metamorphic relations | `AnswerMatchFuzzTests` | 3 000 | `VOCATERRA_FUZZ_ITERATIONS` |
| O7 | Intent resolver: allowed intents respected, ladder order respected, unresolved is margin 0 and confidence 0 | `IntentResolverFuzzTests` | 2 000 | `VOCATERRA_FUZZ_ITERATIONS_GRAMMAR` |
| O8 | G2P: phones in the inventory, syllables partition each word and own the phones, one stressed syllable per word, deterministic, IPA string splits back, dialects differ only at dialect sites, predicted errors are one edit away | `G2pFuzzTests` | 3 000 | `VOCATERRA_FUZZ_ITERATIONS` |
| O9 | Pronunciation scoring: perfect posteriors score 1 and tag nothing, one injected predicted error gives exactly that tag at that grapheme span, scores and confidence in [0, 1], deterministic, CTC segments ordered and disjoint | `PronunciationFuzzTests` | 1 000 (two facts) | `VOCATERRA_FUZZ_ITERATIONS_SIGNAL` |
| O10 | Verdict assembly follows its documented rules; order of unordered inputs does not matter; JSON round trip | `VerdictAssemblyFuzzTests` | 3 000 | `VOCATERRA_FUZZ_ITERATIONS` |
| O11 | Decision layer: agent step agrees with the documented rules, lands in a declared state, draws variants in range, same seed same path, defective definitions rejected with `InvalidDataException`; BKT follows its formula; FSRS stays positive, schedules ahead and orders the ratings; feedback plans follow the rule precedence; the repeat counts after one verdict (`RepeatCounts.Next`) are exactly the verdict's tags plus `not_communicated`, each one more than before (1 when new, saturating at `int.MaxValue`), and do not depend on the order of the input | `DecisionFuzzTests` | 3 000 (seven facts) | `VOCATERRA_FUZZ_ITERATIONS` |
| O12 | Every template renders clean text; only the missing-slot `TemplateException`; stable seeded selection; planner | `TemplateFuzzTests` | 2 000 (three facts) | `VOCATERRA_FUZZ_ITERATIONS_GRAMMAR` |
| O13 | Audio front-end: `WavFile.Read` throws only `InvalidDataException` / `NotSupportedException` on damaged bytes; a valid clip reads back exactly and survives write and read; `Process` agrees with the stages composed by hand (downmix, resampled length, gain, VAD slice, hash) | `AudioFrontEndFuzzTests` | 1 000 (two facts) | `VOCATERRA_FUZZ_ITERATIONS_SIGNAL` |
| O14 | Pipeline end to end: never throws, deterministic on fresh instances (and under the other recognizer scheduling), verdict and decision agree with the stages called by hand, the envelope round-trips and replays clean, changing one recorded output gives exactly that replay diff | `PipelineFuzzTests` | 300 (two facts) | `VOCATERRA_FUZZ_ITERATIONS_PIPELINE` |
| O15 | A mutated copy of `content/v1` makes the loader throw `ContentValidationException` and nothing else, naming the damaged file; a byte flip changes the bundle hash; the unmutated bundle loads | `ContentSchemaFuzzTests` | 500 | `VOCATERRA_FUZZ_ITERATIONS_CONTENT` |
| O16 | Everything that flows through `CanonicalJson` round-trips byte for byte | `SerializationRoundTripFuzzTests` | 3 000 stage records, 1 000 catalogs, 300 replay envelopes | `VOCATERRA_FUZZ_ITERATIONS`, `VOCATERRA_FUZZ_ITERATIONS_PIPELINE` |

The mutations of O4 and O5 are `conj.person`, `noun.number` (singular after a plural determiner, and plural after a singular one),
`gender.agree` (article and adjective), `ser_estar`, `word_order`, `conj.irregular`, `conj.tense` and `vocab.unknown`. O5 closes the loop for all
of them except the non-word, for which the recast must leave the words alone.

`PipelineHarnessTests` and `OracleInfrastructureTests` are small guards for the harness builders the oracles rely on and for the suite's own
helpers (failure messages, first-difference path, tolerances, the WAV builder, the corpora, the content mutation catalog).

### Where an oracle is narrower than the plan

Nothing was weakened to make a test pass. Where the plan's one-line property is not true as written, the oracle states the true property in
code comments and here, with the reason:

- **O3, O4 and O5 with the homographs of the lexicon (M18).** The generators now draw nouns that are also verb forms and nouns of both genders, and the analyzer resolves
  their part of speech. A sentence that is *correct under one reading and a mistake under another* cannot be told from either by anything in it, so the oracles stay
  strict about the properties they check and step around exactly these places. Each is a restriction in a generator (the sentence or the mutation is not made), except the first
  and the last, which relax one comparison and say so:
  - *O3 slots.* The names of the slots are compared as before; when a generated noun is also a verb form or an adjective and the grammar has an optional `[VERB]`, `[NOUN]` or
    `[ADJ]` that could take the word (`van` for `[VERB]` or `[NOUN:item]`), the matcher may bind it either way, and only the values of the slots are checked (the same
    relaxation as for two optional tokens of one class).
  - *The realizer* re-rolls a sentence (a retry, not a skip; the counters below count a recipe only when every retry failed) when a verb form that is also a noun or an adjective
    stands right behind an article, numeral, determiner, preposition or an adjective that belongs to no noun (`la baño`, `al adivinas`, `arruinado diseño`; a reader takes
    the article for a clitic or the word for a noun), when a pronoun is followed by a noun that is also a verb form (`ella libro` is `libro` the verb), and when a verb form that is
    a copula by homograph (`fue`, `fueron`: `ir` and `ser`) is followed by an adjective. The last one is a homograph between two verbs, which the resolver does not resolve.
  - *O4 and O5, `conj.tense`.* A generated verb whose present or preterite form is also a noun or an adjective (`parte`, `busca`, `oculto`, `traje`) gets the mistake only
    when a subject pronoun forces it to be the verb; elsewhere (`ayer ... intenciones parte`) a reading without the mistake exists. No literal word of the grammar may be a finite
    verb form, because the oracle names only the generated verbs.
  - *O4 and O5, `gender.agree`.* Nouns that read as both genders (`estudiante`) are not given a gender mistake; the new form of an adjective must be an adjective and nothing else
    (`alguno` is also a pronoun), and must not agree with the word before it (`lata buena`: the adjective is read as that noun's).
  - *O4 and O5, `ser_estar`.* A copula form that is also a noun (`son`) is the mutation only right behind a subject pronoun.
  - *O4 and O5, `word_order`.* An adjective is not moved in front of a noun when the word before it, or before the adjectives before it, can be a noun (`estrella grande violenta
    pieza`: the adjectives belong to `estrella`); when its spelling is an adverb that goes before nouns (`incluso resultado`); or when it is also a pronoun or adverb and the
    noun can be a verb (`suyo peso`).
  - *O4 and O5, tied parses.* When the analyzer's answer differs from the mutation's and the grammar itself reads a word of the sentence in two ways (`duchas complicados`: `[VERB]` or
    `[NOUN:item]`, and the word keeps both readings), the case is excused and counted (`KnownTolerances.IsTiedParse`, `excused:tied-parse:<kind>`); the oracles assert that the
    excused cases stay under one in a hundred. In the runs made it did not occur at default depth.
- **O8.** The dialect sites are `θ s z h ʎ ʝ ʃ ʒ` (the plan lists `θ s ʎ ʝ h`; the shipped dialects also produce the voiced `z` and the rioplatense `ʃ ʒ`).
  `Tokenize(ToIpaString())` is exact except for the pair `t` + `ʃ`, whose concatenation is the affricate token `tʃ` (the inventory documents that
  sequences are lists for this reason); a result with that pair skips the split.
- **O9.** "Viterbi segments partition frames" is checked as: one non-empty segment per phone, in label order, pairwise disjoint (two equal
  neighbours need a blank frame between), inside the matrix, with a finite log-likelihood; blank frames belong to no segment, so the segments do
  not tile the matrix. The alignment is null exactly when the target needs more frames than exist. "Exactly the injected tag" is asserted when the
  scorer explained the speech with the dialect variant the learner started from (an error can sit closer to another dialect's variant, for example an
  inserted `h` next to an aspirated `s`; the skip is counted as `unclaimed:another-dialect-variant-chosen` and did not occur in the runs made).
  In `Gop` mode an extra phone does not lower the word score (it is tagged, not scored: `hola` with an inserted `h` scores 1 for every phone and
  carries `pron.silent_h`; `Greedy` takes 0.5 off the neighbour), so the substitution claims are limited to substituted phones.
- **O11.** BKT: with `slip + guess` safely below 1 a correct answer never lowers P(known) and the posterior of a wrong answer never raises it,
  but the learning step that follows adds `(1 - posterior) * learn`, so the full step after a wrong answer can rise when P(known) is low (0.1
  becomes 0.112 with the default parameters). That is the model, so the monotone claim is made for the posterior, and the full step is checked
  against the formula to 1e-12. FSRS: `Hard < Good < Easy` is strict for a reviewed card (up to the maximum interval, where they may tie); a new card
  has no learning steps, so Again, Hard and Good tie at the one-day floor and only `Hard <= Good <= Easy` holds.
- **O13.** "Samples finite" is vacuous for 16-bit integers; the clip hash is checked to be the hash of the samples instead. A damaged file whose
  header says a sample rate outside 1 kHz to 192 kHz is read (the reader's contract holds) but not run through the front-end (see the resampler
  finding below).
- **O14.** "Mutating any envelope field gives a diff naming that path" holds for the fields the replayer compares: the record (`/clipHash`,
  `/versions/...`, `/verdict/...`, `/hostStateHash`) and the downstream parts of the result (`/decision`, `/response`, `/effects`, `/repair`); one
  changed value gives exactly one reported pointer. The fields it reads back as inputs (the asr and phoneme recordings, the conversation context, the prompt
  id and the phoneme model hash of the record) change the replayed result instead of being compared with it, and the copies `result/verdict` and
  `result/replay` are not compared at all (known findings below).
- **O15.** "Every mutation makes the loader throw" holds for every mutation of a value the loader must read: a dangling reference, an unknown
  enumeration value, an empty required string, a duplicated id and a flipped byte are never accepted. (Since M19a an effect's `kind` is an opaque
  outcome kind, not an enumeration, so it is no longer among the enumerated fields: an unknown kind is a valid kind. An empty kind is refused by
  the effect table, and M19b added that case to O15: `EmptyRequiredString` empties an effect's `effectId`, `intent` or `kind` and a prompt's
  `promptId`, and `AnEmptyRequiredString_IsRefused_NamingTheDamagedFile` checks each of the four fields by name as well.)
  Dropping or retyping a field the loader does not require may leave a
  loadable bundle; `KnownTolerances.AcceptedContentField` lists those places (optional by design, or a silent default; known findings below) and an
  accepted mutation anywhere else fails. "Naming the mutated path" holds for those four kinds; removing or retyping a definition (an intent's
  grammars, an answer set's id) is reported in the file that uses it, so for those kinds any bundle file may be named.

## Pushing even deeper for a one-off soak

Each oracle reads its iteration count from the environment variable above when it is set to a positive integer. The `fuzz-soak` job of the CI
workflow runs exactly this, weekly and on demand:

```bash
VOCATERRA_FUZZ_ITERATIONS=50000 VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=50000 VOCATERRA_FUZZ_ITERATIONS_SIGNAL=10000 \
VOCATERRA_FUZZ_ITERATIONS_PIPELINE=3000 VOCATERRA_FUZZ_ITERATIONS_CONTENT=5000 dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests
```

On the development machine (4 cores) that soak takes **about 3 minutes** (176 s wall, all 49 tests green; the slowest facts are O13 at 2 min 17 s
and O15 at 1 min 33 s). The CI job has a 60-minute limit and the counts are within the 30-minute target by a wide margin, so they were not lowered.
The signal oracles are ten times their default rather than fifty because the audio and pronunciation facts are the ones whose cost per case is
largest.

## Reproducing a failure

CsCheck picks a fresh random seed on every run and prints it on failure:

```
Set seed: "6VTk-wxfywU1" or -e CsCheck_Seed=6VTk-wxfywU1 to reproduce (3 shrinks, 1,519 skipped, 2,000 total).
```

Re-running with `CsCheck_Seed=<seed>` in the environment replays the failing case. The assertion message always carries the generated input
so the case can be pasted into the owning module's test project as a permanent regression `[Fact]` once the bug is fixed:

```
[O3] A correct sentence produced grammar errors: gender.agree@0+6 expected 'buenas' actual 'buenos'.
Input: "pattern=[(buenos|buenas) (días|tardes|noches)] text=\"buenos noches\" normalized=\"buenos noches\" ..."
```

A sentence input prints its grammar pattern, the written text and the normalized text; a verdict input prints the canonical JSON of the whole
`VerdictInputs`; a text input prints the string as a JSON literal; an audio input prints the generator parameters and the SHA-256 of the samples
(never the samples); a content mutation prints the kind, the file and the JSON path. Determinism failures also name the first differing JSON path.

All `Sample` calls run with `threads: 1`, so a determinism oracle compares two runs on the same footing. xUnit still runs the test classes in
parallel; the shared fixtures (`PipelineHarness.Shared`, `ContentMutationGen.Shared`) hold only immutable data and thread-safe lazy indexes. The
content oracle writes each mutated bundle to its own directory under the system temporary folder (`vocaterra-fuzz-content-<pid>-<n>`) and deletes it.

## Layout

- `FuzzOptions.cs` - iteration defaults and env-var overrides; the 3-in-4 benign, 1-in-4 adversarial weights.
- `Generation/`
  - `AdversarialCorpus` - fixed edge values: strings (empty, 10 000 characters, emoji, combining marks, lone surrogates, number words, `{slot}`, `null`, `al`, `del`), audio shapes (zero and one sample, exactly one VAD frame and one sample either side, out-of-phase stereo, DC offset, a long clip at 96 kHz), phone sequences (one phone, twenty equal phones, every vowel, the learner phones), JSON values for a changed kind.
  - `LexiconCatalog` - the lexicon through the generators' eyes: nouns with both numbers, verbs with full paradigms, descriptive adjectives, the closed classes. With
    thousands of lemmas it also decides which words the generators cannot spell correctly (a noun that is also an article, numeral or determiner, whose form two noun lemmas
    share, that has no noun reading of the generated form, or that the normalizer rewrites, like the noun `cero`), flags the nouns that are also a verb form or a noun of the other
    gender (they are drawn like any other; the flags say which mistakes cannot be built on them), indexes the lexicon for the generators, and prints the counts as
    `nouns:`, `verbs:`, `adjectives:` ... diagnostics of O3.
  - `SpanishTextGen` / `TextNoise` - lexicon-driven phrases (word salad and loosely structured phrases) with casing, accent, NFD, `¿¡?!,.`, spacing, number-word and adversarial-token noise. The adversarial token goes in last and verbatim, so a lone surrogate reaches the stage under test unrepaired.
  - `GrammarPattern`, `PatternGen` - slot grammar patterns as a tree: every catalog grammar, structural skeletons, and random DSL strings built from the token kinds, including the name token `[WORD:slot]` (M17b).
  - `SentenceGen`, `SentenceRealizer`, `SentenceModel` - correct sentences from a pattern, agreement right by construction (see below). A name (`[WORD]`) is two or three invented syllables drawn from the seeded generator, written with a capital and normalized in lower case (`TokenRole.Name`).
  - `UnboundNames` (in `Oracles/`) - the `vocab.unknown` errors that the names of a sentence must get when its grammar does not bind them (see below).
  - `MutationGen` - exactly one mutation of a correct sentence, with the errors the analyzer must report and the sentence the recast must restore.
  - `SlotMapGen` - slot maps derived from the placeholders and filters of the real templates.
  - `VerdictGen` - random stage results within the contract ranges, including exact-threshold confidences.
  - `AudioGen` - `WavData` at 8 000 to 96 000 Hz, mono and stereo, PCM16 and float32, three header layouts (canonical, odd-sized extra chunks, `WAVE_FORMAT_EXTENSIBLE`), 0 ms to 5 s, from silence to clipped noise; white noise is drawn from the CsCheck generator; byte damage (truncate, corrupt a header byte, wrong chunk size) of valid files. `WavBuilder` writes the files independently of `WavFile.Write`.
  - `PhoneGen` - phone sequences of one to twenty inventory phones and phrases from the lexicon, posteriors through `FakePhonemeRecognizer.FromPhones` and `FromMumbled`, confusions from the predicted-error table (`ɾ→ɹ`, `r→ɹ`, `a→ə`, `e→eɪ`, `o→oʊ`, an inserted `h`), frame layouts, mumbled frames, dialects.
  - `DecisionGen` - agent definitions (the bundle's, random valid ones, valid ones with one defect), BKT parameters, FSRS review scripts, feedback policies, repeat counts.
  - `ContentMutationGen` - walks the JSON files of `content/v1` and applies exactly one mutation: remove a property, change a value's JSON kind, point a reference at a fresh id, give an enumerated field an unknown value, empty a required non-empty string (`EmptyRequiredString`: an effect's `effectId`, `intent` or `kind`, a prompt's `promptId`), duplicate an element that has an id, flip a byte without touching the manifest. The manifest is recomputed for every kind but the byte flip (the committed manifest with the entry of the one mutated file replaced), so the loader's schema and reference checks run and not only its hash check. Files over 1 MB (the imported lexicon) are not walked for structural mutations, because they have the schema of the small lexicon files and writing them back costs more than everything else; a flipped byte still reaches them.
  - `PipelineGen` - one pipeline run: a transcript from a correct sentence of the prompt's own grammars, a mutated sentence, an answer, free text or an adversarial string, plus prompt, agent, state, counter, seeds, learner, repeat counts and options.
- `Oracles/` - `PipelineHarness` (bundle plus fakes wired once, with builders for services, pipeline, `ConversationContext`, prompts), `KnownTolerances`, `Determinism`.
- `*FuzzTests.cs` - one file per oracle.

### How the sentence generator stays honest

`SentenceRealizer` mirrors the analyzer's own scans (noun phrase = determiners, adjuncts, a noun head, adjuncts; a subject pronoun pairs with
the next verb after at most two adverbs or clitics) and builds each sentence so that articles, numerals and adjectives agree with their noun,
verbs agree with their subject pronoun, adjectives sit on the right side of the noun, copulas meet an adjective of their class, and no past
marker sits next to a present verb. It is a model of the analyzer, so the oracles are an agreement check between two implementations. A
pattern that cannot be realised correctly is **skipped and counted**, never silently weakened; each oracle prints its generator
diagnostics and asserts a minimum of realised sentences, and `MutationTagFuzzTests` and `RecastLoopFuzzTests` assert that every mutation
kind stays reachable. `CorrectSentenceFuzzTests` also asserts that every grammar of the catalog is realised at least once and that no literal
word of any grammar is missing from the lexicon (the old `cuál` gap would fail it). If a mirrored table in `LexiconCatalog` (the ser/estar
adjective lists, the prenominal adjectives, the past-time adverbs, ...) drifts from `AnalysisTables` in `Vocaterra.Nlp.Language`, the mutation oracles fail and
name the kind.

Homographs are generated, not avoided (M18): `SentenceGen` counts the sentences that hold a noun that is also a verb form, a verb form that is also a noun and a noun of both genders
(`homograph:sentence-with-...`), and O3 asserts that each is at least one sentence in fifty (about 17 %, 6 % and 6 % of the sentences in a default run), so a generator that quietly went back to the
plain words would fail. The few placements the analyzer reads differently on purpose are re-rolled (see the list above); a recipe that fails every retry is counted as
`skip:verb-form-homograph` or `skip:function-word-homograph`, and O3, O4 and O5 assert that these are at most one recipe in two hundred (a default run has 0 to 1, against 1, 6
and 3 before M18).

A name (M17b) has no reading the checks use: the realiser treats it like an unknown word, so it breaks every phrase and every subject-verb link around it, and
the analyzer does the same (a bound name keeps no reading at all). `CorrectSentenceFuzzTests` and `SerializationRoundTripFuzzTests` (`NameSentences_SurviveCanonicalJson`)
realise the catalog grammars that carry a name. O4 and O5 analyze a mutated sentence with its own grammar, and a mutation such as a leading `ayer` breaks that match: the
name is then a word the lexicon lacks, which the analyzer reports as `vocab.unknown`. `UnboundNames.Expect` predicts exactly those errors, and O4 and O5 require them
(missing or extra is a failure). A case in which the unbound name is a word the lexicon knows is not compared at all, because its checks would then apply to it; the
counter `unpredicted:name-known-to-lexicon` says how many, and a default run has none.

Slots are compared strictly unless the pattern is ambiguous (two optional tokens that can match the same word, `[DET]` against `[ART]` or
`[ADJ]`, `un` and `una` as article or numeral, a literal word that is also a noun): then the matcher may legitimately bind the words in more
than one way and only the extracted values are checked against the words of the sentence.

### How the stage-2 generators stay honest

Every stage-2 oracle prints diagnostics and asserts a minimum of each situation it claims to cover (every signal kind and sample rate, both VAD
outcomes, both reader outcomes for every kind of byte damage, every kind of injected pronunciation error, agent steps that draw a variant and that
do not, plans of every size, every mutation kind, the pipeline with and without phonemes, with and without an agent step, with and without a repair
step). The minimums scale down when a smaller iteration count is requested.

## Bounds the suite needs

- **Pronunciation scoring.** Pronunciation scoring costs roughly the cube of the phone count, so the oracles bound it: O9 scores inventory
  sequences of 1 to 20 phones and phrases of at most 40 phones (`PhoneGen.MaxSequenceLength`, `MaxSpokenPhones`); O14 and the envelope round trip of
  O16 pair a transcript with the phoneme recognizer only when it has at most 150 characters (`PipelineCase.MaxScoredTranscriptCharacters`, 109
  phones of `hola amigo ...`, 25 ms to score) and feed at most 2 000 characters otherwise (`MaxTranscriptCharacters`; the adversarial corpus has strings of 10 000). The measured
  curve, `dotnet test --filter CostCurve` with `VOCATERRA_FUZZ_COST_CURVE=1`, text `hola amigo ...`, `Gop` mode, one dialect variant, in milliseconds:

  | Characters | Phones | `PredictedErrorsFor` | `Score` (Gop) | `PhoneAligner.Align` for every predicted variant | `CtcForcedAligner.Align` |
  | --- | --- | --- | --- | --- | --- |
  | 50 | 36 | 0.3 | 1.7 | 5.4 | 0.2 |
  | 100 | 72 | 1.7 | 8.6 | 11.4 | 0.5 |
  | 200 | 145 | 19 | 118 | 113 | 2.0 |
  | 400 | 291 | 68 | 384 | 573 | 6.3 |
  | 800 | 582 | 494 | 2 768 | 4 313 | 23.5 |

  Doubling the phones multiplies `Score` by 7 (2.85 power), the aligner loop by 7.5 and the Viterbi alignment by 3.7 (quadratic, negligible). The
  function that is super-linear is `PredictedErrorMatcher.Match` (called by `ResultAssembler.Assemble` in both scorers): it runs a full
  `PhoneAligner.Align` (an `n * m` edit-distance table) against the reference for every predicted-error variant, and the number of variants is about
  the number of phones. A predicted variant is by construction at most one edit from the reference, so a banded or diagonal-first alignment would
  make it linear per variant. `PronunciationScorer.PredictedErrorsFor` is cubic too (a `List.Contains` over variants compared phone by phone), with a
  smaller constant. Neither is rewritten here; see the known findings.
- **Audio.** `AudioFrontEnd.Process` costs 8 to 12 ms for 100 ms of audio at 11 025, 22 050 and 44 100 Hz (the resampler rebuilds its kernel bank
  on every call) and 0.4 to 1.8 ms at the other rates, about 150 ns per input sample on top, so the clip durations are weighted towards short clips
  (most under 60 ms, a tail up to 5 s) and the 96 kHz edge shape is 2 s.
- **Content.** A load of the bundle took about 12 ms, so 500 mutations cost about 6 s; with the imported lexicon a load takes 110 to 270 ms (4.7 MB, 41 000 entries), which
  would be a minute and a half for O15. The oracle therefore loads with an opt-in `LexiconCache` (a copy whose lexicon files are byte for byte those of an earlier valid copy
  reuses the parsed and merged lexicon; any other lexicon file is parsed in full), derives each copy's manifest from the committed one, and runs on up to four threads; it
  takes 10 to 17 s alone.

## Bugs found and fixed while building this suite

- **A name spelled like an irregular verb form was reported as `conj.irregular`** (`Vocaterra.Nlp.Language`, M17b). The regularization of an irregular
  form ran before the grammar matched, so a `[WORD]` name such as `sona` (the regularized form of `suena`) was a verb mistake. A name now carries no
  reading and no error. The same finding showed a second defect, which O5 found with seed `bum3moYdGVI1`: a name that is a lexicon verb form (`vivá`)
  kept its readings and was conjugated by a recast. Names carry no reading any more; the gap clamp of the embedding rung is the O7 finding (seed
  `5lSWF-U_rpTa`, a raw gap of 1.005 between `farewell` and `sell_item`). Found by O3 (CsCheck seed `1_BGsh0nh9k4`). Regression: `WordTokenAnalysisTests.ANameSpelledLikeAnIrregularVerbFormIsNotAnIrregularVerb`
  (`soy sabo`, the seed's irregular spelling of `sé`).
- **`RecastBuilder` picked the gender of `un`/`una` from the word after the numeral, not from the noun** (`Vocaterra.Nlp.Response`, stage 1). `1 grande queso`
  was recast as `una grande queso`, because the invariant adjective `grande` has a feminine reading that came first. The builder now looks
  through the adjectives to the noun the numeral quantifies and falls back to the next word only when there is no noun. Found by O5
  (CsCheck seed `2PhTP0PVr4W1`, input `ellos compran taberna 1 grande queso`). Regression: `RecastBuilderTests.DigitsAreSpelledOutBeforeSpeaking`
  (`tengo 1 grande queso`, `tengo 1 buena espada`) in `tests/Vocaterra.Nlp.Response.Tests`.
- **`Accents.Strip` and `SpanishPhoneticKey.Compute` threw `ArgumentException` on a lone surrogate** (`Vocaterra.Nlp.Language`; the stage-1 known finding about
  ill-formed UTF-16). Both reached `string.Normalize`, which rejects ill-formed UTF-16, and a recognizer or a keyboard can deliver one. An internal
  `Utf16.ReplaceLoneSurrogates` now replaces each lone surrogate with U+FFFD at the entry of `TextNormalizer.Normalize` (which already treated it as
  a separator), `Accents.Strip` and `SpanishPhoneticKey.Compute`; valid pairs are untouched. The stage-1 generator guard is gone: the noise
  generator now splices the adversarial token in last and verbatim, and O1 feeds the raw text to all three entry points. Found by O1 (seed
  `cA-a9vcyA8R1`). Regression: `IllFormedUtf16Tests` in `tests/Vocaterra.Nlp.Language.Tests/Normalization`.
- **`SpanishG2p.Phonemize` threw `ArgumentException` on a lone surrogate** (`Vocaterra.Nlp.Phonology`). `G2pNormalizer` handed the text to `string.Normalize`
  as well; it now reads a lone surrogate as a word separator, like any other non-letter. Found by O8 (seed `4Y4ZiHH2bn31`). Regression:
  `IllFormedUtf16Tests` in `tests/Vocaterra.Nlp.Phonology.Tests`.
- **A lexicon entry without a `features` property, and a lexicon file without `entries`, made the content loader throw `NullReferenceException`**
  (`Vocaterra.Nlp.Language`). A missing property arrives as null although the records are non-nullable (`RespectNullableAnnotations` only rejects an explicit
  null); `LexiconSchema.Validate` dereferenced `Features` and `Lexicon.Load` enumerated `Entries`, so a damaged `lexicon/*.json` escaped the loader's
  `ContentValidationException` as an unhandled `NullReferenceException`. Both now fail with a `FormatException` that the loader reports as a
  problem naming the file. Found by O15 (seed `0003TzidR6n1`, a flipped bit in a key of `lexicon/seed.json`). Regression: four new cases of
  `LexiconTests.InvalidDocumentsAreRejected` in `tests/Vocaterra.Nlp.Language.Tests`.

Found when the imported lexicon was added (M17a). The seeds belong to the lexicon of that moment and do not reproduce with the final one; the regression tests do.

- **A subject pronoun was checked against the imperative readings of its verb** (`Vocaterra.Nlp.Language`, O4/O5). Imperatives are spelled like the third person
  (`habla`, `come`, `está`), so `tú habla` agreed with `tú`, and the correction of `ella está alta` was `eres` (the first reading of `está` is the imperative
  of `estar`, second person). The person check and the copula check now use the declarative readings. Found by O4 (seed `dU3ctY0Mvpx9`, a ser/estar mutation) and
  O5 (seed `4ybQyIqiaD61`). Regression: `AmbiguousReadingTests.AnImperativeThatIsSpelledLikeTheThirdPersonDoesNotExcuseAPersonMistake` and
  `TheExpectedCopulaFormComesFromTheDeclarativeReadingNotFromTheImperative` in `tests/Vocaterra.Nlp.Language.Tests/Analysis`.
- **A form that is an adjective under two lemmas was classified by whichever sorts first** (`Vocaterra.Nlp.Language`, O5). `ocupada` is a lemma of its own ("pregnant") next to
  `ocupado`; `ella es ocupada` passed the ser/estar check because the first reading's lemma is not in the estar class. The ser/estar class and the prenominal
  check now look at every adjective reading. Found by O5 (seed `8Wkp2ivvMDP1`). Regression: `AFormThatIsAnAdjectiveUnderTwoLemmasIsClassifiedByAllOfThem`.
- **A noun form that reads as singular and as plural made a correct adjective a number mistake** (`Vocaterra.Nlp.Language`, O3). `corazones` is the plural of `corazón` and the card
  suit; `malos corazones` was tagged `noun.number` because the first noun reading is the singular. Without a determiner that fixes the number, an adjective now
  agrees with either reading. Found by O3 (seed `dYkkd05aEvg7`). Regression: `ANounFormThatIsSingularAndPluralAgreesWithAnAdjectiveOfEitherNumber` and
  `ADeterminerStillFixesTheNumberOfAnAmbiguousNounPhrase`.
- **`Utf8JsonReader.GetString` throws `InvalidOperationException` for invalid UTF-8**; the new lexicon reader turns it into a `JsonException` (caught by the
  tests `LexiconFileReaderTests.InvalidUtf8IsAJsonException`).

Found and fixed in M18 (the seeds belong to the code of that moment and do not reproduce with the final resolver; the regression tests do):

- **The analyzer's checks read every word as every part of speech it can be** (`Vocaterra.Nlp.Language`, the M16a and M17a findings: 21 % of the usable nouns are also a verb form). `ayer compré un libro`
  was `conj.tense` (`libro` is `librar` in the first person), `yo lanza` passed or failed by the order of the readings, `cuánto cuesta la espada` was `gender.agree` (`cuánto` the adjective
  agreeing with `cuesta` the noun), `tú ayudas` and `las ayudas` could not both be right. `ReadingResolver` now narrows each word to one reading from local syntax, the slot grammar and, last,
  the reading with the fewest errors, and every check runs on that reading (design in [docs/PIPELINE.md](../../docs/PIPELINE.md#4-linguistic-analysis-d0)). Regression:
  `HomographSentencesTests` (47 correct sentences, 20 more under the grammars of the bundle, 26 incorrect ones that keep their mistake, the `cuánto cuesta` family) and `ReadingResolverTests` in `tests/Vocaterra.Nlp.Language.Tests/Analysis`, and the
  matcher bindings in `SlotGrammarTests`. With the CLI (`eval --transcript ... --prompt buy-item-open`) `ayer compré un libro` went from correctness 0.75 and `conj.tense` (`libro` -> `libré`) to
  correctness 1 and no errors, and so do the 19 homograph sentences checked on both `buy-item-open` and `offer-items-open` (`quiero comprar una lanza`, `ayer compré una lanza`,
  `el vino es bueno`, `él vino ayer`, `tú ayudas`, `nosotros comemos como siempre`, ...); `cuánto cuesta la espada`, `el escudo`, `la poción` and `la lanza` on `ask-price-open` are correctness 1
  again (they were 0.85 with `gender.agree` `cuánto` -> `cuánta` after M17a), `cuánto cuesta el espada` is still `gender.agree` on `el`, and `yo tiene dos espada` is still exactly `conj.person`
  and `noun.number`. **Numbers**, from the counters of O3, O4 and O5 and `lexicon-report` (the M17a column is the table that stood in "Known, deliberately not fixed"):

  | | Before the lexicon | M17a | M18 |
  | --- | --- | --- | --- |
  | Forms of the merged lexicon with more than one reading | 89 of 1 179 | 7 216 of 32 494 | same |
  | Forms read under more than one lemma | 19 | 1 035 | same |
  | Nouns the generators can use / also a verb form | 154 / 3 | 1 583 / 339 (21 %) | 1 880 / 379 (20 %) |
  | ... also a noun of the other gender | left out (3) | left out (347) | used (136) |
  | Nouns left out: form shared with another noun lemma / no distinct plural / reads as article, numeral or determiner / no noun reading of the generated form | 0 / 5 / 0 / n/a | 61 / 16 / 2 / n/a | 94 / 16 / 2 / 17 |
  | Sentences skipped as `verb-form-homograph` in one run of O3, O4, O5 | none | 1, 6, 3 | 0, 1, 0 |
  | Sentences skipped as `function-word-homograph` | none | none | none |
  | Sentences of an O3 run with a noun that is also a verb form / a verb form that is also a noun / a noun of both genders (of about 1 980) | n/a | not counted (a verb-form noun was drawn in 15 % of the positions after a determiner, never elsewhere) | 330 / 121 / 117 |
  | Verbs / descriptive adjectives / adverbs the generators can use | 34 / 62 / 30 | 677 / 250 / 105 | same |
- Found by the oracles while the resolver was built, each now a rule or a test: a slot grammar accepts a modifier where a noun is needed (`falso aliado dije` matches `[ADJ] [NOUN]` as `[ADJ] [ADJ]`),
  so the neighbouring words take precedence over a grammar binding; the lexicon lists `sus` as an adjective of its own and as an interjection next to the possessive `su`, which made `alquileres sus
  dispuestos extraños` a list of adjuncts of `alquileres`; `mucho espada` and `temprano pasillo` were read with the adverb, which does not modify a noun, and the mistake disappeared; a past-time
  adverb with no verb must cost more than a tense mistake (`ayer canto una canción`) but a sentence of nouns and adjectives with no adverb of the past must cost nothing (`temperaturas relacionadas
  saque logrado colega` was read with `saque` as a verb); a literal spelled like the word (`muestra`, `lanza`) says nothing about its reading; two optional elements that tie for a word leave it both
  readings. The first draft that applied grammar bindings first hid word-order mistakes in O5 and was reversed.

Generator fixes (not analyzer bugs, found by the same runs): the generators now skip words the normalizer rewrites (`cero`, `mil`, `diez` are nouns in the imported lexicon and
become digits), nouns that are articles, numerals or determiners (`poca`), forms two noun lemmas share (`ratos`), nouns that are also an infinitive (`proceder`), and
a function word that is also a verb form or a noun (counted as `skip:function-word-homograph`; none occurred in the final runs).

Content fix (stage 1 follow-up): **`cuál` is now in `content/v1/lexicon/seed.json`** (a pronoun, like `qué` and `quién`), so the correct
`ask_price` utterance `cuál es el precio de ...` no longer gets `vocab.unknown`. The bundle was re-hashed (`b076558f...efb`), the pinned hash in
`ManifestTests` and the three `contentHash` lines of the end-to-end golden file were updated (the committed golden manifest is untouched and stays
synthetic), and O3 now realises the `ask_price` grammar (about 30 sentences per run).

## Known, deliberately not fixed

Findings that are content gaps or design limits rather than defects of one stage. None changes behaviour here; the generators step around
them explicitly (each is a skip or a restriction in the generator, counted in the diagnostics) and the oracles stay strict.

- **What the part-of-speech resolver does not do (M18).** Two verbs that share a form are one class: `fue` and `fueron` are `ir` and `ser`, so a copula check still sees the copula, and
  the generators re-roll `ir` forms followed by an adjective (`ella fue alta`). A sentence that is a mistake under one reading and correct under another, with nothing in the neighbouring
  words or the grammar to prefer either, is read the charitable way (`ayer ... intenciones parte`, where `parte` is a noun or a verb).
  The closed classes of the generators (adverbs, prepositions, conjunctions, interjections) still leave out words that are also a verb form or a noun (`como`, `para`); the resolver reads
  `nosotros como` as the verb and `nosotros comemos como siempre` as the conjunction, and both are covered by `HomographSentencesTests`, not by the generators. Nouns whose form two noun
  lemmas share (`ratos`: `rato`, `rata`) stay out of the pools (94 lemmas), because the correction the analyzer names depends on which lemma it takes.
- **`CanonicalJson` writes a lone surrogate as an escaped U+FFFD**, and a real U+FFFD as the character itself, so a transcript with a lone
  surrogate does not survive serialize, deserialize, serialize byte for byte, and a replay envelope for it does not round-trip. Recognizers return
  well-formed text. O16 and O14 make transcripts well-formed before the JSON round trip (O14 still runs the pipeline and replays the in-memory
  envelope for the raw text). `Vocaterra.Nlp.Core` is final, so the fix (write U+FFFD as an escape, or reject at serialization) is a maintainers' decision.
- **Measurements with the imported lexicon (M17a).** Lexicon file `wiktionary-es.json`: 4 718 227 bytes (limit 15 MB); first load of the bundle with hash verification in a
  fresh process 217 to 267 ms, repeated in one process 114 to 213 ms (before: 8 ms); fuzz project alone 18.7 to 20.3 s (before 17.1 to 18.2 s); whole solution 24.6 to 25.5 s
  (before 20.5 to 21.0 s). The same numbers at 5 000 lemmas (6.6 MB, first load 333 to 373 ms) are in [docs/LEXICON.md](../../docs/LEXICON.md); 5 000 fails the 300 ms load gate.
  Scaling traps found on the way: building a fresh `MorphologyGenerator` costs 7 ms with this lexicon (it indexes every irregular verb), which is now done once per lexicon;
  the non-word generator compared each candidate with 10 000 forms; a linear scan per sentence in the realizer.
- **Pronunciation scoring grows with the cube of the phone count** (table above). Follow-up: make `PredictedErrorMatcher.Match` compare a predicted
  variant with the reference in linear time (they are at most one edit apart) and make `PronunciationScorer.PredictedErrorsFor` deduplicate with a
  hash set. A spoken utterance is far below the sizes where it matters (110 phones cost 15 ms), so nothing user-visible is affected.
- **The resampler rebuilds its kernel bank on every call, and the front-end trusts any positive sample rate.** At 11 025, 22 050 and 44 100 Hz a
  call costs 8 to 12 ms for 100 ms of audio (0.4 to 1.8 ms at the other rates) because most of it is building the polyphase kernels. `WavFile.Read`
  accepts any positive rate and `AudioFrontEnd.Process` follows it: a header that says 1 Hz turns 3 samples into 48 000 (the output grows 16 000
  times, so 200 000 samples throw `OverflowException` from the checked length, and fewer exhaust memory), and one that says 2 000 000 000 Hz makes a
  200 000-sample clip build a single 6.7-million-tap kernel for one output sample (2.1 s). A real device does not report either, so the follow-up is a
  policy: caching the bank per rate and rejecting implausible rates (the bounds are for the maintainers to pick) at the reader or in the front-end.
  O13 reads such files (the reader's contract holds) but processes an accepted damaged file only for rates from 1 kHz to 192 kHz.
- **A removed or retyped number, flag or enumeration in a content file loads without a message** (`System.Text.Json` binds a missing value-typed
  constructor parameter to 0, false or the first enumeration member). The loader validates structure and references, not the presence of
  value-typed fields. O15 found this for `prompts.json` (`mode`, `difficulty`: a prompt silently becomes Constrained and Beginner),
  `evaluation-config.json` (`communicatedConfidenceThreshold`, `correctnessThreshold`, `pronunciationThreshold`, `epsilon`, `unknownTagWeight`:
  a missing threshold is 0), `feedback-policy.json` (`rules[].type`: Recast) and `effects.json` (`curve[].correctness` and `value`: 0). They are
  the `SilentDefault` rows of `KnownTolerances.AcceptedContentField`. The fix is `[JsonRequired]` on the value-typed record parameters of the models
  (`PromptSpec` is in `Vocaterra.Nlp.Core`) or a presence check in the loader; both touch every model type.
- **The replayer compares the record and the downstream parts of the result, and nothing else.** The copies `result/verdict` and `result/replay`
  of the envelope duplicate the record and are never compared, so a log edited there replays clean; the record's `promptId` and its phoneme model
  hash are read back to build the replay, so the replay agrees with them by construction; the embedding model and the synthesizer are those of the
  pipeline that replays, so replaying with another embedder reports the changed `/versions/modelHashes/embedding` instead of replaying silently (the
  right behaviour, and why O14's replay pipeline is built with the recording's embedder setting). A tampered log is outside the threat model;
  checking that `result/replay` equals `record` when the log is read would close the first gap.
- **Optional pieces of content are optional by design:** the `answer_accepted` and `feedback.*` dialogue pools (the pipeline falls back to the
  generic pool or to no feedback line), answer aliases and dialects, lexicon features, intent examples and keyword rules, agent `fallback`,
  `hostEvent`, `variantPoolId` and the `communicated`/`intent` conditions, the per-tag error weights, provenance approvals. Removing one loads
  (`Optional` rows of the same list).
- **A numeral next to `un`/`una`/`uno` is one number when spoken** (`cuatrocientos una` is 401, `una mil` is 1000), and two numerals in
  a row recast as `un una`. These are properties of the language, so the sentence generator does not produce them (`skip:numeral-next-to-article`,
  `skip:adjacent-numerals`).
- `AgentDefinition` is a plain record over lists, so it has no value equality; O16 compares its JSON only.

## Adding to the suite

`PipelineHarness` exposes what an oracle needs: `Bundle`, `NewServices` (scripted word recognizer, optional fake phoneme recognizer, fake embedder,
silent synthesizer), `NewPipeline`, `NewContext`, `Prompt`, `Clip`, `Words`, `Grammars`. A new oracle gets its own `*FuzzTests.cs`, a generator under
`Generation/` if the existing ones do not fit, an entry in `KnownTolerances` for every exception it waves through (with the reason), an iteration
default in `FuzzOptions` and a row in the table above. A bug it finds is fixed in the owning module with a regression test there, and listed above
with the CsCheck seed.
