# Noun Town: what the positive Steam reviews say

Source: `positive_reviews_for_ai.json`, fetched by `fetch-reviews.py` from the Steam review API
(app 2313720, filter `recent`, `review_type=positive`, all languages). 648 reviews, written between
2023-11-02 and 2026-10-04. 563 were written during Early Access, 85 after the 1.0 release.
Median playtime at review time is 1.5 hours; 110 reviewers played more than 5 hours and 19 more than
20 hours. Review languages: English 486, Simplified Chinese 59, Brazilian Portuguese 26, German 18,
Spanish 12, Korean 10, Latin American Spanish 6, French 6, Japanese 5, Russian 5, Polish 4, and a
handful each of Traditional Chinese, Italian, Norwegian, Dutch, Danish, Turkish.

About 150 reviews are eight words or fewer ("good", "cute and fun", "banger"). The substance comes
from the remaining 500 or so. Theme counts below are from a keyword pass over the review text and are
approximate; a review often hits several themes.

| What reviewers praise | Reviews (of 648) |
|---|---|
| Fun, cute, cozy, relaxing | ~230 |
| It works for vocabulary / nouns | ~290 |
| Speaking and pronunciation practice | ~155 |
| NPC conversations and roleplay lessons | ~100 |
| SRS, revision, Learning Rush minigame | ~100 |
| Better than Duolingo, flashcards or Anki | ~80 |
| Colouring the grey world as you progress | ~50 |
| Responsive developers on Discord | ~40 |
| One-time price versus subscriptions | ~30 |

| What reviewers still complain about, inside a thumbs-up | Reviews (of 648) |
|---|---|
| Speech recognition or microphone problems | ~120 |
| Not for beginners: no grammar, alphabet, tones | ~110 |
| Bugs, freezes, jank | ~75 |
| Word cap, pacing, SRS design | ~70 |
| Word selection (random, rare, loanwords) | ~65 |
| Errors in the language content | ~60 |
| Controls, placement, delivery robot, menus | ~60 |
| Save files, cloud sync, lost progress | ~10 |

The ratio matters: 648 positive to 115 negative. Many positives read like a negative review with a
different thumb, so the recurring line is "I would recommend it as a supplement" (about 50 reviews
say so explicitly) and "you need some prior knowledge first" (about 60).

---

## 1. Why people like it

### The core loop is fun and it does not feel like studying

The single most common sentiment. Reviewers describe the game as cozy, relaxing, cute, charming,
"like lofi while studying", "hardly feels like learning at all in the absolute best way". Several say
they get disappointed when they hit the daily limit because they want to keep going. Multiple
reviewers with ADHD say this is the first language tool they have stuck with. A Japanese teacher and
a Norwegian teacher say they use it with students; an ESL teacher wants to.

### Nouns in a physical space stick

Reviewers repeatedly say the words stay with them: "101 words in 4 days, remember 80%+", "41 words in
two days", "learned more Japanese than in a year of self study", "still remember kitchen appliances
from the VR version a year later". The mechanism they credit is seeing the 3D object, hearing it,
saying it, and remembering *where* it was. Several call it a memory palace or "organising your Anki
deck in 3D". Splitting vocabulary by location (cafe, farm, bakery, office, zoo) is praised as a
natural chunking that makes recall easier. Learning the article with the noun (der/die/das, el/la,
le/la) is called out as a real advantage over other tools, especially for German.

### It fills a gap other tools leave

The most frequent comparison is Duolingo, almost always in Noun Town's favour: no owl harassment, no
ads, no paywall, no AI voices, actually teaches concrete everyday objects that courses skip. Duolingo
streak holders (645 days, 3 years) say they learned more nouns here in a week than in years there.
Others compare favourably to Anki, Rosetta Stone, Pimsleur, Drops, flashcards and textbooks. The
one-time price is contrasted with $100 to $200 per year subscriptions; "worth every penny" and "buy
it on sale" are both common.

### Speaking out loud

