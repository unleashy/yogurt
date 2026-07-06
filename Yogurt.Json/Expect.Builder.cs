namespace Yogurt.Json;

public static partial class Expect
{
    public sealed class Builder<T>
    {
        private readonly List<(string Key, Func<JsonParser, T, T> Parse, bool IsRequired)>
            _handlers = [];

        public Builder<T> Require(string key, Func<JsonParser, T, T> parse)
        {
            _handlers.Add((key, parse, IsRequired: true));
            return this;
        }

        public Builder<T> Require(string key, Action<JsonParser> parse)
        {
            return Require(key, (j, v) => { parse(j); return v; });
        }

        public Builder<T> Allow(string key, Func<JsonParser, T, T> parse)
        {
            _handlers.Add((key, parse, IsRequired: false));
            return this;
        }

        public Parser<T> Build()
        {
            var valueRef = new ValueRef<T>();
            var impl = Expect.Compose(
                _handlers
                    .Select(it => it.IsRequired
                        ? Expect.Require(it.Key, p => valueRef.Value = it.Parse(p, valueRef.Value))
                        : Expect.Allow(it.Key, p => valueRef.Value = it.Parse(p, valueRef.Value))
                    )
                    .ToArray()
            );

            return new Parser<T>(valueRef, impl);
        }
    }

    public sealed class Parser<T>
    {
        private readonly ValueRef<T> _valueRef;
        private readonly IHandler _impl;

        internal Parser(ValueRef<T> valueRef, IHandler impl)
        {
            _valueRef = valueRef;
            _impl = impl;
        }

        public T Parse(JsonParser parser, T initial)
        {
            _valueRef.Value = initial;
            parser.ExpectObject(_impl);
            parser.ExpectEnd();
            return _valueRef.Value;
        }
    }

    internal sealed class ValueRef<T>
    {
        public T Value { get; set; } = default!;
    }
}
