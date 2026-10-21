# Scripts

Repository-level helper scripts. All are bash and can be run from any directory. The language core's own scripts (pack, licence scan,
model and lexicon fetching, the sample host) are in `nlp/scripts/` and documented there.

## content.sh

Runs the language core's content tool on a game content bundle (default `game/content/game-v1`):

- `validate` checks every file's hash and the cross-references, and prints the bundle hash.
- `hash` rewrites `manifest.json` from the files on disk, the step after any edit. Options after the bundle directory are passed on, so a
  new bundle id is set with `hash game/content/game-v1 --bundle-id game-v1`; without the option the manifest keeps its id.
- `cefr-check` lists dialogue-template and prompt-text lemmas missing from a CEFR list (`cefr-check game/content/game-v1 --level a1`).
- `lint`, `pack` and `verify-pack` are passed through as `nlp/TOOLS.md` and `nlp/docs/PACKAGING.md` describe.

The tool is the `Vocaterra.Nlp.ContentBuilder` project under `nlp/tools`, run from source with `dotnet run`, so it is always the core at
the current commit. Needs bash and the .NET SDK (`~/.dotnet` is used when present).

## merge-gate.sh

The orchestrator's gate, run from main: `scripts/merge-gate.sh "<label>:<branch>" ...` merges each branch with `--no-ff` (aborting that
merge on a conflict), then runs on `Vocaterra.sln` the build with warnings as errors, the whole test suite (core and game; model-backed
tests when `VOCATERRA_MODELS_DIR` is set or the shared models folder exists), the format check, `content.sh validate` and the core's
`nlp/scripts/license-scan.sh --offline`. Each check's log is kept under `.claude/gate-logs/`. It prints the counts and `result: PASS` or
`result: FAIL`, and exits non-zero on a failure.
