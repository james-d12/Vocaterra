# I04c report (the doll's headings as bare nouns)

The agent's report, saved by the orchestrator (2026-11-18). Sonnet 5.5 at medium effort; brief `i04c-bare-headings.md`. Gated at
`b048d781`: 13,312 passed, 0 failed, 9 skipped; timing 23; format clean.

Branch `I04c-bare-headings`, one commit b8bebfdb. `HeadingText.Pick(noun, width, measure)` returns the bare noun, or a three-letter mark
when it does not fit (compact and tight); `InventoryLayout.HeadingBox` overhangs at the regular size (the first column over the row
labels' width, free in the heading row; the last into the gap before the bag, 4 px short of it) and `HeadingTextLeft` keeps the text inside
its box; `InventoryLayer` passes the bare lemma. Regular layouts (down to about 950x700) show «ropa», «armadura», «apariencia» in full in the
16 px font; compact and tight unchanged («rop», «arm», «apa», the word on hover); the hover keeps the article and the English; title, item
names and slot labels keep their articles. Tests: a real-font test over every regular case (30, 40, 50 slots, 5 needs: the full noun, width
inside its box, no overlap with each other, the labels, the bag or the buttons); the fourth-group test checks the bag too; `HeadingTextTests`
two tests changed from the article to the bare noun and the mark. No content change. The orchestrator's 1280x720 shot confirms it.
