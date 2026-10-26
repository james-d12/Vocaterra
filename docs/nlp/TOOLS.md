# Tools, libraries, models and data sources

An inventory of everything external that the language core builds on, runs, downloads or plans to use: runtimes, NuGet packages, native binaries, machine-learning models, data sources and developer tooling, with versions, licenses and sources. It is a hand-checked snapshot of the language core (`nlp/`, in the Vocaterra repository) at commit `a642066` (2026-10-22, the M50 base); regenerate the numbers when a dependency changes. It replaces the snapshot of 2026-10-20 (commit `4a91cad`), which was taken when the core was a repository of its own.

**How this was compiled.** From the repository root's `global.json` and `Directory.Packages.props`, the root `Directory.Build.props`, every `*.csproj`, the output of `scripts/license-scan.sh --offline` (run on 2026-10-22 after `dotnet restore`: 28 packages, 28 pass; see the note on the counts below), the cached `.nuspec` files and native binaries of the restored packages, `models/manifest.json`, `scripts/*.sh`, `.github/workflows-pending/ci.yml`, `content/game-v1/ATTRIBUTION.md` and `provenance.json`, `docs/nlp/history/HIGH_LEVEL_DOCS.md`, `IMPLEMENTATION_PLAN.md` and `docs/nlp/FUZZING_PLAN.md`.

**Counts.** The licence scan at the M50 base reports 28 packages, one more than the 27 of the earlier snapshot. Every one of the 28 is named in this file, but the package-by-package tables (sections 2, 3 and 9) were not re-audited for that difference, so their counts still read as they were at `4a91cad`. The scan is the authority for the total.

**Conventions.** A license is the SPDX identifier the package metadata or the manifest declares, unless a cell says otherwise. Anything this inventory could not confirm from files in the repository or from the restored packages is marked **unverified**. Items that are only planned are in section 8 and are not dependencies of the code.

