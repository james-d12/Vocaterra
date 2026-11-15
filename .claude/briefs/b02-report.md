# B02 report (Sonnet 5.5, medium; 2026-11-15)

Branch `B02-world-preview`, head `0438bc3a` (6f02f988 tool and tests, 0438bc3a height stats, README, running.md). Agent's suite:
11,671 passed, 3 skipped (21 new); licence scan PASSED. Not yet gated: the gate was stopped when the owner stopped the work.

- Built: tools/Vocaterra.WorldPreview in Vocaterra.sln (reads only; PNG via ZLibStream and a hand-written CRC32). Commands `biomes`
  (+ .legend.txt), `heights` (+ .stats.txt), `maps`, `slice`; options --seed --x --z --size --scale --out --content --threads (4),
  slice --x0 --z0 --x1 --z1 --zoom. Fill in strips at 4 blocks/pixel or finer, At per column above (same samples, tested). Palette in
  the tool by row position (blues for water rows). Tests: tests/Vocaterra.WorldPreview.Tests (21). Docs: tool README, running.md
  "World preview (B02)", root README tools line.
- Times: about 1.0 s per 4096-block map at 4/pixel, 1.4 s per 16384-block map at 16/pixel, slices under 0.1 s.
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b02/ (37 files, index.md), seeds 42, 7, 2026.
- Biome shares (42/7/2026): ocean 13.9/14.1/15.3, river 2.6/2.7/2.5, lake 0.35/0.37/0.33, beach 3.1/3.2/3.0, woodland 30.4/25.2/27.8,
  plains 14.8/14.7/15.5, taiga 7.1/5.4/6.4, birchwood 6.7/11.4/8.0, dense 4.7/6.9/5.6, grove 5.5/5.4/5.3, meadow 5.1/5.3/4.8,
  snowy-slopes 3.7/3.3/3.5, jagged 1.2/1.3/1.4, stony 0.8/0.75/0.7.
- What looked wrong (the owner reviewed it, D-game-34 item 16; B02b tunes): ranges are needles 40 to 70 blocks wide to about 290 out of
  land at 40 to 60 (5% of land above 150, 2% above 200); cliffs at upland shelves; coasts drop 15 to 20 blocks with no shelf, flat sea
  floors at 10 to 22; most "ocean" is inland basins on seeds 42 and 7; snow on cold lowland; rivers are short segments, some loops
  (seed 42 near x 5780, z 1424); dithered borders show as stripes in slices.
