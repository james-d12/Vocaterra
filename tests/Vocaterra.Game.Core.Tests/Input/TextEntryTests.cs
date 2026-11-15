using Vocaterra.Game.Core.Input;

namespace Vocaterra.Game.Core.Tests.Input;

/// <summary>The dialogue panel's text box, its Spanish characters and the accent shortcuts, without a window.</summary>
public sealed class TextEntryTests
{
    // The Spanish map (game/words.json, typing) as the fixture writes it; the game reads the same list from the file.
    private static readonly TypingMap Spanish = new(
    [
        new TypingKey('a', null, 'á'), new TypingKey('e', null, 'é'), new TypingKey('i', null, 'í'), new TypingKey('o', null, 'ó'),
        new TypingKey('u', null, 'ú'), new TypingKey('n', null, 'ñ'), new TypingKey('/', '?', '¿'), new TypingKey('1', '!', '¡'),
    ]);

    // A German map: the same keys, its own letters (ä, ö, ü and ß), with no code change.
    private static readonly TypingMap German = new(
    [
        new TypingKey('a', null, 'ä'), new TypingKey('o', null, 'ö'), new TypingKey('u', null, 'ü'), new TypingKey('s', null, 'ß'),
    ]);

    [Fact]
    public void AGermanMapTypesItsOwnLettersAndItsHintListsThem()
    {
        Assert.Equal('Ä', German.ForTyped('A', alt: true));
        Assert.Equal('ß', German.ForTyped('s', alt: true));
        Assert.Equal('e', German.ForTyped('e', alt: true)); // no key for it on this map
        Assert.Null(German.Map('e', false));
        Assert.Equal("Alt+a o u s: ä ö ü ß", German.Hint());
    }

    [Fact]
    public void TheSpanishHintListsTheLettersAndTheMarks()
    {
        Assert.Equal("Alt+a e i o u n: á é í ó ú ñ   Alt+/: ¿   Alt+1: ¡", Spanish.Hint());
    }

