using System.Collections.Frozen;

namespace Yogurt.JsonRpc;

public sealed partial class JsonRpcRouter
{
    public sealed class Builder
    {
        private Dictionary<string, IJsonRpcMethod> _methods = new();

        [PublicAPI]
        public IJsonRpcMethod this[string name] {
            get => _methods[name];
            set => _methods[name] = value;
        }

        [PublicAPI]
        public JsonRpcRouter Build() => new(_methods.ToFrozenDictionary());
    }
}
