using Yogurt.Json;
using Yogurt.JsonRpc;

namespace Yogurt.Tests;

public class JsonRpcRequestTests
{
    [Test]
    public void RequestNoParams()
    {
        var json = JsonValue.Parse(
            """
            { "jsonrpc": "2.0"
            , "method": "no-params"
            , "id": 0 }
            """
        );

        var sut = JsonRpcRequest.Parse(json);

        Assert.That(
            sut,
            Is.EqualTo(new JsonRpcRequest {
                Id     = JsonRpcId.Int(0),
                Method = "no-params",
                Params = null,
            })
        );
    }

    [Test]
    public void RequestNullId()
    {
        var json = JsonValue.Parse(
            """
            { "jsonrpc": "2.0"
            , "method": "null-id"
            , "id": null }
            """
        );

        var sut = JsonRpcRequest.Parse(json);

        Assert.That(
            sut,
            Is.EqualTo(new JsonRpcRequest {
                Id     = JsonRpcId.Null,
                Method = "null-id",
                Params = null,
            })
        );
    }

    [Test]
    public void RequestStringId()
    {
        var json = JsonValue.Parse(
            """
            { "jsonrpc": "2.0"
            , "method": "string-id"
            , "id": "🆔" }
            """
        );

        var sut = JsonRpcRequest.Parse(json);

        Assert.That(
            sut,
            Is.EqualTo(new JsonRpcRequest {
                Id     = JsonRpcId.String("🆔"),
                Method = "string-id",
                Params = null,
            })
        );
    }

    [Test]
    public void RequestNotification()
    {
        var json = JsonValue.Parse(
            """
            { "jsonrpc": "2.0"
            , "method": "notification" }
            """
        );

        var sut = JsonRpcRequest.Parse(json);

        Assert.That(
            sut,
            Is.EqualTo(new JsonRpcRequest {
                Id     = null,
                Method = "notification",
                Params = null,
            })
        );
    }

    [Test]
    public void RequestWithParams()
    {
        var json = JsonValue.Parse(
            """
            { "jsonrpc": "2.0"
            , "method": "with-params"
            , "id": 123
            , "params": [1, 2, 3] }
            """
        );

        var sut = JsonRpcRequest.Parse(json);

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Id,     Is.EqualTo(JsonRpcId.Int(123)));
            Assert.That(sut.Method, Is.EqualTo("with-params"));
            Assert.That(
                sut.Params?.TryArray(static json => json.TryNumber<int>()),
                Is.EqualTo([1, 2, 3])
            );
        }
    }

    [Test]
    public void RequestWithInvalidParams()
    {
        var json = JsonValue.Parse(
            """
            { "jsonrpc": "2.0"
            , "method": "invalid-params"
            , "params": 0 }
            """
        );

        var sut = () => JsonRpcRequest.Parse(json);

        Assert.That(sut, Throws.TypeOf<JsonValueException>());
    }

    [Test]
    public void RequestWithInvalidVersion()
    {
        var json = JsonValue.Parse(
            """
            { "jsonrpc": "1.0"
            , "method": "invalid-version" }
            """
        );

        var sut = () => JsonRpcRequest.Parse(json);

        Assert.That(sut, Throws.TypeOf<JsonValueException>());
    }
}
