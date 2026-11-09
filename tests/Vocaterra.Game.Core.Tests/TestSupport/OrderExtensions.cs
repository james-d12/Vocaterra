using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>What the tests ask of the orders since issue #54: an order is shown, counted and delivered only once the player has taken it on.</summary>
internal static class OrderExtensions
{
    /// <summary>
    /// Counts orders as taken on, as a learner who said «sí» to each NPC's offer did, with no talk and no event: a test about something else then talks to the NPC as it
    /// did before issue #54 (the talk opens with the NPC's greeting, or its question for the goods). With no ids, every order of the session.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="orders">The orders' objective ids, or none for all.</param>
    /// <returns>The session.</returns>
    public static VoxelSession TakeOrders(this VoxelSession session, params string[] orders)
    {
        var taken = orders.Length > 0 ? orders : session.Objectives.Where(static o => o.Spec.IsOrder).Select(static o => o.Id).ToArray();
        session.RestoreGivenOrders([.. session.GivenOrders, .. taken]);
        return session;
    }

    /// <summary>When the NPC the panel is open on offers its order (issue #54, <see cref="GuideSession.OrderOffered"/>), says «Sí.» and returns that turn; otherwise <see langword="null"/>.</summary>
    /// <param name="session">The session.</param>
    public static GuideTurn? AcceptOrder(this VoxelSession session) => session.OrderOffered is null ? null : session.Say("Sí.");
}