    [Fact]
    public void AKeyGivenTwiceIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new TypingMap([new TypingKey('a', null, 'á'), new TypingKey('A', null, 'Á')]));
    }

    [Fact]
    public void LettersAndSpacesAreInsertedInOrder()
    {
        var entry = new TextEntry();

        Assert.Equal(4, entry.Insert("hola"));
        Assert.True(entry.Insert(' '));
        Assert.Equal(3, entry.Insert("tú!"));

        Assert.Equal("hola tú!", entry.Text);
        Assert.Equal(8, entry.Caret);
    }

    [Theory]
    [InlineData('á')]
    [InlineData('é')]
    [InlineData('í')]
    [InlineData('ó')]
    [InlineData('ú')]
    [InlineData('ü')]
    [InlineData('ñ')]
    [InlineData('Á')]
    [InlineData('Ñ')]
    [InlineData('¿')]
    [InlineData('¡')]
    [InlineData('?')]
    [InlineData('!')]
    [InlineData(',')]
    [InlineData('7')]
    public void TheSpanishLettersAndMarksAreAccepted(char c)
    {
        var entry = new TextEntry();

        Assert.True(TextEntry.IsAccepted(c));
        Assert.True(entry.Insert(c));
        Assert.Equal(c.ToString(), entry.Text);
    }

    [Theory]
    [InlineData('\r')]
    [InlineData('\n')]
    [InlineData('\t')]
    [InlineData('\b')]
    [InlineData('\u001b')]
    [InlineData('\0')]
    [InlineData('́')] // a combining accent on its own
    [InlineData('\ud83d')] // half a surrogate pair
    public void ControlCharactersLineBreaksAndStraySurrogatesAreRefused(char c)
    {
        var entry = new TextEntry();
        entry.Insert("a");

        Assert.False(TextEntry.IsAccepted(c));
        Assert.False(entry.Insert(c));
        Assert.Equal("a", entry.Text);
    }

    [Fact]
    public void ASentenceWithEveryAccentCanBeTyped()
    {
        var entry = new TextEntry();
        const string Sentence = "¿Dónde está el árbol? ¡Mañana, por favor! Pingüino, qué sí.";

        Assert.Equal(Sentence.Length, entry.Insert(Sentence));
        Assert.Equal(Sentence, entry.Text);
    }

    [Fact]
    public void BackspaceRemovesTheCharacterBeforeTheCaretAndStopsAtTheStart()
    {
        var entry = new TextEntry();
        entry.Insert("añ");

        Assert.True(entry.Backspace());
        Assert.Equal("a", entry.Text);
        Assert.True(entry.Backspace());
        Assert.False(entry.Backspace());
        Assert.Equal(string.Empty, entry.Text);
        Assert.Equal(0, entry.Caret);
    }

    [Fact]
    public void TheCaretMovesAndEditsHappenAtTheCaret()
    {
        var entry = new TextEntry();
        entry.Insert("rbol");
        entry.Home();
        entry.Insert('á');
        Assert.Equal("árbol", entry.Text);
        Assert.Equal(1, entry.Caret);

        entry.End();
        entry.MoveLeft();
        entry.MoveLeft();
        entry.Insert('x');
        Assert.Equal("árbxol", entry.Text);

        Assert.True(entry.Delete());
        Assert.Equal("árbxl", entry.Text);
        entry.End();
        Assert.False(entry.Delete());
        entry.MoveRight();
        Assert.Equal(5, entry.Caret);
        entry.Home();
        entry.MoveLeft();
        Assert.Equal(0, entry.Caret);
    }

    [Fact]
    public void TheBoxStopsAtItsMaximumLength()
    {
        var entry = new TextEntry(5);

        Assert.Equal(5, entry.Insert("abcdefgh"));
        Assert.Equal("abcde", entry.Text);
        Assert.False(entry.Insert('f'));
        Assert.True(entry.Backspace());
        Assert.True(entry.Insert('z'));
        Assert.Equal("abcdz", entry.Text);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextEntry(0));
    }

    [Fact]
    public void TakeReturnsTheTrimmedLineAndEmptiesTheBox()
    {
        var entry = new TextEntry();
        entry.Insert("  ¿qué es esto?  ");
        Assert.False(entry.IsBlank);

        Assert.Equal("¿qué es esto?", entry.Take());
        Assert.Equal(string.Empty, entry.Text);
        Assert.Equal(0, entry.Caret);
        Assert.True(entry.IsBlank);
    }

    [Fact]
    public void ABoxOfSpacesIsBlank()
    {
        var entry = new TextEntry();
        entry.Insert("   ");

        Assert.True(entry.IsBlank);
        Assert.Equal(string.Empty, entry.Take());
    }

    [Theory]
    [InlineData('a', false, 'á')]
    [InlineData('e', false, 'é')]
    [InlineData('i', false, 'í')]
    [InlineData('o', false, 'ó')]
    [InlineData('u', false, 'ú')]
    [InlineData('n', false, 'ñ')]
    [InlineData('a', true, 'Á')]
    [InlineData('n', true, 'Ñ')]
    [InlineData('/', false, '¿')]
    [InlineData('?', true, '¿')]
    [InlineData('1', false, '¡')]
    [InlineData('!', true, '¡')]
    public void AltPlusAKeyTypesTheSpanishLetter(char key, bool shift, char expected)
    {
        Assert.Equal(expected, Spanish.Map(key, shift));
    }

    [Theory]
    [InlineData('b')]
    [InlineData('2')]
    [InlineData(' ')]
    public void OtherKeysHaveNoShortcut(char key)
    {
        Assert.Null(Spanish.Map(key, false));
    }

    [Fact]
    public void EveryLetterTheShortcutsMakeIsAcceptedByTheBox()
    {
        foreach (var key in "aeioun/1")
        {
            foreach (var shift in new[] { false, true })
            {
                var typed = Spanish.Map(key, shift);
                Assert.NotNull(typed);
                Assert.True(TextEntry.IsAccepted(typed.Value));
            }
        }
    }
}
