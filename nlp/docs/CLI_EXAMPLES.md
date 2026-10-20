# More CLI examples

Worked runs of the demo CLI (`tools/Slg.Cli`) against the sample bundle `content/v1`, continuing the eleven examples in the README
section [More examples](../README.md#more-examples). Together they show five of the six intents, every error tag the CLI can trigger and the
main command-line options. All commands run from the repository root; the output is what the CLI printed.

Each verdict is abbreviated: `…` marks the cut fields (`pronunciation`, `confidence`, `resolvedBy` and `versions`, unless an example
keeps them), and the closing `decision:` line is left out. Error spans count characters of the normalized text. The prompts are
listed in `content/v1/prompts/prompts.json`, and [PIPELINE.md](PIPELINE.md) explains every stage behind the output.

- [Each intent, said correctly](#each-intent-said-correctly)
- [Each error tag](#each-error-tag)
- [Quiz prompts](#quiz-prompts)
- [The repair ladder](#the-repair-ladder)
- [A replay log](#a-replay-log)

## Each intent, said correctly

Six intents exist; `buy_item` is the first example in the README, the other four follow. Each is a fully correct utterance with
correctness 1.

A greeting on a constrained prompt resolves by answer match (`resolvedBy` is `Exact`) and the blacksmith greets back:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "buenos días" --prompt greet-blacksmith --content content/v1
```

```text
{"intent":"greet","slots":{},"communicated":true,"correctness":1,"errors":[],"resolvedBy":"Exact",…}
npc: ¡Hola, amigo! ¿Qué necesitas hoy?
```

Selling is the mirror of buying, with its own price factor (`sell_item_price` runs from 0.7 at correctness 0 to 1 at correctness 1):

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "quiero vender un escudo" --prompt sell-item-open --content content/v1
```

```text
{"intent":"sell_item","slots":{"item":"escudo"},"communicated":true,"correctness":1,"errors":[],…}
npc: Mmm, el escudo... Te doy diez monedas.
effect: sell_item_price trade_price 1
```

Offering needs two slots, `item` and `qty`. The matched grammar also names the verb, so the slots carry `verb`:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "tengo dos espadas" --prompt offer-items-open --content content/v1
```

```text
{"intent":"offer_item","slots":{"item":"espada","qty":"2","verb":"tener"},"communicated":true,"correctness":1,"errors":[],…}
npc: Vale, dos espadas. Gracias, amigo.
```

Casting a spell is the one intent whose effect is a power rather than a price. The prompt has no NPC, so the only line is the praise for
an accepted answer, and the power is 1 at full correctness:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "lanzo un rayo" --prompt spell-cast-open --content content/v1
```

```text
{"intent":"cast_spell","slots":{"spell":"rayo"},"communicated":true,"correctness":1,"errors":[],…}
npc: ¡Muy bien!
effect: cast_spell_power spell_power 1
```

## Each error tag

Where each tag is shown:

| Tag | Example |
| --- | --- |
| `conj.person`, `noun.number` | The worked example `yo tiene dos espada` in the README ("Without models") |
| `gender.agree`, `vocab.unknown` | The README, "More examples" |
| `ser_estar`, `word_order`, `conj.irregular`, `conj.tense`, `vocab.wrong` | Below |
| `pron.vowel`, `pron.r`, `pron.silent_h`, `pron.stress` | Not triggered by any command here: they need the phoneme model (no ONNX export yet), and `pron.stress` is not scored in v1 |

`ser_estar`: `alto` takes `ser`, so `estoy alto` is a copula error. The greeting is still understood and the NPC recasts the whole
sentence:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "hola estoy alto" --prompt greet-open --content content/v1
```

```text
{"intent":"greet","slots":{},"communicated":true,"correctness":0.8,
 "errors":[{"tag":"ser_estar","span":{"start":5,"length":5},"expected":"soy","actual":"estoy"}],…}
npc: Ah, hola soy alto. ¡Perfecto!
npc: ¡Hola, amigo! ¿Qué necesitas hoy?
```

`word_order`: `mágica` normally follows its noun, so `mágica espada` is flagged and the expected order is given:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "quiero comprar una mágica espada" --prompt buy-item-open --content content/v1
```

```text
{"intent":"buy_item","slots":{"item":"espada"},"communicated":true,"correctness":0.9,
 "errors":[{"tag":"word_order","span":{"start":19,"length":13},"expected":"espada mágica","actual":"mágica espada"}],…}
npc: Ah, quiero comprar una espada mágica. ¡Perfecto!
npc: La espada es una buena elección.
effect: buy_item_price trade_price 1.03
```

`conj.irregular`: `teno` is the regularized form that learners make of `tengo`, which is the expected form:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "yo teno dos espadas" --prompt offer-items-open --content content/v1
```

```text
{"intent":"offer_item","slots":{"item":"espada","qty":"2","verb":"tener"},"communicated":true,"correctness":0.75,
 "errors":[{"tag":"conj.irregular","span":{"start":3,"length":4},"expected":"tengo","actual":"teno"}],…}
npc: Ah, yo tengo dos espadas. ¡Perfecto!
npc: Vale, dos espadas. Gracias, amigo.
```

`conj.tense`: a present verb after a past-time word such as `ayer` should be in the preterite, and the recast uses it:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "ayer compro una espada" --prompt buy-item-open --content content/v1
```

```text
{"intent":"buy_item","slots":{"item":"espada"},"communicated":true,"correctness":0.75,
 "errors":[{"tag":"conj.tense","span":{"start":5,"length":6},"expected":"compré","actual":"compro"}],…}
npc: Ah, ayer compré una espada. ¡Perfecto!
npc: La espada es una buena elección.
effect: buy_item_price trade_price 1.075
```

`vocab.wrong`: this tag is derived rather than analyzed. On a constrained prompt, a word the lexicon knows but the answer set does not
contain (here a spell name given to the item quiz) is wrong, not unknown. Its `expected` field holds the answer set id. The answer does
not count, and with no NPC on the prompt only the repair step is printed:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "fuego" --prompt quiz-item-name --content content/v1
```

```text
{"intent":"unknown","slots":{},"communicated":false,"correctness":0.4,
 "errors":[{"tag":"vocab.wrong","span":{"start":0,"length":5},"expected":"items-basic","actual":"fuego"}],…}
repair: AskAgain ¿Cómo?
```

## Quiz prompts

An exact answer on an answer-only prompt is `communicated` with correctness 1, no intent and `resolvedBy` `Exact`. With no NPC on the
prompt, the praise comes from the `answer_accepted` pool. The near-miss case is in the README.

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "espada" --prompt quiz-item-name --content content/v1
```

```text
{"intent":"unknown","slots":{},"communicated":true,"correctness":1,"errors":[],"resolvedBy":"Exact",…}
npc: ¡Muy bien!
```

## The repair ladder

`--repeat not_communicated=<n>` counts failed attempts in a row. The first attempt is `AskAgain` (see the README), the second `SlowRepeat`,
the third `OfferChoices` and the fourth and later `GiveUp`. The sentence is the unknown-word one from the README and its verdict does not
change, so only the repair line is shown:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "quiero comprar una zanahoria" --prompt buy-item-open --content content/v1 --repeat not_communicated=2
```

```text
repair: SlowRepeat Perdón, te lo repito más despacio.
```

`OfferChoices` offers the first three distinct phrases of the answer set in ordinal order, aliases included, which is why `arco` and
`el arco` can appear together:

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "quiero comprar una zanahoria" --prompt buy-item-open --content content/v1 --repeat not_communicated=3
```

```text
repair: OfferChoices ¿Quieres decir «arco», «brebaje» o «el arco»?
```

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "quiero comprar una zanahoria" --prompt buy-item-open --content content/v1 --repeat not_communicated=4
```

```text
repair: GiveUp Bueno, no importa.
```

## A replay log

`--replay-out <file>` appends one line per run, so two runs make a two-entry log. `replay` re-evaluates every entry from the recorded
recognizer output, without models or audio, and prints `OK` when every result is identical to what was recorded. The two `eval`
commands print the output shown above and are not repeated here.

```bash
dotnet run --project tools/Slg.Cli -- eval --transcript "yo tiene dos espada" --prompt offer-items-open --content content/v1 --replay-out replay.jsonl
dotnet run --project tools/Slg.Cli -- eval --transcript "quiero comprar un espada" --prompt buy-item-open --content content/v1 --replay-out replay.jsonl
dotnet run --project tools/Slg.Cli -- replay replay.jsonl --content content/v1
```

```text
OK
```

An entry recorded from a `--wav` run replays the same way and needs no model. What a `DIFF` line looks like, and what causes one, is in
[REPLAY.md](REPLAY.md).
