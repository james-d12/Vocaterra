# Scripts

Helper scripts for the repository. All are bash and can be run from any directory.

## content.sh

Runs the core's ContentBuilder on a game content bundle (default `content/game-v1`). `validate` checks every file's hash and the cross-references
and prints the bundle hash; `hash` rewrites `manifest.json` from the files on disk, which is the step after any edit. Options after the bundle
directory are passed to the ContentBuilder, so a new bundle id is set with `hash content/game-v1 --bundle-id game-v1`; without the option the
manifest keeps its id.

The ContentBuilder is the `tools/Vocaterra.Nlp.ContentBuilder` project of the core checkout, found through `VOCATERRA_NLP_DIR` (default
`../SpanishLearningGame`, beside this repository). Packaging the ContentBuilder as a dotnet tool, so that this script does not need the core
checkout, is a follow-up in the core. Needs bash and the .NET SDK.
