using System.Globalization;
using System.Numerics;
using System.Reflection;
using Yogurt.Json;

namespace Yogurt.Tests.Json;

public class ValueTests
{
    [Test]
    public void Empty()
    {
        var sut = () => JsonValue.Parse("");

        Assert.That(
            sut,
            Throws
                .TypeOf<JsonParseException>().And
                .Message.EqualTo("Unexpected end of input: no JSON value found (1:1)")
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
        "Unterminated string (1:1)"
    )]
    [TestCase(
        """
        "bad\escape"
        """,
        @"Invalid escape sequence '\e' in string (1:6)"
    )]
    [TestCase(
        """
        "\
        """,
        "Unexpected end of input while parsing escape sequence (1:3)"
    )]
    [TestCase(
        """
        "\u123"
        """,
        "Invalid Unicode escape sequence: expected 4 hexadecimal digits; found 3 (1:7)"
    )]
    [TestCase(
        """
        "\u12G4"
        """,
        "Invalid Unicode escape sequence: expected 4 hexadecimal digits; found 2 (1:6)"
    )]
    [TestCase("\"\u001F\"", @"Unescaped control character '\x1F' in string (1:2)")]
    [TestCase(
        """
        "\uD800"
        """,
        @"Invalid Unicode escape sequence: expected high surrogate '\uD800' to be paired with a low surrogate (1:8)"
    )]
    [TestCase(
        """
        "\uDC00"
        """,
        @"Invalid Unicode escape sequence: unexpected low surrogate '\uDC00' not following a high surrogate (1:8)"
    )]
    [TestCase(
        """
        "\uD800\u0041"
        """,
        @"Invalid Unicode escape sequence: expected high surrogate '\uD800' to be followed by a low surrogate; found '\u0041' (1:14)"
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

    [TestCase("01", "Invalid number: leading zero is not allowed (1:2)")]
    [TestCase("-01", "Invalid number: leading zero is not allowed (1:3)")]
    [TestCase("-", "Invalid number: expected digit after negative sign '-' (1:2)")]
    [TestCase("1.", "Invalid number: expected digit after decimal point '.' (1:3)")]
    [TestCase(".1", "Invalid number: leading decimal point is not allowed (1:2)")]
    [TestCase("1e", "Invalid number: expected digit after exponent (1:3)")]
    [TestCase("1E", "Invalid number: expected digit after exponent (1:3)")]
    [TestCase("1e+", "Invalid number: expected digit after exponent (1:4)")]
    [TestCase("1e-", "Invalid number: expected digit after exponent (1:4)")]
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
            typeof(ValueTests)
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
        var e = sut.TryArray()!.Value;
        _ = e.MoveNext();
        result.Add(e.Current.TryLiteral(true));
        _ = e.MoveNext();
        result.Add(e.Current.TryLiteral(false));
        _ = e.MoveNext();
        result.Add(e.Current.TryLiteral(3.14));
        _ = e.MoveNext();
        result.Add(e.Current.TryLiteral("🤓☝️"));

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

        var result = sut.TryArray()?
            .Select(it => it.TryNumber<int>())
            .OfType<int>()
            .ToArray();

        Assert.That(result, Is.EqualTo(values));
    }

    [TestCase("[", "Unexpected end of input: unclosed array (1:2)")]
    [TestCase("[,1]", "Unexpected character ',' (1:2)")]
    [TestCase("[1, 2,]", "Trailing comma is not allowed (1:7)")]
    [TestCase("[1 2]", "Expected ',' or ']' after value in array; found '2' (1:4)")]
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

        var result =
            sut.TryObject()?
                .Select(m =>
                    m.Value.TryNumber<int>() is {} value
                        ? (m.Key, value)
                        : ((string, int)?)null
                )
                .OfType<(string, int)>()
                .ToArray();

        Assert.That(result, Is.EqualTo(entries));
    }

    [TestCase("{", "Unexpected end of input: unclosed object (1:2)")]
    [TestCase("""{"" """, "Unexpected end of input: unclosed object (1:5)")]
    [TestCase("""{"":1""", "Unexpected end of input: unclosed object (1:6)")]
    [TestCase("""{"":1,""", "Unexpected end of input: unclosed object (1:7)")]
    [TestCase("""{,"":1}""", "Unexpected character ',' (1:2)")]
    [TestCase("""{"":1,}""", "Trailing comma is not allowed (1:7)")]
    [TestCase("""{"":1 "":2}""", "Expected ',' or '}' after value in object; found '\"' (1:7)")]
    [TestCase("{1:2}", "Object keys must be strings; found '1' (1:2)")]
    [TestCase("""{"" 1}""", "Expected ':' after object key; found '1' (1:5)")]
    public void ObjectsInvalid(string input, string message)
    {
        var sut = () => JsonValue.Parse(input);

        Assert.That(sut,
            Throws
                .TypeOf<JsonParseException>().And
                .Message.EqualTo(message)
        );
    }

    [Test]
    public void ObjectsWithReader()
    {
        var sut = JsonValue.Parse("""{ "foo": 123, "bar": 456, "bux": "no" }""");
        var reader = new FakeJsonObjectReader();

        var result = sut.TryObject(new Dictionary<string, int>(), reader);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(new Dictionary<string, int> {
                ["foo"] = 123,
                ["bar"] = 456,
            }));

