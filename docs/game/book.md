# The book

The learner's book: its pages, replaying words and phrases, the conjugation tables and the polish.

## The book (S20, D-game-16)

The journal is the learner's book: an open book drawn in code over the scene, in the panel style of S15. It opens with **J** (or with the book
item, which the learner starts with at the camp: select it on the hotbar and use it with the right button). Both call the same opening. It covers the
screen and takes the keyboard while it is open. Every title, tab, label and key hint is English; the Spanish is what the learner learnt (D-game-13).

- **Pages.** Five tabs, turned with **Tab** (Shift and Tab turns back), **Left** and **Right**, or a click on a tab: **Nouns**, **Verbs**, **Phrases**,
  **Lessons**, **What I said**. The lists scroll with the wheel, **Page Up** and **Page Down**; **Up** and **Down** move the selected row (see the buttons below),
  or scroll a line on a page with no button; the page's line count is
  shown at the bottom right. The book reads at 1280 x 720.
- **Nouns.** The catalogue words as they were (seen or `¿?`, the icon, heard, read, said, correct and the three pips), then every other noun the learner
  used (from the record's word rows), each with its article from the bundle's morphology (`el agua`, `la botella`), the forms used with their counts, and
  the last sentence (with its English when the sentence is a phrase of the sheet). A fixed expression (`por favor`) and an interjection (`gracias`) are not
  nouns and are not listed.
- **Verbs.** The infinitive with its English from `game/glosses.json` (`querer — to want`), then each form used with a plain-English label built by
  `VerbLabels` (`quiero — I want`, `¿quieres? — do you want?`, `está — he/she/it is`), its counts and its last sentence. A form used in a question is a
  form of its own (the record keeps the question flag). `to be` has its own forms (am, is, are, was, were); an irregular third person or past comes from
  the glosses file; a verb with no gloss shows its infinitive alone.
- **Phrases.** The phrases page of S8, unchanged in meaning: the phrases the learner has met first, each with its Spanish, its English, its help now
  (the card and audio lamps) and its counts.
- **Lessons.** Every lesson of the bundle in its order: its state (completed in session n, in progress with the units done, not started), its units with a
  tick each, the phrases it taught (S17's recap), the help used and, once completed, its English summary, stored in `game/lessons.json` beside the lesson
  (`summary`). Since V12c a lesson not begun says why on its own line under its title: `next: talk to Luna` for the one the objective sends the learner to (the new
  session's, or with none under way the one to go on with; title and line in gold, so the eye finds it first), `open: talk to Luna` for another that is unlocked,
  `opens in session 4, Mar` when its session has not come (said first when it also waits for a lesson), `after “Ayer y mañana”` when the
  lesson before it is not done. The NPC is named as `game/npcs/<id>.json` calls it (its name, else its label), the earlier lesson by its short title. The lines
  are whole on the left page at every text size (at 150 % in an 800 x 600 window the longest, `opens in session 2, Aldeano`, is 169 of 174 px; `UiScaleLayoutTests`).
- **What I said.** `Learner.Recent(500)`, newest first: the line, said or typed, where it was said (the NPC's English name, the lesson and its unit, or a
  check), and the core's feedback in English (`EnglishText.FeedbackFor(InputEntry)`, the same lines as S16's).
- **Speaker buttons (S20b).** A row with a recording has a small speaker at its right edge: every seen catalogue noun on the Nouns page (its word card,
  `card.<noun>`, with the accent folded in the file name: `card.rio`), every phrase on the Phrases page and every phrase a lesson taught on the Lessons
  page. A click plays the normal recording and **Shift**+click the slow one. From the keyboard, **Up** and **Down** move a selection (a gold bar) among
  the rows with a speaker, the selected row stays on screen, **Enter** plays it and **Shift+Enter** plays its slow recording. The first such row is
  selected until the learner moves. An unmet noun (`¿?`) and a noun used outside the catalogue have no button, so nothing is given away; so do the verb
  forms. A phrase the learner only hears has no file of its own: its speaker plays the NPC line that speaks it (`¿Qué necesitas?` is the shopkeeper's
  `shop_need.1`, `Me llamo Luna.` is `guide.lesson.name.1`; the mapping is the phrase sheet's `audioLineIds`).
  The sound goes through the voice (the sfx and ambient ducks under it; `--mute` keeps it silent), and each play that is accepted is a mark in the sound
  log: `000123 mark    speak card.madera slow`. The rules are in `Frontend/BookRecordings.cs` (which recording a row plays and the button's
  geometry), `Frontend/BookSpeechControls.cs` (the click, Enter, the arrows and the scripted step) and `Frontend/BookSpeaker.cs`; tested in `BookSpeechTests`.

**Page models.** `src/Vocaterra.Game.Core/Language/Book/BookPages.cs` (what each page lists, as `BookLine` values: a tone, a text, an aside, a right-hand text,
an icon, pips, lamps or a tick), `VerbLabels.cs` (the English table of a verb form: person, number, tense and mood), `Book.cs` (`BookModel`, the pages in
`BookTabs`, `BookGeometry` for the panel, the tabs and the lines at a size, `BookScroll`). They are engine-free and tested in
`BookPagesTests`, `VerbLabelsTests` and `BookModelTests`; the drawing is `src/Vocaterra.Game/UI/Book/BookLayer.cs`.

**The record and the bundle (what the book reads).** The learner's record gains, per form, the question flag and the last use (`FormRow.Question`,
`FormRow.Last`; a save of version 2 keeps them). Lessons gain `summary` (optional) and the step ids (`LessonRecordRow.StepIds`, from which the book groups
the steps into units by their prefix: `u1-hello` is unit `u1`). `game/glosses.json` is new (the verbs' English, `VerbGlosses`; it is in the manifest and
`provenance.json`). `btnHold` is removed from `english.json` and from `EnglishText.UiKeys`. The book item is `ItemId.Book` (29), given by
`VoxelSession` at a new game; a loaded save puts its own slots back.

**Demo and flags.** The flags open the book for screenshots once the steps are done: `--book` (the nouns page, the same as `--journal`),
`--book-page nouns|verbs|phrases|lessons|said`, and `--journal-phrases` (the phrases page, as before). `--book-play <noun|phrase:id>` (and
`--book-play-slow`) is a scripted step: it opens the book on the target's page, selects its row and plays it as Enter does, so a script can play a
word or a phrase and take a screenshot in the same run. A target with no button yet (a noun not met) says so and plays nothing.

```bash
# S20b: after the S17 lesson route, play a noun from the book (normal) and a phrase (slow), with the sound log (the plays are the "speak" marks)
dotnet run --project src/Vocaterra.Game -- --seed 42 --sound-log /tmp/s20b.log --lesson-run first-words --book-play fruta --shot /tmp/s20b-noun.png --book-play-slow phrase:pedir-agua --shot /tmp/s20b-phrase.png --screenshot /tmp/s20b-final.png --frames 30
# the Nouns and Phrases pages with their buttons (the speaker at each row's right edge; the selected row is gold)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --book-page nouns --screenshot /tmp/s20b-nouns.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --book-page phrases --screenshot /tmp/s20b-phrases.png --frames 30
```

```bash
# the five pages after the S17 lesson route (the lesson is walked first, then the book opens on the page named)
for page in nouns verbs phrases lessons said; do
  dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --book-page $page --screenshot /tmp/s20-$page.png --frames 30
done
```

The S17 route (`--lesson-run first-words` and the steps above it) still completes with `fire_lit` and `session_complete`.

## The book as a book, with conjugation tables (S28, D-game-19)

- **The spread.** The book is an open two-page book drawn in code (no image files, D-game-5): a leather cover with brass corners, the paper with page edges, the spine with
  its stitches, and five ribbon bookmarks along the top edge (Nouns, Verbs, Phrases, Lessons, What I said; the page on screen hangs lower and brighter). A click on a ribbon, Tab,
  Left and Right turn the page; a page turn shows a short leaf hinged at the spine (0.3 s on the frame clock) and S24's `book_page` cue plays as before. The left page lists (the
  rows, the scroll, the speaker buttons of S20b, the "Practise again" buttons of S26, the selected row); the right page shows the selected item. `BookGeometry` (engine free,
  `Frontend/BookGeometry.cs`) gives every rectangle for a window size; the layer, the mouse and the tests read the same numbers.
- **The right page.** Verbs: the verb, its English and its conjugation table, then what you said (each form with its count and its last sentence, as the S20 page listed them).
  Up and Down or a click on a verb choose it. Every other page: the selected item's own lines (a noun's counts and last sentence, a phrase, a lesson with its units and phrases,
  a line you said), written out in full on the page, wrapped instead of cut short. The selected item is the selected row (the rows with a button) or, with none, the first on screen.
