using System.Globalization;
using System.Numerics;
using System.Reflection;
using Yogurt.Json;

namespace Yogurt.Tests;

public class JsonTests
{
    [Test]
    public void Empty()
    {
        var sut = () => JsonValue.Parse("");

        Assert.That(
            sut,
            Throws
                .TypeOf<JsonParseException>().And
                .Message.EqualTo("Unexpected end of input: no JSON value found")
        );
    }

    [Test]
    public void Null()
    {
        var sut = JsonValue.Parse("null");

        Assert.That(sut.TryNull(), Is.True);
    }

    [TestCase("true", true)]
    [TestCase("false", false)]
    public void Booleans(string input, bool expected)
    {
        var sut = JsonValue.Parse(input);

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.TryBoolean(), Is.EqualTo(expected));
        }
    }

    [TestCase("",  "")]
    [TestCase(" ", " ")]
    [TestCase("hello", "hello")]
    [TestCase(@"\""", "\"")]
    [TestCase(@"\""\\\/\b\f\n\r\t", "\"\\/\b\f\n\r\t")]
    [TestCase("a/b", "a/b")]
    [TestCase(@"\u0041\u00e9\u00E7\u0000", "Aéç\0")]
    [TestCase(@"Price: \u20AC100", "Price: €100")]
    [TestCase("🍞", "🍞")]
    [TestCase(@"bunny: \uD83D\uDC07!", "bunny: 🐇!")]
    public void Strings(string raw, string interpreted)
    {
        var sut = JsonValue.Parse($"\"{raw}\"");

        Assert.That(sut.TryString(), Is.EqualTo(interpreted));
    }

    [TestCase(
        """
        "
        """,
        "Unterminated string"
    )]
    [TestCase(
        """
        "bad\escape"
        """,
        @"Invalid escape sequence '\e' in string"
    )]
    [TestCase(
        """
        "\
        """,
        "Unexpected end of input while parsing escape sequence"
    )]
    [TestCase(
        """
        "\u123"
        """,
        "Invalid Unicode escape sequence: expected 4 hexadecimal digits; found 3"
    )]
    [TestCase(
        """
        "\u12G4"
        """,
        "Invalid Unicode escape sequence: expected 4 hexadecimal digits; found 2"
    )]
    [TestCase("\"\u001F\"", @"Unescaped control character '\x1F' in string")]
    [TestCase(
        """
        "\uD800"
        """,
        @"Invalid Unicode escape sequence: expected high surrogate '\uD800' to be paired with a low surrogate"
    )]
    [TestCase(
        """
        "\uDC00"
        """,
        @"Invalid Unicode escape sequence: unexpected low surrogate '\uDC00' not following a high surrogate"
    )]
    [TestCase(
        """
        "\uD800\u0041"
        """,
        @"Invalid Unicode escape sequence: expected high surrogate '\uD800' to be followed by a low surrogate; found '\u0041'"
    )]
    public void StringsInvalid(string raw, string message)
    {
        var sut = () => JsonValue.Parse(raw);

        Assert.That(sut,
            Throws
                .TypeOf<JsonParseException>().And
                .Message.EqualTo(message)
        );
    }

    [TestCase("0")]
    [TestCase("1")]
    [TestCase("-0")]
    [TestCase("1234567890987654321234567890")]
    [TestCase("-1234567890987654321234567890")]
    [TestCase("0.0")]
    [TestCase("3.14159265358979323846")]
    [TestCase("-123.45678")]
    [TestCase("1e10")]
    [TestCase("123E456")]
    [TestCase("678e+901")]
    [TestCase("678e-901")]
    [TestCase("1.5e3")]
    [TestCase("100000e0")]
    public void Numbers(string input)
    {
        var sut = JsonValue.Parse(input);

        var result = sut.TryNumber();

        Assert.That(result, Is.EqualTo(input));
    }

    [TestCase("01", "Invalid number: leading zero is not allowed")]
    [TestCase("-01", "Invalid number: leading zero is not allowed")]
    [TestCase("-", "Invalid number: expected digit after negative sign '-'")]
    [TestCase("1.", "Invalid number: expected digit after decimal point '.'")]
    [TestCase(".1", "Invalid number: leading decimal point is not allowed")]
    [TestCase("1e", "Invalid number: expected digit after exponent")]
    [TestCase("1E", "Invalid number: expected digit after exponent")]
    [TestCase("1e+", "Invalid number: expected digit after exponent")]
    [TestCase("1e-", "Invalid number: expected digit after exponent")]
    public void NumbersInvalid(string input, string message)
    {
        var sut = () => JsonValue.Parse(input);

        Assert.That(sut,
            Throws
                .TypeOf<JsonParseException>().And
                .Message.EqualTo(message)
        );
    }

    [TestCaseSource(nameof(NumbersTypedCases))]
    public void NumbersTyped<T>(T value)
        where T : struct, INumberBase<T>
    {
        var sut = JsonValue.Parse(value.ToString("G", CultureInfo.InvariantCulture));

        var result = sut.TryNumber<T>();

        Assert.That(result, Is.EqualTo(value));
    }

    private static IEnumerable<TestCaseData> NumbersTypedCases()
    {
        Type[] numericTypes = [
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
        ];

        var method =
            typeof(JsonTests)
                .GetMethod(
                    nameof(NumbersTypedSingleCases),
                    BindingFlags.NonPublic | BindingFlags.Static
                )!;

        foreach (var ty in numericTypes) {
            var e = (IEnumerable<TestCaseData>)method.MakeGenericMethod(ty).Invoke(null, null)!;
            foreach (var item in e) yield return item;
        }
    }

    private static IEnumerable<TestCaseData> NumbersTypedSingleCases<T>()
        where T : struct, IMinMaxValue<T>
    {
        yield return new TestCaseData(T.MaxValue) { TypeArgs = [typeof(T)] };
        yield return new TestCaseData(T.MinValue) { TypeArgs = [typeof(T)] };
    }

    [Test]
    public void Literals()
    {
        var sut = JsonValue.Parse("""[true, false, 3.14, "🤓☝️"]""");

        var result = new List<object?>();
        _ = sut.TryArray();
        _ = sut.TryArrayElement();
        result.Add(sut.TryLiteral(true));
        _ = sut.TryArrayElement();
        result.Add(sut.TryLiteral(false));
        _ = sut.TryArrayElement();
        result.Add(sut.TryLiteral(3.14));
        _ = sut.TryArrayElement();
        result.Add(sut.TryLiteral("🤓☝️"));

        Assert.That(result, Is.EqualTo(new object?[] { true, false, 3.14, "🤓☝️" }));
    }

    [Test]
    public void LiteralNoMatch()
    {
        var sut = JsonValue.Parse("\"string\"");

        var result = sut.TryLiteral("something");
        var s = sut.TryString();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.Null);
            Assert.That(s, Is.EqualTo("string"));
        }
    }

    [TestCase("[]", new int[0])]
    [TestCase("[123]", new[] { 123 })]
    [TestCase("[1, 2, 3]", new[] { 1, 2, 3 })]
    public void Arrays(string input, int[] values)
    {
        var sut = JsonValue.Parse(input);

        var result = new List<int>();
        if (sut.TryArray()) {
            while (sut.TryArrayElement()) {
                if (sut.TryNumber<int>() is {} n) {
                    result.Add(n);
                }
            }
        }

        Assert.That(result, Is.EqualTo(values));
    }

    [TestCase("[", "Unexpected end of input: unclosed array")]
    [TestCase("[,1]", "Unexpected character ','")]
    [TestCase("[1, 2,]", "Trailing comma is not allowed")]
    [TestCase("[1 2]", "Expected ',' or ']' after value in array; found '2'")]
    public void ArraysInvalid(string input, string message)
    {
        var sut = () => JsonValue.Parse(input);

        Assert.That(sut,
            Throws
                .TypeOf<JsonParseException>().And
                .Message.EqualTo(message)
        );
    }

    [TestCase("{}", new string[0], new int[0])]
    [TestCase("""{ "": 0 }""", new[] { "" }, new[] { 0 })]
    [TestCase("""{ "foo": 123, "bar": 456 }""", new[] { "foo", "bar" }, new[] { 123, 456 })]
    [TestCase("""{ "x": -1, "x": -2 }""", new[] { "x", "x" }, new[] { -1, -2 })]
    public void Objects(string input, string[] keys, int[] values)
    {
        var entries = keys.Zip(values).ToArray();
        var sut = JsonValue.Parse(input);

        var result = new List<(string, int)>();
        if (sut.TryObject()) {
            while (sut.TryObjectKey() is {} key) {
                if (sut.TryNumber<int>() is {} value) {
                    result.Add((key, value));
                }
            }
        }

        Assert.That(result, Is.EqualTo(entries));
    }

    [TestCase("{", "Unexpected end of input: unclosed object")]
    [TestCase("""{"" """, "Unexpected end of input: unclosed object")]
    [TestCase("""{"":1""", "Unexpected end of input: unclosed object")]
    [TestCase("""{"":1,""", "Unexpected end of input: unclosed object")]
    [TestCase("""{,"":1}""", "Unexpected character ','")]
    [TestCase("""{"":1,}""", "Trailing comma is not allowed")]
    [TestCase("""{"":1 "":2}""", "Expected ',' or '}' after value in object; found '\"'")]
    [TestCase("{1:2}", "Object keys must be strings; found '1'")]
    [TestCase("""{"" 1}""", "Expected ':' after object key; found '1'")]
    public void ObjectsInvalid(string input, string message)
    {
        var sut = () => JsonValue.Parse(input);

        Assert.That(sut,
            Throws
                .TypeOf<JsonParseException>().And
                .Message.EqualTo(message)
        );
    }

    [TestCase("1 2", "Unexpected trailing content after JSON value: '2'")]
    [TestCase("[][]", "Unexpected trailing content after JSON value: '[]'")]
    [TestCase("{}{}", "Unexpected trailing content after JSON value: '{}'")]
    public void TrailingData(string input, string message)
    {
        var sut = () => JsonValue.Parse(input);

        Assert.That(sut,
            Throws
                .TypeOf<JsonParseException>().And
                .Message.EqualTo(message)
        );
    }

    [TestCase("", 0, 0)]
    [TestCase("{", 0, 1)]
    [TestCase("{,}", 0, 1)]
    [TestCase("{\n  \"a\": 1,\n}", 2, 0)]
    [TestCase("{\r\n  \"a\": 1,\r\n}", 2, 0)]
    [TestCase("\n\n\n{bad}", 3, 1)]
    [TestCase("""{"a": "unterminated}""", 0, 6)]
    [TestCase("\"first line\nsecond line\"", 0, 11)]
    [TestCase("\"a\\qb\"", 0, 3)]
    [TestCase("\n[\"🩻\", Utf8]", 1, 9)]
    public void LineColumnTracking(string input, int line, int column)
    {
        Action sut = () => JsonValue.Parse(input);

        using (Assert.EnterMultipleScope()) {
            var jpe = Assert.Throws<JsonParseException>(sut)!;

            Assert.That(jpe.Line, Is.EqualTo(line));
            Assert.That(jpe.Column, Is.EqualTo(column));
        }
    }

    [Test]
    public void ValueExtraction()
    {
        var sut = JsonValue.Parse(
            """
            [ null
            , true
            , 42
            , "abc"
            , [1, [2], [3]]
            , { "key": "value"
              , "nest": { "is": { "ok": true } } } ]
            """);

        var values = new List<JsonValue>();
        if (sut.TryArray()) {
            while (sut.TryArrayElement()) {
                if (sut.TryValue() is {} value) values.Add(value);
            }
        }

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.TryValue(), Is.Null);

            Assert.That(values[0].TryNull(), Is.True);
            Assert.That(values[1].TryBoolean(), Is.True);
            Assert.That(values[2].TryNumber(), Is.EqualTo("42"));
            Assert.That(values[3].TryString(), Is.EqualTo("abc"));
            Assert.That(values[4].TryArray(), Is.True);
            Assert.That(values[5].TryObject(), Is.True);
        }
    }
}
