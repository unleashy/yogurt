using System.Text.Json;

namespace Yogurt.Json;

public static class ExpectObject
{
    public static IHandler Require(string key, Action<JsonParser> parse) =>
        new Handler(key, parse, isRequired: true);

    public static IHandler Allow(string key, Action<JsonParser> parse) =>
        new Handler(key, parse, isRequired: false);

    public static IHandler Compose(params IHandler[] handlers) =>
        new CompositeHandler(handlers);

    public interface IHandler
    {
        bool Accept(JsonParser parser, string key);
        void Complete();
    }

    private sealed class Handler(string ourKey, Action<JsonParser> parse, bool isRequired) :
        IHandler
    {
        private bool _didAccept;

        public bool Accept(JsonParser parser, string key)
        {
            if (key == ourKey) {
                if (_didAccept) {
                    throw new JsonException($"Duplicate key '{key}' in object");
                }

                parse(parser);
                _didAccept = true;
                return true;
            }
            else {
                return false;
            }
        }

        public void Complete()
        {
            if (isRequired && !_didAccept) {
                throw new JsonException($"Required key '{ourKey}' not found in object");
            }
        }
    }

    private sealed class CompositeHandler(IHandler[] handlers) : IHandler
    {
        public bool Accept(JsonParser parser, string key) =>
            handlers.Any(h => h.Accept(parser, key));

        public void Complete()
        {
            foreach (var h in handlers) h.Complete();
        }
    }
}
