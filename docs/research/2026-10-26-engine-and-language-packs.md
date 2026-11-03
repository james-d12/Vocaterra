# One language-neutral engine and language packs: review and plan

Review and plan, 2026-10-26, written on `feature/organise` (after `83e3011`). It answers the owner's request: "Move all the
`src/Vocaterra.Nlp.*` projects into one Nlp project", and adds: "i want the NLP to become language agnostic kind of, so we have the current
spanish implementation on top of the core shared NLP code. So adding languages can be done." No code was changed. The owner chose the
target shape and the scope (section 3). Nothing has been implemented yet.

**How to take over.** Act as the orchestrator (`.claude/agents/orchestrator.md`, or `claude --agent orchestrator`). The milestones are
M31, then M52, then M32 to M35, then M53, in that order (section 4). They are rows in `docs/nlp/implementation-plan.md` (§5 for M52 and
M53, §11 for M31 to M35), and D14 in `docs/nlp/decisions.md` carries the amendment. Each milestone is a branch `M<nn>-<name>` off `main`,
goes through `scripts/merge-gate.sh` and is pushed to that branch, never to `main` (the owner merges by pull request). Agents run one at
a time, and every heavy command goes through `scripts/limited.sh`. Start only after the owner has merged `feature/organise`, which
touches `src/Vocaterra.Game.Core`.

## 1. The short answer

Merging the 11 library projects makes sense. Nothing is published, and every consumer already takes all 11. But **one** `Vocaterra.Nlp`
with Spanish inside it would work against the language goal: the compiler could no longer stop neutral code from calling Spanish code.
So the one project boundary that remains should sit between the engine and each language, not between pipeline stages:

```
src/
  Vocaterra.Nlp/            language-neutral engine (today's 11 projects as folders, namespaces unchanged)
    Core/ Audio/ Asr/ Phonology/ Pronunciation/ Language/ Evaluation/ Decision/ Response/ Content/ Pipeline/
  Vocaterra.Nlp.Es/         Spanish pack (ILanguagePack), from M53
  Vocaterra.Nlp.De/         German pack, when M36 onward runs
tests/
  Vocaterra.Nlp.Tests/      engine unit tests (today's 11 unit test projects as folders), with an architecture test
  Vocaterra.Nlp.Es.Tests/   from M53
  Vocaterra.Nlp.Fuzzing.Tests/
  Vocaterra.Nlp.Golden.Tests/
```

## 2. Review

### 2.1 For the merge (from the repository as it stands)

- **Nothing is published.** `Directory.Build.props` says so, and `nuget.config` lists nuget.org only. The 11-package table in
  `docs/nlp/package-readme.md` describes packages that do not exist.
- **No consumer takes a subset.**
  - `Vocaterra.Nlp.Content` and `Vocaterra.Nlp.Pipeline` each reference all the other libraries.
  - `Vocaterra.Game.Core` and `Vocaterra.Host` list 7 projects and get all 11 transitively.
  - The CLI, Bench and ContentBuilder reference `Pipeline`, and so get all 11 too.
  - The native packages (Vosk, sherpa-onnx, ONNX Runtime, through `Asr`, `Language`, `Pronunciation` and `Response`) reach every
    consumer either way.
- **The split costs this machine.** There are 11 libraries and 13 test projects. The G7 native trim in `Directory.Build.props` excludes
  core projects, so every core test project's `bin/` gets every platform's natives. Fewer projects means fewer MSBuild nodes and far
  less disk and IO.
- **The move is mechanical.**
  - Under root namespace `Vocaterra.Nlp`, the folders `Core/`, `Language/` and so on map onto today's namespaces, so no `using` line
    changes.
  - The three embedded resources set an explicit `LogicalName`.
  - The goldens hold no assembly-qualified type names.
  - The only fully qualified name that appears in two places, `Vocaterra.Nlp.Pipeline.UtterancePipeline`, is one `partial` class.
  - There are no `[assembly:]` attributes in the core's source.

### 2.2 What it costs, and the mitigations

| Cost | Mitigation |
| --- | --- |
| The compiler no longer enforces ADR 0003's layering | An architecture test (`LayeringTests`, NetArchTest.Rules, MIT, already on `scripts/license-allowlist.txt`) encodes today's project graph as namespace rules (section 5.1) |
| `internal` widens: about 156 internal declarations become visible across stages | The architecture test covers the dependency direction; the rest is accepted |
| Tests that relied on assembly isolation (environment variables such as `VOCATERRA_MODELS_DIR`, static caches) can race in one test assembly | Put them in a non-parallel xUnit collection; the merged suite must pass three runs in a row |
| An incremental build recompiles the whole engine (about 26,000 lines) | Seconds; outweighed by the project overhead removed |
| A single assembly would let Spanish leak into neutral code | Spanish leaves into `Vocaterra.Nlp.Es` (M53), behind the D14 seams |

Research `2026-10-22-multilingual.md` §4.7 advised extracting Spanish into its own project "only if a third language arrives". The
owner's goal of a neutral core supersedes that advice: the assembly boundary is the cheapest proof that the engine is neutral.

### 2.3 Found along the way

