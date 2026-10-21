# Asset attribution

Every art, font and sound file committed under `assets/` is CC0 (decision D-game-5, while the repository's own licence, D-game-2, is open).
Each file is listed here with its source, author, licence and the date it was fetched. Placeholder art drawn in code (the tiles and the
player in `src/Vocaterra.MonoGame`) is part of the source and is not a file asset.

| File | Source | Author | Licence | Fetched |
| --- | --- | --- | --- | --- |
| `fonts/pixel-operator/PixelOperator.ttf` | Pixel Operator 2018.10.04-1, the `PixelOperator.ttf` of the family's download at https://www.dafont.com/pixel-operator.font (zip: https://dl.dafont.com/dl/?f=pixel_operator) | Jayvee Enaguas (HarvettFox96) | CC0 1.0 Universal, https://creativecommons.org/licenses/zero/1.0/ | 2026-10-21 |
| `fonts/pixel-operator/LICENSE.txt` | the `LICENSE.txt` in the same zip (the CC0 1.0 legal code) | Creative Commons | CC0 1.0 Universal | 2026-10-21 |

## Pixel Operator

- **Licence check (2026-10-21).** The dafont page lists "Public domain / GPL / OFL" as its licence group and quotes the author's note
  "Changed licence to Creative Commons Zero (CC0) 1.0" and "This typeface is made by Jayvee Enaguas (HarvettFox96), licensed under a
  Creative Commons Zero (CC0) 1.0. (c) 2009-2018". The font file's own name table says the same: "Released by Jayvee Enaguas (HarvettFox96)
  ... licensed under a Creative Commons Zero (CC0) 1.0 ... (c) 2009-2018", with licence URL https://creativecommons.org/licenses/zero/1.0/.
  The zip contains `LICENSE.txt`, the CC0 1.0 legal code, which is committed beside the font.
- **Coverage.** Latin-1 and more (241 glyphs per the dafont page). `tests/Vocaterra.MonoGame.Tests/FontTests.cs` parses the TTF's cmap and
  fails if any of `á é í ó ú ü ñ Á É Í Ó Ú Ü Ñ ¿ ¡` has no glyph.
- **Use.** Loaded at run time by FontStashSharp (no content pipeline). A 16 px bitmap-style face: draw it at 16 px or a multiple of 16.
- Only the regular weight is committed; the zip also holds bold, small-caps, mono, 8 px and "HB" variants (not needed yet).
