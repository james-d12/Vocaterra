# L01 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `L01-lessons-as-classes`, built at `1534cb17` (pushed, clean), then synced with N05 by a Haiku agent (brief `l01-sync.md`).
Before the sync: 9403 passed, 3 skipped (9385 before); CI's case 9361 passed, 45 skipped; format, validate, A2 lint clean; grammar 0 new
findings; the identity baseline re-recorded (the bundle hash moved). The core untouched.

- **Introduction and first class:** `first-words` keeps its id with u1, u2, u3, u5, u6 (25 steps) and alone keeps `autoStart`; u4 and u4v
  are a new lesson `first-class` (12 steps, lines unchanged, `after: first-words`). Old saves split on load (`LessonRunner.SplitIntroduction`).
- **Classes:** every other lesson `autoStart: false`. `LessonRunner.Classes.cs`: the teacher's one-line offer (five pools
  `guide.lesson.<npc>.offer`, with WAVs); «Sí» starts it; «No»/«No, gracias» cost nothing (the NPC's reply suppressed, its trade prompt
  kept, no second offer that day); any other line withdraws the offer. «¿Me enseñas?» and the Lessons page's "Ask for a class" both work;
  the core has no intent for «¿Me enseñas?» (unresolved in the CLI): a core gap; the game reads its plain forms from `classAnswers` in
  `game/words.json`.
- **Esc** pauses a class at a talk step and frees its NPC; at a `do` step the class stays engaged (the panel is closed for the walk).
- **Unlock:** met its teacher plus its `after` lesson, never the day; saved list `metNpcs` (no format bump; an old save counts every NPC as
  met from day 2).
- **Sleep:** from day 2 the fire offers sleep when the sleep drive is below 75 (`SleepWanted`); otherwise E shows «No tengo sueño.» as a
  toast with the English beside it; no lesson holds sleep; `--sleep` and the scripted sleep step skip the drive check.
- **Orders** show once their NPC is met; `HoldsOrder` is gone; the oven order no longer waits for `n4-back` (Luz pays at the first talk; a
  late class finds the order done). **Cards** from rung 1. **Beacon** follows the tracked objective. **Lessons page:** each class with
  `next:`, `open: ask X` or `meet X first`, and "Ask for a class". `--skip-to-session n` marks the classes before n done and their teachers met.
- Tests: `ClassTests.cs` (18), a panel-pump test, `NextSessionTests` rewritten, about 40 updated; removed: the hold-the-order and day-gating
  tests and two tests of the session-2 and session-3 objective lines. Docs: `lessons.md`, `running.md`, `saves.md`, `scripted-runs.md`.
- Values chosen: the villager's offer «¿Una clase hoy? ¿Sí o no?» («conmigo» is not A2), the others «¿Quieres una clase? ¿Sí o no?»; one
  offer per NPC per day, the earliest open class first; «No tengo sueño.» is a pool line (`guide.sleep.not_sleepy.1`) and got a WAV.
- Design against code: the design's "Domingo" (the smith) teaches nothing, so the villager was used; an order's `session` field only
  places `--skip-to-session`'s delivery; the unreachable «Primero, la lección» order-hold lines and host events stay in the content.
- Behaviour: day 2 at the villager the offer; «No, gracias.» no reply, the villager's talk goes on; day 3 «Sí» → `lesson personas started`.
  The fire: sleep 60 shows the prompt, 90 does not (the «No tengo sueño.» line itself only unit-tested). New run `r30-no-classes`: days 1
  to 3, no class, the oven order delivered at Luz's first talk.
- Scripted runs (14 of 26 compared; r02, r07, r08, r10, r11, r12, r14, r15, r16, r17, r19, r25 not compared): speech 639 to 645 lines;
  saves about 208 bytes longer; 11 lessons listed; r20 and r21 re-baselined (no `beacon_set` on the villager or Mar, the new Lessons
  page); r04 to r06 lose the 12 steps of Luna's first class; r22 to r24 start no lesson at the baker, so «quiero dos panes» is "supported"
  where it was "unaided"; r26 sets the beacon to "your things" after death.
- Saves: an engaged class loads paused; the old `first-words` splits (mid-lesson and done); v1 and v4 by the fixtures, v2, v3, v5, v6 not
  separately.
