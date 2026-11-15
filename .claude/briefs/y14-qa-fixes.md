You are implementing Y14, "QA fixes for the settlement slice", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host; the language core is taken as it is. Four small
fixes from the QA report `docs/qa-reports/2026-11-12-progression.md` (read it first: each bug has its reproduction, evidence and the
likely file).

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y14, on the branch `Y14-qa-fixes` (cut from `feature/settlement` after Y13).
Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y14 &&`; absolute paths for file tools. Commit after each fix
with the prefix `Y14: `. Do not push; never push or merge `feature/settlement` or `main`. Leave the worktree on its branch, clean, nothing
running. No other agent builds while you work.

Resources and environment as in CLAUDE.md: every heavy command through scripts/limited.sh (builds `-m:4`, tests `-m:2`; scripts/test.sh and
scripts/content-loop.sh call it themselves), never two at once, no `pgrep` wait loops; prefix PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; a wrapper script for any command
line with `eval` or `hash`; `VOCATERRA_RUNS_WORK` a folder unique to Y14.

Rules: edit only the game projects, their tests, content/game-v1 and content/locale if needed, `scripts/scripted-runs.sh` and docs/game/
(not the implementation plans, decisions or settlement-plan.md). Tests are C# Given_When_Then in folders mirroring the source; engine code
only in src/Vocaterra.Game; no clock or random in the evaluation path; the A2 content gate stays clean.

The fixes:
1. **QA-…-progression-1, the fail-forward's phrase.** `GuideSession.cs` (about line 1425) credits `PhraseIds.RequestWater`
   (`pedir-agua`) for every `turn.FailForward`, whatever the NPC. Credit the phrase the learner was asked for at that prompt (the NPC's
   `focus` for the prompt the turn was at, its first phrase, or the phrase the help line taught: find what the data offers and choose;
   say why), or nothing when the prompt has none. The stall keeps `pedir-agua` where that is its prompt's phrase. Check the transfers the
   report names (`phrase pedir-agua transfer at baker (pan)`, `at villager (terreno)`): if the same constant is behind them, apply the same
   rule and say so. Tests: «no sé» at the inn credits `quiero-una-habitacion` (or what you chose) and not `pedir-agua`; the stall
   unchanged.
2. **…-2, the wake card under the sleep prompt.** After a death beside the inn's bed (or at the camp) the bed's prompt "E: sleep until
   morning" is drawn over the wake card's second line, and "Your land" under it. While the wake card is up, hide the sleep prompt (and the
   plot's HUD line), or move it so nothing overlaps (choose; say why). A test in the game's layout tests if they exist for these layers;
   screenshots before and after into `artifacts/y14/` (git-ignored).
3. **…-4, «sí» that starts a class shown as "unknown, not understood".** After the purchase, the mayor's class offer follows (Y11's
   `ReleaseClass`); the player's «sí» starts the class (`lesson personas started`) while the turn's verdict line reads `unknown, not
   understood`. Find why the turn is graded at a prompt that does not take `confirm` while the class offer takes it (the prompt the turn
   is evaluated at should be the class offer's), and fix it so the verdict agrees with what happens. Test through talk turns: after a
   purchase, «sí» to the class offer reads `confirm`, understood, and starts the class.
4. **…-5, r30's baker.** `scripts/scripted-runs.sh`' `r30-no-classes` never takes Luz's oven order since issue #54 made it a task the
   player accepts: add the «sí» at her offer (and whatever delivery the row expects) so the run does what `docs/game/scripted-runs.md`'s
   row says, or correct the row if the run's purpose is otherwise; capture r30 before and after and report the difference.

Not in this milestone: the scripted walker giving up at a tree (QA-…-3, recorded in the open items), the report's observations.

Gate (from the worktree's root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full (report
the count; 11,457 passed and 3 skipped after Y13); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes; scripts/limited.sh
scripts/content.sh validate; the A2 content gate (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level
--host-pools 'guide.*'`); re-run `r40-inn`, `r41-land`, `r42-camp`, `r43-stake-refused` and `r30-no-classes` and compare with captures
taken before your change (report each difference). If the goldens move (a phrase credit is in a golden), record them and say why.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>

Report, half a page: branch, SHA, worktree; each fix (cause, change, test); the runs' comparison; test counts; anything not fixed and why.
