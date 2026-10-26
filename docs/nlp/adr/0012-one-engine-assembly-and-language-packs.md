# ADR 0012: One engine assembly, and language packs beside it

- Status: accepted
- Date: 2026-10-26
- Deciders: repository owner (2026-10-26: "move all the `src/Vocaterra.Nlp.*` projects into one Nlp project", and the language-agnostic goal;
  research `docs/nlp/research/2026-10-26-engine-and-language-packs.md` section 3); decision D14 in [DECISIONS.md](../DECISIONS.md)
- Supersedes: the layering bullet of [ADR 0003](0003-engine-agnostic-core.md) (the layering stays; the project boundary changes)

## Context

The core was eleven library projects (`Vocaterra.Nlp.Core`, `Audio`, `Asr`, `Phonology`, `Pronunciation`, `Language`, `Evaluation`, `Decision`,
`Response`, `Content`, `Pipeline`) and eleven unit test projects. Nothing is published, and every consumer (the game's core projects, the host, the
three tools) takes all eleven. The project boundary therefore enforced nothing a consumer could choose, and it cost MSBuild nodes, disk and IO on
the build machine.

The owner also wants the core to become language-agnostic, so that a second language can be added beside Spanish. A single project with Spanish
inside it would make that goal harder: the compiler would no longer stop neutral code from calling Spanish code. The boundary that should remain
is the one between the language-neutral engine and each language pack, not between the pipeline stages.

## Decision

1. **One engine assembly.** `src/Vocaterra.Nlp/Vocaterra.Nlp.csproj` replaces the eleven libraries. Each former project is a folder of the same
   short name (`Core/`, `Audio/`, `Asr/`, `Phonology/`, `Pronunciation/`, `Language/`, `Evaluation/`, `Decision/`, `Response/`, `Content/`,
   `Pipeline/`), and every namespace is unchanged (`Vocaterra.Nlp.<Folder>`). The folders were moved with `git mv`, so history follows the files.
   One `Vocaterra.Nlp.AssemblyMarker` replaces the eleven markers.
2. **The layering is enforced by a test, not by the compiler.** `tests/Vocaterra.Nlp.Tests/Architecture/LayeringTests.cs` (NetArchTest.Rules,
   MIT) encodes the project graph of 2026-10-26 as namespace rules over the `Vocaterra.Nlp` assembly:
   - `Core` depends on no other stage;
   - `Audio`, `Phonology`, `Language`, `Evaluation`, `Decision` and `Asr` depend only on `Core`;
   - `Pronunciation` depends on `Core` and `Phonology`;
   - `Response` depends on `Core`, `Asr`, `Language` and `Phonology`;
   - `Content` depends on everything except `Pipeline`;
   - nothing but `Pipeline` depends on `Pipeline`.

   The test also checks that every stage has types (a rule over an empty namespace would pass for nothing), and it has a negative fixture: a
   core-like type that depends on the pipeline must be reported by the same helper. A rule that does not bite fails the build.
3. **The project boundary is engine and pack.** The engine is the language-neutral part. Its Spanish data and rules (today inside
   `Phonology`, `Language` and the bundle's lexicon) are to move out in the later milestones: the pack seams come in M32 to M35 (under
   `src/Vocaterra.Nlp/Language/Packs/`), and the Spanish pack is extracted to `src/Vocaterra.Nlp.Es` (`ILanguagePack`, research section 5.2,
   milestone M53). A German pack, if started, goes to `src/Vocaterra.Nlp.De`. This record does not move any Spanish code; M52 moves none
   between projects either. The engine must not depend on a pack, and `LayeringTests` gains that rule with the first pack.
4. **Build settings follow the name.** `VocaterraCore` in `Directory.Build.props` is true for the engine `Vocaterra.Nlp` and for every project
   named `Vocaterra.Nlp.*` (tests and tools), so the XML documentation, `CS1591`, the package metadata and the native-trim exclusion still apply.
   The engine's `InternalsVisibleTo` is `Vocaterra.Nlp.Tests`.
5. **The tests are merged, the suites that differ in purpose stay apart.** The eleven unit test projects become `tests/Vocaterra.Nlp.Tests/`, one
   folder per area, with each `Data/` folder moved under its area unchanged. The eleven `SmokeTests` collapse to one. The fuzz suite
   (`Vocaterra.Nlp.Fuzzing.Tests`) and the golden suite (`Vocaterra.Nlp.Golden.Tests`) stay separate, because they differ in purpose, in
   `VOCATERRA_RECORD_GOLDEN` and in run time, and they see only the public API.
6. **Tests that depended on assembly isolation are not parallel.** Tests that load a native speech or ONNX runtime, or read the models folder
   through the process-wide gates, run in the `NativeRuntime` collection (`DisableParallelization`). The sherpa-onnx library and the managed
   ONNX Runtime each need their own `libonnxruntime.so`, and both files are named the same; `NativeOnnxRuntimes.cs` loads the sherpa copy first
   and maps the managed package's `onnxruntime` to a copy in `ort-managed/`, so both run in the one test process.

## Consequences

- The compiler no longer enforces the layering. `LayeringTests` does, for the dependency direction. Visibility widens: about 156 `internal`
  declarations are visible across the former stages. That is accepted; the test covers the direction, the rest is review.
- A change inside the engine recompiles the whole engine (about 26,000 lines). That costs seconds and saves the eleven project overheads.
- Consumers reference one project: `src/Vocaterra.Game.Core`, `src/Vocaterra.Host` and the three tools. The game still never copies core source.
- Behaviour does not change. The `content/game-v1` bundle hash, the goldens and the Spanish identity baseline are untouched; the core demo gives
  the same output.
- The package table in [PACKAGE_README.md](../PACKAGE_README.md) now describes one package, `Vocaterra.Nlp`.
- ADR 0003's layering bullet is superseded by this record. Its other points (the engine-agnostic contracts, the interfaces for models and
  capture devices, the thin host) stand.
- ADR 0009's boundary is unchanged: the core is the `Vocaterra.Nlp` engine and its packs, and the game is still the other side of it.
