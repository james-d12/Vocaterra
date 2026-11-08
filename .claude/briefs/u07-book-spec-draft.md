# U07 build spec: the book's hierarchy (issue #7)

Source: `mockup.html` beside this file (pixel-exact spreads, ink table with ratios, line-kind table). Plan: `docs/game/implementation-plan.md` 4.10, U07. Kinds and pages in `Game.Core`, drawing in `BookLayer`; no new package.

## Sizes and inks
- Only 16 and 32 px. 32 px: the left page's title (page accent) and the right page's item title and stat numbers (`Ink`). The 19 px row grid stays.
- Inks in `UiPalette` and `TextPairs` (on Paper; on the selection too if the row can be selected): `Ink` 44,32,26 (12.39/11.48); `InkSeen` 78,64,52 (7.81/7.24); **`InkMeta` 96,90,98, new, replaces `InkHint`** (5.25/4.87); `InkUnseen` 108,92,72 (5.04/4.68); `InkKnown` 116,70,6, marks only (6.28/5.82). Accents: the existing `Ribbons` (4.77 to 6.18 on Paper; white on each 6.08 to 7.88). New `BandMuted` 118,104,84 (white 5.41). Marks, 3:1: 44,102,62 / 136,88,20 / 150,46,46.
- Optional, owner to confirm: "strong" weight, text drawn twice 1 px apart, for known entries, the next lesson, labels.

## Line kinds (`BookLineKind` on `BookLine`, set by `BookPages`; the layer picks size, ink and spacing from kind and `BookTone`)
- `Section`: 1 row, band in the page's accent (`BandMuted` for Not met yet), white label, count at right; follows a `Gap` (none at the page top); not selectable.
- `Entry`: 1 row, selectable. Ink by tone: Known `Ink`, Learning and Seen `InkSeen`, Unseen `InkUnseen`, Gold `InkKnown`. Aside and right text `InkMeta` (`InkUnseen` on an Unseen row). Left to right: lead (icon, tick, arrow, mark), text, aside, right text, pips or lamps, speaker or button.
- `Meta`: 1 row, `InkMeta`, not selectable. `Gap`: 1 row.
- New `Tiles`: 1 row of up to 12 grey icons (`Lemmas`), 24 px apart.
- New `Detail`, replacing `Indent`: a line of an entry's block, drawn on the right page only. `ListFor` drops it; `BlockOf` reads it; `BookSpeakers.Selectable` becomes "Entry".
- Geometry: `TitleTop` 36, `RuleTop` 72 (left rule 2 px, accent), `ContentTop` 80 (was 68): 28 rows at 1200 x 720 (was 29), 22 at 800 x 600. "Your book  -" leaves the title.
- Selection keeps its gold and gains a 3 px accent edge.

## Per page
- **Nouns**: Sections Known, Learning (credit or met), Not met yet; empty ones omitted. Entry: icon, word, 3 pips, speaker. Not met: `Tiles` only (no text, pips, `¿?`). Counts, due, forms, last sentence become `Detail`. Right: icon and word at 32 px, pips with "n of 3 correct uses", "What you did" label, four 32 px numbers over captions, due text, "What you said".
- **Verbs**: one Entry per verb (gloss aside, "used n  correct n" right), a `Meta` note. Right (the table): tense header in the accent with a rule, labels `InkSeen`, dashes `InkUnseen`, the legend sentence as three swatches.
- **Phrases**: top Gold Entry "Review now" (button) when due. Sections Without help, With help, Not met yet (by tone). Entry: Spanish, English aside, lamps (met phrases only), speaker. `LevelText` and `CountsText` leave the list; right: lamps named (Card, Audio), a key, five numbers, due text.
- **Lessons**: one Entry per lesson: mark (ticked box done, half box in progress, arrow next, dashed box waiting), `LessonDefinition.ShortOf` title, state aside, `Practise` button as today. Next lesson: accent arrow, Gold, "next: talk to Luna". Waiting: Unseen, "after ..." aside. Units, summary, phrases (with `Speak`), help counts become `Detail`. Right: full title (wraps to two lines), state, summary, units strip (steps only for an unfinished unit), numbers, phrase rows with speakers.
- **What I said**: Section per run of equal `Session`. Entry: outcome mark, quoted text (`Ink`), short place ("talk", "lesson · u3", "a check"). Mark from `InputEntry`: `Communicated`, not `Held`, no errors = understood (tick); with errors = small mistake (!); not `Communicated`, not `Held` = not understood (cross); `Held` or help = dot. Blank text: "(nothing heard)" in meta. Right: text at 32 px, outcome in words, session, source and full place, all feedback uncut.

## Must stay
Tabs, ribbons, page turn, wheel and key scroll, Shift+Up/Down, "Review now", "Practise again", "Ask for a class", every speaker button, 800 x 600. Hit tests count list rows; a Section is one row, so their arithmetic is unchanged.

## Tests
- `BookPages` kinds: sections per page, empty sections omitted, no `Detail` in `ListFor`, `BlockOf` returns an entry's `Detail`, no zero counts in the list, Said's sections and marks.
- `TextPairs`: every new ink, band, mark.
- `BookSpreadTests`, `UiScaleLayoutTests`: new `ContentTop`, 28 and 22 rows, 32 px title fits at every scale.
- Hit tests: list buttons after a Section; Lessons' right-page speakers.
- Re-record the scripted-run pictures that show the book.

## Departures from the plan
1. Section is one row after a Gap, not a two-row heading: row index = line index keeps scroll and hit tests.
2. Phrases grouped by help, not per lesson: `PhraseJournalRow` has no lesson.
3. New kinds `Tiles`, `Detail` (plan: four): icon row for unmet nouns; `Detail` carries "moves to the right page".
4. Meta is a cool grey, not a lighter brown: no lightness is left between meta and unseen under 4.5 on the selection, so hue separates them.
5. Title replaces "Your book - X": costs one row.
6. Lessons' phrases leave the list, speakers on the right page: new hit tests; keys still reach recordings from Phrases. Owner to confirm.
7. Additions: double strike, selection edge, pips key.