Even reviewers who complain about recognition say the act of saying every word aloud is what makes it
work. "Forces me to confront how terrible my pronunciation is and I need that." Many prefer the mic to
the typing fallback and tell others to use a proper headset. Repeating a word until the recogniser
accepts it is described by a few as accidentally helpful for retention. Reviewers appreciate multiple
recorded voices at different speeds and that NPCs are real human voices.

### Progression you can see

Restoring colour to a grey town is cited as a strong, legible reward loop. Completionists like the
robot parts for numbers, colour seeds, and 100%-ing rooms. Steam achievements were requested and
later welcomed.

### Conversations and lessons

Roleplay lessons (ordering a drink, asking about a business) and talking to NPCs are liked as the one
place the game goes beyond nouns. Reviewers enjoy the characters having quirks and relationships,
and the translate button. Later reviews note the lessons now include grammar tips and that review
tests cover sentences used with NPCs, which is praised as a nice touch.

### Developers who respond

Especially in 2023 and 2024, reviewers praise fast Discord responses, bugs fixed "within hours of
reading my review", a VR buyer swapped to PC for free, and frequent patches. Several reviews were
updated from negative to positive after a fix (speech server errors, mic detection, settings not
saving, furigana added, no-mic mode added).

### Steam Deck and VR

The desktop version is praised as the cure for VR motion sickness and as "perfect for the Steam
Deck so I can practice in bed", though Deck UI handling is also criticised.

## 2. What positive reviewers still complain about

### Speech recognition, again

The most common caveat in a positive review, present from the first week of Early Access to the
latest reviews. The same failure modes as in the negative set: mic not detected until settings are
toggled on every room change, inconsistent verdicts on identical input, short words and single
syllables failing (Korean 잔, 꿀; Chinese single characters; Greek "to poto"; "taco" in any language),
recordings cut off before the last word, slow turnaround, false positives on wrong answers, phrases
accepted before the speaker finishes. Chinese tone detection is called inadequate by multiple heritage
and advanced speakers. Chinese players report needing a VPN or the Chinese UI for recognition to work
at all. Several reviewers simply switch to the typing fallback and say the game is fine without voice.

Requests that recur: a cancel button, a longer record window, a mic level meter and test, playback of
your own attempt next to the reference, a persistent view of "what it heard vs what it wanted" (the
bubble vanishes too fast), a slow-speech button, and local offline recognition instead of a server.

### Not a tool for absolute beginners

A very consistent framing: great if you are A1 to B1, confusing if you know nothing. The game does
not teach alphabets (hiragana, katakana, hangul, Cyrillic, Arabic script), grammar, tones, verbs,
conjugation or sentence construction. NPCs speak in full native-speed sentences from the first room.
Many reviewers tell newcomers to learn the script elsewhere first and to use Noun Town alongside a
textbook, Language Transfer, Assimil, Duolingo or Anki. "Verb Town" and "Grammar Village" are asked
for half-jokingly a dozen times.

### Word choice

Positive reviewers echo the negatives: too many Japanese katakana loanwords (one count: half of all
nouns), "chimpanzee, cash register, jar before train station, police, hotel", "pork as the second
Arabic word in a cafe", "black pepper before food", "river dolphin" but no "fridge" or "sink". Rare
animals dominate. Reviewers ask for frequency ordering, tiers, or letting the learner pick boxes, and
for more categories (body parts, furniture, jobs, countries, car parts). Around 500 nouns per language
is widely called too few; several would buy a 1000-word pack as DLC.

### Content errors

