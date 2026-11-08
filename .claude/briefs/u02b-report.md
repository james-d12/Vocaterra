U02b is finished and committed locally on `U02b-qa-fixes` at **dc72864e** (not pushed). `git status` is clean and none of my processes are running. The first attempt's report is `.claude/briefs/u02b-report-1.md`.

**What I corrected**
- **ui-4 is not a bug.**
  - I restored U02's `DialogueModel.CoachingFor` rule: a turn not `Taken` gets the not-understood feedback, and a typed turn with `OffPromptIntent` that the lesson did not handle gets `ui.coachOffPrompt`.
  - I put the `ui.coachOffPrompt` string back in `content/locale/en/ui.json` and in the required keys of `LocaleText`.
  - U02's test `ATypedLineOffThePromptCoachesWithTheOffPromptNoteButASpokenOneDoesNot` is back with its original meaning.
- **ui-3, corrected.**
  - `LocaleText.FeedbackFor` now gives a not-`Taken` turn `not_understood`.
  - A turn with `OffPromptIntent` that is not communicated and not lesson-handled gets a new line, `feedback.outcome.off_prompt`: "{npc} understood, but that is not what was asked." (I chose a new key over reusing `coachOffPrompt`.) The key is in `OutcomeKeys`, in `content/locale/en/feedback.json`, and the locale manifest is refreshed.
  - A communicated turn keeps `understood` or `understood_with_mistake`.
  - New tests: `TheEnglishOfAnOffPromptTurnNeitherPraisesNorSaysNotUnderstood`, `ATypedTurnNotTakenGetsTheNotUnderstoodCoachingNotTheOffPromptNote`, and the existing understood-line test.
- **A bug the screenshots showed (ui-2 not fully fixed).** At 800x600 and 150 % with `--dev`, the collapsed panel dropped «You: gracias» because the developer note counted as part of the exchange.
  - `PanelFitter` (Core, `PanelFit.cs`) now drops a `Note` block before the learner's line and keeps it only when there is room.
  - `PanelFit` has a new `Skipped` property, `DialoguePanel` skips such blocks, and there is a new test in `PanelFitTests`.

**Behaviour screenshots** (in `/tmp/claude-1000/u02b/shots/`)
- «gracias» alone (`g1.png`): "English: The shopkeeper understood, but that is not what was asked." No "Good" line.
- Expanded with 8 turns (`e8.png`): shows "25 earlier lines above (Up to scroll)".
- 800x600 at 150 % collapsed (`sm-c.png`): after the fix it shows «You: gracias» and the reply.
- 800x600 at 150 % expanded (`sm-e.png`): shows "13 earlier lines above".
- Lesson heading at 800x600 and 150 % (`lh.png`): "Lesson · Welcome: first word... · step 2 of 25 · Listen", so the step and kind stay.
- I could not take a "scrolled to the top" screenshot because there is no scripted scroll step. Scrolling is covered by the unit tests only.

**Gate**
- Build with `-warnaserror`: 0 warnings. `dotnet format --verify-no-changes`, `content.sh validate` and the A2 lint are clean.
- `scripts/test.sh full`: **9586 passed, 0 failed, 3 skipped**, up from 9561 passed before U02b (+25).
- CI case (empty models folder): 9544 passed, 0 failed, 45 skipped.
- **OOM:** two earlier runs of `Vocaterra.Game.Core.Tests` alone hit `System.OutOfMemoryException` (3 and then 1 test). The first overlapped the baseline scripted run; the second was a single test that passed when rerun alone. The final full run was clean, and I did not raise any cap.
- **Scripted runs:** the baseline `base2` completed from the temporary worktree at `d647908a`, and I removed that worktree. I captured the after runs from the final commit and compared with `compare-runs.py`. 26 of 27 runs are the same (some differ in order only). The one difference is `r30-no-classes/declined.png`, and the only change is the dev note's wall-clock figure ("14.6 ms" against "10.8 ms"). I confirmed this by viewing both images, so it is noise and not a behaviour change.

**Left undone**
- Nothing, apart from the scrolled-to-top screenshot above.