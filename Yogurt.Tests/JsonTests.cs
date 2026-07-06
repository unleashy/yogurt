using System.Globalization;
using System.Text.Json;
using Yogurt.Json;

namespace Yogurt.Tests;

public class JsonTests
{
    [Test]
    public void Empty()
    {
        Assert.That(
            () => new JsonParser(""),
            Throws
                .TypeOf<JsonException>().And
                .Message.EqualTo("Unexpected empty JSON document")
        );
    }

    [Test]
    public void Null()
    {
        var sut = new JsonParser("null");

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Null(), Is.True);
            Assert.That(sut.IsAtEnd, Is.True);
        }
    }

    [TestCase("true", true)]
    [TestCase("false", false)]
    public void Booleans(string input, bool expected)
    {
        var sut = new JsonParser(input);

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Boolean(), Is.EqualTo(expected));
            Assert.That(sut.IsAtEnd, Is.True);
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
        var sut = new JsonParser($"\"{raw}\"");

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.String(), Is.EqualTo(interpreted));
            Assert.That(sut.IsAtEnd, Is.True);
        }
    }

    [TestCase(
        """
        "
        """,
        "Unclosed string literal"
)]
    [TestCase(
        """
        "bad\escape"
        """,
        @"Invalid escape sequence '\e'"
    )]
    [TestCase(
        """
        "\
        """,
        "Unexpected end of input"
    )]
    [TestCase(
        """
        "\u123"
        """,
        "Invalid Unicode escape sequence; expected 4 hexadecimal digits"
    )]
    [TestCase(
        """
        "\u12G4"
        """,
        "Invalid Unicode escape sequence; expected 4 hexadecimal digits"
    )]
    [TestCase("\"\u001F\"", "Unescaped control character in string literal")]
    [TestCase(
        """
        "\uD800"
        """,
        "Lone surrogate in string literal"
    )]
    [TestCase(
        """
        "\uDC00"
        """,
        "Lone surrogate in string literal"
    )]
    [TestCase(
        """
        "\uD800\u0041"
        """,
        "Malformed surrogate pair in string literal"
    )]
    public void StringsInvalid(string raw, string message)
    {
        var sut = new JsonParser(raw);

        Assert.That(sut.String,
            Throws
                .TypeOf<JsonException>().And
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
        var sut = new JsonParser(input);

        var result = sut.Number();

        Assert.That(result, Is.EqualTo(input));
    }

    [TestCase("01", "Invalid leading zero in number literal")]
    [TestCase("-01", "Invalid leading zero in number literal")]
    [TestCase("-", "Expected a number after '-'")]
    [TestCase("1.", "Expected digits for fractional part")]
    [TestCase("1e", "Expected digits for exponent part")]
    [TestCase("1E", "Expected digits for exponent part")]
    [TestCase("1e+", "Expected digits for exponent part")]
    [TestCase("1e-", "Expected digits for exponent part")]
    public void NumbersInvalid(string input, string message)
    {
        var sut = new JsonParser(input);

        Assert.That(sut.Number,
            Throws
                .TypeOf<JsonException>().And
                .Message.EqualTo(message)
        );
    }

    [TestCase("[]", new int[0])]
    [TestCase("[123]", new[] { 123 })]
    [TestCase("[1, 2, 3]", new[] { 1, 2, 3 })]
    public void Arrays(string input, int[] values)
    {
        var sut = new JsonParser(input);

        var result = new List<int>();
        if (sut.Array()) {
            while (sut.ArrayElement()) {
                if (sut.Number<int>() is {} n) {
                    result.Add(n);
                }
            }
        }

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(values));
            Assert.That(sut.IsAtEnd, Is.True);
        }
    }

    [TestCase("{}", new string[0], new int[0])]
    [TestCase("""{ "": 0 }""", new[] { "" }, new[] { 0 })]
    [TestCase("""{ "foo": 123, "bar": 456 }""", new[] { "foo", "bar" }, new[] { 123, 456 })]
    [TestCase("""{ "x": -1, "x": -2 }""", new[] { "x", "x" }, new[] { -1, -2 })]
    public void Objects(string input, string[] keys, int[] values)
    {
        var entries = keys.Zip(values).ToArray();
        var sut = new JsonParser(input);

        var result = new List<(string, int)>();
        if (sut.Object()) {
            while (sut.ObjectKey() is {} key) {
                if (sut.Number<int>() is {} value) {
                    result.Add((key, value));
                }
            }
        }

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(entries));
            Assert.That(sut.IsAtEnd, Is.True);
        }
    }

    [Test]
    public void Complex()
    {
        var sut = new JsonParser(
            """
            {
              "Date": "2019-08-01T00:00:00-07:00",
              "TemperatureCelsius": 25,
              "Summary": "Hot",
              "DatesAvailable": [
                "2019-08-01T00:00:00-07:00",
                "2019-08-02T00:00:00-07:00"
              ],
              "TemperatureRanges": {
                "Cold": {
                  "High": 20,
                  "Low": -10
                },
                "Hot": {
                  "High": 60,
                  "Low": 20
                }
              },
              "SummaryWords": [
                "Cool",
                "Windy",
                "Humid"
              ]
            }
            """
        );

        var forecast = new WeatherForecast();

        sut.ExpectObject(
            Expect.Require("Date", p => forecast.Date = ExpectDate(p)),
            Expect.Require("TemperatureCelsius", p =>
                forecast.TemperatureCelsius = p.ExpectNumber<int>()
            ),
            Expect.Allow("Summary", p =>
                forecast.Summary = p.ExpectString()
            ),
            Expect.Allow("SummaryField", p =>
                forecast.SummaryField = p.ExpectString()
            ),
            Expect.Allow("DatesAvailable", p =>
                forecast.DatesAvailable = p.ExpectArray(ExpectDate).ToList()
            ),
            Expect.Allow("TemperatureRanges", p =>
                forecast.TemperatureRanges = p.ExpectObjectDictionary(p => {
                    var ranges = new HighLowTemps();

                    p.ExpectObject(
                        Expect.Require("High", p => ranges.High = p.ExpectNumber<int>()),
                        Expect.Require("Low", p => ranges.Low = p.ExpectNumber<int>())
                    );

                    return ranges;
                })
            ),
            Expect.Allow("SummaryWords", p =>
                forecast.SummaryWords = p.ExpectArray(p => p.ExpectString()).ToArray()
            )
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(forecast.Date,
                Is.EqualTo(new DateTimeOffset(2019, 8, 1, 0, 0, 0, TimeSpan.FromHours(-7)))
            );
            Assert.That(forecast.TemperatureCelsius, Is.EqualTo(25));
            Assert.That(forecast.Summary, Is.EqualTo("Hot"));
            Assert.That(forecast.SummaryField, Is.Null);
            Assert.That(forecast.DatesAvailable, Is.EqualTo(new List<DateTimeOffset> {
                new (2019, 8, 1, 0, 0, 0, TimeSpan.FromHours(-7)),
                new (2019, 8, 2, 0, 0, 0, TimeSpan.FromHours(-7)),
            }));
            Assert.That(forecast.TemperatureRanges,
                Is.EqualTo(new Dictionary<string, HighLowTemps> {
                    ["Cold"] = new() { High = 20, Low = -10 },
                    ["Hot"] = new() { High = 60, Low = 20 },
                })
            );
            Assert.That(forecast.SummaryWords, Is.EqualTo(["Cool", "Windy", "Humid"]));
        }

        return;

        static DateTimeOffset ExpectDate(JsonParser p) =>
            DateTimeOffset.Parse(p.ExpectString(), CultureInfo.InvariantCulture);
    }
}

internal record struct WeatherForecast(
    DateTimeOffset Date,
    int TemperatureCelsius,
    string? Summary,
    string? SummaryField,
    IList<DateTimeOffset>? DatesAvailable,
    Dictionary<string, HighLowTemps>? TemperatureRanges,
    string[]? SummaryWords
);

internal record struct HighLowTemps(int High, int Low);
