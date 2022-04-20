using System.Linq.Expressions;
using System.Reflection;

// ReSharper disable MemberCanBePrivate.Global

namespace PureES.Core.ExpBuilders;

/// <summary>
/// Builds expressions to
/// get stream id from command
/// </summary>
public class GetStreamIdExpBuilder
{
    private static readonly NullabilityInfoContext NullContext = new ();
    
    private readonly CommandHandlerOptions _options;

    public GetStreamIdExpBuilder(CommandHandlerOptions options)
    {
        _options = options;
    }

    public Expression GetStreamId(Expression cmdParam)
    {
        //As cmd.Id.StreamId
        var idProp = _options.GetAggregateIdProperty?.Invoke(cmdParam.Type)
                     ?? GetDefaultAggregateIdProperty(cmdParam.Type);
        var streamIdProp = _options.GetStreamIdProperty?.Invoke(idProp.PropertyType)
                           ?? GetDefaultStreamIdProperty(idProp.PropertyType);
        //TODO: Add null checking
        var getId = Expression.Property(cmdParam, idProp);
        return Expression.Property(getId, streamIdProp);
    }


    private static PropertyInfo GetDefaultAggregateIdProperty(Type commandType)
    {
        var props = commandType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return props.SingleOrDefault(p => p.Name == AggregateIdProperty)
               ?? throw new InvalidOperationException($"AggregateId property not found on command type {commandType}");
    }
    
    private static void ValidAggregateIdProperty(Type commandType, PropertyInfo prop)
    {
        //Ensure type is class/struct
        if (!prop.PropertyType.IsClass && !(prop.PropertyType.IsValueType && !prop.PropertyType.IsPrimitive))
            throw new InvalidOperationException($"AggregateId must be strongly-typed with StreamId property for command {commandType}");
        //Check nullability
        if ((prop.PropertyType.IsClass && NullContext.Create(prop).ReadState == NullabilityState.Nullable)
            || Nullable.GetUnderlyingType(prop.PropertyType) != null)
            throw new InvalidOperationException(
                $"AggregateId property on {commandType} has an invalid return type. Return type must be non-nullable class/struct");
        if (prop.GetGetMethod() == null)
            throw new InvalidOperationException($"Getter not found for property {prop} on {commandType}");
        if (!prop.GetGetMethod()!.IsPublic)
            throw new InvalidOperationException($"Getter not public for property {prop} on {commandType}");
    }

    private static PropertyInfo GetDefaultStreamIdProperty(Type idType)
    {
        var props = idType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return props.SingleOrDefault(p => p.Name == StreamIdProperty)
               ?? throw new InvalidOperationException($"StreamId property not found on strongly-typed ID {idType}");
    }

    private static void ValidateStreamIdProperty(Type idType, PropertyInfo prop)
    {
        if (prop.PropertyType != typeof(string) || NullContext.Create(prop).ReadState == NullabilityState.Nullable)
            throw new InvalidOperationException(
                $"StreamId property on {idType} has an invalid return type. Return type must be non-nullable string");
        if (prop.GetGetMethod() == null)
            throw new InvalidOperationException($"Getter not found for property {prop} on {idType}");
        if (!prop.GetGetMethod()!.IsPublic)
            throw new InvalidOperationException($"Getter not public for property {prop} on {idType}");
    }

    public const string AggregateIdProperty = "Id";
    public const string StreamIdProperty = "StreamId";
}