- **The table** (`ConjugationTable`, `Frontend/ConjugationTable.cs`). Rows: yo, tú, él/ella/usted, nosotros, ustedes/ellos (no vosotros row). Columns: present, past (preterite),
  future (ir a) and, since V16, the simple future ("future"). A cell is **highlighted** with its count when you have said the form (the learner's record), **plain** when a lesson that is completed or under way has taught it
  (`lessons.json` `verbs`) and you have not said it yet, and a dash when it is neither. A tense column shows once a lesson has taught a form of that tense for the verb or you have
  said one (what you said is never hidden); a verb with neither gets the present alone. Every form is the core's (`ContentBundle.Morphology`, `TaughtVerbForm.Text`); the future (ir a)
  of `comer` is `ir`'s present, `a` and the infinitive (`voy a comer`), counted from the history's words in order (`ir` form, `a`, the verb), over the lines the word record counts.
- **`vamos`.** The core reads `vamos` as an imperative with no tense, so the word record says `vamos (1 pl imp)`. The table fixes that in the game: a form of `ir` whose text is `vamos` counts in `ir`'s present column,
  nosotros, whatever the core made of it (so a bare `vamos` and the one in `vamos a comer` are `ir`, present, nosotros), and `vamos a comer` is also the nosotros cell of the
  future (ir a) of `comer`. The word record itself is unchanged.
- **Flags.** `--book-verb <verb>` opens the book on the Verbs page with that verb's table; `--size <W>x<H>` sets the window (`--size 800x600` for the smallest size the book is made for).
  Everything of S20 to S22 stays: `--book`, `--book-page`, `--book-play`, `--book-practise`, `--greyscale`.

```bash
# the table of ir after the three lessons: the S17 lesson, then each later session on the previous save (a load kept its day from V25 on; a sleep is the next session)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --save /tmp/s28-1.save --screenshot /tmp/s28-1.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --load /tmp/s28-1.save --lesson-run personas --save /tmp/s28-2.save --screenshot /tmp/s28-2.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --load /tmp/s28-2.save --lesson-run ayer-y-manana --save /tmp/s28-3.save --screenshot /tmp/s28-3.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --load /tmp/s28-3.save --book-verb ir --screenshot /tmp/s28-ir.png --frames 30
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --size 800x600 --load /tmp/s28-3.save --book-verb comer --screenshot /tmp/s28-comer-800.png --frames 30
```

## Book polish (X03)

- **The right page scrolls.** The wheel over the right page scrolls that page, and over the list it scrolls the list (three lines a notch, as before). Shift with Up or Down moves the right page a line; Shift with Page Up or Page Down, a page of it. The list keeps Up, Down (the speaker rows), Page Up and Page Down. A "more below" marker shows while writing lies under the page. The right page goes back to its top when the selection or the page changes (`BookModel.DetailOffset`, `DetailScroll`, `BookModel.KeepDetail`). The detail is laid out as rows (`BookLayer.DetailSheet`), so a long block (a lesson with all its units and phrases) is read to its end at 800 x 600. `--book-detail <lines>` scrolls it when the book opens (a scripted run).
- **An imperative that is a present is that present.** The core reads some forms as an imperative with no tense (S27: `vamos`, `está`, `come`). When such a form's text is also the indicative present of the same verb (the bundle's morphology), the labels and the conjugation tables read it as that present: `vamos` is "we go", `está` is "he/she/it is" (estar's 3 sg cell), `come` is "he/she/it eats" (`VerbReading`). A form with no indicative twin (`ven`) keeps its imperative label. `vamos a comer` is not labelled `we are going to …`: the labels are per form, not per sentence, and the future (ir a) column of the table already counts it.
- **ser and estar.** The bundle's glosses (`game/glosses.json`) read `to be: what` (ser) and `to be: where or how` (estar), in the list and on the table's heading. The forms are still the forms of *to be* (`VerbLabels`). The lessons say the same (S26: "Estar is “to be” for where someone is; ser ... is for what someone is").
- **A narrow row keeps its note.** A row with an English aside (a phrase) keeps its Spanish and its English whole; its right-hand note is shortened with "..." to what is left, and is left off only when less than 90 pixels would be left (`MinNoteWidth`).
- **The hint line** now reads `wheel: scroll   Shift+Up/Down: right page`.

```bash
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --size 800x600 --load /tmp/s28-3.save --book-page lessons --book-detail 9 --screenshot /tmp/x03-lessons.png --frames 30
```
