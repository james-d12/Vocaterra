You are implementing the core milestone **M82, the loader refuses an intent nothing can produce**, in the Vocaterra repository (C#, .NET 10). The language core is the engine `src/Vocaterra.Nlp`, the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects. Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m82`, already on the branch `M82-bare-intent` (from `feature/adaptive_design`). cd there first; edit nothing elsewhere. Commit with the prefix `M82: ` after each step that builds and passes its tests; push after every commit (`git push -u origin M82-bare-intent` the first time). Never push or merge `feature/*` or `main`. Leave the worktree clean on your branch.

## Resources and environment
Prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Run every dotnet build/test/format/run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/test.sh` calls it itself. One heavy command at a time. The sandbox refuses command lines containing the words `eval` or `hash`; use a wrapper script under /tmp/claude-1000/m82/ for those.

## The owner's decision (2026-11-05)
M81 found that an intent with no `slotGrammars`, no `keywordRules` and no `examples` loads (`IntentCatalogTests.OptionalMembersMayBeOmitted` pins it), though no grammar, rule or embedding can ever produce it. No bundle has one (`content/game-v1/intents/intents.json` is the only catalog; its 33 intents all have at least one). The owner: enforce it.

## The work
1. `IntentCatalog` loading (`src/Vocaterra.Nlp/Language/Intent/IntentCatalog.cs`, where it builds an intent): an intent with none of the three (each absent or empty) is a `ContentValidationException` naming `intents/intents.json` and the intent id, with a message that says what to add.
2. `OptionalMembersMayBeOmitted`: keep what it pins (each member alone may be omitted) by giving its intent one of the three; add a test that the bare intent is refused, and one per member that an intent with only that member loads.
3. The fuzz suite: `KnownTolerances.AcceptedGrammarlessIntent` (M81) still holds (removing `slotGrammars` from an intent that keeps rules or examples is accepted); a mutation that leaves an intent bare must now be rejected and name the file. Check that O15 passes at its full depth, and update the fuzz README's M81 entry ("Known, deliberately not fixed" moves to fixed by M82).
4. `docs/nlp/content-format.md` or wherever the intent catalog's members are documented (`grep -rln 'keywordRules' docs/nlp`): say an intent needs at least one of the three. `docs/nlp/api.md` only if a public type changes.

## Rules
Edit only the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp*`, `docs/nlp/` except the implementation plan and decisions). No `content/` edit. Never weaken a fuzz oracle. A regression check is a C# test.

## Gate (yours, before reporting)
`scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings); `scripts/test.sh full` (9358 passed and 3 skipped before you, with models); `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes` clean; `scripts/limited.sh scripts/content.sh validate` passes. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf

## Report (half a page)
Branch and commit SHA; the check and its message; the tests; the counts; anything found and not fixed.
