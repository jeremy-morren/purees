

// ReSharper disable MemberCanBePrivate.Global

namespace PureES.Core.ExpBuilders;

/// <summary>
///     Builds expressions to
///     get stream id from command
/// </summary>
internal class GetStreamIdExpBuilder
{
    public const string AggregateIdProperty = "Id";
    public const string StreamIdProperty = "StreamId";
    private static readonly NullabilityInfoContext NullContext = new();

    private readonly CommandHandlerBuilderOptions _options;

    public GetStreamIdExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression GetStreamId(Expression cmdParam)
    {
        //We first check to see if the 'GetStreamId' attribute exists
        //If it does, then we return a constant
        var attr = cmdParam.Type.GetCustomAttribute<StreamIdAttribute>();
        if (attr != null)
            return Expression.Constant(attr.StreamId);

        //As cmd.Id.StreamId
        var idProp = _options.GetAggregateIdProperty?.Invoke(cmdParam.Type)
                     ?? GetDefaultAggregateIdProperty(cmdParam.Type);

        var getId = WithNullCheck(Expression.Property(cmdParam, idProp), idProp.Name);
        
        //If StreamId is a string, then we return that
        if (idProp.PropertyType == typeof(string))
            return getId;

        var streamIdProp = _options.GetStreamIdProperty?.Invoke(idProp.PropertyType)
                           ?? GetDefaultStreamIdProperty(idProp.PropertyType);

        return WithNullCheck(Expression.Property(getId, streamIdProp), $"{idProp.Name}.{streamIdProp.Name}");
    }

    private static Expression WithNullCheck(Expression src, string name)
    {
        if (!src.Type.IsNullable())
            return src;
        var @null = Expression.Constant(null, src.Type);
        var ex = new NullReferenceException($"Unable to get StreamId: {name} is null");
        var nullCheck = Expression.IfThen(
            Expression.ReferenceEqual(src, @null),
            Expression.Throw(Expression.Constant(ex)));
        return Expression.Block(nullCheck, src);
    }

    private static PropertyInfo GetDefaultAggregateIdProperty(Type commandType)
    {
        var props = commandType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return props.SingleOrDefault(p => p.Name.Equals(AggregateIdProperty, StringComparison.InvariantCultureIgnoreCase))
               ?? throw new InvalidOperationException($"'{AggregateIdProperty}' property not found on command type {commandType}");
    }

    private static PropertyInfo GetDefaultStreamIdProperty(Type idType)
    {
        var props = idType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return props.SingleOrDefault(p => p.Name == StreamIdProperty)
               ?? throw new InvalidOperationException($"StreamId property not found on strongly-typed ID {idType}");
    }
}