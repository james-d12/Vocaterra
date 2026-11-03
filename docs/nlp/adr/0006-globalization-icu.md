# ADR 0006: Leave invariant globalization off; pin an app-local ICU later

> **Note (M19e, 2026-10-21):** game-side matters in this record, such as the engine choice and the game's scenes, are now decided in the Vocaterra game repository, not in this one. The text below is unchanged.

- Status: accepted
- Date: 2026-10-20
- Deciders: repository owner and the implementation plan (`implementation-plan.md` section 3, rule 5, and the M0/M1 finding in section 5b)

## Context

Determinism rule 5 requires Unicode NFC, invariant culture and ordinal comparison everywhere. The obvious way to remove
culture from a .NET program is `<InvariantGlobalization>true</InvariantGlobalization>`, which also removes the dependency on
the ICU libraries.

That setting breaks NFC. This was found after M0 and re-checked for this ADR with a throwaway console project on the pinned
SDK (.NET 10.0.402, Linux x64, system ICU 74), normalizing the decomposed string `"cami" + "o" + U+0301 + "n ni" + "n" + U+0303 + "o"`
(13 UTF-16 code units, the way a keyboard layout or a macOS file name can deliver `camión niño`):

| `InvariantGlobalization` | `string.Normalize(NormalizationForm.FormC)` result |
| --- | --- |
| `true` | unchanged, 13 code units, accents still decomposed |
| `false` | composed, 11 code units, equal to `"camión niño"` (precomposed U+00F3 and U+00F1) |

Everything downstream compares ordinally: the answer matcher, the lexicon lookup, the grammar matcher and the content
hashes all assume composed text. A decomposed `ó` that is not composed would not match `ó` in an answer set, and would hash
differently. The written accents of Spanish are exactly where this matters.

## Decision

- `Directory.Build.props` sets `InvariantGlobalization` to `false` for every project. ICU-backed normalization is used for
  `string.Normalize(NormalizationForm.FormC)`.
- Culture behaviour must still never leak in. Every comparison is ordinal (`StringComparison.Ordinal`,
  `StringComparer.Ordinal`), every number is formatted and parsed with `CultureInfo.InvariantCulture`, and case mapping uses
  the invariant methods (`ToLowerInvariant`; the culture-sensitive `ToLower()` and `ToUpper()` do not appear in `src/`).
  The ICU behaviour the program deliberately relies on is Unicode normalization, which is stable across ICU versions for
  characters that already exist (Unicode's normalization stability policy), so the system ICU version should not change a
  result for Spanish text.
- Shipped builds should not rely on whatever ICU the player's machine has. The planned change is an **app-local ICU**: ship a
  fixed ICU build with the game and point the runtime at it with the `System.Globalization.AppLocalIcu` runtime setting. This
  is future work and is not implemented. It needs a new package (the ICU runtime assets for the .NET host), so the repository
  owner must approve the package and its license, and the license scan allowlist has to cover it.

## Consequences

- On Linux the runtime needs the ICU shared libraries. With `InvariantGlobalization` off and no ICU installed, the .NET host
  stops at startup instead of degrading. CI images and developer machines normally have ICU; a minimal container may not.
- Until the app-local ICU exists, two machines with different ICU versions run the same normalization code against different
  data files. The expectation above (normalization of existing characters is stable) is the safety net, plus ordinal
  comparison everywhere else. It is not proven by a test; the cross-platform CI matrix in `.github/workflows-pending/ci.yml`
  would exercise it once activated.
- Alternative considered and not chosen: keep invariant mode and implement NFC ourselves for the characters Spanish needs
  (`á é í ó ú ü ñ` and their capitals). It would remove the ICU dependency and the version question at the price of owning a
  small normalization table and of silently ignoring any other decomposed character. It stays an option if app-local ICU
  turns out to be impractical for an engine's export pipeline.
- When app-local ICU is added, add a test that normalizes a fixed set of decomposed Spanish strings and compares them with
  the expected composed strings, so a broken or missing ICU fails a test and not a player.
