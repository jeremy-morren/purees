using Microsoft.AspNetCore.Mvc;

namespace PureES.Core.ExpBuilders.EventHandlers;

internal class EventHandlerExpBuilder
{
    private readonly PureESBuilderOptions _options;

    public EventHandlerExpBuilder(PureESBuilderOptions options) => _options = options;

    /// <summary>
    /// Builds a delegate for the provided method
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public Expression<Func<EventEnvelope, IServiceProvider, CancellationToken, Task>> BuildEventHandlerFactory(MethodInfo method)
    {
        if (method.GetCustomAttribute(typeof(EventHandlerAttribute)) == null)
            throw new ArgumentException($"Method {GetFullName(method)} not decorated with {nameof(EventHandlerAttribute)}");
        
        //Validate method parameters
        if (!method.GetParameters().Any(p => IsEventEnvelope(p.ParameterType)))
            throw new ArgumentException($"EventEnvelope parameter not found on {GetFullName(method)}");

        if (method.ReturnType != typeof(void)
            && (method.ReturnType.IsValueTask(out var rt) || method.ReturnType.IsTask(out rt)) 
            && rt != null)
            throw new ArgumentException($"{GetFullName(method)}: Event handlers must return void, non-generic Task or non-generic ValueTask");

        if (method.ContainsGenericParameters)
            throw new ArgumentException($"{GetFullName(method)}: Event handlers cannot have generic parameters");
        
        try
        {
            return method.IsStatic ? BuildStatic(method) : BuildInstance(method);
        }
        catch (Exception e)
        {
            throw new Exception($"An error occurred building event handler for {GetFullName(method)}", e);
        }
    }
    
    /// <summary>
    /// Build call to an instance method
    /// </summary>
    private Expression<Func<EventEnvelope, IServiceProvider, CancellationToken, Task>> BuildInstance(MethodInfo method)
    {
        var envelope = Expression.Parameter(typeof(EventEnvelope)); //non-generic input
        var serviceProvider = Expression.Parameter(typeof(IServiceProvider));
        var cancellationToken = Expression.Parameter(typeof(CancellationToken));

        var getSvc = new GetServiceExpBuilder(_options);
        
        //Looks like serviceProvider.GetRequiredService<Class>().Method(@event, params)

        var type = method.DeclaringType ??
                   throw new InvalidOperationException($"Declaring type on method {method} not found");

        var instance = getSvc.GetRequiredService(serviceProvider, type);

        var arguments = new List<Expression>();
        foreach (var param in method.GetParameters())
            if (IsEventEnvelope(param.ParameterType))
                arguments.Add(new NewEventEnvelopeExpBuilder(_options).New(param.ParameterType, envelope));
            else if (param.GetCustomAttribute(typeof(FromServicesAttribute)) != null)
                arguments.Add(getSvc.GetRequiredService(serviceProvider, param.ParameterType));
            else if (param.ParameterType == typeof(CancellationToken))
                arguments.Add(cancellationToken);
            else
                throw new InvalidOperationException(
                    $"Unknown parameter on EventHandler {GetFullName(method)}: {param.Name}");

        var call = WrapResponse(method, Expression.Call(instance, method, arguments));

        return Expression.Lambda<Func<EventEnvelope, IServiceProvider, CancellationToken, Task>>(
            call, envelope, serviceProvider, cancellationToken);
    }
    
    /// <summary>
    /// Build call to a static method
    /// </summary>
    private Expression<Func<EventEnvelope, IServiceProvider, CancellationToken, Task>> BuildStatic(MethodInfo method)
    {
        var envelope = Expression.Parameter(typeof(EventEnvelope)); //non-generic input
        var serviceProvider = Expression.Parameter(typeof(IServiceProvider));
        var cancellationToken = Expression.Parameter(typeof(CancellationToken));
        
        var arguments = new List<Expression>();
        foreach (var param in method.GetParameters())
            if (IsEventEnvelope(param.ParameterType))
                arguments.Add(new NewEventEnvelopeExpBuilder(_options).New(param.ParameterType, envelope));
            else if (param.GetCustomAttribute(typeof(FromServicesAttribute)) != null)
                arguments.Add(new GetServiceExpBuilder(_options).GetRequiredService(serviceProvider, param.ParameterType));
            else if (param.ParameterType == typeof(CancellationToken))
                arguments.Add(cancellationToken);
            else
                throw new InvalidOperationException(
                    $"Unknown parameter on EventHandler {GetFullName(method)}: {param.Name}");

        var call = WrapResponse(method, Expression.Call(method, arguments));
        
        return Expression.Lambda<Func<EventEnvelope, IServiceProvider, CancellationToken, Task>>(
            call, envelope, serviceProvider, cancellationToken);
    }

    private static Expression WrapResponse(MethodInfo method, Expression call)
    {
        //Response wrapper: Wrap in suitable result

        if (method.ReturnType == typeof(void))
            //Call method, return Task.CompletedTask
            return Expression.Block(call, CompletedTask);

        if (method.ReturnType.IsValueTask(out _))
            //Convert ValueTask to task
            return Expression.Call(ValueTaskHelpers.ToTaskVoidMethod, call);
        
        if (method.ReturnType.IsTask(out _))
            //Method returns Task, no wrapper necessary
            return call;
        
        throw new InvalidOperationException("Unknown EventHandler return type");
    }

    public bool IsEventEnvelope(Type type)
    {
        if (_options.IsStronglyTypedEventEnvelope != null
            && _options.IsStronglyTypedEventEnvelope(type))
            return true;

        return type.GetGenericArguments().Length == 2 &&
               typeof(EventEnvelope<,>).MakeGenericType(type.GetGenericArguments()) == type;
    }

    private static string GetFullName(MemberInfo method) => $"{method.DeclaringType?.FullName}.{method.Name}";
    
    private static Expression CompletedTask => Expression.Property(null,
        typeof(Task).GetProperty(nameof(Task.CompletedTask), BindingFlags.Static | BindingFlags.Public)!);
}