# Spanish Learning Game

A Terraria-style exploration and progression game in which learners practise Spanish by speaking to NPCs; this repository holds the deterministic voice recognition, decision, and feedback system written in C# on .NET.

## Prerequisites

- .NET SDK 10.0.402 (pinned in `global.json`; `rollForward` allows later patch releases)

## Build and test

```bash
dotnet restore
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

## Documentation

- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md): milestones, repository layout, determinism rules
- [HIGH_LEVEL_DOCS.md](HIGH_LEVEL_DOCS.md): high-level design
