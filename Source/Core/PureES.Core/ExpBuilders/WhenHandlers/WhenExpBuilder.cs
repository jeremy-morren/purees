using Microsoft.AspNetCore.Mvc;

namespace PureES.Core.ExpBuilders.WhenHandlers;

/// <summary>
/// Builds expression to call 'When' method (i.e. non-generic, called for all events)
/// </summary>
internal class WhenExpBuilder
{
    private readonly PureESBuilderOptions _options;

    public WhenExpBuilder(PureESBuilderOptions options) => _options = options;

    public Expression BuildWhenExpression(Type aggregateType,
        Expression current,
        Expression envelope,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (envelope.Type != typeof(EventEnvelope))
            throw new ArgumentException("Invalid envelope expression");
        if (current.Type != aggregateType)
            throw new ArgumentException("Invalid current expression");
        if (serviceProvider.Type != typeof(IServiceProvider))
            throw new ArgumentException("Invalid service provider expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new ArgumentException("Invalid CancellationToken expression");

        var methods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => IsWhen(aggregateType, m))
            .ToList();

        return methods.Count switch
        {
            //Since we cannot await a method, we throw if there are more than one
            > 1 => throw new InvalidOperationException("Multiple When methods found"),
            
            //If no method found, return current as is
            0 => Expression.Call(ValueTaskHelpers.FromResultMethod.MakeGenericMethod(aggregateType), current),
            
            //Call method as is
            _ => BuildWhenHandler(aggregateType, methods[0], current, envelope, serviceProvider, cancellationToken)
        };
    }
    
    private Expression BuildWhenHandler(Type aggregateType,
        MethodInfo method,
        Expression current,
        Expression envelope,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (envelope.Type != typeof(EventEnvelope))
            throw new ArgumentException("Invalid envelope expression");
        if (current.Type != aggregateType)
            throw new ArgumentException("Invalid current expression");
        if (serviceProvider.Type != typeof(IServiceProvider))
            throw new ArgumentException("Invalid service provider expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new ArgumentException("Invalid CancellationToken expression");
        
        //Looks like T When(current, envelope)

        var parameters = method.GetParameters()
            .Select(p => p.ParameterType == typeof(CancellationToken)
                ? cancellationToken
                : p.GetCustomAttribute(typeof(FromServicesAttribute)) != null
                    ? new GetServiceExpBuilder(_options).GetRequiredService(serviceProvider, p.ParameterType)
                    : p.ParameterType == envelope.Type
                        ? envelope
                        : current)
            .ToArray();

        var invoke = Expression.Call(method, parameters);
        MethodInfo wrapper;
        if (method.ReturnType.IsTask(out _))
        {
            wrapper = ValueTaskHelpers.FromTaskMethod.MakeGenericMethod(aggregateType);
            return Expression.Call(wrapper, invoke);
        }

        if (method.ReturnType.IsValueTask(out _))
            //We don't need a wrapper
            return invoke;
        wrapper = ValueTaskHelpers.FromResultMethod.MakeGenericMethod(aggregateType);
        return Expression.Call(wrapper, invoke);
    }
    
    
    /// <summary>
    ///     Returns parameters that are not CancellationTokens or have [FromServices] attribute
    /// </summary>
    private static List<ParameterInfo> GetParams(MethodBase method) => method.GetParameters()
        .Where(p => p.ParameterType != typeof(CancellationToken) &&
                    p.GetCustomAttribute(typeof(FromServicesAttribute)) == null)
        .ToList();

    public void ValidateWhen(Type aggregateType, MethodInfo method)
    {
        //We are expecting a T When(T current, EventEnvelope<TAny, TAny> @event) method
        FactoryExpBuilder.ValidateWhenMethod(aggregateType, method);
        var parameters = GetParams(method);
        switch (parameters.Count)
        {
            case < 2:
                throw new InvalidOperationException($"When method {method} has too few parameters");
            case > 2:
                throw new InvalidOperationException($"When method {method} has too many parameters");
        }

        //Must be 2 parameters: 1 Aggregate, 1 Event
        if (parameters[0].ParameterType != aggregateType || parameters[0].IsNullable())
            throw new InvalidOperationException(
                "When method must take non-nullable aggregate as 1st parameter");
        if (parameters[1].ParameterType != typeof(EventEnvelope))
            throw new InvalidOperationException(
                "When method must take EventEnvelope as 2nd parameter");
    }

    public bool IsWhen(Type aggregateType, MethodInfo method)
    {
        var parameters = GetParams(method);

        //We are expecting a T When(T current, EventEnvelope<TAny, TAny> @event) method
        //Check if return type is aggregateType, takes 2 parameters
        //1 Parameter should be aggregateType
        //1 Parameter should be EventEnvelope

        if (method.ReturnType.IsTask(out var returnType))
        {
            if (returnType != aggregateType)
                return false;
        }
        else if (method.ReturnType.IsValueTask(out returnType))
        {
            if (returnType != aggregateType)
                return false;
        }
        else
        {
            if (method.ReturnType != aggregateType)
                return false;
        }

        return parameters.Count == 2
               && parameters.All(p => p.ParameterType == aggregateType
                   || p.ParameterType == typeof(EventEnvelope));
    }
}