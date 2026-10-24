using Vocaterra.Game.Npc;

namespace Vocaterra.Game.Tests;

/// <summary>The game's host digest covers the simulation digest, the host slots and the extra skills of an utterance (the core's contract).</summary>
public sealed class HostDigestTests
{
    private const string Simulation = "0123abcd";

    [Fact]
    public void TheSameInputsGiveTheSameDigest()
    {
        var slots = new Dictionary<string, string> { ["place"] = "árbol", ["direction"] = "a la derecha" };

        Assert.Equal(HostDigest.Of(Simulation, slots, ["word.árbol"]), HostDigest.Of(Simulation, slots, ["word.árbol"]));
        Assert.Matches("^[0-9a-f]{64}$", HostDigest.Of(Simulation, slots, []));
    }

    [Fact]
    public void TheOrderOfTheSlotsAndTheSkillsDoesNotChangeTheDigest()
    {
        var forward = new Dictionary<string, string> { ["place"] = "árbol", ["direction"] = "a la derecha", ["count"] = "8" };
        var backward = new Dictionary<string, string> { ["count"] = "8", ["direction"] = "a la derecha", ["place"] = "árbol" };

        Assert.Equal(
            HostDigest.Of(Simulation, forward, ["word.árbol", "word.piedra"]),
            HostDigest.Of(Simulation, backward, ["word.piedra", "word.árbol"]));
    }

    [Fact]
    public void AChangedSlotChangesTheDigest()
    {
        var slots = new Dictionary<string, string> { ["place"] = "árbol", ["count"] = "8" };
        var other = new Dictionary<string, string> { ["place"] = "árbol", ["count"] = "9" };

        Assert.NotEqual(HostDigest.Of(Simulation, slots, []), HostDigest.Of(Simulation, other, []));
        Assert.NotEqual(HostDigest.Of(Simulation, slots, []), HostDigest.Of(Simulation, new Dictionary<string, string> { ["place"] = "árbol" }, []));
    }

    [Fact]
    public void AChangedSkillChangesTheDigest()
    {
        Assert.NotEqual(HostDigest.Of(Simulation, new Dictionary<string, string>(), ["word.árbol"]), HostDigest.Of(Simulation, new Dictionary<string, string>(), ["word.piedra"]));
        Assert.NotEqual(HostDigest.Of(Simulation, new Dictionary<string, string>(), []), HostDigest.Of(Simulation, new Dictionary<string, string>(), ["word.árbol"]));
    }

    [Fact]
    public void AChangedSimulationDigestChangesTheDigest()
    {
        var slots = new Dictionary<string, string> { ["place"] = "árbol" };

        Assert.NotEqual(HostDigest.Of(Simulation, slots, ["word.árbol"]), HostDigest.Of("ffff0000", slots, ["word.árbol"]));
    }

    [Fact]
    public void ARepeatedSkillCountsOnce()
    {
        Assert.Equal(HostDigest.Of(Simulation, new Dictionary<string, string>(), ["word.árbol"]), HostDigest.Of(Simulation, new Dictionary<string, string>(), ["word.árbol", "word.árbol"]));
    }
}
