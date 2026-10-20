# Vocaterra

Vocaterra is a Terraria-like game in which learning Spanish is one of the main pillars: exploring, progression and looting, with speech and
dynamic conversation in Spanish. The game design is in [docs/SPECIFICATION.md](docs/SPECIFICATION.md).

The Spanish language processing is a separate repository, the language core **Vocaterra.NLP**
(`https://github.com/james-d12/Vocaterra.NLP`). It takes a learner's utterance to a verdict (what they meant, how well they said it), feedback,
a conversation step and the agent's response text. The core is consumed here as NuGet packages, `Vocaterra.Nlp.*` version 0.1.0; nothing
of the core's source is copied into this repository. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) says what the game takes from the core and
what the game owns.

This repository is the scaffold. The engine is not chosen yet (decision D12 in the core's `docs/DECISIONS.md`), so the host is a console program
that exercises the core's calls. It has no rendering, audio capture or game world yet.

## Layout

```
Vocaterra.sln                    the solution: the host and its tests
Directory.Build.props            the build settings (net10.0, nullable, warnings as errors, deterministic)
Directory.Packages.props         central package versions: the Vocaterra.Nlp.* packages and the test packages
nuget.config                     the core's local feed, then nuget.org
global.json                      the .NET SDK pin
src/Vocaterra.Host/              the console host: loads the bundle, evaluates lines, runs the text loop
tests/Vocaterra.Host.Tests/      smoke tests: the two demo utterances, the bundle hash, the carried conversation
content/game-v1/                 the game's content bundle (see below)
scripts/content.sh               validate or hash a bundle with the core's ContentBuilder
docs/SPECIFICATION.md            the game design (moved from the core)
docs/ARCHITECTURE.md             how the game consumes the core, and what the game owns
.github/workflows-pending/       the parked CI job (see below)
```

## Build and run today

The packages come from the core's local feed, so the core must be checked out beside this repository, in a folder named
`SpanishLearningGame`, with its packages packed:

```bash
# in the core checkout (SpanishLearningGame):
scripts/pack.sh                  # writes artifacts/feed, the folder nuget.config reads

# in this repository:
dotnet restore Vocaterra.sln
dotnet build Vocaterra.sln -warnaserror
dotnet test Vocaterra.sln
```

The .NET SDK is pinned in `global.json` (10.0.402, `latestPatch`). A user-local install is enough; see the core's README for the commands.
`nuget.config` says where the feed is; if the core lives elsewhere, change its path.

Run the host:

```bash
# one line, evaluated once and printed (the form the tests and scripts use)
dotnet run --project src/Vocaterra.Host -- --transcript "yo tiene dos espada" --prompt offer-items-open

# the text loop: type a Spanish line; /prompt <id> switches prompt, /quit leaves
dotnet run --project src/Vocaterra.Host
```

The host runs from the repository root, because the default bundle is `content/game-v1` relative to the current directory (`--content <dir>`
names another).

## Content

`content/game-v1` is the game's bundle. For now it is a copy of the core's sample bundle (a blacksmith's dialogue, spells and items), with the
manifest's bundle id set to `game-v1`; the game's own scenes and characters replace it as they are written. The bundle has its own hash, which
the loader checks against `manifest.json`. After any edit, rewrite the manifest:

```bash
scripts/content.sh validate                                  # every file's hash and the cross-references
scripts/content.sh hash content/game-v1 --bundle-id game-v1  # rewrite manifest.json (keep the bundle id once it is set)
```

The script runs the core's ContentBuilder from `VOCATERRA_NLP_DIR` (default `../SpanishLearningGame`). Packaging the ContentBuilder as a dotnet
tool is a follow-up in the core. `ATTRIBUTION.md` and `provenance.json` in the bundle are the core sample's: the Wiktionary lexicon in it is
CC BY-SA and keeps its notice.

## Continuous integration

`.github/workflows-pending/ci.yml` builds, checks the format and tests on Ubuntu. It is parked: it needs the workflow scope on the token
(core DECISIONS.md D1) and a feed the runner can read (D5). Until then nothing runs in CI.

## Packages and the feed

Nothing is published. The core has not chosen its feed (D5); the options are GitHub Packages, a committed release folder, or nuget.org. When
GitHub Packages is chosen, `nuget.config` gets that source and the runner needs a token with read access to the packages. The core's
packages depend on third-party native libraries, and one of them carries GPL code (core D2); the game does not distribute a build that includes
them until that is settled.

## Licence

No licence is chosen for this repository yet (core DECISIONS.md D3). The packages it consumes name none either. Until a licence is chosen,
this code grants no permission to use, copy or modify it.

## Documentation

- [docs/SPECIFICATION.md](docs/SPECIFICATION.md): the game design
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how the game consumes the core, the host's calls, what the host owns and what the game owns
- [Vocaterra.NLP's docs/API.md](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/API.md): the calls a host makes, in order
- [Vocaterra.NLP's docs/PACKAGING.md](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/PACKAGING.md): the packages, the local feed and the sample host
- [Vocaterra.NLP's ADR 0009](https://github.com/james-d12/Vocaterra.NLP/blob/main/docs/adr/0009-core-and-game-boundary.md): what the core and the game each own
- [CLAUDE.md](CLAUDE.md): working notes for the agents and the owner
