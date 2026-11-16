using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The river banks (B03): the valley's bank profile, ported from <c>TerrainGenerator.BankHeight</c>. A dry column the river or a lake reached (the height model's carve weight above zero)
/// starts at the water's own level (the level of the river or lake: B02b round 3, <see cref="WaterSpec"/>) where the carve is full and eases up to the natural ground as it fades, by the square of the distance, so the shore stands a block over the shallow bed at
/// most and a long gentle shoulder leads up to the land. A column keeps the lower of the height the model gave it and the bank, and never falls under the water's level. Pure per column: it reads only the column's own shape.
/// </summary>
internal sealed class RiverBankPass : IColumnShaper
{
    private readonly GenerationWorld _world;

    public RiverBankPass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name.</summary>
    public const string Name = "gen/banks";

    /// <inheritdoc />
    public string PassName => Name;

    /// <summary>
    /// The height of a bank column (sixteenths of a block) beside water whose surface layer is <paramref name="water"/>: the bank starts at the water's own level and eases up to the natural
    /// ground (the weight squared). Copied from the valley's generator unchanged.
    /// </summary>
    /// <param name="water">The water's surface layer.</param>
    /// <param name="nat16">The natural ground, in sixteenths.</param>
    /// <param name="weight">0 at the water's edge to <see cref="IntMath.One"/> far from it.</param>
    internal static int BankHeight(int water, int nat16, int weight) =>
        IntMath.Lerp(water * 16, Math.Max(nat16, (water + 1) * 16), (int)((long)weight * weight / IntMath.One));

    /// <inheritdoc />
    public void Shape(ColumnContext ctx)
    {
        for (var z = ctx.OriginZ; z < ctx.OriginZ + ctx.Size; z++)
        {
            for (var x = ctx.OriginX; x < ctx.OriginX + ctx.Size; x++)
            {
                var shape = ctx.ShapeAt(x, z);
                var carve = Math.Max(shape.RiverWeight, shape.LakeWeight);
                if (shape.Water || carve == 0)
                {
                    continue;
                }

                var level = shape.Surface >= 0 ? shape.Surface : _world.SeaLevel;
                var bank = IntMath.DivDown(BankHeight(level, shape.NaturalHeight * 16, IntMath.One - carve), 16);
                if (bank < ctx.HeightAt(x, z))
                {
                    ctx.SetHeight(x, z, Math.Max(bank, level));
                }
            }
        }
    }
}
