namespace PureES.Core.ExpBuilders.WhenHandlers;

/// <summary>
///     Builds an expression which
///     left folds over an Array of Events
/// </summary>
/// <remarks>
/// Works with 3 methods : CreateWhen(event), UpdateWhen(Agg, event) and When(Agg, event)
/// CreateWhen for first event, UpdateWhen for every subsequent event
/// When for every event (after CreateWhen/UpdateWhen)
/// </remarks>
internal class FactoryExpBuilder
{
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
        var when = BuildWhen(aggregateType, serviceProvider, cancellationToken);
        return Expression.Call(method, 
            events, 
            createdWhen, 
            updatedWhen, 
            when,
            cancellationToken);
    }

    private static async ValueTask<T> Load<T>(IAsyncEnumerable<EventEnvelope> events,
        Func<EventEnvelope, ValueTask<T>> createWhen,
        Func<T, EventEnvelope, ValueTask<T>> updateWhen,
        Func<T, EventEnvelope, ValueTask<T>> when,
        CancellationToken ct)
    {
        if (events == null) throw new ArgumentNullException(nameof(events));
        await using var enumerator = events.GetAsyncEnumerator(ct);
        if (!await enumerator.MoveNextAsync())
            throw new ArgumentException("Provided events list is empty");
        //TODO: handle exceptions in when methods
        var aggregate = await createWhen(enumerator.Current);
        aggregate = await when(aggregate, enumerator.Current); //Call after every method
        while (await enumerator.MoveNextAsync())
        {
            ct.ThrowIfCancellationRequested();
            aggregate = await updateWhen(aggregate, enumerator.Current);
            aggregate = await when(aggregate, enumerator.Current); //Call after every method
        }
        return aggregate;
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
    
    private Expression BuildWhen(Type aggregateType,
        Expression serviceProvider, 
        Expression cancellationToken)
    {
        var current = Expression.Parameter(aggregateType);
        var envelope = Expression.Parameter(typeof(EventEnvelope));
        var builder = new WhenExpBuilder(_options);
        var exp = builder.BuildWhenExpression(aggregateType, current, envelope, serviceProvider, cancellationToken);
        //Results in Func<TAggregate, EventEnvelope, ValueTask<TAggregate>>
        var type = typeof(Func<,,>).MakeGenericType(aggregateType,
            typeof(EventEnvelope),
            typeof(ValueTask<>).MakeGenericType(aggregateType));
        return Expression.Lambda(type, exp, $"When<{aggregateType}>", true, new[] {current, envelope });
    }

    public static void ValidateWhenMethod(Type aggregateType, MethodInfo method)
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
            throw new InvalidOperationException($"{methodName}: must be public static method");
    }

    public void ValidateStronglyTypedEventEnvelope(ParameterInfo parameter)
    {
        //We are expecting EventEnvelope<TAny, TAny>
        if (parameter.IsNullable())
            throw new InvalidOperationException("EventEnvelope parameter must be non-nullable");
        var ex = new InvalidOperationException($"Invalid EventEnvelope parameter {parameter.ParameterType}");
        if (_options.IsStronglyTypedEventEnvelope != null)
        {
            if (!_options.IsStronglyTypedEventEnvelope(parameter.ParameterType))
                throw ex;
            return;
        }

        var args = parameter.ParameterType.GetGenericArguments();
        if (args.Length != 2) throw ex;
        if (typeof(EventEnvelope<,>).MakeGenericType(args) != parameter.ParameterType) throw ex;
    }

    public bool IsStronglyTypedEventEnvelope(Type type)
    {
        if (_options.IsStronglyTypedEventEnvelope != null)
            return _options.IsStronglyTypedEventEnvelope(type);
        if (type.GetGenericArguments().Length != 2)
            return false;
        return typeof(EventEnvelope<,>).MakeGenericType(type.GetGenericArguments()) == type;
    }
}