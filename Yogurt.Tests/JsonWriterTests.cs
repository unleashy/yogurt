using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Yogurt.Json;

namespace Yogurt.Tests;

public class JsonWriterTests
{
    private static (JsonWriter, Utf8BufferWriter) Arrange()
    {
        var buffer = new Utf8BufferWriter();
        var sut = new JsonWriter(buffer);
        return (sut, buffer);
    }

    [Test]
    public void Null()
    {
        var (sut, buffer) = Arrange();
        sut.Null();
        Assert.That(buffer.WrittenString, Is.EqualTo("null"));
    }

    [TestCase(true, "true")]
    [TestCase(false, "false")]
    public void Boolean(bool value, string result)
    {
        var (sut, buffer) = Arrange();
        sut.Boolean(value);
        Assert.That(buffer.WrittenString, Is.EqualTo(result));
    }

    [TestCase(0, "0")]
    [TestCase(123, "123")]
    [TestCase(double.E, "2.718281828459045")]
    [TestCase(double.Epsilon, "5E-324")]
    [TestCase(double.NegativeZero, "-0")]
    public void Number(double value, string result)
    {
        var (sut, buffer) = Arrange();
        sut.Number(value);
        Assert.That(buffer.WrittenString, Is.EqualTo(result));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void NumberInvalid(double value)
    {
        var (sut, _) = Arrange();

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
    [TestCase("💀😎", @"""\uD83D\uDC80\uD83D\uDE0E""")]
    public void String(string value, string result)
    {
        var (sut, buffer) = Arrange();
        sut.String(value);
        Assert.That(buffer.WrittenString, Is.EqualTo(result));
    }

    [Test]
    [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
    public void Array()
    {
        var (sut, buffer) = Arrange();

        sut.Array(it => {
            it.Item(it => it.Number(42));
            it.Item(it => it.Array(it => {
                it.Item(it => it.String("nested"));
            }));
            it.Item(it => it.Boolean(true));
            it.Item(it => it.Array(_ => {}));
        });

        Assert.That(buffer.WrittenString, Is.EqualTo("""[42,["nested"],true,[]]"""));
    }

    [Test]
    [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
    public void Object()
    {
        var (sut, buffer) = Arrange();

        sut.Object(it => {
            it.Member("foo", it => it.String("bar"));
            it.Member("data", it => it.Object(it => {
                it.Member("code", it => it.Number(-32_768));
                it.Member("stuff", it => it.Array(it => it.Item(it => it.Null())));
            }));
            it.Member("は", it => it.Object(_ => {}));
        });

        Assert.That(buffer.WrittenString,
            Is.EqualTo(
                """{"foo":"bar","data":{"code":-32768,"stuff":[null]},"は":{}}"""
            )
        );
    }
}

internal sealed class Utf8BufferWriter : IBufferWriter<byte>
{
    private readonly ArrayBufferWriter<byte> _array = new();
    private readonly StringBuilder _str = new();

    public string WrittenString => _str.ToString();

    public void Advance(int count)
    {
        var prevCount = _array.WrittenCount;
        _array.Advance(count);

        var utf8 = _array.WrittenSpan[prevCount .. _array.WrittenCount];
        _ = _str.Append(Encoding.UTF8.GetString(utf8));
    }

    public Memory<byte> GetMemory(int sizeHint = 0) => _array.GetMemory(sizeHint);

    public Span<byte> GetSpan(int sizeHint = 0) => _array.GetSpan(sizeHint);
}
