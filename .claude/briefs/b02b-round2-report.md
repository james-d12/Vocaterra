# B02b round 2 report (Sonnet 5.5, high; 2026-11-16)

Branch `B02b-terrain-tuning`, head `03ee06fa` (four commits on round 1's 3ccc69c6). Agent's suite: 11,933 passed, 0 failed, 3 skipped
(green on the second run: the first was killed at the 6 GB cap while others built; the one before had a timing failure in
`GeneratedLexiconTests.LoadingTheBundleIsFastEnough` that passed alone); build 0 warnings; format clean; validate passes; GoldenDigest
unchanged; fixture bundle a2d6073a..., state digest df006ae6....

- Seas: `ContinentField` (`climate.continents`): jittered Voronoi cells, one open-sea cell in each 2 x 2 block of cells, sea channels of
  120 to 700 blocks along every border (land cells never joined, so one ocean for any seed); continentalness the signed distance from the
  coast, two warps; `salt` 23 picked so the three seeds come near 40% on 16k squares (39 to 42% on 32k for any salt; 15 to 75% on other
  16k windows). A Gaussian field could not be both under 50% sea and connected.
- Ranges: one-octave ridges with rounded crests (`round`, also ending the foothill creases), erosion cell 12,288 (chains 4,000 to
  7,000 long), a steeper peaks spline with no flat top, `peakVariation` (summits and saddles), bigger crags (34 every 80).
- Round the valley: `landBias` 450 at the edge to 150 at 3,000 blocks (no sea within 1,000 on any seed); `landBias.erosion` calm to 800,
  a rugged belt from 1,800 to 8,000.
- Coasts: a clean beach strip (32 to 40 over about 90 blocks), plains at 42 to 50; `coast.flatFrom`/`flatTo`; lakes at their natural
  height; `RiverBankPass` leaves a valley's own profile alone.
- Rivers: valleys cut from sources at 190 to 250 down to the plains, widening with height (`rivers.valley`); water only where the floor
  is under the sea (`waterFrom`/`waterTo`); headwaters kept (valleys shallow to nothing over 320 blocks beyond a region's margin).
- Figures (16k, seeds 42 / 7 / 2026): sea 42.0 / 37.7 / 39.9%; largest sea 100 / 95.8 / 99.5% of ocean; continents 8,256 to 12,352;
  land above 150/200/250 10.0/5.3/2.4, 19.5/13.3/7.4, 13.6/7.5/3.3%; high land spread 28.6 to 34.0 within 64 blocks, no level 64 x 64
  tile; pools 0; river water reaching sea or lake 99.4 / 94.1 / 96.2%; valley columns climbing to 150+ 56.8 / 60.2 / 82.7%; steepest
  step 3 to 5, walls 0.04 to 0.3%; ring walk 191 / 189 / 190 of 192.
- Tests: `ContinentFieldTests`, `RegionLandBiasTests`, `TerrainTuningTests` rewritten, `HeightModelTests` cases, validators,
  `HeadwaterTests` carve depths. Pins: the 18 B03 goldens re-recorded; the fixture; `WorldShapeTests` water share 35 to 48%; B06's
  land-bias tests adapted (radius 3,000).
- Still wrong: water only at sea level, so most rivers are dry valleys (a per-column water level touches the water pass, the biome
  water test, the edge pass, the at-rest and flow invariants); lakes few (0.03% on seed 7, 0.45 to 0.6% on the others); the Voronoi
  polygon shows in coasts and straight channels; ranges broad massifs and Y-shapes; river reach margin thin on seeds 7 and 2026.
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b02b/ (round 1 in `round1/`).
