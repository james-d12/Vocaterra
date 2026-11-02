# Vocaterra.Nlp.Fuzzing.Tests

Combinatorial, property-based fuzz testing for the voice pipeline, modelled on `CodeGuard.RuleFuzzing.Tests`. The design is
`docs/nlp/FUZZING_PLAN.md` (generators, oracles O1 to O16, tolerances, iteration budgets). In short: every generator walks **our own
catalogs** (the lexicon, the `IntentCatalog` grammars, `ErrorTags.All`, the `DialogueCatalog` templates, the `AnswerSet`s and the JSON files
of `content/game-v1`, `PhoneInventory`, `DialectConfig.AllShipped`, the agent definitions), mixes benign and adversarial inputs 3 to 1, and runs them
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
| O3 | Correct sentences analyze clean, match their grammar, yield their slot values; every catalog grammar is realised (one the sample left out from fixed recipes, M71) | `CorrectSentenceFuzzTests` | 2 000 | `VOCATERRA_FUZZ_ITERATIONS_GRAMMAR` |
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
| O14 | Pipeline end to end: never throws, deterministic on fresh instances (and under the other recognizer scheduling), verdict and decision agree with the stages called by hand, the envelope round-trips and replays clean, changing one recorded output gives exactly that replay diff; about half the cases also run as `Analyze` on one pipeline and `Complete` (host facts given to phase 2) on another and must equal `Evaluate` with the recognizer called once (M28a, CR-017); about a third first take a host-initiated turn (`Advance`, in a copy of the bundle whose agents handle `fuzz.ask` in every state), which must equal the machine step with the counter advanced, leave learner and counts alone, replay clean and be skipped by `Read`/`ReadEnvelopes` (M28a, CR-015) | `PipelineFuzzTests` | 300 (two facts) | `VOCATERRA_FUZZ_ITERATIONS_PIPELINE` |
| O15 | A mutated copy of `content/game-v1` makes the loader throw `ContentValidationException` and nothing else, naming the damaged file; a byte flip changes the bundle hash; the unmutated bundle loads; (M32) a manifest `language` is hashed only when present, an unregistered one is refused, and a random tag resolves only when well-formed and Spanish | `ContentSchemaFuzzTests` | 500 | `VOCATERRA_FUZZ_ITERATIONS_CONTENT` |
| O16 | Everything that flows through `CanonicalJson` round-trips byte for byte (M32: including the manifest's `language` and `supportLanguage`, present or null) | `SerializationRoundTripFuzzTests` | 3 000 stage records, 1 000 catalogs, 300 replay envelopes | `VOCATERRA_FUZZ_ITERATIONS`, `VOCATERRA_FUZZ_ITERATIONS_PIPELINE` |

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
  - *O4 and O5, `word_order`, past a word that can be an adjective (M58 (c), narrowed by the owner's decision of 2026-10-31, "go with option 1").* The guard reads the
    words' readings, not the pattern's roles: walking back from the adjective's new place, it passes over a word that can be a descriptive adjective and cannot be a noun,
    an article, a numeral or a determiner, as it passes over the adjectives, and makes no move when a word that can be a noun stands behind (`MutationGen.WordOrder`,
    `CanOnlyExtendAPhrase`). Found at M57 (O5, CsCheck seed `ff2aNwHuJBk8`, `MutationCase { Kind = WordOrder, Seed = 64247025744 }`; it failed the same way on the analyzer
    of `0041eff`, before M57, about one full fuzz run in eight): the random pattern `[NOUN:item] ([NOUN:qty]|[VERB:soltar:spell]|[ART])? [VERB:plantar]? [NOUN:thing]
    [ADJ]` gave `estacionamientos suelta plantar felicitación satisfecha` (`circos suelta plantar columna satisfecha` on the bundle of M58), and the mutation moved the
    adjective before the noun. The old guard (no move when the word before the adjective, or before the adjectives before it, can be a noun) saw `plantar` as the pattern's
    verb; the analyzer sees that `plantar` can be an adjective and `suelta` before it a noun, reads `suelta plantar satisfecha` as one phrase, and rightly reports no
    `word_order`. This narrows O4 and O5 for exactly that case and no other: every move the old guard skipped is still skipped, and the moves it skips besides have a word
    that can be a descriptive adjective (and nothing that opens a phrase) between the moved adjective and a word that can be a noun. `WordOrderGuardTests` replays the case
    (it now skips that sentence and mutates `las treinta blancas tristes`, which the analyzer flags as named, O4's comparison) and checks that the analyzer finds no
    `word_order` in either sentence it built; a run of the project at five times its depth found nothing else (M58).
  - *O4 and O5, `vocab.unknown` (M26b).* The mutation is not made on a sentence whose grammar has a name (`[WORD]`): the name binds any word of letters, so the non-word becomes the name, the
    grammar still matches and nothing is unknown (seed `2to272FZCXD3`, `religioso nosotros focatudil` against `[ADJ:item] [PRON] [NOUN:qty]? [WORD:spell]?`; it failed on main before M26b).
  - *The realiser and the accent variants (M26b).* A word that is another word once its accent is written (`el`/`él`, `tu`/`tú`, `esta`/`está`, `te`/`té`, `se`/`sé`) is read as the
    accented word when the neighbours leave no reading of the word as typed (an article, demonstrative or possessive with no noun phrase after it; an object pronoun with no verb after it).
    The realiser re-rolls a pattern that puts a verb, adverb, preposition, conjunction, pronoun or interjection right behind a determiner (`[DET] [VERB]`), and an object pronoun with no
    verb after it (a pattern ending in `[PRON]`); the skips count as `skip:accent-variant-homograph`, inside the homograph budget. `CorrectSentences_WithAnAccentDropped...` (O3) writes
    the correct sentences without the accent of `él`, `tú`, `está` and `estás` where the neighbours leave no doubt (an independent model of the rule) and requires the same errors.
  - *Invented names (M26b).* A name is drawn again when its spelling is a closed-class word of the lexicon once accents are ignored (`nadá` folds to `nada`), which `[WORD]` cannot bind (M19f;
    seed `fApfampI1fQ3` failed O3 on main before M26b). O5 does not compare a case whose unbound name the lexicon knows (`Mirá` is the voseo imperative `mirá`), as O4 never did (seed `belmI3pADeia`
    failed on main before M26b).
  - *O4 and O5, `conj.person` with a name in an alternative (M26b).* The mutation is not made on a grammar that has both an alternation and a name: the mutated verb can be bound as the name
    (`nosotras freís` against `([VERB:freír]|...|[PRON] [WORD])`), which has no reading to check (seed `1RTZdjQ2tFb2` failed O4 on main before M26b). The tense
    mutation shares the guard since M65 (a), below.
  - *The realiser and a literal behind a preposition (M26b).* A literal that is a noun and a verb form right behind a preposition (`de meta`) is a noun head, as the analyzer reads it, so the adjective after it
    agrees with it (seed `8OeIOEx_Hfm3` failed O3 on main before M26b).
  - *O4 and O5, tied parses.* When the analyzer's answer differs from the mutation's and the grammar itself reads a word of the sentence in two ways (`duchas complicados`: `[VERB]` or
    `[NOUN:item]`, and the word keeps both readings), the case is excused and counted (`KnownTolerances.IsTiedParse`, `excused:tied-parse:<kind>`); the oracles assert that the
    excused cases stay under one in a hundred. In the runs made it did not occur at default depth.
  - *O4 and O5, `conj.person` after the verb (M56, a widening).* Besides a verb behind its subject pronoun, the person mutation now also changes the number of a
    third-person verb whose subject follows it (`dónde están los libros` becomes `dónde está los libros`, expected `están`). It is made only where the analyzer's
    reading is certain: the verb is `estar`, or an experiencer verb right behind a dative pronoun (`me gustan las manzanas`); no word before the verb can be a noun, a
    subject pronoun or a name, or is unknown; an article or numeral right after the verb opens the phrase of a noun of one number that cannot be a verb, is no noun of
    time or quantity and has no `y`, `e`, `o`, `u` or `ni` after it; and the new form is that verb and nothing else. The verbs of price behind `cuánto` are checked by the
    analyzer but not mutated. A pronoun right behind a word that can be a preposition is no subject of the person check any more (`a ella le gusta`), so the mutation
    behind a subject pronoun is not made on one.
  - *O4 and O5, `conj.person` after a noun subject (M59, a widening).* The person mutation also changes the number of a third-person verb right after a noun phrase
    with an article or numeral that opens the sentence (`los niños comen pan` becomes `los niños come pan`, expected `comen`). It is made only where the analyzer's reading
    is certain: the first two words of the sentence are an article or numeral and its noun; every reading of the noun is a noun of one number, no noun of time or
    quantity; the verb follows the noun directly; and the new form has no reading but that verb in that tense. It never mutates the verb after a lone name (`domingo hace
    un hacha`), which the analyzer leaves unchecked because the name may be the person spoken to, and it never makes a verb of the first or second person, which the
    analyzer does not check after a noun phrase. The oracles as they stood before M59 are unchanged.
  - *O4 and O5, `gender.agree` on a predicate adjective (M62, a widening).* The gender mutation of an adjective also flips an adjective after `ser` or `estar` whose
    copula has a subject pronoun of a known gender before it (`ella es alta` becomes `ella es alto`, expected `alta`), which the analyzer agrees with the pronoun since
    M62. It is made only where that reading is certain: the copula is a generated finite `ser` or `estar` at most two skippable words before the adjective, its subject
    pronoun (the one the person check pairs with it, not behind a word that can be a preposition) at most two before the copula, and the new form an adjective of the
    other gender and nothing else, as for an attributive adjective. `PredicateAdjectiveMutationTests` shows that the draw reaches it. The oracles as they stood before
    M62 are unchanged.
  - *O3, O4 and O5, a subject pronoun joined by `y` (M64, a widening).* The realiser agrees the verb after a pronoun that ends a coordinated subject with the
    coordination (`luz y yo comemos`), and the person mutation of that verb expects the coordinated form (`luz y yo como` gives `comemos`); see "How the sentence
    generator stays honest". The mutation leaves out the cells the analyzer accepts, as it always left out the pronoun's own, and no oracle as it stood before M64 is
    narrowed.
  - *O4 and O5, `conj.person` after a noun subject, an amount before a singular `ser` (M64 (b), 2026-11-01, narrowed at the owner's request to fix the review's
    findings).* The analyzer reads a phrase of plural cardinals before a singular `ser` with a singular predicate as one sum (`diez monedas es mucho dinero`, `dos panes
    es suficiente`; R5 of the review of `feature/village`), so `es` there is as right as `son`. M59's mutation of a third-person verb after a noun subject is not made
    on exactly that case: the sentence opens with a numeral, the verb is `ser` and the new form singular, and the word after it (past at most two skippable words) is a
    word of `LexiconCatalog.AmountPredicates` (`mucho`, `poco`, `suficiente`, `caro`...) or can open a phrase of the singular (an article, a numeral or a determiner
    adjective of the singular), as `SpanishAgreementChecker.IsAmountReadAsUnit` asks (it also asks for the phrase's noun, so the guard is a little wider). Every other
    case of the mutation is made as before: an article before the noun (`las manzanas es roja`), a verb other than `ser` (`dos niños come pan`), a predicate of the
    plural or an adjective that says no amount (`dos niños son altos` becomes `es altos`, expected `son`). `AmountMutationGuardTests` shows that the guard is needed
    (without it the mutation builds `dos niños es un problema`, which the analyzer rightly finds correct) and that the mutation is still made before a predicate of the
    plural. This is the one narrowing of M64.
  - *O4 and O5, `conj.tense` with a name in an alternative (M65 (a), 2026-11-01, the owner's yes (M65)).* The tense mutation takes the guard M26b gave the
    person mutation, one predicate for both (`MutationGen`, `NameBesideAnAlternation`): it is not made on a grammar that has both an alternation and a name. With
    `ayer` in front, an alternative with the name can fit and bind the verb as the name, which has no reading to check (seed `egVgLELrxyL4`, `MutationCase { Kind =
    ConjTense, Seed = 4236916336216397 }`: `ayer presiono abusar` against `([VERB:presionar]|[ADV] [WORD:item]|[VERB]) [VERB:abusar]`, where `[ADV] [WORD:item]`
    takes `ayer presiono`; found at M58, it failed about one run of the project in six at its default depth). The guard is as wide as M26b's and no wider: a grammar
    with a name and no alternation, or an alternation and no name, is mutated as before (a narrower test, a name inside the alternative itself, would rest on how the
    matcher breaks ties between parses of one length). `NarrowedMutationGuardTests` (a) shows that the analyzer binds the verb as the name in every realisation of
    that pattern, and that the same pattern with `[NOUN:item]` in place of the name is still mutated and flagged as named.
  - *O4 and O5, `conj.person` after a noun subject and `haber` (M65 (b), 2026-11-01, the owner's yes (M65)).* M59's check leaves `haber` out by design (`hay dos
    panes`: the existential agrees with nothing), so M59's mutation of a third-person verb after a noun subject leaves it out too and models what the analyzer claims
    to check (seeds `b3HSKSLCsL_1`, `MutationCase { Kind = ConjPerson, Seed = 729196 }`: `el subtítulo ha peligroso` made `han`; `4YuZ2yubxdC1`, `Seed =
    169110944`: `unas televisiones han` made `ha`; found at M62, at the default depth since M64's gate). The new form has no reading but that verb's, so the guard is
    exactly the analyzer's exclusion: every other verb, and `haber` after a subject pronoun (`ellos ha` gives `han`), is mutated as before.
    `NarrowedMutationGuardTests` (b) shows that the analyzer leaves the other number of `haber` after a noun subject unchecked, and that the same pattern with
    `estar` is still mutated and flagged as named. The realiser draws `haber` like any verb with a full paradigm and gives it no participle (`el subtítulo ha
    peligroso`), which is not Spanish. It stays so for now: leaving `haber` out of the draws would be a small change, but it would also take away O4's person check of
    `haber` after a subject pronoun, which the perfect needs. *The later widening*, when a lesson teaches the perfect (`he comido`): the analyzer checks `ha` and `han`
    against a noun subject when a participle follows (those two forms are never existential), the realiser gives `haber` its participle, and this guard keeps only a
    `haber` with no participle after it.
  - *O4 and O5, `conj.tense` beside a literal with an accent variant (M65 (c), 2026-11-01, the owner's yes (M65)).* A literal of the grammar that is another word
    once its accent is written (`porque` and `porqué`) is read as typed while the grammar binds it; when no grammar matches, the analyzer chooses between the two
    from the neighbouring words (`ReadingResolver`: a literal spelled like the word keeps every reading it has as typed, so the accent is the one reading the match
    decides), and the realiser leaves that choice to the grammar for a literal. The tense mutation puts `ayer` in front, and then the grammar may no longer match
    (seed `2ldq2rIKLrG2`, `MutationCase { Kind = ConjTense, Seed = 4340032 }`: `ayer las los despego unas porque` against `[ART] [ART]? [VERB]? [ART]? porque?`,
    where `unas porque` then reads as the article and the noun `porqué`, giving `gender.agree` and `noun.number` beside the expected `conj.tense`; found at M62, at
    five times the default depth). Such a case is kept only when the mutated sentence matches the original's grammar; the generator asks the analyzer for the match
    alone, never for the errors the oracles compare. The owner's yes covered every tense mutation, but the guard asks for the match only beside such a literal: of 6 002
    tense mutations measured before the guard, 46 (0.8 %) still matched their grammar (`ayer` in front fits only a grammar that can open with an adverb or with `ayer` itself), and 237
    (4 %) had such a literal and no match, so the wider guard would have left the mutation almost nothing but grammars that open with an adverb. A literal with two
    parts of speech is not guarded: the match does not narrow the readings of a literal spelled like the word. `NarrowedMutationGuardTests` (c) shows the analyzer's
    reading of the failing sentence, that the pattern is not mutated, and that the same pattern with `aunque` (no accent variant), drawn from the same seeds, and the
    same pattern with an `[ADV]?` in front that takes `ayer`, are still mutated and flagged as named.
  - *O3, O4 and O5, the grading gaps of M66 (2026-11-01, widenings; no oracle narrowed).* (a) The realiser models a subject joined by `ni … ni` and by `o`, which the
    analyzer now joins as `y` (and `o` with the nearest member's form as well): `SentenceRealizer.CoordinationBefore` reads `ni`, `o` and `u` beside `y` and `e`, and the
    word said before the first member (`ni tú ni yo`) at the start of the sentence or after a word of `SubjectClauseOpeners`; the verb after `o` takes the nearest
    member's form on half the draws; the person mutation leaves out the nearest member's cell too, and makes none on a verb of that cell (the analyzer's correction there
    is the plural). Before M66 the realiser read such a pronoun alone, which the analyzer now flags for `ni` (`ni tú ni yo fui`) and accepts in more cells for `o`.
    (b) The realiser agrees a verb after a subject pronoun and a question word (`ella qué quiere`; M63) with the pronoun, and the person mutation makes a first or
    second person there (`ella qué quieres` gives `quiere`); a third person is not made through such a pronoun, since the analyzer leaves it unchecked in two readings.
    It makes no person mutation through a pronoun right after a verb that agrees with it (`¿puede usted…?`), whose verb the analyzer checks no more. (c) The tense
    mutation makes, on every other sentence that has a modal and its infinitive (by the parity of the sentence's length, so that no draw moves), the learner's slip for
    the infinitive in place of the past marker (`puedo pagar` becomes `puedo pago`, expected `pagar`): a new case of O4 and O5, made only where the form is that verb's
    indicative and nothing else, has no accent variant, and is no modal, copula, `haber`, `tener` or `ir`. (d) The realiser makes the verb after `tener que` an infinitive,
    as after a modal and `ir a`, and re-rolls a word that the reading resolver may read as a verb: a word right after a modal (`skip:modal-complement-homograph`; nouns
    that are also verb forms are not drawn after `poder` and `tener que`), and a noun that is also a verb of the third person right after the noun of the phrase that
    opens the sentence or a noun that is also a copula right after `cuánto` (`skip:noun-reads-as-verb`; such nouns are not drawn after the opening phrase). Both are
    broader than the resolver, which also reads what follows. `CoordinatedSubjectFuzzTests` gained five patterns with `ni` and `o`, and `ModalAndQuestionFuzzTests`
    realises nine patterns with a modal or a pronoun before a question word, checking O3 and O4 on each. The sentence stream digest of `FuzzLanguageStreamTests` was
    recorded again for these changes (the text digest did not move).
  - *O3, O4 and O5, the core findings of the QA pass on the village (M70, 2026-11-02, widenings; no oracle narrowed).* (a) A new O3 fact,
    `CorrectSentences_WithAnNTildeTypedAsNWhereThatSpellingIsNoWord_AnalyzeAsTheSentenceWithIt`, writes a correct sentence's words with `ñ` with an `n` where that
    spelling is no lexicon form (accents ignored) and no Spanish word of the test's own list (`ano`, `cana`, `pena`, `una`…, an independent copy of the idea), and
    requires the errors of the sentence with `ñ` (tag, span and expected; the heard text is the learner's spelling); it asserts that some `ñ` is typed so at the
    default depth (`ntilde:typed-as-n`). (b) The person mutation of a subject after its verb (M56) makes, for `gustar` and `encantar` behind their dative pronoun, a
    form of the first or second person on half its draws (`me gustas el pan`, expected `gusta`), where only the third person of the other number was made, and the form
    has no reading but that verb's. (c) The irregular mutation also makes the first person of the preterite of a verb in `-car`, `-gar` or `-zar` without its spelling
    change (`busqué` becomes `buscé`, expected `busqué`), modelled by the test's own spelling rule and kept only where the morphology reports the same slip. (d) A realiser
    correction: `gustar` and `encantar` after a subject pronoun with a phrase after them were realised as correct (`nosotras gustamos los alcoholes`, `tú aquí gustas el
    cristal`), which the analyzer now reads as the learner's English order (`conj.person`, `nos gusta`); the realiser cannot make a dative of the pronoun, so it
    re-rolls such a draw (`skip:liking-verb-after-a-subject-pronoun`, under the default skip budget). It is a sentence the realiser got wrong, not one an oracle left out.
    The sentence stream digest of `FuzzLanguageStreamTests` was recorded again for (d) (the text digest did not move).
  - *O3, O4 and O5, the left points of M56, M58 and M62 (M69, 2026-11-02, widenings; no oracle narrowed).* (a) Two skeletons, drawn by O3 and the mutations: `[ART]
    [NOUN:thing] [VERB:estar] en [ART] [NOUN:place]`, whose `estar` the `ser_estar` mutation makes `ser` (`la casa está en la plaza` becomes `es`, expected `está`; not
    after an event noun, before a noun of time or of an adverbial phrase, nor where the form of `ser` is also `ir` or a noun, as `fue`, `son`, `era`), and `[ART]
    [NOUN:thing] de [ART] [NOUN:owner] [VERB:ser] [ADJ:quality]`, whose predicate adjective the `gender.agree` mutation flips against the subject with `de` when the copula
    has its number (`la casa de los niños es bonita` becomes `bonito`). `LexiconCatalog` mirrors the event nouns, the nouns that are no place after `en` and the clause
    openers; `PlaceAndSubjectWithDeMutationTests` shows each mutation reached and flagged as named. The first draft of the `ser_estar` mutation also made `es en` after
    a subject of time (`la semana es en los escuadrones`, seed `bc29DoKXymZ2`; `el año es en una boda`, seed `1F1qU37RZE0a` at five times), which the analyzer rightly
    takes for no subject (a phrase of time): the mutation's own miss, fixed before the runs that count (it skips a noun of time, part or quantity as the subject). (b) The realiser models the new checks, never wrongly: a verb behind
    a question word or a relative with a subject pronoun right after it takes the pronoun's person and number (`¿dónde estás tú?`), and a literal verb that cannot is
    re-rolled (`skip:inverted-subject`, under the default budget); a predicate adjective agrees with the subject with `de` as well as with the other candidates. Before
    (b) the first run failed O3, O4 and O5 on the catalog's `qué [VERB:hacer] (tú|usted)?` and `dónde [VERB:trabajar] (tú|usted)?` (seeds `9mbOVQ0eFYM3`,
    `dUYM6Yw-H953`, `2d5pW9wRCBp4`): the realiser drew `qué hago tú` as correct, which the analyzer now reads as `conj.person`. (c) M71's finding: the
    `literal-verb-subject` re-roll no longer takes a literal that is the head of the phrase before it, so O3 draws `buenas tardes`
    (`TheGreetingGrammarRealisesBuenasTardes`). The sentence stream digest was recorded again for these changes (the text digest did not move).
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

## Per-language harness (M35)

The oracles are language-neutral in their statement and Spanish in their generators. M35 splits the two. `Oracles/FuzzLanguage.cs` is the record one
language runs with: its pack (`ILanguagePack`), its bundle, a free-text generator, a sentence realiser (`Sentences`, which yields a `FuzzSentence`: the
text and the grammar it was built from) and its mutation kinds. `FuzzLanguage.Spanish` holds the generators the Spanish suite has always used.

- `Oracles/LanguageOracles.cs` holds the bodies of O1 (normalizer), O2 (analyzer), O7 (intent resolver) and O14 (a neutral pipeline run: never throws,
  deterministic, verdict by hand through the pack, decision, replay round trip). Each takes a `FuzzLanguage`. The Spanish classes (`NormalizerFuzzTests`,
  `AnalyzerFuzzTests`, `IntentResolverFuzzTests`) call the same bodies with their own coverage facts, and `NormalizerFuzzTests` also calls the Spanish static
  entry points (`Accents.Strip`, `SpanishPhoneticKey`). The Spanish O14 (`PipelineFuzzTests`) stays as it was: it also covers phonemes, two-phase runs,
  host turns and the envelope mutations, which need Spanish inputs.
- The same seed gives the same Spanish cases. CsCheck seeds only the first iteration of a sample, so `FuzzLanguageStreamTests` hashes the first case of
  400 fixed seeds, for the text stream and the sentence stream. **What the two digests guard: the generators** (a generator, its draw order, the realiser, a
  mirrored table of `LexiconCatalog`, `PipelineHarness.FixtureGrammars`). A changed generator or a changed realisation fails that test, and the commit that changes
  them records them again and says why. They are computed on **frozen inputs** (M57, `Generation/GeneratorInputs.cs`, `FrozenGeneratorInputs`): the files of
  `Fixtures/StreamDigest`, a copy of `content/game-v1`'s hand-authored lexicon files (`seed`, `game`, `world`, `supplement`; 1 299 entries; the imported Wiktionary
  lexicon is left out) and of its 92 slot grammars as they were at `0041eff`, never edited, with the suite's fixture grammars appended. So no content edit moves
  them: from M35 to M59 they were recorded on the bundle itself, and the content milestones V10 to V16 each re-recorded them with the generators unchanged. A
  second fact checks that `FuzzLanguage.Spanish`, the record the oracles run on, gives the old expressions' streams on the bundle (compared with each other, not
  with a recorded digest, so a content edit moves both alike), and a third that most first cases on the frozen inputs are realised sentences.
- `Languages/Xx/` is the toy language (M35): `XxLanguagePack` implements every `ILanguagePack` member with trivial, non-Spanish behaviour (no accent, no
  inverted mark, no subject pronoun, numbers 0 to 3 as invented words, no G2P, its own three error tags); `XxBundle` writes a three-word bundle
  (`ka`, `mo`, `ti`; two intents; two prompts, one of them constrained; an agent; a dialogue; an effect table) with `"language": "xx"` into a temporary
  directory and loads it with `LanguagePacks.Of(XxLanguagePack.Instance)`, a registry without Spanish. `XxFuzzLanguage` is its record.
  `XxOracleTests` runs O1, O2, O7 and O14 on it, and checks that its results carry no Spanish error tag and no Spanish mark. The toy suite is
  in-process and uses no model. It is not a shipped language.
- Which oracles run per language: O1, O2 and O7 run on Spanish and on the toy language. The neutral O14 body runs on the toy language; the Spanish O14 is the
  full version (phonemes, two-phase, host turns, envelope mutations) and is not run on the toy language. O3, O4 and O5 (realised grammar sentences, mutation
  sets) run on Spanish only; they need a realiser and a mutation set for the language (German, M36 onward). O6, O8 to O13, O15 and O16 are Spanish or
  shared (O8, O9 and O13 need a G2P or the signal front end; O15 and O16 read the Spanish content bundle).
- Golden files: a Spanish golden keeps `Data/<name>`; another language's goldens live in `Data/<lang>/<name>` (`GoldenFiles`, the same
  `VOCATERRA_RECORD_GOLDEN` variable). The layout is in code; no file under a `Data` folder was added for it.

Bugs the toy language found in the core (M35, fixed at the source with regression tests, see the findings log of the plan): the Spanish fold threw on a lone
surrogate; the content validator filled templates with Spanish sample words; the recast spelled digit runs above the pack's number bound.

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
  - `GeneratorInputs` - what the pattern and sentence generators read (`IGeneratorInputs`: the lexicon catalog and the grammars): the bundle's through `PipelineHarness`,
    or the frozen copy of `Fixtures/StreamDigest` that the stream digests are recorded on (`FrozenGeneratorInputs`, M57).
  - `GrammarPattern`, `PatternGen` - slot grammar patterns as a tree: every catalog grammar, structural skeletons, and random DSL strings built from the token kinds, including the name token `[WORD:slot]` (M17b).
  - `TimeMarkerModel` - the time markers of the tense checks as phrases of a sentence's words (M57), over the words of the pack's `SpanishTimeMarkers`.
  - `SentenceGen`, `SentenceRealizer`, `SentenceModel` - correct sentences from a pattern, agreement right by construction (see below). A name (`[WORD]`) is two or three invented syllables drawn from the seeded generator, written with a capital and normalized in lower case (`TokenRole.Name`).
  - `UnboundNames` (in `Oracles/`) - the `vocab.unknown` errors that the names of a sentence must get when its grammar does not bind them (see below).
  - `MutationGen` - exactly one mutation of a correct sentence, with the errors the analyzer must report and the sentence the recast must restore.
  - `SlotMapGen` - slot maps derived from the placeholders and filters of the real templates.
  - `VerdictGen` - random stage results within the contract ranges, including exact-threshold confidences.
  - `AudioGen` - `WavData` at 8 000 to 96 000 Hz, mono and stereo, PCM16 and float32, three header layouts (canonical, odd-sized extra chunks, `WAVE_FORMAT_EXTENSIBLE`), 0 ms to 5 s, from silence to clipped noise; white noise is drawn from the CsCheck generator; byte damage (truncate, corrupt a header byte, wrong chunk size) of valid files. `WavBuilder` writes the files independently of `WavFile.Write`.
  - `PhoneGen` - phone sequences of one to twenty inventory phones and phrases from the lexicon, posteriors through `FakePhonemeRecognizer.FromPhones` and `FromMumbled`, confusions from the predicted-error table (`ɾ→ɹ`, `r→ɹ`, `a→ə`, `e→eɪ`, `o→oʊ`, an inserted `h`), frame layouts, mumbled frames, dialects.
  - `DecisionGen` - agent definitions (the bundle's, random valid ones, valid ones with one defect), BKT parameters, FSRS review scripts, feedback policies, repeat counts.
  - `ContentMutationGen` - walks the JSON files of `content/game-v1` and applies exactly one mutation: remove a property, change a value's JSON kind, point a reference at a fresh id, give an enumerated field an unknown value, empty a required non-empty string (`EmptyRequiredString`: an effect's `effectId`, `intent` or `kind`, a prompt's `promptId`), duplicate an element that has an id, flip a byte without touching the manifest. The manifest is recomputed for every kind but the byte flip (the committed manifest with the entry of the one mutated file replaced), so the loader's schema and reference checks run and not only its hash check. Files over 1 MB (the imported lexicon) are not walked for structural mutations, because they have the schema of the small lexicon files and writing them back costs more than everything else; a flipped byte still reaches them.
  - `PipelineGen` - one pipeline run: a transcript from a correct sentence of the prompt's own grammars, a mutated sentence, an answer, free text or an adversarial string, plus prompt, agent, state, counter, seeds, learner, repeat counts and options.
- `Oracles/` - `PipelineHarness` (bundle plus fakes wired once, with builders for services, pipeline, `ConversationContext`, prompts), `KnownTolerances`, `Determinism`.
- `*FuzzTests.cs` - one file per oracle.
- `AmountMutationGuardTests`, `WordOrderGuardTests`, `NarrowedMutationGuardTests` - the guards of the narrowed oracles, replayed from their seeds with a close neighbour
  that is still mutated; `SkipBudgetTests` - the skip budget (M65, below).

### How the sentence generator stays honest

`SentenceRealizer` mirrors the analyzer's own scans (noun phrase = determiners, adjuncts, a noun head, adjuncts; a subject pronoun pairs with
the next verb after at most two adverbs or clitics) and builds each sentence so that articles, numerals and adjectives agree with their noun,
verbs agree with their subject pronoun, adjectives sit on the right side of the noun, copulas meet an adjective of their class, and no past
marker sits next to a present verb. It is a model of the analyzer, so the oracles are an agreement check between two implementations. A
pattern that cannot be realised correctly is **skipped and counted**, never silently weakened; each oracle prints its generator
diagnostics and asserts a minimum of realised sentences, and `MutationTagFuzzTests` and `RecastLoopFuzzTests` assert that every mutation
kind stays reachable. `CorrectSentenceFuzzTests` also asserts that every grammar of the catalog is realised at least once (since M71 a grammar
its random sample left out is first realised from fixed recipes, through the same checks: "Bounds the suite needs") and that no literal
word of any grammar is missing from the lexicon (the old `cuál` gap would fail it). If a mirrored table in `LexiconCatalog` (the ser/estar
adjective lists, the prenominal adjectives, ...) drifts from `SpanishAnalysisTables` in the Spanish pack, the mutation oracles fail and
name the kind. The time markers are not mirrored (M57): the words come from the pack's public table `SpanishTimeMarkers`, which the checker reads too.

Homographs are generated, not avoided (M18): `SentenceGen` counts the sentences that hold a noun that is also a verb form, a verb form that is also a noun and a noun of both genders
(`homograph:sentence-with-...`), and O3 asserts that each is at least one sentence in fifty (about 17 %, 6 % and 6 % of the sentences in a default run), so a generator that quietly went back to the
plain words would fail. The few placements the analyzer reads differently on purpose are re-rolled (see the list above); a recipe that fails every retry is counted as
`skip:verb-form-homograph` or `skip:function-word-homograph`, and O3, O4 and O5 assert that these are at most one recipe in two hundred (a default run has 0 to 1, against 1, 6
and 3 before M18).

M56 made the analyzer agree a verb with a subject after it and flag `ser` where a place takes `estar`, so the realiser models both. It gives `estar`, the
experiencer verbs (`LexiconCatalog.ExperiencerVerbs`) and the verbs of price the third person and the number of a noun phrase that follows them, whatever stands
before them (`costar` and `gustar` always took the third person, as before); agreeing where the analyzer would not check is never wrong. It leaves out `ser` before
`lejos` or `cerca` and `ser` between `dónde` and a noun phrase (`skip:ser-of-place`; the analyzer's exceptions for events are not modelled, which only leaves out
correct sentences), and draws no noun that is also a verb form right behind `dónde`, `donde` or `cómo`, where the reading resolver takes the verb (another word there
that can be a finite verb is re-rolled as `verb-form-homograph`). The sentence stream digest of `FuzzLanguageStreamTests` was recorded again for these changes.

M59 made the analyzer agree a third-person verb with a noun phrase that opens its clause before it (`los niños comen`) and a reflexive clitic after `cómo` with
`llamar` or `sentir` (`¿cómo te llamas?`), so the realiser models both. It gives a verb right after a noun phrase (past at most two skippable words or object
pronouns) the third person and the phrase's number, whatever stands before the phrase; agreeing where the analyzer would not check (a phrase of time, one a preposition
governs, an object put first) is never wrong. After a lone name (one bare noun, `domingo`), which the analyzer never checks because it may be the person spoken to, the
realiser draws the verb's person and number like any other, so O3 also holds `domingo hago un hacha` correct. It skips a subject of two phrases joined by `y` or `e`
(`skip:coordinated-subject`: the analyzer makes it plural only where the first phrase opens its clause, which the realiser does not model) and a literal verb form that
disagrees with a phrase before it that is no lone name (`skip:literal-verb-subject`). It gives
`llamar` and `sentir` right after `cómo` and a reflexive clitic the clitic's person (`se`: the third, either number). The sentence stream digest of
`FuzzLanguageStreamTests` was recorded again for these changes.

M57 taught the realiser the time markers of more than one word. A marker is a phrase of the pattern's literal words, found by `TimeMarkerModel` with the
checker's rules over the words of `SpanishTimeMarkers` (the pack's public table, which the checker reads): `ayer`, `anoche`, `anteayer` or a time noun followed by a form of
`pasado` (`el año pasado`, `la semana pasada`) put every verb in the preterite; `mañana` (not after `la`, `esta`, `de` ...), `pasado mañana`, `próximo/a` before a time noun
(`el próximo día`, `la próxima semana`) or a time noun followed by `que viene` (`la semana que viene`) put it in the present; both in one sentence is
`skip:conflicting-time-markers`, as before. Before M57 it knew only the single words `mañana`, `ayer`, `anoche` and `anteayer`, so a grammar with `la semana que viene` or
`el año pasado` gave it sentences it counted as correct that the checker rightly flags `conj.tense` (the game's V10 found it; with the fixture grammars below, O3, O4 and O5
fail on the old realiser with `ustedes secuestran un acabo el año pasado`). The words still to be chosen break a phrase across them, and once they are chosen the sentence is
read again: a sentence whose generated words make or unmake a marker with the literals (a time noun drawn before a literal `que viene`, an article before a literal `mañana`)
is drawn again (`skip:time-marker-of-generated-words`; none in a run of 20 000). `PipelineHarness.FixtureGrammars` gained two patterns that carry these markers around a verb
and its object, in the shape of the game's `state_action` rows (one mixes past and future markers as those rows do), so the oracles cover them before the bundle has them;
O3 on them also finds the `pasado mañana` bug below. The sentence stream digest of `FuzzLanguageStreamTests` was recorded again for these changes.

M62 made the analyzer agree an adjective after `ser` or `estar` with the copula's subject (a subject pronoun before it; else a noun phrase that opens its clause before
a copula of the third person, or a noun phrase with a determiner or a subject pronoun right after the adjectives; else the copula's own number), and read a colour that
is also a noun (`negras`, `rosa`, `gris`) as the adjective right before a noun it agrees with. The realiser agrees a predicate adjective, and the adjectives joined to it by
`y`, `e`, `o`, `u` or `pero`, with every candidate at once (the pronoun alone when there is one; otherwise the phrase before the copula unless it is a lone name, the
phrase after the adjectives and the copula's number), since whether a phrase opens its clause is not modelled; candidates that disagree skip the draw
(`skip:predicate-subject`; none in the runs made for M62). It re-rolls a word that is a colour and a noun right before a word that can only be a noun
(`skip:colour-before-noun`, broader than the analyzer's rule, which also asks the phrase to agree; seed `f8C3x9UYGMz6` built `las 25 negras enterados despejados`
before it), and a word that can be an adjective and a noun right after the noun of a phrase with a determiner that it agrees with, which the analyzer reads as that
noun's adjective (`skip:noun-reads-as-adjective`; a word that can also be a finite verb is left alone by both), and a noun that is also something else right after
`muy` or `tan`, where the analyzer reads no noun (`skip:noun-after-degree-adverb`). The colours are read from the Spanish pack's public rules (`IAnalysisRules.IsPostnominalAdjective`), not mirrored. The sentence stream digest of
`FuzzLanguageStreamTests` was recorded again for these changes.

M64 (the review of `feature/village`, R1) made the analyzer read a subject pronoun joined by `y` or `e` to members before it that open their clause as the end of one
plural subject (`luz y yo fuimos`, `tú y ella son`), so the realiser models it where it can tell what the analyzer reads, a widening of O3, O4 and O5: before M64 it agreed
the verb with the last pronoun, which the analyzer now flags. `SentenceRealizer.CoordinationBefore` (mirror of `SpanishAgreementChecker.CoordinationOf`) reads the
members back from the pronoun: a subject pronoun, a noun phrase or a name, whose clause is surely opened when it is the sentence's first word or follows `y`, `e`, `pero`,
`porque`, `si`, `cuando` or `aunque` (`LexiconCatalog.SubjectClauseOpeners`). The verb then takes the first person plural when a member is of the first person; else the
third person plural (seven draws in ten) or the second (three in ten) when one is of the second; else the third plural; a literal verb that agrees with none skips the
draw. A predicate adjective after it is plural, with the coordination's gender, known only when every member has one. A member right after a word that can be a verb or a
preposition, or a word before `y` that can be no member, leaves the pronoun its own subject (`y` joins two clauses: `comí pan y tú bebes`), as before. Everything else is
skipped as `skip:coordinated-subject`, the reason M59 gave a subject of noun phrases joined by `y` (unchanged): a word not drawn yet when the verb is finished, a word that
may be a noun or a subject pronoun of another reading, a phrase of time, a bare noun of another reading, a noun of both genders, a phrase with `de` before it and a
clause the analyzer may open past adverbs or phrases of time. The token of the pronoun carries the coordination (`SentenceToken.Coordination`), so the person mutation
leaves out every cell the analyzer accepts (both plurals for a member of the second person) and makes none on a coordinated verb of the second person plural (the
analyzer's correction there is the third person plural, not the form the sentence had), and the gender mutation of a predicate adjective reads the coordination's
gender. `CoordinatedSubjectFuzzTests` realises eight patterns with `y` before a pronoun (150 seeds each: 637 coordinated subjects of all three persons, 548 person and 71
gender mutations, all flagged as named). The shape is rare in the oracles' own draws (a random pattern with a conjunction before a pronoun and a verb), and the
sentence stream digest did not move.

A name (M17b) has no reading the checks use: the realiser treats it like an unknown word, so it breaks every phrase and every subject-verb link around it, and
the analyzer does the same (a bound name keeps no reading at all). `CorrectSentenceFuzzTests` and `SerializationRoundTripFuzzTests` (`NameSentences_SurviveCanonicalJson`)
realise the catalog grammars that carry a name. O4 and O5 analyze a mutated sentence with its own grammar, and a mutation such as a leading `ayer` breaks that match: the
name is then a word the lexicon lacks, which the analyzer reports as `vocab.unknown`. `UnboundNames.Expect` predicts exactly those errors, and O4 and O5 require them
(missing or extra is a failure). A case in which the unbound name is a word the lexicon knows is not compared at all, because its checks would then apply to it; the
counter `unpredicted:name-known-to-lexicon` says how many, and a default run has none.

Slots are compared strictly unless the pattern is ambiguous (two optional tokens that can match the same word, `[DET]` against `[ART]` or
`[ADJ]`, `un` and `una` as article or numeral, a literal word that is also a noun): then the matcher may legitimately bind the words in more
than one way and only the extracted values are checked against the words of the sentence.

### The skip budget (M65)

The realiser re-rolls a draw it cannot spell correctly: a retry, which a later attempt usually makes good, so the `skip:` counter of a recipe whose every attempt
failed hardly moves. The oracles see only the sentences it kept, so a re-roll can leave out a whole class of sentence without a failure; one hid the review's R1
(`tú y yo fuimos` graded wrong, fixed at M64). Since M65 each rejected draw is counted by its reason as well (`rejected:<reason>`, and `FuzzDiagnostics.Draws` for
every draw): each attempt of the realiser that failed, and each realised sentence that `SentenceGen.Realize` rejects afterwards. `SkipBudgetTests` draws the
sentences of O3 and the mutations of O4 and O5 from 2 000 fixed seeds each (their default depth; about two seconds, no model) and fails when one reason takes more than
5 % of a stream's draws. A reason above 5 % gets its own budget, named in the test with a one-line reason; a reason with no entry falls under the default, so a new
re-roll cannot silently eat a class of sentence. Measured at M65 on `content/game-v1` (each reason's share of the draws, and its share of the recipes that gave no
sentence after every attempt):

| Reason | O3 sentences: draws | O3 sentences: recipes | O4 and O5 mutations: draws | O4 and O5 mutations: recipes |
| --- | ---: | ---: | ---: | ---: |
| `accent-variant-homograph` | 4.21 % | 0.00 % | 3.70 % | 0.01 % |
| `adjacent-numerals` | 0.55 % | 0.60 % | 0.56 % | 0.61 % |
| `colour-before-noun` | 0.00 % | 0.00 % | 0.01 % | 0.00 % |
| `conflicting-time-markers` | 0.73 % | 0.00 % | 1.68 % | 0.01 % |
| `function-word-homograph` | 0.05 % | 0.00 % | 0.16 % | 0.00 % |
| `literal-disagreement` | 1.01 % | 0.05 % | 0.85 % | 0.03 % |
| `literal-verb-subject` | 0.18 % | 0.05 % | 0.13 % | 0.01 % |
| `name-read-as-noun` | 0.00 % | 0.00 % | 0.01 % | 0.00 % |
| `noun-reads-as-adjective` | 1.19 % | 0.00 % | 0.43 % | 0.00 % |
| `numeral-next-to-article` | 0.64 % | 0.70 % | 0.45 % | 0.49 % |
| `verb-form-homograph` | 1.10 % | 0.00 % | 1.07 % | 0.01 % |
| all draws (draws, recipes) | 2 183 | 2 000 | 14 172 | 13 041 |

No reason is above 5 %, so the test names no budget of its own; the largest is `accent-variant-homograph`, 4.2 % of O3's draws, each made good by a later attempt.
The other reasons the realiser has (`coordinated-subject`, `ser-of-place`, `predicate-subject`, `time-marker-of-generated-words`, `past-marker-without-preterite`,
`modal-complement-homograph` and `noun-reads-as-verb` (M66),
`no-adjective-form` and the rest) took no draw from these seeds; `CoordinatedSubjectFuzzTests` draws the coordinated subjects on purpose.

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
- **Catalog coverage (M71, 2026-11-01).** O3 claims that every catalog grammar is realised at least once, and its random sample draws a catalog grammar in two
  recipes of five (`PatternGen.Any`): at the default 2 000 recipes about 800, some seven for each of the 110 grammars of `content/game-v1` at M71. A given grammar
  is then never drawn with a probability of about e^-7.3 (0.07 %), and some grammar of the 110 in about one run in thirteen. The village lessons had grown the
  catalog to that size, and O3 failed twice in a row on `feature/village-followups` (V23a's gate and its rerun, on the `[VERB:vender]` and `[VERB:comprar]` grammars
  of V15 and V17) with no change of the core, the suite or the bundle since a passing gate. Measured before the fix:
  - 2 000 simulated runs of the sample (the realisation alone, random seeds): 151 left a grammar out (7.6 %), 83 different grammars among them, 8 of them of the
    family below; the lowest count of a run was 0 in 151 runs, 1 in 802, 2 in 925 and 3 in 122. Ten runs of O3 alone: one failed (`ayuda (por favor)?`).
  - The O3 stream at 2 000 fixed seeds (one case each, `M71O00000000` on): 718 catalog sentences, the lowest count 2 (two grammars), and the family of V15 and V17
    (`(sí)? (mañana|ayer|...)? (domingo|luz|mar|león)? [VERB:...] ... (mañana|ayer|...)?`, ten grammars) 4 to 8.
  - Each catalog grammar from 400 fixed realiser seeds: every one gives a sentence from at least 90 % of its recipes (the lowest is `(buenos|buenas) (día|días|tardes|noches)`
    at 90 %, the next 94.8 %), and the family from 100 %, with 0.16 to 0.20 re-rolled draws a recipe (`conflicting-time-markers` about one draw in ten,
    `noun-reads-as-adjective` one in sixteen), each made good by a later attempt. The realiser does not hold the family back: drawing the second time slot
    compatible with the first, or the verb after a name in the third person, would not raise its counts (each recipe already gives a sentence) and would move
    the sentence stream digest, so the realiser is unchanged.

  Since M71, after the sample, each grammar it left out is realised from up to 50 fixed recipes (`TargetedInputs`: the realiser seed and the safe noise drawn at
  the CsCheck seeds `M71T00000000` on), until one gives a sentence, which goes through the same checks as the sample's; only a grammar that none of them realises
  fails, with the reasons of its rejections (`TheCoveragePass_ReportsOnlyAGrammarTheRealiserCannotRealise`). The claim no longer depends on the draw, so it is
  asserted at every depth (before, from 1 000 recipes). The sample, its counts and its other assertions, the realiser, the skip budget's shares and the stream
  digests are unchanged. After it: ten runs of O3 alone, none failed; at one recipe (`VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=1`) 109 of the 110 grammars took
  the pass and each gave its sentence from the first fixed recipe, passing every check (at 20 recipes: 101 grammars, the same); the pass adds nothing
  when the sample realised every grammar.

## Bugs found and fixed while building this suite

**R02 (2026-10-23): the suite moved from the core's sample bundle to the game's, `content/game-v1`.** The game's bundle is bigger (three agents, many more intents,
the lessons' grammars with time adverbs and the near future, expectations and waivers in the intent file, host files, 433 speech clips) and its effect table
is empty. Running the same oracles on it found one defect in the core, three gaps in the suite's own models, and some places the oracles needed a fixture. No oracle was
weakened: each fix is at the source, or an independent model brought in line with documented behaviour, and has a regression test.

- **Two `conj.tense` errors on one word** (`Vocaterra.Nlp.Language`, `AgreementChecker`; found by O2 with CsCheck seed `3KdllRVaJeE6`, input `va a vais a culpa ayer por favor`).
  A finite verb right after `ir a` (`vais`) was reported by the near-future check (expected: the infinitive) and, when the sentence also had a past-time word
  (`ayer`), once more by the past-tense check (expected: the preterite): two errors with one tag and one span. The verbs after `ir a` are now found in one place
  (`FormsAfterIrA`) and the past-tense check leaves them to the near-future check. Regression: `NearFutureTenseTests.AVerbAfterIrAWithAPastMarkerIsReportedOnce` and
  `NoTwoErrorsShareATagAndASpanWhenAPastMarkerMeetsIrAAndAFiniteVerb` in `tests/Vocaterra.Nlp.Tests/Language`.
- **The sentence realiser did not know time adverbs written into a pattern** (the suite's own generator, `Generation/SentenceRealizer.cs`; found by O3, O4 and O5,
  seeds `aT2NgQa-TLO9`, `0aWmfyhjNR41`, `3T-JO84wBeq9`). The game's grammars write `(mañana|ayer|hoy)? [VERB:ir] a [VERB:comer] ... (mañana|ayer|hoy)?`. The realiser
  drew the tense at random, so it produced `ayer vas a la casa` and `mañana ... ayer` as "correct" sentences, and made the verb after `ir a` a conjugated form (`voy a
  coméis`). It now gives every verb the preterite after `ayer`, the present after `mañana`, skips a sentence with both markers, and makes the verb behind `ir a` an infinitive.
  The analyzer was right in each case. Regression: `OracleInfrastructureTests.SentencesOfPatternsWithTimeAdverbsAreCorrect`.
- **O11's independent model of the agent step ignored host-event transitions** (`DecisionFuzzTests.ExpectedStep`; seed `emrSNsnDrkq5`, the bundle's villager). The documented rule
  (CR-015) is that a transition waiting for a host event is never taken by a verdict, and `AgentStateMachine.Step` follows it; the model did not, because the sample bundle
  had no such transition. The model now skips them, and the random agent definitions of `DecisionGen` now include host-event transitions (one in five), so the rule is exercised.
- **O14's aim at effects** (`PipelineGen.Aimed`): a prompt of the game allows many intents and few resolve an effect, so aiming at "a prompt that allows an effect intent" left
  `region:effects` at one mutation in a run. The aimed sentence is now drawn from the grammars of the effect intents the prompt allows.
- **O15's list of documented tolerances** gained three entries that the game's bundle exercises, each with its reason in `KnownTolerances`: a whole dialogue pool that no agent
  names is removable (`HostVoicedPool`: the game's guide voices its `guide.*` pools itself), the slot grammars of an intent that also has keyword rules are removable
  (`RuleOnlyIntent`), and the lint expectation and waiver fields of the intent file are optional (`examples/*/prompt`, `examples/*/errors`, `lint/**`). Each is accepted only
  where the content makes it valid, so removing the grammars of an intent without a rule, or a pool an agent names, is still rejected.
- **What the game's bundle lacks, the suite builds** (no second bundle is kept on disk): `PipelineHarness.EffectsBundle` (the bundle plus an effect table, the one the pipeline
  oracles run with; its hash is the repository bundle's because the copy keeps the manifest), `PipelineHarness.FixtureGrammars` (a name `[WORD]`, an adjective `[ADJ]`, a verb bound to a
  slot), and `ContentMutationGen.WithEffects` (a rehashed copy with the effect table, so the loader's effect checks stay under mutation). The host-turn bundle gives every agent
  the `fuzz.ask` transition. The game's host files are not mutated structurally (the loader does not read them), and the speech clips are linked, not copied, into each damaged copy.

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
  (`tengo 1 grande queso`, `tengo 1 buena espada`) in `tests/Vocaterra.Nlp.Tests/Response`.
- **`Accents.Strip` and `SpanishPhoneticKey.Compute` threw `ArgumentException` on a lone surrogate** (`Vocaterra.Nlp.Language`; the stage-1 known finding about
  ill-formed UTF-16). Both reached `string.Normalize`, which rejects ill-formed UTF-16, and a recognizer or a keyboard can deliver one. An internal
  `Utf16.ReplaceLoneSurrogates` now replaces each lone surrogate with U+FFFD at the entry of `TextNormalizer.Normalize` (which already treated it as
  a separator), `Accents.Strip` and `SpanishPhoneticKey.Compute`; valid pairs are untouched. The stage-1 generator guard is gone: the noise
  generator now splices the adversarial token in last and verbatim, and O1 feeds the raw text to all three entry points. Found by O1 (seed
  `cA-a9vcyA8R1`). Regression: `IllFormedUtf16Tests` in `tests/Vocaterra.Nlp.Tests/Language/Normalization`.
- **`SpanishG2p.Phonemize` threw `ArgumentException` on a lone surrogate** (`Vocaterra.Nlp.Phonology`). `G2pNormalizer` handed the text to `string.Normalize`
  as well; it now reads a lone surrogate as a word separator, like any other non-letter. Found by O8 (seed `4Y4ZiHH2bn31`). Regression:
  `IllFormedUtf16Tests` in `tests/Vocaterra.Nlp.Tests/Phonology`.
- **A lexicon entry without a `features` property, and a lexicon file without `entries`, made the content loader throw `NullReferenceException`**
  (`Vocaterra.Nlp.Language`). A missing property arrives as null although the records are non-nullable (`RespectNullableAnnotations` only rejects an explicit
  null); `LexiconSchema.Validate` dereferenced `Features` and `Lexicon.Load` enumerated `Entries`, so a damaged `lexicon/*.json` escaped the loader's
  `ContentValidationException` as an unhandled `NullReferenceException`. Both now fail with a `FormatException` that the loader reports as a
  problem naming the file. Found by O15 (seed `0003TzidR6n1`, a flipped bit in a key of `lexicon/seed.json`). Regression: four new cases of
  `LexiconTests.InvalidDocumentsAreRejected` in `tests/Vocaterra.Nlp.Tests/Language`.

Found when the imported lexicon was added (M17a). The seeds belong to the lexicon of that moment and do not reproduce with the final one; the regression tests do.

- **A subject pronoun was checked against the imperative readings of its verb** (`Vocaterra.Nlp.Language`, O4/O5). Imperatives are spelled like the third person
  (`habla`, `come`, `está`), so `tú habla` agreed with `tú`, and the correction of `ella está alta` was `eres` (the first reading of `está` is the imperative
  of `estar`, second person). The person check and the copula check now use the declarative readings. Found by O4 (seed `dU3ctY0Mvpx9`, a ser/estar mutation) and
  O5 (seed `4ybQyIqiaD61`). Regression: `AmbiguousReadingTests.AnImperativeThatIsSpelledLikeTheThirdPersonDoesNotExcuseAPersonMistake` and
  `TheExpectedCopulaFormComesFromTheDeclarativeReadingNotFromTheImperative` in `tests/Vocaterra.Nlp.Tests/Language/Analysis`.
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
  the reading with the fewest errors, and every check runs on that reading (design in [docs/nlp/PIPELINE.md](../../docs/nlp/PIPELINE.md#4-linguistic-analysis-d0)). Regression:
  `HomographSentencesTests` (47 correct sentences, 20 more under the grammars of the bundle, 26 incorrect ones that keep their mistake, the `cuánto cuesta` family) and `ReadingResolverTests` in `tests/Vocaterra.Nlp.Tests/Language/Analysis`, and the
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

Found and fixed in M19f:

- **A leading adverb was taken for an optional `[WORD]` name, and its tense mistake was lost** (`Vocaterra.Nlp.Language`, O5, CsCheck seed `1diyRPH9lxJ1`).
  The pattern `[WORD:item]? [VERB] [ADJ]? [VERB:sufrir]` matched the mutated `ayer tengo sufrir` with `ayer` as the name, because a name took any word of
  letters. A name carries no reading, so the past-time marker of `ayer` vanished, the analyzer reported no `conj.tense`, and the recast had nothing to
  correct (it repeated `tengo`; the recast builder and the morphology were right, `tuve` is generated). A word whose readings are all closed-class
  (article, numeral, determiner adjective, pronoun, preposition, conjunction, adverb, interjection) is no name now, so the sentence matches no grammar and
  the mistake is reported on `tengo` (`tengo` -> `tuve`). Regression: `WordTokenAnalysisTests.AnOptionalNameDoesNotAbsorbAPastTimeAdverbAndItsTenseMistake`
  (`ayer tengo sufrir`); `WordTokenTests.WordDoesNotTakeNumbersOrPunctuation` no longer takes a spelled numeral (`dos`) as a name.

Content fix (stage 1 follow-up): **`cuál` is now in `content/game-v1/lexicon/seed.json`** (a pronoun, like `qué` and `quién`), so the correct
`ask_price` utterance `cuál es el precio de ...` no longer gets `vocab.unknown`. The bundle was re-hashed (`b076558f...efb`), the pinned hash in
`ManifestTests` and the three `contentHash` lines of the end-to-end golden file were updated (the committed golden manifest is untouched and stays
synthetic), and O3 now realises the `ask_price` grammar (about 30 sentences per run).

- **O14's `hostturn:none` coverage was a flake, not a pipeline bug** (M29e; the generator, `Generation/PipelineGen.cs`). A host turn was drawn
  with probability 1/3 and an unknown agent with 1/5, so `hostturn:none` (a host turn for an agent id the bundle does not know) was 1 case in 15:
  a mean of 20 in 300 iterations with a standard deviation of 4.3, against a minimum of 10 (about 2.3 standard deviations). Five measured runs
  gave 8, 11, 15, 17 and 20, and the merge gate of the S2 work failed on 8. The oracle is unchanged and the minimum is unchanged. The generator now
  draws the agent after the host-turn flag: a host turn goes to the blacksmith or to an unknown agent, one in two each (expected 50 in 300, standard
  deviation 6.5, so the minimum is about 6 standard deviations below the mean). The other draws keep their values. Measured over 20 runs after the
  change: `hostturn:none` mean 53.6, minimum 43; before (15 runs): mean 18.9, minimum 12. Every other asserted key was checked with the same
  arithmetic and sits at least 4 standard deviations above its minimum (`agent:none` now counts the unknown agents of host turns too: about 90).

- **The replay test's `effects` region was a flake, not a pipeline bug** (M29h; the generator, `Generation/PipelineGen.cs`, and the path choice in
  `PipelineFuzzTests.ChangingOneRecordedOutput_MakesTheReplayerNameThatPath`). The S2 gate's merge gate failed once with "No mutation reached the
  effects part of the envelope". The effects part exists only when the verdict is communicated and its intent is `buy_item`, `cast_spell` or
  `sell_item` (the bundle's effect table), so only 37 of 300 cases had it; and the path was drawn uniformly over every compared leaf, of which
  the three effect leaves are a few percent. The merge gate failed on this assertion once. Measured before (15 runs): `region:effects` minimum 2,
  mean 5.4, standard deviation 2.4, so the minimum of 1 was about 1.7 standard deviations below the mean, with a failure in roughly one run in
  a hundred. The fix has two parts, and the
  oracle and the minimum (1 per region) are unchanged. The path is drawn in two steps, a region uniformly among the regions the envelope has,
  then a leaf inside it, so a rare region gets the same share of the mutations as a common one. The generator draws, last of all, whether one case
  in four is aimed at a prompt that allows an effect intent with a correct sentence of it; only the prompt, transcript, source and phoneme flag of
  those cases change, and every earlier draw keeps its value. After (15 runs): `region:effects` minimum 30, mean 47.4, standard deviation 7.3; the
  other regions' minimums were record 163, decision 151, response 117, context 158 and repair 33, all far above 1. Over five runs of the other
  pipeline test every asserted key stayed well above its minimum of 10 (`hostturn:none` minimum 50; the lowest minimum was `hostturn:step`, 38).
  The count of mutations with no valid replacement (skipped, not failed) rose from 24 to 119 in one run each; most likely a null leaf, such as an
  absent repair, has no replacement that deserializes. This was not measured by region.
- **The same assertion failed again after R02: the pipeline's bundle lacked the generator's words** (`Oracles/PipelineHarness.cs`, `BuildCopy`). CI on
  `feature/game_refactor` (run 40776714320) failed with "No mutation reached the effects part of the envelope". `EffectsBundle` and `HostTurnBundle`
  were built from the light `TempBundle` copy, without the imported lexicon, while the generators draw their nouns from `Bundle`, the full bundle; so
  a correct sentence of an effect intent's grammar with an imported noun (`sí él tiene el cuchillo`, `las carreteras por favor`) resolved to
  `unknown` and was not communicated. Measured before: 66 of 85 aimed cases unknown, 7 of 300 cases with effects, `region:effects` 0, 1, 2 and 6 in
  four runs (main the same). The fixture bundles are now built from the full copy; the oracle and the minimum are unchanged. After: every aimed case
  communicated, 81 of 300 cases with effects, `region:effects` 42.

Found and fixed in M59:

- **The accent-variant choice made `te estas` the noun `té` and the verb `estás`** (`Vocaterra.Nlp`, `AccentVariantChoice`; found by O3's accent-dropping fact, CsCheck seed
  `e8VfxkxpBVH8`, skeleton `[PRON] [ADV]? [VERB:estar] [ADJ]`, `te estas relacionado`, once M59 checked a noun before its verb). The choice decides every word of a pass at
  once: `te` against the demonstrative `estas` (no verb after it, so the noun `té`) and `estas` against its neighbours (no noun after it, so the verb `estás`). Before M59 the
  misreading said nothing; with M59 `té` became a bare noun subject before a verb of the second person, and the sentence without its accent gave a `conj.person` that the
  accented one did not. A pronoun before a verb now keeps its reading when the next word is a verb once accented. Regression:
  `AccentVariantSentencesTests.APronounBeforeAWordThatIsAVerbOnceAccentedStaysThePronoun` in `tests/Vocaterra.Nlp.Tests/Language/Analysis`.
- **`pasado mañana` after a plural noun was read as an adjective of the noun** (`Vocaterra.Nlp.Es`, `SpanishAgreementChecker`; found by the game's V10, and by O3 on
  the M57 fixture grammars, CsCheck seed `22513329`, `nosotras producimos diecisiete empieces pasado mañana`, `noun.number` expected `pasados`). After a time noun the same
  `pasado` was a past marker (`voy a ir el domingo pasado mañana` gave `conj.tense` on `voy`). The `pasado` of `pasado mañana` is neither now. Found beside it, by the
  regression tests: on the bundle's lexicon `la semana pasada` was no past marker (`compro pan la semana pasada` gave nothing), because `pasada` is also a noun and the
  resolver's charitable fallback chose that reading to hide the slip; a form of `pasado` after a time noun is matched as written now. Regression:
  `NearFutureTenseTests.PasadoManana...` in `tests/Vocaterra.Nlp.Es.Tests/Analysis`.

Found and fixed in M62:

- **The first build of M62 read a list of nouns without its commas as a colour before its noun** (`Vocaterra.Nlp`, `ReadingResolver`; found by the Spanish identity
  baseline, not the fuzz suite: `fuzz-correct-0918`, `DoRaDo, bIbLiAs, aCtUaCiOnEs, uNoS`, gave `word_order` and agreement errors on `dorado biblias`). The normalizer
  drops commas, so `una rosa, pan y agua` reads as `una rosa pan y agua`. A colour that is also a noun is now the adjective before a noun only where it and the
  determiners before it agree with that noun. Regression: `PrenominalAdjectiveTests.AListOfNounsIsNoSlipOfOrder` in `tests/Vocaterra.Nlp.Es.Tests/Analysis`.
- **The rule that reads an adjective-and-noun word after a noun as that noun's adjective (added for `las manzanas rojas son caro`) fired where a mutation had made the
  phrase disagree with itself, and on a word the grammar binds as the verb** (`Vocaterra.Nlp`, `ReadingResolver`; found by O4, O5 and O3 at five times the default
  depth: `22 emperador americano` gave `noun.number` on `americano` beside the expected one on `emperador` (`MutationCase { Kind = NounNumber, Seed = 7228 }`), so did
  `1 martillos chinos` and `20 polvo mate`; `una gana media encantado` under `[DET] [NOUN:item] [VERB] [ADJ]` (seed `bgaIBqtK8hv3`) and `la tradición limpia levantado`
  (seed `95cHVPFveJD1`) read `media` and `limpia` as adjectives and gave `gender.agree` on the predicate). The word must now agree with the determiners as well as the
  noun, and have no finite verb reading. Regressions: `PredicateAdjectiveTests.APhraseThatDisagreesWithItselfKeepsOneSlip` and
  `AWordThatCanBeTheVerbStaysTheVerbUnderItsGrammar`. Before that, the project's first runs failed only where the realiser had not yet learned the new rules (O3's
  digest, and O5 on seed `f8C3x9UYGMz6`).

Found and fixed in M62b (by the gate of the game's V15):

- **The realiser took a drawn name that is also a lexicon noun for a name where the grammar ties `[WORD]` with `[NOUN]`** (the realiser; O3, CsCheck seed
  `aeG5w1c3Hvh2`: the random pattern `[ART]? [ADJ:item] ([WORD:qty]|[NOUN]|[ADJ] [VERB])?` gave `un japón Dejo`, and the analyzer reported `word_order` on `japón
  dejo`). Older than M62 and no part of its colour rule (`japón` is no colour): the analyzer of `76f52f8` reads the sentence the same way. A tied parse keeps every
  reading (`ReadingResolver`, "Grammar bindings"), so the analyzer reads `dejo` as the noun, and after an adjective that goes after its noun the reading resolver makes
  it the head; only a grammar that binds the word as a name alone (`[ART]? [ADJ:item] [WORD:qty]`) leaves it a name. The analyzer is right; the realiser now re-rolls a
  drawn name that is a lexicon noun right after a determiner or an adjective that belongs to no noun (`skip:name-read-as-noun`). Regression:
  `NameReadAsNounTests` (the recipe of the seed now realises a sentence the analyzer finds correct; it built `un japón Dejo` before). No digest moved.

Found and fixed in M64:

- **A feminine noun that takes `el` was masculine for the predicate check** (`Vocaterra.Nlp.Es`, `SpanishAgreementChecker.PhraseGender`; found by O4 at five times the
  default depth, CsCheck seed `clQQ50b0dLj2`, `MutationCase { Kind = VocabUnknown, Seed = 8144391 }`; older than M64, the same at `951e7c8`). The pattern
  `(sí)? [ART]? [NOUN:object]? [VERB:ser] tan [ADJ] como [ART]? [NOUN]` gave `el ala es tan sincera como saeta`; the mutation's non-word in place of `saeta` unbinds the
  grammar, and the lexicon lists `ala` with both genders, so the article `el` decided the masculine and the analyzer gave `gender.agree` `sincero` beside the expected
  `vocab.unknown`. A noun of the pack's list of feminine nouns that take `el` (`ala`, `agua`, `hacha`...) is feminine now, whatever its article. Regression:
  `PredicateAdjectiveTests.AFeminineNounThatTakesElIsFeminine` in `tests/Vocaterra.Nlp.Es.Tests/Analysis` (`el ala es muy sincera` gave `sincero` before).

Found and fixed in M69:

- **O5, a recast that kept a gender error** (`Vocaterra.Nlp`, `RecastBuilder.GenderAfter`; M67b's seed `deXmFwx4GzL4`, reproduced at M70; older than M69). The
  random pattern `[NOUN]? [NUM:item] [DET] [NUM:qty] [NOUN]?` gave `miedo uno tu uno exposición`, the mutation `exposiciones` (`noun.number`), and the recast `miedo un
  tu una exposición`: a digit 1 is spelled after the gender of the noun it quantifies, looking through the adjectives before that noun, and a numeral or an article
  between stopped the walk, so the gender of the next word (the possessive `tu`, none) decided the masculine. The walk now passes over determiners, numerals and
  articles too: the recast is `miedo una tu una exposición`, which re-analyses clean. Regression: `RecastBuilderTests.DigitsAreSpelledOutBeforeSpeaking` (`tengo 1 tu 1
  espada` gives `una tu una`). The seed no longer replays the same case on M69's suite (its skeletons moved the draws); the case was checked with its pattern and text.
- **O3, the ñ typed as n kept a reading the typed accent rules out** (`Vocaterra.Nlp`, `Lexicon.Lookup`; CsCheck seed `2cqJbc9y05h3`, the fact
  `CorrectSentences_WithAnNTildeTypedAsNWhereThatSpellingIsNoWord_AnalyzeAsTheSentenceWithIt`; found once by V21's gate, older than M69: M70's fold). The pattern `[ADJ]?
  [ART]? [VERB:dañar] [NOUN:item]` gave `delicado dañó hechos`, clean, and typed with `n`, `delicado danó hechos`, `word_order` `danó delicado`: the unmarked spelling
  `dano` stands for `daño` (also a noun) and `dañó`, and the fold gave the readings of both, so `delicado` was read as an adjective put before the noun `daño`. An
  accent the writer typed is a choice, so a spelling typed with an accent now keeps only the forms with that accent where it was typed (`danó` is `dañó`); one with no
  accent (`dano`) keeps every form, as before. Regressions: `UnmarkedSpellingTests.ASpellingTypedWithAnAccentIsTheFormWithThatAccent` and
  `TheTypedAccentChoosesAmongTheFormsOfOneUnmarkedSpelling` in `tests/Vocaterra.Nlp.Es.Tests/Lexicon`.

- **O4, `gustar` in a form of two tenses after a past marker** (`Vocaterra.Nlp.Es`, `SpanishAgreementChecker.CheckLikingVerb`; CsCheck seed `cTAdl0hRGj58`,
  M69's runs; older than M69, the rule of M70). The catalog's `(ayer|hoy)? (sí|no)? me [VERB:gustar] (mucho)? [ART] [NOUN:item] ...` gave `ayer no me gustaron unos
  aparatos de león hoy`, and the person mutation `gustamos`, expected `gustaron`; the analyzer gave `gustan`, because it took the first reading of `gustamos`, the
  present, though the preterite is one too. After a past marker the preterite reading is taken now. Regression:
  `LikingVerbTests.AFormOfTwoTensesAfterAPastMarkerTakesThePreterite`.
- **The realiser drew a form of `podar` before an infinitive as correct** (the realiser; CsCheck seed `2DQ_Pma1Hfsa`, O3, M69's runs). The random pattern
  `[NUM:item] [VERB:podar] [VERB] [ART]` gave `21 podo pasar unos`, which is no Spanish, and which the analyzer reads since M69 as `poder`'s slip (`conj.irregular`
  `puedo`). The realiser now asks the pack's own rule (`IAnalysisRules.CallsForSlip`, through `LexiconCatalog.ReadsAsSlip`) and re-rolls a word that the words around it
  make a learner's slip (`skip:slip-in-context`, under the default budget). It is a sentence the realiser got wrong, not one an oracle left out.

Found and fixed in M66 (its own run of O3 at five times the default depth):

- **A form of a modal that is also another verb's was taken for the modal** (`Vocaterra.Nlp`, `ReadingResolver.ModalComplement`, and `Vocaterra.Nlp.Es`,
  `SpanishAgreementChecker.ModalComplementIndex`; CsCheck seed `c5E0ikVTT_s3`): the random pattern `[ART] realmente? [VERB] [NOUN]? [ART]` gave `los realmente
  podamos estudio unas`, with `podar` drawn (`podamos`, we prune), and M66's new rule read `podamos` as `poder`'s subjunctive and `estudio` as the slip for `estudiar`
  (`conj.tense`). A modal is now a finite verb whose readings of the indicative are all that verb's (`podemos`, `poder`'s indicative and `podar`'s subjunctive, still
  is). Regression: `ModalComplementTests.AFormThatIsAlsoAnotherVerbsIsNoModal` in `tests/Vocaterra.Nlp.Es.Tests/Analysis` (`podemos estudio` is still flagged).

## Known, deliberately not fixed

Findings that are content gaps or design limits rather than defects of one stage. None changes behaviour here; the generators step around
them explicitly (each is a skip or a restriction in the generator, counted in the diagnostics) and the oracles stay strict.

- **What the part-of-speech resolver does not do (M18).** Two verbs that share a form are one class: `fue` and `fueron` are `ir` and `ser`, so a copula check still sees the copula, and
  the generators re-roll `ir` forms followed by an adjective (`ella fue alta`). A sentence that is a mistake under one reading and correct under another, with nothing in the neighbouring
  words or the grammar to prefer either, is read the charitable way (`ayer ... intenciones parte`, where `parte` is a noun or a verb).
  The closed classes of the generators (adverbs, prepositions, conjunctions, interjections) still leave out words that are also a verb form or a noun (`como`, `para`); the resolver reads
  `nosotros como` as the verb and `nosotros comemos como siempre` as the conjunction, and both are covered by `HomographSentencesTests`, not by the generators. Nouns whose form two noun
  lemmas share (`ratos`: `rato`, `rata`) stay out of the pools (94 lemmas), because the correction the analyzer names depends on which lemma it takes. So do, since M58, the nouns a
  form of which is also the regular cell of an irregular verb (`costa` for `cuesta`, the stem change missed; `cabo`, `costa`, `jugo` and `mente`, four lemmas,
  `nouns:excluded-also-a-learners-slip-of-a-verb`): the analyzer reads such a word as that verb, with `conj.irregular`, where a verb is expected (after a subject pronoun or a
  clitic, after `cuánto`, after the noun of a phrase that opens the sentence) and as the noun elsewhere, and the generators place a noun by the pattern, not by those rules.
  `LearnerSlipTests` in the Spanish pack's tests covers them. The plural slips of M58 (`pans` for `panes`) are words the lexicon lacks, which no generator writes: the
  non-word of the `vocab.unknown` mutation is at least three edits from every form, and an invented name ends in a vowel.
- **`CanonicalJson` writes a lone surrogate as an escaped U+FFFD**, and a real U+FFFD as the character itself, so a transcript with a lone
  surrogate does not survive serialize, deserialize, serialize byte for byte, and a replay envelope for it does not round-trip. Recognizers return
  well-formed text. O16 and O14 make transcripts well-formed before the JSON round trip (O14 still runs the pipeline and replays the in-memory
  envelope for the raw text). `Vocaterra.Nlp.Core` is final, so the fix (write U+FFFD as an escape, or reject at serialization) is a maintainers' decision.
- **Measurements with the imported lexicon (M17a).** Lexicon file `wiktionary-es.json`: 4 718 227 bytes (limit 15 MB); first load of the bundle with hash verification in a
  fresh process 217 to 267 ms, repeated in one process 114 to 213 ms (before: 8 ms); fuzz project alone 18.7 to 20.3 s (before 17.1 to 18.2 s); whole solution 24.6 to 25.5 s
  (before 20.5 to 21.0 s). The same numbers at 5 000 lemmas (6.6 MB, first load 333 to 373 ms) are in [docs/nlp/LEXICON.md](../../docs/nlp/LEXICON.md); 5 000 fails the 300 ms load gate.
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
- **Found at M62, older than M62, and left for a decision** (seven runs of O3 to O5 at five times the default depth, three of them after the last fix of M62, found
  these besides the M62 bugs above; every run of the project at its default depth passed). The analyzer gives the same analysis of each sentence on `feature/village` at `76f52f8`, before M62 (checked with the pattern as the grammar), and no rule of
  M62 reads any of their words; the realiser's new draws only made the seeds reach them. Two of them, the `conj.tense` mutation beside `porque` (`2ldq2rIKLrG2`) and
  the `conj.person` mutation of `haber` after a noun subject (`b3HSKSLCsL_1`), are guarded since M65 with the owner's yes (under the narrowed oracles above).
  - *O5 (and O3 on the sentence before the mutation), a subject pronoun before a question word* (CsCheck seed `8zKtIhOY3951`, `MutationCase { Kind = WordOrder, Seed =
    19287181529 }`): the pattern `[PRON] [ADV]? [VERB] [DET]? [NOUN:item] [ADJ]?` gives `ellas adónde están un detenido lento`. The realiser agrees `están` with `ellas`;
    M56's check of `estar` against the phrase after it starts its clause after `adónde` and gives `conj.person` `está` (on the sentence as generated too, which the
    word-order mutation then exposes). Either the realiser models the clause opener (a generator change, a widening) or the check keeps a subject pronoun before the
    question word (an analyzer change); it is left for the orchestrator, and filed as M63. O3 found the same with `ellas dónde están teme` (seed `c5SlpISRmTZ3`).
    **Fixed in M66** (M63 folded in): the check keeps a subject pronoun before the question word as the subject, and the realiser agrees the verb after `qué` with it
    too; both seeds pass, and `InvertedSubjectTests.TheFuzzSeedsOfM63AreClean` in `tests/Vocaterra.Nlp.Es.Tests/Analysis` holds the two sentences.
- **Found at M64, older than M64, and left for a decision** (seven runs of O3 to O5 at five times the default depth, the last three after the fix of `el ala` above
  and all passing; every run of the project at its default depth passed). *O5, the recast of a `word_order` mutation where the grammar can bind the moved adjective as a name* (CsCheck seed `2tpLGUOSqj41`, `MutationCase { Kind =
  WordOrder, Seed = 17287070985274212 }`): the random pattern `([WORD:item]|[ADJ:qty] [PRON]|[NOUN:spell])? [ADJ]` gives `militar necesaria`; the mutation makes
  `necesaria militar`, and its recast is `necesaria militar` again instead of `militar necesaria` (with the order slipped, `[WORD:item] [ADJ]` can bind `necesaria`
  as a name and `militar` as the adjective). It fails the same way on `feature/village` at `951e7c8`, before M64, and no rule of M64 reads its words; it is left for the
  orchestrator with M62's findings above. The guard would be to skip `word_order` beside a `[WORD]` slot, a narrowing; the owner, 2026-11-01: leave it recorded.
  M69's run at five times met the same family on O4 (CsCheck seed `fSMdah3kOSd7`, `WordOrder`): the random pattern `[ART]? ([NOUN:item] [ADJ]|[WORD:qty]
  [NOUN:spell]) [NOUN:thing] [ART] [VERB:traicionar]` gave `el pete fantástico kilos una traicionas`; the mutation `el fantástico pete` lets the alternative `[WORD:qty]
  [NOUN:spell]` bind `fantástico` as a name, which has no reading, so no `word_order` is reported. No rule of M69 reads it (the grammar matcher binds the name); left
  recorded with the family, and the run was made again once, as for `2tpLGUOSqj41`.
- **Found at M65, older than M65, and left for a decision** (a run of O4 after M65's last build; none of M65's four runs of the project met it, and it fails the
  same way on the suite before M65: the build of `feature/village-followups` at `1ff8a5a`, whose core, fuzz suite and bundle are those of `9dfa08b`). *O4, a
  `word_order` mutation that breaks the match where the grammar decided a reading* (CsCheck seed `8yc522XLB_W2`, `MutationCase { Kind = WordOrder, Seed =
  670401880765 }`): the random pattern `([ART] [NOUN:item]|[VERB]|[VERB:encargar]) [ADJ] [NOUN:qty]? ([VERB:desfilar:spell] [ADJ]|[ART])?` gives `la joya unida rica
  desfilaron perdido`, where the grammar binds `rica` (a noun and an adjective) as `[NOUN:qty]`, a lone noun whose verb the realiser draws freely. The mutation makes
  `la unida joya rica`; `unida` is no noun, so the grammar no longer matches, `rica` reads as the adjective of `joya`, and M59's check gives `conj.person` `desfiló`
  beside the expected `word_order`. The same family as M65 (c) and `2tpLGUOSqj41`: either the realiser extends M62's `noun-reads-as-adjective` re-roll past the
  adjectives after the head (a change of O3's draws), or the mutation keeps only a case that still matches its grammar (a narrowing); it is left for the orchestrator.
- **Found at M71, older than M71; fixed in M69** (the widening of O3 above, (c)). *O3 never realised `buenas tardes`.* From
  400 fixed recipes the realiser gives `(buenos|buenas) (día|días|tardes|noches)` as `buenos días` (196) and `buenas noches` (164) only, and 40 recipes give nothing;
  the analyzer finds `buenas tardes` correct. `tardes` is also a form of `tardar`, and 163 of the grammar's draws are re-rolled as `literal-verb-subject`: M59's
  re-roll of a literal verb form that disagrees with a noun phrase before it appears to take `buenas` for that phrase, whose head is `tardes` itself
  (`SentenceRealizer.PreverbalPhrase` does not ask whether the head is the literal it is checking). Leaving such a literal alone would widen O3's draws and may move
  the sentence stream digest; it is left for the orchestrator.
- **Found at M67b, reproduced at M70; fixed in M69** ("Found and fixed in M69" above; M67b's runs on a widened O4 that was not merged found two; M70 ran each seed on its own branch, after the
  merge of V20). *O5, a recast that keeps a gender error* (CsCheck seed `deXmFwx4GzL4`, `MutationCase { Kind = NounNumberPlural, Seed = 3622186191696 }`): the
  random pattern `[NOUN]? [NUM:item] [DET] [NUM:qty] [NOUN]?` gives `miedo uno tu uno exposición`; the mutation makes `exposiciones` (`noun.number`, expected
  `exposición`), and the recast `miedo un tu una exposición` still has `gender.agree` `una` on the first `un` (the numeral before the possessive is written as
  the masculine article). It reproduces with `CsCheck_Seed=deXmFwx4GzL4` and the `RecastLoopFuzzTests` alone; no unseeded run of M70 met it (four runs, one at
  five times). The O4 seed of the same report, `3isZGy3IeUc8` (`vaya sentir`, `ir` with a bare infinitive), does not reproduce on M70's branch.


## Adding to the suite

`PipelineHarness` exposes what an oracle needs: `Bundle`, `NewServices` (scripted word recognizer, optional fake phoneme recognizer, fake embedder,
silent synthesizer), `NewPipeline`, `NewContext`, `Prompt`, `Clip`, `Words`, `Grammars`. A new oracle gets its own `*FuzzTests.cs`, a generator under
`Generation/` if the existing ones do not fit, an entry in `KnownTolerances` for every exception it waves through (with the reason), an iteration
default in `FuzzOptions` and a row in the table above. A bug it finds is fixed in the owning module with a regression test there, and listed above
with the CsCheck seed.
