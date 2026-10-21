# Noun Town: what the negative Steam reviews complain about

Source: `negative_reviews_for_ai.json`, fetched by `fetch-reviews.py` from the Steam review API
(app 2313720, filter `recent`, `review_type=negative`, all languages). 115 reviews, written between
2023-11-02 and 2026-10-13. 75 were written during Early Access, 40 after the 1.0 release.
Median playtime at review time is 0.9 hours; about a quarter of reviewers played more than 5 hours.
Review languages: English 71, Simplified Chinese 24, German 5, French 4, Japanese 3, Traditional
Chinese 2, Spanish 2, one each Turkish, Latin American Spanish, Brazilian Portuguese, Korean.

Theme counts below come from a keyword pass over the review text and are approximate. A review often
hits several themes.

| Theme | Reviews (of 115) |
|---|---|
| Speech recognition and microphone problems | ~63 |
| Errors in the language content itself | ~48 |
| Bugs, crashes, freezes, softlocks | ~37 |
| Controls, camera, UI, tutorial | ~33 |
| Learning loop design (word cap, SRS, feedback) | ~25 |
| No grammar, script or fundamentals | ~25 |
| Vocabulary selection (random, useless, loanwords) | ~21 |
| Developer follow-through, spin-offs, abandonment | ~17 |
| Online-only speech, server and region issues | ~11 |

