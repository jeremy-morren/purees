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
    private readonly CommandHandlerBuilderOptions _options;

    public FactoryExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression BuildExpression(Type aggregateType,
        Expression @events,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (!typeof(IAsyncEnumerable<EventEnvelope>).IsAssignableFrom(@events.Type))
            throw new InvalidOperationException("Invalid events expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new InvalidOperationException("Invalid CancellationToken expression");
        var method = typeof(FactoryExpBuilder).GetStaticMethod(nameof(Load)).MakeGenericMethod(aggregateType);
        var createdWhen = BuildCreatedWhen(aggregateType);
        var updatedWhen = BuildUpdatedWhen(aggregateType);
        return Expression.Call(method, @events, createdWhen, updatedWhen, serviceProvider, cancellationToken);
    }

    private static async ValueTask<LoadedAggregate<T>> Load<T>(IAsyncEnumerable<EventEnvelope> events,
        Func<EventEnvelope, IServiceProvider, CancellationToken, ValueTask<T>> createWhen, 
        Func<T, EventEnvelope, IServiceProvider, CancellationToken, ValueTask<T>> updateWhen,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        await using var enumerator = events.GetAsyncEnumerator(ct);
        if (!await enumerator.MoveNextAsync())
            throw new ArgumentException("Provided events list is empty");
        //TODO: handle exceptions in when methods
        var current = await createWhen(enumerator.Current, serviceProvider, ct);
        var revision = (ulong)1; //After createWhen version is 1
        while (await enumerator.MoveNextAsync())
        {
            ct.ThrowIfCancellationRequested();
            current = await updateWhen(current, enumerator.Current, serviceProvider, ct);
            ++revision;
        }
        return new LoadedAggregate<T>(current, revision);
    }

    private ConstantExpression BuildCreatedWhen(Type aggregateType)
    {
        var envelope = Expression.Parameter(typeof(EventEnvelope));
        var serviceProvider = Expression.Parameter(typeof(IServiceProvider));
        var token = Expression.Parameter(typeof(CancellationToken));
        var builder = new CreatedWhenExpBuilder(_options);
        var exp = builder.BuildCreateExpression(aggregateType, envelope, serviceProvider, token);
        //Results in Func<EventEnvelope, IServiceProvider, CancellationToken, ValueTask<T>>
        var type = typeof(Func<,,,>).MakeGenericType(typeof(EventEnvelope), 
            typeof(IServiceProvider),
            typeof(CancellationToken),
            typeof(ValueTask<>).MakeGenericType(aggregateType));
        var lambda = Expression.Lambda(type, exp, "CreatedWhen", true, 
            new[] {envelope, serviceProvider, token});
        return Expression.Constant(lambda.Compile(), type);
    }
    
    private ConstantExpression BuildUpdatedWhen(Type aggregateType)
    {
        var current = Expression.Parameter(aggregateType);
        var @event = Expression.Parameter(typeof(EventEnvelope));
        var serviceProvider = Expression.Parameter(typeof(IServiceProvider));
        var token = Expression.Parameter(typeof(CancellationToken));
        var builder = new UpdatedWhenExpBuilder(_options);
        var exp = builder.BuildUpdateExpression(aggregateType, current, @event, serviceProvider, token);
        //Results in Func<T, EventEnvelope, IServiceProvider, CancellationToken, ValueTask<T>>
        var type = typeof(Func<,,,,>).MakeGenericType(aggregateType, 
            typeof(EventEnvelope),  
            typeof(IServiceProvider),
            typeof(CancellationToken),
            typeof(ValueTask<>).MakeGenericType(aggregateType));
        var lambda = Expression.Lambda(type, exp, "UpdatedWhen", true, 
            new []{current, @event, serviceProvider, token});
        return Expression.Constant(lambda.Compile(), type);
    }

    public const string MethodName = "When";

    public static void ValidateWhen(Type aggregateType, MethodInfo method)
    {
        var methodName = $"{aggregateType}+{method}";
        //Should be T When(..)
        //Return type should be T, Task<T> or ValueTask<T>
        if (method.GetGenericArguments().Length != 0)
            throw new InvalidOperationException($"When method {methodName} has generic parameters");
        if (method.ReturnType.IsTask(out var rt) || method.ReturnType.IsValueTask(out rt))
        {
            if (rt != aggregateType || rt.IsNullable())
                throw new InvalidOperationException($"When method does not return {methodName} non-nullable aggregate type");
        }
        else
        {
            if (method.ReturnType != aggregateType || method.ReturnType.IsNullable())
                throw new InvalidOperationException($"When method does not return {methodName} non-nullable aggregate type");
        }
        if (!method.IsStatic)
            throw new InvalidOperationException($"When method {methodName} is not static static");
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