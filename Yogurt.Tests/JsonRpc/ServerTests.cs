using Yogurt.Json;
using Yogurt.JsonRpc;

namespace Yogurt.Tests.JsonRpc;

public class ServerTests
{
    [Test]
    public async Task OnRequest_WithRequest_CallsInvoke()
    {
        var subject = new FakeSubject();
        var sut = new JsonRpcServer(new FakeChannel(), subject);
        var request = new JsonRpcRequest {
            Id = JsonRpcId.Int(1),
            Method = "foobar",
            Params = JsonValue.Parse("[]"),
        };

        var handled = await sut.OnRequestAsync(request);

        using (Assert.EnterMultipleScope()) {
            Assert.That(handled, Is.True);
            Assert.That(subject.Invocations, Is.EqualTo([
                new FakeSubject.Invocation(IsNotification: false, request.Method, request.Params),
            ]));
        }
    }

    [Test]
    public async Task OnRequest_WithNotification_CallsNotify()
    {
        var subject = new FakeSubject();
        var sut = new JsonRpcServer(new FakeChannel(), subject);
        var request = new JsonRpcRequest {
            Method = "42",
            Params = JsonValue.Parse("[]"),
        };

        var handled = await sut.OnRequestAsync(request);

        using (Assert.EnterMultipleScope()) {
            Assert.That(handled, Is.True);
            Assert.That(subject.Invocations, Is.EqualTo([
                new FakeSubject.Invocation(IsNotification: true, request.Method, request.Params),
            ]));
        }
    }

    [Test]
    public async Task OnRequest_WithRequest_WritesResponse()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcServer(channel, new FakeSubject());
        var request = new JsonRpcRequest {
            Id = JsonRpcId.Int(1),
            Method = "echo !!!",
            Params = JsonValue.Parse("[]"),
        };

        _ = await sut.OnRequestAsync(request);

        var message = await channel.OutputReader.ReadAsync();
        Assert.That(message, Is.EqualTo(
            JsonRpcMessage.Response(
                JsonRpcResponse.Result(request.Id.Value, request.Params.Value)
            )
        ));
    }

    [Test]
    public async Task OnRequest_WithNotification_HasNoResponse()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcServer(channel, new FakeSubject());
        var request = new JsonRpcRequest {
            Method = "echo !!!",
            Params = JsonValue.Parse("[]"),
        };

        _ = await sut.OnRequestAsync(request);

        Assert.That(channel.OutputReader.Count, Is.Zero);
    }

    [Test]
    public async Task OnRequest_WithRequestError_WritesErrorResponse()
    {
        var channel = new FakeChannel();
        var sut = new JsonRpcServer(channel, new FakeSubject());
        var request = new JsonRpcRequest {
            Id = JsonRpcId.Int(1),
            Method = "epic-fail",
        };

        _ = await sut.OnRequestAsync(request);

        var message = await channel.OutputReader.ReadAsync();
        Assert.That(message, Is.EqualTo(
            JsonRpcMessage.Response(
                JsonRpcResponse.Error(
                    request.Id.Value,
                    new JsonRpcError(JsonRpcErrorCodes.InvalidParams, "No parameters given")
                )
            )
        ));
    }

    [Test]
    public async Task OnResponse_Rejects()
    {
        var channel = new FakeChannel();
        var subject = new FakeSubject();
        var sut = new JsonRpcServer(channel, subject);

        var handled = await ((IJsonRpcObserver)sut).OnResponseAsync(default);

        using (Assert.EnterMultipleScope()) {
            Assert.That(handled, Is.False);
            Assert.That(subject.Invocations, Is.Empty);
            Assert.That(channel.OutputReader.Count, Is.Zero);
        }
    }
}

internal sealed class FakeSubject : IJsonRpcSubject
{
    public readonly record struct Invocation(bool IsNotification, string Method, JsonValue? Params);

    private readonly List<Invocation> _invocations = new();
    public IReadOnlyList<Invocation> Invocations => _invocations;

    public ValueTask<JsonRpcMethodResult> InvokeAsync(
        string method,
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    )
    {
        _invocations.Add(new Invocation(IsNotification: false, method, parameters));

        var result = parameters is {} @params
            ? JsonRpcMethodResult.Ok(@params)
            : JsonRpcMethodResult.Error(
                new JsonRpcError(JsonRpcErrorCodes.InvalidParams, "No parameters given")
            );

        return ValueTask.FromResult(result);
    }

    public ValueTask NotifyAsync(
        string method,
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    )
    {
        _invocations.Add(new Invocation(IsNotification: true, method, parameters));
        return ValueTask.CompletedTask;
    }
}