Almost every review, including the harshest, opens by praising the concept ("great idea, poor
execution" is the single most common shape). The complaints are about delivery, not premise.

---

## 1. Speech recognition is the number one reason for refunds

This is the most frequent complaint by a wide margin and spans the entire three-year window, so the
problem was never resolved. Specific failure modes reviewers describe:

- **Does not hear the player at all.** Microphones that work in Discord, OBS and every other game
  are not picked up. Several reviewers tried multiple mics and the developer's guides before
  refunding. Some note it worked in the tutorial and failed in the main game.
- **Inconsistent verdicts on identical input.** The same word, said the same way, is accepted once
  and rejected the next six times. Native speakers report being marked wrong in their own language
  while deliberately mangled attempts in the target language pass.
- **False positives.** Saying "I don't know this word", gibberish or the wrong word on purpose is
  marked correct. One reviewer passed a sentence prompt before finishing speaking. Another found that
  pressing "listen" then "speak" lets the playback pass the test.
- **Latency and hangs.** Ten-plus seconds of waiting after speaking is common. The recognition state
  cannot be cancelled, so a hung request means waiting for a timeout or killing the process. Repeated
  use of the speak key eventually stops responding altogether.
- **Online dependency.** Recognition runs on a remote server. Players with working internet get "no
  connection" errors; a Chinese reviewer could only get it working through a VPN set to Germany, and
  only on the first day. One reviewer argues the game should not be sold in regions where the service
  is unreachable.
- **Language-specific weakness.** Chinese recognition is called "atrocious" and ignores tones.
  Russian is reported as far stricter than German. Japanese is the one language where several
  reviewers say it works "about 75%" of the time.
- **Round-trip transcription errors.** A Japanese reviewer deduces that speech is converted to kanji
  and then back to a reading, so saying "to" for 東 is re-read as "higashi" and marked wrong. Full
  sentence prompts are reported as impossible to pass even when the on-screen transcript is correct.
- **No pronunciation feedback.** When the game says "wrong", it never says what was wrong or how to
  fix it. Several reviewers ask for phonetic hints or at least a display of what the game heard versus
  what it wanted. One reviewer with a speech impediment refunded because the system has no tolerance.
- **No escape hatch.** Review quizzes have no "I don't know" button, so forgetting a word means
  saying nonsense twice and losing two lives. Some lessons that claim to be skippable are not.

Several longer reviews conclude that beginners should turn voice mode off entirely, because being
told you are wrong when you are right "will hurt their learning in the long run".

## 2. The language content contains errors a learner cannot detect

Second most common, and arguably more damaging to trust: reviewers who already know the target
language catch mistakes within minutes and then conclude that everything in languages they do not
know is suspect.

- **Japanese readings.** Wrong furigana is the most-cited single error class: 二人 given as "ninin"
  instead of "futari", 月 in a date read as "tsuki" instead of "gatsu", 他に read as "ta" instead of
  "hoka", on'yomi and kun'yomi mixed, は and わ used interchangeably, "ring" voiced as "yubiwa" but
  requiring the typed answer "yubitamaki". Furigana is also simply missing in many places, and item
  previews show kanji with no reading at all. One reviewer concludes the readings were generated
  automatically.
- **Machine-translated text.** Chinese-interface players report UI strings that look machine
  translated ("exit" rendered as the noun for a doorway, "speech" rendered as "public speech"),
  unreadable sentence translations, and tutorial text that "does not serve as teaching". A French
  reviewer cites "Ich bin Krankenpfleger" translated with the feminine noun, and "allemande" for the
  language name. German-to-English conversation translations are also called wrong.
- **Audio that does not match the text.** Recorded words cut off before the final syllable (Italian,
  Russian "тостер" played as "тостр", Russian "сок" as "ok"). A number robot plays "eleven" but
  requires "seventeen". Characters say "asu" while the text says "ashita". The expected answer audio
  is sometimes repeated four times in a row, making the voice test unpassable.
- **Non-native or unclear voices.** English words are a single male voice, some mumbled, with
  uneven volume. Japanese dialogue is said to be voiced by non-natives with thick accents; French
  characters sound "like an abridged series". Some reviewers suspect TTS for vocabulary.
- **Errors in the lesson notes.** "Your very welcome" in a lesson; notes that say something is a
  contraction without saying of what; grammar tips that reference concepts never introduced.
- **Scripts and directions.** Arabic answer options are laid out left to right. There is no
  Traditional Chinese option. Chinese learners say the word selection ignores the structured
  progression the language needs.

Multiple reviewers state these bugs have been reported on Discord and the forums for months or years
and remain unfixed.

## 3. Bugs, crashes and softlocks

- Freezes on launch, random freezes mid-session, and crashes when raising the resolution (one
  reviewer could not run 3440x1440).
- A kiosk save step that freezes and loses the words learned since the last save; one reviewer
  describes needing "2 to 3 restarts every other session".
- Softlocks in minigames and in review, often attributed to the speech server hanging, with no way
  to back out.
- Review and quiz corruption: random letter strings as answers ("ASUEUWQE"), the same wrong options
  offered repeatedly, a direction word appearing as the correct answer for every item after playing the
  delivery minigame.
- Multi-monitor and window bugs: the game relaunches on the secondary display in portrait orientation
  and the settings panel ends up off screen. Kiosks also open off screen.
- Mac is called out as untested. Steam Deck lost D-pad menu navigation between demo and Early Access.
- Progression breakers: collecting robot parts before instructed causes a softlock; interacting with
  objects during delivery breaks later exercises until restart; noun boxes counted as remaining when
  none are visible, blocking 100%.
- Opening more than the daily five nouns is reported to disable the review test for them.
- A localisation file missing on launch ("localisation text not found"); the game starting in Korean
  with no way to pick an interface language; a "Link account" website flow that does not work.
- No cloud saves, so progress differs per device.

## 4. Controls, camera, interaction and UI

- **Fixed isometric camera.** Objects dropped behind furniture take minutes to recover. Depth is
  misjudged when placing items, so an object meant for a table lands in the middle of the room.
  Several reviewers say the game is a VR port and the mechanics only make sense in first person.
- **Imprecise object interaction.** Hover targeting selects the wrong item or one across the room;
  items are tiny and hard to identify (napkin, beef, beer versus wine, CD versus folder, juice versus
  drink). Picking up resets rotation. Objects embed in the floor. Fish cannot go in the water.
- **Awkward hybrid control scheme.** Click plus WASD plus targeted interact "feels bad on every
  device". No key rebinding. The delivery vehicle spins out on the slightest touch; throwing trash at
  robots has no aiming.
- **Menus and UI.** Menus are slow, fragmented and inconsistent: Escape works in some and not others,
  a pop-up covers the close button, item spawning needs separate screens, long sentences are truncated.
  Mic settings reset on every area change.
- **Tutorial.** Does not explain the mechanics; players must find the handbook. Some cannot get past
  the tutorial without auto-pass. Daily goals are confusing (chatting does not always count, "5 nouns"
  is not what it sounds like).
- **Audio design.** No footstep or button sound effects; repetitive music, with "intense" music in the
  memory game that reviewers find deliberately distracting.

## 5. The learning loop is poorly designed

Reviewers with language-learning experience critique the pedagogy, not just the bugs.

- **Five-word daily cap** per room feels like an ad break in a paid product. Progress is gated by a
  slow kiosk save that must be triggered manually.
- **No placement test or test-out.** An A2 or near-fluent learner must relearn "plate" as if new.
- **Random mystery boxes** decide what you learn next; there is no choice of word order or category.
- **The SRS cannot be failed.** Forgotten words fall through to a four-option multiple choice that is
  easy to recognise, so the word is counted as known and buried. Answers in multiple-choice tests are in
  the player's own language, making them guessable without hearing the audio.
- **Sentence prompts before the words.** Lessons ask players to recite a full sentence from memory
  having never taught its individual words. NPCs speak with advanced grammar on random topics
  unrelated to the room. Translation is for the whole sentence only; no per-word gloss or hover
  dictionary.
- **Slow throughput.** One reviewer measured about 10 words per half hour versus near 100 per hour in
  a dedicated vocabulary app.
- **Written form not reinforced.** The spelled word is shown in some modes and not others; no option
  to see romanised and native script together; for Japanese the typed-answer mode asks for hiragana
  after showing only kanji.
- **Minigames.** The memory rush gets harder as you learn more words, so the high score becomes
  unbeatable; the colour game is daily RNG; after finishing all rooms, around 100 words can only be
  learned through the "horrible" overworld minigames, and the streak can no longer be maintained.

## 6. No grammar, no script, no fundamentals

A recurring question is "who is this for". Beginners get no alphabet (Cyrillic, Hangul, kana are
never introduced), no grammar, no tones and no sentence construction, so the NPC dialogue is
incomprehensible. Non-beginners already know the nouns on offer. Specific asks: a grammar reference
on the pause screen (one German learner uses the dative as the example), plural forms alongside
singulars for German, tone marks and a Chinese-appropriate progression. Several reviewers say they
learned more from Duolingo, Anki, Drops, The Sims or Animal Crossing.

## 7. Vocabulary selection

- Dominated by animals and kitchen utensils; "unless you are a marine biologist and a chef" the
  words are not used in daily life.
- For Japanese, 22 of the first 25 nouns one reviewer opened were katakana loanwords (hot dog,
  knife, belt), which a learner does not need to pay to practise. Another finished every room in about
  10 days because the words were so easy.
- Single "definitive" word per object when several are in common use; no regional variation for
  Spanish and no indication of which dialect is taught. "Cuántos años tienes" is marked wrong where
  "Qué edad tienes" is required.
- About 550 words for some languages, which reviewers consider too few for the price.
- Portuguese is an interface language but not a learnable one, which misled at least one buyer.
- Words should be tiered by frequency, and boxes colour-coded by usefulness.

## 8. Developer follow-through

Longer-tenured players (20 to 57 hours) are the most negative about the studio itself: bugs reported
for months are never fixed, the Early Access label was dropped with no fixes, and the team released a
VR version and an idler game on the same content instead of correcting it. One reviewer calls it
"ADHD development": many features started, none finished. The Discord community manager is
responsive but reports go nowhere. The separate full-price VR edition is seen as greedy. A Korean
reviewer tracked monthly updates stopping for over six months.

## 9. Smaller points

- Price considered too high for the content, especially with no sale planned.
- The US flag represents English.
- Concern that voice data is collected.
- Resolution cannot be changed in some cases.
- The companion website's dictionary links are broken, yet its free revision and lesson tools are
  judged better than the in-game ones.

---

## What the pattern says

The failure is not the premise. Reviewers consistently like: learning nouns from objects in a space,
native-voiced NPCs where present, the art style, the price relative to subscriptions, and the breadth
of languages. What loses them is, in order of frequency and severity:

1. A speech pipeline that is slow, online-only, unreliable in both directions, uncancellable, and
   silent about what went wrong.
2. Language data that was never checked by a native speaker, so expert learners catch errors fast and
   beginners absorb them without knowing.
3. Stability and input problems that make short daily sessions a chore.
4. A learning loop with no placement, no way to fail a word honestly, random word order, and an
   arbitrary daily cap.
5. No path from nouns to sentences: dialogue arrives before grammar or the words it uses.