Fewer and milder than in the negative set but the same classes: wrong furigana on special readings
(二人, 月 in dates, 20日), mismatched text and audio ("yubitamaki" vs "yubiwa"), cut-off Italian
numbers ("undi" for "undici"), missing Greek accent marks and articles, "Unterhemd" for shirt, a slow
cooker labelled "la cuisinière", 饺子 as "boulettes" with a bao model, "我想下雪" for "I want snow",
summarised rather than literal sentence translations, misspellings in Spanish lessons, Arabic
rendered left to right with disconnected letters, question marks on the wrong side. Chinese UI
translation is repeatedly called machine-made ("exit" as a doorway, "creative mode" as "function
constructor mode"). A Ukrainian dictionary shows the Japanese word for apple where entries are missing.
Several reviewers note quality varies sharply by language: Italian and Chinese fully voice-acted,
Ukrainian partly text-to-speech.

### Bugs and jank

Freezes at the kiosk, items falling through the floor, cannot drop items until restart, delivery robot
stuck in geometry, UI duplicating, pathfinding catching on corners, controller cursor slow and
unselectable, Escape inconsistent, a lesson console you cannot leave, flowers and fabricated items
vanishing between sessions, streak counter stuck, farm too crowded to decorate. Most reviewers say
re-entering the room fixes it and forgive it as Early Access. Three reviewers lost all progress with no
recovery.

### The learning loop

- **Five-word cap.** Understood as pedagogy and defended by some, but many want to set their own
  limit and find the kiosk-save-then-reload step tedious. "Duolingo trying to stretch the basics
  across four years."
- **No "I don't know".** Forgetting a word means failing twice or guessing; multiple choice then makes
  the word count as known. Reviewers want a give-up button and harder drilling of failed words.
- **No placement.** Intermediate learners want to test out of known words or get hints about box
  contents.
- **Review variety.** Only Learning Rush and SRS revision; reviewers want longer or endless rush
  mode, multi-room rush, custom decks, and typing as a third input mode to learn spelling.
- **Per-word translation.** Whole-sentence translation only; many want to click a word in NPC
  dialogue for its meaning, or a literal word-order gloss ("Birthday have you when?").
- **Romanisation toggle.** Korean and Japanese learners want to turn romaji off; Chinese learners
  want pinyin shown earlier.

### Platform and saves

One save slot, so households and multi-language learners must use separate Steam accounts. No Steam
Cloud. Requests for mobile and console versions because "after 8 hours at my desk I don't want to sit
there more". The VR edition is a separate purchase with separate progress, which some find unfair.

### Development slowing

Reviews from late 2025 onward note "funding has run out", no new language in a year, no update since
January 2025, and hope the game is not dead. This is the only theme that trends worse over time.

## 3. Who the reviewers are

- **Target languages mentioned:** Japanese ~70, Chinese ~45, Spanish ~35, French ~30, German and
  Korean ~25 each, Italian ~12, Russian, Arabic, Greek, Ukrainian a few each, and about 13 learning
  English from another language.
- **Level:** overwhelmingly self-described as past the basics and using the game to widen
  vocabulary. A smaller group are true beginners who are enjoying it but say they are lost in
  dialogue.
- **Attention:** many cite ADHD, boredom with flashcards, or inability to sit with a textbook as the
  reason this works for them.
- **Session shape:** 10 to 30 minutes a day, often as a morning routine or wind-down, driven by the
  daily tasks and streak.

---

## What the pattern says

Positive reviewers agree with negative reviewers about nearly every flaw. They forgive them because
the core loop delivers something no other tool does: concrete vocabulary learned by seeing, hearing
and saying objects in a place, with articles attached, in a session short enough to repeat daily.
What the positive set adds that the negative set does not:

1. **Retention is the proof point.** Reviewers measure success by words that stuck, and they report
   high retention with low effort. That is the feature to protect.
2. **The audience is intermediates.** The game is bought by people who already have the basics and
   are starved of noun vocabulary. Beginners are the ones who bounce.
3. **Speaking aloud is valued even when the recogniser is bad.** The act matters more than the
   verdict, which argues for honest, fast, cancellable feedback over strict gating.
4. **Articles and location chunking are quietly the best teaching decisions.** They are mentioned
   unprompted and never criticised.
5. **Developer visibility buys patience.** Early reviews forgive bugs because fixes arrive within
   days; late reviews stop forgiving when updates stop.
6. **The asks are consistent and specific:** cancel and retry on voice, show what was heard, a give-up
   button that drills the word, frequency-ordered words, per-word glosses, typing as a third input,
   script and grammar primers, multiple saves, and a local recogniser.
