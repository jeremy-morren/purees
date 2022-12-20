using Microsoft.AspNetCore.Mvc;

namespace PureES.Core.ExpBuilders.WhenHandlers;

internal class UpdatedWhenExpBuilder
{
    private readonly CommandHandlerBuilderOptions _options;

    public UpdatedWhenExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression BuildUpdateExpression(Type aggregateType,
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

        var updateMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => IsUpdatedWhen(aggregateType, m))
            .ToList();
        //TODO: validate multiple methods with identical parameters

        //We need an expression along the lines of
        //if (envelope.Event is EventType) return When(NewGenericEnvelope(envelope))
        var eventProperty =
            envelope.Type.GetProperty(nameof(EventEnvelope.Event), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Unable to get EventEnvelope.Event property");
        var @event = Expression.Variable(typeof(object));
        var envelopeVar = Expression.Variable(typeof(EventEnvelope));
        var currentVar = Expression.Variable(aggregateType);
        var expressions = new List<Expression>
        {
            //Assign to local variables
            Expression.Assign(envelopeVar, envelope),
            Expression.Assign(@event, Expression.Property(envelopeVar, eventProperty)),
            Expression.Assign(currentVar, current)
        };
        var returnTarget = Expression.Label(typeof(ValueTask<>).MakeGenericType(aggregateType));
        foreach (var m in updateMethods)
        {
            ValidateUpdatedWhen(aggregateType, m);
            var envelopeType = m.GetParameters()[1].ParameterType;
            var eventType = _options.GetEventType?.Invoke(envelopeType) ?? envelopeType.GetGenericArguments()[0];
            var check = Expression.TypeIs(@event, eventType);
            var call = BuildUpdatedWhen(aggregateType, m, currentVar, envelopeVar, serviceProvider, cancellationToken);
            var whole = Expression.IfThen(check, Expression.Return(returnTarget, call));
            expressions.Add(whole);
        }

        var @base = Expression.Call(ExceptionHelpers.ThrowUpdatedWhenBaseMethod,
            Expression.Constant(aggregateType), @event);
        expressions.Add(@base);
        //Note: We never actually get here, because we throw just above
        var @default = ValueTaskHelpers.DefaultMethod.MakeGenericMethod(aggregateType);
        expressions.Add(Expression.Label(returnTarget, Expression.Call(@default)));
        return Expression.Block(new[] {envelopeVar, @event, currentVar}, expressions);
    }

    public Expression BuildUpdatedWhen(Type aggregateType,
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
        ValidateUpdatedWhen(aggregateType, method);
        envelope = new NewEventEnvelopeExpBuilder(_options)
            .New(method.GetParameters()[1].ParameterType, envelope);
        //Looks like T When(current, envelope)

        var parameters = method.GetParameters()
            .Select(p => p.ParameterType == typeof(CancellationToken)
                ? cancellationToken
                : p.GetCustomAttribute(typeof(FromServicesAttribute)) != null
                    ? new GetServiceExpBuilder(_options).GetRequiredService(serviceProvider, p.ParameterType)
                    : IsEnvelope(p)
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
    private static List<ParameterInfo> GetEnvelopeParams(MethodBase method) => method.GetParameters()
        .Where(p => p.ParameterType != typeof(CancellationToken) &&
                    p.GetCustomAttribute(typeof(FromServicesAttribute)) == null)
        .ToList();

    public void ValidateUpdatedWhen(Type aggregateType, MethodInfo method)
    {
        //We are expecting a T When(T current, EventEnvelope<TAny, TAny> @event) method
        FactoryExpBuilder.ValidateWhen(aggregateType, method);
        var parameters = GetEnvelopeParams(method);
        switch (parameters.Count)
        {
            case < 2:
                throw new InvalidOperationException($"UpdateWhen method {method} has too few parameters");
            case > 2:
                throw new InvalidOperationException($"UpdateWhen method {method} has too many parameters");
        }

        //Must be 2 parameters: 1 Aggregate, 1 Event
        if (parameters[0].ParameterType != aggregateType || parameters[0].IsNullable())
            throw new InvalidOperationException(
                "UpdateWhen method must take non-nullable aggregate as 1st parameter");
        new FactoryExpBuilder(_options).ValidateEnvelope(parameters[1]);
    }

    public bool IsUpdatedWhen(Type aggregateType, MethodInfo method)
    {
        var parameters = GetEnvelopeParams(method);

        //We are expecting a T When(T current, EventEnvelope<TAny, TAny> @event) method
        //Check if return type is aggregateType, takes 2 parameters
        //Parameter 1 should be aggregateType
        //Parameter 2 should be EventEnvelope

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
               && parameters[0].ParameterType == aggregateType
               && IsEnvelope(parameters[1]);
    }

    private bool IsEnvelope(ParameterInfo parameter) =>
        _options.IsEventEnvelope?.Invoke(parameter.ParameterType)
        ?? new FactoryExpBuilder(_options).IsEnvelope(parameter.ParameterType);
}