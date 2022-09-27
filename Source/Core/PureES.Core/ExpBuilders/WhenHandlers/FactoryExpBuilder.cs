using System.Linq.Expressions;
using System.Reflection;

namespace PureES.Core.ExpBuilders.WhenHandlers;

/// <summary>
/// Builds an expression which
/// left folds over an Array of Events
/// </summary>
/// <remarks>
/// Works with 2 methods : CreateWhen(event) and UpdateWhen(Agg, event)
/// (depending on if this is the first event)
/// </remarks>
internal class FactoryExpBuilder
{
    private readonly CommandHandlerOptions _options;

    public FactoryExpBuilder(CommandHandlerOptions options) => _options = options;

    public Expression BuildExpression(Type aggregateType,
        Expression @events,
        Expression cancellationToken)
    {
        if (!typeof(IAsyncEnumerable<EventEnvelope>).IsAssignableFrom(@events.Type))
            throw new InvalidOperationException("Invalid events expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new InvalidOperationException("Invalid CancellationToken expression");
        var method = typeof(FactoryExpBuilder).GetMethod(nameof(Load), BindingFlags.NonPublic | BindingFlags.Static)
                         ?.MakeGenericMethod(aggregateType)
                     ?? throw new InvalidOperationException($"Unable to get inner {nameof(Load)} method");
        var createdWhen = BuildCreatedWhen(aggregateType);
        var updatedWhen = BuildUpdatedWhen(aggregateType);
        return Expression.Call(method, @events, createdWhen, updatedWhen, cancellationToken);
    }

    private static async Task<LoadedAggregate<T>> Load<T>(IAsyncEnumerable<EventEnvelope> @events,
        Func<EventEnvelope, T> createWhen, 
        Func<T, EventEnvelope, T> updateWhen,
        CancellationToken ct)
    {
        await using var enumerator = @events.GetAsyncEnumerator(ct);
        if (!await enumerator.MoveNextAsync())
            throw new ArgumentException("Provided events list is empty");
        //TODO: handle exceptions in when methods
        var current = createWhen(enumerator.Current);
        var version = (ulong)0; //After createWhen version is 0
        while (await enumerator.MoveNextAsync())
        {
            current = updateWhen(current, enumerator.Current);
            ++version;
        }
        return new LoadedAggregate<T>(current, version);
    }

    private ConstantExpression BuildCreatedWhen(Type aggregateType)
    {
        var parameter = Expression.Parameter(typeof(EventEnvelope));
        var builder = new CreatedWhenExpBuilder(_options);
        var exp = builder.BuildCreateExpression(aggregateType, parameter);
        var type = typeof(Func<,>).MakeGenericType(typeof(EventEnvelope), aggregateType);
        var lambda = Expression.Lambda(type, exp, "CreatedWhen", true, new[] {parameter});
        return Expression.Constant(lambda.Compile(), type);
    }
    
    private ConstantExpression BuildUpdatedWhen(Type aggregateType)
    {
        var current = Expression.Parameter(aggregateType);
        var @event = Expression.Parameter(typeof(EventEnvelope));
        var builder = new UpdatedWhenExpBuilder(_options);
        var exp = builder.BuildUpdateExpression(aggregateType, current, @event);
        var type = typeof(Func<,,>).MakeGenericType(aggregateType, typeof(EventEnvelope), aggregateType);
        var lambda = Expression.Lambda(type, exp, "UpdatedWhen", true, new []{current, @event});
        return Expression.Constant(lambda.Compile(), type);
    }

    public const string MethodName = "When";

    public static void ValidateWhen(Type aggregateType, MethodInfo method)
    {
        //Should be T When(..)
        if (method.GetGenericArguments().Length != 0)
            throw new InvalidOperationException("When method cannot have generic parameters");
        if (method.ReturnType != aggregateType || method.ReturnParameter.IsNullable())
            throw new InvalidOperationException("When method must return non-nullable aggregate type");
        if (!method.IsStatic)
            throw new InvalidOperationException("When method must be static");
    }

    public void ValidateEnvelope(ParameterInfo parameter)
    {
        //We are expecting EventEnvelope<TAny, TAny>
        if (parameter.IsNullable())
            throw new InvalidOperationException("EventEnvelope parameter must be non-nullable");
        var ex = new InvalidOperationException($"Invalid EventEnvelope parameter {parameter.ParameterType}");
        if (_options.IsEventEnvelope != null)
        {
            if (!_options.IsEventEnvelope(parameter.ParameterType))
                throw ex;
            return;
        }
        var args = parameter.ParameterType.GetGenericArguments();
        if (args.Length != 2) throw ex;
        if (typeof(EventEnvelope<,>).MakeGenericType(args) != parameter.ParameterType) throw ex;
    }

    public bool IsEnvelope(Type type)
    {
        if (_options.IsEventEnvelope != null)
            return _options.IsEventEnvelope(type);
        if (type.GetGenericArguments().Length != 2)
            return false;
        return typeof(EventEnvelope<,>).MakeGenericType(type.GetGenericArguments()) == type;
    }
}