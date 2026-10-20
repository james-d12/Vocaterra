# Packaging

This is how the language core ships as NuGet packages: the versions, `scripts/pack.sh` and the local feed it fills, the sample host that
proves the packages are complete, the content bundle archive, the parked CI job, and what is deliberately left undecided. The plan is
`IMPLEMENTATION_PLAN.md` section 8.2 (M19b); the owner decisions are in [DECISIONS.md](DECISIONS.md) (D2, D3, D4, D5).

## Versions and identity

- Every library under `src/` is one package, and its package id is its project name: `Vocaterra.Nlp.Core`, `Vocaterra.Nlp.Audio`,
  `Vocaterra.Nlp.Asr`, `Vocaterra.Nlp.Phonology`, `Vocaterra.Nlp.Pronunciation`, `Vocaterra.Nlp.Language`, `Vocaterra.Nlp.Evaluation`,
  `Vocaterra.Nlp.Decision`, `Vocaterra.Nlp.Response`, `Vocaterra.Nlp.Content` and `Vocaterra.Nlp.Pipeline`.
- The version is `VersionPrefix` 0.1.0 in `Directory.Build.props`, the same for all eleven. A project reference becomes a package
  dependency at that version, so the packages depend on each other at 0.1.0.
- Tests, tools and samples are never packed: `IsPackable` is false in `Directory.Build.props` and true only under `src/`.
- The content bundle has its own version, `contentVersion` 1.0.0 in `content/v1/manifest.json` (bundle id `v1`), and it is not in the packages.
  Models are not in the packages either; a host gives them a directory through `VOCATERRA_MODELS_DIR`.
- Each package holds its assembly, its XML documentation, the shared readme (`docs/PACKAGE_README.md`, shipped as `README.md`) and a `.snupkg`
  with the symbols. The nuspec names the repository and the commit it was built from.
- Builds are deterministic (`Deterministic` is on in `Directory.Build.props`), and `ContinuousIntegrationBuild` is set when the `CI` environment
  variable is present.
- No licence is named. `PackageLicenseExpression` and `PackageLicenseFile` are not set, because the licence is not chosen (D3). See below.

## Pack

```bash
scripts/pack.sh
```

The script runs `dotnet pack Vocaterra.Nlp.sln -c Release -o artifacts/packages`, which writes a `.nupkg` and a `.snupkg` for each of the eleven
libraries, and then copies the `.nupkg` files into `artifacts/feed`. Both folders are emptied first, so a run leaves exactly the current tree's
packages. `artifacts/` is git-ignored.

## The local feed

A folder is a NuGet feed. `artifacts/feed` is the feed `samples/HostConsole` reads, through its own `nuget.config`:

```xml
<packageSources>
  <clear />
  <add key="vocaterra-local" value="../../artifacts/feed" />
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources>
```

nuget.org is listed only for the third-party packages the core depends on (Vosk, sherpa-onnx, ONNX Runtime); no `Vocaterra.Nlp.*` package
is published there. Another project can use the same feed with `dotnet nuget add source <path-to>/artifacts/feed --name vocaterra-local`.

The NuGet global packages folder (`~/.nuget/packages`, or `NUGET_PACKAGES`) keeps every version it has restored. The feed is rebuilt under the same
version number, so `scripts/sample-host.sh` removes the `vocaterra.nlp.*` folders there before it restores; without that a rebuilt 0.1.0 could be
ignored in favour of the cached one.

## The sample host

`samples/HostConsole` is a console host written against the packages, the way a game would consume them. It is not in `Vocaterra.Nlp.sln`,
it references the packages at version 0.1.0 and not the projects, and its `nuget.config` is the one above. It does what the document
[API.md](API.md) describes, in order: loads `content/v1`, builds the services with the scripted recognizer (no model files), builds the
pipeline and a conversation context, evaluates `yo tiene dos espada` on `offer-items-open` and `cuánto cuesta la espada` on `ask-price-open`,
and prints the verdicts. It exits 1 unless the first utterance has exactly the errors `conj.person` and `noun.number`, and the second scores 1.

