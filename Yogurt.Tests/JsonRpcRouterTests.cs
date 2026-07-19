using Yogurt.Json;
using Yogurt.JsonRpc;

namespace Yogurt.Tests;

public class JsonRpcRouterTests
{
    [Test]
    public async Task Invoke_OnRealMethod_ReturnsResponse()
    {
        var method = new FakeMethod();
        var router = new JsonRpcRouter.Builder() { ["method"] = method }.Build();
        var param = JsonValue.Parse("""["🐇"]""");

        var result = await router.InvokeAsync("method", param);

        Assert.That(result.ToValue, Is.EqualTo(param));
    }

    [Test]
    public async Task Invoke_OnInvalidMethod_ReturnsNotFound()
    {
        var method = new FakeMethod();
        var router = new JsonRpcRouter.Builder() { ["method"] = method }.Build();

        var result = await router.InvokeAsync("foobar", null);

        Assert.That(result.ToError, Is.EqualTo(
            new JsonRpcError(JsonRpcErrorCodes.MethodNotFound, "Method not found")
        ));
    }

    [Test]
    public async Task Notify_OnRealMethod_Returns()
    {
        var method = new FakeMethod();
        var router = new JsonRpcRouter.Builder() { ["method"] = method }.Build();
        var param = JsonValue.Parse("""{ "ok": true }""");

        await router.NotifyAsync("method", param);

        Assert.That(method.Notifications, Is.EqualTo([param]));
    }

    [Test]
    public async Task Notify_OnInvalidMethod_Returns()
    {
        var method = new FakeMethod();
        var router = new JsonRpcRouter.Builder() { ["method"] = method }.Build();
        var param = JsonValue.Parse("""{ "ok": true }""");

        await router.NotifyAsync("nope", param);

        Assert.That(method.Notifications, Is.Empty);
    }
}

internal sealed class FakeMethod : IJsonRpcMethod
{
    private readonly List<JsonValue?> _notifications = new();

    public ValueTask<JsonRpcMethodResult> InvokeAsync(
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested) {
            return ValueTask.FromCanceled<JsonRpcMethodResult>(cancellationToken);
        }

        var result = parameters is {} @params
            ? JsonRpcMethodResult.Ok(@params)
            : JsonRpcMethodResult.Error(
                new JsonRpcError(JsonRpcErrorCodes.InvalidParams, "No parameters given")
            );

        return ValueTask.FromResult(result);
    }

    public ValueTask NotifyAsync(
        in JsonValue? parameters,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested) {
            return ValueTask.FromCanceled(cancellationToken);
        }

        _notifications.Add(parameters);
        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<JsonValue?> Notifications => _notifications;
}
