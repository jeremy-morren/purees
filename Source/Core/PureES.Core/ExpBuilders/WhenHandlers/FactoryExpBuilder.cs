namespace PureES.Core.ExpBuilders.WhenHandlers;

/// <summary>
///     Builds an expression which
///     left folds over an Array of Events
/// </summary>
/// <remarks>
///     Works with 2 methods : CreateWhen(event) and UpdateWhen(Agg, event)
///     (depending on if this is the first event)
/// </remarks>
internal class FactoryExpBuilder
{
    public const string MethodName = "When";
    private readonly CommandHandlerBuilderOptions _options;

    public FactoryExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression BuildExpression(Type aggregateType,
        Expression events,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (!typeof(IAsyncEnumerable<EventEnvelope>).IsAssignableFrom(events.Type))
            throw new InvalidOperationException("Invalid events expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new InvalidOperationException("Invalid CancellationToken expression");
        var method = typeof(FactoryExpBuilder).GetStaticMethod(nameof(Load)).MakeGenericMethod(aggregateType);
        var createdWhen = BuildCreatedWhen(aggregateType, serviceProvider, cancellationToken);
        var updatedWhen = BuildUpdatedWhen(aggregateType, serviceProvider, cancellationToken);
        return Expression.Call(method, events, createdWhen, updatedWhen, cancellationToken);
    }

    private static async ValueTask<LoadedAggregate<T>> Load<T>(IAsyncEnumerable<EventEnvelope> events,
        Func<EventEnvelope, ValueTask<T>> createWhen,
        Func<T, EventEnvelope, ValueTask<T>> updateWhen,
        CancellationToken ct)
    {
        if (events == null) throw new ArgumentNullException(nameof(events));
        await using var enumerator = events.GetAsyncEnumerator(ct);
        if (!await enumerator.MoveNextAsync())
            throw new ArgumentException("Provided events list is empty");
        //TODO: handle exceptions in when methods
        var aggregate = await createWhen(enumerator.Current);
        var revision = (ulong) 1; //After createWhen revision is 1
        while (await enumerator.MoveNextAsync())
        {
            ct.ThrowIfCancellationRequested();
            aggregate = await updateWhen(aggregate, enumerator.Current);
            ++revision;
        }
        return new LoadedAggregate<T>(aggregate, revision);
    }

    private Expression BuildCreatedWhen(Type aggregateType, Expression serviceProvider, Expression cancellationToken)
    {
        var envelope = Expression.Parameter(typeof(EventEnvelope));
        var builder = new CreatedWhenExpBuilder(_options);
        var exp = builder.BuildCreateExpression(aggregateType, envelope, serviceProvider, cancellationToken);
        //Results in Func<EventEnvelope, ValueTask<TAggregate>>
        var type = typeof(Func<,>).MakeGenericType(typeof(EventEnvelope),
            typeof(ValueTask<>).MakeGenericType(aggregateType));
        return Expression.Lambda(type, exp, $"CreatedWhen<{aggregateType}>", true, new[] {envelope });
    }

    private Expression BuildUpdatedWhen(Type aggregateType,
        Expression serviceProvider, 
        Expression cancellationToken)
    {
        var current = Expression.Parameter(aggregateType);
        var envelope = Expression.Parameter(typeof(EventEnvelope));
        var builder = new UpdatedWhenExpBuilder(_options);
        var exp = builder.BuildUpdateExpression(aggregateType, current, envelope, serviceProvider, cancellationToken);
        //Results in Func<TAggregate, EventEnvelope, ValueTask<TAggregate>>
        var type = typeof(Func<,,>).MakeGenericType(aggregateType,
            typeof(EventEnvelope),
            typeof(ValueTask<>).MakeGenericType(aggregateType));
        return Expression.Lambda(type, exp, $"UpdatedWhen<{aggregateType}>", true, new[] {current, envelope });
    }

    public static void ValidateWhen(Type aggregateType, MethodInfo method)
    {
        var methodName = $"{aggregateType}+{method}";
        //Should be T When(..)
        //Return type should be T, Task<T> or ValueTask<T>
        if (method.GetGenericArguments().Length != 0)
            throw new InvalidOperationException($"{methodName}: has generic parameters");
        if (method.ReturnType.IsTask(out var rt) || method.ReturnType.IsValueTask(out rt))
        {
            if (rt != aggregateType || rt.IsNullableValueType())
                throw new InvalidOperationException(
                    $"{methodName}: does not return non-nullable aggregate type");
        }
        else
        {
            if (method.ReturnType != aggregateType || method.ReturnType.IsNullableValueType())
                throw new InvalidOperationException(
                    $"When method does not return {methodName} non-nullable aggregate type");
        }

        if (!method.IsStatic)
            throw new InvalidOperationException($"{methodName}: is not static static");
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