| Section | Contents |
| --- | --- |
| [1](#1-runtime-and-sdk) | .NET SDK, target framework, ICU |
| [2](#2-nuget-packages-direct) | The 7 packages referenced by projects |
| [3](#3-nuget-packages-transitive) | The 20 packages restored because of them |
| [4](#4-native-runtimes-bundled-by-packages) | Vosk/Kaldi, sherpa-onnx and ONNX Runtime binaries per platform |
| [5](#5-models) | The 5 models in `models/manifest.json` |
| [6](#6-data-sources) | Wiktionary and its extraction tool, the word frequency list and its corpus, CEFR lists, hand-authored content |
| [7](#7-developer-tooling) | git, shell tools, GitHub Actions, scripts |
| [8](#8-planned-or-referenced-but-not-used) | espeak-ng, Qwen3, CsCheck and other future items |
| [9](#9-license-summary) | Counts by license and the GPL components |
| [10](#10-findings-to-follow-up) | Things noticed while inventorying |

## 1. Runtime and SDK

| Item | Value | Defined in | License / note |
| --- | --- | --- | --- |
| .NET SDK | `10.0.402`, `rollForward: latestPatch` | [`global.json`](../../global.json) (repository root) | MIT (`LICENSE.txt` of the SDK install). Source: https://github.com/dotnet/sdk |
| Target framework | `net10.0` for every project | [`Directory.Build.props`](../../Directory.Build.props) | .NET runtime is MIT: https://github.com/dotnet/runtime |
| .NET runtime on the authoring machine | `Microsoft.NETCore.App 10.0.13` (not pinned by the repository; only the SDK is) | `dotnet --list-runtimes` | MIT |
| C# language version | `latest` | `Directory.Build.props` | - |
| Compiler policy | `Nullable` enable, `ImplicitUsings` enable, `TreatWarningsAsErrors` true, `Deterministic` true, `EnableNETAnalyzers` true, `AnalysisLevel` latest, XML docs generated for `src/`, `ContinuousIntegrationBuild` when `CI` is set | `Directory.Build.props` | .NET analyzers ship with the SDK |
| Package versions | central: `ManagePackageVersionsCentrally` true | [`Directory.Packages.props`](../../Directory.Packages.props) (repository root) | agents may not add packages (plan section 1) |
| Globalization / ICU | `InvariantGlobalization` is **false**, so .NET uses the operating system's ICU for Unicode normalization (NFC, NFKC). On Linux the `libicu` shared library must be installed (the authoring machine has ICU 74). Pinning an app-local ICU is an open item for an ADR | `Directory.Build.props`; [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) section 3.5 | ICU license for the system version: **unverified** (Unicode License family); not shipped by the repository |
| Base class library features used | `System.Text.Json` (canonical JSON, content files, `Utf8JsonReader` for the lexicon), `System.Security.Cryptography` SHA-256 (hashing), `System.Text.RegularExpressions` (kaikki importer), `string.Normalize` (NFC / NFKC) | `src/Vocaterra.Nlp/Core` (`CanonicalJson`, `DeterministicHash`), `src/Vocaterra.Nlp/Language`, `src/Vocaterra.Nlp/Phonology`, `src/Vocaterra.Nlp/Content`, `tools/Vocaterra.Nlp.ContentBuilder` | part of the runtime, MIT |
| Source languages | C#, plus bash and embedded python3 in `scripts/`, YAML for CI, JSON / JSONL for content | - | - |
| OS / CPU targets | `ubuntu-latest`, `ubuntu-24.04-arm`, `windows-latest`, `macos-latest` (CI matrix, currently parked, see section 7) | [`.github/workflows-pending/ci.yml`](../../.github/workflows-pending/ci.yml) | - |
| Minimum hardware | 8 GB RAM, 4-core x86-64, no GPU; CPU-only inference, 1 intra-op and 1 inter-op thread for ONNX Runtime sessions | `IMPLEMENTATION_PLAN.md` section 1; `OnnxEmbeddingModel`, `Wav2Vec2PhonemeRecognizer`, `KokoroSynthesizer` | - |

## 2. NuGet packages: direct

Seven packages are referenced by projects. The first three are product dependencies, the last four are test-only (referenced by the core's test projects, with `PrivateAssets="all"` for the runner and coverage collector). the engine's `Core`, `Audio`, `Phonology`, `Evaluation` and `Decision` folders and the three tools reference no package themselves; the D0 core has no third-party dependency.

| Package | Version | Purpose | License (how determined) | Referenced by | Source |
| --- | --- | --- | --- | --- | --- |
| `Vosk` | 0.3.38 | Managed P/Invoke wrapper plus the native Kaldi-based `libvosk`; grammar-constrained and open word recognition (`VoskWordRecognizer`) | Apache-2.0 (nuspec) | [`src/Vocaterra.Nlp`](../../src/Vocaterra.Nlp/Vocaterra.Nlp.csproj) (`Asr`) | https://alphacephei.com/vosk/ , https://github.com/alphacep/vosk-api |
| `org.k2fsa.sherpa.onnx` | 1.13.8 | sherpa-onnx managed API: Parakeet open recognition (`SherpaOnnxOfflineRecognizer`) and Kokoro speech synthesis (`KokoroSynthesizer`) | Apache-2.0 (nuspec) | [`src/Vocaterra.Nlp`](../../src/Vocaterra.Nlp/Vocaterra.Nlp.csproj) (`Asr`, `Response`) | https://github.com/k2-fsa/sherpa-onnx |
| `Microsoft.ML.OnnxRuntime` | 1.31.0 | ONNX Runtime for the wav2vec2 phoneme recognizer (`Wav2Vec2PhonemeRecognizer`) and the multilingual-e5-small embedder (`OnnxEmbeddingModel`) | MIT (reviewed override 2026-10-20: the nuspec only points to a `LICENSE` file; see [`scripts/license-overrides.txt`](../../scripts/license-overrides.txt)) | [`src/Vocaterra.Nlp`](../../src/Vocaterra.Nlp/Vocaterra.Nlp.csproj) (`Language`, `Pronunciation`) | https://github.com/microsoft/onnxruntime |
| `xunit` | 2.9.3 | Unit-test framework (plain `Assert`, plan section 1) | Apache-2.0 (nuspec) | all 12 test projects | https://github.com/xunit/xunit |
| `xunit.runner.visualstudio` | 2.8.2 | Test adapter for `dotnet test` | Apache-2.0 (nuspec) | all 12 test projects | https://github.com/xunit/visualstudio.xunit |
| `Microsoft.NET.Test.Sdk` | 18.11.0 | Test host and MSBuild integration | MIT (nuspec) | all 12 test projects | https://github.com/microsoft/vstest |
| `coverlet.collector` | 10.1.0 | Code-coverage collector (referenced; no script or CI step invokes coverage yet) | MIT (nuspec) | all 12 test projects | https://github.com/coverlet-coverage/coverlet |

The three tools (`Vocaterra.Nlp.Cli`, `Vocaterra.Nlp.Bench`, `Vocaterra.Nlp.ContentBuilder`) and the engine `Vocaterra.Nlp` (its `Content` and `Pipeline` folders included) receive the product packages and their 14 transitive packages (Vosk 1, sherpa-onnx 10, ONNX Runtime 3) only through project references.

## 3. NuGet packages: transitive

Restored because of the direct packages; every one passed `scripts/license-scan.sh --offline`.

| Package | Version | License (how determined) | Pulled in by | Notes |
| --- | --- | --- | --- | --- |
| `Microsoft.ML.OnnxRuntime.Managed` | 1.31.0 | MIT (reviewed override; nuspec ships `LICENSE.txt`) | `Microsoft.ML.OnnxRuntime` | .NET API surface (`net8.0` / `netstandard2.0` assemblies) |
| `System.Numerics.Tensors` | 9.0.0 | MIT (nuspec) | `Microsoft.ML.OnnxRuntime.Managed` | https://github.com/dotnet/runtime |
| `org.k2fsa.sherpa.onnx.runtime.linux-x64` | 1.13.8 | Apache-2.0 (nuspec) | `org.k2fsa.sherpa.onnx` | native pack, see section 4 |
| `org.k2fsa.sherpa.onnx.runtime.linux-arm64` | 1.13.8 | Apache-2.0 (nuspec) | `org.k2fsa.sherpa.onnx` | native pack |
| `org.k2fsa.sherpa.onnx.runtime.win-x64` | 1.13.8 | Apache-2.0 (nuspec) | `org.k2fsa.sherpa.onnx` | native pack |
| `org.k2fsa.sherpa.onnx.runtime.win-x86` | 1.13.8 | Apache-2.0 (nuspec) | `org.k2fsa.sherpa.onnx` | native pack |
| `org.k2fsa.sherpa.onnx.runtime.win-arm64` | 1.13.8 | Apache-2.0 (nuspec) | `org.k2fsa.sherpa.onnx` | native pack |
| `org.k2fsa.sherpa.onnx.runtime.osx-x64` | 1.13.8 | Apache-2.0 (nuspec) | `org.k2fsa.sherpa.onnx` | native pack |
| `org.k2fsa.sherpa.onnx.runtime.osx-arm64` | 1.13.8 | Apache-2.0 (nuspec) | `org.k2fsa.sherpa.onnx` | native pack |
| `org.k2fsa.sherpa.onnx.runtime.android-x64` | 1.13.8 | Apache-2.0 (nuspec) | `org.k2fsa.sherpa.onnx` | native pack (restored although no Android target exists) |
| `org.k2fsa.sherpa.onnx.runtime.android-arm64` | 1.13.8 | Apache-2.0 (nuspec) | `org.k2fsa.sherpa.onnx` | native pack (same remark) |
| `Microsoft.TestPlatform.TestHost` | 18.11.0 | MIT (nuspec) | `Microsoft.NET.Test.Sdk` | test projects only |
| `Microsoft.TestPlatform.ObjectModel` | 18.11.0 | MIT (nuspec) | `Microsoft.TestPlatform.TestHost` | test projects only |
| `Microsoft.CodeCoverage` | 18.11.0 | MIT (nuspec) | `Microsoft.NET.Test.Sdk` | test projects only |
| `xunit.core` | 2.9.3 | Apache-2.0 (nuspec) | `xunit` | test projects only |
| `xunit.assert` | 2.9.3 | Apache-2.0 (nuspec) | `xunit` | test projects only |
| `xunit.analyzers` | 1.18.0 | Apache-2.0 (nuspec) | `xunit` | Roslyn analyzers, build time |
| `xunit.extensibility.core` | 2.9.3 | Apache-2.0 (nuspec) | `xunit.core` | test projects only |
| `xunit.extensibility.execution` | 2.9.3 | Apache-2.0 (nuspec) | `xunit.core` | test projects only |
| `xunit.abstractions` | 2.0.3 | Apache-2.0 (reviewed override 2026-10-20: nuspec names no license; evidence https://raw.githubusercontent.com/xunit/xunit/master/license.txt) | `xunit.extensibility.core` | test projects only |

Totals: 7 direct + 20 transitive = **27 packages** (the scan reports `pass: 27, fail: 0, unknown: 0`). Three of them (`Microsoft.ML.OnnxRuntime`, `Microsoft.ML.OnnxRuntime.Managed`, `xunit.abstractions`) rely on a reviewed override rather than their own metadata.

## 4. Native runtimes bundled by packages

The NuGet packages above contain compiled native code that the .NET assemblies load at run time. These binaries are what the license scan cannot see into.

| Component | Delivered by | Files (size in bytes) | Declared license | Notes |
| --- | --- | --- | --- | --- |
| **Vosk / Kaldi** (`libvosk`) | `Vosk` 0.3.38, folder `build/lib/`, copied to the output directory by `Vosk.targets` | `linux-x64/libvosk.so` (25,994,752); `win-x64/libvosk.dll` (25,989,632) with `libgcc_s_seh-1.dll` (1,266,641), `libstdc++-6.dll` (19,442,799), `libwinpthread-1.dll` (592,104); `osx-universal/libvosk.dylib` (12,533,472). Managed wrapper `Vosk.dll` (7,680) | Apache-2.0 for the package | A strings scan of the Linux library shows OpenBLAS and CLAPACK code statically linked in (their licenses: **unverified**). The three `win-x64` MinGW runtime DLLs are separate works (GCC runtime library, winpthreads); their licenses are **unverified** and are not covered by the package's SPDX expression |
| **sherpa-onnx C API** (`libsherpa-onnx-c-api`) with its **own ONNX Runtime build** (`libonnxruntime`) | one runtime pack per platform, pulled in by `org.k2fsa.sherpa.onnx` 1.13.8; managed wrapper `sherpa-onnx.dll` (51,200, `net10.0` build) | `linux-x64`: `libsherpa-onnx-c-api.so` (5,120,672), `libonnxruntime.so` (27,026,609); `win-x64`: `sherpa-onnx-c-api.dll` (4,605,952), `onnxruntime.dll` (17,799,168); `osx-arm64`: `libsherpa-onnx-c-api.dylib` (4,178,984), `libonnxruntime.dylib` (29,006,384); equivalent pairs for `linux-arm64`, `win-x86`, `win-arm64`, `osx-x64`, `android-x64`, `android-arm64` | Apache-2.0 for every pack | The Linux C API library contains **espeak-ng** entry points (`espeak_Initialize`, `espeak_TextToPhonemesWithTerminator`, "Failed to initialize espeak-ng with data dir"), so the GPL-3.0 phonemizer is linked into this Apache-2.0 package (verified by `strings` on the `linux-x64` pack; other platforms **unverified**). It also embeds `kaldifst`. See section 9 |
| **ONNX Runtime** (Microsoft) | `Microsoft.ML.OnnxRuntime` 1.31.0, folder `runtimes/<rid>/native/` | `linux-x64/libonnxruntime.so` (29,345,384) and `libonnxruntime_providers_shared.so`; `linux-arm64` (25,266,408); `win-x64/onnxruntime.dll` (17,253,176); `win-arm64` (17,507,640); `osx-arm64/libonnxruntime.dylib` (44,830,288); `android/onnxruntime.aar`; `ios/onnxruntime.xcframework.zip` | MIT (package metadata points to a `LICENSE` file; reviewed override) | Ships `ThirdPartyNotices.txt` (344,457 bytes) listing its own third-party code. The adapters use the CPU provider only (1 intra-op and 1 inter-op thread) |
| **.NET runtime, C++ runtime, libc** | the operating system or the .NET install | `libstdc++.so.6`, `libgcc_s.so.1`, `libm`, `libc`, `libdl`, `librt`, `libpthread` (from `ldd` on the three Linux libraries) | system licenses, **unverified** | Native libraries are not self-contained: a Linux host needs a C++ runtime installed |

Platform coverage of the three native families (from the package contents):

| RID | Vosk | sherpa-onnx | Microsoft.ML.OnnxRuntime |
| --- | --- | --- | --- |
| linux-x64 | yes | yes | yes |
| linux-arm64 | **no** | yes | yes |
| win-x64 | yes (plus 3 MinGW DLLs) | yes | yes |
| win-arm64 | **no** | yes | yes |
| win-x86 | **no** | yes | **no** |
| osx-arm64 | yes (`osx-universal`) | yes | yes |
| osx-x64 | yes (`osx-universal`) | yes | **no** |
| android-x64, android-arm64 | no | yes | `android/onnxruntime.aar` |
| ios | no | no | `ios/onnxruntime.xcframework.zip` |

## 5. Models

Model files are never committed ([`.gitignore`](../../.gitignore) excludes `models/*` except the manifest and README); `scripts/fetch-models.sh` downloads them and verifies the SHA-256 values in [`models/manifest.json`](../../models/manifest.json). D1 adapters run only when `VOCATERRA_MODELS_DIR` is set; otherwise the real-model tests are skipped. Sizes are the archive bytes from the manifest unless stated.

| Model | Role | Version | Size | License | Source | Status |
| --- | --- | --- | --- | --- | --- | --- |
| **Vosk small Spanish** `vosk-model-small-es-0.42` | `asr-constrained` (grammar-constrained and open decoding via `Vocaterra.Nlp.Asr`) | 0.42 | 39,817,833 (zip); about 60.3 MB over 14 extracted files | Apache-2.0 | https://alphacephei.com/vosk/models/vosk-model-small-es-0.42.zip | **fetched and fully pinned**: archive and all 14 files hashed from a real download on 2026-10-20. Stock model dithers, so `VoskWordRecognizer` loads a runtime copy with `--dither=0.0` ([`models/README.md`](../../models/README.md)) |
| **NVIDIA Parakeet TDT 0.6B v3 int8** `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` | `asr-open` (`SherpaOnnxOfflineRecognizer`) | v3-int8 | 487,170,055 (tar.bz2); about 670 MB extracted (encoder 652,184,281, decoder 11,845,275, joiner 6,355,277, tokens 93,939 per the Hugging Face mirror listing; not checked against the tarball) | CC-BY-4.0 (attribution to NVIDIA required) | https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2 | **downloadable, pinned**: archive hash is the GitHub release digest (matched on the M26e download, 2026-10-21); all 8 extracted files pinned |
| **wav2vec2 XLSR-53 espeak CV fine-tuned** `wav2vec2-xlsr-53-espeak-cv-ft` | `phoneme` (`Wav2Vec2PhonemeRecognizer`: posteriors for CTC alignment and GOP scoring) | HF revision `2c733782da56` (full SHA `2c733782da5604684829819a5eb744c193fe9398`, last modified 2021-12-10) | upstream `pytorch_model.bin` 1,263,535,127; the ONNX export is not produced yet, so its size is unknown | Apache-2.0 | https://huggingface.co/facebook/wav2vec2-xlsr-53-espeak-cv-ft | **needs ONNX export**: upstream ships PyTorch weights only; the file list (`model.onnx`, `vocab.json`) is provisional and has no hashes. Fine-tuned on Common Voice per the model card (data not used by this repository) |
| **multilingual-e5-small** | `embedding` (`OnnxEmbeddingModel`: intent rung 3, accepted only with margins) | HF revision `614241f622f5` (full SHA `614241f622f53c4eeff9890bdc4f31cfecc418b3`) | upstream `onnx/model.onnx` 470,268,510; `onnx/tokenizer.json` 17,082,730; `sentencepiece.bpe.model` 5,069,051 (sizes from the Hugging Face API listing, not pinned in the manifest) | MIT | https://huggingface.co/intfloat/multilingual-e5-small | **manifest-only**: no archive and no hashes; the manifest says "needs ONNX export" but the upstream repository already publishes `onnx/model.onnx` (and quantized variants), so copying and pinning is enough. The adapter reads `model.onnx` and `tokenizer.json` (the manifest also lists `sentencepiece.bpe.model`, which the adapter does not read) |
| **Kokoro multi-lang v1.0 (sherpa-onnx)** `kokoro-multi-lang-v1_0` | `tts` (`KokoroSynthesizer` through sherpa-onnx offline TTS) | v1_0 | 349,906,910 (tar.bz2) | Apache-2.0 for the model; the archive also contains `espeak-ng-data/` (GPL-3.0, see section 9) and lexicon files | https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-multi-lang-v1_0.tar.bz2 | **downloadable, pinned**: archive and all 377 files pinned from a real download (M26e, 2026-10-21). The adapter needs `model.onnx`, `voices.bin`, `tokens.txt` and the `espeak-ng-data` directory; `SynthesisRequest.Phones` is accepted but ignored until the in-house G2P replaces the bundled phonemizer (`TODO(ADR)` in `KokoroSynthesizer.cs`) |

Also part of the model pipeline, but in-house and committed: the label map [`src/Vocaterra.Nlp/Pronunciation/Data/espeak-to-vocaterra.json`](../../src/Vocaterra.Nlp/Pronunciation/Data/espeak-to-vocaterra.json) (wav2vec2 phone labels to the `Phonology` inventory) and the SentencePiece Unigram tokenizer `UnigramTokenizer` (reads the model's `tokenizer.json`; the normalization table `precompiled_charsmap` is approximated, see its remarks).

Counts: 5 models; 1 fully pinned, 2 archive-pinned, 2 without archive (1 needs an ONNX export, 1 needs its upstream ONNX file copied in).

## 6. Data sources

| Source | What it supplies | License | Provenance in the repository | Status |
| --- | --- | --- | --- | --- |
| **kaikki.org Spanish extract of English Wiktionary** (wiktextract JSONL, post-processed), `https://kaikki.org/dictionary/Spanish/kaikki.org-dictionary-Spanish.jsonl` | Spanish lemmas, inflected forms, gender and number, imported with `Vocaterra.Nlp.ContentBuilder import-lexicon` into `content/game-v1/lexicon/wiktionary-es.json` | Wiktionary text: CC BY-SA 4.0 (older revisions CC BY-SA 3.0) and GFDL; share-alike applies to the imported file and works derived from it. https://en.wiktionary.org/wiki/Wiktionary:Copyrights , https://creativecommons.org/licenses/by-sa/3.0/ | Pinned in [`scripts/fetch-lexicon-sources.sh`](../../scripts/fetch-lexicon-sources.sh) (1 054 565 723 bytes, SHA-256 `7b23a5dc...b97c`, file of 2026-10-03 from the dump of 2026-09-02); URL, hash, size and retrieval date copied into the sidecar `lexicon/wiktionary-es.source.json`; marked `wiktionary-import` in `provenance.json` and in the file; [`content/game-v1/ATTRIBUTION.md`](../../content/game-v1/ATTRIBUTION.md) carries the notice (the validator enforces it). The download is **not committed** (`lexicon-sources/` is git-ignored) | **in use**: 3 000 lemmas, 40 263 entries, 4.7 MB; the first load of the whole bundle takes about 230 ms (before: 8 ms), see [docs/nlp/LEXICON.md](LEXICON.md). kaikki.org marks this post-processed download "DEPRECATED, will be removed in the near future" (the raw all-languages download is 23.9 GB); the hash pin and the committed file make that survivable. A 20-line fixture, `tests/Vocaterra.Nlp.Tests/Content/Data/kaikki-sample.jsonl`, and a set of synthetic lines exercise the importer |
| **wiktextract** (Tatu Ylonen), https://github.com/tatuylonen/wiktextract | the extraction tool that produced the kaikki.org data; **not used by this repository** | MIT (its README: "licensed under the MIT license"; `LICENSE` is the MIT text, with certain files under `tests/` under the Wiktionary licence). The data it extracts is Wiktionary text (row above) | the paper to cite if the data is used in research is in the attribution notice | tool only; nothing of it is built, run or shipped |
| **hermitdave/FrequencyWords** `content/2018/es/es_50k.txt`, https://github.com/hermitdave/FrequencyWords | the 50 000 most frequent Spanish words with counts; ranks the lemmas that `import-lexicon --frequency --top` keeps | MIT for code, CC-BY-SA-4.0 for content (README at the pinned commit; an older commit of the same repository says CC-BY-SA-3.0 for the content, which is why the commit `525f9b560de45753a5ea01069454e72e9aa541c6` is pinned) | Pinned by commit URL and SHA-256 in `scripts/fetch-lexicon-sources.sh` (658 626 bytes, `dcff3ad4...56c0`); recorded in the sidecar; attribution in `ATTRIBUTION.md`. **Not committed** | **in use** as a ranking only: no count ships |
| **OpenSubtitles 2018** (OPUS), http://opus.nlpl.eu/OpenSubtitles2018.php , subtitles from https://www.opensubtitles.org | the corpus the frequency list counts (P. Lison and J. Tiedemann, LREC 2016; P. Lison, J. Tiedemann and M. Kouylekov, LREC 2018) | the list's author licenses his counts under CC-BY-SA-4.0; the terms of the subtitles themselves are **unverified**. Only a ranking derived from the counts is used, and no subtitle text or count ships | cited in `ATTRIBUTION.md` | indirect; the owner should confirm the terms before release ([ADR 0008](adr/0008-lexicon-sources-and-frequency-filter.md)) |
| **CEFR word lists** `content/game-v1/cefr/a1.txt` (350 lemmas), `a2.txt` (663 lemmas) | Vocabulary limit for dialogue templates and prompt text, checked by `cefr-check` (prompt findings are listed under their prompt id) | Apache-2.0 with the repository (hand-authored; `LICENSE`) | `provenance.json`: `hand-authored`, `approvedBy` null. No external list is cited anywhere in the repository, so whether the lemmas were derived from a published CEFR vocabulary list is **unverified** | in use |
| **Hand-authored lexicons** `content/game-v1/lexicon/seed.json` (1217 entries), `game.json` (51), `world.json` (16) and `supplement.json` (7) | Forms, lemmas, features for analysis and morphology; the seed is also embedded in `Vocaterra.Nlp.Language`. They win over the import where both define a word | not stated | `hand-authored` | in use |
| **Content bundle** `answer-sets/`, `intents/`, `prompts/`, `agents/` (guide, shopkeeper, villager), `dialogue/`, `feedback/`, `effects/`, `evaluation/`, and the game's host files `game/` and `speech/` | The repository's one bundle: the agents, prompts, answer sets, dialogue and policy tables of the game, which the core's tests and demo run on (R02) | not stated | `hand-authored`, `approvedBy` null (the speech clips are `model-output`); hashed by [`content/game-v1/manifest.json`](../../content/game-v1/manifest.json) | in use |
| **G2P golden set** `tests/Vocaterra.Nlp.Golden.Tests/Data/g2p-golden.jsonl` (1,788 lines) | Expected IPA for the Spanish G2P tests | not stated | hand-authored; the plan intends to regenerate it from Wiktionary later (not done) | in use |
| **G2P exception lexicon** `src/Vocaterra.Nlp/Phonology/Data/exceptions.json` | Loanwords and irregular pronunciations | not stated | hand-authored | in use |
| **Golden learner audio** `tests/golden/manifest.jsonl` | Annotations for recognizer benchmarking | n/a | 3 synthetic entries; recordings are gitignored, opt-in, local only, with a consent protocol ([`tests/golden/README.md`](../../tests/golden/README.md)) | no real recordings yet |
| **FSRS-4.5 default weights** | Review scheduling constants in `Vocaterra.Nlp.Decision` | published reference constants, license **unverified** | copied from the open-spaced-repetition reference implementation (`ts-fsrs` 3.5.7 `default_w`, per `FsrsParameters.cs`) | in use |
| **Model training data** | Common Voice (wav2vec2 model card); Parakeet and Kokoro training data not inspected | **unverified** | none; only the pretrained weights are used | not used directly |

The hand-authored content has no third-party text in it, and it is licensed Apache-2.0 with the code (see `LICENSE` and `NOTICE`).

## 7. Developer tooling

| Tool | Version seen on the authoring machine | Used for | Where | License (general knowledge, **unverified**; not distributed) |
| --- | --- | --- | --- | --- |
| `dotnet` CLI | 10.0.402 | `restore`, `build -warnaserror`, `test` (`--logger trx`), `format` / `format --verify-no-changes`, `list package --include-transitive --format json` | [`README.md`](README.md), `ci.yml`, `scripts/license-scan.sh` | MIT |
| NuGet | bundled in the SDK | package restore from nuget.org; license scan reads `~/.nuget/packages` or `NUGET_PACKAGES` | `scripts/license-scan.sh` | Apache-2.0 / MIT |
| NuGet registration API | `https://api.nuget.org/v3/registration5-gz-semver2/{id}/{version}.json` | online license lookup when a nuspec names no license (skipped with `--offline`) | `scripts/license-scan.sh` | service |
| `git` | 2.43.0 | version control; LF normalization via [`.gitattributes`](../../.gitattributes) (`* text=auto eol=lf`, `*.wav` and `*.onnx` binary); worktrees for parallel agents | repository | GPL-2.0 |
| `gh` (GitHub CLI) | 2.102.0 | `gh auth refresh -h github.com -s workflow` to activate the parked workflow | [`.github/workflows-pending/ci.yml`](../../.github/workflows-pending/ci.yml) | MIT |
| `bash` | not recorded | `scripts/fetch-models.sh`, `scripts/fetch-lexicon-sources.sh`, `scripts/license-scan.sh` (`set -euo pipefail`) | [`scripts/`](../../scripts/README.md) | GPL-3.0-or-later |
| `python3` | 3.12.3, standard library only, run as `python3 -I` | parses `models/manifest.json` in the fetch script and writes `lexicon-sources/sources.json` in the lexicon fetch script; evaluates SPDX expressions, reads nuspecs and calls the NuGet API in the license scan (`json`, `re`, `gzip`, `urllib`, `xml.etree`, `datetime`, `os`, `time`) | both scripts | PSF-2.0 |
| `python3` with `yaml` (YAML syntax check of `ci.yml`) | **unverified**: named in the task brief, not referenced anywhere in the repository | validating the parked workflow file | - | - |
| `curl` | 8.5.0 | model and lexicon-source downloads (`--fail --retry 3 --continue-at -`) | `scripts/fetch-models.sh`, `scripts/fetch-lexicon-sources.sh` | curl license (MIT-style) |
| `sha256sum` or `shasum -a 256` | coreutils 9.4 | archive and file hashes | `scripts/fetch-models.sh`, `scripts/fetch-lexicon-sources.sh` | GPL-3.0-or-later (coreutils) |
| `unzip`, `tar` with bzip2 | UnZip 6.00, GNU tar 1.35 | extracting `.zip` and `.tar.bz2` model archives | `scripts/fetch-models.sh` | Info-ZIP / GPL-3.0-or-later |
| coreutils (`mktemp`, `cut`, `wc`, `tr`, `basename`) | coreutils 9.4 | fetch script housekeeping | `scripts/fetch-models.sh` | GPL-3.0-or-later |
| `.editorconfig` | - | 4-space C#, 2-space JSON/YAML/XML, LF, file-scoped namespaces as a warning; enforced by `dotnet format` | [`.editorconfig`](../../.editorconfig) | - |
| Mermaid | rendered by GitHub | flowcharts in `docs/nlp/history/HIGH_LEVEL_DOCS.md` | `docs/nlp/history/HIGH_LEVEL_DOCS.md` | MIT |
| Temurin JRE | 21.0.13+9 (`tools/manifest.json`) | the Java runtime of the LanguageTool server (M30); a separate local process, never linked or shipped; started by `scripts/languagetool.sh` (repository root) | `scripts/fetch-tools.sh` installs it under `${VOCATERRA_TOOLS_DIR:-~/.local/share/vocaterra/tools}` | GPL-2.0 with the classpath exception (declared in the manifest; D10) |
| LanguageTool | 6.6 (`tools/manifest.json`) | grammar check of the authored content (M30, `lint`); a separate local server on localhost, no n-gram data installed | `scripts/fetch-tools.sh` and `scripts/languagetool.sh` (repository root) | LGPL-2.1 (declared in the manifest; D10) |
| draw.io (diagrams.net) | file format only | the hand-written overview diagram | [`docs/nlp/diagrams/system-overview.drawio`](diagrams/system-overview.drawio) | Apache-2.0 |

**GitHub Actions** (workflow parked in `.github/workflows-pending/` because the push token lacks the `workflow` scope, so nothing runs in CI today). Licenses of the actions: MIT per their upstream repositories, **unverified**.

| Action / runner | Version | Used in job | Purpose |
| --- | --- | --- | --- |
| [`actions/checkout`](https://github.com/actions/checkout) | `v4` | all 4 jobs | fetch the repository |
| [`actions/setup-dotnet`](https://github.com/actions/setup-dotnet) | `v4` (reads `global.json`) | all 4 jobs | install the pinned SDK |
| [`actions/upload-artifact`](https://github.com/actions/upload-artifact) | `v4` | build-and-test, golden, models | upload `.trx` test results |
| [`actions/cache`](https://github.com/actions/cache) | `v4` | models | cache the Vosk model, keyed by `hashFiles('models/manifest.json')` |
| Runners | `ubuntu-latest`, `ubuntu-24.04-arm`, `windows-latest`, `macos-latest` | build-and-test matrix | cross-platform determinism; golden and license jobs run on `ubuntu-latest` |
| Repository variable | `VOCATERRA_RUN_MODEL_TESTS == 'true'` | models job | opt-in model-backed tests |

**Network endpoints the repository's scripts contact:** `alphacephei.com` (Vosk model zip), `github.com` release downloads from `k2-fsa/sherpa-onnx` (Parakeet, Kokoro), `api.nuget.org` and the NuGet feed (restore, license lookup). The development-time LanguageTool archive comes from `languagetool.org` and the Temurin JRE from `github.com/adoptium` (both by `scripts/fetch-tools.sh`; the servers themselves run on localhost). `huggingface.co` (wav2vec2, multilingual-e5-small) is referenced but no script downloads from it yet.

**In-repository tools** (first-party, not third-party): `tools/Vocaterra.Nlp.ContentBuilder` (validate, hash, lint, cefr-check, import-lexicon, lexicon-report), `tools/Vocaterra.Nlp.Cli` (eval, replay), `tools/Vocaterra.Nlp.Bench` (WER, learner-error preservation, latency).

### Content lint

`vocaterra-content lint <bundleDir> [--strict] [--level a1|a2] [--fail-on-level] [--json] [--prompts a,b] [--host-pools p*,q]` (library:
`ContentLinter.Lint(bundle, options)` in `Vocaterra.Nlp.Content`, see [docs/nlp/API.md](API.md)). No new dependency: it runs the real
`LinguisticAnalyzer`, `IntentResolver` (no embedding model, so the grammar and rule rungs only) and `TextGrader` on the bundle's own content.
It does not verify hashes. Output is deterministic: one finding per line, ordinal-sorted by path, check id, ids and message, as
`severity check path [ids] message`, then `lint: N errors, N warnings, N info, N waived`. A waived finding starts with `waived` and ends with
`(was <severity>; waiver: ...)`. `--json` prints `{"summary":{...},"findings":[...]}` instead.

**Exit codes.** `lint`: 0 no errors (and, with `--strict`, no warnings); 1 errors found, or warnings with `--strict`, or the bundle does not load,
or `--prompts` names an unknown prompt, or the level has no CEFR list; 2 the command line is wrong. Info and waived findings never fail.
`validate` keeps its codes (0 valid, 1 invalid, 2 usage), prints the lint's errors and warnings after its `OK:` line, and returns 1 for them only
with `--strict`.

| Check id | Severity | What it finds |
| --- | --- | --- |
| `coverage.uncovered` | error | An intent example that neither a grammar nor a keyword rule of its own intent resolves; prints a suggested grammar (closed-class words literal, open-class words as `[POS]` or `[VERB:lemma]`, a noun that varies between the intent's examples as `[NOUN:slot]`), a hint only |
| `coverage.rule-only` | warning | An example only a keyword rule covers (rung `rules`, no grammar matches) |
| `coverage.unknown-word` | warning | An example with a word the lexicon lacks (unless the example expects `vocab.unknown`, or a `[WORD]` element binds it as a name) |
| `confusion.prompt` | warning, error | An example of intent A that intent B's grammar also matches, or the resolver would give to B, where some prompt allows both. Warning when A still wins (by the specificity tie-break); error when the resolver gives the example to B, or when a prompt's whole allow-list gives it to another intent. Names the grammar that matched and the prompts |
| `confusion.catalog` | info | The same overlap when no prompt allows both intents (harmless today, a trap if a prompt ever allows both). The game's bundle reports `request_item` and `state_want` sharing `necesito agua`, waived by the intents' own `lint` blocks |
| `hazard.optional-word` | warning | An optional `[WORD]` next to an optional element in a grammar |
| `hazard.lexicon-gap` | error | A grammar literal, a `[VERB:lemma]` lemma or a keyword-rule lemma the lexicon does not know |
| `hazard.homograph` | info | A `[VERB:x]` lemma some of whose forms are also noun forms (`cuesta`). Info, because inside the grammar the typed element binds the verb reading; the noun reading only matters outside it |
| `reach.pool` | warning | A dialogue pool no agent action uses and the pipeline does not reach by itself (reserved: `not_understood`, `answer_accepted`, `feedback.<kind>[.<tag>]`), or used only by agents no prompt names; a `feedback.` pool whose kind is not a feedback kind |
| `reach.intent` / `reach.answer-set` / `reach.agent` | warning | An intent no prompt allows, an answer set or an agent no prompt names |
| `reach.state` | warning | An agent state that cannot be reached from the initial state |
| `cefr.dialogue` / `cefr.prompt` / `cefr.answer` | warning, error with `--fail-on-level` | A lemma of dialogue templates, prompt text or answer-set canonicals (aliases are not held to the level) not in the CEFR list of `--level` or below |
| `expect.mismatch` | error | An example with `errors` whose verdict's tag set differs |
| `expect.no-prompt` / `expect.unknown-prompt` / `expect.prompt-intent` | warning, error, error | An expectation with no prompt to grade against; one naming a prompt the bundle lacks; one naming a prompt that does not allow the intent |

**Reachability** is measured from the prompts: a host starts a conversation with a prompt, and a prompt names its agent. `--prompts a,b` limits the
roots to those prompts (a game that uses two of the sample's prompts sees everything else listed), and `--host-pools` names pools the host voices
itself without an agent action (exact ids, or a prefix ending in `*`). A pool named like an intent (`greet`) is not treated as reachable: the
planner only falls back to it when an agent transition has no actions.

**Example expectations.** An intent example is a string, or an object `{ "text": "yo es Rosa", "errors": ["conj.person"], "prompt": "introduce-open" }`.
`errors` is the exact set of error tags the grader gives the text (an empty list asserts a clean verdict); string examples state no expectation
and are not graded. The lint grades the text with `TextGrader`, which is the text path of `UtterancePipeline` (a test compares the two):
normalization, answer matching against the prompt's answer set, analysis with the prompt's grammars, intent resolution, verdict assembly, without
pronunciation. The prompt is the example's `prompt` when given (it must allow the intent), otherwise the first **open-mode** prompt (ordinal id) that
allows the intent, or the first of any mode when none is open: an open prompt grades a sentence, while a constrained prompt expects one of its
answer set's phrases and would add `vocab.wrong`. The tags can depend on the prompt's difficulty and answer set, which is why an example may name its
prompt.

**Waivers.** Per intent, `"lint": { "allowOverlap": [...], "allowRuleOnly": [...], "allowHomograph": [...] }`. A waived finding is still printed (as
`waived`, with the waiver) and counted separately; it never fails. There are three kinds, only what the findings needed:

| Waiver | Where | Waives |
| --- | --- | --- |
| `allowOverlap`: intent ids | on either intent of the pair | `confusion.prompt` warnings and `confusion.catalog` info between the two intents, in both directions (`state_want` may take `request_item`'s examples, or `request_item` may declare it); an error (the resolver gives the example to the other intent) is a defect and stays |
| `allowRuleOnly`: example texts | on the example's intent | `coverage.rule-only` for exactly that example |
| `allowHomograph`: verb lemmas | on the intent whose grammar uses the lemma | `hazard.homograph` for that lemma |

Errors cannot be waived: fix the content. The waivers and the example objects are additive schema (schema version stays 1); a catalog that uses
neither serializes byte for byte as before.

## 8. Planned or referenced but not used

**None of the items in this section is a dependency of the code today.** They come from `docs/nlp/history/HIGH_LEVEL_DOCS.md` ("Technology choices", "Licensing", per-step "Non-deterministic considerations"), `IMPLEMENTATION_PLAN.md` and `docs/nlp/FUZZING_PLAN.md`.

| Item | Intended role | License per the HLD | Status | Source |
| --- | --- | --- | --- | --- |
| **espeak-ng** | dev-only diff oracle for the G2P golden set; never shipped | GPL-3.0 | not wired. Note: its code and data already reach the project indirectly through sherpa-onnx and the Kokoro archive (section 9) | HLD steps 3 and Licensing |
| **Piper** (OHF-Voice/piper1-gpl) | none: avoid embedding | GPL-3.0 since 2025 (the original MIT repository is archived) | rejected | https://github.com/OHF-voice/piper1-gpl/releases |
| **piper-plus** (`PiperPlus.Core`) | MIT fork of Piper as a TTS alternative | MIT | alternative only | https://www.nuget.org/packages/PiperPlus.Core/ |
| **Qwen3** (build-time LLM) | drafts synonyms, dialogue, hints and example utterances under the Freeze pattern, with human approval; dev machines only | Apache-2.0 | Phase 3; `ContentProvenance` has an `llm-draft` kind but no LLM code exists, and plan section 3.8 forbids LLM packages in `src/` | HLD Technology choices, step 16 |
| **Gemma** | alternative build-time LLM | custom terms, review before redistribution | alternative only | HLD Licensing |
| **Qwen3-ASR 0.6B**, **Cohere Transcribe** | alternatives to Parakeet for open ASR (benchmark learner-error preservation first) | not stated | alternative only | https://k2-fsa.github.io/sherpa/onnx/qwen3-asr/index.html , https://k2-fsa.github.io/sherpa/onnx/cohere_transcribe/index.html |
| **Whisper / whisper.cpp**, Whisper.net, whisper-rs | extension-only ASR (temperature fallback must be disabled) | MIT | not used | HLD Technology choices |
| **llama.cpp**, LLamaSharp, `llama-cpp-2`, mistral.rs | extension-only local LLM runtime (Phase 3 free conversation, Cache and Advisory only) | not stated | not used | HLD Technology choices |
| **Silero VAD** | D1 upgrade for noisy rooms | not stated | not used; the current VAD is the in-house energy VAD | HLD step 1 |
| **UDPipe** | statistical tagger (D1) to widen linguistic coverage | model licenses to be checked | not used | HLD step 7 |
| **paraphrase-multilingual-MiniLM** | alternative embedding model | not stated | alternative only | HLD Technology choices |
| **NAudio**, `cpal`, `malgo`, engine capture API | microphone capture | not stated | not used: v1 has no OS capture adapter, only `ICaptureSource` and `WavFileCaptureSource` | HLD step 1; plan M2 |
| **CsCheck 4.9.1** | generators for the combinatorial fuzz suite `tests/Vocaterra.Nlp.Fuzzing.Tests` (milestone M16a) | MIT (per the fuzzing plan) | planned; not yet in `Directory.Packages.props` and must pass the license scan | [`docs/nlp/FUZZING_PLAN.md`](FUZZING_PLAN.md) |
| **CodeGuard.RuleFuzzing.Tests** | design reference for the fuzz suite, not a dependency | not stated | reference only | https://github.com/james-d12/CodeGuard/tree/main/tests/CodeGuard.RuleFuzzing.Tests |
| **OpenPronounce**, GOP paper (arXiv 2309.07739) | references for pronunciation scoring | not stated | reference only | https://github.com/Halleck45/OpenPronounce , https://arxiv.org/pdf/2309.07739 |
| **Kokoro-82M with in-house G2P phones** | replace the bundled espeak-ng phonemizer with `Vocaterra.Nlp.Phonology` phones | Apache-2.0 | planned; blocked by the open ADR | `KokoroSynthesizer.cs` `TODO(ADR)` |
| **ONNX export tooling** (for wav2vec2) | produce `model.onnx` from PyTorch weights | not decided | **unverified**: no script or tool is named in the repository | `models/manifest.json` notes |
| **Wiktionary / kaikki.org dump** as G2P golden source | regenerate the G2P golden set | CC BY-SA | not done | plan M3 |

## 9. License summary

**NuGet packages (27, from `scripts/license-scan.sh --offline`, mode offline, `Result: PASSED`):**

| License | Count | Packages |
| --- | --- | --- |
| Apache-2.0 | 19 | `Vosk`; `org.k2fsa.sherpa.onnx` and its 9 runtime packs; 8 xunit packages (`xunit`, `xunit.runner.visualstudio`, `xunit.core`, `xunit.assert`, `xunit.analyzers`, `xunit.extensibility.core`, `xunit.extensibility.execution`, `xunit.abstractions`) |
| MIT | 8 | `Microsoft.ML.OnnxRuntime`, `Microsoft.ML.OnnxRuntime.Managed`, `System.Numerics.Tensors`, `Microsoft.NET.Test.Sdk`, `Microsoft.TestPlatform.TestHost`, `Microsoft.TestPlatform.ObjectModel`, `Microsoft.CodeCoverage`, `coverlet.collector` |
| GPL / LGPL / AGPL / SSPL | 0 | the scan refuses these even if listed in the allowlist ([`scripts/license-allowlist.txt`](../../scripts/license-allowlist.txt)) |

**Models (5):** Apache-2.0 x3 (Vosk small, wav2vec2, Kokoro), CC-BY-4.0 x1 (Parakeet; credit NVIDIA), MIT x1 (multilingual-e5-small).

**Runtime and SDK:** .NET SDK and runtime MIT; system ICU and C++ runtime not shipped (**unverified**).

**Data:** Wiktionary-derived data CC BY-SA 4.0 / 3.0 plus GFDL with share-alike (imported: `content/game-v1/lexicon/wiktionary-es.json`, selected with a CC-BY-SA-4.0 frequency list; the terms of the OpenSubtitles corpus behind that list are unverified); every other committed content file is hand-authored and has no license chosen. The repository itself has **no `LICENSE` file**; picking one is an owner decision (plan section 1).

**GPL components deliberately kept out:**

| Component | License | How it is kept out | Residual exposure found in this inventory |
| --- | --- | --- | --- |
| **espeak-ng** | GPL-3.0 | not a NuGet package; not in the repository; planned only as a dev-time test oracle; Kokoro to be switched to in-house G2P phones | (a) The `linux-x64` sherpa-onnx native library has espeak-ng linked in (verified with `strings`; other platforms unverified). (b) The Kokoro archive contains `espeak-ng-data/`, and `KokoroSynthesizer` requires that directory. (c) The wav2vec2 model's label set is named after espeak, but only a label table is used, not espeak-ng code. Whether (a) and (b) matter for redistribution is an open ADR item, not decided here |
| **Piper** | GPL-3.0 | not used; piper-plus (MIT) noted as an alternative | none found |

## 10. Findings to follow up

1. The HLD says the shipped stack stays permissive if espeak-ng and Piper are kept out of the binary. The Apache-2.0 sherpa-onnx native library already embeds espeak-ng (section 4), so the claim needs an ADR before release.
2. The Vosk package has no `linux-arm64` or `win-arm64` binary, while the parked CI matrix includes `ubuntu-24.04-arm`. Vosk-backed tests are model-gated, but how `Vocaterra.Nlp.Asr` behaves on those platforms is **unverified**.
3. `Microsoft.ML.OnnxRuntime` and every sherpa-onnx runtime pack ship a native file with the same name (`libonnxruntime.so`, `onnxruntime.dll`, `libonnxruntime.dylib`) but different builds (27,026,609 bytes versus 29,345,384 on `linux-x64`). Which copy a built application ends up with was not checked (**unverified**).
4. Restoring `org.k2fsa.sherpa.onnx` also restores the Android runtime packs although no Android target exists, and Vosk's `win-x64` folder carries three MinGW runtime DLLs whose licenses are not covered by the package's SPDX expression.
5. `models/manifest.json` and `models/README.md` describe `multilingual-e5-small` as needing an ONNX export, but upstream already publishes `onnx/model.onnx`; the manifest's provisional file list also names a file (`sentencepiece.bpe.model`) that `OnnxEmbeddingModel` does not read.
