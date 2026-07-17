namespace Yogurt.Json;

public interface IJsonable<out TSelf> : IJsonParseable<TSelf>, IJsonBuildable
    where TSelf : IJsonable<TSelf>;
