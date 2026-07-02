namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ClosedAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IsClosedTypeAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class UnionAttribute : Attribute;

public interface IUnion
{
    object? Value { get; }
}
