You are implementing the core milestone **M83, the O7 fuzz seed**, in the Vocaterra repository (C#, .NET 10). The language core is the engine `src/Vocaterra.Nlp`, the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects. The game (`Vocaterra.Game*`, `Vocaterra.Host`) is not yours. Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m83`, already on the branch `M83-o7-seed` (from `feature/bread-pilot` at its tip). cd there first; edit nothing elsewhere and never touch another worktree (`adaptive`, `german`, `t9` are not yours). The worktree is new and not built: build once first. Commit with the prefix `M83: ` after each step that builds and passes its tests. **Do not push.** Never push or merge `feature/*` or `main`. Run everything in the foreground and finish in one turn: no background commands, monitors or wait loops. Leave the worktree clean on your branch. Another agent builds T9 at the same time; `scripts/limited.sh` shares the machine, so a heavy command may wait for a slot.

## Resources and environment
Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Run every `dotnet build|test|format|run` and `scripts/content.sh` call through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/test.sh` calls it itself. One heavy command at a time. Exit 137 or 143 means a cap killed the command (likely while the other agent's suite ran): rerun; never raise a cap. The sandbox refuses command lines containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/m83/`. Logs under `/tmp/claude-1000/m83/`; print at most 30 lines; prefer `grep -n` and `sed -n` ranges. Do not copy the repository.

## The problem
`docs/nlp/implementation-plan.md` §5 row M83 and §5b ("An O7 failure found by W02"). Reproduce with `CsCheck_Seed=8eu5g1sTe_A8` on `tests/Vocaterra.Nlp.Fuzzing.Tests` (`--filter IntentResolverFuzzTests`). The case: «ayuda por favor por favor» at a prompt allowing `ask_activity`, `no_such_intent`, `request_item`, `farewell` (not `ask_help`); the analyzer matched `request_item`'s `[ART]? [NUM:qty]? [NOUN:item] [ADJ]? por favor`, which `ask_help`'s `ayuda (por favor)?` outranks, yet the resolution is Grammar `request_item`. The oracle is `LanguageOracles.CheckResolution` (`tests/Vocaterra.Nlp.Fuzzing.Tests/Oracles/LanguageOracles.cs`); its documented rule is in the class comment of `IntentResolverFuzzTests` and in the fuzz README (O7).

## What to do
1. **Find the cause** before changing anything: is the resolver taking a grammar match that an outranking grammar (of an intent the prompt does not allow) beat, contrary to its ladder? Or does `ask_help`'s grammar wrongly fail to match «ayuda por favor por favor» (the repeated `por favor`), so the oracle's "outranks" is computed differently from the analyzer's? Or does `ayuda` read as an item noun when it should not? Say which, with the evidence.
2. **Fix it at the cause**, in the core. **Never weaken the oracle**: if, after study, the oracle is wrong about the ladder, stop and report that with the evidence rather than change it (the owner decides).
3. **A regression test** in the owning module's test project with the seed's input pasted as a `[Fact]` (as the fuzz README's replay note says), plus whatever unit test pins the fixed rule.
4. **Check the effect on real lines:** run the CLI (through a wrapper script, `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "<line>" --prompt <prompt> --content content/game-v1`) on «ayuda, por favor», «ayuda por favor por favor», «quiero agua, por favor», «pan, por favor» at `stall-need` and at `fisher-open`, before and after; report both.
5. Goldens, the identity baseline, replay and stream digests: if one moves, explain it; re-record only if the move is the intended effect of the fix, and list every moved entry.

## Rules
Edit only the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp*`, `tools/Vocaterra.Nlp.*`, `docs/nlp/` except `implementation-plan.md` and `decisions.md`). No `content/` edit and no game project edit. Determinism: nothing in the evaluation path reads the clock or draws random numbers. A regression check is a C# test. Document the fix in the fuzz README's findings, as earlier seeds are.

## Gate (yours, before reporting)
`scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings); `scripts/test.sh full` with the models (**9938 passed and 3 skipped** before you); `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`; `scripts/limited.sh scripts/content.sh validate`. Also run `IntentResolverFuzzTests` with the seed set, and five more times without it. Do not commit `bin/`, `obj/` or `artifacts/`.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (half a page)
The final SHA (local); the cause, with evidence; the fix (files, any public type, and the `docs/nlp/api.md` change if any); the tests; the CLI lines before and after; goldens and digests moved or not; the test count; anything left undone.

Before your report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|limited.sh'`, kill only your own).
