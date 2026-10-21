# Scripts

Helper scripts for the repository. All are bash and can be run from any directory.

## content.sh

Runs the language core's content tool on a game content bundle (default `content/game-v1`):

- `validate` checks every file's hash and the cross-references, and prints the bundle hash.
- `hash` rewrites `manifest.json` from the files on disk, the step after any edit. Options after the bundle directory are passed on, so a
  new bundle id is set with `hash content/game-v1 --bundle-id game-v1`; without the option the manifest keeps its id.
- `cefr-check` lists dialogue-template lemmas missing from a CEFR list (`cefr-check content/game-v1 --level a1`).
- `pack` and `verify-pack` write and check the bundle archive, as the core's packaging document describes.

The tool is `vocaterra-content` (the `Vocaterra.Nlp.ContentBuilder` dotnet tool, version `TOOL_VERSION` in the script). By default it is
installed under `.tools/` (git-ignored) from the core's local feed, `external/Vocaterra.NLP/artifacts/feed`, the first time the script runs.
Run `bash external/Vocaterra.NLP/scripts/pack.sh` first if the feed is missing; the script says so. Set `VOCATERRA_CONTENT_TOOL_DIR` to
install somewhere else (git worktrees can share one install).

To run the core's ContentBuilder from a core checkout instead, for a core commit the feed does not hold yet, set `VOCATERRA_NLP_DIR` to
that checkout; the script then runs `dotnet run --project $VOCATERRA_NLP_DIR/tools/Vocaterra.Nlp.ContentBuilder`.

Needs bash and the .NET SDK (`~/.dotnet` is used when present).

## merge-gate.sh

The orchestrator's gate, run from main: `scripts/merge-gate.sh "<label>:<branch>" ...` merges each branch with `--no-ff` (aborting that
merge on a conflict), then runs the build with warnings as errors, the whole test suite, the format check and `content.sh validate`. Each
check's log is kept under `.claude/gate-logs/`. It prints the counts and `result: PASS` or `result: FAIL`, and exits non-zero on a failure.
