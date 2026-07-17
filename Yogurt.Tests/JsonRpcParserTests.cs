using System.Text;
using Yogurt.JsonRpc;

namespace Yogurt.Tests;

public class JsonRpcParserTests
{
    private static JsonRpcParser.Result Parse(string s) =>
        JsonRpcParser.Parse(Encoding.UTF8.GetBytes(s));

    [Test]
    public void Parse_MalformedJson_ReturnsError()
    {
        var sut = Parse("1.2.3");

        Assert.That(sut.Error,
            Is.EqualTo(
                new JsonRpcError(
                    JsonRpcErrorCodes.ParseError,
                    "Unexpected trailing content after JSON value: '.3' (1:4)"
                )
            )
        );
    }

    [TestCase("42")]
    [TestCase("""{ "jsonrpc": "1.0" }""")]
    [TestCase("""{ "jsonrpc": "2.0", "method": "", "params": 0 }""")]
    public void Parse_InvalidRequest_ReturnsError(string input)
    {
        var sut = Parse(input);

        Assert.That(sut.Error?.Code, Is.EqualTo((int)JsonRpcErrorCodes.InvalidRequest));
    }

    [TestCase("""{ "jsonrpc": "2.0", "method": "" }""")]
    [TestCase("""{ "jsonrpc": "2.0", "method": "a", "params": [], "id": 42 }""")]
    public void Parse_Request_ReturnsMessage(string input)
    {
        var sut = Parse(input);

        Assert.That(sut.Message?.Request, Is.Not.Null);
    }

    [TestCase("""{ "jsonrpc": "2.0", "result": true, "id": "id" }""")]
    [TestCase("""{ "jsonrpc": "2.0", "error": { "code": 123, "message": "error" }, "id": -32 }""")]
    public void Parse_Response_ReturnsMessage(string input)
    {
        var sut = Parse(input);

        Assert.That(sut.Message?.Response, Is.Not.Null);
    }
}