- `Vocaterra.Nlp.slnf` does not exist, but `scripts/license-scan.sh`, `scripts/README.md`, `scripts/merge-gate.sh`'s header and
  CLAUDE.md all rely on it, so the licence scan cannot run as written. M52 recreates the file.
- `content/v1` was deleted (R02, D7 superseded). The multilingual research and §11 of the plan still speak of "an unchanged `content/v1`
  hash" and draw M31's corpus partly from `content/v1`. Read those as `content/game-v1` throughout.
- ADR numbers 0010 (D9) and 0011 (M32) are already reserved, so this restructure's ADR is 0012.

## 3. Owner decisions (2026-10-26)

- **Target shape:** one language-neutral engine `Vocaterra.Nlp` plus one project per language (`Vocaterra.Nlp.Es` first). The
  alternatives were literally one project, with Spanish kept apart only by folders and tests, and keeping the 11 stage projects.
- **Scope:** the merge now, then D14 Phase A (M31 to M35, until now "record only"), ending with the Spanish pack extracted (M53). M36
  onward (German) stays recorded only.

## 4. Milestone order

| Order | ID | Milestone | Tier | Size | Depends on |
| --- | --- | --- | --- | --- | --- |
| 1 | M31 | Spanish identity guard (as planned in research §4.9). It comes first so its baseline, recorded on the pre-merge commit, also guards the merge | H | S | M29j |
| 2 | M52 | Merge the core libraries into `Vocaterra.Nlp` (section 5.1) | H | M | M31 |
| 3 | M32 to M35 | D14 Phase A as planned, on the merged layout: the interfaces live in `src/Vocaterra.Nlp/Language/Packs/` (namespace `Vocaterra.Nlp.Language.Packs`) | as planned | | M52 |
| 4 | M53 | Extract the Spanish pack into `Vocaterra.Nlp.Es` (section 5.2) | S | M | M35 |

M32 to M34 already require "no `Spanish*` call left outside the Spanish pack and its compatibility entry points" as a gate grep. That
rule is what makes M53 a move rather than a redesign. German (M36 onward), when the owner starts it, goes into `src/Vocaterra.Nlp.De`
and `tests/Vocaterra.Nlp.De.Tests`, not into §4.7's `Vocaterra.Nlp.Language.De`.

## 5. Milestone specifications

### 5.1 M52: merge the core libraries (no behaviour change)

**Source.**
- `src/Vocaterra.Nlp/Vocaterra.Nlp.csproj` replaces `src/Vocaterra.Nlp.{Core,Audio,Asr,Phonology,Pronunciation,Language,Evaluation,
  Decision,Response,Content,Pipeline}`. Each becomes a folder of the same short name, moved with `git mv` so history follows the files.
  Source namespaces do not change.
- The csproj holds the union of the package references (`Vosk`, `org.k2fsa.sherpa.onnx`, `Microsoft.ML.OnnxRuntime`) and the three
  `EmbeddedResource` items with their `LogicalName`s unchanged:
  - `Language/`: `../../content/game-v1/lexicon/seed.json`, the same relative depth;
  - `Phonology/Data/exceptions.json`;
  - `Pronunciation/Data/espeak-to-vocaterra.json`.
- One `Vocaterra.Nlp.AssemblyMarker` replaces the 11 per-project markers. Update `docs/nlp/contributing.md` line 80, which asks to
  keep them.

**Build settings.** In `Directory.Build.props`, `VocaterraCore` becomes
`$(MSBuildProjectName.Equals('Vocaterra.Nlp')) Or $(MSBuildProjectName.StartsWith('Vocaterra.Nlp.'))`, written in whatever form MSBuild
accepts. Today's `StartsWith('Vocaterra.Nlp.')` misses the new name, which would drop XML docs, `CS1591`, the package metadata and
`InternalsVisibleTo`. The rule `InternalsVisibleTo $(MSBuildProjectName).Tests` then gives `Vocaterra.Nlp.Tests` access with no further
change. Update the file's comments.

**Tests.**
- The 11 per-library unit test projects merge into `tests/Vocaterra.Nlp.Tests/`, with one folder per area. Each `Data/` folder moves
  under its area, and the tests' relative paths are fixed. The `tests/Shared/*.cs` links are included once.
- The 11 `SmokeTests` collapse to one.
- `Vocaterra.Nlp.Fuzzing.Tests` and `Vocaterra.Nlp.Golden.Tests` stay separate projects, because they differ in purpose, in
  `VOCATERRA_RECORD_GOLDEN` and in run time. Their project references shrink to `Vocaterra.Nlp` plus the tools they use. They keep seeing
  only the public API, as today.
- New `tests/Vocaterra.Nlp.Tests/Architecture/LayeringTests.cs` (add NetArchTest.Rules to `Directory.Packages.props`). The rules are
  today's project graph, read from the csproj files on 2026-10-26:
  - `Vocaterra.Nlp.Core` depends on no other `Vocaterra.Nlp.*` namespace;
  - `Audio`, `Phonology`, `Language`, `Evaluation`, `Decision` and `Asr` depend only on `Core`;
  - `Pronunciation` depends on `Core` and `Phonology`;
  - `Response` depends on `Core`, `Asr`, `Language` and `Phonology`;
  - `Content` depends on everything except `Pipeline`;
  - nothing in the engine depends on `Pipeline`.
  A negative test proves the check bites: a deliberate violation, kept in the test project, must fail it.

