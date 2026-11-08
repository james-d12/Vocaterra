You are fixing the core fuzz finding **M81, the loader accepts the `describe` intent with its `slotGrammars` removed**, in the Vocaterra repository (C#, .NET 10). The language core is the engine `src/Vocaterra.Nlp`, the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects. Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m81`, already on the branch `M81-describe-slotgrammars` (from `feature/adaptive_design`). cd there first; edit nothing elsewhere. Commit with the prefix `M81: ` after each step that builds and passes its tests, and push after every commit (`git push -u origin M81-describe-slotgrammars` the first time). Never push or merge `feature/*` or `main`. Leave the worktree clean on your branch.

## Resources and environment
Prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Run every dotnet build/test/format/run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/test.sh` calls it itself. One heavy command at a time, never in parallel. The sandbox refuses command lines containing the words `eval` or `hash`; write a wrapper script under /tmp/claude-1000/m81/ for such a command.

## The finding
`ContentSchemaFuzzTests.MutatedBundles_AreRejectedWithContentValidationExceptions_NamingTheMutatedFile` (oracle O15) fails with seed `41s2qgWEC-M9` on `feature/adaptive_design` (it reproduces on its own; it is not N01's):

    RemoveProperty file=intents/intents.json path=/intents/27/slotGrammars
    [O15] The loader accepted a RemoveProperty mutation of intents/intents.json /intents/27/slotGrammars, which is not a documented optional field (KnownTolerances).

Replay: `CsCheck_Seed=41s2qgWEC-M9 scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests -m:2 --filter "FullyQualifiedName~ContentSchemaFuzzTests"`. Intent 27 is `describe` (`requiredSlots` empty, no `keywordRules`). Find why the loader takes an intent with neither `slotGrammars` nor `keywordRules` here when it rejects the same mutation of the other intents (look at `IntentCatalog` loading and validation, and at `KnownTolerances.AcceptedRuleOnlyIntent`), and fix the cause at its source: an intent that no grammar and no rule can ever produce is a content error the loader rejects with a `ContentValidationException` naming `intents/intents.json`. If you find instead that the loader is right and the tolerance list is incomplete, say why in the report and in the fuzz README's findings, and make the tolerance exact (one documented case, never a broad one). Never weaken an oracle.

## Rules
Edit only the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp*`, `docs/nlp/` except the implementation plan and decisions). Add a regression [Fact] in the owning module's test project that pastes the failing input, and a findings entry in `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` like the others there. Do not edit `content/`. A regression check is a C# test.

## Gate (yours, before reporting)
`scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings); the seed replay above passes; `scripts/test.sh full` (9158 passed and 3 skipped before you, with models); `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes` clean. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf

## Report (half a page)
Branch and commit SHA; the cause; the fix and its test; the test counts; anything you found but did not fix.
