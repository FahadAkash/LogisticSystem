namespace LogisticServer.Infrastructure.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireIdempotencyKeyAttribute : Attribute
{
}

