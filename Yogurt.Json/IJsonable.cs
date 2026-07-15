namespace Yogurt.Json;

public interface IJsonable<out TSelf> : IJsonParseable<TSelf>, IJsonWriteable
    where TSelf : IJsonable<TSelf>;