            Assert.That(reader.ActualKeysFound, Is.EquivalentTo(reader.ReceivedFoundKeys));
            Assert.That(
                reader.ReceivedRejectedKeys,
                Is.EquivalentTo(new HashSet<string> { "bux" })
            );
        }
    }

    [Test]
    public void ObjectsWithShapeReader()
    {
        var sut = JsonValue.Parse("""{ "foo": 123, "bar": "of gold" }""");
        var shape = new JsonObjectShape<(int Foo, string? Bar, int? Bux)>()
            .Require("foo", (in json, tuple) => (json.Number<int>(), tuple.Bar, tuple.Bux))
            .Allow("bar", (in json, tuple) => (tuple.Foo, json.String(), tuple.Bux))
            .Allow("bux", (in json, tuple) => (tuple.Foo, tuple.Bar, json.Number<int>()));

        var result = sut.TryObject((Foo: -1, Bar: null, Bux: null), shape);

        Assert.That(result, Is.EqualTo((Foo: 123, Bar: "of gold", Bux: (int?)null)));
    }

    [Test]
    public void ShapeReaderOneOf()
    {
        var sut = JsonValue.Parse("""{ "a": 1, "b": 2 }""");
        var shape = new JsonObjectShape<int>()
            .Require("a", (in json, x) => x + json.Number<int>())
            .RequireOneOf(
                "b", (in json, x) => x * json.Number<int>(),
                "c", (in _, _) => -99
            );

        var result = sut.TryObject(42, shape);

        Assert.That(result, Is.EqualTo((1 + 42) * 2));
    }

    [Test]
    public void ShapeReaderOneOfConflict()
    {
        var sut = JsonValue.Parse("""{ "a": 1, "b": 2, "c": 2 }""");
        var shape = new JsonObjectShape<int>()
            .Require("a", (in json, x) => x + json.Number<int>())
            .RequireOneOf(
                "b", (in json, x) => x * json.Number<int>(),
                "c", (in _, _) => -99
            );

        var result = () => sut.Object(42, shape);

        Assert.That(result,
            Throws
                .TypeOf<JsonValueException>().And
                .Message.EqualTo("$.c: Invalid key \"c\" as it conflicts with \"b\" (at 1:24)"));
    }

    [TestCase("1 2", "Unexpected trailing content after JSON value: '2' (1:3)")]
    [TestCase("[][]", "Unexpected trailing content after JSON value: '[]' (1:3)")]
    [TestCase("{}{}", "Unexpected trailing content after JSON value: '{}' (1:3)")]
    public void TrailingData(string input, string message)
    {
        var sut = () => JsonValue.Parse(input);

        Assert.That(sut,
            Throws
                .TypeOf<JsonParseException>().And
                .Message.EqualTo(message)
        );
    }

    [TestCase("", 1, 1)]
    [TestCase("{", 1, 2)]
    [TestCase("{,}", 1, 2)]
    [TestCase("{\n  \"a\": 1,\n}", 3, 1)]
    [TestCase("{\r\n  \"a\": 1,\r\n}", 3, 1)]
    [TestCase("\n\n\n{bad}", 4, 2)]
    [TestCase("""{"a": "unterminated}""", 1, 7)]
    [TestCase("\"first line\nsecond line\"", 1, 12)]
    [TestCase("\"a\\qb\"", 1, 4)]
    [TestCase("\n[\"🩻\", Utf8]", 2, 10)]
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

        var values = sut.TryArray()?.ToArray() ?? [];

        using (Assert.EnterMultipleScope()) {
            Assert.That(values[0].TryNull(), Is.True);
            Assert.That(values[1].TryBoolean(), Is.True);
            Assert.That(values[2].TryNumber(), Is.EqualTo("42"));
            Assert.That(values[3].TryString(), Is.EqualTo("abc"));
            Assert.That(values[4].TryArray(), Is.Not.Null);
            Assert.That(values[5].TryObject(), Is.Not.Null);
        }
    }
}

internal sealed class FakeJsonObjectReader : IJsonObjectReader<Dictionary<string, int>>
{
    public HashSet<string> ActualKeysFound { get; } = new();
    public IReadOnlySet<string> ReceivedFoundKeys { get; private set; } = new HashSet<string>();
    public IReadOnlySet<string> ReceivedRejectedKeys { get; set; } = new HashSet<string>();

    public bool TryRead(
        string key,
        in JsonValue value,
        JsonObjectReaderKeys keys,
        scoped ref Dictionary<string, int> state
    )
    {
        _ = ActualKeysFound.Add(key);

        if (value.TryNumber<int>() is {} n) {
            state[key] = n;
            return true;
        }
        else {
            return false;
        }
    }

    public bool Complete(
        in JsonValue objectValue,
        JsonObjectReaderKeys keys,
        scoped ref Dictionary<string, int> state
    )
    {
        ReceivedFoundKeys = keys.Found;
        ReceivedRejectedKeys = keys.Rejected;
        return true;
    }
}
