using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The water level value and its byte encoding.</summary>
public sealed class WaterLevelTests
{
    [Fact]
    public void Given_TheDefaultValue_When_Read_Then_ItIsASourceEncodedAsZero()
    {
        Assert.Equal(WaterLevel.Source, default);
        Assert.True(WaterLevel.Source.IsSource);
        Assert.Equal(0, WaterLevel.Source.Distance);
        Assert.False(WaterLevel.Source.Falling);
        Assert.Equal((byte)0, WaterLevel.Source.Encoded);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(7, false)]
    [InlineData(1, true)]
    [InlineData(7, true)]
    public void Given_AFlowingLevel_When_EncodedAndDecoded_Then_ItRoundTrips(int distance, bool falling)
    {
        var level = WaterLevel.Flowing(distance, falling);
        var back = WaterLevel.FromEncoded(level.Encoded);

        Assert.Equal(level, back);
        Assert.Equal(distance, back.Distance);
        Assert.Equal(falling, back.Falling);
        Assert.False(back.IsSource);
        Assert.NotEqual((byte)0, level.Encoded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    public void Given_ADistanceOutsideOneToSeven_When_Made_Then_ItThrows(int distance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WaterLevel.Flowing(distance));
    }

    [Theory]
    [InlineData((byte)8)]
    [InlineData((byte)16)]
    [InlineData((byte)255)]
    public void Given_AnInvalidByte_When_Decoded_Then_ItThrows(byte encoded)
    {
        Assert.Throws<ArgumentException>(() => WaterLevel.FromEncoded(encoded));
    }

    [Fact]
    public void Given_EveryByteValue_When_Decoded_Then_OnlyValidEncodingsRoundTrip()
    {
        var valid = 0;
        for (var b = 0; b < 256; b++)
        {
            try
            {
                Assert.Equal((byte)b, WaterLevel.FromEncoded((byte)b).Encoded);
                valid++;
            }
            catch (ArgumentException)
            {
            }
        }

        Assert.Equal(1 + (7 * 2), valid);
    }
}
