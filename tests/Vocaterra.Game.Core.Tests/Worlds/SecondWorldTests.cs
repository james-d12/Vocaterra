using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Worlds;

/// <summary>
/// The Main menu closes a world and the title starts another in the same process (Q01): nothing a world holds may reach the next one. Three sessions are made one after the other in this process: a world of one seed, a world of
/// another, and the first seed again, and each is what it would be as the only one; a save of the first loads into a session made after the second.
/// </summary>
public sealed class SecondWorldTests
{
    private const int Ticks = 90;

    private static VoxelSession Played(ulong seed)
    {
        var session = TestGame.NewSession(seed);
        for (var i = 0; i < Ticks; i++)
        {
            session.Step([]);
        }

        return session;
    }

    [Fact]
    public void Given_AWorldPlayedAndLeft_When_AnotherSeedAndThenTheFirstSeedAgainAreStarted_Then_EachIsWhatItIsAlone()
    {
        var first = Played(42);
        var firstDigest = first.Simulation.ComputeStateDigest();
        var firstTick = first.Simulation.Tick;

        var other = Played(7);
        var otherDigest = other.Simulation.ComputeStateDigest();
        var again = Played(42);

        Assert.NotEqual(firstDigest, otherDigest); // another seed is another world
        Assert.Equal(firstDigest, again.Simulation.ComputeStateDigest()); // the first seed again is the first world, whatever came between
        Assert.Equal(firstTick, again.Simulation.Tick);
        Assert.Equal(first.Simulation.Player.FeetBlock, again.Simulation.Player.FeetBlock);
        Assert.Equal(otherDigest, Played(7).Simulation.ComputeStateDigest());
        Assert.Equal(first.WorldSeed, again.WorldSeed);
        Assert.Equal(7UL, other.WorldSeed);
        Assert.NotSame(first.Scene.Grid, again.Scene.Grid); // a fresh world: no grid is shared
    }

    [Fact]
    public void Given_ASaveOfTheFirstWorld_When_ItIsLoadedAfterAnotherWorldWasStarted_Then_ItRestoresEqualAndTheOtherWorldIsUntouched()
    {
        var first = Played(42);
        var text = SaveGame.Save(first);
        var other = Played(7);
        var otherDigest = other.Simulation.ComputeStateDigest();

        var loaded = SaveGame.Load(text, TestGame.Data, static seed => TestGame.Route(seed), credit: first.Credit, bumpSession: false);

        Assert.Equal(first.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(42UL, loaded.WorldSeed);
        Assert.Equal(otherDigest, other.Simulation.ComputeStateDigest());
        Assert.Equal(7UL, other.WorldSeed);
    }
}