```bash
scripts/sample-host.sh              # packs, restores and builds the sample against artifacts/feed, then runs it on content/v1
scripts/sample-host.sh <bundleDir>  # the same on another bundle directory
```

The output on the committed bundle (hash `3b1d1f02dff448c830d0cfdbaf1f5e330fc66922a7b7507abc4b56d78afbc657`):

```
bundle v1 1.0.0, hash 3b1d1f02dff448c830d0cfdbaf1f5e330fc66922a7b7507abc4b56d78afbc657
offer-items-open: "yo tiene dos espada"
  intent=offer_item communicated=true correctness=0.5
  error conj.person: expected 'tengo', heard 'tiene'
  error noun.number: expected 'espadas', heard 'espada'
  agent: Ah, ¿tienes dos espadas? ¡Muy bien!
  agent: ¡Dos espadas! Trato hecho.
ask-price-open: "cuánto cuesta la espada"
  intent=ask_price communicated=true correctness=1
  agent: La espada cuesta diez monedas de oro.
check offer-items-open: errors [conj.person, noun.number]: PASS
check ask-price-open: correctness 1: PASS
```

The sample is the proof that the public surface is complete: every call a host needs is reachable from the packages alone. Keep it and
[API.md](API.md) in step: a change to a call in one changes the other in the same commit.

## Content bundles

```bash
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- pack content/v1 out/bundle-v1.zip
dotnet run --project tools/Vocaterra.Nlp.ContentBuilder -- verify-pack out/bundle-v1.zip
```

`pack` validates the bundle first (every file's hash and the cross-references, as `validate` checks them); an invalid bundle writes nothing and
exits 1. It then writes a zip of the bundle's files, the manifest included, with dot-files left out as the bundle hash leaves them out. The entries
are in ordinal path order with a fixed time, so the same bundle always gives the same bytes. It also writes `out/bundle-v1.zip.sha256`, one line
that holds the bundle hash, the value `validate` and `hash` report.

`verify-pack` extracts the archive to a new temporary folder, refuses any entry whose name would leave that folder or that names a file twice,
validates the extracted bundle and compares its hash with the sidecar. The folder is deleted afterwards. It exits 0 only when the two agree.

**Hosts ship the extracted folder, not the zip.** The loader reads a directory (`ContentLoader.Load(directory)`), so a host extracts the archive
into its own content folder with any zip tool and loads that folder. The archive and its sidecar are what the build keeps and checks.

## Continuous integration

The parked workflow (`.github/workflows-pending/ci.yml`) has a `pack` job on Ubuntu: restore, `scripts/pack.sh`, `scripts/sample-host.sh`, and
an upload of `artifacts/packages/*` as the `packages` artifact. It runs on pushes to `main`, on `v*` tags and on pull requests. Tag pushes also run
the other push jobs, because their `if:` matches every push. The workflow can be pushed only once the token has the `workflow` scope (D1, and
`.github/workflows-pending/README.md`).

The publish step is commented out in that job. Publishing waits for the feed decision (D5).

## Deliberately undecided

- **The feed (D5).** Nothing is published. The options are GitHub Packages (the token also needs `write:packages`), release artifacts only, or
  nuget.org. The commented step in the `pack` job is ready for GitHub Packages once that is chosen.
- **The licence (D3).** The repository has no LICENSE and the packages name no licence. Until the owner chooses one, the packages grant no
  permission to use, copy or modify them. Do not add a licence expression to the metadata in `Directory.Build.props` without that decision.
- **The sherpa-onnx native library (D2).** `Vocaterra.Nlp.Asr` and `Vocaterra.Nlp.Response` depend on `org.k2fsa.sherpa.onnx` 1.13.8, and its native
  build contains espeak-ng (GPL-3.0) unless a GPL-free release is pinned. Do not distribute those two packages, or anything that includes them,
  until D2 is settled. Packing and the sample are not affected.
- **Lexicon terms (D4).** The Wiktionary-derived lexicon in the content bundle is CC BY-SA and the frequency-list terms are unconfirmed; both
  matter only for a public release of a bundle.
- **Stability.** 0.1.0 promises no API stability. The boundary in [ADR 0009](adr/0009-core-and-game-boundary.md) is exercised first by the game
  repository (M19c).
