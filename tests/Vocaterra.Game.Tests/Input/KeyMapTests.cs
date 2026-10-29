using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Input;

namespace Vocaterra.Game.Tests.Input;

/// <summary>The one table between the engine's keys and the game's: every game key is the engine key of its name, and back.</summary>
public sealed class KeyMapTests
{
    [Fact]
    public void EveryGameKeyIsTheEngineKeyOfItsNameAndMapsBack()
    {
        foreach (var key in Enum.GetValues<GameKey>())
        {
            var engine = KeyMap.ToKeys(key);
            Assert.Equal(key.ToString(), engine.ToString());
            Assert.Equal(key, KeyMap.ToGameKey(engine));
        }

        Assert.Equal(Keys.NumPad3, KeyMap.ToKeys(GameKey.NumPad3));
        Assert.Null(KeyMap.ToGameKey(Keys.F24)); // a key the game does not name
    }
}
