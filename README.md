# Vocaterra

Vocaterra is a Terraria-like game in which learning Spanish is one of the main pillars: exploring, progression and looting, with speech and
dynamic conversation in Spanish. The game design is in [docs/SPECIFICATION.md](docs/SPECIFICATION.md).

The Spanish language processing is a separate repository, the language core **Vocaterra.NLP**
(`https://github.com/james-d12/Vocaterra.NLP`, Apache-2.0). It takes a learner's utterance to a verdict (what they meant, how well they said
it), feedback, a conversation step and the agent's response text. This repository includes the core as the git submodule
`external/Vocaterra.NLP` and consumes it as the NuGet packages `Vocaterra.Nlp.*` version 0.1.0 from the submodule's local feed; nothing of the
core's source is copied here. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) says what the game takes from the core and what the game owns.

This repository is the scaffold. The engine is not chosen yet (decision D-game-1 in [docs/DECISIONS.md](docs/DECISIONS.md)), so the host is a
console program that exercises the core's calls. It has no rendering, audio capture or game world yet.

## Layout

```
Vocaterra.sln                    the solution: the host and its tests
Directory.Build.props            the build settings (net10.0, nullable, warnings as errors, deterministic)
Directory.Packages.props         central package versions: the Vocaterra.Nlp.* packages and the test packages
nuget.config                     the submodule's local feed, then nuget.org
global.json                      the .NET SDK pin
external/Vocaterra.NLP/          the language core, a git submodule (pinned; its build output is git-ignored there)
src/Vocaterra.Host/              the console host: loads the bundle, evaluates lines, runs the text loop
tests/Vocaterra.Host.Tests/      smoke tests: the two demo utterances, the bundle hash, the carried conversation, the repeat counts, the prompt text
content/game-v1/                 the game's content bundle (see below)
scripts/content.sh               validate, hash and check a bundle with the core's content tool
scripts/merge-gate.sh            the orchestrator's merge gate
docs/                            SPECIFICATION (design), ARCHITECTURE (boundary), DECISIONS, CORE_REQUESTS (requests to the core)
IMPLEMENTATION_PLAN.md           the milestones and the findings log
.claude/agents/orchestrator.md   the orchestrator's role file (how the repository is developed)
.github/workflows-pending/       the parked CI job (see below)
```

## Clone, build and run

```bash
git clone https://github.com/james-d12/Vocaterra.git
cd Vocaterra
git submodule update --init                          # the language core, external/Vocaterra.NLP
bash external/Vocaterra.NLP/scripts/pack.sh          # packs the core's packages into external/Vocaterra.NLP/artifacts/feed

dotnet restore Vocaterra.sln
dotnet build Vocaterra.sln -warnaserror
dotnet test Vocaterra.sln
```

The .NET SDK is pinned in `global.json` (10.0.402, `latestPatch`). A user-local install is enough. `nuget.config` reads the submodule's feed
by a relative path; the packing step is needed after a clone and after each change to the pinned core commit.

A git worktree has an empty submodule, so it does not pack the core again (about 3 GB). It restores from the main checkout's feed instead:

```bash
dotnet restore Vocaterra.sln -p:RestoreSources="/path/to/main/checkout/external/Vocaterra.NLP/artifacts/feed;https://api.nuget.org/v3/index.json"
dotnet build Vocaterra.sln -warnaserror --no-restore
```

Run the host (from the repository root, because the default bundle is `content/game-v1` relative to the current directory; `--content <dir>`
names another):

```bash
# one line, evaluated once and printed (the form the tests and scripts use)
dotnet run --project src/Vocaterra.Host -- --transcript "yo tiene dos espada" --prompt offer-items-open

# the text loop: shows each prompt (and the agent's line, when the prompt has one), then reads a line;
# /prompt <id> switches prompt, /quit leaves
dotnet run --project src/Vocaterra.Host
```

The host makes these calls of the core's API, in order, on the packages (the details are in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)): `ContentLoader.Load` for the bundle; `PipelineServices` and `UtterancePipeline.Evaluate`
for each typed line; `ConversationContext.Create` for the utterance's context and `ConversationContext.Next` after it, which applies the core's
`RepeatCounts.Next` to the error tags. The host owns the world seed, the stored time (fixed for `--transcript`, the clock in the loop),
the learner model and the repeat counts.

## Content

`content/game-v1` is the game's bundle, under the bundle id `game-v1`. It tracks the core's sample bundle (`content/v1` in the core, a
blacksmith with spells and items) for the generic intents (`confirm`, `deny`, `farewell`, `thank`, `introduce_self`), their answer sets and
dialogue, and the blacksmith's `confirming` state, with the prompt text of every prompt (decision D-game-4). The game's own scenes replace the
sample's content as they are written (from G3 on). The bundle has its own hash, which the loader checks against `manifest.json`. After any
edit, rewrite the manifest:

```bash
scripts/content.sh validate                                  # every file's hash and the cross-references
scripts/content.sh hash content/game-v1 --bundle-id game-v1  # rewrite manifest.json (keep the bundle id once it is set)
```

The script runs the core's content tool, `vocaterra-content` 0.1.0, installed under `.tools/` from the submodule's feed on first use (see
[scripts/README.md](scripts/README.md)). `ATTRIBUTION.md` and `provenance.json` describe this bundle; the Wiktionary lexicon in it is CC BY-SA
and keeps its notice.

## Continuous integration

`.github/workflows-pending/ci.yml` is parked. It needs the workflow scope on the token (core DECISIONS.md D1), and it still names the core's
own test projects and feed path, so it has to be rewritten before it is activated (deferred by the owner, see D-game-3 in
[docs/DECISIONS.md](docs/DECISIONS.md)). Until then nothing runs in CI.

## Packages and the feed

The core is consumed through its submodule, and its packages are never published: the core chose no hosted feed (core D5, decided
2026-10-21). The packages depend on third-party native libraries, and one of them carries GPL-3.0 code (core D2); the game does not distribute
a build that includes them until D2 is settled in the core.

## Licence

The core, Vocaterra.NLP, is licensed under Apache-2.0 (core D3). **This repository's own licence is not chosen yet** (D-game-2 in
[docs/DECISIONS.md](docs/DECISIONS.md)). Until a licence is chosen, this code grants no permission to use, copy or modify it.

## Documentation

- [docs/SPECIFICATION.md](docs/SPECIFICATION.md): the game design
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how the game consumes the core, the host's calls, what the host owns and what the game owns
- [docs/DECISIONS.md](docs/DECISIONS.md): the game's open and decided questions
- [docs/CORE_REQUESTS.md](docs/CORE_REQUESTS.md): what the game has asked the core for, and the answers
- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md): the milestones, the scene designs and the findings log
- [Vocaterra.NLP's docs/API.md](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/API.md): the calls a host makes, in order
- [Vocaterra.NLP's docs/PACKAGING.md](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/PACKAGING.md): the packages, the local feed and the content tool
- [Vocaterra.NLP's ADR 0009](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/adr/0009-core-and-game-boundary.md): what the core and the game each own
- [CLAUDE.md](CLAUDE.md): working notes for the agents and the owner
