U02 report. Branch `U02-conversation-surface`, final commit `5ccba6d2` (local, not pushed, worktree clean).

**Built**
1. **Current exchange and older lines** (`Game.Core` `Dialogue/DialogueModel.cs`, `PanelRow.cs`). `PanelRows()` shows the learner's last line, the NPC lines after it (English as before) and the coaching line. `Expanded`/`ToggleExpanded` and `HiddenLineCount` handle the rest. `Lines`, and so the book's "What I said" history, are unchanged. Before the learner speaks, every line shows. The panel adds a `N earlier lines hidden (PageUp or O)` row.
2. **One coaching line** (`DialogueModel.CoachingLine`, the new `DialogueSpeaker.Coaching`). Cases in order: uncertain spoken and not understood (from `LocaleText.FeedbackFor`), off prompt (typed only, not for a lesson-handled turn; new string `ui.coachOffPrompt`), and a slip (`turn.Feedback`, else the first error line). It replaces the correction, accent and English stack in the panel only.
3. **Expand key.** Tab stays Escribir (D-game-9), so I chose **PageUp** (works in every state, including the text box) and **O** (off the text box). `DialogueScreen.cs`.
4. **Lesson heading in the panel header** (`FrameView.LessonHeading`, `DialoguePanel.DrawLessonHeading`, new `TextWrap.Ellipsize`). The strip has no heading or border (`LessonStripLayout.HeadingRow = 0`) and stands flush on the panel. The HUD line during a `do` step is as U01 left it.
5. A new scripted step, `--expand` (`ScriptStepKind.Expand`, with a small `LaunchOptions` edit), so the expanded screenshots can be taken.
6. Docs: `running.md`, `architecture.md`, `scripted-runs.md`. Locale strings are in `content/locale/en/ui.json`, with the manifest refreshed.

**Tests** (+45)
- `tests/Vocaterra.Game.Core.Tests/Dialogue/ConversationSurfaceTests.cs`:
  - coaching, one test per case;
  - collapsed and expanded rows, history unchanged;
  - heard line after every spoken turn, gone at the next typed turn;
  - R, S, H, N, W, Tab each by key in idle, after a turn, thinking and after the timeout offers typing;
  - choice keys, Enter on Escribir, Esc cancel, and the expand key in each state.
- `tests/Vocaterra.Game.Tests/UI/Dialogue/ConversationSurfaceLayoutTests.cs`: heading fits at 1280x720 and 800x600 at 100, 125 and 150 %, and the strip stands flush on the panel.
- I changed three existing assertions from `panel.Y - Gap` to `panel.Y`, because the strip is now flush (`UiScaleLayoutTests` and `LessonFrontendTests`). No D-game-9 test changed.
- The mic check route relies on the existing `MicCheckScreenTests`.

**Values chosen**
- Expand key PageUp/O.
- Current exchange starts at the learner's last line.
- Heading room: 110 px kept free for the status.
- Plain "understood" outcomes show no coaching line; the NPC's reply is the outcome.

**Design and code disagreed**
- The heading reads in English from the locale (`Lesson · First words · step 1 of 7 · Say it`), not the Spanish example.
- The strip's step text and buttons (K, Enter, L) are not in the panel, so they remain a borderless block flush above it.

**Behaviour checks.** I looked at the screenshots:
- the villager at day 2 after three turns, collapsed and expanded, at 1280x720;
- the collapsed panel at 800x600 with text scale 150;
- the lesson offer and step 2 with the header strip;
- r17: after the spoken line «quiero agua», `Heard … Expected …` shows.

I did not look at the 800x600 expanded shot.

**Scripted runs.**
- Baseline built from the pre-change commit (a first baseline was invalid; redone).
- Stdout is unchanged in all 26 runs.
- `compare-runs.py` finds only r30 different (`offer.png`, `declined.png`), as intended.
- Pictures of the tick-moved runs it does not byte-compare (r17 and others) differ by the same changes.
- Documented in `scripted-runs.md`.

**Gate**
- Build with `-warnaserror`: 0 warnings.
- `scripts/test.sh full`: **9521 passed, 3 skipped** (was 9476).
- Without models: 9479 passed, 45 skipped, 0 failed. I did not measure wall-clock tests against their budgets.
- `dotnet format --verify-no-changes`: clean.
- `content.sh validate`: passes.
- Lint at A2: clean.
- No `content/game-v1` edit, so no grammar run.
- Save versions: no save change; the existing save fixtures pass.

**Undone**
- None known. No process of mine is still running.