**Consumers.**
- `src/Vocaterra.Game.Core`, `src/Vocaterra.Host` and `tools/Vocaterra.Nlp.{Cli,Bench,ContentBuilder}` reference the one project. Grep
  the rest of the repository for other consumers.
- `tools/Vocaterra.Nlp.ContentBuilder`: `InternalsVisibleTo Vocaterra.Nlp.Content.Tests` becomes `Vocaterra.Nlp.Tests`.
- `Vocaterra.sln`: remove the 22 entries, add the 2 new ones.
- Create `Vocaterra.Nlp.slnf` (the engine, its three test projects and the three tools), and run `scripts/license-scan.sh` against it.

**Docs.**
- ADR `docs/nlp/adr/0012-one-engine-assembly-and-language-packs.md`, which supersedes ADR 0003's layering bullet: the layering stays,
  enforced by the test, and the project boundary becomes engine/pack.
- ADR 0009's table and `package-readme.md` (one package).
- Project paths in `docs/nlp/README.md`, `api.md`, `contributing.md`, `tools.md`, `replay.md`, `lexicon.md`, `scripts/README.md`,
  `docs/game/architecture.md` and CLAUDE.md's `.slnf` line.
- Leave `docs/archive/nlp/history/` and older research as they are.

**Gate, beyond §6.**
- The build passes with `-warnaserror`, and the whole suite passes, the game's tests included.
- `dotnet format --verify-no-changes` passes.
- The `content/game-v1` hash is unchanged, no golden is re-recorded, and `git diff main -- 'tests/*/Data' golden content` is empty apart
  from the `Data/` moves.
- The M31 identity guard is green.
- The core demo `eval` command gives output identical to `main`.
- The merged test project passes three runs in a row.
- `LayeringTests` fails when a temporary `Core → Pipeline` reference is added.

### 5.2 M53: extract the Spanish pack (after M35)

**Projects.** Add `src/Vocaterra.Nlp.Es` (references `Vocaterra.Nlp`) and `tests/Vocaterra.Nlp.Es.Tests`. The namespace is
`Vocaterra.Nlp.Es.*`.

**What moves into the pack.** `SpanishLanguagePack`, and everything M32 to M34 routed behind it:
- text: the Spanish normalizer pieces, `SpanishNumbers`, `SpanishPhoneticKey` and the accent folding;
- morphology and analysis: `MorphologyGenerator`, and the Spanish analysis rules and tables;
- output text: `PunctuationFixer`'s `¿ ¡` balancing, the repair lines and the grammar defaults;
- the Spanish error-tag set;
- `SpanishG2p` with `exceptions.json`;
- the seed-lexicon resource.
Whether the espeak phone map is Spanish or neutral is decided during the milestone and written in the ADR.

**Registry.**
- The engine cannot reference a pack, so the built-in `LanguagePacks.Default` holding `es` (research §4.1) cannot stay in the engine.
  Hosts register packs through `ContentLoadOptions.LanguagePacks`, for example `LanguagePacks.With(SpanishLanguagePack.Instance)`.
- Callers to update:
  - `Game.Core` and `Host`: one line each, plus a project reference to `Vocaterra.Nlp.Es`;
  - the CLI, Bench and ContentBuilder;
  - the Fuzzing and Golden test projects.
- Record the change in `docs/nlp/api.md` as host-facing.

**Guards and tests.**
- `LayeringTests` gains a rule: no `Vocaterra.Nlp` namespace depends on `Vocaterra.Nlp.Es`. The assembly boundary enforces this too.
- M35's toy "xx" pack runs against the engine alone, with no reference to `Vocaterra.Nlp.Es`, which proves the engine is neutral.
- The Spanish tests move to `Vocaterra.Nlp.Es.Tests`: most of today's Language and Phonology tests, and the G2P golden. Fuzz keeps
  M35's `FuzzLanguage` harness.

**Gate, beyond §6.**
- The identity guard is green and byte-identical.
- The `content/game-v1` hash is unchanged, and the goldens are unchanged.
- The fuzz suite passes at the default depth, and once at 10,000 grammar iterations.
- The game's tests are green.
- `git grep -n 'Spanish' src/Vocaterra.Nlp` finds only comments and the documented compatibility entry points.

## 6. Verification, per milestone

- `GATE_TARGET=<branch> scripts/merge-gate.sh "<label>:<branch>"` runs build, test, format, fuzz and the licence scan. Its logs go to
  `.claude/gate-logs/`.
- `SpanishIdentityTests` is green.
- `scripts/content.sh validate` and the core demo (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos
  espada" --prompt villager-open --content content/game-v1`) give output identical to `main`.

## 7. Progress and handover

Moved on 2026-11-03 to [the handover of 2026-10-26](../handovers/done/2026-10-26-engine-and-language-packs.md).
