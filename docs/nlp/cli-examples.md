# The demo CLI, with worked examples

How to run the demo CLI (`tools/Vocaterra.Nlp.Cli`) with and without the speech models, worked runs of every intent and error tag,
replay logs and the content builder. Run every command from the repository root.

## Running the demo CLI

`tools/Vocaterra.Nlp.Cli` runs one utterance through the whole pipeline and prints the verdict, the agent's lines, effects and the repair step.

### Without models: `--transcript`

Without `--models` the word recognizer is scripted from the text you give it, so no model and no audio are needed. The bundle is the game's, `content/game-v1`
(the core's tests run on it too); the prompt `villager-open` is the open prompt of the villager. This is the worked example from the design, in the words of
that bundle: the player asks for "yo quiere dos espada", with a wrong verb form and a noun left singular after a number.

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1
```

```text
{"intent":"request_item","slots":{"item":"espada","qty":"2"},"communicated":true,"correctness":0.6,"errors":[{"tag":"conj.person","span":{"start":3,"length":6},"expected":"quiero","actual":"quiere"},{"tag":"noun.number","span":{"start":12,"length":6},"expected":"espadas","actual":"espada"}],"pronunciation":[],"confidence":1,"resolvedBy":"Grammar","versions":{"contentHash":"<the bundle hash>","modelHashes":{"asr":"scripted-transcript"}}}
agent: Claro, «yo quiero dos espadas». Muy bien.
agent: Aquí tienes.
decision: rating Good, learner skills 3, feedback items 2
```

The first line is the verdict (the player was understood, and two grammar errors are tagged with the corrected forms); the rest is
what the host does with it. [docs/nlp/pipeline.md](pipeline.md) explains every part. The `contentHash` is the hash of the bundle, so it changes with every
edit of the game's content; the examples below show it as a placeholder or leave it out. The design document's own sentence, "yo tiene dos espada", needs a
prompt that allows `tener` with an item and a quantity; no prompt of the game has one, so it runs in the tests (`TheDesignDocumentsWorkedExampleGivesExactlyConjPersonAndNounNumber`),
which build that prompt in the test, and gives exactly `conj.person` and `noun.number`.

Other prompts to try (the bundle's prompts are listed in `content/game-v1/prompts/prompts.json`):

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero agua por favor" --prompt stall-need --content content/game-v1
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "hola" --prompt lesson-hello --content content/game-v1
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "bla bla" --prompt villager-open --content content/game-v1 --repeat not_communicated=1
```

The conversation context can be set from the command line: `--seed <n>` (world seed), `--now <unixSeconds>`, `--agent-state <id>`, `--counter <n>`
(interaction counter) and `--repeat <tag>=<n>` (how often an error was repeated in a row before this utterance). `dotnet run --project tools/Vocaterra.Nlp.Cli -- --help` prints the
usage. Exit codes: 0 success, 1 failed evaluation or a replay that differs, 2 a malformed command line.

### With models: `--wav`

Real recognition needs a model and a recording. The small Spanish Vosk model is enough for constrained prompts (the answer-set prompts of the stall
and the lessons):

```bash
scripts/fetch-models.sh --list                              # which models are present, missing or mismatching
scripts/fetch-models.sh vosk-model-small-es-0.42            # download, verify the SHA-256 of the archive and of every file
export VOCATERRA_MODELS_DIR="$PWD/models"                         # where the tests and tools look for models
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --wav clip.wav --prompt stall-offer --agent-state offering --content content/game-v1 --models models
```

`clip.wav` is any WAV file (16-bit PCM or 32-bit float, mono or stereo, any sample rate; the audio front end converts it to 16 kHz mono). Models are
downloaded, never committed; `models/manifest.json` pins every file, and [models/README.md](../../models/README.md) describes the manifest.
`--models` needs `--wav`. The CLI picks Vosk for constrained prompts; for open prompts it uses Parakeet when
`sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` is present in the models directory and falls back to Vosk otherwise; it adds
pronunciation scoring when `wav2vec2-xlsr-53-espeak-cv-ft` is present. The last two do not have an ONNX export in the repository yet
(see [known gaps](known-gaps.md)). With `VOCATERRA_MODELS_DIR` set, `dotnet test` also runs the model-gated tests.

## More examples

These are real runs against `content/game-v1`. Each verdict is abbreviated: `…` marks the cut fields (`pronunciation`, `confidence`,
`resolvedBy` and `versions`, unless an example keeps them), and the closing `decision:` line is left out. Error spans count characters of the
normalized text. The bundle changes with the game's lessons, so the exact lines an agent says may change with it; the shape of the output does not. Twelve more
follow below: the other intents, five more error tags, an exact answer, the repair ladder and a replay log.

A correct utterance is understood in full: the slots are filled and there are no errors.

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero dos panes" --prompt villager-open --content content/game-v1
```

```text
{"intent":"request_item","slots":{"item":"pan","qty":"2"},"communicated":true,"correctness":1,"errors":[],…}
agent: Aquí tienes.
```

The bundle's effect table is empty (the game derives its effects from its own host files), so the CLI prints no `effect:` line for it. The core's effect
curves are covered by tests that build a table in the test (`EffectsFixtureTests`).

A slip does not stop the conversation. `una pan` is a `gender.agree` error, so the agent recasts the sentence correctly and still hands the bread over:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero una pan" --prompt villager-open --content content/game-v1
```

```text
{"intent":"request_item","slots":{"item":"pan"},"communicated":true,"correctness":0.85,
 "errors":[{"tag":"gender.agree","span":{"start":7,"length":3},"expected":"un","actual":"una"}],…}
agent: Claro, «quiero un pan». Muy bien.
agent: Aquí tienes.
```

A constrained prompt expects one of the phrases of its answer set. A near miss still counts when the fuzzy match is close: "aguas" for "agua" is accepted, and
`resolvedBy` and `confidence` show how:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero aguas por favor" --prompt stall-need --content content/game-v1
```

```text
{"intent":"request_item","slots":{"item":"agua"},"communicated":true,"correctness":1,"errors":[],…,"confidence":0.9523809523809523,"resolvedBy":"Fuzzy",…}
agent: Toma la botella. El agua está en el río.
agent: ¿Quieres pan?
```

A word the lexicon does not know is a `vocab.unknown` error, and when no intent can be resolved the player is not understood. The agent
asks again, and the first repair step is `AskAgain`:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero una zanahoria" --prompt villager-open --content content/game-v1
```

```text
{"intent":"unknown","slots":{},"communicated":false,"correctness":0.6,
 "errors":[{"tag":"vocab.unknown","span":{"start":11,"length":9},"expected":"","actual":"zanahoria"}],…}
agent: No te entendí. ¿Me lo dices otra vez?
repair: AskAgain ¿Cómo?
```

`--repeat <tag>=<n>` says how many times in a row the learner had made that error before this utterance (the counts a host carries
from `ConversationContext.Next`; the pipeline counts this utterance itself). A host keeps that count between utterances; the CLI takes it
from the command line. The feedback policy (`content/game-v1/feedback/feedback-policy.json`) answers the first offence with a recast, the second
and third with a hint and the fourth and later with an explicit correction. The same `una pan`, as the second and as the fourth offence
(so `--repeat gender.agree=1` and `=3`):

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero una pan" --prompt villager-open --content content/game-v1 --repeat gender.agree=1
```

```text
{"intent":"request_item","slots":{"item":"pan"},"communicated":true,"correctness":0.85,
 "errors":[{"tag":"gender.agree","span":{"start":7,"length":3},"expected":"un","actual":"una"}],…}
agent: Aquí tienes.
agent: ¿una? Piensa: ¿el o la?
```

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero una pan" --prompt villager-open --content content/game-v1 --repeat gender.agree=3
```

```text
{"intent":"request_item","slots":{"item":"pan"},"communicated":true,"correctness":0.85,
 "errors":[{"tag":"gender.agree","span":{"start":7,"length":3},"expected":"un","actual":"una"}],…}
agent: Aquí tienes.
agent: Se dice así: «un».
```

`--agent-state <id>` starts the agent in another state of its machine (the shopkeeper in `content/game-v1/agents/shopkeeper.json` has `idle`, the
default, `asking`, `offering` and `done`; an unknown id is an error). The prompt `stall-offer` accepts a yes or a no. In the default state the
shopkeeper has nothing to confirm, so a yes is understood by the grader but not by the agent:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "sí, por favor" --prompt stall-offer --content content/game-v1
```

```text
{"intent":"confirm","slots":{},"communicated":true,"correctness":1,"errors":[],…,"resolvedBy":"Exact",…}
agent: No te entendí. ¿Me lo dices otra vez?
```

Once the shopkeeper is `offering` bread, the same words finish the trade, and once it is `done` the agent is already saying goodbye. The verdict does not
change, because it depends only on the utterance:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "sí, por favor" --prompt stall-offer --content content/game-v1 --agent-state offering
```

```text
{"intent":"confirm","slots":{},"communicated":true,"correctness":1,"errors":[],…,"resolvedBy":"Exact",…}
agent: Aquí tienes el pan.
```

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "sí, por favor" --prompt stall-offer --content content/game-v1 --agent-state done
```

```text
{"intent":"confirm","slots":{},"communicated":true,"correctness":1,"errors":[],…,"resolvedBy":"Exact",…}
agent: ¡Hasta luego!
```

`--seed <n>` is the world seed. Together with the agent id and `--counter` (the interaction counter) it seeds the draw that picks one of the
agent's variant lines, so the same inputs always give the same line and another seed can give another one. The `villager_thank` pool has
two variants; the default seed 0 says "De nada.", and seed 2 picks the other (running seed 2 again prints the identical line):

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "gracias" --prompt villager-open --content content/game-v1 --seed 0
```

```text
{"intent":"thank","slots":{},"communicated":true,"correctness":1,"errors":[],…}
agent: De nada.
```

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "gracias" --prompt villager-open --content content/game-v1 --seed 2
```

```text
{"intent":"thank","slots":{},"communicated":true,"correctness":1,"errors":[],…}
agent: ¡Con gusto!
```

With the Vosk model fetched, `--wav` runs the same pipeline on audio. Here `si.wav` is the phrase "sí, por favor" synthesized with the Spanish voice of
espeak-ng for this example (`espeak-ng -v es -s 120 -w si.wav "sí, por favor"`); any recording of the phrase works. The constrained prompt recognizes it.
The verdict now carries the recognizer's own confidence, and `versions.modelHashes.asr` holds the SHA-256 of the Vosk model instead of `scripted-transcript`:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --wav si.wav --prompt stall-offer --agent-state offering --content content/game-v1 --models models
```

```text
{"intent":"confirm","slots":{},"communicated":true,"correctness":1,"errors":[],…,"confidence":0.4248798411522463,"resolvedBy":"Alias",…}
agent: Aquí tienes el pan.
```

The pronunciation tags (`pron.*`) need the phoneme model, which has no ONNX export in the repository yet, so none of these commands can trigger them.

## Each intent and error tag, and the repair ladder

Twelve more runs: the intents of the game's conversations, every error tag the CLI can trigger and the main command-line options. The
output is what the CLI printed when this page was written. The bundle is the game's, so the lines an agent says may differ after an edit of
its dialogue, and the shape of the output does not. The tests do not pin these lines; they check the structure the core relies on
(`GameBundleContractTests`) and two end-to-end goldens. Verdicts are abbreviated as above; the prompts are listed in
`content/game-v1/prompts/prompts.json`, and [pipeline.md](pipeline.md) explains every stage behind the output.

- [Each intent, said correctly](#each-intent-said-correctly)
- [Each error tag](#each-error-tag)
- [A confirmation and a goodbye](#a-confirmation-and-a-goodbye)
- [Answers of a constrained prompt](#answers-of-a-constrained-prompt)
- [The repair ladder](#the-repair-ladder)
- [A replay log](#a-replay-log)
- [Replay logs](#replay-logs) and the [content builder](#content-builder), at the end

## Each intent, said correctly

`request_item` is the first intent shown in the README; some of the others follow. Each is a fully correct utterance with
correctness 1.

A greeting on a constrained prompt of the guide's first lesson resolves by answer match (`resolvedBy` is `Alias`, because "buenos días" is an alias of the
canonical "hola") and the guide greets back:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "buenos días" --prompt lesson-hello --content content/game-v1
```

```text
{"intent":"greet","slots":{},"communicated":true,"correctness":1,"errors":[],…,"resolvedBy":"Alias",…}
agent: ¡Hola! Soy la guía.
```

Asking the way needs a `place` slot. The grammar resolves the question and it scores 1 with no errors; the villager points (the host event `villager.point` is in
the step, not in the output of the CLI):

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "dónde está el río" --prompt villager-open --content content/game-v1
```

```text
{"intent":"ask_directions","slots":{"place":"río"},"communicated":true,"correctness":1,"errors":[],…}
agent: Mira, está allí.
```

Asking how to make something and where to find something are two intents of the guide, with the slots `item` and `material`:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "cómo hago una antorcha" --prompt guide-open --content content/game-v1
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "dónde hay arena" --prompt guide-open --content content/game-v1
```

```text
{"intent":"ask_craft","slots":{"item":"antorcha"},"communicated":true,"correctness":1,"errors":[],…}
agent: ¡Claro! Mira.
{"intent":"ask_find","slots":{"material":"arena"},"communicated":true,"correctness":1,"errors":[],…}
agent: A ver, busco.
```

Naming what the guide points at (`name_object`) is the learner's turn in the guide's review: the slot is `object`.

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "es un árbol" --prompt guide-review --content content/game-v1
```

```text
{"intent":"name_object","slots":{"object":"árbol"},"communicated":true,"correctness":1,"errors":[],…}
agent: A ver si es eso.
```

A constrained lesson prompt takes the phrases of its answer set. "sí, tengo una fruta" is an alias of the answer to "¿Tienes una fruta?" and fills `item`:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "sí, tengo una fruta" --prompt lesson-have --content content/game-v1
```

```text
{"intent":"state_have","slots":{"item":"fruta"},"communicated":true,"correctness":1,"errors":[],…,"resolvedBy":"Alias",…}
agent: ¡Muy bien!
```

The same prompt shows what a constrained prompt does with a sentence that is not one of its phrases: "tengo dos frutas" is understood by a slot grammar (the
intent and both slots are found), so it is communicated with no error although its words are not an answer (since M67 a line a slot grammar understood never
gets the derived `vocab.wrong`):

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "tengo dos frutas" --prompt lesson-have --content content/game-v1
```

```text
{"intent":"state_have","slots":{"item":"fruta","qty":"2"},"communicated":true,"correctness":1,"errors":[],…,"resolvedBy":"Grammar",…}
agent: ¡Muy bien!
```

## Each error tag

Where each tag is shown:

| Tag | Example |
| --- | --- |
| `conj.person`, `noun.number` | The worked example `yo quiere dos espada` ("Without models", above) |
| `gender.agree`, `vocab.unknown` | "More examples", above |
| `ser_estar`, `word_order`, `conj.irregular`, `conj.tense`, `vocab.wrong` | Below |
| `pron.vowel`, `pron.r`, `pron.silent_h`, `pron.stress` | Not triggered by any command here: they need the phoneme model (no ONNX export yet), and `pron.stress` is not scored in v1 |

`ser_estar`: `alto` takes `ser`, so `estoy alto` is a copula error. The greeting is still understood (by the rules of the intent, so `resolvedBy` is `Rules`)
and the agent recasts the whole sentence:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "hola estoy alto" --prompt villager-open --content content/game-v1
```

```text
{"intent":"greet","slots":{},"communicated":true,"correctness":0.8,
 "errors":[{"tag":"ser_estar","span":{"start":5,"length":5},"expected":"soy","actual":"estoy"}],…}
agent: Claro, «hola soy alto». Muy bien.
agent: ¡Hola! ¿Qué buscas?
```

`word_order`: `azul` normally follows its noun, so `azul espada` is flagged and the expected order is given. The grammar of `request_item` has no place for an
adjective before the noun, so the sentence is not understood (`communicated` is false) and the agent asks again; the error is found anyway, because the analysis does
not depend on the intent (since M62 the same holds for a colour that is also a noun, `quiero tres rojas manzanas`; [api.md](api.md), "Adjectives"):

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero una azul espada" --prompt villager-open --content content/game-v1
```

```text
{"intent":"unknown","slots":{},"communicated":false,"correctness":0.9,
 "errors":[{"tag":"word_order","span":{"start":11,"length":11},"expected":"espada azul","actual":"azul espada"}],…}
agent: No te entendí. ¿Me lo dices otra vez?
agent: Si necesitas ayuda, di «ayuda».
repair: AskAgain ¿Cómo?
```

`conj.irregular`: `quero` is the regularized form that learners make of `quiero`, which is the expected form:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quero madera" --prompt lesson-want --content content/game-v1
```

```text
{"intent":"state_want","slots":{"item":"madera"},"communicated":true,"correctness":0.8,
 "errors":[{"tag":"conj.irregular","span":{"start":3,"length":5},"expected":"quiero","actual":"quero"}],…,"resolvedBy":"Fuzzy",…}
agent: ¡Eso es! «Yo quiero madera».
agent: ¡Muy bien!
```

`conj.tense`: a present verb after a past-time word such as `ayer` should be in the preterite, and the recast uses it:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "ayer voy a comer pan" --prompt lesson-plan --content content/game-v1
```

```text
{"intent":"state_action","slots":{"item":"pan"},"communicated":true,"correctness":0.75,
 "errors":[{"tag":"conj.tense","span":{"start":5,"length":3},"expected":"fui","actual":"voy"}],…,"resolvedBy":"Fuzzy",…}
agent: Claro, «ayer fui a comer pan». Muy bien.
agent: ¡Muy bien!
```

`vocab.wrong`: this tag is derived rather than analyzed. On a constrained prompt, a line that matched no answer gets it where it can name a word (M67, M72,
M75). A near miss names its word: the line misses the answer phrase nearest it by one word of a slip pair (close in spelling, or both question words,
articles, prepositions or pronouns), and the tag is on that word with the answer's word as `expected`. `lesson-what` asks «¿Qué es esto?»; `cuál` is a
question word where the answer has `qué`. A keyword rule understood the line (`ask_what`), so it is communicated, at 0.4:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "cuál es esto" --prompt lesson-what --content content/game-v1
```

```text
{"intent":"ask_what","slots":{},"communicated":true,"correctness":0.4,
 "errors":[{"tag":"vocab.wrong","span":{"start":0,"length":4},"expected":"qué","actual":"cuál"}],…,"resolvedBy":"Rules",…}
agent: ¡Eso es! «Cuál es esto».
agent: Miro bien.
```

Before M75 the tag was on `es` with an empty `expected`, and M67's rule (a distance to the nearest answer within the fuzzy budget for the line's length) also
put it on correct Spanish: `quiero hacer un hacha` at `smith-open` scored 0.4 while the agent said «Sí, «quiero hacer un hacha». ¡Muy bien!»; it has no
error now. The recast still says the line as it was: it is built from the analysis's errors, and the derived tag is the verdict's. A line that resolved no
intent at a prompt whose answer set has one answer, or at a quiz (no intent allowed), gets the tag on its first content word, with the canonical answer as
`expected` when the set has one; a correct line that is simply not among a prompt's several answers gets none (M72) and is not communicated.

## A confirmation and a goodbye

The shopkeeper's machine decides what a yes or a no does, and that depends on its state (`--agent-state`). Offering bread, a no ends the exchange
politely; the verdict is the same in every state:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "no, gracias" --prompt stall-offer --content content/game-v1 --agent-state offering
```

```text
{"intent":"deny","slots":{},"communicated":true,"correctness":1,"errors":[],…,"resolvedBy":"Exact",…}
agent: Está bien.
```

A goodbye to the villager moves its machine to `idle`, whatever state it was in:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "adiós" --prompt villager-open --content content/game-v1
```

```text
{"intent":"farewell","slots":{},"communicated":true,"correctness":1,"errors":[],…}
agent: ¡Adiós! Hasta luego.
```

The core can also bind a name the learner says (the `[WORD:name]` token of the slot-grammar DSL: the slot is the name as written, in lower case, and the name needs
no lexicon entry), but no intent of the game does. The tests keep that coverage on a bundle with one extra intent, built in the test (`TestSupport.NameBundle`
in the pipeline tests).

## Answers of a constrained prompt

An exact answer to a constrained prompt is `communicated` with correctness 1 and `resolvedBy` `Exact`; the intent grammar then reads the same words and fills the
slot:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero agua por favor" --prompt stall-need --content content/game-v1
```

```text
{"intent":"request_item","slots":{"item":"agua"},"communicated":true,"correctness":1,"errors":[],…,"resolvedBy":"Exact",…}
agent: Toma la botella. El agua está en el río.
agent: ¿Quieres pan?
```

The prompts of the game all allow intents. An answer-only prompt (no intent, the answer set alone decides) exists in the tests, which build one, and in the core's
API; the near-miss case "aguas" for "agua" is in the README.

## The repair ladder

`--repeat not_communicated=<n>` is the number of failed attempts in a row before this one (the host's count from the previous `Next`; the
pipeline counts this attempt itself). The first attempt is `AskAgain` (see the README), the second `SlowRepeat`, the third `OfferChoices`
and the fourth and later `GiveUp`, so `<n>` of 0, 1, 2 and 3 gives those four. The sentence is a nonsense one that no grammar reads, and
its verdict does not change, so only the repair line is shown:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "xyzzy plugh" --prompt stall-need --content content/game-v1 --repeat not_communicated=1
```

```text
repair: SlowRepeat Perdón, te lo repito más despacio.
```

`OfferChoices` offers one phrase per answer, the canonical phrase, in the answer set's declared order (no aliases, no sort), the first three
distinct ones; `supplies` declares water, bread and fruit first:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "xyzzy plugh" --prompt stall-need --content content/game-v1 --repeat not_communicated=2
```

```text
repair: OfferChoices ¿Quieres decir «quiero agua por favor», «quiero pan por favor» o «quiero fruta por favor»?
```

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "xyzzy plugh" --prompt stall-need --content content/game-v1 --repeat not_communicated=3
```

```text
repair: GiveUp Bueno, no importa.
```

## A replay log

`--replay-out <file>` appends one line per run, so two runs make a two-entry log. `replay` re-evaluates every entry from the recorded
recognizer output, without models or audio, and prints `OK` when every result is identical to what was recorded. The two `eval`
commands print the output shown above and are not repeated here.

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1 --replay-out replay.jsonl
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "quiero una pan" --prompt villager-open --content content/game-v1 --replay-out replay.jsonl
dotnet run --project tools/Vocaterra.Nlp.Cli -- replay replay.jsonl --content content/game-v1
```

```text
OK
```

An entry recorded from a `--wav` run replays the same way and needs no model. What a `DIFF` line looks like, and what causes one, is in
[replay.md](replay.md).

## Replay logs

`--replay-out <file>` appends one JSON line per utterance with everything needed to re-evaluate it without models or audio:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1 --replay-out replay.jsonl
dotnet run --project tools/Vocaterra.Nlp.Cli -- replay replay.jsonl --content content/game-v1
```

The second command prints `OK`, or the JSON paths that differ (for example `DIFF entry 1: /verdict/correctness`) and exits 1.
Audio is never stored and logs are opt-in. The format, a real example and what a difference means are in [docs/nlp/replay.md](replay.md).

## Content builder

Content (answer sets, intents, prompts, agents, dialogue, lexicon, evaluation weights, effects, and the game's own host files) lives in `content/game-v1` as a hashed
bundle, the one bundle of the repository. `tools/Vocaterra.Nlp.ContentBuilder` is the build-time tool (`scripts/content.sh` runs it from source):

```bash
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- validate content/game-v1                 # verify hashes and cross-references
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- hash content/game-v1                     # rewrite content/game-v1/manifest.json after an edit
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- lint content/game-v1 --level a1          # check examples, overlaps, hazards, reachability, CEFR (run before hash; see tools.md)
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- cefr-check content/game-v1 --level a1    # list lemmas of dialogue templates and of prompt text missing from the CEFR list (a1 or a2)
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- import-lexicon kaikki.jsonl lexicon.json # wiktextract Spanish JSONL to a lexicon file (--max N limits it;
#   --frequency es_50k.txt --top N keeps the N most frequent lemmas; --sidecar writes the source record; see docs/nlp/lexicon.md)
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- lexicon-report content/game-v1           # each lexicon file, how they overlap, the merged lexicon
```

Any edit to a file in `content/game-v1` changes the bundle hash, so run `hash` afterwards (see [changing-the-core.md](changing-the-core.md)); no test pins the hash,
each compares the manifest with the hash computed from the files. `validate` prints `OK: bundle 'game-v1' <version>, <n> files, hash <sha256>`, then the errors and warnings of the content lint.
