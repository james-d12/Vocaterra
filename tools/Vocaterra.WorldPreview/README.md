# Vocaterra.WorldPreview

Draws the procedural world as PNG pictures (B02, `docs/game/biomes-plan.md`): the biome of every column, the ground's height, and a vertical slice. It loads `content/game-v1` through `GameData`, reads B01's climate (`ClimateSampler`, filled in strips with `Fill` at 4 blocks a pixel or finer), height model (`HeightModel`) and biome table (`BiomeTable.Classify`), and changes nothing: no tuning, no content edit. The PNGs are written by hand (`ZLibStream` and a CRC-32), so the tool takes no package.

```bash
# both maps of one area (the default area is 4 096 x 4 096 blocks centred on 0,0, 4 blocks a pixel)
scripts/limited.sh dotnet run --project tools/Vocaterra.WorldPreview -- maps --seed 42 --out artifacts/b-series/b02
# one map, a larger area
scripts/limited.sh dotnet run --project tools/Vocaterra.WorldPreview -- biomes --seed 7 --x 0 --z 0 --size 16384 --scale 16 --out biomes.png
scripts/limited.sh dotnet run --project tools/Vocaterra.WorldPreview -- heights --seed 7 --size 16384 --scale 16 --out artifacts/b-series/b02
# a vertical section along a line, 2 pixels per block of height
scripts/limited.sh dotnet run --project tools/Vocaterra.WorldPreview -- slice --seed 42 --x0 2560 --z0 744 --x1 6656 --z1 744 --scale 4 --out slice.png
```

| Command | Writes |
|---|---|
| `biomes` | each column in its biome row's colour; `<name>.legend.txt` lists each row's id, colour and share of the area |
| `heights` | sea blue by depth, lowland green to upland brown to grey stone, white where snow lies, lit from the north-west; `<name>.stats.txt` gives the lowest and highest ground and the share of land above 100, 150, 200 and 250 blocks |
| `maps` | both, for the same area |
| `slice` | y -64 to 320: ground in the surface biome's colour, water blue, the snow line (white) and tree line (green), the sea level dashed yellow, ticks every 64 blocks up and every 1 024 along; with `--mode blocks` (B11) the real blocks of the world, each in its own colour (stone grey, slate dark blue, granite pink, limestone cream, bedrock black, lava orange, water blue, iron, tin and copper ore in their own tints), so the strata, the ores, the caves, the lava, the floor and the entrances show |
| `entrances` | B11: the cave entrances of a square area, one pixel a block (`--size` at most 2 048): the ground in grey by height, water blue, every column cut open to a cave in red; it prints how many columns it found |

`--world valley` (B06) samples the world as the streamed game makes it round the valley, through the scene's place query (`Scene.Places`): the valley's own ground inside its footprint, the land eased into its edge across the blend margin, the continentalness pulled towards land. Its maps carry the outlines of the footprint (red), the end of the blend margin (orange) and the land bias's radius (dashed yellow); `--world free` (the default) is the procedural world alone.

Options: `--world` (`free` or `valley`), `--seed` (default 42), `--x` `--z` (the area's centre), `--size` (blocks, default 4 096), `--scale` (blocks a pixel, default 4; the size is a multiple of it), `--out` (a folder, or a `.png` path for one picture; files in a folder are named from the seed, centre, size and scale), `--content` (default: `content/game-v1` of the checkout), `--threads` (default 4), and for `slice` `--x0 --z0 --x1 --z1`, `--zoom` (pixels per block of height, default 2). x runs to the right, z down the picture. The same arguments write byte-identical files. A 4 096-block map at 4 blocks a pixel, and a 16 384-block one at 16, each take about a second after the bundle loads.

The palette lives in `Palette.cs`: a hue from each row's position in `biomes.json` (the golden-ratio step), and blues for rows whose `when` asks for water. No biome id is written in the code. Tests: `tests/Vocaterra.WorldPreview.Tests` (CRC-32 vectors, PNG decoding, byte-identical output, the biome map against `BiomeTable.Classify`).
