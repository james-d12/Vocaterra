# Packaging

This is how the language core ships as NuGet packages and how a consuming repository takes them: the versions, `scripts/pack.sh` and the
local feed it fills, the two ways to consume the core, the sample host that proves the packages are complete, the content bundle archive, the
parked CI job, and what is still undecided. The plan is `IMPLEMENTATION_PLAN.md` section 8.2 (M19b); the owner decisions are in
[DECISIONS.md](DECISIONS.md). D3 (licence) and D5 (feed) were decided on 2026-10-21; D2 and D4 are open.

## Versions and identity

- Every library under `src/` is one package, and its package id is its project name: `Vocaterra.Nlp.Core`, `Vocaterra.Nlp.Audio`,
  `Vocaterra.Nlp.Asr`, `Vocaterra.Nlp.Phonology`, `Vocaterra.Nlp.Pronunciation`, `Vocaterra.Nlp.Language`, `Vocaterra.Nlp.Evaluation`,
  `Vocaterra.Nlp.Decision`, `Vocaterra.Nlp.Response`, `Vocaterra.Nlp.Content` and `Vocaterra.Nlp.Pipeline`.
- The twelfth package is the content tool, `Vocaterra.Nlp.ContentBuilder`, a dotnet tool whose command is `vocaterra-content` (see
  [The content tool](#the-content-tool)).
- The version is `VersionPrefix` 0.1.0 in `Directory.Build.props`, the same for all twelve. A project reference becomes a package
  dependency at that version, so the packages depend on each other at 0.1.0.
- Tests and samples are never packed: `IsPackable` is false in `Directory.Build.props` and true only under `src/`. The one tool that is packed
  is `tools/Vocaterra.Nlp.ContentBuilder`, which sets `IsPackable`, `PackAsTool` and its package id in its own project file; the other tools are not
  packages.
- The content bundle has its own version, `contentVersion` 1.0.0 in `content/v1/manifest.json` (bundle id `v1`), and it is not in the packages.
  Models are not in the packages either; a host gives them a directory through `VOCATERRA_MODELS_DIR`.
- Each package holds its assembly, its XML documentation, the shared readme (`docs/PACKAGE_README.md`, shipped as `README.md`) and a `.snupkg`
  with the symbols. The nuspec names the repository and the commit it was built from.
- Builds are deterministic (`Deterministic` is on in `Directory.Build.props`), and `ContinuousIntegrationBuild` is set when the `CI` environment
  variable is present.
- The licence is Apache-2.0 (D3). Every package sets `PackageLicenseExpression` to `Apache-2.0` and carries the repository's `LICENSE` and
  `NOTICE` at its root. The Wiktionary-derived lexicon in the content bundle is CC BY-SA 4.0 and is not in any package.

## Pack

```bash
scripts/pack.sh
```

The script runs `dotnet pack Vocaterra.Nlp.sln -c Release -o artifacts/packages`, which writes a `.nupkg` and a `.snupkg` for each of the twelve
packages (the eleven libraries and the tool), and then copies the `.nupkg` files into `artifacts/feed`. Both folders are emptied first, so a run leaves exactly the current tree's
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

## Consuming the core

No package is published to a hosted feed, and none is planned: the decision is D5 (decided 2026-10-21). A consuming repository, such as the
Vocaterra game, adds this repository as a submodule and uses it in one of two ways:

```bash
git submodule add https://github.com/james-d12/Vocaterra.NLP.git external/Vocaterra.NLP
git submodule update --init
```

1. **Project references.** Reference the projects directly, for example
   `<ProjectReference Include="external/Vocaterra.NLP/src/Vocaterra.Nlp.Pipeline/Vocaterra.Nlp.Pipeline.csproj" />`. The consumer then builds the
   core from the commit its submodule pins, and no pack step is needed.
2. **A local feed.** Run `bash external/Vocaterra.NLP/scripts/pack.sh`, then add a `nuget.config` beside the consumer's solution. It reads the
   packed folder and nuget.org, which the core lists for its third-party dependencies, in the same shape as `samples/HostConsole/nuget.config`:

   ```xml
   <configuration>
     <packageSources>
       <clear />
       <add key="vocaterra-local" value="external/Vocaterra.NLP/artifacts/feed" />
       <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
     </packageSources>
   </configuration>
   ```

   The consumer then references the `Vocaterra.Nlp.*` packages at version `0.1.0`.

Project references always build from the pinned commit, but they compile the core's sources into the consumer and never exercise the packaged
surface. The local feed does exercise the packaged surface, which is what `samples/HostConsole` tests, at the cost of running `scripts/pack.sh`
after each change to the core.

GitHub Packages and nuget.org are not planned as publishing targets. No `Vocaterra.Nlp.*` package goes to either.

## The sample host

`samples/HostConsole` is a console host written against the packages, the way a host application would consume them. It is not in `Vocaterra.Nlp.sln`,
it references the packages at version 0.1.0 and not the projects, and its `nuget.config` is the one above. It does what the document
[API.md](API.md) describes, in order: loads `content/v1`, builds the services with the scripted recognizer (no model files), builds the
pipeline and a conversation context, evaluates `yo tiene dos espada` on `offer-items-open` and `cuánto cuesta la espada` on `ask-price-open`,
and prints the verdicts. It exits 1 unless the first utterance has exactly the errors `conj.person` and `noun.number`, and the second scores 1.

```bash
scripts/sample-host.sh              # packs, restores and builds the sample against artifacts/feed, then runs it on content/v1
scripts/sample-host.sh <bundleDir>  # the same on another bundle directory
```

The output on the committed bundle (hash `cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1`):

```
bundle v1 1.0.0, hash cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1
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

## The content tool

`Vocaterra.Nlp.ContentBuilder` is packed as a dotnet tool, package `Vocaterra.Nlp.ContentBuilder` 0.1.0, with the command `vocaterra-content`. It
runs the same commands as the project (`validate`, `hash`, `pack`, `verify-pack`, `cefr-check`, `import-lexicon`, `lexicon-report`). A consuming
repository installs it from the local feed into a folder of its own, so it stays out of the global tool list:

```bash
dotnet tool install --tool-path <dir> --add-source <core>/artifacts/feed Vocaterra.Nlp.ContentBuilder --version 0.1.0
<dir>/vocaterra-content validate content/v1
<dir>/vocaterra-content hash content/v1
<dir>/vocaterra-content pack content/v1 out/bundle-v1.zip
<dir>/vocaterra-content verify-pack out/bundle-v1.zip
```

`<core>` is the language core checkout, after `scripts/pack.sh` has filled its `artifacts/feed`. On a machine with a user-local SDK the
launcher needs `DOTNET_ROOT` set to that SDK, as the other commands in this document do. A consuming repository's content script calls
this command instead of `dotnet run` on the project.

Size and dependencies, measured on 2026-10-21 with `scripts/pack.sh`:

- The `.nupkg` is 218.5 MB, and it has no NuGet dependencies. `PackAsTool` copies every dependency's assemblies and native libraries into
  `tools/net10.0/any`, for every runtime identifier, so the package is self-contained. Installed under `--tool-path` it takes about 0.8 GB.
- The native libraries are about 210 MB of the package: `onnxruntime` (Microsoft.ML.OnnxRuntime, for every platform including iOS and Android),
  `libvosk` (Vosk) and `libsherpa-onnx-c-api` (sherpa-onnx, whose build contains espeak-ng under GPL-3.0; D2). The `Vocaterra.Nlp.*` assemblies
  are about 0.4 MB compressed.
- The tool's own sources use `Vocaterra.Nlp.Content` and `Vocaterra.Nlp.Language`; they do not use `Vocaterra.Nlp.Pipeline`, so that project
  reference could be dropped. It would not remove the natives: `Vocaterra.Nlp.Content` references `Asr`, `Response` and `Pronunciation`, and those
  carry sherpa-onnx, Vosk and ONNX Runtime. Restructuring the references is a separate decision and is not made here.

The tool package is for local installation from the feed. It is not for distribution until D2 is settled.

## Continuous integration

The parked workflow (`.github/workflows-pending/ci.yml`) has a `pack` job on Ubuntu: restore, `scripts/pack.sh`, `scripts/sample-host.sh`, and
an upload of `artifacts/packages/*` as the `packages` artifact. It runs on pushes to `main`, on `v*` tags and on pull requests. Tag pushes also run
the other push jobs, because their `if:` matches every push. The workflow can be pushed only once the token has the `workflow` scope (D1, and
`.github/workflows-pending/README.md`).

The publish step is commented out in that job and stays so: no hosted feed is planned (D5, decided 2026-10-21). The step is kept only as a
record of what a GitHub Packages publish would need (the token would also need `write:packages`).

## Still undecided

- **The sherpa-onnx native library (D2).** `Vocaterra.Nlp.Asr` and `Vocaterra.Nlp.Response` depend on `org.k2fsa.sherpa.onnx` 1.13.8, and its native
  build contains espeak-ng (GPL-3.0) unless a GPL-free release is pinned. Do not distribute those two packages, or anything that includes them,
  until D2 is settled. Packing and the sample are not affected.
- **The content tool (D2).** `Vocaterra.Nlp.ContentBuilder` carries the same native libraries as the libraries that reference sherpa-onnx and
  Vosk (see [The content tool](#the-content-tool)). It is for local installation from the feed only, and is not to be published or shared until
  D2 is settled.
- **Lexicon terms (D4).** The Wiktionary-derived lexicon in the content bundle is CC BY-SA and the frequency-list terms are unconfirmed; both
  matter only for a public release of a bundle.
- **Stability.** 0.1.0 promises no API stability. The boundary in [ADR 0009](adr/0009-core-and-game-boundary.md) is exercised first by a
  consuming host, the Vocaterra game (M19c).
