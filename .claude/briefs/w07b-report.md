# W07b report (Haiku 5.5, 2026-11-06)

Branch `W07b-station-points` (local), final commit a4840942: 16f98802 title, 3b337089 help and the third slip select the highlight,
c3ae4d01 the one-click bake offer, a4840942 running.md. Title: `ui.station.<id>` in the locale, else the catalogue noun with its article,
else the id (StationTitleTests, 4). Help and the third slip set the held thing and the target to the highlighted ones (StationPanelTests).
Offer: `StationView.Offer` (`BakeOffer`) after mastery, not while ReadyToCollect; guided first and selected when the goal row is due
(`PhraseProgress.IsScaffoldDue`, since `DuePhrases` leaves out instruction rows); B bakes at once, Enter when selected, clicks; missing
ingredients give `StationNotice.NotEnough`; `ui.stationBakeAtOnce`, `ui.stationGuided`, `ui.stationNotEnough` (StationBakeOfferTests, 7;
layout tests on six canvases with the offer). Scripted runs: 25 of 26 same; r30-no-classes declined.png differs (known, T8). Screenshot not
made: the throwaway bundle copy failed the content checks (the test rows' ids not in the lexicon). The agent skipped the full suite, build,
format and validate on the orchestrator's instruction; the gate ran them (9758 passed).
