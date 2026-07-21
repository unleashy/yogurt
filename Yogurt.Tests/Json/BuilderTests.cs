using System.Diagnostics.CodeAnalysis;
using Yogurt.Json;

namespace Yogurt.Tests.Json;

public class BuilderTests
{
    [Test]
    public void Null()
    {
        var sut = new JsonBuilder();

        sut.Null();

        using (Assert.EnterMultipleScope()) {
            var json = sut.Build();
            Assert.That(json.TryNull(), Is.True);
            Assert.That(json.ToString(), Is.EqualTo("null"));
        }
    }

    [TestCase(true, "true")]
    [TestCase(false, "false")]
    public void Boolean(bool value, string result)
    {
        var sut = new JsonBuilder();

        sut.Boolean(value);

        var json = sut.Build();
        Assert.That(json.TryBoolean(), Is.EqualTo(value));
    }

    [TestCase(0, "0")]
    [TestCase(123, "123")]
    [TestCase(double.E, "2.718281828459045")]
    [TestCase(double.Epsilon, "5E-324")]
    [TestCase(double.NegativeZero, "-0")]
    public void Number(double value, string result)
    {
        var sut = new JsonBuilder();

        sut.Number(value);

        using (Assert.EnterMultipleScope()) {
            var json = sut.Build();
            Assert.That(json.Number<double>(), Is.EqualTo(value));
            Assert.That(json.ToString(), Is.EqualTo(result));
        }
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void NumberInvalid(double value)
    {
        var sut = new JsonBuilder();

        var f = () => sut.Number(value);

        Assert.That(f,
            Throws
                .TypeOf<ArgumentOutOfRangeException>().And
                .Message.Contains("Number cannot be represented in JSON").And
                .Property("ParamName").EqualTo("value").And
                .Property("ActualValue").EqualTo(value)
        );
    }

    [TestCase("", "\"\"")]
    [TestCase("foobar", @"""foobar""")]
    [TestCase("foo\\bar", @"""foo\\bar""")]
    [TestCase("<&+/\r\"\n'", @"""<&+/\r\""\n'""")]
    [TestCase("大人になる", @"""大人になる""")]
    [TestCase("💀😎", @"""💀😎""")]
    [TestCase("\0\x01", @"""\u0000\u0001""")]
    public void String(string value, string result)
    {
        var sut = new JsonBuilder();

        sut.String(value);

        using (Assert.EnterMultipleScope()) {
            var json = sut.Build();
            Assert.That(json.String(), Is.EqualTo(value));
            Assert.That(json.ToString(), Is.EqualTo(result));
        }
    }

    [Test]
    [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
    [SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments")]
    public void Array()
    {
        var sut = new JsonBuilder();

        sut.Array(it => {
            it.Item(it => it.Number(42));
            it.Item(it => it.Array(it => {
                it.Item(it => it.String("nested"));
            }));
            it.Item(it => it.Boolean(true));
            it.Item(it => it.Array(_ => {}));
        });

        using (Assert.EnterMultipleScope()) {
            var json = sut.Build();
            Assert.That(json.Array().Count(), Is.EqualTo(4));
            Assert.That(json.ToString(), Is.EqualTo("""[42,["nested"],true,[]]"""));
        }
    }

    [Test]
    [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
    public void Object()
    {
        var sut = new JsonBuilder();

        sut.Object(it => {
            it.Member("foo", it => it.String("bar"));
            it.Member("data", it => it.Object(it => {
                it.Member("code", it => it.Number(-32_768));
                it.Member("stuff", it => it.Array(it => it.Item(it => it.Null())));
            }));
            it.Member("は", it => it.Object(_ => {}));
        });

        using (Assert.EnterMultipleScope()) {
            var json = sut.Build();
            Assert.That(json.Object().Count(), Is.EqualTo(3));
            Assert.That(json.ToString(),
                Is.EqualTo("""{"foo":"bar","data":{"code":-32768,"stuff":[null]},"は":{}}""")
            );
        }
    }
}
