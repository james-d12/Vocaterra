# Notes moved from the core repository

Moved from `Vocaterra.NLP` (the language core) in milestone M19e, on 2026-10-21. The core documents the language core only, so the
game-side text below was removed from it. Each item gives the file it came from and the text as it stood there. Items already covered by
[SPECIFICATION.md](SPECIFICATION.md) or [ARCHITECTURE.md](ARCHITECTURE.md) are not repeated here.

## HIGH_LEVEL_DOCS.md (the core's high-level design)

- **Overview, purpose line:** "**Purpose:** core gameplay loop for a Terraria-inspired game where speaking Spanish is a mechanic (spells, trade, quests, agent dialogue)."
  Replaced in the core by a generic purpose line. The game's Terraria-like framing is in SPECIFICATION.md; the list of mechanics is not.
- **Overview:** "and reacts in-world." and "feedback and game outcome." (now "reacts." and "feedback and host outcome.").
- **Determinism model, hard rule 2:** "(dialogue variant choice, loot rolls)" (now "(dialogue variant choice and any other host draw)").
- **Diagram, Effects node:** "E3[Effects<br/>spell power, trade price]" (now "host-defined outcome kinds").
- **Components, step 8 (intent):** "slot grammar match in current agent/quest context" (now "current agent context").
- **Components, step 10 (agent logic):** "never changes agent state, prices, quests or rewards." (now "never changes agent state or host state.").
- **Components, step 15 (effects):** "(spell at 60% power, reduced trade price)" (now "(for example 60% of the full effect)").
- **Technology choices:** "and the Terraria lineage (XNA → MonoGame)" (the language recommendation now reads without it).
- **Technology choices, table row:** "| Game engine | — | Godot .NET, MonoGame | Bevy, macroquad | Ebitengine |". The engine decision is D12, open in the core's DECISIONS.md.
- **Open questions:** "- [ ] Final language and engine: C# + Godot/MonoGame, or Rust + Bevy?" (the language is decided in the core's ADR 0001; the engine is open).
- **Open questions:** "- [ ] Default dialect for agent speech, and is it player-selectable?" (now a decided default, ADR 0005, and "Whether a host lets its learner choose another is the host's decision.").
- **Open questions:** "- [ ] Does free conversation mode (Phase 3, Cache + Advisory) ship at all?" (a product decision for the game).
- **Roadmap, text:** "The first playable target is Phase 1:" (now "The first target a host can use is Phase 1:").
- **Roadmap, Phase 0 node:** "language, engine pick" (now "language (ADR 0001)").
- **Roadmap and patterns:** the name "free conversation mode" was the game's mode; the core now calls it "optional conversation mode" wherever it appears.

## TOOLS.md (the core's inventory)

- **Section 8 contents row:** "espeak-ng, Qwen3, Godot, CsCheck and other future items" (Godot removed).
- **Section 8 table row:** "| **Godot .NET**, **MonoGame** (C#); Bevy, macroquad (Rust); Ebitengine (Go) | game engine | not stated | engine choice deferred; the core is engine-agnostic class libraries | HLD Technology choices; plan section 1 |"

## README.md (the core's front page)

- **Opening quote:** "> The game built on this core is [Vocaterra](https://github.com/james-d12/Vocaterra). Its design (`SPECIFICATION.md`, moved there in M19c) and its architecture notes live in that repository." (now one link and "This repository documents the language core only.").
- **Architecture introduction:** "The host that uses the core lives in the separate `Vocaterra` repository; nothing game-specific (scenes, items, prices, rewards) is in this one." (now "Hosts live in their own repositories; nothing host-specific ... is in this one.").
- **Using the packages:** "consumes them the way a game would" (now "the way a host application would").
- **Repository layout, last row:** "The game design (`SPECIFICATION.md`) is in the Vocaterra repository since M19c".
- **Known gaps, engine:** "The engine is not chosen" (now "The engine is the host's own choice").
- **Known gaps, open decisions:** "- **Open decisions.** The license of the repository, whether the player can choose the agent dialect, and whether a free conversation mode (Phase 3 in the design) ships at all." (now the open items D2 and D4 in the core's DECISIONS.md).
- **Documentation list:** "- [Vocaterra](https://github.com/james-d12/Vocaterra), the game repository: its docs/SPECIFICATION.md ... and docs/ARCHITECTURE.md ..." (the two links now sit in SPECIFICATION.md and ARCHITECTURE.md here).

## docs/PACKAGE_README.md (the readme shipped inside the packages)

- "Vocaterra.NLP is the language core of a Spanish learning game" (now "of a Spanish learning application").
- "The core knows nothing about the host's game" (now "the host application").

## docs/PACKAGING.md (the packaging notes)

- "The game repository's `scripts/content.sh` is to call this command instead of `dotnet run` on the project." (now "A consuming repository's content script calls this command ...").
- "`samples/HostConsole` is a console host written against the packages, the way a game would consume them." (now "the way a host application would").
- "The boundary in ADR 0009 is exercised first by the game repository (M19c)." (now "by a consuming host, the Vocaterra game (M19c)").

## docs/LEXICON.md

- "| `game.json` | hand-authored: the words the host itself uses (spells, items) |" (now "the sample bundle's own words (its spells and items)"). The file name and its content are the core's sample and are unchanged.
