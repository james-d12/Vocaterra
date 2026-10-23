# More CLI examples

Worked runs of the demo CLI (`tools/Vocaterra.Nlp.Cli`) against the repository's one bundle, `content/game-v1`, continuing the examples in the README
section [More examples](README.md#more-examples). Together they show the intents of the game's conversations, every error tag the CLI can trigger and the
main command-line options. All commands run from the repository root; the output is what the CLI printed when this page was written. The bundle is the
game's, so its lessons change: the lines an agent says may differ from these after an edit of the game's dialogue, and the shape of the output does not.
The tests do not pin these lines; they check the structure the core relies on (`GameBundleContractTests`) and two end-to-end goldens.

Each verdict is abbreviated: `…` marks the cut fields (`pronunciation`, `confidence`, `resolvedBy` and `versions`, unless an example
keeps them), and the closing `decision:` line is left out. Error spans count characters of the normalized text. The prompts are
listed in `content/game-v1/prompts/prompts.json`, and [PIPELINE.md](PIPELINE.md) explains every stage behind the output.

- [Each intent, said correctly](#each-intent-said-correctly)
- [Each error tag](#each-error-tag)
- [A confirmation and a goodbye](#a-confirmation-and-a-goodbye)
- [Answers of a constrained prompt](#answers-of-a-constrained-prompt)
- [The repair ladder](#the-repair-ladder)
- [A replay log](#a-replay-log)

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

The same prompt shows what a constrained prompt does with a sentence that is not one of its phrases: "tengo dos frutas" is understood (the intent and both
slots are found) but the words are not an answer, so the verdict carries `vocab.wrong` and the agent recasts what the learner said:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "tengo dos frutas" --prompt lesson-have --content content/game-v1
```

```text
{"intent":"state_have","slots":{"item":"fruta","qty":"2"},"communicated":true,"correctness":0.4,
 "errors":[{"tag":"vocab.wrong","span":{"start":0,"length":5},"expected":"","actual":"tengo"}],…}
agent: ¡Eso es! «Tengo dos frutas».
agent: ¡Muy bien!
```

## Each error tag

Where each tag is shown:

| Tag | Example |
| --- | --- |
| `conj.person`, `noun.number` | The worked example `yo quiere dos espada` in the README ("Without models") |
| `gender.agree`, `vocab.unknown` | The README, "More examples" |
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
adjective, so the sentence is not understood (`communicated` is false) and the agent asks again; the error is found anyway, because the analysis does not depend on
the intent:

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

`vocab.wrong`: this tag is derived rather than analyzed. On a constrained prompt, a word the lexicon knows but the answer set does not
contain (here a spell-like word given to the stall) is wrong, not unknown. Its `expected` field is the answer set's canonical answer when
the set has exactly one answer, and empty otherwise; `supplies` has three item answers, so it is empty here. The sentence is not understood and the
agent asks again:

```bash
dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "fuego" --prompt stall-need --content content/game-v1
```

```text
{"intent":"unknown","slots":{},"communicated":false,"correctness":0.4,
 "errors":[{"tag":"vocab.wrong","span":{"start":0,"length":5},"expected":"","actual":"fuego"}],…}
agent: No te entendí. ¿Me lo dices otra vez?
repair: AskAgain ¿Cómo?
```

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
[REPLAY.md](REPLAY.md).
