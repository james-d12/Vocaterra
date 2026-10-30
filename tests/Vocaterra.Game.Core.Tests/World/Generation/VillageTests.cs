using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation;

/// <summary>
/// The village, the stall, the two camps and the signposts (S15), and since VILLAGE_PLAN V04 the bakery, the smithy, the well, the farmer's corner and the fisher's
/// bank: what is built where, that it comes from the layout and the seed alone, and its digests.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class VillageTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;

    // The digest of the built things in a box of the world, with the undergrowth, the stall's goods, the wheat and the trees counted as air (they are the seed's, S14 and S15).
    private const string VillageStructureDigest = "1a6c04b2d1b53f34";
    private const string CampsStructureDigest = "ed42e2a69d88beb7c801126a48c72c7a";

    // The village's levelled disc, whose structure the digest covers: 24 since VILLAGE_PLAN V04 (the bakery, the smithy, the well), 19 of the 20 before.
    private const int VillageRadius = 24;

    // The columns within radius of (cx, cz), from y0 to y1: the level ground of a site, where nothing but the built things rises above the ground.
    private static string StructureDigest(VoxelGrid grid, int cx, int cz, int radius, int y0, int y1)
    {
        var hash = 0xCBF29CE484222325UL;
        for (var z = cz - radius; z <= cz + radius; z++)
        {
            for (var y = y0; y <= y1; y++)
            {
                for (var x = cx - radius; x <= cx + radius; x++)
                {
                    if (((x - cx) * (x - cx)) + ((z - cz) * (z - cz)) > radius * radius)
                    {
                        continue;
                    }

                    var block = grid.GetBlock(x, y, z);
                    if (BlockDefinition.IsDecor(block) || BlockDefinition.IsLeaves(block) || BlockDefinition.IsTrunk(block))
                    {
                        block = BlockId.Air;
                    }

                    hash = (hash ^ (ulong)block) * 0x100000001B3UL;
                }
            }
        }

        return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int Count(VoxelGrid grid, int x0, int x1, int y0, int y1, int z0, int z1, BlockId kind)
    {
        var n = 0;
        for (var z = z0; z <= z1; z++)
        {
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    n += grid.GetBlock(x, y, z) == kind ? 1 : 0;
                }
            }
        }

        return n;
    }

    [Fact]
    public void TheVillageIsTheSameForTheSameSeedAndItsStructureNeverChangesWithTheSeed()
    {
        var a = VoxelTestSupport.BuildRoute(Seed);
        var b = VoxelTestSupport.BuildRoute(Seed);
        var village = a.Point("village");
        string Digest(Scene s) => StructureDigest(s.Grid, village.X, village.Z, VillageRadius, village.Y + 1, village.Y + 22);
        Assert.Equal(Digest(a), Digest(b));
        // The layout is the village's and the seed's only in the decorations: another seed gives the same houses, tower, lanterns, signs and stall.
        Assert.Equal(Digest(a), Digest(VoxelTestSupport.BuildRoute(7)));
        Assert.Equal(Digest(a), Digest(VoxelTestSupport.BuildRoute(123456789)));
        output.WriteLine($"village structure digest {Digest(a)}");
    }

    [Fact]
    public void TheVillageStructureMatchesItsGolden()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var v = scene.Point("village");
        var digest = StructureDigest(scene.Grid, v.X, v.Z, VillageRadius, v.Y + 1, v.Y + 22);
        output.WriteLine($"village structure digest {digest}");
        Assert.Equal(VillageStructureDigest, digest);
    }

    [Fact]
    public void TheTwoCampsMatchTheirGolden()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var p = scene.Point("camp.ledge");
        var g = scene.Point("camp.guide");
        var digest = StructureDigest(scene.Grid, p.X, p.Z, 9, p.Y + 1, p.Y + 8) + StructureDigest(scene.Grid, g.X, g.Z, 11, g.Y + 1, g.Y + 8);
        output.WriteLine($"camps structure digest {digest}");
        Assert.Equal(CampsStructureDigest, digest);
    }

    [Fact]
    public void TheHousesAreTimberFramedWithSteppedRoofsAndChimneys()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        foreach (var name in new[] { "house1", "house2", "house3" })
        {
            var centre = scene.Point(name); // the middle of a 5 x 5 house, on its floor
            var floor = centre.Y;
            // The four corner posts and the beam under the eaves are timber, the walls between are plaster, the roof steps in 5, 3 and 1.
            foreach (var (dx, dz) in new[] { (-2, -2), (2, -2), (-2, 2), (2, 2) })
            {
                for (var y = 1; y <= 3; y++)
                {
                    Assert.Equal(BlockId.Timber, grid.GetBlock(centre.X + dx, floor + y, centre.Z + dz));
                }
            }

            Assert.Equal(BlockId.Roof, grid.GetBlock(centre.X, floor + 4, centre.Z)); // the first layer is the whole footprint
            Assert.Equal(BlockId.Roof, grid.GetBlock(centre.X + 2, floor + 4, centre.Z + 2));
            Assert.Equal(BlockId.Roof, grid.GetBlock(centre.X + 1, floor + 5, centre.Z + 1)); // the second is 3 x 3
            Assert.NotEqual(BlockId.Roof, grid.GetBlock(centre.X + 2, floor + 5, centre.Z)); // so the outer ring of the second layer is not roof
            Assert.Equal(BlockId.Roof, grid.GetBlock(centre.X, floor + 6, centre.Z)); // the ridge cap
            var chimneys = Count(grid, centre.X - 2, centre.X + 2, floor + 5, floor + 9, centre.Z - 2, centre.Z + 2, BlockId.Cobble);
            Assert.True(chimneys >= 3, $"{name} has a chimney of cobble rising above its roof");
        }
    }

    [Fact]
    public void TheStallHasAStripedAwningAndGoodsInCratesAndOnTheCounter()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var counter = scene.Point("stall"); // the middle of the counter, on the ground; the counter is on the west side
        Assert.Equal(BlockId.Stall, grid.GetBlock(counter.X, counter.Y + 1, counter.Z));
        var goods = new[] { BlockId.Loaves, BlockId.Apples, BlockId.Bottles };
        for (var dz = -1; dz <= 1; dz++)
        {
            Assert.Contains(grid.GetBlock(counter.X, counter.Y + 2, counter.Z + dz), goods);
        }

        // The awning (y + 4) alternates red and white across the counter, and reaches one block out over the front.
        var kinds = Enumerable.Range(-2, 5).Select(dz => grid.GetBlock(counter.X, counter.Y + 4, counter.Z + dz)).ToList();
        Assert.All(kinds, static k => Assert.True(k is BlockId.AwningRed or BlockId.AwningWhite));
        for (var i = 1; i < kinds.Count; i++)
        {
            Assert.NotEqual(kinds[i - 1], kinds[i]);
        }

        Assert.Equal(BlockId.AwningRed, grid.GetBlock(counter.X - 1, counter.Y + 4, counter.Z - 2)); // the overhang
        // Crates behind the counter, with goods on them, and the middle cell behind it empty for the shopkeeper.
        Assert.Equal(BlockId.Crate, grid.GetBlock(counter.X + 1, counter.Y + 1, counter.Z - 1));
        Assert.Equal(BlockId.Crate, grid.GetBlock(counter.X + 1, counter.Y + 1, counter.Z + 1));
        Assert.Contains(grid.GetBlock(counter.X + 1, counter.Y + 2, counter.Z + 1), goods);
        Assert.Equal(BlockId.Air, grid.GetBlock(counter.X + 1, counter.Y + 1, counter.Z));
        var shopkeeper = scene.Npc("shopkeeper").Feet;
        Assert.Equal((counter.X + 1, counter.Z), (shopkeeper.X, shopkeeper.Z));
    }

    [Fact]
    public void TheGoodsAreTheSeedsAndTheOtherSeedsStockTheStallDifferently()
    {
        var a = VoxelTestSupport.BuildRoute(Seed);
        var stock = (Scene s) =>
        {
            var c = s.Point("stall");
            return string.Concat(Enumerable.Range(-1, 3).Select(dz => ((int)s.Grid.GetBlock(c.X, c.Y + 2, c.Z + dz)).ToString(System.Globalization.CultureInfo.InvariantCulture) + ","));
        };
        var again = stock(VoxelTestSupport.BuildRoute(Seed));
        Assert.Equal(stock(a), again);
        var seeds = new[] { 1UL, 2UL, 3UL, 7UL, 99UL, 123456789UL }.Select(seed => stock(VoxelTestSupport.BuildRoute(seed))).Append(stock(a)).Distinct().Count();
        Assert.True(seeds > 1, "the counter's goods come from the seed");
    }

    [Fact]
    public void TheVillageHasATowerACropFieldFencesAndLanternsThatLightTheSquare()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var tower = scene.Point("tower");
        Assert.Equal(BlockId.Stone, grid.GetBlock(tower.X, tower.Y + 1, tower.Z - 2));
        Assert.Equal(BlockId.Air, grid.GetBlock(tower.X, tower.Y + 1, tower.Z + 2)); // the door in the south face: the stamp is drawn with the door on its bottom row
        Assert.Equal(BlockId.Cobble, grid.GetBlock(tower.X + 2, tower.Y + 3, tower.Z));
        Assert.Equal(BlockId.Air, grid.GetBlock(tower.X + 2, tower.Y + 4, tower.Z)); // a window slit in the next layer
        Assert.True(Count(grid, tower.X - 2, tower.X + 2, tower.Y, tower.Y + 12, tower.Z - 2, tower.Z + 2, BlockId.Roof) > 20, "a stepped roof");
        Assert.Equal(2, Count(grid, tower.X - 2, tower.X + 2, tower.Y, tower.Y + 12, tower.Z - 2, tower.Z + 2, BlockId.Fence)); // a flagpole

        var field = scene.Point("field");
        var tilled = Count(grid, field.X - 4, field.X + 4, field.Y - 1, field.Y, field.Z - 4, field.Z + 4, BlockId.Farmland);
        Assert.Equal(5 * 4, tilled); // a 7 x 6 field inside its fence is 5 x 4 of soil
        Assert.True(Count(grid, field.X - 4, field.X + 4, field.Y, field.Y + 2, field.Z - 4, field.Z + 4, BlockId.Wheat) > 5, "wheat in rows");
        Assert.True(Count(grid, field.X - 4, field.X + 4, field.Y, field.Y + 2, field.Z - 4, field.Z + 4, BlockId.Fence) >= 16, "the field is fenced");

        var lamps = scene.Points.Keys.Where(static k => k.StartsWith("lamp.", StringComparison.Ordinal)).ToList();
        Assert.Equal(9, lamps.Count); // S15's six, and the bakery's, the smithy's and the fisher's (V04)
        foreach (var lamp in lamps)
        {
            var p = scene.Points[lamp];
            Assert.Equal(BlockId.Fence, grid.GetBlock(p.X, p.Y + 1, p.Z));
            Assert.Equal(BlockId.Lantern, grid.GetBlock(p.X, p.Y + 3, p.Z));
        }

        Assert.Equal(10, grid.BlocksOfKind(BlockId.Lantern).Count); // the nine on posts and the one hung in the guide's pergola
    }

    [Fact]
    public void TheSignpostCarriesTheThreePlaceNamesAsBoardsThatReadFromTheRoad()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var post = scene.Point("signpost");
        // Read from the west (the road comes in from there) the boards run to the reader's right: south, from the post.
        Assert.Equal(BlockId.Fence, grid.GetBlock(post.X, post.Y + 1, post.Z));
        for (var row = 0; row < 3; row++)
        {
            Assert.Equal(BlockId.Sign, grid.GetBlock(post.X, post.Y + 3 + row, post.Z));
        }

        Assert.Equal(BlockId.SignTiendaA, grid.GetBlock(post.X, post.Y + 3, post.Z + 1));
        Assert.Equal(BlockId.SignTiendaB, grid.GetBlock(post.X, post.Y + 3, post.Z + 2));
        Assert.Equal(BlockId.SignRio, grid.GetBlock(post.X, post.Y + 4, post.Z + 1));
        Assert.Equal(BlockId.Air, grid.GetBlock(post.X, post.Y + 4, post.Z + 2)); // the river's board is one cell long
        Assert.Equal(BlockId.SignMontanaA, grid.GetBlock(post.X, post.Y + 5, post.Z + 1));
        Assert.Equal(BlockId.SignMontanaB, grid.GetBlock(post.X, post.Y + 5, post.Z + 2));
    }

    [Fact]
    public void ThePlayersCampHasAColdFireRingATentAChestAndACrate()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var ring = scene.Point("firering.player");
        Assert.Equal(BlockId.Air, grid.GetBlock(ring.X, ring.Y + 1, ring.Z)); // cold: nothing in the middle (S17 lights it with a torch on the rim)
        Assert.Equal(BlockId.FireRing, grid.GetBlock(ring.X + 1, ring.Y + 1, ring.Z));
        var tent = scene.Point("tent");
        Assert.Equal(BlockId.Canvas, grid.GetBlock(tent.X, tent.Y + 3, tent.Z));
        Assert.Equal(BlockId.Chest, grid.GetBlock(scene.Point("chest.player").X, scene.Point("chest.player").Y + 1, scene.Point("chest.player").Z));
        Assert.Equal(BlockId.Chest, grid.GetBlock(scene.Point("chest.guide").X, scene.Point("chest.guide").Y + 1, scene.Point("chest.guide").Z));
    }

    [Fact]
    public void TheGuidesWorkbenchStandsUnderAPergolaOnThinPostsWithATorchAndAHangingLantern()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var bench = scene.Point("workbench");
        Assert.Equal(BlockId.Table, grid.GetBlock(bench.X, bench.Y + 1, bench.Z));
        var corner = scene.Point("pergola"); // the north-west post
        foreach (var (dx, dz) in new[] { (0, 0), (5, 0), (0, 2), (5, 2) })
        {
            for (var y = 1; y <= 3; y++)
            {
                // Thin fence posts like the stall's front posts (S22): full timber blocks filled the view at the workbench.
                Assert.Equal(BlockId.Fence, grid.GetBlock(corner.X + dx, corner.Y + y, corner.Z + dz));
            }
        }

        Assert.Equal(BlockId.PostTorch, grid.GetBlock(corner.X, corner.Y + 4, corner.Z)); // a torch on the north-west post (scenery: not the item's torch block)
        Assert.Empty(grid.BlocksOfKind(BlockId.Torch)); // so "where is the torch" never points at it
        Assert.Equal(BlockId.Timber, grid.GetBlock(corner.X + 1, corner.Y + 4, corner.Z)); // the beam
        Assert.Equal(BlockId.Lantern, grid.GetBlock(corner.X + 3, corner.Y + 3, corner.Z + 1)); // over the bench
    }

    [Fact]
    public void NothingNewBlocksTheScriptedRouteOrTheNpcs()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        foreach (var npc in scene.Npcs)
        {
            Assert.False(grid.IsSolid(npc.Feet.X, npc.Feet.Y, npc.Feet.Z));
            Assert.False(grid.IsSolid(npc.Feet.X, npc.Feet.Y + 1, npc.Feet.Z));
        }

        // The cells the S17 route stands on and walks to are clear of the furnishing (feet and head).
        foreach (var (x, y, z) in new[] { (172, 43, 187), (173, 43, 183), (177, 43, 185), (174, 43, 185), (155, 42, 187), (153, 74, 109) })
        {
            var ground = grid.HighestBlock(x, z);
            Assert.False(grid.IsSolid(x, ground + 1, z), $"({x}, {z}) feet");
            Assert.False(grid.IsSolid(x, ground + 2, z), $"({x}, {z}) head");
        }
    }

    [Fact]
    public void TheBakeryHasItsOvenWithAFireAndAChimneyACrateOfLoavesALampAndAPathToTheSquare()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var bakery = scene.Point("bakery"); // the middle of a 6 x 5 house, on its floor
        var floor = bakery.Y;
        Assert.Equal(BlockId.Planks, grid.GetBlock(bakery.X, floor, bakery.Z));
        Assert.Equal(BlockId.Door, grid.GetBlock(bakery.X, floor + 1, bakery.Z + 2)); // the door in the south wall
        Assert.Equal(BlockId.Timber, grid.GetBlock(bakery.X + 2, floor + 1, bakery.Z - 2)); // the north-east corner post
        Assert.True(Count(grid, bakery.X - 3, bakery.X + 2, floor + 5, floor + 9, bakery.Z - 2, bakery.Z + 2, BlockId.Cobble) >= 3, "the bakery has a chimney");

        // The oven against the east wall: a cobble vault with the fire in its mouth, open to the south, and a chimney at the back by the wall.
        var oven = scene.Point("oven"); // the middle of its mouth row
        Assert.Equal(bakery.X + 4, oven.X);
        Assert.Equal(BlockId.PostTorch, grid.GetBlock(oven.X, floor + 1, oven.Z));
        Assert.False(grid.IsSolid(oven.X, floor + 1, oven.Z + 1), "the oven's mouth is open to the south");
        foreach (var (x, y, z) in new[] { (oven.X - 1, 1, oven.Z), (oven.X + 1, 1, oven.Z), (oven.X, 1, oven.Z - 1), (oven.X, 2, oven.Z), (oven.X - 1, 2, oven.Z - 1) })
        {
            Assert.Equal(BlockId.Cobble, grid.GetBlock(x, floor + y, z));
        }

        Assert.Equal(BlockId.Wall, grid.GetBlock(oven.X - 2, floor + 1, oven.Z)); // the bakery's east wall, behind the vault
        Assert.Equal(3, Count(grid, oven.X - 1, oven.X - 1, floor + 3, floor + 5, oven.Z - 1, oven.Z - 1, BlockId.Cobble)); // the oven's chimney

        var crate = scene.Point("crate.loaves");
        Assert.Equal((BlockId.Crate, BlockId.Loaves), (grid.GetBlock(crate.X, floor + 1, crate.Z), grid.GetBlock(crate.X, floor + 2, crate.Z)));
        Assert.Equal(BlockId.Lantern, grid.GetBlock(scene.Point("lamp.bakery").X, floor + 3, scene.Point("lamp.bakery").Z));

        // A paved strip from the step outside the door south and then east, onto the square's paving.
        foreach (var (x, z) in new[] { (bakery.X, bakery.Z + 3), (bakery.X, bakery.Z + 5), (bakery.X, bakery.Z + 7), (bakery.X, bakery.Z + 8), (bakery.X + 1, bakery.Z + 8), (bakery.X + 2, bakery.Z + 8) })
        {
            Assert.Equal(BlockId.Path, grid.GetBlock(x, floor, z));
        }
    }

    [Fact]
    public void TheSmithyHasItsForgeWithAFireAnAnvilCratesALampAndAPathToTheSquare()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var smithy = scene.Point("smithy");
        var floor = smithy.Y;
        Assert.Equal(BlockId.Door, grid.GetBlock(smithy.X - 2, floor + 1, smithy.Z)); // the door in the west wall

        // The forge outside the door: a ring of eight stones with a fire burning in it (a torch of the scenery, never the item's), then the anvil to the south.
        var forge = scene.Point("forge");
        Assert.Equal(smithy.Z, forge.Z);
        Assert.Equal(8, Count(grid, forge.X - 1, forge.X + 1, floor + 1, floor + 1, forge.Z - 1, forge.Z + 1, BlockId.FireRing));
        Assert.Equal(BlockId.PostTorch, grid.GetBlock(forge.X, floor + 1, forge.Z));
        Assert.False(grid.IsSolid(forge.X + 2, floor + 1, forge.Z), "the way in between the forge and the door is clear");
        var anvil = scene.Point("anvil");
        Assert.Equal(BlockId.Stone, grid.GetBlock(anvil.X, floor + 1, anvil.Z));
        Assert.Equal(3, Count(grid, smithy.X - 2, smithy.X + 2, floor + 1, floor + 2, smithy.Z + 3, smithy.Z + 3, BlockId.Crate));
        Assert.Equal(BlockId.Lantern, grid.GetBlock(scene.Point("lamp.smithy").X, floor + 3, scene.Point("lamp.smithy").Z));

        // A paved strip from the step outside the door, south, west along the forge's yard and south again to the square's corner.
        foreach (var (x, z) in new[] { (smithy.X - 3, smithy.Z), (smithy.X - 3, smithy.Z + 3), (smithy.X - 5, smithy.Z + 3), (smithy.X - 7, smithy.Z + 3), (smithy.X - 7, smithy.Z + 7), (smithy.X - 7, smithy.Z + 8) })
        {
            Assert.Equal(BlockId.Path, grid.GetBlock(x, floor, z));
        }
    }

    [Fact]
    public void TheWellHoldsItsWaterInsideARimThatNoOneOnTheGroundCanReachOver()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var well = scene.Point("well"); // the middle of the 3 x 3 well, on the ground under its water
        var y = well.Y + 1;
        Assert.Equal(BlockId.Water, grid.GetBlock(well.X, y, well.Z));
        Assert.True(grid.IsSolid(well.X, well.Y, well.Z), "the water stands on the ground");
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            Assert.Equal(BlockId.Cobble, grid.GetBlock(well.X + dx, y, well.Z + dz)); // contained on its four sides
            Assert.Equal(BlockId.Cobble, grid.GetBlock(well.X + dx, y + 1, well.Z + dz)); // and a rim a block higher
        }

        Assert.Equal(BlockId.Air, grid.GetBlock(well.X, y + 1, well.Z));
        Assert.Equal(1, Count(grid, well.X - 2, well.X + 2, y - 1, y + 6, well.Z - 2, well.Z + 2, BlockId.Water)); // one block of water, no more
        Assert.Equal(4 * 2, Count(grid, well.X - 1, well.X + 1, y + 2, y + 3, well.Z - 1, well.Z + 1, BlockId.Fence)); // four posts
        Assert.Equal(9 + 1, Count(grid, well.X - 1, well.X + 1, y + 4, y + 5, well.Z - 1, well.Z + 1, BlockId.Roof)); // the roof and its cap

        // No ray from an eye on the ground round the well reaches its water, standing or at the top of a jump (the rim is two blocks high), so a bottle is
        // filled at the river, where the villager sends the learner (S3), and never here.
        const int U = VoxelPhysics.UnitsPerBlock;
        var jumpApex = 0;
        for (var v = VoxelPhysics.JumpVelocity - VoxelPhysics.Gravity; v > 0; v -= VoxelPhysics.Gravity)
        {
            jumpApex += v;
        }

        var rays = 0;
        for (var fz = well.Z - 5; fz <= well.Z + 5; fz++)
        {
            for (var fx = well.X - 5; fx <= well.X + 5; fx++)
            {
                if (Math.Abs(fx - well.X) <= 1 && Math.Abs(fz - well.Z) <= 1 || grid.IsSolid(fx, y, fz) || grid.IsSolid(fx, y + 1, fz) || !grid.IsSolid(fx, y - 1, fz))
                {
                    continue; // the well itself, or no place to stand
                }

                for (var lift = 0; lift <= jumpApex; lift += jumpApex / 4)
                {
                    foreach (var (ox, oz) in new[] { (U / 2, U / 2), ((U * 3) / 10, (U * 3) / 10), ((U * 7) / 10, (U * 7) / 10), ((U * 3) / 10, (U * 7) / 10), ((U * 7) / 10, (U * 3) / 10) })
                    {
                        var eye = (X: (fx * U) + ox, Y: (y * U) + VoxelPhysics.EyeHeight + lift, Z: (fz * U) + oz);
                        foreach (var (tx, tz) in new[] { (0.1, 0.1), (0.5, 0.5), (0.9, 0.9), (0.1, 0.9), (0.9, 0.1), (0.5, 0.1), (0.5, 0.9), (0.1, 0.5), (0.9, 0.5) })
                        {
                            var hit = Cast(grid, eye, ((well.X + tx) * U, (y + 0.95) * U, (well.Z + tz) * U));
                            rays++;
                            Assert.False(hit?.Id == BlockId.Water, $"an eye at {eye} reaches the well's water");
                        }
                    }
                }
            }
        }

        output.WriteLine($"{rays} rays from the ground round the well, none reaches its water (a jump lifts the eye {jumpApex / (double)U:F2} blocks)");
        Assert.True(rays > 1000);
    }

    [Fact]
    public void TheFarmerHasACrateOfApplesAndAnOrchardAndTheFisherABankAtTheBridge()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var apples = scene.Point("crate.apples");
        Assert.Equal((BlockId.Crate, BlockId.Apples), (grid.GetBlock(apples.X, apples.Y + 1, apples.Z), grid.GetBlock(apples.X, apples.Y + 2, apples.Z)));
        var farmer = scene.Point("npc.farmer");
        Assert.Equal(BlockId.Fence, grid.GetBlock(farmer.X + 1, farmer.Y + 1, farmer.Z)); // he stands at the field's west fence, facing the wheat
        Assert.Equal(3, Distance(apples, farmer));

        // Three oaks in a row east of the field, the farmer's orchard.
        var village = scene.Point("village");
        foreach (var dz in new[] { -4, 0, 4 })
        {
            Assert.Equal(BlockId.Trunk, grid.GetBlock(village.X + 19, village.Y + 1, village.Z + dz));
        }

        // The fisher's bank on the bridge's east landing: two crates and a lamp beside the place he will stand.
        var fisher = scene.Point("npc.fisher");
        Assert.Equal(BlockId.Crate, grid.GetBlock(fisher.X + 1, grid.HighestBlock(fisher.X + 1, fisher.Z), fisher.Z));
        Assert.Equal(BlockId.Crate, grid.GetBlock(fisher.X + 1, grid.HighestBlock(fisher.X + 1, fisher.Z + 1), fisher.Z + 1));
        var lamp = scene.Point("lamp.fisher");
        Assert.Equal(BlockId.Lantern, grid.GetBlock(lamp.X, lamp.Y + 3, lamp.Z));
        Assert.InRange(Distance(lamp, fisher), 1, 3);
    }

    [Fact]
    public void TheNewPlacesShowTheirWordsOnTheBeacon()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        Assert.Equal(
            ["panadería", "herrería", "pozo", "torre", "campo", "puente"],
            new[] { "bakery", "smithy", "well", "tower", "field", "bridge" }.Select(scene.LabelOf));
    }

    // The first block a ray from an eye towards a point meets, as the block ray finds it (positions in units).
    private static BlockRayHit? Cast(VoxelGrid grid, (int X, int Y, int Z) eye, (double X, double Y, double Z) target)
    {
        var (dx, dy, dz) = (target.X - eye.X, target.Y - eye.Y, target.Z - eye.Z);
        var length = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        return BlockRay.Cast(grid, eye.X, eye.Y, eye.Z, (int)Math.Round(dx / length * Angles.One), (int)Math.Round(dy / length * Angles.One), (int)Math.Round(dz / length * Angles.One), (long)8 * VoxelPhysics.UnitsPerBlock);
    }

    private static int Distance(BlockPos a, BlockPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));

    [Fact]
    public void EveryNewKindIsInTheSceneAndTheCountsArePrinted()
    {
        var grid = VoxelTestSupport.BuildRoute(Seed).Grid;
        var total = 0;
        foreach (var kind in Enum.GetValues<BlockId>().Where(static k => k >= BlockId.Timber))
        {
            var count = grid.BlocksOfKind(kind).Count;
            total += count;
            output.WriteLine($"{kind}: {count}");
            Assert.True(count > 0, $"{kind} is in the scene");
        }

        output.WriteLine($"blocks of the S15 kinds: {total}");
    }
}
