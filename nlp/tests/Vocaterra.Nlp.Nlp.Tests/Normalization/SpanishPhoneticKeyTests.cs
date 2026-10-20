using Vocaterra.Nlp.Nlp.Normalization;

namespace Vocaterra.Nlp.Nlp.Tests.Normalization;

/// <summary>Tests for <see cref="SpanishPhoneticKey"/>.</summary>
public class SpanishPhoneticKeyTests
{
    [Theory]
    [InlineData("vaca", "baca")]
    [InlineData("llave", "yave")]
    [InlineData("cerveza", "serbesa")]
    [InlineData("hola", "ola")]
    [InlineData("zapato", "sapato")]
    [InlineData("casa", "caza")]
    [InlineData("queso", "keso")]
    [InlineData("kilo", "quilo")]
    [InlineData("jirafa", "girafa")]
    [InlineData("Canción", "cancion")]
    [InlineData("HOLA", "hola")]
    public void SoundAlikeSpellingsShareAKey(string a, string b)
    {
        Assert.Equal(SpanishPhoneticKey.Compute(a), SpanishPhoneticKey.Compute(b));
    }

    [Theory]
    [InlineData("espado", "espada")]
    [InlineData("perro", "pero")]
    [InlineData("gato", "pato")]
    [InlineData("año", "ano")]
    [InlineData("guerra", "gerra")]
    [InlineData("pingüino", "pinguino")]
    public void DifferentSoundsGetDifferentKeys(string a, string b)
    {
        Assert.NotEqual(SpanishPhoneticKey.Compute(a), SpanishPhoneticKey.Compute(b));
    }

    [Theory]
    [InlineData("cerveza", "serbesa")]
    [InlineData("llave", "yabe")]
    [InlineData("queso", "keso")]
    [InlineData("quiero", "kiero")]
    [InlineData("guitarra", "gitarra")]
    [InlineData("geranio", "xeranio")]
    [InlineData("jamón", "xamon")]
    [InlineData("pingüino", "pingwino")]
    [InlineData("ñandú", "nyandu")]
    [InlineData("chico", "ciko")]
    [InlineData("taxi", "taksi")]
    [InlineData("acción", "aksion")]
    [InlineData("cuatro", "kuatro")]
    [InlineData("hola", "ola")]
    [InlineData("AÑO", "anyo")]
    [InlineData("leer", "ler")]
    [InlineData("perro", "perro")]
    [InlineData("rrr", "rr")]
    [InlineData("22 espadas", "22 espadas")]
    [InlineData("11", "11")]
    [InlineData("vaca, baca!", "baka baka")]
    [InlineData("", "")]
    public void ComputesTheDocumentedKey(string word, string expected)
    {
        Assert.Equal(expected, SpanishPhoneticKey.Compute(word));
    }

    [Fact]
    public void ResultIsAlwaysLowercaseAscii()
    {
        const string text = "ÁÉÍÓÚ üñ Çedilla straße ХЛЕБ ¿Qué? ¡Hola! 123";

        var key = SpanishPhoneticKey.Compute(text);

        Assert.All(key, c => Assert.True(c is >= 'a' and <= 'z' or >= '0' and <= '9' or ' ', $"unexpected '{c}' in '{key}'"));
        Assert.Equal(key, SpanishPhoneticKey.Compute(text));
    }

    [Fact]
    public void DecomposedAndComposedInputGiveTheSameKey()
    {
        Assert.Equal(SpanishPhoneticKey.Compute("canción"), SpanishPhoneticKey.Compute("canción"));
        Assert.Equal(SpanishPhoneticKey.Compute("pingüino"), SpanishPhoneticKey.Compute("pingüino"));
    }

    [Fact]
    public void ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => SpanishPhoneticKey.Compute(null!));
    }
}
